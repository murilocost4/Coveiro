using OCoveiro.Data;
using Unity.Netcode;
using UnityEngine;

namespace OCoveiro.Player
{
    /// <summary>
    /// Stamina do jogador. Autoridade: o DONO (gastar stamina não pode esperar a rede).
    /// O valor é copiado para uma NetworkVariable só quando muda pelo menos 1 ponto,
    /// para o parceiro poder ver sem lotar a rede.
    /// </summary>
    public class PlayerStamina : NetworkBehaviour
    {
        [SerializeField] private PlayerStatsData stats;

        private readonly NetworkVariable<float> netStamina = new NetworkVariable<float>(
            100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private float current;
        private float regenBlockedUntil;

        public float Max => stats != null ? stats.maxStamina : 100f;
        public float Current => IsOwner ? current : netStamina.Value;
        public float Normalized => Max > 0f ? Current / Max : 0f;

        /// <summary>Regra souls-like: com qualquer stamina dá para agir; com zero, não.</summary>
        public bool HasAny => Current > 0.01f;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;
            current = Max;
            netStamina.Value = current;
        }

        /// <summary>Gasta stamina (pode zerar, nunca fica negativa). Falha se já estiver zerada.</summary>
        public bool TrySpend(float amount)
        {
            if (!IsOwner || !HasAny) return false;

            current = Mathf.Max(0f, current - amount);
            regenBlockedUntil = Time.time + (stats != null ? stats.staminaRegenDelay : 0.8f);
            Sync(true);
            return true;
        }

        public void RefillFull()
        {
            if (!IsOwner) return;
            current = Max;
            Sync(true);
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || stats == null) return;
            if (Time.time < regenBlockedUntil || current >= Max) return;

            current = Mathf.Min(Max, current + stats.staminaRegenPerSecond * Time.deltaTime);
            Sync(false);
        }

        private void Sync(bool force)
        {
            float delta = Mathf.Abs(netStamina.Value - current);
            if (delta < 0.001f) return;
            if (force || delta >= 1f || current >= Max) netStamina.Value = current;
        }
    }
}
