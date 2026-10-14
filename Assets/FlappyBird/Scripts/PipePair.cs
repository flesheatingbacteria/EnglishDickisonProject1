using UnityEngine;

namespace FlappyBird
{
    public sealed class PipePair : MonoBehaviour
    {
        private FlappyBirdGame game;
        private bool scored;
        // Which line of the stanza this pipe's beat belongs to, and whether it ends the line.
        public int Line = -1;
        public bool LineEnd;
        public void Initialize(FlappyBirdGame owner) { game = owner; }

        private void Update()
        {
            // Pipes ride the world's eased speed, so they slow, stop and start with everything else.
            transform.position += Vector3.left * game.WorldSpeed * Time.deltaTime;
            if (!scored && transform.position.x + (game.pipeWidth + 0.14f) * 0.5f < game.Bird.transform.position.x)
            {
                scored = true;
                game.AddScore(this);
            }
            if (transform.position.x < -game.HalfWidth - 2) Destroy(gameObject);
        }
    }
}
