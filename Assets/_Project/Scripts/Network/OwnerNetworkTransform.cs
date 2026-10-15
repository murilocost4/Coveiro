using Unity.Netcode.Components;
using UnityEngine;

namespace OCoveiro.Network
{
    /// <summary>
    /// NetworkTransform em que o DONO manda na posição (o padrão do Netcode é o host mandar).
    /// Usado no jogador: o movimento responde na hora, sem esperar a rede.
    /// </summary>
    [DisallowMultipleComponent]
    public class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
