using Unity.AI.Navigation;
using UnityEngine;

namespace OCoveiro.World
{
    /// <summary>
    /// Gera o NavMesh (o "mapa de caminhos" dos inimigos) quando a cena abre.
    /// Para áreas pequenas é instantâneo e evita ter que lembrar de "bakear" a cada mudança no mapa.
    /// Inimigos, jogadores e objetos espectrais ficam de fora (NavMeshModifier "Ignore From Build").
    /// </summary>
    [RequireComponent(typeof(NavMeshSurface))]
    public class RuntimeNavMeshBaker : MonoBehaviour
    {
        private void Awake()
        {
            var surface = GetComponent<NavMeshSurface>();
            if (surface.navMeshData == null) surface.BuildNavMesh();
        }
    }
}
