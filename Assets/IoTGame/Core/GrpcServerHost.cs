#if UNITY_SERVER
using System.IO;
using Grpc.Core;
using IoTGame.Core;
using UnityEngine;

namespace Server
{
    public static class GrpcServerHost
    {
        private static Grpc.Core.Server _server;

        public static void Start(int port, string certPath, string keyPath, string caCertPath)
        {
            ServerCredentials credentials;
            if (File.Exists(certPath) && File.Exists(keyPath) && File.Exists(caCertPath))
            {
                var keyCertPair = new KeyCertificatePair(
                    File.ReadAllText(certPath),
                    File.ReadAllText(keyPath));
                credentials = new SslServerCredentials(
                    new[] { keyCertPair },
                    File.ReadAllText(caCertPath),
                    SslClientCertificateRequestType.RequestAndRequireAndVerify);
                Debug.Log($"[gRPC] Starting with mTLS on port {port}");
            }
            else
            {
                credentials = ServerCredentials.Insecure;
                Debug.LogWarning($"[gRPC] Cert files not found — starting INSECURE on port {port}. certPath={certPath}");
            }

            _server = new Grpc.Core.Server
            {
                Services = { GameController.V2.GameControllerService.BindService(new IoTGameServiceImpl()) },
                Ports    = { new ServerPort("0.0.0.0", port, credentials) }
            };

            _server.Start();
            Debug.Log($"[gRPC] Server listening on port {port}");
        }

        public static void Stop()
        {
            _server?.ShutdownAsync().Wait();
        }
    }
}
#endif
