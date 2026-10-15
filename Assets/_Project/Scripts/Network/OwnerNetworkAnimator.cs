using Unity.Netcode.Components;
using UnityEngine;

namespace OCoveiro.Network
{
    /// <summary>
    /// NetworkAnimator em que o dono manda nos parâmetros de animação.
    /// Entra no prefab quando os modelos animados chegarem.
    /// </summary>
    [DisallowMultipleComponent]
    public class OwnerNetworkAnimator : NetworkAnimator
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
