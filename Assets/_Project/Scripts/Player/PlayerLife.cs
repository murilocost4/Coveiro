using System.Collections.Generic;
using OCoveiro.Combat;
using OCoveiro.Core;
using OCoveiro.Data;
using Unity.Netcode;
using UnityEngine;

namespace OCoveiro.Player
{
    /// <summary>
    /// Estado "caído" do jogador. Autoridade: o HOST.
    /// PROVISÓRIO (semana 2): o jogador levanta sozinho depois de alguns segundos.
    /// Na semana 4 vira o sistema do GDD: o parceiro reanima; os dois caídos voltam ao altar.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerLife : NetworkBehaviour
    {
        [SerializeField] private PlayerStatsData stats;

        private readonly NetworkVariable<bool> downed = new NetworkVariable<bool>(false);

        private Health health;
        private CharacterAnimator anim;
        private float downedAt;
        private readonly List<Transform> bodies = new List<Transform>();
        private float tilt;

        public bool IsDowned => downed.Value;

        private bool Animated => anim != null && anim.HasAnimator;

        private void Awake()
        {
            health = GetComponent<Health>();
            anim = GetComponent<CharacterAnimator>();

            // Os corpos (Visual_Coveiro / Visual_Aprendiz) tombam quando o jogador cai.
            foreach (Transform child in transform)
                if (child.name.StartsWith("Visual_")) bodies.Add(child);
        }

        public override void OnNetworkSpawn()
        {
            downed.OnValueChanged += OnDownedChanged;
            if (IsServer) health.DiedServer += OnDiedServer;
        }

        public override void OnNetworkDespawn()
        {
            downed.OnValueChanged -= OnDownedChanged;
            if (IsServer) health.DiedServer -= OnDiedServer;
        }

        /// <summary>Em todos os PCs: cai (animação de morte) ou levanta.</summary>
        private void OnDownedChanged(bool previous, bool current)
        {
            if (!Animated) return;
            anim.Play(current ? ShovelVisual.Death : ShovelVisual.StandUp, 0.1f);
        }

        private void OnDiedServer()
        {
            downed.Value = true;
            downedAt = Time.time;
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsServer && downed.Value)
            {
                float wait = stats != null ? stats.downedDuration : 3f;
                if (Time.time >= downedAt + wait)
                {
                    health.RestoreFull();
                    downed.Value = false;
                }
            }

            if (Animated) return; // com modelo, quem tomba é a animação

            // Visual em todos os PCs (cápsulas): tomba e levanta suavemente.
            tilt = Mathf.MoveTowards(tilt, downed.Value ? 1f : 0f, Time.deltaTime * 4f);
            var rotation = Quaternion.Euler(tilt * 80f, 0f, 0f);
            foreach (var body in bodies) body.localRotation = rotation;
        }
    }
}
