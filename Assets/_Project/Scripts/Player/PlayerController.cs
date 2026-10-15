using OCoveiro.Core;
using Unity.Netcode;
using UnityEngine;

namespace OCoveiro.Player
{
    /// <summary>
    /// Movimento do jogador em rede.
    /// Só o DONO do personagem move; a posição chega no outro jogador pelo OwnerNetworkTransform
    /// (autoridade do dono = andar e esquivar respondem na hora, sem esperar a rede).
    /// O PlayerCombat controla este componente (trava, desacelera, dá impulso, esquiva).
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputReader))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Movimento")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSpeed = 720f; // graus por segundo
        [SerializeField] private float gravity = -20f;
        [Tooltip("Quão rápido empurrões (knockback, passo do golpe) perdem força.")]
        [SerializeField] private float impulseDecay = 18f;

        [Header("Spawn")]
        [Tooltip("Distância lateral entre o Coveiro e a Aprendiz ao nascer.")]
        [SerializeField] private float spawnSpacing = 2f;

        [Header("Opcional")]
        [Tooltip("Animator do modelo. Precisa de um parâmetro float 'Speed'.")]
        [SerializeField] private Animator animator;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private CharacterController controller;
        private PlayerInputReader input;
        private Transform cameraTransform;
        private float verticalVelocity;

        private Vector3 impulse;
        private Vector3 dashVelocity;
        private float dashDuration;
        private float dashTimeLeft;

        /// <summary>Parado (golpe, atordoado, caído). Empurrões ainda funcionam.</summary>
        public bool MovementLocked { get; set; }
        /// <summary>O combate decide para onde o personagem olha (mira, golpe).</summary>
        public bool RotationLocked { get; set; }
        /// <summary>1 = normal. Carregando golpe ou bebendo frasco deixa mais lento.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        public bool IsDashing => dashTimeLeft > 0f;
        /// <summary>Direção pedida pelo input, no chão e relativa à câmera.</summary>
        public Vector3 MoveDirection { get; private set; }
        public Vector3 CurrentVelocity { get; private set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<PlayerInputReader>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;

            // Host (id 0) nasce na origem; o convidado nasce ao lado.
            Teleport(new Vector3(OwnerClientId * spawnSpacing, 0f, 0f));

            var cam = Camera.main;
            if (cam == null) return;
            cameraTransform = cam.transform;
            if (cam.TryGetComponent(out IsoCamera iso)) iso.SetTarget(transform);
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || input == null) return;

            float dt = Time.deltaTime;
            MoveDirection = ToWorldDirection(input.Move);

            Vector3 planar;
            if (IsDashing)
            {
                // Esquiva: arranca rápido e desacelera no fim (a média dá a distância certa).
                dashTimeLeft -= dt;
                float t = 1f - Mathf.Clamp01(dashTimeLeft / dashDuration);
                planar = dashVelocity * Mathf.Lerp(1.6f, 0.4f, t);
            }
            else if (MovementLocked)
            {
                planar = Vector3.zero;
            }
            else
            {
                planar = MoveDirection * (moveSpeed * SpeedMultiplier);
                if (!RotationLocked && MoveDirection.sqrMagnitude > 0.001f) Face(MoveDirection, false);
            }

            planar += impulse;
            impulse = Vector3.MoveTowards(impulse, Vector3.zero, impulseDecay * dt);

            // Gravidade simples: o jogo não tem pulo.
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * dt;

            CurrentVelocity = planar;
            controller.Move((planar + Vector3.up * verticalVelocity) * dt);

            if (animator != null) animator.SetFloat(SpeedHash, planar.magnitude);
        }

        /// <summary>Vira o personagem para uma direção no chão.</summary>
        public void Face(Vector3 direction, bool instant)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;

            var target = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = instant
                ? target
                : Quaternion.RotateTowards(transform.rotation, target, rotationSpeed * Time.deltaTime);
        }

        /// <summary>Rolamento: percorre a distância no tempo dado, ignorando o input.</summary>
        public void StartDash(Vector3 direction, float distance, float duration)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;
            direction.Normalize();

            dashDuration = Mathf.Max(0.01f, duration);
            dashTimeLeft = dashDuration;
            dashVelocity = direction * (distance / dashDuration);
            impulse = Vector3.zero;
            Face(direction, true);
        }

        public void StopDash() => dashTimeLeft = 0f;

        /// <summary>Empurrão que some sozinho (knockback, passo do golpe).</summary>
        public void AddImpulse(Vector3 velocity)
        {
            velocity.y = 0f;
            impulse += velocity;
        }

        public void Teleport(Vector3 position)
        {
            controller.enabled = false; // o CharacterController ignora teleportes se estiver ligado
            transform.position = position;
            controller.enabled = true;
            impulse = Vector3.zero;
            dashTimeLeft = 0f;
        }

        /// <summary>Converte um input 2D numa direção no chão, relativa à câmera isométrica.</summary>
        public Vector3 ToWorldDirection(Vector2 value)
        {
            if (value.sqrMagnitude < 0.0001f) return Vector3.zero;

            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;
            if (cameraTransform != null)
            {
                forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            }

            return Vector3.ClampMagnitude(forward * value.y + right * value.x, 1f);
        }
    }
}
