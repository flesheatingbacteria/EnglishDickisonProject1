using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FlappyBird
{
    public sealed class FlappyBirdGame : MonoBehaviour
    {
        // "And never stops - at all -": there is no game over. GameOver is a crash: the bird
        // freezes, then flaps on. Interlude is the stanza cutscene that opens the game and
        // links one level to the next.
        public enum PlayState { Ready, Playing, GameOver, AwaitingFlap, Complete, Interlude }
        [Header("Bird")]
        [Min(0.1f)] public float gravityScale = 2.2f;
        [Min(0.1f)] public float flapSpeed = 6.5f;
        [Header("Pipes")]
        [Min(0.1f)] public float pipeSpeed = 2.7f;
        [Min(0.1f)] public float spawnInterval = 1.65f;
        [Range(1.5f, 4f)] public float pipeGap = 2.8f;
        [Min(0.1f)] public float pipeWidth = 0.85f;
        [Header("Hope")]
        [Tooltip("Energy lost per second on level 1; each later level drains 1.5 more.")]
        [Min(0)] public float energyDrain = 5f;
        [Min(0)] public float crashCost = 25f;
        [Min(1)] public float heartValue = 35f;
        [Tooltip("A heart appears with every Nth pipe.")]
        [Min(1)] public int heartEvery = 3;
        [Header("Arena")]
        [Min(4)] public float cameraHalfHeight = 5f;
        public PlayState State { get; private set; }
        public BirdController Bird { get; private set; }
        public float HalfWidth { get; private set; }
        public int Score { get; private set; }
        // The score is how many times you crashed; the record is the fewest crashes in a full run.
        public int Crashes { get; private set; }
        public int FewestCrashes { get; private set; } = -1;
        public float Energy { get; private set; } = 100;
        // Pipes speed up within each level and from one level to the next.
        public bool WorldMoving => State == PlayState.Playing || State == PlayState.Interlude;
        public float CurrentSpeed => pipeSpeed * (1 + 0.12f * LevelIndex + 0.1f * LevelProgress + 0.15f * StormSurge);
        // "And sore must be the storm": over the Gale's last stretch the storm throws everything it has.
        public float StormSurge => LevelIndex == 1 && (State == PlayState.Playing || State == PlayState.GameOver || State == PlayState.AwaitingFlap)
            ? Mathf.Clamp01((LevelProgress - 0.6f) / 0.3f) : 0;
        private float LevelProgress => (float)(Score - levelStartScore) / PipesThisLevel;

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

        private const string BestKey = "FlappyBird.FewestCrashes";
        public int LevelIndex { get; private set; }
        // "And sings the tune without the words": in this version the poem's form builds each level.
        // Every stressed beat of a line is one pipe, every dash is a breath (a longer gap with no
        // pipe), lines that rhyme share a pipe colour, and the HUD loses its words level by level.
        private sealed class VerseLine
        {
            public readonly string Beats, EndWord;
            public readonly char Rhyme; // lines with the same letter rhyme; ' ' means no rhyme
            public readonly bool Slant;
            public VerseLine(string beats, string endWord, char rhyme, bool slant = false)
            {
                Beats = beats; EndWord = endWord; Rhyme = rhyme; Slant = slant;
            }
        }

        // x = a stressed beat (a pipe), - = a dash (a breath). Common meter: four beats, then three.
        private static readonly VerseLine[][] Verse = {
            new[] {
                new VerseLine("x x x -", "feathers", ' '),        // HOPE is the THING with FEATH-ers -
                new VerseLine("x x x -", "soul", 'b'),            // that PERCH-es IN the SOUL -
                new VerseLine("x x x x -", "words", ' '),         // and SINGS the TUNE with-OUT the WORDS -
                new VerseLine("x x - x -", "all", 'b', true)      // and NEV-er STOPS - at ALL -
            },
            new[] {
                new VerseLine("x - x x - x -", "heard", 'a'),     // and SWEET-est - IN the GALE - is HEARD -
                new VerseLine("x x x -", "storm", 'b'),           // and SORE must BE the STORM -
                new VerseLine("x x x x", "Bird", 'a'),            // that COULD a-BASH the LIT-tle BIRD
                new VerseLine("x x x -", "warm", 'b', true)       // that KEPT so MAN-y WARM -
            },
            new[] {
                new VerseLine("x x x x -", "land", ' '),          // i've HEARD it IN the CHILL-est LAND -
                new VerseLine("x x x -", "Sea", 'b'),             // and ON the STRANG-est SEA -
                new VerseLine("x - x - x x", "Extremity", 'b', true), // YET - NEV-er - IN ex-TREM-i-TY,
                new VerseLine("x x - x", "Me", 'b')               // it ASKED a CRUMB - of ME.
            }
        };
        private static readonly string[] RhymeSchemes = { "ABCB", "ABAB", "ABBB" };
        private static readonly Color RhymeA = new Color(1f, 0.8f, 0.42f), RhymeB = new Color(0.55f, 0.84f, 1f);

        private struct Beat { public int Line; public bool LineEnd; public float Gap; }
        private readonly System.Collections.Generic.List<Beat> beats = new System.Collections.Generic.List<Beat>();
        private int beatIndex, levelStartScore, lineShown = -1;
        private float lineShownAt = -10;

        private static int BeatsIn(int level)
        {
            int count = 0;
            foreach (VerseLine line in Verse[level]) foreach (char c in line.Beats) if (c == 'x') count++;
            return count;
        }

        public int PipesThisLevel => BeatsIn(LevelIndex);

        private static Color RhymeColor(char rhyme) => rhyme == 'a' ? RhymeA : rhyme == 'b' ? RhymeB : Color.white;

        // Turn the stanza into a pipe schedule: one pipe per beat, with breaths for dashes and line ends.
        private void StartBeats()
        {
            beats.Clear();
            beatIndex = 0;
            float breath = 0;
            VerseLine[] stanza = Verse[LevelIndex];
            for (int l = 0; l < stanza.Length; l++)
            {
                string[] marks = stanza[l].Beats.Split(' ');
                int lastBeat = System.Array.LastIndexOf(marks, "x");
                for (int m = 0; m < marks.Length; m++)
                {
                    if (marks[m] == "-") { breath += 0.7f; continue; }
                    beats.Add(new Beat { Line = l, LineEnd = m == lastBeat, Gap = 1 + breath });
                    breath = 0;
                }
                if (marks[marks.Length - 1] != "-") breath += 0.5f; // a line end is a breath too
            }
        }
        // One level per stanza, in the poem's order.
        private static readonly string[] LevelNames = { "Hope", "The Gale", "Extremity" };
        private static readonly string[] LevelLines = {
            "\"Hope\" is the thing with feathers -",
            "And sweetest - in the Gale - is heard -",
            "I've heard it in the chillest land -"
        };
        // The stanza each level's cutscene flies through.
        private static readonly string[][] Stanzas = {
            new[] { "\"Hope\" is the thing with feathers -", "That perches in the soul -", "And sings the tune without the words -", "And never stops - at all -" },
            new[] { "And sweetest - in the Gale - is heard -", "And sore must be the storm -", "That could abash the little Bird", "That kept so many warm -" },
            new[] { "I've heard it in the chillest land -", "And on the strangest Sea -", "Yet - never - in Extremity,", "It asked a crumb - of Me." }
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
        private SpriteRenderer backdropArt;
        private Sprite heartArt;
        private float groundScroll, backdropScroll;
        // Procedural pipes: each gap is placed relative to the one before it.
        private float nextGapMiddle;
        private int pipesSpawned, screenWidth, screenHeight;
        private float hopeFlashAt = -10;
        // Cutscene state, and the outgoing world while it cross-fades into the next one.
        private StanzaRibbon ribbon;
        private int interludeLevel;
        private float interludeStart, crossfadeStart = -10;
        private bool worldSwapped;
        private SpriteRenderer backdropOld, groundOld;
        private Transform sceneryOld;
        private Color skyFrom, skyTo;
        private int interludeFrom;
        private WeatherFx weather;
        private const float CrossfadeTime = 4f;
        private static readonly float[] SceneryAlpha = { 0.7f, 0.32f, 0.6f };
        // Tree that holds the nest (summer, storm or winter to match the world), and crash/fall effects.
        private SpriteRenderer treeCrown;
        private SpriteRenderer[] nestArt = new SpriteRenderer[0];
        private Sprite featherArt;
        private float nestFadeAt = -10, shakeUntil = -10, crashAt = -10;
        private int lostHope;
        private bool showingTitle = true, newRecord;
        // The Gale's storm surge: gusts, lightning and the "sore" storm line.
        private float nextGustAt, gustAt = -10, nextBoltAt, boltAt = -10, surgeStart = -10;
        private int gustDirection;
        private Rect playButton, titlePoemButton;
        private float CrossfadeBlend => Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.time - crossfadeStart) / CrossfadeTime));
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
        private float bannerStart = -10, crashPopAt = -10;
        private int shownCrashes;
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
            FitCamera();

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
            backdropArt = new GameObject("Backdrop", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            backdropArt.transform.SetParent(transform, false);
            backdropArt.drawMode = SpriteDrawMode.Tiled;
            backdropArt.sortingOrder = -10;
            heartArt = PixelArt.Prop("heart", new Vector2(0.5f, 0.5f));
            featherArt = PixelArt.Prop("feather_bit", new Vector2(0.5f, 0.5f));
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
            weather = gameObject.AddComponent<WeatherFx>();
            weather.Initialize(this, square, Floor);
            FewestCrashes = PlayerPrefs.GetInt(BestKey, -1);
            ApplyLevel();
            ResetRun();
            showingTitle = true;
        }

        // Keep the arena usable in very narrow windows too.
        private void FitCamera()
        {
            screenWidth = Screen.width;
            screenHeight = Screen.height;
            arenaCamera.orthographicSize = cameraHalfHeight;
            HalfWidth = Mathf.Max(3.2f, cameraHalfHeight * arenaCamera.aspect);
            if (cameraHalfHeight * arenaCamera.aspect < 3.2f)
                arenaCamera.orthographicSize = 3.2f / arenaCamera.aspect;
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
            Sprite trunkArt = PixelArt.Prop("tree_trunk", new Vector2(0.5f, 0));
            if (bowlArt != null && branchArt != null && trunkArt != null)
            {
                // A tree grows up out of the ground; the nest sits at the end of one of its branches.
                const float trunkX = -1.25f, branchY = -0.25f;
                float groundBelow = Floor - 0.5f - nestStartPosition.y;
                SpriteRenderer trunk = NestPiece("Tree Trunk", trunkArt, new Vector2(trunkX, groundBelow), 1);
                trunk.drawMode = SpriteDrawMode.Tiled;
                trunk.size = new Vector2(trunkArt.bounds.size.x, branchY + 0.2f - groundBelow);
                SpriteRenderer branch = NestPiece("Branch", branchArt, new Vector2((trunkX + 0.1f) / 2, branchY), 1);
                branch.drawMode = SpriteDrawMode.Tiled;
                branch.size = new Vector2(0.1f - trunkX, branchArt.bounds.size.y);
                treeCrown = NestPiece("Tree Crown", null, new Vector2(trunkX, -0.12f), 2);
                // In front of the bird, so it sits inside the nest.
                NestPiece("Nest Bowl", bowlArt, new Vector2(0, 0.1f), 6);
                Sprite[] falling = { PixelArt.Prop("leaf_0", new Vector2(0.5f, 0.5f)), PixelArt.Prop("leaf_1", new Vector2(0.5f, 0.5f)),
                    PixelArt.Prop("leaf_2", new Vector2(0.5f, 0.5f)) };
                if (System.Array.TrueForAll(falling, sprite => sprite != null))
                    nest.gameObject.AddComponent<TreeLeaves>().Initialize(this, falling, treeCrown.transform, new Vector2(trunkX, 0.55f), 1.7f, groundBelow + 0.5f);
                nestArt = nest.GetComponentsInChildren<SpriteRenderer>();
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

        private SpriteRenderer NestPiece(string label, Sprite sprite, Vector2 position, int order)
        {
            SpriteRenderer piece = new GameObject(label, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            piece.transform.SetParent(nest, false);
            piece.transform.localPosition = position;
            piece.sprite = sprite;
            piece.sortingOrder = order;
            return piece;
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
            if (showingTitle) return playButton.Contains(ui) || titlePoemButton.Contains(ui);
            return poemButton.Contains(ui) || playAgainButton.Contains(ui);
        }

        private void Update()
        {
            if (Screen.width != screenWidth || Screen.height != screenHeight)
            {
                // Window resized or went full screen: refit so pipes still enter off-screen.
                FitCamera();
                ApplyLevelArt();
            }
            if (showingPoem) return;
            AnimateScenery();
            AnimateBird();
            float nestAlpha = Mathf.Clamp01((Time.time - nestFadeAt) / 0.6f);
            foreach (SpriteRenderer piece in nestArt) piece.color = new Color(1, 1, 1, nestAlpha);
            if (showingTitle)
            {
                if (FlapPressed() && !PointerOverControls()) StartGame();
                return;
            }
            if (State == PlayState.Complete) return;
            if (State == PlayState.Interlude) { UpdateInterlude(); return; }
            float surge = StormSurge;
            weather.Surge(surge);
            storm.Surge(surge);
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
                // Out of hope the bird still flies - it never stops - but its wingbeats are weaker.
                Bird.Flap(Energy > 0 ? 1 : 0.8f);
            }
            if (State != PlayState.Playing) return;
            Energy = Mathf.Max(0, Energy - (energyDrain + 1.5f * LevelIndex) * Time.deltaTime);
            if (surge > 0) StormTurns(surge);
            ScrollWorld(CurrentSpeed);
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0 && beatIndex < beats.Count)
            {
                SpawnPipe(beats[beatIndex++]);
                spawnTimer = beatIndex < beats.Count ? Interval * beats[beatIndex].Gap : float.MaxValue;
            }
            if (!Bird.Recovering && (Bird.transform.position.y < Floor || Bird.transform.position.y > cameraHalfHeight)) EndRun();
        }

        private void StormTurns(float surge)
        {
            float now = Time.unscaledTime;
            if (surgeStart < 0) surgeStart = now;
            // Gusts shove the bird up or down.
            if (now >= nextGustAt)
            {
                if (gustAt > 0)
                {
                    gustDirection = Random.value < 0.55f ? -1 : 1;
                    Bird.Push(new Vector2(0, gustDirection * Random.Range(1.8f, 3f) * surge));
                    shakeUntil = Mathf.Max(shakeUntil, now + 0.12f);
                }
                gustAt = now;
                nextGustAt = now + Random.Range(1.8f, 3.2f);
            }
            // Lightning, more often as the surge builds.
            if (surge > 0.3f && now >= nextBoltAt)
            {
                if (nextBoltAt > 0) { boltAt = now; shakeUntil = Mathf.Max(shakeUntil, now + 0.1f); }
                nextBoltAt = now + Random.Range(1.2f, 2.6f) / surge;
            }
        }

        private void ScrollWorld(float speed)
        {
            float step = speed * Time.deltaTime;
            if (groundArt.sprite != null)
            {
                // Shift by whole tiles only, so the wrap is invisible.
                groundScroll = Mathf.Repeat(groundScroll + step, groundArt.sprite.bounds.size.x);
                groundArt.transform.localPosition = new Vector3(-groundScroll, Floor, 0);
            }
            if (backdropArt.sprite != null)
            {
                // The backdrop drifts slower than the pipes, for a little depth.
                backdropScroll = Mathf.Repeat(backdropScroll + step * 0.2f, backdropArt.sprite.bounds.size.x);
                backdropArt.transform.localPosition = new Vector3(-backdropScroll, Floor, 0);
            }
            // The outgoing world sits just in front of the new one while it fades.
            if (backdropOld != null) backdropOld.transform.localPosition = backdropArt.transform.localPosition + Vector3.back * 0.5f;
            if (groundOld != null) groundOld.transform.localPosition = groundArt.transform.localPosition + Vector3.back * 0.5f;
            if (nest.gameObject.activeSelf)
            {
                nest.position += Vector3.left * step;
                if (nest.position.x < -HalfWidth - 1) nest.gameObject.SetActive(false);
            }
        }

        // ---- Stanza cutscene ------------------------------------------------------------

        private Vector2 ToUi(Vector2 world)
        {
            Vector3 screen = arenaCamera.WorldToScreenPoint(world);
            float scale = Screen.height / UiHeight;
            return new Vector2(screen.x / scale, (Screen.height - screen.y) / scale);
        }

        private void StartInterlude(int level)
        {
            State = PlayState.Interlude;
            Bird.Stop();
            interludeLevel = level;
            interludeFrom = LevelIndex;
            interludeStart = Time.time;
            worldSwapped = level == LevelIndex;
            float right = arenaCamera.orthographicSize * arenaCamera.aspect;
            ribbon = new StanzaRibbon(Stanzas[level], right + 1, Time.time + 0.6f, 2 * arenaCamera.orthographicSize / UiHeight);
        }

        private void UpdateInterlude()
        {
            float t = Time.time - interludeStart;
            ScrollWorld(pipeSpeed);
            // Leftover pipes fade out instead of vanishing.
            if (pipes.childCount > 0)
            {
                float fade = Mathf.Clamp01(1 - t / 0.5f);
                foreach (SpriteRenderer piece in pipes.GetComponentsInChildren<SpriteRenderer>())
                {
                    Color color = piece.color;
                    piece.color = new Color(color.r, color.g, color.b, Mathf.Min(color.a, fade));
                }
                if (fade <= 0) ClearPipes();
            }
            if (!worldSwapped && t >= 1f) SwapWorld();
            float blend = CrossfadeBlend;
            if (backdropOld != null && backdropOld.enabled)
            {
                backdropOld.color = groundOld.color = new Color(1, 1, 1, 1 - blend);
                arenaCamera.backgroundColor = Color.Lerp(skyFrom, skyTo, blend);
                if (blend >= 1) backdropOld.enabled = groundOld.enabled = false;
            }
            // The next world's weather rolls in from the right as the old one clears.
            weather.Blend(interludeFrom, interludeLevel, interludeFrom == interludeLevel ? 1 : worldSwapped ? blend : 0);
            if (blend >= 1 && sceneryOld != null) { Destroy(sceneryOld.gameObject); sceneryOld = null; }
            if (blend >= 1 && interludeFrom == 1 && interludeLevel != 1) storm.gameObject.SetActive(false);

            // The bird flies the wave of words.
            Vector3 at = Bird.transform.position;
            float target = ribbon.Advance(Time.time, at.x, ToUi);
            float y = Mathf.Lerp(at.y, target, 1 - Mathf.Exp(-5 * Time.deltaTime));
            float climb = Time.deltaTime > 0 ? (y - at.y) / Time.deltaTime : 0;
            Bird.transform.SetPositionAndRotation(new Vector3(at.x, y, 0), Quaternion.Euler(0, 0, Mathf.Clamp(climb * 5, -40, 25)));

            bool done = ribbon.AllCaught && Time.time - ribbon.LastCatchTime > 1.6f;
            if (done || (t > 0.8f && FlapPressed() && !PointerOverControls())) FinishInterlude();
        }

        // Keeps the old world on screen in front of the new one so they can cross-fade.
        private void SwapWorld()
        {
            worldSwapped = true;
            if (backdropOld == null)
            {
                backdropOld = new GameObject("Backdrop (fading out)", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                backdropOld.transform.SetParent(transform, false);
                backdropOld.drawMode = SpriteDrawMode.Tiled;
                backdropOld.sortingOrder = backdropArt.sortingOrder;
                groundOld = new GameObject("Ground (fading out)", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                groundOld.transform.SetParent(transform, false);
                groundOld.drawMode = SpriteDrawMode.Tiled;
                groundOld.sortingOrder = groundArt.sortingOrder;
            }
            backdropOld.sprite = backdropArt.sprite;
            backdropOld.size = backdropArt.size;
            backdropOld.enabled = backdropArt.enabled;
            groundOld.sprite = groundArt.sprite;
            groundOld.size = groundArt.size;
            groundOld.enabled = groundArt.enabled;
            backdropOld.color = groundOld.color = Color.white;
            skyFrom = arenaCamera.backgroundColor;
            LevelIndex = interludeLevel;
            ApplyLevel();
            // The following storm drifts in to the Gale and drifts away when leaving it.
            if (interludeLevel == 1) storm.EnterFromLeft();
            else if (interludeFrom == 1) { storm.gameObject.SetActive(true); storm.Leave(); }
            skyTo = arenaCamera.backgroundColor;
            arenaCamera.backgroundColor = skyFrom;
            crossfadeStart = Time.time;
            ScrollWorld(0);
        }

        private void FinishInterlude()
        {
            if (!worldSwapped) SwapWorld();
            if (backdropOld != null && backdropOld.enabled)
            {
                backdropOld.enabled = groundOld.enabled = false;
                arenaCamera.backgroundColor = skyTo;
            }
            ribbon = null;
            crossfadeStart = -10;
            if (sceneryOld != null) { Destroy(sceneryOld.gameObject); sceneryOld = null; }
            weather.Set(LevelIndex);
            storm.gameObject.SetActive(LevelIndex == 1);
            ClearPipes();
            Bird.ResetBird();
            nest.localPosition = nestStartPosition;
            nest.gameObject.SetActive(true);
            nestFadeAt = Time.time;
            State = PlayState.Ready;
            Energy = 100;
            pipesSpawned = 0;
            StartBeats();
            spawnTimer = 0.8f;
        }

        private float Interval => Mathf.Max(0.1f, spawnInterval - 0.1f * LevelIndex);

        private void SpawnPipe(Beat beat)
        {
            // Gaps narrow as each level goes on, and every level starts tighter than the last.
            float gap = Mathf.Clamp(pipeGap - 0.25f * LevelIndex - 0.2f * LevelProgress, 1.6f, cameraHalfHeight - Floor - 1);
            float low = Floor + gap / 2 + 0.5f, high = cameraHalfHeight - gap / 2 - 0.5f;
            // Each gap follows on from the previous one; the jumps between them grow too.
            float reach = 1.2f + 0.5f * LevelIndex + 0.4f * LevelProgress;
            float middle = pipesSpawned == 0 ? Random.Range(low, high) : Mathf.Clamp(nextGapMiddle, low, high);
            nextGapMiddle = Mathf.Clamp(middle + Random.Range(-reach, reach), low, high);
            pipesSpawned++;
            GameObject pair = new GameObject("Pipe Pair", typeof(PipePair));
            pair.transform.SetParent(pipes, false);
            // Always enter just past the right edge of the current view.
            pair.transform.position = new Vector3(arenaCamera.orthographicSize * arenaCamera.aspect + pipeWidth, 0, 0);
            PipePair pipe = pair.GetComponent<PipePair>();
            pipe.Initialize(this);
            pipe.Line = beat.Line;
            pipe.LineEnd = beat.LineEnd;
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
            if (topArt != null && bottomArt != null)
            {
                foreach (SpriteRenderer block in pair.GetComponentsInChildren<SpriteRenderer>()) block.enabled = false;
                float screenTop = arenaCamera.orthographicSize + 1;
                float overhang = PixelArt.TopPipeOverhangRows / PixelArt.PixelsPerUnit;
                PipeArt(pair.transform, topArt, screenTop, screenTop - topEdge + overhang);
                PipeArt(pair.transform, bottomArt, Floor - 1, bottomEdge - Floor + 1);
                // Pipes on rhyming lines share a colour, so the rhyme scheme can be seen.
                Color tint = RhymeColor(Verse[LevelIndex][beat.Line].Rhyme);
                foreach (SpriteRenderer art in pair.GetComponentsInChildren<SpriteRenderer>())
                    if (art.enabled) art.color = tint;
            }
            // Added last, so hiding the placeholder blocks above can't hide the heart too.
            if (pipesSpawned % heartEvery == 0) SpawnHeart(pair.transform, middle, gap);
        }

        // Hearts sit a little out of the way: hugging a cap inside the gap, or off the
        // straight line between this gap and the next one.
        private void SpawnHeart(Transform pair, float middle, float gap)
        {
            Vector2 spot;
            if (Random.value < 0.5f)
                spot = new Vector2(0, middle + (Random.value < 0.5f ? 1 : -1) * (gap / 2 - 0.4f));
            else
            {
                float between = (middle + nextGapMiddle) / 2 + (Random.value < 0.5f ? 1 : -1) * Random.Range(0.9f, 1.4f);
                spot = new Vector2(CurrentSpeed * Interval / 2, Mathf.Clamp(between, Floor + 0.6f, cameraHalfHeight - 0.6f));
            }
            GameObject heart = new GameObject("Hope Heart", typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(HopeHeart));
            heart.transform.SetParent(pair, false);
            heart.transform.localPosition = spot;
            SpriteRenderer art = heart.GetComponent<SpriteRenderer>();
            art.sortingOrder = 4;
            if (heartArt != null) art.sprite = heartArt;
            else
            {
                art.sprite = square;
                art.color = new Color(0.91f, 0.25f, 0.23f);
                heart.transform.localScale = Vector3.one * 0.3f;
            }
            heart.GetComponent<HopeHeart>().Initialize(this);
        }

        public void CollectHope()
        {
            if (State != PlayState.Playing) return;
            Energy = Mathf.Min(100, Energy + heartValue);
            hopeFlashAt = Time.unscaledTime;
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

        public void AddScore(PipePair passed = null)
        {
            if (State != PlayState.Playing) return;
            Score++;
            if (passed != null && passed.LineEnd) { lineShown = passed.Line; lineShownAt = Time.unscaledTime; }
            if (Score - levelStartScore < PipesThisLevel) return;
            if (LevelIndex < 2)
            {
                Energy = 100; // Reaching a new stanza refills hope.
                surgeStart = -10;
                levelStartScore = Score;
                lineShown = -1;
                StartInterlude(LevelIndex + 1);
            }
            else
            {
                Bird.Stop();
                SaveBest();
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
            if (sceneryOld != null) Destroy(sceneryOld.gameObject);
            sceneryOld = null;
            // During a cutscene the old level's wind/motes/waves fade out instead of vanishing.
            if (scenery != null && State == PlayState.Interlude) sceneryOld = scenery;
            else if (scenery != null) { scenery.gameObject.SetActive(false); Destroy(scenery.gameObject); }
            if (State != PlayState.Interlude) weather.Set(LevelIndex);
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
            // One long strip that wraps seamlessly; it scrolls in Update.
            backdropArt.sprite = PixelArt.Background(LevelIndex, arenaCamera.orthographicSize - Floor);
            backdropArt.enabled = backdropArt.sprite != null;
            if (backdropArt.sprite != null)
            {
                Vector2 tile = backdropArt.sprite.bounds.size;
                backdropArt.size = new Vector2(HalfWidth * 2 + 2 + tile.x * 2, tile.y);
                backdropArt.transform.localPosition = new Vector3(-backdropScroll, Floor, 0);
            }

            if (treeCrown != null) treeCrown.sprite = PixelArt.Prop("crown_" + LevelIndex, new Vector2(0.5f, 0.12f));
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
            else if (State == PlayState.Playing || State == PlayState.Interlude)
            {
                int frame = Mathf.FloorToInt(Time.time * 12) % 4; // up, mid, down, mid
                birdArt.sprite = birdFrames[frame == 3 ? 1 : frame];
            }
            else birdArt.sprite = birdFrames[1];
            bool resting = State == PlayState.Ready && nest.gameObject.activeSelf;
            birdArt.transform.localPosition = new Vector3(0, resting ? Mathf.Round(Mathf.Sin(Time.time * 2) + 0.5f) / PixelArt.PixelsPerUnit : 0, 0);
        }

        private void AnimateScenery()
        {
            float blend = CrossfadeBlend;
            AnimateScenery(scenery, LevelIndex, blend);
            AnimateScenery(sceneryOld, interludeFrom, 1 - blend);
        }

        private void AnimateScenery(Transform group, int level, float visibility)
        {
            if (group == null) return;
            float time = Time.time;
            for (int i = 0; i < group.childCount; i++)
            {
                Transform piece = group.GetChild(i);
                float x = Mathf.Repeat(i * 1.73f - time * (level == 1 ? 2.2f : 0.5f), HalfWidth * 2 + 2) - HalfWidth - 1;
                float y = level == 0 ? Floor + Mathf.Repeat(time * 0.5f + i * 0.43f, cameraHalfHeight - Floor)
                    : level == 1 ? Floor + 1 + Mathf.Repeat(i * 0.71f, cameraHalfHeight - Floor - 1.5f)
                    : Floor + 0.2f + (i % 3) * 0.2f + Mathf.Sin(time * 1.5f + i) * 0.1f;
                piece.localPosition = new Vector3(x, y, 0);
                SpriteRenderer art = piece.GetComponent<SpriteRenderer>();
                Color c = art.color;
                art.color = new Color(c.r, c.g, c.b, SceneryAlpha[level] * visibility);
            }
        }

        public void EndRun()
        {
            if (State != PlayState.Playing) return;
            Bird.Stop();
            Crashes++;
            resumeAt = Time.unscaledTime + 1f;
            // A crash shakes the screen, knocks feathers loose and costs hope - but never ends the run.
            crashAt = Time.unscaledTime;
            shakeUntil = Time.unscaledTime + 0.3f;
            lostHope = Mathf.RoundToInt(Mathf.Min(Energy, crashCost));
            FeatherPuff.Burst(Bird.transform.position, 8, featherArt, square);
            Energy = Mathf.Max(0, Energy - crashCost);
            State = PlayState.GameOver;
        }

        private void LateUpdate()
        {
            // Screen shake, fading out over its duration.
            float left = shakeUntil - Time.unscaledTime;
            Vector2 offset = left > 0 ? Random.insideUnitCircle * 0.5f * left : Vector2.zero;
            arenaCamera.transform.position = new Vector3(offset.x, offset.y, -10);
        }

        private void StartGame()
        {
            showingTitle = false;
            StartInterlude(LevelIndex);
        }

        // A full run is over: keep it if it had the fewest crashes yet.
        private void SaveBest()
        {
            newRecord = FewestCrashes < 0 || Crashes < FewestCrashes;
            if (!newRecord) return;
            FewestCrashes = Crashes;
            PlayerPrefs.SetInt(BestKey, FewestCrashes);
            PlayerPrefs.Save();
        }

        public void ResetRun()
        {
            State = PlayState.Ready;
            ClearPipes();
            levelStartScore = 0;
            for (int level = 0; level < LevelIndex; level++) levelStartScore += BeatsIn(level);
            Score = levelStartScore;
            Crashes = 0;
            Energy = 100;
            pipesSpawned = 0;
            StartBeats();
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
            else if (showingTitle) DrawTitle(width);
            else DrawHud(width);
            PixelUi.Frame(new Rect(0, 0, width, UiHeight), "screen_frame", 4);
            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }

        private void DrawHud(float width)
        {
            float now = Time.unscaledTime;
            Color accent = LevelAccent[LevelIndex];
            Rect fullScreen = new Rect(0, 0, width, UiHeight);
            float sinceCrash = now - crashAt;
            if (sinceCrash < 0.15f) PixelUi.Fill(fullScreen, new Color(1, 1, 1, 0.35f * (1 - sinceCrash / 0.15f)));
            float sinceBolt = now - boltAt;
            if (sinceBolt < 0.1f || sinceBolt > 0.16f && sinceBolt < 0.22f) PixelUi.Fill(fullScreen, new Color(1, 1, 1, 0.4f));
            float sinceGust = now - gustAt;
            if (StormSurge > 0 && sinceGust < 0.5f)
            {
                // Wind streaks show which way the gust is shoving.
                for (int i = 0; i < 9; i++)
                {
                    float y = 120 + i * 67 + gustDirection * sinceGust * 160;
                    float x = width - Mathf.Repeat(i * 173 + sinceGust * 2400, width + 200);
                    PixelUi.Fill(new Rect(Mathf.Round(x), Mathf.Round(y), 120, 3), new Color(1, 1, 1, 0.5f * (1 - sinceGust / 0.5f)));
                }
            }
            if (State == PlayState.Interlude)
            {
                Rect screen = fullScreen;
                // The opening starts at night and brightens like a sunrise.
                if (interludeFrom == interludeLevel)
                    PixelUi.Fill(screen, new Color(0.08f, 0.06f, 0.18f, 0.55f * (1 - Mathf.SmoothStep(0, 1, (Time.time - interludeStart - 0.3f) / 3.5f))));
                // The Gale's storm breaks as you leave it.
                if (interludeFrom == 1 && interludeLevel == 2 && Time.time - interludeStart < 0.3f)
                    PixelUi.Fill(screen, new Color(1, 1, 1, 0.6f * (1 - (Time.time - interludeStart) / 0.3f)));
                // Lightning announces the Gale.
                float sinceSwap = Time.time - crossfadeStart;
                if (interludeLevel == 1 && interludeFrom != 1 && (sinceSwap > 3.2f && sinceSwap < 3.28f || sinceSwap > 3.42f && sinceSwap < 3.52f))
                    PixelUi.Fill(screen, new Color(1, 1, 1, 0.45f));
            }

            // The words fall away as you go: all of them on level 1, fewer on 2, none on 3.
            int words = State == PlayState.Complete ? 2 : 2 - LevelIndex;

            // Level card: name, progress, and the stanza's rhyme scheme as coloured blocks.
            string levelName = LevelNames[LevelIndex].ToUpperInvariant();
            int progress = State == PlayState.Complete ? PipesThisLevel : Score - levelStartScore;
            Rect card = new Rect(UiMargin, UiMargin, Mathf.Max(words > 0 ? PixelUi.TextWidth(levelName, 4) : 0, 150) + 36, 96);
            PixelUi.Frame(card, "panel");
            if (words == 2) PixelUi.Text("LEVEL " + (LevelIndex + 1), card.x + 18, card.y + 16, 2, UiMuted);
            if (words > 0)
            {
                PixelUi.Text($"{progress}/{PipesThisLevel}", card.xMax - 18, card.y + 16, 2, UiMuted, 1);
                PixelUi.Text(levelName, card.x + 18, card.y + 32, 4, accent);
            }
            Rect track = new Rect(card.x + 18, card.y + 58, card.width - 36, 6);
            PixelUi.Fill(track, new Color(0.16f, 0.15f, 0.25f));
            PixelUi.Fill(new Rect(track.x, track.y, Mathf.Round(track.width * progress / PipesThisLevel / 3) * 3, track.height), accent);
            VerseLine[] stanza = Verse[LevelIndex];
            int currentLine = beatIndex > 0 && State != PlayState.Complete ? beats[Mathf.Min(progress, beats.Count - 1)].Line : 0;
            for (int l = 0; l < stanza.Length; l++)
            {
                Rect block = new Rect(card.x + 18 + l * 21, card.y + 72, 15, 12);
                if (l == currentLine) PixelUi.Fill(new Rect(block.x - 3, block.y - 3, block.width + 6, block.height + 6), Color.white);
                PixelUi.Fill(block, stanza[l].Rhyme == ' ' ? new Color(0.45f, 0.43f, 0.55f) : RhymeColor(stanza[l].Rhyme));
            }
            if (words == 2) PixelUi.Text(RhymeSchemes[LevelIndex], card.x + 18 + stanza.Length * 21 + 6, card.y + 74, 2, UiMuted);

            // The scoreboard is just your crashes; it pops (and flushes red) when one happens.
            if (Crashes != shownCrashes) { if (Crashes > shownCrashes) crashPopAt = now; shownCrashes = Crashes; }
            bool popping = now - crashPopAt < 0.2f;
            float crashSize = popping ? 9 : 8;
            PixelUi.Text(Crashes.ToString(), width / 2, UiMargin + (9 - crashSize) * 2.5f, crashSize, popping ? new Color(1f, 0.45f, 0.4f) : Color.white, 0.5f);
            if (words == 2) PixelUi.Text(FewestCrashes >= 0 ? $"CRASHES - FEWEST {FewestCrashes}" : "CRASHES", width / 2, UiMargin + 56, 2, UiMuted, 0.5f);

            // A finished line shows its end word in its rhyme colour - and, at first, what it rhymes with.
            float sinceLine = now - lineShownAt;
            if (lineShown >= 0 && sinceLine < 1.8f && State != PlayState.Interlude)
            {
                VerseLine line = stanza[lineShown];
                Color tone = RhymeColor(line.Rhyme);
                tone.a = Mathf.Clamp01((1.8f - sinceLine) / 0.4f);
                float rise = Mathf.Round(Mathf.Clamp01(1 - sinceLine / 0.25f) * 4) * 3;
                if (words == 0) PixelUi.Fill(new Rect(Mathf.Round(width / 2 - 60), 112 + rise, 120, 6), tone);
                else
                {
                    PixelUi.Text(line.EndWord.ToUpperInvariant(), width / 2, 104 + rise, 4, tone, 0.5f);
                    VerseLine partner = null;
                    for (int l = 0; l < lineShown; l++) if (line.Rhyme != ' ' && stanza[l].Rhyme == line.Rhyme) partner = stanza[l];
                    if (words == 2 && partner != null)
                        PixelUi.Text((line.Slant ? "SLANT RHYME WITH " : "RHYMES WITH ") + partner.EndWord.ToUpperInvariant(),
                            width / 2, 132 + rise, 2, new Color(1, 1, 1, tone.a), 0.5f);
                }
            }

            // Hope bar: drains while flying, refills from hearts, blinks red when low.
            Rect bar = new Rect(width - UiMargin - 168, UiMargin + 6, 168, 30);
            Vector2 heartIcon = PixelUi.IconSize("heart");
            PixelUi.Icon("heart", bar.x - heartIcon.x - 6, bar.y + (bar.height - heartIcon.y) / 2);
            PixelUi.Frame(bar, "bar");
            Rect trough = new Rect(bar.x + 9, bar.y + 9, bar.width - 18, bar.height - 18);
            Rect fill = new Rect(trough.x, trough.y, Mathf.Round(trough.width * Mathf.Clamp01(Energy / 100) / 3) * 3, trough.height);
            bool low = Energy < 30;
            bool flash = now - hopeFlashAt < 0.2f;
            if (!low || Mathf.Repeat(now * 4, 1) < 0.6f)
            {
                PixelUi.Fill(fill, flash ? Color.white : low ? new Color(0.91f, 0.31f, 0.23f) : new Color(1f, 0.72f, 0.29f));
                PixelUi.Fill(new Rect(fill.x, fill.y, fill.width, 3), new Color(1, 1, 1, 0.35f));
                PixelUi.Fill(new Rect(fill.x, fill.yMax - 3, fill.width, 3), new Color(0, 0, 0, 0.2f));
            }
            float shine = trough.x + Mathf.Round(Mathf.Repeat(now * 90, trough.width + 90) / 3) * 3 - 45;
            Rect glint = Rect.MinMaxRect(Mathf.Max(fill.x, shine), fill.y, Mathf.Min(fill.xMax, shine + 6), fill.yMax);
            if (glint.width > 0 && !low) PixelUi.Fill(glint, new Color(1, 1, 1, 0.55f));
            if (words == 2) PixelUi.Text(Energy > 0 ? "HOPE" : "HOPE IS QUIET - FIND A HEART", bar.xMax, bar.yMax + 9, 2, low ? new Color(1f, 0.5f, 0.42f) : UiMuted, 1);
            if (sinceCrash < 0.9f && lostHope > 0)
                PixelUi.Text("-" + lostHope, bar.x + fill.width, bar.yMax + 9 + Mathf.Round(sinceCrash * 30 / 3) * 3, 3,
                    new Color(1f, 0.4f, 0.35f, 1 - sinceCrash / 0.9f), 0.5f);

            poemButton = new Rect(width - UiMargin - 84, UiMargin + 64, 84, 36);
            if (Button(poemButton, words > 0 ? "POEM" : "", 2)) SetPoemVisible(true);
            if (words == 0)
            {
                Vector2 quill = PixelUi.IconSize("feather");
                PixelUi.Icon("feather", poemButton.center.x - quill.x / 2, poemButton.center.y - quill.y / 2);
            }

            // Centre messages.
            float bob = Mathf.Round(Mathf.Sin(now * 4) * 2) * 3;
            bool banner = State == PlayState.Ready || now - bannerStart < 2.6f;
            if (banner && words > 0 && (State == PlayState.Ready || State == PlayState.Playing))
            {
                float fade = State == PlayState.Ready ? 1 : Mathf.Clamp01((2.6f - (now - bannerStart)) / 0.5f);
                float slide = State == PlayState.Ready ? 0 : Mathf.Round(Mathf.Clamp01(1 - (now - bannerStart) / 0.25f) * 8) * 3;
                GUI.color = new Color(1, 1, 1, fade);
                string line = LevelLines[LevelIndex].ToUpperInvariant();
                Rect panel = new Rect(0, 214 - slide, Mathf.Max(PixelUi.TextWidth(levelName, 7), PixelUi.TextWidth(line, 2)) + 60, 124);
                panel.x = Mathf.Round((width - panel.width) / 2);
                PixelUi.Frame(panel, "panel");
                if (words == 2) PixelUi.Text("LEVEL " + (LevelIndex + 1), width / 2, panel.y + 18, 2, UiMuted, 0.5f);
                PixelUi.Text(levelName, width / 2, panel.y + 40, 7, accent, 0.5f);
                PixelUi.Text(line, width / 2, panel.y + 90, 2, Color.white, 0.5f);
                GUI.color = Color.white;
            }
            float sinceSurge = now - surgeStart;
            if (LevelIndex == 1 && surgeStart > 0 && sinceSurge < 3f)
            {
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01((3f - sinceSurge) / 0.6f));
                PixelUi.Text("AND SORE MUST BE THE STORM -", width / 2, 150 + Mathf.Round(Mathf.Clamp01(1 - sinceSurge / 0.3f) * 6) * 3, 3,
                    new Color(1f, 0.55f, 0.45f), 0.5f);
                GUI.color = Color.white;
            }
            if (State == PlayState.Ready)
            {
                if (words > 0) PixelUi.Text("SPACE OR CLICK TO FLY", width / 2, 520 + bob, 3, Color.white, 0.5f);
                if (words == 2) PixelUi.Text("FLY TO THE BEAT - DASHES ARE BREATHS, MATCHING COLOURS RHYME", width / 2, 556, 2, UiMuted, 0.5f);
            }
            else if (State == PlayState.GameOver || State == PlayState.AwaitingFlap)
            {
                Rect panel = new Rect(0, 300, words > 0 ? 372 : 150, State == PlayState.AwaitingFlap && words > 0 ? 96 : 66);
                panel.x = Mathf.Round((width - panel.width) / 2);
                PixelUi.Frame(panel, "panel");
                if (words > 0) PixelUi.Text("HOPE KEEPS SINGING...", width / 2, panel.y + 24, 3, accent, 0.5f);
                else
                {
                    // Without words: the song carries on as notes.
                    Vector2 note = PixelUi.IconSize("note");
                    for (int i = 0; i < 3; i++)
                        PixelUi.Icon("note", width / 2 - 1.5f * note.x - 9 + i * (note.x + 9), panel.y + 33 - note.y / 2 + Mathf.Round(Mathf.Sin(now * 5 + i) * 1) * 3);
                }
                if (State == PlayState.AwaitingFlap && words > 0 && Mathf.Repeat(now, 1) < 0.7f)
                    PixelUi.Text("SPACE OR CLICK TO RISE AGAIN", width / 2, panel.y + 60, 2, UiMuted, 0.5f);
            }
            else if (State == PlayState.Complete)
            {
                Rect panel = new Rect(0, 250, 420, 202);
                panel.x = Mathf.Round((width - panel.width) / 2);
                PixelUi.Frame(panel, "panel");
                PixelUi.Text("YOU CARRIED HOPE", width / 2, panel.y + 26, 4, accent, 0.5f);
                PixelUi.Text("THROUGH ALL THREE WORLDS", width / 2, panel.y + 58, 3, Color.white, 0.5f);
                PixelUi.Text($"CRASHES {Crashes}", width / 2, panel.y + 86, 3, Color.white, 0.5f);
                PixelUi.Text(newRecord ? "NEW RECORD - FEWEST YET" : $"FEWEST {FewestCrashes}", width / 2, panel.y + 108,
                    2, newRecord ? accent : UiMuted, 0.5f);
                playAgainButton = new Rect(width / 2 - 96, panel.y + 134, 192, 48);
                if (Button(playAgainButton, "PLAY AGAIN", 3)) SelectLevel(0);
            }

            if (State == PlayState.Interlude && ribbon != null)
            {
                float t = Time.time - interludeStart;
                float alpha = Mathf.Clamp01((t - 0.4f) / 0.4f);
                if (ribbon.AllCaught) alpha *= Mathf.Clamp01(1 - (Time.time - ribbon.LastCatchTime - 1.2f) / 0.4f);
                ribbon.Draw(Time.time, width, LevelAccent[interludeLevel], alpha, ToUi);
                if (t > 0.8f && interludeLevel < 2) PixelUi.Text("SPACE TO SKIP", width / 2, UiHeight - UiMargin - 96, 2, UiMuted, 0.5f);
            }

            // Credits, small, bottom right (gone with the other words on the last level).
            if (words == 0) return;
            const string poemCredit = "POEM BY EMILY DICKINSON";
            float creditWidth = Mathf.Max(PixelUi.TextWidth(poemCredit, 2), PixelUi.TextWidth(CreatorCredit, 2));
            PixelUi.Fill(new Rect(width - UiMargin - creditWidth - 9, UiHeight - UiMargin - 36, creditWidth + 18, 39),
                new Color(0.08f, 0.06f, 0.1f, 0.6f));
            Color credit = new Color(1, 1, 1, 0.9f);
            PixelUi.SmallPrint(poemCredit, width - UiMargin, UiHeight - UiMargin - 30, credit, 1);
            PixelUi.SmallPrint(CreatorCredit, width - UiMargin, UiHeight - UiMargin - 15, credit, 1);
        }

        private void DrawTitle(float width)
        {
            float now = Time.unscaledTime;
            float bob = Mathf.Round(Mathf.Sin(now * 1.6f) * 2) * 3;
            Color gold = LevelAccent[0];
            // Title at the top, buttons at the bottom: the middle stays clear for the bird
            // resting in its nest in the tree.
            PixelUi.Fill(new Rect(0, 40, width, 190), new Color(0.12f, 0.08f, 0.2f, 0.35f));
            Vector2 feather = PixelUi.IconSize("feather");
            PixelUi.Icon("feather", width / 2 - feather.x / 2, 56 + bob);
            PixelUi.Text("THE THING", width / 2, 96 + bob, 10, gold, 0.5f);
            PixelUi.Text("WITH FEATHERS", width / 2, 160 + bob, 7, Color.white, 0.5f);
            PixelUi.Text("A FLIGHT THROUGH EMILY DICKINSON'S POEM", width / 2, 208, 2, UiMuted, 0.5f);

            PixelUi.Fill(new Rect(0, 560, width, 132), new Color(0.12f, 0.08f, 0.2f, 0.35f));
            playButton = new Rect(Mathf.Round(width / 2 - 162), 574, 192, 54);
            if (Button(playButton, "PLAY", 4)) StartGame();
            titlePoemButton = new Rect(playButton.xMax + 12, 574, 120, 54);
            if (Button(titlePoemButton, "POEM", 3)) SetPoemVisible(true);
            string hint = FewestCrashes >= 0 ? $"FEWEST CRASHES {FewestCrashes}  -  OR PRESS SPACE" : "OR PRESS SPACE";
            if (Mathf.Repeat(now, 1.2f) < 0.8f) PixelUi.Text(hint, width / 2, 646, 2, Color.white, 0.5f);

            const string poemCredit = "POEM BY EMILY DICKINSON";
            float creditWidth = Mathf.Max(PixelUi.TextWidth(poemCredit, 2), PixelUi.TextWidth(CreatorCredit, 2));
            PixelUi.Fill(new Rect(width - UiMargin - creditWidth - 9, UiHeight - UiMargin - 36, creditWidth + 18, 39),
                new Color(0.08f, 0.06f, 0.1f, 0.6f));
            PixelUi.SmallPrint(poemCredit, width - UiMargin, UiHeight - UiMargin - 30, new Color(1, 1, 1, 0.9f), 1);
            PixelUi.SmallPrint(CreatorCredit, width - UiMargin, UiHeight - UiMargin - 15, new Color(1, 1, 1, 0.9f), 1);
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
            string label = showingTitle ? "BACK" : "BACK TO THE GAME";
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
