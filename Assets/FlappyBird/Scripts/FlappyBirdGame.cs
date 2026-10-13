using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FlappyBird
{
    public sealed class FlappyBirdGame : MonoBehaviour
    {
        public enum PlayState { Ready, Playing, GameOver, AwaitingFlap }
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

        private const string BestKey = "FlappyBird.BestScore";
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
            if (camera == null) camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
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

            Block("Ground", transform, new Vector2(0, Floor - 0.35f), new Vector2(HalfWidth * 2 + 4, 0.7f), new Color(0.8f, 0.68f, 0.42f), true);
            Block("Grass", transform, new Vector2(0, Floor - 0.04f), new Vector2(HalfWidth * 2 + 4, 0.12f), new Color(0.5f, 0.77f, 0.24f), false);
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
            BestScore = PlayerPrefs.GetInt(BestKey, 0);
            ResetRun();
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

        private void Update()
        {
            if (State == PlayState.GameOver)
            {
                if (Time.unscaledTime >= resumeAt) State = PlayState.AwaitingFlap;
                // Keep the world frozen until a fresh flap after the one-second pause.
                return;
            }
            if (FlapPressed())
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
            Color green = new Color(0.3f, 0.7f, 0.22f);
            Block("Lower Pipe", pair.transform, new Vector2(0, (Floor + bottomEdge) / 2), new Vector2(pipeWidth, bottomEdge - Floor), green, true, 2);
            Block("Upper Pipe", pair.transform, new Vector2(0, (cameraHalfHeight + topEdge) / 2), new Vector2(pipeWidth, cameraHalfHeight - topEdge), green, true, 2);
            Block("Lower Rim", pair.transform, new Vector2(0, bottomEdge - 0.12f), new Vector2(pipeWidth + 0.14f, 0.24f), green, true, 3);
            Block("Upper Rim", pair.transform, new Vector2(0, topEdge + 0.12f), new Vector2(pipeWidth + 0.14f, 0.24f), green, true, 3);
        }

        public void AddScore() { if (State == PlayState.Playing) Score++; }

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
            foreach (Transform pipe in pipes)
            {
                pipe.gameObject.SetActive(false);
                Destroy(pipe.gameObject);
            }
            Score = 0;
            spawnTimer = 0.8f;
            Bird.ResetBird();
            storm.ResetStorm();
            nest.localPosition = nestStartPosition;
            nest.gameObject.SetActive(true);
        }

        private void OnGUI()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 38, fontStyle = FontStyle.Bold };
                textStyle = new GUIStyle(titleStyle) { fontSize = 20, fontStyle = FontStyle.Normal };
                scoreStyle = new GUIStyle(titleStyle) { fontSize = 48 };
                titleStyle.normal.textColor = textStyle.normal.textColor = scoreStyle.normal.textColor = Color.white;
            }
            float scale = Mathf.Min(Screen.width / 640f, Screen.height / 800f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 640 * scale) / 2, (Screen.height - 800 * scale) / 2), Quaternion.identity, Vector3.one * scale);
            GUI.Label(new Rect(0, 30, 640, 65), "Infinity", scoreStyle);
            GUI.Label(new Rect(0, 100, 640, 35), "BEST:  Infinity", textStyle);
            // Energy is permanently full, including before a run and after game over.
            Color previousColor = GUI.color;
            GUI.color = new Color(0.08f, 0.18f, 0.22f);
            GUI.DrawTexture(new Rect(190, 150, 260, 34), texture);
            GUI.color = Color.orange;
            GUI.DrawTexture(new Rect(193, 153, 254, 28), texture);
            GUI.color = Color.black;
            GUI.Label(new Rect(190, 150, 260, 34), "ENERGY  100", textStyle);
            GUI.color = previousColor;
            GUI.matrix = previous;
        }

        private void OnDestroy()
        {
            if (square != null) Destroy(square);
            if (texture != null) Destroy(texture);
        }
    }
}
