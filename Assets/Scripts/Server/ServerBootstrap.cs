using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Server
{
    public class ServerBootstrap : MonoBehaviour
    {
        [Header("Client / Host Settings")]
        [SerializeField] private string _playerId = "player-1";
        [SerializeField] private string _serverAddress = "127.0.0.1";
        [SerializeField] private ushort _clientGamePort = 7777;
        [SerializeField] private bool _startAsHost = false;

        private void Start()
        {
#if UNITY_SERVER
            int grpcPort = 9090;
            int gamePort = 7777;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-grpcPort" && int.TryParse(args[i + 1], out int gp)) grpcPort = gp;
                if (args[i] == "-gamePort" && int.TryParse(args[i + 1], out int mp)) gamePort = mp;
            }

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
                transport.SetConnectionData("0.0.0.0", (ushort)gamePort);

            ConnectionApprovalHandler.Register();
            NetworkManager.Singleton.StartServer();
            GrpcServerHost.Start(grpcPort);
            Debug.Log($"[ServerBootstrap] Server started — gRPC:{grpcPort} game:{gamePort}");
#else
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

            if (_startAsHost)
            {
                if (transport != null)
                    transport.SetConnectionData("0.0.0.0", _clientGamePort);

                NetworkManager.Singleton.NetworkConfig.ConnectionData =
                    System.Text.Encoding.UTF8.GetBytes(_playerId);
                ConnectionApprovalHandler.Register();
                NetworkManager.Singleton.StartHost();
                Debug.Log($"[ServerBootstrap] Host started as '{_playerId}' on port {_clientGamePort}");
            }
            else
            {
                if (transport != null)
                    transport.SetConnectionData(_serverAddress, _clientGamePort);

                NetworkManager.Singleton.NetworkConfig.ConnectionData =
                    System.Text.Encoding.UTF8.GetBytes(_playerId);
                NetworkManager.Singleton.StartClient();
                Debug.Log($"[ServerBootstrap] Client started as '{_playerId}'");
            }
#endif
        }

        private void OnDestroy()
        {
#if UNITY_SERVER
            GrpcServerHost.Stop();
#endif
        }
    }
}