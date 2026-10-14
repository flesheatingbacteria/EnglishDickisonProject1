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
        private const string CreatorCredit = "Made by Alvin, Shridhar and Ishaan";
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
        // HUD (drawn with PixelUi in a virtual space 800 units tall).
        private const float UiHeight = 800, UiMargin = 24;
        private static readonly Color[] LevelAccent = {
            new Color(1f, 0.72f, 0.29f), new Color(0.81f, 0.85f, 0.91f), new Color(0.66f, 0.91f, 1f)
        };
        private static readonly Color UiMuted = new Color(0.86f, 0.82f, 0.9f);
        private Rect poemButton, playAgainButton;
        private float bannerStart = -10, scorePopAt = -10;
        private int shownScore;
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
            Sprite bowlArt = PixelArt.Prop("nest", new Vector2(0.5f, 1)), branchArt = PixelArt.Prop("branch", new Vector2(0.5f, 0.5f));
            if (bowlArt != null && branchArt != null)
            {
                SpriteRenderer branch = new GameObject("Branch", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                branch.transform.SetParent(nest, false);
                branch.transform.localPosition = new Vector2(-branchLength / 2 + 0.2f, -0.25f);
                branch.sprite = branchArt;
                branch.drawMode = SpriteDrawMode.Tiled;
                branch.size = new Vector2(branchLength, branchArt.bounds.size.y);
                branch.sortingOrder = 1;
                SpriteRenderer bowl = new GameObject("Nest Bowl", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                bowl.transform.SetParent(nest, false);
                bowl.transform.localPosition = new Vector2(0, 0.1f);
                bowl.sprite = bowlArt;
                bowl.sortingOrder = 6; // In front of the bird, so it sits inside the nest.
                return;
            }
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

        private bool PointerOverControls()
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
            float scale = Screen.height / UiHeight;
            Vector2 ui = new Vector2(pointer.x / scale, (Screen.height - pointer.y) / scale);
            return poemButton.Contains(ui) || playAgainButton.Contains(ui);
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
            // Reaching a level mid-run shows its banner briefly; on a fresh start the Ready screen shows it.
            bannerStart = State == PlayState.Playing ? Time.unscaledTime : -10;
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
            float scale = Screen.height / UiHeight;
            float width = Screen.width / scale;
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUI.color = Color.white;
            playAgainButton = Rect.zero;
            if (showingPoem) DrawPoem(width);
            else DrawHud(width);
            PixelUi.Frame(new Rect(0, 0, width, UiHeight), "screen_frame", 4);
            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }

        private void DrawHud(float width)
        {
            float now = Time.unscaledTime;
            Color accent = LevelAccent[LevelIndex];

            // Level card: name and one pip per obstacle in this level.
            string levelName = LevelNames[LevelIndex].ToUpperInvariant();
            const float pipStep = 21;
            Rect card = new Rect(UiMargin, UiMargin,
                Mathf.Max(PixelUi.TextWidth(levelName, 4), pipStep * ObstaclesPerLevel - 3) + 36, 96);
            PixelUi.Frame(card, "panel");
            PixelUi.Text("LEVEL " + (LevelIndex + 1), card.x + 18, card.y + 16, 2, UiMuted);
            PixelUi.Text(levelName, card.x + 18, card.y + 32, 4, accent);
            int progress = State == PlayState.Complete ? ObstaclesPerLevel : Score % ObstaclesPerLevel;
            for (int i = 0; i < ObstaclesPerLevel; i++)
                PixelUi.Icon(i < progress ? "pip_on" : "pip_off", card.x + 18 + i * pipStep, card.y + 62);

            // Score pops one size up for a moment when it changes.
            if (Score != shownScore) { if (Score > shownScore) scorePopAt = now; shownScore = Score; }
            float scoreSize = now - scorePopAt < 0.12f ? 9 : 8;
            PixelUi.Text(Score.ToString(), width / 2, UiMargin + (9 - scoreSize) * 2.5f, scoreSize, Color.white, 0.5f);
            PixelUi.Text("BEST " + BestScore, width / 2, UiMargin + 56, 2, UiMuted, 0.5f);

            // Energy: always full, with a shimmer running along it.
            Rect bar = new Rect(width - UiMargin - 168, UiMargin + 6, 168, 30);
            PixelUi.Icon("feather", bar.x - 36, bar.y);
            PixelUi.Frame(bar, "bar");
            Rect fill = new Rect(bar.x + 9, bar.y + 9, bar.width - 18, bar.height - 18);
            PixelUi.Fill(fill, new Color(1f, 0.72f, 0.29f));
            PixelUi.Fill(new Rect(fill.x, fill.y, fill.width, 3), new Color(1f, 0.89f, 0.63f));
            PixelUi.Fill(new Rect(fill.x, fill.yMax - 3, fill.width, 3), new Color(0.78f, 0.47f, 0.16f));
            for (float x = fill.x + 24; x < fill.xMax - 3; x += 24)
                PixelUi.Fill(new Rect(x, fill.y + 3, 3, fill.height - 6), new Color(0.88f, 0.58f, 0.2f));
            float shine = fill.x + Mathf.Round(Mathf.Repeat(now * 90, fill.width + 90) / 3) * 3 - 45;
            Rect glint = Rect.MinMaxRect(Mathf.Max(fill.x, shine), fill.y, Mathf.Min(fill.xMax, shine + 6), fill.yMax);
            if (glint.width > 0) PixelUi.Fill(glint, new Color(1, 1, 1, 0.55f));
            PixelUi.Text("ENERGY", bar.xMax, bar.yMax + 9, 2, UiMuted, 1);

            poemButton = new Rect(width - UiMargin - 84, UiMargin + 64, 84, 36);
            if (Button(poemButton, "POEM", 2)) SetPoemVisible(true);

            // Centre messages.
            float bob = Mathf.Round(Mathf.Sin(now * 4) * 2) * 3;
            bool banner = State == PlayState.Ready || now - bannerStart < 2.6f;
            if (banner && State != PlayState.GameOver && State != PlayState.AwaitingFlap)
            {
                float fade = State == PlayState.Ready ? 1 : Mathf.Clamp01((2.6f - (now - bannerStart)) / 0.5f);
                float slide = State == PlayState.Ready ? 0 : Mathf.Round(Mathf.Clamp01(1 - (now - bannerStart) / 0.25f) * 8) * 3;
                GUI.color = new Color(1, 1, 1, fade);
                string line = LevelLines[LevelIndex].ToUpperInvariant();
                Rect panel = new Rect(0, 214 - slide, Mathf.Max(PixelUi.TextWidth(levelName, 7), PixelUi.TextWidth(line, 2)) + 60, 124);
                panel.x = Mathf.Round((width - panel.width) / 2);
                PixelUi.Frame(panel, "panel");
                PixelUi.Text("LEVEL " + (LevelIndex + 1), width / 2, panel.y + 18, 2, UiMuted, 0.5f);
                PixelUi.Text(levelName, width / 2, panel.y + 40, 7, accent, 0.5f);
                PixelUi.Text(line, width / 2, panel.y + 90, 2, Color.white, 0.5f);
                GUI.color = Color.white;
            }
            if (State == PlayState.Ready)
                PixelUi.Text("SPACE OR CLICK TO FLY", width / 2, 520 + bob, 3, Color.white, 0.5f);
            else if (State == PlayState.GameOver || State == PlayState.AwaitingFlap)
            {
                Rect panel = new Rect(0, 300, 372, State == PlayState.AwaitingFlap ? 96 : 66);
                panel.x = Mathf.Round((width - panel.width) / 2);
                PixelUi.Frame(panel, "panel");
                PixelUi.Text("HOPE KEEPS SINGING...", width / 2, panel.y + 24, 3, accent, 0.5f);
                if (State == PlayState.AwaitingFlap && Mathf.Repeat(now, 1) < 0.7f)
                    PixelUi.Text("SPACE OR CLICK TO RISE AGAIN", width / 2, panel.y + 60, 2, UiMuted, 0.5f);
            }
            else if (State == PlayState.Complete)
            {
                Rect panel = new Rect(0, 250, 420, 196);
                panel.x = Mathf.Round((width - panel.width) / 2);
                PixelUi.Frame(panel, "panel");
                PixelUi.Text("YOU CARRIED HOPE", width / 2, panel.y + 26, 4, accent, 0.5f);
                PixelUi.Text("THROUGH ALL THREE WORLDS", width / 2, panel.y + 58, 3, Color.white, 0.5f);
                PixelUi.Text($"SCORE {Score}   BEST {BestScore}", width / 2, panel.y + 88, 2, UiMuted, 0.5f);
                playAgainButton = new Rect(width / 2 - 96, panel.y + 120, 192, 48);
                if (Button(playAgainButton, "PLAY AGAIN", 3)) SelectLevel(0);
            }

            // Credits, small, bottom right.
            const string poemCredit = "POEM BY EMILY DICKINSON";
            float creditWidth = Mathf.Max(PixelUi.TextWidth(poemCredit, 2), PixelUi.TextWidth(CreatorCredit, 2));
            PixelUi.Fill(new Rect(width - UiMargin - creditWidth - 9, UiHeight - UiMargin - 36, creditWidth + 18, 39),
                new Color(0.08f, 0.06f, 0.1f, 0.6f));
            Color credit = new Color(1, 1, 1, 0.9f);
            PixelUi.SmallPrint(poemCredit, width - UiMargin, UiHeight - UiMargin - 30, credit, 1);
            PixelUi.SmallPrint(CreatorCredit, width - UiMargin, UiHeight - UiMargin - 15, credit, 1);
        }

        private void DrawPoem(float width)
        {
            PixelUi.Fill(new Rect(0, 0, width, UiHeight), new Color(0.04f, 0.03f, 0.07f, 0.75f));
            Rect page = new Rect(0, 36, Mathf.Min(width - 48, 600), 728);
            page.x = Mathf.Round((width - page.width) / 2);
            PixelUi.Frame(page, "parchment");
            float center = page.center.x, room = page.width - 60;
            Color ink = new Color(0.23f, 0.14f, 0.09f), faded = new Color(0.5f, 0.36f, 0.25f);
            const string title = "\"HOPE\" IS THE THING WITH FEATHERS";
            PixelUi.Text(title, center, page.y + 36, PixelUi.TextWidth(title, 3) <= room ? 3 : 2, ink, 0.5f, false);
            PixelUi.Text("EMILY DICKINSON", center, page.y + 66, 2, faded, 0.5f, false);
            Vector2 feather = PixelUi.IconSize("feather");
            PixelUi.Icon("feather", center - feather.x / 2, page.y + 90);

            string[] lines = PoemText.Split('\n');
            float size = 3;
            foreach (string line in lines)
                if (PixelUi.TextWidth(line, 3) > room) size = 2;
            float y = page.y + 140;
            foreach (string line in lines)
            {
                if (line.Length == 0) { y += 5 * size; continue; }
                PixelUi.Text(line, center, y, size, ink, 0.5f, false);
                y += 5 * size + 8;
            }

            PixelUi.Text("POEM 254 (JOHNSON) / 314 (FRANKLIN)", center, page.yMax - 148, 2, faded, 0.5f, false);
            PixelUi.Text("PUBLIC-DOMAIN TEXT: ACADEMY OF AMERICAN POETS", center, page.yMax - 132, 2, faded, 0.5f, false);
            PixelUi.Text(CreatorCredit, center, page.yMax - 108, 2, ink, 0.5f, false);
            string label = State == PlayState.Ready ? "BEGIN THE JOURNEY" : "BACK TO THE GAME";
            Rect button = new Rect(0, page.yMax - 80, PixelUi.TextWidth(label, 3) + 48, 48);
            button.x = Mathf.Round(center - button.width / 2);
            if (Button(button, label, 3)) SetPoemVisible(false);
        }

        private static bool Button(Rect rect, string label, float size)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            PixelUi.Frame(rect, hover ? "button_hover" : "button");
            PixelUi.Text(label, rect.center.x, rect.y + (rect.height - 5 * size) / 2 - Mathf.Floor(size / 2), size,
                new Color(0.23f, 0.12f, 0.05f), 0.5f, false);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        private void OnDestroy()
        {
            if (showingPoem) Time.timeScale = timeScaleBeforePoem;
            if (square != null) Destroy(square);
            if (texture != null) Destroy(texture);
        }
    }
}
