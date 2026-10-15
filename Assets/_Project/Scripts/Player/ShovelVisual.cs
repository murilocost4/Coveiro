using System.Collections.Generic;
using OCoveiro.Data;
using UnityEngine;

namespace OCoveiro.Player
{
    /// <summary>
    /// Animação PROVISÓRIA da pá feita por código (gira o objeto "Pa").
    /// Serve para enxergar os golpes antes das animações do Mixamo.
    /// Roda em todos os PCs: o dono toca na hora, o parceiro recebe por RPC.
    /// </summary>
    public class ShovelVisual : MonoBehaviour
    {
        [SerializeField] private Transform pivot;

        private static readonly Quaternion Rest = Quaternion.Euler(65f, 20f, 0f);
        private static readonly Quaternion ChargePose = Quaternion.Euler(-120f, 0f, 0f);

        private readonly struct Segment
        {
            public readonly Quaternion Target;
            public readonly float Duration;

            public Segment(Quaternion target, float duration)
            {
                Target = target;
                Duration = duration;
            }
        }

        private readonly Queue<Segment> queue = new Queue<Segment>();
        private Segment current;
        private bool hasSegment;
        private Quaternion segmentStart;
        private float segmentTime;

        private float freezeUntil;
        private float pulse;
        private Vector3 baseScale = Vector3.one;

        private void Awake()
        {
            if (pivot == null) pivot = transform.Find("Pa");
            if (pivot == null) return;
            baseScale = pivot.localScale;
            pivot.localRotation = Rest;
        }

        public void PlayAttack(AttackKind kind, int comboStep, WeaponData.AttackData attack)
        {
            GetPoses(kind, comboStep, out var from, out var to);
            Clear();
            Enqueue(from, attack.windup);
            Enqueue(to, attack.active);
            Enqueue(to, attack.recovery * 0.5f);
            Enqueue(Rest, attack.recovery * 0.5f);
        }

        public void SetCharging(bool charging)
        {
            Clear();
            Enqueue(charging ? ChargePose : Rest, 0.18f);
        }

        /// <summary>Golpe pesado carregado: a pá "pulsa".</summary>
        public void PulseCharged() => pulse = 1f;

        /// <summary>Hit-stop: congela a pá por um instante quando o golpe acerta.</summary>
        public void Freeze(float seconds) => freezeUntil = Time.time + seconds;

        public void ResetPose()
        {
            Clear();
            Enqueue(Rest, 0.12f);
        }

        private static void GetPoses(AttackKind kind, int step, out Quaternion from, out Quaternion to)
        {
            if (kind == AttackKind.Heavy)
            {
                from = Quaternion.Euler(-125f, 0f, 0f); // por cima da cabeça
                to = Quaternion.Euler(80f, 0f, 0f);     // pancada no chão
                return;
            }

            switch (step)
            {
                case 0: // da esquerda para a direita
                    from = Quaternion.Euler(15f, -90f, 0f);
                    to = Quaternion.Euler(15f, 75f, 0f);
                    break;
                case 1: // volta da direita para a esquerda
                    from = Quaternion.Euler(15f, 75f, 0f);
                    to = Quaternion.Euler(15f, -90f, 0f);
                    break;
                default: // finalizador de cima para baixo
                    from = Quaternion.Euler(-100f, 0f, 0f);
                    to = Quaternion.Euler(70f, 0f, 0f);
                    break;
            }
        }

        private void Clear()
        {
            queue.Clear();
            hasSegment = false;
        }

        private void Enqueue(Quaternion target, float duration) => queue.Enqueue(new Segment(target, duration));

        private void Update()
        {
            if (pivot == null) return;

            if (pulse > 0f)
            {
                pulse = Mathf.Max(0f, pulse - Time.deltaTime * 6f);
                pivot.localScale = baseScale * (1f + 0.25f * pulse);
            }

            if (Time.time < freezeUntil) return;

            if (!hasSegment)
            {
                if (queue.Count == 0) return;
                current = queue.Dequeue();
                segmentStart = pivot.localRotation;
                segmentTime = 0f;
                hasSegment = true;
            }

            segmentTime += Time.deltaTime;
            float t = current.Duration <= 0f ? 1f : Mathf.Clamp01(segmentTime / current.Duration);
            t = t * t * (3f - 2f * t); // suaviza início e fim
            pivot.localRotation = Quaternion.Slerp(segmentStart, current.Target, t);

            if (segmentTime >= current.Duration) hasSegment = false;
        }
    }
}
