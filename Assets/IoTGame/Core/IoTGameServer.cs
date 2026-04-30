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
        public static string SharedSecret { get; private set; } = "";
        public static bool IsRunning { get; private set; }

#if UNITY_SERVER
        public static void Initialize(int grpcPort = 9090, string secret = "")
        {
            SharedSecret = secret ?? "";
            Server.GrpcServerHost.Start(grpcPort);
            IsRunning = true;
            Debug.Log($"[IoTGameServer] Initialized — gRPC port {grpcPort}, secret {(string.IsNullOrEmpty(secret) ? "not set" : "set")}");
        }

        public static void Shutdown()
        {
            Server.GrpcServerHost.Stop();
            IsRunning = false;
        }
#endif
    }
}
