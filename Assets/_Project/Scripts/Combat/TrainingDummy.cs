using Unity.Netcode;
using UnityEngine;

namespace OCoveiro.Combat
{
    /// <summary>
    /// Boneco de treino: balança quando apanha e recupera a vida sozinho.
    /// Serve para testar o "peso" do combate antes dos inimigos (semana 3).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class TrainingDummy : NetworkBehaviour
    {
        [SerializeField] private Transform visual;
        [Tooltip("Segundos sem apanhar até recuperar a vida.")]
        [SerializeField] private float resetDelay = 2.5f;

        [Header("Balanço")]
        [SerializeField] private float lightTilt = 14f;
        [SerializeField] private float heavyTilt = 32f;
        [SerializeField] private float wobbleSpeed = 16f;
        [SerializeField] private float wobbleDamping = 5f;

        private Health health;
        private float lastHitTime = -100f;
        private Vector3 tiltAxis = Vector3.right;
        private float tiltAmount;

        private void Awake()
        {
            health = GetComponent<Health>();
            if (visual == null) visual = transform.Find("Visual");
        }

        public override void OnNetworkSpawn() => health.Damaged += OnDamaged;
        public override void OnNetworkDespawn() => health.Damaged -= OnDamaged;

        private void OnDamaged(float amount, Vector3 direction, bool heavy)
        {
            lastHitTime = Time.time;

            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) tiltAxis = Vector3.Cross(Vector3.up, direction.normalized);
            tiltAmount = Mathf.Max(tiltAmount, heavy ? heavyTilt : lightTilt);

            if (IsServer)
                Debug.Log($"[Treino] {name} levou {amount:0.#} de dano{(heavy ? " (pesado)" : "")} — vida {health.Current:0}/{health.Max:0}");
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsServer && health.Current < health.Max && Time.time - lastHitTime > resetDelay)
                health.RestoreFull();

            if (visual == null) return;
            tiltAmount *= Mathf.Exp(-wobbleDamping * Time.deltaTime);
            float angle = tiltAmount * Mathf.Cos((Time.time - lastHitTime) * wobbleSpeed);
            visual.localRotation = Quaternion.AngleAxis(angle, tiltAxis);
        }
    }
}
