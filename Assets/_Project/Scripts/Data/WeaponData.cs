using System;
using UnityEngine;

namespace OCoveiro.Data
{
    public enum AttackKind : byte
    {
        Light,
        Heavy,
    }

    /// <summary>
    /// Dados de uma arma (a pá). Balancear o combate = mexer neste asset, sem tocar no código.
    /// Os valores padrão vêm do GDD e são pontos de partida para os testes.
    /// </summary>
    [CreateAssetMenu(menuName = "O Coveiro/Arma", fileName = "NovaArma")]
    public class WeaponData : ScriptableObject
    {
        [Serializable]
        public class AttackData
        {
            [Tooltip("Dano base do golpe.")]
            public float damage = 10f;
            [Tooltip("Stamina gasta ao iniciar o golpe.")]
            public float staminaCost = 12f;

            [Header("Tempos (segundos)")]
            [Tooltip("Preparação: o golpe ainda não acerta.")]
            public float windup = 0.12f;
            [Tooltip("Golpe ativo: o acerto é calculado no início desta fase.")]
            public float active = 0.08f;
            [Tooltip("Recuperação: o jogador fica preso no golpe (a esquiva cancela).")]
            public float recovery = 0.3f;
            [Tooltip("Depois do golpe ativo, quanto esperar até poder emendar o próximo golpe do combo.")]
            public float chainDelay = 0.1f;

            [Header("Alcance")]
            public float reach = 2f;
            [Tooltip("Abertura do golpe em graus, na frente do jogador.")]
            [Range(10f, 360f)] public float arcAngle = 140f;

            [Header("Impacto")]
            [Tooltip("Empurrão aplicado no alvo.")]
            public float knockback = 2.5f;
            [Tooltip("Passo à frente que o jogador dá ao golpear.")]
            public float lunge = 2.5f;
            [Tooltip("Golpe atordoa o alvo (inimigos reagem a partir da semana 3).")]
            public bool stagger;
        }

        public string displayName = "Pá";

        [Header("Ataque leve (combo)")]
        public AttackData light = new AttackData();
        [Min(1)] public int comboLength = 3;
        [Tooltip("O último golpe do combo empurra mais.")]
        public float finisherKnockbackMultiplier = 2f;

        [Header("Ataque pesado (segurar)")]
        public float heavyChargeTime = 0.6f;
        [Range(0f, 1f)] public float chargeMoveMultiplier = 0.35f;
        public AttackData heavy = new AttackData
        {
            damage = 30f,
            staminaCost = 30f,
            windup = 0.1f,
            active = 0.1f,
            recovery = 0.55f,
            chainDelay = 0f,
            reach = 2.3f,
            arcAngle = 120f,
            knockback = 7f,
            lunge = 4f,
            stagger = true,
        };

        [Header("Lanterna")]
        [Tooltip("Inimigos dentro da luz de uma lanterna levam mais dano (+25% no GDD).")]
        public float litDamageMultiplier = 1.25f;

        public AttackData Get(AttackKind kind) => kind == AttackKind.Heavy ? heavy : light;
    }
}
