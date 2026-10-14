using UnityEngine;

namespace FlappyBird
{
    // A loose feather knocked off the bird: flies out, tumbles, falls and fades.
    public sealed class FeatherPuff : MonoBehaviour
    {
        private Vector2 velocity;
        private float spin, age, life;
        private SpriteRenderer art;

        public static void Burst(Vector3 at, int count, Sprite sprite, Sprite square)
        {
            for (int i = 0; i < count; i++)
            {
                SpriteRenderer piece = new GameObject("Feather", typeof(SpriteRenderer), typeof(FeatherPuff)).GetComponent<SpriteRenderer>();
                piece.transform.position = at;
                piece.sortingOrder = 6;
                if (sprite != null) piece.sprite = sprite;
                else
                {
                    piece.sprite = square;
                    piece.color = new Color(0.79f, 0.58f, 0.35f);
                    piece.transform.localScale = new Vector3(0.12f, 0.06f, 1);
                }
                FeatherPuff feather = piece.GetComponent<FeatherPuff>();
                feather.art = piece;
                float angle = Random.Range(0f, Mathf.PI * 2);
                feather.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.7f + 0.6f) * Random.Range(1.5f, 3.5f);
                feather.spin = Random.Range(-540f, 540f);
                feather.life = Random.Range(0.8f, 1.3f);
            }
        }

        private void Update()
        {
            age += Time.deltaTime;
            velocity.y -= 5f * Time.deltaTime;
            velocity.x *= 1 - 1.5f * Time.deltaTime; // air drag
            transform.position += (Vector3)(velocity * Time.deltaTime);
            transform.Rotate(0, 0, spin * Time.deltaTime);
            Color c = art.color;
            art.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(1 - age / life) * 1.5f);
            if (age >= life) Destroy(gameObject);
        }
    }
}
