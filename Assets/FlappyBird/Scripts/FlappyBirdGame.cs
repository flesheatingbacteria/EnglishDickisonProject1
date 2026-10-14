using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FlappyBird
{
    public sealed class FlappyBirdGame : MonoBehaviour
    {
        public enum PlayState { Ready, Playing, GameOver, AwaitingFlap, Complete }
        [Header("Bird")]
        [Min(0.1f)] public float gravityScale = 2.2f;
        [Min(0.1f)] public float flapSpeed = 6.5f;
        [Header("Pipes")]
        [Min(0.1f)] public float pipeSpeed = 2.7f;
        [Min(0.1f)] public float spawnInterval = 1.65f;
        [Range(1.5f, 4f)] public float pipeGap = 2.8f;
        [Min(0.1f)] public float pipeWidth = 0.85f;
        [Header("Arena")]
        [Min(4)] public float cameraHalfHeight = 5f;
        public PlayState State { get; private set; }
        public BirdController Bird { get; private set; }
        public float HalfWidth { get; private set; }
        public int Score { get; private set; }
        public int BestScore { get; private set; }

        private bool showingPoem;
        private float timeScaleBeforePoem = 1f;
        private const string CreatorCredit = "Created by Alvin, Shridhar, Ishaan";
        // Public-domain text: Academy of American Poets, poem 254 (Johnson).
        private const string PoemText =
            "Hope is the thing with feathers\n"
            + "That perches in the soul,\n"
            + "And sings the tune without the words,\n"
            + "And never stops at all,\n\n"
            + "And sweetest in the gale is heard;\n"
            + "And sore must be the storm\n"
            + "That could abash the little bird\n"
            + "That kept so many warm.\n\n"
            + "I've heard it in the chillest land,\n"
            + "And on the strangest sea;\n"
            + "Yet, never, in extremity,\n"
            + "It asked a crumb of me.";

        private const string BestKey = "FlappyBird.BestScore";
        public int LevelIndex { get; private set; }
        public const int ObstaclesPerLevel = 8;
        private static readonly string[] LevelNames = { "The Gale", "Keeping Hope Warm", "At Sea" };
        private static readonly string[] LevelLines = {
            "And sweetest - in the Gale - is heard -",
            "That kept so many warm -",
            "And on the strangest Sea -"
        };
        private Camera arenaCamera;
        private DickinsonMusic music;
        private Transform scenery;
        private SpriteRenderer ground, grass;
        private Transform pipes;
        private Transform nest;
        private FollowingStorm storm;
        private Vector3 nestStartPosition;
        private Sprite square;
        private Texture2D texture;
        private float spawnTimer;
        private float resumeAt;
        private GUIStyle titleStyle, textStyle, scoreStyle;
        private float Floor => -cameraHalfHeight + 0.7f;

        // Open SampleScene and press Play. Other scenes can opt in with this component.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (SceneManager.GetActiveScene().name == "SampleScene" && FindAnyObjectByType<FlappyBirdGame>() == null)
                new GameObject("Flappy Bird Game").AddComponent<FlappyBirdGame>();
        }

        private void Awake()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                camera.tag = "MainCamera";
            }
            arenaCamera = camera;
            camera.orthographic = true;
            camera.orthographicSize = cameraHalfHeight;
            camera.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.34f, 0.73f, 0.84f);
            // Keep the arena usable in very narrow windows too.
            HalfWidth = Mathf.Max(3.2f, cameraHalfHeight * camera.aspect);
            if (cameraHalfHeight * camera.aspect < 3.2f)
                camera.orthographicSize = 3.2f / camera.aspect;

            texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            square = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
            pipes = new GameObject("Pipes").transform;
            pipes.SetParent(transform);

            ground = Block("Ground", transform, new Vector2(0, Floor - 0.35f), new Vector2(HalfWidth * 2 + 4, 0.7f), new Color(0.8f, 0.68f, 0.42f), true).GetComponent<SpriteRenderer>();
            grass = Block("Grass", transform, new Vector2(0, Floor - 0.04f), new Vector2(HalfWidth * 2 + 4, 0.12f), new Color(0.5f, 0.77f, 0.24f), false).GetComponent<SpriteRenderer>();
            Block("Ceiling", transform, new Vector2(0, cameraHalfHeight + 0.5f), new Vector2(HalfWidth * 2 + 4, 1), Color.clear, true);
            CreateNest();

            GameObject bird = new GameObject("Bird", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(BirdController));
            bird.transform.SetParent(transform);
            SpriteRenderer renderer = bird.GetComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = new Color(1, 0.83f, 0.18f);
            renderer.sortingOrder = 5;
            // Scale visuals independently so the collision radius stays in world units.
            renderer.enabled = false;
            Block("Body", bird.transform, Vector2.zero, new Vector2(0.58f, 0.44f), renderer.color, false, 5);
            Block("Beak", bird.transform, new Vector2(0.34f, -0.04f), new Vector2(0.22f, 0.13f), new Color(1, 0.42f, 0.1f), false, 6);
            Block("Eye", bird.transform, new Vector2(0.16f, 0.1f), new Vector2(0.08f, 0.08f), Color.black, false, 7);
            Bird = bird.GetComponent<BirdController>();
            Bird.Initialize(this, gravityScale, flapSpeed);
            GameObject stormObject = new GameObject("Following Storm", typeof(FollowingStorm));
            stormObject.transform.SetParent(transform, false);
            storm = stormObject.GetComponent<FollowingStorm>();
            storm.Initialize(this, square);
            music = gameObject.AddComponent<DickinsonMusic>();
            music.Initialize();
            gameObject.AddComponent<BirdThoughtNotes>().Initialize(Bird.transform, music, square);
            BestScore = PlayerPrefs.GetInt(BestKey, 0);
            ApplyLevel();
            ResetRun();
            SetPoemVisible(true);
        }

        private GameObject Block(string label, Transform parent, Vector2 position, Vector2 size, Color color, bool solid, int order = 0)
        {
            GameObject block = new GameObject(label, typeof(SpriteRenderer));
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = new Vector3(size.x, size.y, 1);
            SpriteRenderer renderer = block.GetComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = color;
            renderer.sortingOrder = order;
            if (solid) block.AddComponent<BoxCollider2D>();
            return block;
        }

        private void CreateNest()
        {
            nest = new GameObject("Starting Nest").transform;
            nest.SetParent(transform, false);
            nestStartPosition = new Vector3(-1.5f, -0.32f, 0);
            nest.localPosition = nestStartPosition;
            Color wood = new Color(0.38f, 0.21f, 0.1f);
            Color straw = new Color(0.73f, 0.48f, 0.23f);
            // A branch supports the bowl; all pieces are decorative so takeoff is unobstructed.
            float branchLength = HalfWidth - 1.5f + 0.6f;
            Block("Branch", nest, new Vector2(-branchLength / 2 + 0.2f, -0.25f), new Vector2(branchLength, 0.15f), wood, false, 1);
            for (int layer = 0; layer < 3; layer++)
            {
                for (int twig = 0; twig < 9; twig++)
                {
                    float x = (twig - 4) * 0.12f;
                    float y = x * x * 0.75f - 0.14f + layer * 0.065f;
                    GameObject stick = Block("Nest Twig", nest, new Vector2(x, y), new Vector2(0.29f, 0.06f),
                        (twig + layer) % 2 == 0 ? straw : wood, false, 6);
                    stick.transform.localRotation = Quaternion.Euler(0, 0, x * 55 + (layer - 1) * 12);
                }
            }
        }

        private static bool FlapPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)
                || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
#else
            return false;
#endif
        }

        private static bool PointerOverControls()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) return false;
            Vector2 pointer = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed
                ? Touchscreen.current.primaryTouch.position.ReadValue()
                : Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
            if (Input.GetKeyDown(KeyCode.Space)) return false;
            Vector2 pointer = Input.mousePosition;
#endif
            float scale = Mathf.Min(Screen.width / 640f, Screen.height / 800f);
            float top = (Screen.height - 800 * scale) / 2;
            float left = (Screen.width - 640 * scale) / 2;
            return pointer.x >= left && pointer.x <= left + 640 * scale
                && Screen.height - pointer.y >= top + 685 * scale;
        }

        private void Update()
        {
            if (showingPoem) return;
            AnimateScenery();
            if (State == PlayState.Complete) return;
            if (State == PlayState.GameOver)
            {
                if (Time.unscaledTime >= resumeAt) State = PlayState.AwaitingFlap;
                // Keep the world frozen until a fresh flap after the one-second pause.
                return;
            }
            if (FlapPressed() && !PointerOverControls())
            {
                if (State == PlayState.AwaitingFlap)
                {
                    Bird.BeginRecovery();
                    State = PlayState.Playing;
                }
                if (State == PlayState.Ready) State = PlayState.Playing;
                Bird.Flap();
            }
            if (State != PlayState.Playing) return;
            if (nest.gameObject.activeSelf)
            {
                nest.position += Vector3.left * pipeSpeed * Time.deltaTime;
                if (nest.position.x < -HalfWidth - 1) nest.gameObject.SetActive(false);
            }
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0)
            {
                SpawnPipe();
                spawnTimer = Mathf.Max(0.1f, spawnInterval);
            }
            if (!Bird.Recovering && (Bird.transform.position.y < Floor || Bird.transform.position.y > cameraHalfHeight)) EndRun();
        }

        private void SpawnPipe()
        {
            float gap = Mathf.Clamp(pipeGap, 1, cameraHalfHeight - Floor - 1);
            float middle = Random.Range(Floor + gap / 2 + 0.5f, cameraHalfHeight - gap / 2 - 0.5f);
            GameObject pair = new GameObject("Pipe Pair", typeof(PipePair));
            pair.transform.SetParent(pipes, false);
            pair.transform.position = new Vector3(HalfWidth + pipeWidth, 0, 0);
            pair.GetComponent<PipePair>().Initialize(this);
            float bottomEdge = middle - gap / 2;
            float topEdge = middle + gap / 2;
            Color green = LevelIndex == 0 ? new Color(0.38f, 0.48f, 0.62f)
                : LevelIndex == 1 ? new Color(0.65f, 0.23f, 0.12f) : new Color(0.15f, 0.5f, 0.52f);
            Block("Lower Pipe", pair.transform, new Vector2(0, (Floor + bottomEdge) / 2), new Vector2(pipeWidth, bottomEdge - Floor), green, true, 2);
            Block("Upper Pipe", pair.transform, new Vector2(0, (cameraHalfHeight + topEdge) / 2), new Vector2(pipeWidth, cameraHalfHeight - topEdge), green, true, 2);
            Block("Lower Rim", pair.transform, new Vector2(0, bottomEdge - 0.12f), new Vector2(pipeWidth + 0.14f, 0.24f), green, true, 3);
            Block("Upper Rim", pair.transform, new Vector2(0, topEdge + 0.12f), new Vector2(pipeWidth + 0.14f, 0.24f), green, true, 3);
        }

        public void AddScore()
        {
            if (State != PlayState.Playing) return;
            Score++;
            if (Score % ObstaclesPerLevel != 0) return;
            if (LevelIndex < 2)
            {
                LevelIndex++;
                ClearPipes();
                spawnTimer = 1.2f;
                ApplyLevel();
            }
            else
            {
                EndRun();
                State = PlayState.Complete;
            }
        }

        private void ClearPipes()
        {
            foreach (Transform pipe in pipes)
            {
                pipe.gameObject.SetActive(false);
                Destroy(pipe.gameObject);
            }
        }

        public void SelectLevel(int index)
        {
            LevelIndex = Mathf.Clamp(index, 0, 2);
            ResetRun();
            ApplyLevel();
        }

        private void ApplyLevel()
        {
            arenaCamera.backgroundColor = LevelIndex == 0 ? new Color(0.24f, 0.34f, 0.48f)
                : LevelIndex == 1 ? new Color(0.4f, 0.19f, 0.16f) : new Color(0.1f, 0.35f, 0.5f);
            ground.color = LevelIndex == 1 ? new Color(0.32f, 0.13f, 0.1f) : new Color(0.28f, 0.36f, 0.4f);
            grass.color = LevelIndex == 0 ? new Color(0.56f, 0.67f, 0.72f)
                : LevelIndex == 1 ? new Color(1f, 0.5f, 0.17f) : new Color(0.38f, 0.78f, 0.86f);
            storm.gameObject.SetActive(LevelIndex == 0);
            if (scenery != null) { scenery.gameObject.SetActive(false); Destroy(scenery.gameObject); }
            scenery = new GameObject("Level Imagery").transform;
            scenery.SetParent(transform, false);
            for (int i = 0; i < 24; i++)
            {
                Vector2 size = LevelIndex == 0 ? new Vector2(0.65f, 0.025f)
                    : LevelIndex == 1 ? new Vector2(0.06f, 0.13f) : new Vector2(0.7f, 0.07f);
                Color color = LevelIndex == 0 ? new Color(0.8f, 0.9f, 1f, 0.32f)
                    : LevelIndex == 1 ? new Color(1f, 0.65f, 0.24f, 0.65f) : new Color(0.65f, 0.91f, 1f, 0.6f);
                Block(LevelIndex == 0 ? "Wind" : LevelIndex == 1 ? "Ember" : "Wave", scenery,
                    Vector2.zero, size, color, false, -2);
            }
            if (LevelIndex == 1)
                for (int i = 0; i < 14; i++)
                {
                    GameObject flame = Block("Hearth Flame", scenery, new Vector2(-HalfWidth + i * HalfWidth * 2 / 13, Floor + 0.15f),
                        new Vector2(0.16f, 0.4f), new Color(1, 0.4f, 0.1f, 0.6f), false, -1);
                    flame.transform.localRotation = Quaternion.Euler(0, 0, 18);
                }
            music.SetLevel(LevelIndex);
        }

        private void AnimateScenery()
        {
            if (scenery == null) return;
            float time = Time.time;
            for (int i = 0; i < scenery.childCount; i++)
            {
                if (LevelIndex == 1 && i >= 24)
                {
                    Transform flame = scenery.GetChild(i);
                    float flicker = 0.28f + 0.15f * Mathf.Sin(time * 5 + i * 1.7f);
                    flame.localScale = new Vector3(0.16f, flicker, 1);
                    flame.localPosition = new Vector3(-HalfWidth + (i - 24) * HalfWidth * 2 / 13, Floor + flicker / 2, 0);
                    continue;
                }
                float x = Mathf.Repeat(i * 1.73f - time * (LevelIndex == 0 ? 2.2f : 0.5f), HalfWidth * 2 + 2) - HalfWidth - 1;
                float y = LevelIndex == 0 ? Floor + 1 + Mathf.Repeat(i * 0.71f, cameraHalfHeight - Floor - 1.5f)
                    : LevelIndex == 1 ? Floor + Mathf.Repeat(time * 0.5f + i * 0.43f, cameraHalfHeight - Floor)
                    : Floor + 0.2f + (i % 3) * 0.2f + Mathf.Sin(time * 1.5f + i) * 0.1f;
                scenery.GetChild(i).localPosition = new Vector3(x, y, 0);
            }
        }

        public void EndRun()
        {
            if (State != PlayState.Playing) return;
            State = PlayState.GameOver;
            Bird.Stop();
            resumeAt = Time.unscaledTime + 1f;
            if (Score > BestScore)
            {
                BestScore = Score;
                PlayerPrefs.SetInt(BestKey, BestScore);
                PlayerPrefs.Save();
            }
        }

        public void ResetRun()
        {
            State = PlayState.Ready;
            ClearPipes();
            Score = LevelIndex * ObstaclesPerLevel;
            spawnTimer = 0.8f;
            Bird.ResetBird();
            storm.ResetStorm();
            nest.localPosition = nestStartPosition;
            nest.gameObject.SetActive(true);
        }

        private void SetPoemVisible(bool visible)
        {
            if (showingPoem == visible) return;
            showingPoem = visible;
            if (visible)
            {
                timeScaleBeforePoem = Time.timeScale;
                Time.timeScale = 0;
            }
            else Time.timeScale = timeScaleBeforePoem;
        }

        private void OnGUI()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 30,
                    fontStyle = FontStyle.Bold,
                    wordWrap = true
                };
                textStyle = new GUIStyle(titleStyle) { fontSize = 18, fontStyle = FontStyle.Normal };
                scoreStyle = new GUIStyle(textStyle) { fontSize = 16 };
                titleStyle.normal.textColor = textStyle.normal.textColor = scoreStyle.normal.textColor = Color.white;
            }
            float scale = Mathf.Min(Screen.width / 640f, Screen.height / 800f);
            Matrix4x4 previous = GUI.matrix;
            Color previousColor = GUI.color;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 640 * scale) / 2,
                (Screen.height - 800 * scale) / 2), Quaternion.identity, Vector3.one * scale);
            GUI.color = Color.white;

            if (showingPoem)
            {
                DrawPanel(new Rect(30, 30, 580, 740), new Color(0.035f, 0.07f, 0.12f, 0.97f));
                GUI.Label(new Rect(50, 52, 540, 72), "Hope is the thing with feathers", titleStyle);
                GUI.Label(new Rect(50, 126, 540, 30), "Emily Dickinson", textStyle);
                GUI.Label(new Rect(50, 158, 540, 30), "Poem 254 (Johnson) / 314 (Franklin)", scoreStyle);
                GUI.Label(new Rect(50, 194, 540, 32), CreatorCredit, textStyle);
                GUI.Label(new Rect(65, 242, 510, 370), PoemText, textStyle);
                GUI.Label(new Rect(50, 626, 540, 44), "Public-domain text: Academy of American Poets", scoreStyle);
                GUIStyle button = new GUIStyle(GUI.skin.button) { fontSize = 20 };
                if (GUI.Button(new Rect(185, 694, 270, 48), State == PlayState.Ready ? "Begin the journey" : "Return to game", button))
                    SetPoemVisible(false);
            }
            else
            {
                DrawPanel(new Rect(40, 20, 560, 240), new Color(0.035f, 0.07f, 0.12f, 0.82f));
                GUI.Label(new Rect(55, 30, 530, 50), $"{LevelIndex + 1}. {LevelNames[LevelIndex]}", titleStyle);
                GUI.Label(new Rect(55, 84, 530, 28), "Hope is the thing with feathers", textStyle);
                GUI.Label(new Rect(55, 116, 530, 28), "Emily Dickinson | Poem 254 / 314", scoreStyle);
                GUI.Label(new Rect(55, 148, 530, 28), CreatorCredit, scoreStyle);
                Rect energy = new Rect(190, 187, 260, 28);
                DrawPanel(energy, new Color(1f, 0.7f, 0.25f));
                GUI.color = Color.black;
                GUI.Label(energy, "ENERGY  100", scoreStyle);
                GUI.color = Color.white;
                int progress = State == PlayState.Complete ? ObstaclesPerLevel : Score % ObstaclesPerLevel;
                GUI.Label(new Rect(55, 222, 530, 28), $"Obstacles: {progress} / {ObstaclesPerLevel}   |   Best: {BestScore}", scoreStyle);
                string prompt = State == PlayState.Ready ? "Space / click to fly"
                    : State == PlayState.GameOver ? "Hope keeps singing..."
                    : State == PlayState.AwaitingFlap ? "Space / click to rise again"
                    : State == PlayState.Complete ? "You carried hope through all three worlds!" : "";
                GUI.Label(new Rect(40, 610, 560, 45), prompt, textStyle);
                GUIStyle button = new GUIStyle(GUI.skin.button) { fontSize = 18 };
                if (GUI.Button(new Rect(210, 690, 220, 44), "Read poem / Credits", button)) SetPoemVisible(true);
            }
            GUI.color = previousColor;
            GUI.matrix = previous;
        }

        private void DrawPanel(Rect bounds, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(bounds, texture);
            GUI.color = previous;
        }

        private void OnDestroy()
        {
            if (showingPoem) Time.timeScale = timeScaleBeforePoem;
            if (square != null) Destroy(square);
            if (texture != null) Destroy(texture);
        }
    }
}
