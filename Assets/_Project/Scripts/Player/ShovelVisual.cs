using System.Collections.Generic;
using OCoveiro.Combat;
using OCoveiro.Core;
using OCoveiro.Data;
using UnityEngine;

namespace OCoveiro.Player
{
    /// <summary>
    /// Visual das ações do jogador. Roda em todos os PCs: o dono toca na hora, o parceiro recebe por RPC.
    /// - Com modelo animado (CharacterAnimator): toca as animações KayKit, ajustadas aos tempos do combate.
    /// - Sem modelo (cápsulas): gira o objeto "Pa" por código, como na semana 2.
    /// </summary>
    public class ShovelVisual : MonoBehaviour
    {
        // Nomes das animações (estados do Animator Controller do jogador).
        public const string LightAttack1 = "Melee_1H_Attack_Slice_Horizontal";
        public const string LightAttack2 = "Melee_1H_Attack_Slice_Diagonal";
        public const string LightAttack3 = "Melee_1H_Attack_Chop";
        public const string HeavyAttack = "Melee_2H_Attack_Chop";
        public const string Dodge = "Dodge_Forward";
        public const string Drink = "Use_Item";
        public const string Hit = "Hit_A";
        public const string Death = "Death_A";
        public const string StandUp = "Lie_StandUp";

        // Momento do impacto dentro dos clipes de golpe (fração do clipe).
        private const float ImpactMoment = 0.42f;
        // Durante a carga, o golpe pesado para com a pá lá em cima.
        private const float ChargeHoldMoment = 0.28f;

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

        private CharacterAnimator anim;
        private Health health;
        private bool charging;

        private bool Animated => anim != null && anim.HasAnimator;

        private void Awake()
        {
            anim = GetComponent<CharacterAnimator>();
            health = GetComponent<Health>();
            if (pivot == null) pivot = transform.Find("Pa");
            if (pivot == null) return;
            baseScale = pivot.localScale;
            if (!Animated) pivot.localRotation = Rest;
        }

        private void OnEnable()
        {
            if (health != null) health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        // ------------------------------------------------------------------ ações

        public void PlayAttack(AttackKind kind, int comboStep, WeaponData.AttackData attack)
        {
            if (Animated)
            {
                float toImpact = attack.windup + attack.active;
                if (kind == AttackKind.Heavy)
                    anim.PlayToMoment(HeavyAttack, ImpactMoment, toImpact, charging ? ChargeHoldMoment : 0f, 0.05f);
                else
                    anim.PlayToMoment(comboStep == 0 ? LightAttack1 : comboStep == 1 ? LightAttack2 : LightAttack3,
                        ImpactMoment, toImpact);
                charging = false;
                return;
            }

            GetPoses(kind, comboStep, out var from, out var to);
            Clear();
            Enqueue(from, attack.windup);
            Enqueue(to, attack.active);
            Enqueue(to, attack.recovery * 0.5f);
            Enqueue(Rest, attack.recovery * 0.5f);
        }

        public void SetCharging(bool value)
        {
            charging = value;
            if (Animated)
            {
                // Levanta a pá (início do golpe pesado) e segura lá em cima.
                if (value) anim.PlayToMoment(HeavyAttack, ChargeHoldMoment, 0.35f);
                else anim.ReturnToLocomotion();
                return;
            }

            Clear();
            Enqueue(value ? ChargePose : Rest, 0.18f);
        }

        public void PlayDodge(float seconds)
        {
            if (Animated) anim.PlayFitted(Dodge, seconds, 0.05f);
        }

        public void PlayDrink(float seconds)
        {
            if (Animated) anim.PlayFitted(Drink, seconds);
        }

        /// <summary>Golpe pesado carregado: a pá "pulsa".</summary>
        public void PulseCharged() => pulse = 1f;

        /// <summary>Hit-stop: congela a pá (ou a animação inteira) por um instante quando o golpe acerta.</summary>
        public void Freeze(float seconds)
        {
            if (Animated) anim.Freeze(seconds);
            freezeUntil = Time.time + seconds;
        }

        public void ResetPose()
        {
            if (Animated) return; // a animação volta sozinha para andar/parado
            Clear();
            Enqueue(Rest, 0.12f);
        }

        private void OnDamaged(float amount, Vector3 direction, bool heavy)
        {
            charging = false;
            if (Animated && health != null && !health.IsDead) anim.Play(Hit, 0.05f);
        }

        // ------------------------------------------------------------------ modo sem modelo (cápsulas)

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

            if (Animated)
            {
                // Segura a pá no alto enquanto o golpe pesado carrega.
                if (charging && anim.NormalizedTime(HeavyAttack) >= ChargeHoldMoment) anim.SetActionSpeed(0f);
                return; // a pá segue a mão (HandAttachment)
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
