using UnityEngine;

namespace OCoveiro.AI
{
    /// <summary>Motor do Espectro: flutua em linha reta até o destino e atravessa paredes.</summary>
    public class GhostMotor : MonoBehaviour, IEnemyMotor
    {
        [SerializeField] private float impulseDecay = 10f;
        [SerializeField] private float moveTurnSpeed = 540f;

        private bool active;
        private bool moving;
        private Vector3 destination;
        private float speed;
        private Vector3 impulse;

        public void Initialize(bool isServer) => active = isServer;

        public void MoveTo(Vector3 target, float moveSpeed)
        {
            destination = target;
            destination.y = transform.position.y;
            speed = moveSpeed;
            moving = true;
        }

        public void Stop() => moving = false;

        public void FaceTowards(Vector3 point, float degreesPerSecond)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            var target = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, degreesPerSecond * Time.deltaTime);
        }

        public void AddImpulse(Vector3 velocity)
        {
            velocity.y = 0f;
            impulse += velocity;
        }

        public void Warp(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            impulse = Vector3.zero;
            moving = false;
        }

        public bool HasArrived(float tolerance)
        {
            Vector3 offset = destination - transform.position;
            offset.y = 0f;
            return !moving || offset.sqrMagnitude <= tolerance * tolerance;
        }

        private void Update()
        {
            if (!active) return;

            float dt = Time.deltaTime;
            Vector3 step = impulse * dt;

            if (moving)
            {
                Vector3 toTarget = destination - transform.position;
                toTarget.y = 0f;
                float distance = toTarget.magnitude;
                if (distance > 0.05f)
                {
                    Vector3 direction = toTarget / distance;
                    step += direction * Mathf.Min(speed * dt, distance);
                    FaceTowards(transform.position + direction, moveTurnSpeed);
                }
            }

            transform.position += step;
            impulse = Vector3.MoveTowards(impulse, Vector3.zero, impulseDecay * dt);
        }
    }
}
