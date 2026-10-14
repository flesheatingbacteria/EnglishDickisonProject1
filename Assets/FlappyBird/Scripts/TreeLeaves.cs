using UnityEngine;

namespace FlappyBird
{
    // What falls from the nest's tree, matched to the weather: summer leaves drift,
    // storm-torn leaves are whipped sideways (and the crown sways), snow drops slowly.
    // Children of the nest, so they scroll and fade with it.
    public sealed class TreeLeaves : MonoBehaviour
    {
        private const int Count = 18;
        private FlappyBirdGame game;
        private Sprite[] kinds;
        private Transform crown;
        private SpriteRenderer[] leaves;
        private float[] born, sway;
        private Vector2[] from;
        private Vector2 centre;
        private float width, bottom;
        private int shownLevel = -1;

        public void Initialize(FlappyBirdGame owner, Sprite[] perLevel, Transform crownArt, Vector2 crownCentre, float crownWidth, float groundBelow)
        {
            game = owner;
            kinds = perLevel;
            crown = crownArt;
            centre = crownCentre;
            width = crownWidth;
            bottom = groundBelow;
            leaves = new SpriteRenderer[Count];
            born = new float[Count];
            sway = new float[Count];
            from = new Vector2[Count];
            for (int i = 0; i < Count; i++)
            {
                leaves[i] = new GameObject("Falling Leaf", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                leaves[i].transform.SetParent(transform, false);
                leaves[i].sortingOrder = 7;
                Respawn(i, Time.time - Random.Range(0f, 6f)); // start mid-fall, not all at once
            }
        }

        private void Respawn(int i, float time)
        {
            born[i] = time;
            sway[i] = Random.Range(0f, Mathf.PI * 2);
            from[i] = centre + new Vector2(Random.Range(-width / 2, width / 2), Random.Range(-0.2f, 0.4f));
            leaves[i].sprite = kinds[Mathf.Clamp(game.LevelIndex, 0, kinds.Length - 1)];
        }

        private void Update()
        {
            int level = game.LevelIndex;
            if (level != shownLevel)
            {
                // New world: every falling leaf changes to match it at once.
                shownLevel = level;
                foreach (SpriteRenderer leaf in leaves) leaf.sprite = kinds[Mathf.Clamp(level, 0, kinds.Length - 1)];
            }
            bool storm = level == 1, snow = level == 2;
            float fall = storm ? 0.9f : snow ? 0.35f : 0.45f;
            float drift = storm ? -1.8f : snow ? -0.1f : -0.25f;
            float flutter = snow ? 0.08f : 0.18f;
            for (int i = 0; i < Count; i++)
            {
                float age = Time.time - born[i];
                Vector2 at = from[i] + new Vector2(drift * age + Mathf.Sin(age * 2.2f + sway[i]) * flutter, -fall * age);
                if (at.y < bottom) { Respawn(i, Time.time); at = from[i]; }
                leaves[i].transform.localPosition = at;
                leaves[i].transform.localRotation = Quaternion.Euler(0, 0, snow ? 0 : Mathf.Sin(age * 3 + sway[i]) * (storm ? 90 : 40));
            }
            // The storm tree rocks in the wind.
            if (crown != null) crown.localRotation = Quaternion.Euler(0, 0, storm ? 3 + Mathf.Sin(Time.time * 2.6f) * 4 : 0);
        }
    }
}
