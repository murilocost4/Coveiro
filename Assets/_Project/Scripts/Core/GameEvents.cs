using System;
using OCoveiro.Player;
using UnityEngine;

namespace OCoveiro.Core
{
    /// <summary>
    /// Barramento de eventos do jogo (padrão Observer).
    /// Gameplay dispara, interface/áudio/save escutam — ninguém se referencia diretamente.
    /// Novos eventos (OnBossDefeated, OnAltarRest, OnRiteDone...) entram aqui nas próximas semanas.
    /// </summary>
    public static class GameEvents
    {
        public static event Action<PlayerAppearance> OnPlayerSpawned;
        public static event Action<PlayerAppearance> OnPlayerDespawned;

        public static void RaisePlayerSpawned(PlayerAppearance player) => OnPlayerSpawned?.Invoke(player);
        public static void RaisePlayerDespawned(PlayerAppearance player) => OnPlayerDespawned?.Invoke(player);

        // Com "Enter Play Mode" rápido (sem recarregar o domínio), eventos estáticos sobreviveriam
        // entre um Play e outro. Isto limpa tudo a cada Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OnPlayerSpawned = null;
            OnPlayerDespawned = null;
        }
    }
}
