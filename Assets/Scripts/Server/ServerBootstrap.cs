using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Server
{
    public class ServerBootstrap : MonoBehaviour
    {
        [Header("Client Settings")]
        [Tooltip("Optional: highlights this player's UI on the spectator client. Leave blank for neutral view.")]
        [SerializeField] private string _preferredPlayerId = "";
        [SerializeField] private string _serverAddress = "127.0.0.1";
        [SerializeField] private ushort _clientGamePort = 7777;
        [SerializeField] private bool _startAsHost = false;

        private void Start()
        {
#if UNITY_SERVER
            int    grpcPort   = 9090;
            int    gamePort   = 7777;
            string certPath   = "Assets/Certs/device.crt";
            string keyPath    = "Assets/Certs/device.key";
            string caCertPath = "Assets/Certs/ca.crt";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-grpcPort"  && int.TryParse(args[i + 1], out int gp)) grpcPort   = gp;
                if (args[i] == "-gamePort"  && int.TryParse(args[i + 1], out int mp)) gamePort   = mp;
                if (args[i] == "-grpcCert")  certPath   = args[i + 1];
                if (args[i] == "-grpcKey")   keyPath    = args[i + 1];
                if (args[i] == "-caCert")    caCertPath = args[i + 1];
            }

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
                transport.SetConnectionData("0.0.0.0", (ushort)gamePort);

            ConnectionApprovalHandler.Register();
            NetworkManager.Singleton.StartServer();
            IoTGame.Core.IoTGameServer.Initialize(grpcPort, certPath, keyPath, caCertPath);
            Debug.Log($"[ServerBootstrap] Server started — gRPC mTLS:{grpcPort} game:{gamePort}");
#else
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

            if (_startAsHost)
            {
                if (transport != null)
                    transport.SetConnectionData("0.0.0.0", _clientGamePort);

                ConnectionApprovalHandler.Register();
                NetworkManager.Singleton.NetworkConfig.ConnectionData = BuildSpectatorPayload();
                NetworkManager.Singleton.StartHost();
                Debug.Log($"[ServerBootstrap] Host started (spectator:{_preferredPlayerId}) on port {_clientGamePort}");
            }
            else
            {
                string address = _serverAddress;
                ushort port    = _clientGamePort;
                var args = System.Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i] == "-serverAddress") address = args[i + 1];
                    if (args[i] == "-gamePort" && ushort.TryParse(args[i + 1], out ushort p)) port = p;
                }

                if (transport != null)
                    transport.SetConnectionData(address, port);

                NetworkManager.Singleton.NetworkConfig.ConnectionData = BuildSpectatorPayload();
                NetworkManager.Singleton.StartClient();
                Debug.Log($"[ServerBootstrap] Client connecting to {address}:{port} (preferred: '{_preferredPlayerId}')");
            }
#endif
        }

        private byte[] BuildSpectatorPayload()
        {
            string payload = string.IsNullOrWhiteSpace(_preferredPlayerId)
                ? "spectator"
                : $"spectator:{_preferredPlayerId}";
            return Encoding.UTF8.GetBytes(payload);
        }

        private void OnDestroy()
        {
#if UNITY_SERVER
            IoTGame.Core.IoTGameServer.Shutdown();
#endif
        }
    }
}
