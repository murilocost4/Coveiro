using OCoveiro.Player;
using UnityEngine;

namespace OCoveiro.World
{
    public enum SpectralMode
    {
        /// <summary>Passarela, ponte, inscrição: só existe sob a luz.</summary>
        AppearsInLight,
        /// <summary>Parede ilusória: some quando a luz bate.</summary>
        VanishesInLight,
    }

    /// <summary>
    /// Objeto que reage à luz das lanternas (pilar de design "enxergar custa").
    /// Cada PC calcula sozinho, a partir das lanternas sincronizadas, então os dois veem igual.
    /// O collider liga e desliga junto: dá para andar na passarela e atravessar a parede ilusória.
    /// </summary>
    public class SpectralObject : MonoBehaviour
    {
        [SerializeField] private SpectralMode mode = SpectralMode.AppearsInLight;
        [Tooltip("Evita piscar quando a luz está bem na borda.")]
        [SerializeField] private float switchDelay = 0.15f;

        private Renderer[] renderers;
        private Collider[] colliders;
        private Bounds bounds;
        private bool present;
        private float pendingSince = -1f;

        public bool IsPresent => present;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);

            bounds = new Bounds(transform.position, Vector3.zero);
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            present = mode == SpectralMode.VanishesInLight; // começa no escuro
            Apply();
        }

        private void Update()
        {
            bool lit = Lantern.IsLit(bounds);
            bool shouldBePresent = mode == SpectralMode.AppearsInLight ? lit : !lit;

            if (shouldBePresent == present)
            {
                pendingSince = -1f;
                return;
            }

            if (pendingSince < 0f) pendingSince = Time.time;
            if (Time.time - pendingSince < switchDelay) return;

            present = shouldBePresent;
            pendingSince = -1f;
            Apply();
        }

        private void Apply()
        {
            foreach (var r in renderers) if (r != null) r.enabled = present;
            foreach (var c in colliders) if (c != null) c.enabled = present;
        }
    }
}
