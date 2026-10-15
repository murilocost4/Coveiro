using System.Collections.Generic;
using OCoveiro.Core;
using Unity.Netcode;
using UnityEngine;

namespace OCoveiro.Player
{
    /// <summary>
    /// Decide quem é quem: o dono do host é o Coveiro, o convidado é a Aprendiz.
    /// Um único prefab de jogador, dois visuais. Também mantém a lista dos jogadores na partida.
    /// </summary>
    public class PlayerAppearance : NetworkBehaviour
    {
        private static readonly List<PlayerAppearance> all = new List<PlayerAppearance>();

        [SerializeField] private GameObject coveiroVisual;
        [SerializeField] private GameObject aprendizVisual;
        [SerializeField] private Light lanternLight;

        [Header("Cor da lanterna")]
        [SerializeField] private Color coveiroLantern = new Color(1f, 0.62f, 0.28f);
        [SerializeField] private Color aprendizLantern = new Color(1f, 0.85f, 0.55f);

        /// <summary>Jogadores na partida (no máximo 2).</summary>
        public static IReadOnlyList<PlayerAppearance> All => all;

        /// <summary>True se este personagem pertence ao host.</summary>
        public bool IsCoveiro => OwnerClientId == NetworkManager.ServerClientId;
        public string DisplayName => IsCoveiro ? "Coveiro" : "Aprendiz";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();

        public override void OnNetworkSpawn()
        {
            bool coveiro = IsCoveiro;

            if (coveiroVisual != null) coveiroVisual.SetActive(coveiro);
            if (aprendizVisual != null) aprendizVisual.SetActive(!coveiro);
            if (lanternLight != null) lanternLight.color = coveiro ? coveiroLantern : aprendizLantern;

            gameObject.name = coveiro ? "Coveiro (host)" : "Aprendiz (convidado)";
            if (!all.Contains(this)) all.Add(this);
            GameEvents.RaisePlayerSpawned(this);
        }

        public override void OnNetworkDespawn()
        {
            all.Remove(this);
            GameEvents.RaisePlayerDespawned(this);
        }
    }
}
