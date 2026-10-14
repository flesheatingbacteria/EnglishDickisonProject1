using UnityEngine;

namespace FlappyBird
{
    public sealed class PipePair : MonoBehaviour
    {
        private FlappyBirdGame game;
        private bool scored;
        public void Initialize(FlappyBirdGame owner) { game = owner; }

        private void Update()
        {
            if (!game.WorldMoving) return;
            transform.position += Vector3.left * game.CurrentSpeed * Time.deltaTime;
            if (!scored && transform.position.x + (game.pipeWidth + 0.14f) * 0.5f < game.Bird.transform.position.x)
            {
                scored = true;
                game.AddScore();
            }
            if (transform.position.x < -game.HalfWidth - 2) Destroy(gameObject);
        }
    }
}
