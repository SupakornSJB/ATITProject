using UnityEngine;

namespace IoTGame.Core
{
    /// <summary>
    /// Developer-facing facade for IoT game server initialization.
    /// Call Initialize() once in Start() after NetworkManager.Singleton.StartServer().
    /// To receive events, subscribe directly to InputEventDispatcher, EnvironmentEventDispatcher,
    /// or AdminCommandDispatcher.
    /// </summary>
    public static class IoTGameServer
    {
        public static bool IsRunning { get; private set; }

#if UNITY_SERVER
        public static void Initialize(
            int    grpcPort   = 9090,
            string certPath   = "Assets/Certs/unity-server.crt",
            string keyPath    = "Assets/Certs/unity-server.key",
            string caCertPath = "Assets/Certs/ca.crt")
        {
            Server.GrpcServerHost.Start(grpcPort, certPath, keyPath, caCertPath);
            IsRunning = true;
            Debug.Log($"[IoTGameServer] Initialized — gRPC mTLS port {grpcPort}");
        }

        public static void Shutdown()
        {
            Server.GrpcServerHost.Stop();
            IsRunning = false;
        }
#endif
    }
}
