using UnityEngine;

namespace OCoveiro.Data
{
    public enum EnemyMovement
    {
        /// <summary>Anda pelo NavMesh, contornando paredes.</summary>
        Walker,
        /// <summary>Flutua em linha reta e atravessa paredes (Espectro).</summary>
        Ghost,
    }

    /// <summary>
    /// Dados de um tipo de inimigo. Um inimigo novo = um asset novo + um prefab;
    /// o código da IA (EnemyBrain) é o mesmo para todos.
    /// </summary>
    [CreateAssetMenu(menuName = "O Coveiro/Inimigo", fileName = "NovoInimigo")]
    public class EnemyData : ScriptableObject
    {
        public string displayName = "Errante";

        [Header("Vida")]
        public float maxHealth = 30f;
        [Tooltip("Com 2 jogadores, a vida é multiplicada por este valor (GDD: 1,5).")]
        public float coopHealthMultiplier = 1.5f;
        [Tooltip("Dano acumulado em pouco tempo que faz o inimigo cambalear. Golpes pesados sempre fazem.")]
        public float poise = 25f;
        [Tooltip("Tempo sem apanhar até o acúmulo de dano (poise) zerar.")]
        public float poiseResetTime = 1.5f;
        public float staggerDuration = 0.7f;
        [Range(0f, 1f)] public float knockbackResistance;

        [Header("Movimento")]
        public EnemyMovement movement = EnemyMovement.Walker;
        public float chaseSpeed = 2.2f;
        [Tooltip("Graus por segundo ao virar para o alvo.")]
        public float turnSpeed = 240f;

        [Header("Percepção")]
        public float detectionRadius = 8f;
        [Tooltip("Longe disto o inimigo desiste e volta para o lugar dele.")]
        public float loseRadius = 14f;
        [Tooltip("Precisa enxergar o jogador (paredes bloqueiam).")]
        public bool needsLineOfSight = true;

        [Header("Ataque")]
        [Tooltip("Distância em que para de andar e começa o golpe.")]
        public float attackRange = 1.7f;
        [Tooltip("Aviso antes do golpe (o braço sobe e o corpo fica vermelho). É a janela para esquivar.")]
        public float attackWindup = 0.9f;
        public float attackActive = 0.12f;
        public float attackRecovery = 0.8f;
        [Tooltip("Pausa mínima entre um golpe e outro.")]
        public float attackCooldown = 0.8f;
        public float attackDamage = 15f;
        public float attackReach = 2f;
        [Range(10f, 360f)] public float attackArc = 100f;
        public float attackKnockback = 5f;
        [Tooltip("Passo à frente no momento do golpe.")]
        public float attackLunge = 2f;
        public bool heavyAttack;

        [Header("Especiais")]
        [Tooltip("Espectro: só aparece e só leva dano dentro da luz de uma lanterna.")]
        public bool spectral;
        [Tooltip("Tenta chegar pelas costas do jogador.")]
        public bool flankFromBehind;
        [Tooltip("Errante Vigia: ao ver um jogador, grita e chama os inimigos próximos.")]
        public bool alertsOthers;
        public float alertRadius = 14f;
        public float alertDuration = 1.1f;
    }
}
