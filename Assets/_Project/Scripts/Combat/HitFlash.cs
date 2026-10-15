using UnityEngine;

namespace OCoveiro.Combat
{
    /// <summary>
    /// Pisca o objeto de branco quando leva dano. Roda em todos os PCs (é só visual).
    /// Usa MaterialPropertyBlock para não criar cópias dos materiais.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class HitFlash : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private float duration = 0.08f;

        private Health health;
        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private float flashUntil;
        private bool flashing;

        private void Awake()
        {
            health = GetComponent<Health>();
            renderers = GetComponentsInChildren<Renderer>(true);
            block = new MaterialPropertyBlock();
        }

        private void OnEnable() => health.Damaged += OnDamaged;

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            if (flashing) Apply(false);
        }

        private void OnDamaged(float amount, Vector3 direction, bool heavy)
        {
            flashUntil = Time.time + (heavy ? duration * 1.5f : duration);
            Apply(true);
        }

        private void Update()
        {
            if (flashing && Time.time >= flashUntil) Apply(false);
        }

        private void Apply(bool on)
        {
            flashing = on;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (!on)
                {
                    r.SetPropertyBlock(null);
                    continue;
                }

                block.Clear();
                block.SetColor(BaseColorId, flashColor);
                block.SetColor(ColorId, flashColor);
                r.SetPropertyBlock(block);
            }
        }
    }
}
