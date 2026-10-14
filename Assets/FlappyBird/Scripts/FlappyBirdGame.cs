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
        // One level per stanza, in the poem's order.
        private static readonly string[] LevelNames = { "Hope", "The Gale", "Extremity" };
        private static readonly string[] LevelLines = {
            "\"Hope\" is the thing with feathers -",
            "And sweetest - in the Gale - is heard -",
            "I've heard it in the chillest land -"
        };
        // DickinsonMusic themes: 0 = Gale, 1 = Warmth, 2 = Sea.
        private static readonly int[] LevelMusic = { 1, 0, 2 };
        private Camera arenaCamera;
        private DickinsonMusic music;
        private Transform scenery;
        private SpriteRenderer ground, grass;
        // Pixel art (null when the sprites are missing; the placeholder blocks are used instead).
        private SpriteRenderer groundArt, birdArt;
        private Sprite[] birdFrames;
        private Sprite birdHurt;
        private Transform backdrop;
        private float groundScroll;
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
            groundArt = new GameObject("Ground Art", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            groundArt.transform.SetParent(transform, false);
            groundArt.drawMode = SpriteDrawMode.Tiled;
            groundArt.sortingOrder = 4;
            CreateNest();

            GameObject bird = new GameObject("Bird", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(BirdController));
            bird.transform.SetParent(transform);
            SpriteRenderer renderer = bird.GetComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = new Color(1, 0.83f, 0.18f);
            renderer.sortingOrder = 5;
            // Scale visuals independently so the collision radius stays in world units.
            renderer.enabled = false;
            GameObject[] placeholder = {
                Block("Body", bird.transform, Vector2.zero, new Vector2(0.58f, 0.44f), renderer.color, false, 5),
                Block("Beak", bird.transform, new Vector2(0.34f, -0.04f), new Vector2(0.22f, 0.13f), new Color(1, 0.42f, 0.1f), false, 6),
                Block("Eye", bird.transform, new Vector2(0.16f, 0.1f), new Vector2(0.08f, 0.08f), Color.black, false, 7)
            };
            birdFrames = new[] { PixelArt.Bird("up"), PixelArt.Bird("mid"), PixelArt.Bird("down") };
            birdHurt = PixelArt.Bird("hurt");
            if (birdFrames[0] != null && birdFrames[1] != null && birdFrames[2] != null)
            {
                birdArt = new GameObject("Bird Art", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                birdArt.transform.SetParent(bird.transform, false);
                birdArt.sprite = birdFrames[1];
                birdArt.sortingOrder = 5;
                foreach (GameObject piece in placeholder) piece.SetActive(false);
            }
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
            AnimateBird();
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
            if (groundArt.sprite != null)
            {
                // Shift by whole tiles only, so the wrap is invisible.
                groundScroll = Mathf.Repeat(groundScroll + pipeSpeed * Time.deltaTime, groundArt.sprite.bounds.size.x);
                groundArt.transform.localPosition = new Vector3(-groundScroll, Floor, 0);
            }
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
            Color green = LevelIndex == 0 ? new Color(0.36f, 0.66f, 0.3f)
                : LevelIndex == 1 ? new Color(0.38f, 0.48f, 0.62f) : new Color(0.5f, 0.75f, 0.88f);
            Block("Lower Pipe", pair.transform, new Vector2(0, (Floor + bottomEdge) / 2), new Vector2(pipeWidth, bottomEdge - Floor), green, true, 2);
            Block("Upper Pipe", pair.transform, new Vector2(0, (cameraHalfHeight + topEdge) / 2), new Vector2(pipeWidth, cameraHalfHeight - topEdge), green, true, 2);
            Block("Lower Rim", pair.transform, new Vector2(0, bottomEdge - 0.12f), new Vector2(pipeWidth + 0.14f, 0.24f), green, true, 3);
            Block("Upper Rim", pair.transform, new Vector2(0, topEdge + 0.12f), new Vector2(pipeWidth + 0.14f, 0.24f), green, true, 3);

            // Pixel art drawn over the same colliders; the blocks stay for collision only.
            Sprite topArt = PixelArt.TopPipe(LevelIndex), bottomArt = PixelArt.BottomPipe(LevelIndex);
            if (topArt == null || bottomArt == null) return;
            foreach (SpriteRenderer block in pair.GetComponentsInChildren<SpriteRenderer>()) block.enabled = false;
            float screenTop = arenaCamera.orthographicSize + 1;
            float overhang = PixelArt.TopPipeOverhangRows / PixelArt.PixelsPerUnit;
            PipeArt(pair.transform, topArt, screenTop, screenTop - topEdge + overhang);
            PipeArt(pair.transform, bottomArt, Floor - 1, bottomEdge - Floor + 1);
        }

        private static void PipeArt(Transform pair, Sprite sprite, float y, float height)
        {
            SpriteRenderer art = new GameObject("Pipe Art", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            art.transform.SetParent(pair, false);
            art.transform.localPosition = new Vector3(0, y, 0);
            art.sprite = sprite;
            art.drawMode = SpriteDrawMode.Tiled;
            art.tileMode = SpriteTileMode.Continuous;
            art.size = new Vector2(sprite.bounds.size.x, height);
            art.sortingOrder = 3;
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
            // 0 Hope: dawn, drifting motes. 1 The Gale: storm, wind, following storm. 2 Extremity: frozen sea, waves.
            arenaCamera.backgroundColor = LevelIndex == 0 ? new Color(0.78f, 0.7f, 0.92f)
                : LevelIndex == 1 ? new Color(0.18f, 0.2f, 0.28f) : new Color(0.06f, 0.09f, 0.23f);
            ground.color = LevelIndex == 0 ? new Color(0.78f, 0.61f, 0.42f)
                : LevelIndex == 1 ? new Color(0.28f, 0.36f, 0.4f) : new Color(0.61f, 0.77f, 0.89f);
            grass.color = LevelIndex == 0 ? new Color(0.55f, 0.81f, 0.35f)
                : LevelIndex == 1 ? new Color(0.56f, 0.67f, 0.72f) : new Color(0.96f, 0.97f, 1f);
            storm.gameObject.SetActive(LevelIndex == 1);
            if (scenery != null) { scenery.gameObject.SetActive(false); Destroy(scenery.gameObject); }
            scenery = new GameObject("Level Imagery").transform;
            scenery.SetParent(transform, false);
            for (int i = 0; i < 24; i++)
            {
                Vector2 size = LevelIndex == 0 ? new Vector2(0.06f, 0.06f)
                    : LevelIndex == 1 ? new Vector2(0.65f, 0.025f) : new Vector2(0.7f, 0.07f);
                Color color = LevelIndex == 0 ? new Color(1f, 0.96f, 0.75f, 0.7f)
                    : LevelIndex == 1 ? new Color(0.8f, 0.9f, 1f, 0.32f) : new Color(0.85f, 0.95f, 1f, 0.6f);
                Block(LevelIndex == 0 ? "Mote" : LevelIndex == 1 ? "Wind" : "Wave", scenery,
                    Vector2.zero, size, color, false, -2);
            }
            ApplyLevelArt();
            music.SetLevel(LevelMusic[LevelIndex]);
        }

        private void ApplyLevelArt()
        {
            if (backdrop != null) { backdrop.gameObject.SetActive(false); Destroy(backdrop.gameObject); }
            backdrop = null;
            // The background's horizon sits on the floor and its sky fills the view above it.
            Sprite sky = PixelArt.Background(LevelIndex, arenaCamera.orthographicSize - Floor);
            if (sky != null)
            {
                backdrop = new GameObject("Backdrop").transform;
                backdrop.SetParent(transform, false);
                float width = sky.bounds.size.x;
                int copies = Mathf.CeilToInt(HalfWidth / width);
                for (int i = -copies; i <= copies; i++)
                {
                    SpriteRenderer tile = new GameObject("Backdrop Tile", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                    tile.transform.SetParent(backdrop, false);
                    tile.transform.localPosition = new Vector3(i * width, Floor, 0);
                    tile.sprite = sky;
                    tile.flipX = i % 2 != 0; // Mirror neighbours so their edges always match.
                    tile.sortingOrder = -10;
                }
            }

            groundArt.sprite = PixelArt.Ground(LevelIndex);
            bool hasGround = groundArt.sprite != null;
            groundArt.enabled = hasGround;
            ground.enabled = grass.enabled = !hasGround;
            if (hasGround)
            {
                // Two extra tiles of width leave room to scroll by one tile either way.
                groundArt.size = new Vector2(HalfWidth * 2 + 4 + groundArt.sprite.bounds.size.x * 2, groundArt.sprite.bounds.size.y);
                groundArt.transform.localPosition = new Vector3(-groundScroll, Floor, 0);
            }
        }

        private void AnimateBird()
        {
            if (birdArt == null) return;
            if (State == PlayState.GameOver || State == PlayState.AwaitingFlap)
                birdArt.sprite = birdHurt != null ? birdHurt : birdFrames[1];
            else if (State == PlayState.Playing)
            {
                int frame = Mathf.FloorToInt(Time.time * 12) % 4; // up, mid, down, mid
                birdArt.sprite = birdFrames[frame == 3 ? 1 : frame];
            }
            else birdArt.sprite = birdFrames[1];
        }

        private void AnimateScenery()
        {
            if (scenery == null) return;
            float time = Time.time;
            for (int i = 0; i < scenery.childCount; i++)
            {
                float x = Mathf.Repeat(i * 1.73f - time * (LevelIndex == 1 ? 2.2f : 0.5f), HalfWidth * 2 + 2) - HalfWidth - 1;
                float y = LevelIndex == 0 ? Floor + Mathf.Repeat(time * 0.5f + i * 0.43f, cameraHalfHeight - Floor)
                    : LevelIndex == 1 ? Floor + 1 + Mathf.Repeat(i * 0.71f, cameraHalfHeight - Floor - 1.5f)
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
