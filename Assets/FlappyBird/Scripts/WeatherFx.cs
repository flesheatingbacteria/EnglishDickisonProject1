using UnityEngine;

namespace FlappyBird
{
    // Rain, snow and drifting storm clouds. Each kind has an intensity from 0 to 1, and
    // weather arrives from the right edge (the way the bird is flying) and leaves to the left.
    // Decorative only: nothing here has a collider.
    public sealed class WeatherFx : MonoBehaviour
    {
        private const int Drops = 70, Flakes = 60, Clouds = 6;
        // Per level: rain, snow, clouds.
        private static readonly Vector3[] LevelWeather = { new Vector3(0, 0, 0), new Vector3(1, 0, 1), new Vector3(0, 1, 0) };

        private FlappyBirdGame game;
        private Transform[] drops, flakes, clouds;
        private SpriteRenderer[] cloudArt;
        private Vector3 intensity;
        private float left, right, top, floor;

        public void Initialize(FlappyBirdGame owner, Sprite square, float floorY)
        {
            game = owner;
            floor = floorY;
            float px = 1 / PixelArt.PixelsPerUnit;
            drops = new Transform[Drops];
            for (int i = 0; i < Drops; i++)
                drops[i] = Piece("Rain", square, new Vector2(px, px * 5), new Color(0.67f, 0.82f, 1f, 0.6f), -3);
            flakes = new Transform[Flakes];
            for (int i = 0; i < Flakes; i++)
                flakes[i] = Piece("Snow", square, new Vector2(px * 2, px * 2), new Color(1, 1, 1, 0.9f), -3);
            Sprite cloud = PixelArt.Prop("storm_cloud", new Vector2(0.5f, 0.5f));
            clouds = new Transform[Clouds];
            cloudArt = new SpriteRenderer[Clouds];
            for (int i = 0; i < Clouds; i++)
            {
                clouds[i] = cloud != null
                    ? Piece("Rolling Cloud", cloud, Vector2.one * 2, new Color(0.75f, 0.78f, 0.88f, 1), -5)
                    : Piece("Rolling Cloud", square, new Vector2(2.6f, 1f), new Color(0.3f, 0.33f, 0.42f, 1), -5);
                cloudArt[i] = clouds[i].GetComponent<SpriteRenderer>();
            }
        }

        private Transform Piece(string label, Sprite sprite, Vector2 size, Color color, int order)
        {
            SpriteRenderer piece = new GameObject(label, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            piece.transform.SetParent(transform, false);
            piece.transform.localScale = new Vector3(size.x, size.y, 1);
            piece.sprite = sprite;
            piece.color = color;
            piece.sortingOrder = order;
            return piece.transform;
        }

        public void Set(int level) => intensity = LevelWeather[level];

        // Part-way between two levels' weather, for the cutscene between them.
        public void Blend(int from, int to, float amount) => intensity = Vector3.Lerp(LevelWeather[from], LevelWeather[to], amount);

        private void Update()
        {
            Camera view = Camera.main;
            if (view == null) return;
            right = view.orthographicSize * view.aspect + 1;
            left = -right;
            top = view.orthographicSize + 0.5f;
            float time = Time.time, span = right - left, height = top - floor;
            float drift = game.WorldMoving ? 1 : 0.3f;

            for (int i = 0; i < Drops; i++)
            {
                float fall = Mathf.Repeat(time * 1.1f + i * 0.618f, 1);
                float x = left + Mathf.Repeat(i * 0.37f * span - time * 2.5f * drift - fall * 2, span);
                Place(drops[i], x, top - fall * height, intensity.x);
            }
            for (int i = 0; i < Flakes; i++)
            {
                float fall = Mathf.Repeat(time * 0.18f + i * 0.618f, 1);
                float x = left + Mathf.Repeat(i * 0.53f * span - time * 0.8f * drift + Mathf.Sin(time * 1.3f + i) * 0.3f, span);
                Place(flakes[i], x, top - fall * height, intensity.y);
            }
            for (int i = 0; i < Clouds; i++)
            {
                float x = left + Mathf.Repeat(i * span / Clouds - time * 0.7f * drift, span);
                Place(clouds[i], x, top - 1.1f - (i % 3) * 0.35f, intensity.z);
                Color c = cloudArt[i].color;
                cloudArt[i].color = new Color(c.r, c.g, c.b, Mathf.Clamp01(intensity.z * 1.5f) * 0.95f);
            }
        }

        // Weather only shows ahead of a front that sweeps in from the right as intensity rises.
        private void Place(Transform piece, float x, float y, float amount)
        {
            bool shown = amount > 0.001f && x >= Mathf.Lerp(right, left, amount);
            if (piece.gameObject.activeSelf != shown) piece.gameObject.SetActive(shown);
            if (shown) piece.localPosition = new Vector3(x, y, 0);
        }
    }
}
