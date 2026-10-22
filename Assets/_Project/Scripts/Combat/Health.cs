using System;
using Unity.Netcode;
using UnityEngine;

namespace OCoveiro.Combat
{
    /// <summary>
    /// Vida genérica em rede (jogador, inimigo, boneco).
    /// Autoridade: o HOST. Só ele altera a vida; os outros só leem a NetworkVariable.
    /// </summary>
    public class Health : NetworkBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private Team team = Team.Enemies;

        private readonly NetworkVariable<float> current = new NetworkVariable<float>(100f);
        private readonly NetworkVariable<float> max = new NetworkVariable<float>(100f);

        private IDamageFilter[] filters;
        private bool maxOverridden;

        /// <summary>Disparado em TODOS os PCs quando o dano entra: (quantidade, direção, pesado).</summary>
        public event Action<float, Vector3, bool> Damaged;
        /// <summary>Disparado só no host, com todos os detalhes do golpe (empurrão, atordoar, quem bateu).</summary>
        public event Action<DamageInfo> DamagedServer;
        /// <summary>Disparado só no host quando a vida zera.</summary>
        public event Action DiedServer;

        public Team Team => team;
        public float Current => current.Value;
        public float Max => max.Value;
        public float Normalized => max.Value > 0f ? current.Value / max.Value : 0f;
        public bool IsDead => current.Value <= 0f;

        private void Awake()
        {
            filters = GetComponents<IDamageFilter>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer || maxOverridden) return;
            max.Value = maxHealth;
            current.Value = maxHealth;
        }

        /// <summary>
        /// Host: define a vida máxima (ex.: vinda do PlayerStatsData ou da escala do co-op).
        /// refill = vida cheia; senão mantém a mesma porcentagem de vida.
        /// </summary>
        public void SetMax(float value, bool refill = true)
        {
            if (!IsServer) return;
            maxOverridden = true;

            float ratio = refill ? 1f : Normalized;
            max.Value = Mathf.Max(1f, value);
            current.Value = max.Value * ratio;
        }

        public bool ApplyDamage(DamageInfo info)
        {
            if (!IsServer || IsDead || info.Amount <= 0f) return false;

            foreach (var filter in filters)
                if (!filter.CanReceive(info)) return false;

            current.Value = Mathf.Max(0f, current.Value - info.Amount);
            DamagedRpc(info.Amount, info.Direction, info.Heavy);
            DamagedServer?.Invoke(info);

            if (IsDead) DiedServer?.Invoke();
            return true;
        }

        public void Heal(float amount)
        {
            if (!IsServer || IsDead) return;
            current.Value = Mathf.Min(max.Value, current.Value + amount);
        }

        public void RestoreFull()
        {
            if (!IsServer) return;
            current.Value = max.Value;
        }

        // O host avisa todo mundo para tocar o feedback (flash, empurrão, tremor de câmera).
        [Rpc(SendTo.Everyone)]
        private void DamagedRpc(float amount, Vector3 direction, bool heavy)
        {
            Damaged?.Invoke(amount, direction, heavy);
        }
    }
}
