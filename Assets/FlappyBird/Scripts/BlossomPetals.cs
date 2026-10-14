using UnityEngine;

namespace FlappyBird
{
    // Petals drifting down from the cherry tree. Children of the nest, so they scroll
    // and fade with it. In the Gale they are blown sideways.
    public sealed class BlossomPetals : MonoBehaviour
    {
        private const int Count = 18;
        private FlappyBirdGame game;
        private Transform[] petals;
        private float[] born, sway;
        private Vector2[] from;
        private Vector2 crown;
        private float crownWidth, bottom;

        public void Initialize(FlappyBirdGame owner, Sprite petal, Vector2 crownCenter, float width, float groundBelow)
        {
            game = owner;
            crown = crownCenter;
            crownWidth = width;
            bottom = groundBelow;
            petals = new Transform[Count];
            born = new float[Count];
            sway = new float[Count];
            from = new Vector2[Count];
            for (int i = 0; i < Count; i++)
            {
                SpriteRenderer piece = new GameObject("Petal", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                piece.transform.SetParent(transform, false);
                piece.sprite = petal;
                piece.sortingOrder = 7;
                petals[i] = piece.transform;
                Respawn(i, Time.time - Random.Range(0f, 6f)); // start mid-fall, not all at once
            }
        }

        private void Respawn(int i, float time)
        {
            born[i] = time;
            sway[i] = Random.Range(0f, Mathf.PI * 2);
            from[i] = crown + new Vector2(Random.Range(-crownWidth / 2, crownWidth / 2), Random.Range(-0.2f, 0.4f));
        }

        private void Update()
        {
            bool gale = game.LevelIndex == 1;
            float fall = gale ? 0.9f : 0.45f, drift = gale ? -1.6f : -0.25f;
            for (int i = 0; i < Count; i++)
            {
                float age = Time.time - born[i];
                Vector2 at = from[i] + new Vector2(drift * age + Mathf.Sin(age * 2.2f + sway[i]) * 0.18f, -fall * age);
                if (at.y < bottom) { Respawn(i, Time.time); at = from[i]; }
                petals[i].localPosition = at;
                petals[i].localRotation = Quaternion.Euler(0, 0, Mathf.Sin(age * 3 + sway[i]) * 40);
            }
        }
    }
}
