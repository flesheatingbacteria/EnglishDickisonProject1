using UnityEngine;

namespace FlappyBird
{
    // A heart of hope: touching it refills the bird's energy. Moves with its pipe pair.
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class HopeHeart : MonoBehaviour
    {
        private FlappyBirdGame game;
        private SpriteRenderer art;
        private Vector3 home;
        private float phase, takenAt = -1;

        public void Initialize(FlappyBirdGame owner)
        {
            game = owner;
            art = GetComponent<SpriteRenderer>();
            home = transform.localPosition;
            phase = Random.value * Mathf.PI * 2;
            CircleCollider2D trigger = GetComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.22f / Mathf.Max(0.01f, transform.localScale.x);
        }

        private void Update()
        {
            if (takenAt >= 0)
            {
                // Pop and fade, then disappear.
                float t = (Time.time - takenAt) / 0.25f;
                transform.localScale = Vector3.one * (1 + t * 0.6f);
                if (art != null) art.color = new Color(1, 1, 1, 1 - t);
                if (t >= 1) Destroy(gameObject);
                return;
            }
            if (game.State == FlappyBirdGame.PlayState.Playing)
                transform.localPosition = home + Vector3.up * (Mathf.Round(Mathf.Sin(Time.time * 3 + phase) * 3) / PixelArt.PixelsPerUnit);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (takenAt >= 0 || other.GetComponent<BirdController>() == null) return;
            takenAt = Time.time;
            GetComponent<Collider2D>().enabled = false;
            game.CollectHope();
        }
    }
}
