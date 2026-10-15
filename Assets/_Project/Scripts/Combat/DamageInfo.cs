using UnityEngine;

namespace OCoveiro.Combat
{
    /// <summary>Tudo o que um golpe carrega. Montado e consumido só no host.</summary>
    public struct DamageInfo
    {
        public float Amount;
        /// <summary>Direção do golpe, do atacante para o alvo (no chão).</summary>
        public Vector3 Direction;
        public float Knockback;
        public bool Heavy;
        public bool Stagger;
        public Team SourceTeam;
        public ulong SourceObjectId;
    }
}
