using UnityEngine;
using UnityEngine.AI;

namespace OCoveiro.AI
{
    /// <summary>Motor de inimigo que anda pelo NavMesh. O NavMeshAgent só fica ligado no host.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class NavMeshMotor : MonoBehaviour, IEnemyMotor
    {
        [SerializeField] private float impulseDecay = 14f;

        private NavMeshAgent agent;
        private bool active;
        private Vector3 impulse;

        private bool Ready => active && agent.enabled && agent.isOnNavMesh;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        public void Initialize(bool isServer)
        {
            active = isServer;
            agent.enabled = isServer; // nos clientes quem move é o NetworkTransform
            if (!isServer) return;

            agent.updateRotation = true;
            if (NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas))
                agent.Warp(hit.position);
            else
                Debug.LogWarning($"[IA] {name} não está em cima do NavMesh. O chão tem o NavMeshSurface?");
        }

        public void MoveTo(Vector3 destination, float speed)
        {
            if (!Ready) return;
            agent.speed = speed;
            agent.isStopped = false;
            if (!agent.hasPath || (agent.destination - destination).sqrMagnitude > 0.09f)
                agent.SetDestination(destination);
        }

        public void Stop()
        {
            if (!Ready) return;
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

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
            if (Ready) agent.Warp(position);
            else transform.position = position;
            transform.rotation = rotation;
            impulse = Vector3.zero;
        }

        public bool HasArrived(float tolerance) =>
            !Ready || (!agent.pathPending && agent.remainingDistance <= tolerance);

        private void Update()
        {
            if (!Ready || impulse.sqrMagnitude < 0.0001f) return;
            agent.Move(impulse * Time.deltaTime); // empurrão respeitando as bordas do NavMesh
            impulse = Vector3.MoveTowards(impulse, Vector3.zero, impulseDecay * Time.deltaTime);
        }
    }
}
