using UnityEngine;

namespace OCoveiro.Data
{
    /// <summary>
    /// Atributos do jogador: vida, stamina, esquiva, frascos e lanterna.
    /// O Coveiro e a Aprendiz usam o mesmo asset (mesmo moveset, como no GDD).
    /// </summary>
    [CreateAssetMenu(menuName = "O Coveiro/Atributos do Jogador", fileName = "AtributosJogador")]
    public class PlayerStatsData : ScriptableObject
    {
        [Header("Vida")]
        public float maxHealth = 100f;
        [Tooltip("Tempo travado ao levar um golpe comum.")]
        public float hitStun = 0.3f;
        [Tooltip("Tempo travado ao levar um golpe pesado.")]
        public float heavyHitStun = 0.5f;
        [Tooltip("Empurrão que o jogador sofre ao levar um golpe.")]
        public float hitKnockback = 5f;

        [Header("Stamina")]
        public float maxStamina = 100f;
        public float staminaRegenPerSecond = 30f;
        [Tooltip("Espera depois de gastar stamina para ela voltar a regenerar.")]
        public float staminaRegenDelay = 0.8f;

        [Header("Esquiva")]
        public float dodgeCost = 20f;
        public float dodgeDistance = 4.5f;
        public float dodgeDuration = 0.35f;
        [Tooltip("Invencibilidade (i-frames) desde o início da esquiva.")]
        public float iFrameDuration = 0.3f;
        [Tooltip("Pequena pausa depois da esquiva antes de poder agir de novo.")]
        public float dodgeRecovery = 0.08f;

        [Header("Frascos de cura")]
        public int flaskCount = 3;
        public float flaskHeal = 40f;
        [Tooltip("Tempo bebendo, vulnerável. A cura entra no fim.")]
        public float flaskUseTime = 1f;
        [Range(0f, 1f)] public float healMoveMultiplier = 0.3f;

        [Header("Lanterna")]
        public float maxOil = 100f;
        public float oilDrainPerSecond = 4f;
        [Tooltip("Raio da luz. Também é o raio que revela Espectros e dá bônus de dano.")]
        public float lanternRadius = 7f;
        public float lanternIntensity = 3f;

        [Header("Caído (provisório)")]
        [Tooltip("Semana 2: o jogador caído levanta sozinho depois deste tempo. Na semana 4 vira o sistema de reanimar.")]
        public float downedDuration = 3f;
    }
}
