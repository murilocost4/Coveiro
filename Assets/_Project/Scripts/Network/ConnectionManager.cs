using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace OCoveiro.Network
{
    /// <summary>
    /// Ponto único para iniciar e encerrar partidas.
    /// Semana 1: conexão direta por IP (mesmo PC ou mesma rede).
    /// Semana 3: ganha o Relay (código de entrada pela internet) sem mudar quem chama.
    /// </summary>
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public class ConnectionManager : MonoBehaviour
    {
        public const int MaxPlayers = 2;

        [SerializeField] private ushort port = 7777;

        private NetworkManager networkManager;
        private UnityTransport transport;

        public string Status { get; private set; } = "Desconectado";
        public event Action<string> OnStatusChanged;

        public bool IsRunning => networkManager != null && networkManager.IsListening;

        private void Awake()
        {
            networkManager = GetComponent<NetworkManager>();
            transport = GetComponent<UnityTransport>();

            // Aprovação de conexão: limita a sala a 2 jogadores.
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback = ApproveConnection;
        }

        private void OnEnable()
        {
            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        private void OnDisable()
        {
            if (networkManager == null) return;
            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        /// <summary>Solo = host que só aceita conexões da própria máquina.</summary>
        public bool StartSolo()
        {
            transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
            return Begin(networkManager.StartHost(), "Jogando solo");
        }

        /// <summary>Hospeda escutando em todas as interfaces (permite outro PC da rede).</summary>
        public bool StartHost()
        {
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            return Begin(networkManager.StartHost(), $"Hospedando na porta {port}");
        }

        public bool StartClient(string hostAddress)
        {
            transport.SetConnectionData(hostAddress, port);
            return Begin(networkManager.StartClient(), $"Conectando a {hostAddress}...");
        }

        public void Leave()
        {
            networkManager.Shutdown();
            SetStatus("Desconectado");
        }

        private bool Begin(bool started, string statusIfOk)
        {
            SetStatus(started ? statusIfOk : "Falha ao iniciar a rede (veja o Console)");
            return started;
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
                                       NetworkManager.ConnectionApprovalResponse response)
        {
            bool hasRoom = networkManager.ConnectedClientsIds.Count < MaxPlayers;
            response.Approved = hasRoom;
            response.CreatePlayerObject = hasRoom;
            response.Reason = hasRoom ? string.Empty : "A sala já tem 2 jogadores.";
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (networkManager.IsServer)
                SetStatus($"Host — {networkManager.ConnectedClientsIds.Count} jogador(es) na sala");
            else if (clientId == networkManager.LocalClientId)
                SetStatus("Conectado como Aprendiz");
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (networkManager.IsServer)
            {
                SetStatus($"Host — {networkManager.ConnectedClientsIds.Count} jogador(es) na sala");
                return;
            }

            // Para o convidado: perdeu o host ou foi recusado.
            string reason = networkManager.DisconnectReason;
            SetStatus(string.IsNullOrEmpty(reason) ? "Desconectado do host" : $"Desconectado: {reason}");
        }

        private void SetStatus(string status)
        {
            Status = status;
            OnStatusChanged?.Invoke(status);
            Debug.Log($"[Rede] {status}");
        }
    }
}
