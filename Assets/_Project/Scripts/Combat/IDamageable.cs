using UnityEngine;

namespace OCoveiro.Combat
{
    /// <summary>Lado de cada um no combate. Não existe fogo amigo: golpes só atingem o outro time.</summary>
    public enum Team : byte
    {
        Players,
        Enemies,
    }

    /// <summary>Tudo que leva dano (jogador, inimigo, boneco, objeto quebrável) implementa este contrato.</summary>
    public interface IDamageable
    {
        Team Team { get; }
        Transform transform { get; }

        /// <summary>Só roda no host. Retorna true se o dano foi aplicado.</summary>
        bool ApplyDamage(DamageInfo info);
    }

    /// <summary>
    /// Permite que outro componente recuse um dano (ex.: i-frames da esquiva).
    /// O Health consulta todos os filtros do objeto antes de aplicar o dano.
    /// </summary>
    public interface IDamageFilter
    {
        bool CanReceive(in DamageInfo info);
    }
}
