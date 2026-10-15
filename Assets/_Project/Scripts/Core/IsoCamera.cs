using UnityEngine;

namespace OCoveiro.Core
{
    /// <summary>
    /// Câmera isométrica fixa que segue o jogador local, com tremor (screen shake).
    /// Cada jogador tem a sua câmera; ela segue só o personagem do dono.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class IsoCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float distance = 18f;
        [SerializeField, Range(20f, 80f)] private float pitch = 50f; // inclinação
        [SerializeField] private float yaw = 45f;                    // giro em volta do jogador
        [SerializeField] private float focusHeight = 1f;             // mira no peito, não no pé
        [SerializeField] private float smoothTime = 0.12f;

        [Header("Tremor")]
        [SerializeField] private float shakeFrequency = 28f;

        private static IsoCamera instance;

        private Vector3 velocity;
        private Vector3 followPosition;
        private float shakeStrength;
        private float shakeDuration;
        private float shakeTimeLeft;

        private Quaternion Rotation => Quaternion.Euler(pitch, yaw, 0f);
        private Vector3 Focus => target.position + Vector3.up * focusHeight;
        private Vector3 DesiredPosition => Focus + Rotation * new Vector3(0f, 0f, -distance);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        /// <summary>Treme a câmera do jogador local (força em metros, duração em segundos).</summary>
        public static void Shake(float strength, float duration)
        {
            if (instance == null) return;
            // Um tremor mais forte substitui um mais fraco; nunca somam até ficar enjoativo.
            if (strength < instance.shakeStrength * (instance.shakeTimeLeft / Mathf.Max(0.0001f, instance.shakeDuration))) return;
            instance.shakeStrength = strength;
            instance.shakeDuration = Mathf.Max(0.01f, duration);
            instance.shakeTimeLeft = instance.shakeDuration;
        }

        private void Awake()
        {
            instance = this;
            followPosition = transform.position;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target == null) return;
            followPosition = DesiredPosition; // sem "voo" inicial
            velocity = Vector3.zero;
            transform.SetPositionAndRotation(followPosition, Rotation);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            followPosition = Vector3.SmoothDamp(followPosition, DesiredPosition, ref velocity, smoothTime);
            transform.SetPositionAndRotation(followPosition + ShakeOffset(), Rotation);
        }

        private Vector3 ShakeOffset()
        {
            if (shakeTimeLeft <= 0f) return Vector3.zero;

            shakeTimeLeft -= Time.deltaTime;
            float fade = Mathf.Clamp01(shakeTimeLeft / shakeDuration);
            float t = Time.time * shakeFrequency;
            var offset = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f) * 2f;
            return transform.rotation * offset * (shakeStrength * fade);
        }
    }
}
