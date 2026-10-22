using OCoveiro.Combat;
using OCoveiro.Player;
using UnityEngine;

namespace OCoveiro.AI
{
    /// <summary>
    /// Visual PROVISÓRIO do inimigo, em todos os PCs, a partir do estado sincronizado:
    /// - Ataque: o braço sobe e o corpo vai ficando vermelho (o aviso para esquivar).
    /// - Grito do Vigia: pulsa em amarelo.
    /// - Cambaleio, queda ao morrer, flash branco ao apanhar.
    /// - Espectro: só aparece dentro da luz de uma lanterna.
    /// Quando chegarem os modelos animados, isto vira parâmetros de um Animator.
    /// </summary>
    [RequireComponent(typeof(EnemyBrain))]
    public class EnemyVisual : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private Transform body;
        [SerializeField] private Transform arm;

        [Header("Cores")]
        [SerializeField] private Color telegraphColor = new Color(1f, 0.22f, 0.08f);
        [SerializeField] private Color alertColor = new Color(1f, 0.82f, 0.25f);
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private float flashDuration = 0.08f;

        private static readonly Quaternion ArmRest = Quaternion.Euler(60f, 0f, 0f);
        private static readonly Quaternion ArmRaised = Quaternion.Euler(-85f, 0f, 0f);
        private static readonly Quaternion ArmStrike = Quaternion.Euler(80f, 0f, 0f);

        private EnemyBrain brain;
        private Health health;
        private Renderer[] renderers;
        private Color[] baseColors;
        private MaterialPropertyBlock block;
        private Vector3 bodyBasePosition;
        private Quaternion bodyBaseRotation;
        private Vector3 bodyBaseScale;
        private float flashUntil;
        private bool visible = true;

        private void Awake()
        {
            brain = GetComponent<EnemyBrain>();
            health = GetComponent<Health>();
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

            bool show = state != EnemyStateId.Dead || t < 1.4f;
            if (data.spectral) show &= Lantern.IsLit(transform.position);
            SetVisible(show);
            if (!show) return;

            Quaternion armTarget = ArmRest;
            Quaternion bodyTilt = Quaternion.identity;
            Vector3 bodyOffset = Vector3.zero;
            float scale = 1f;
            Color tint = Color.white;
            float tintAmount = 0f;

            switch (state)
            {
                case EnemyStateId.Attack:
                {
                    float windup = data.attackWindup;
                    if (t < windup)
                    {
                        float p = t / windup;
                        armTarget = Quaternion.Slerp(ArmRest, ArmRaised, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, p * 1.4f)));
                        tint = telegraphColor;
                        tintAmount = p * (0.65f + 0.35f * Mathf.Sin(t * 28f)); // pisca mais forte perto do golpe
                        bodyTilt = Quaternion.Euler(-8f * p, 0f, 0f);    // inclina para trás, carregando
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
                {
                    float fade = 1f - Mathf.Clamp01(t / Mathf.Max(0.01f, data.alertDuration));
                    scale = 1f + 0.12f * Mathf.Abs(Mathf.Sin(t * 22f)) * fade;
                    armTarget = ArmRaised;
                    tint = alertColor;
                    tintAmount = 0.6f * fade;
                    break;
                }
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

            ApplyColors(tint, tintAmount);
        }

        private void ApplyColors(Color tint, float amount)
        {
            bool flash = Time.time < flashUntil;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                Color color = flash ? flashColor : Color.Lerp(baseColors[i], tint, amount);
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
