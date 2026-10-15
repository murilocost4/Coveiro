using UnityEngine;

namespace OCoveiro.Network
{
    /// <summary>
    /// Menu de conexão TEMPORÁRIO (OnGUI) só para testar a rede na semana 1.
    /// Na semana 3 é substituído pelo menu de verdade (uGUI + Relay).
    /// </summary>
    [RequireComponent(typeof(ConnectionManager))]
    public class ConnectionDebugUI : MonoBehaviour
    {
        private ConnectionManager connection;
        private string hostAddress = "127.0.0.1";

        private void Awake() => connection = GetComponent<ConnectionManager>();

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16, 16, 260, 220), GUI.skin.box);
            GUILayout.Label("O Coveiro — teste de rede");

            if (!connection.IsRunning)
            {
                if (GUILayout.Button("Jogar solo")) connection.StartSolo();
                if (GUILayout.Button("Hospedar (Coveiro)")) connection.StartHost();

                GUILayout.Space(8);
                GUILayout.Label("IP do host:");
                hostAddress = GUILayout.TextField(hostAddress);
                if (GUILayout.Button("Entrar (Aprendiz)")) connection.StartClient(hostAddress.Trim());
            }
            else if (GUILayout.Button("Sair da partida"))
            {
                connection.Leave();
            }

            GUILayout.Space(8);
            GUILayout.Label(connection.Status);
            GUILayout.EndArea();
        }
    }
}
