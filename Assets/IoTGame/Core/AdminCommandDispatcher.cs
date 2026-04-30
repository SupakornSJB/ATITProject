using System;
using IoTGame.Models;

namespace IoTGame.Core
{
    /// <summary>
    /// Drains the gRPC admin command queue on the Unity main thread and fires OnReceived.
    /// Called every frame by IoTInputManager. Developer code subscribes to OnReceived.
    /// </summary>
    public static class AdminCommandDispatcher
    {
        public static event Action<AdminCommand> OnReceived;

#if UNITY_SERVER
        internal static void Drain()
        {
            while (IoTGameServiceImpl.AdminCommandQueue.TryDequeue(out var cmd))
                OnReceived?.Invoke(cmd);
        }
#endif
    }
}
