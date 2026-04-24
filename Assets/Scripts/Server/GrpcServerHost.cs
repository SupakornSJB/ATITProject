#if UNITY_SERVER
using Grpc.Core;
using UnityEngine;

namespace Server
{
    public static class GrpcServerHost
    {
        private static Grpc.Core.Server _server;

        public static void Start(int port)
        {
            _server = new Grpc.Core.Server
            {
                Services = { GameController.V2.GameControllerService.BindService(new IoTGameServiceImpl()) },
                Ports = { new ServerPort("0.0.0.0", port, ServerCredentials.Insecure) }
            };
            _server.Start();
            Debug.Log($"[GrpcServerHost] Listening on port {port}");
        }

        public static void Stop()
        {
            _server?.ShutdownAsync().Wait();
        }
    }
}
#endif