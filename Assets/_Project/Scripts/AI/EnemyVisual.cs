using OCoveiro.Combat;
using OCoveiro.Core;
using OCoveiro.Data;
using OCoveiro.Player;
using UnityEngine;

namespace OCoveiro.AI
{
    /// <summary>
    /// Visual do inimigo, em todos os PCs, a partir do estado sincronizado pelo EnemyBrain.
    /// - Com modelo animado: cada estado vira uma animação (o golpe acerta exatamente no fim do aviso).
    /// - Sem modelo (formas simples): braço e corpo são girados por código, como na semana 3.
    /// Nos dois casos: o corpo fica vermelho durante o aviso, pisca ao apanhar,
    /// e o Espectro só aparece dentro da luz de uma lanterna.
    /// </summary>
    [RequireComponent(typeof(EnemyBrain))]
    public class EnemyVisual : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // Momento do impacto dentro dos clipes de golpe (fração do clipe).
        private const float ImpactMoment = 0.42f;

        [SerializeField] private Transform body;
        [SerializeField] private Transform arm;

        [Header("Cores")]
        [SerializeField] private Color telegraphColor = new Color(1f, 0.22f, 0.08f);
        [SerializeField] private Color alertColor = new Color(1f, 0.82f, 0.25f);
        [SerializeField] private Color flashColor = Color.white;
        [Tooltip("Acima de 1 clareia até modelos com textura.")]
        [SerializeField] private float flashIntensity = 2.5f;
        [SerializeField] private float flashDuration = 0.08f;

        private static readonly Quaternion ArmRest = Quaternion.Euler(60f, 0f, 0f);
        private static readonly Quaternion ArmRaised = Quaternion.Euler(-85f, 0f, 0f);
        private static readonly Quaternion ArmStrike = Quaternion.Euler(80f, 0f, 0f);

        private EnemyBrain brain;
        private Health health;
        private CharacterAnimator anim;
        private Renderer[] renderers;
        private Color[] baseColors;
        private MaterialPropertyBlock block;
        private Vector3 bodyBasePosition;
        private Quaternion bodyBaseRotation;
        private Vector3 bodyBaseScale;
        private float flashUntil;
        private bool visible = true;
        private EnemyStateId lastState = (EnemyStateId)255;
        private float lastStateChange = -1f;

        private bool Animated => anim != null && anim.HasAnimator;

        private void Awake()
        {
            brain = GetComponent<EnemyBrain>();
            health = GetComponent<Health>();
            anim = GetComponent<CharacterAnimator>();
            if (body == null) body = transform.Find("Visual");

            renderers = GetComponentsInChildren<Renderer>(true);
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var material = renderers[i].sharedMaterial;
                baseColors[i] = material == null ? Color.gray
                    : material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : material.color;
            }

            block = new MaterialPropertyBlock();
            if (body != null)
            {
                bodyBasePosition = body.localPosition;
                bodyBaseRotation = body.localRotation;
                bodyBaseScale = body.localScale;
            }
        }

        private void OnEnable() => health.Damaged += OnDamaged;
        private void OnDisable() => health.Damaged -= OnDamaged;

        private void OnDamaged(float amount, Vector3 direction, bool heavy) => flashUntil = Time.time + flashDuration;

        private void Update()
        {
            var data = brain.Data;
            if (data == null || !brain.IsSpawned) return;

            float t = Time.time - brain.StateChangedAt;
            var state = brain.StateId;

            bool show = state != EnemyStateId.Dead || t < (Animated ? 4f : 1.4f);
            if (data.spectral) show &= Lantern.IsLit(transform.position);
            SetVisible(show);

            if (state != lastState || !Mathf.Approximately(brain.StateChangedAt, lastStateChange))
            {
                lastState = state;
                lastStateChange = brain.StateChangedAt;
                if (Animated) PlayStateAnimation(state, data);
            }

            if (!show) return;

            Color tint = Color.white;
            float tintAmount = 0f;

            if (state == EnemyStateId.Attack && t < data.attackWindup)
            {
                float p = t / data.attackWindup;
                tint = telegraphColor;
                tintAmount = p * (0.65f + 0.35f * Mathf.Sin(t * 28f)); // pisca mais forte perto do golpe
            }
            else if (state == EnemyStateId.Alert)
            {
                tint = alertColor;
                tintAmount = 0.6f * (1f - Mathf.Clamp01(t / Mathf.Max(0.01f, data.alertDuration)));
            }

            if (!Animated) UpdateProceduralPose(state, t, data);
            ApplyColors(tint, tintAmount);
        }

        /// <summary>Cada estado da IA vira uma animação.</summary>
        private void PlayStateAnimation(EnemyStateId state, EnemyData data)
        {
            switch (state)
            {
                case EnemyStateId.Attack:
                    // O impacto da animação cai exatamente no fim do aviso.
                    anim.PlayToMoment(data.attackAnimation, ImpactMoment, data.attackWindup + data.attackActive * 0.5f);
                    break;
                case EnemyStateId.Alert:
                    anim.PlayFitted(data.alertAnimation, data.alertDuration);
                    break;
                case EnemyStateId.Stagger:
                    anim.PlayFitted(data.staggerAnimation, data.staggerDuration, 0.05f);
                    break;
                case EnemyStateId.Dead:
                    anim.Play(data.deathAnimation, 0.05f);
                    break;
                default:
                    anim.ReturnToLocomotion();
                    break;
            }
        }

        /// <summary>Sem modelo: braço e corpo girados por código.</summary>
        private void UpdateProceduralPose(EnemyStateId state, float t, EnemyData data)
        {
            Quaternion armTarget = ArmRest;
            Quaternion bodyTilt = Quaternion.identity;
            Vector3 bodyOffset = Vector3.zero;
            float scale = 1f;

            switch (state)
            {
                case EnemyStateId.Attack:
                {
                    float windup = data.attackWindup;
                    if (t < windup)
                    {
                        float p = t / windup;
                        armTarget = Quaternion.Slerp(ArmRest, ArmRaised, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, p * 1.4f)));
                        bodyTilt = Quaternion.Euler(-8f * p, 0f, 0f);
                    }
                    else if (t < windup + data.attackActive + 0.08f)
                    {
                        armTarget = ArmStrike;
                        bodyTilt = Quaternion.Euler(12f, 0f, 0f);
                    }
                    else
                    {
                        float r = Mathf.Clamp01((t - windup - data.attackActive) / Mathf.Max(0.01f, data.attackRecovery));
                        armTarget = Quaternion.Slerp(ArmStrike, ArmRest, r);
                    }
                    break;
                }
                case EnemyStateId.Alert:
                    scale = 1f + 0.12f * Mathf.Abs(Mathf.Sin(t * 22f)) * (1f - Mathf.Clamp01(t / Mathf.Max(0.01f, data.alertDuration)));
                    armTarget = ArmRaised;
                    break;
                case EnemyStateId.Stagger:
                {
                    float fade = 1f - Mathf.Clamp01(t / Mathf.Max(0.01f, data.staggerDuration));
                    bodyTilt = Quaternion.Euler(-22f * fade + 6f * Mathf.Sin(t * 18f) * fade, 0f, 0f);
                    break;
                }
                case EnemyStateId.Dead:
                {
                    float fall = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.5f));
                    float sink = Mathf.Clamp01((t - 0.7f) / 0.7f);
                    bodyTilt = Quaternion.Euler(-85f * fall, 0f, 0f);
                    bodyOffset = Vector3.down * (0.8f * sink);
                    break;
                }
            }

            if (body != null)
            {
                body.localRotation = bodyBaseRotation * bodyTilt;
                body.localPosition = bodyBasePosition + bodyOffset;
                body.localScale = bodyBaseScale * scale;
            }

            if (arm != null)
                arm.localRotation = Quaternion.Slerp(arm.localRotation, armTarget, 1f - Mathf.Exp(-20f * Time.deltaTime));
        }

        private void ApplyColors(Color tint, float amount)
        {
            bool flash = Time.time < flashUntil;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                Color color = flash ? flashColor * flashIntensity : Color.Lerp(baseColors[i], tint, amount);
                color.a = baseColors[i].a; // mantém a transparência do Espectro
                block.Clear();
                block.SetColor(BaseColorId, color);
                block.SetColor(ColorId, color);
                r.SetPropertyBlock(block);
            }
        }

        private void SetVisible(bool value)
        {
            if (visible == value) return;
            visible = value;
            foreach (var r in renderers)
                if (r != null) r.enabled = value;
        }
    }
}
