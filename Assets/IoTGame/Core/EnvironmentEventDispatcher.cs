using System;
using IoTGame.Models;

namespace IoTGame.Core
{
    /// <summary>
    /// Drains the gRPC environment queue on the Unity main thread and fires OnReceived.
    /// Called every frame by IoTInputManager. Developer code subscribes to OnReceived.
    /// </summary>
    public static class EnvironmentEventDispatcher
    {
        public static event Action<EnvironmentEvent> OnReceived;

#if UNITY_SERVER
        internal static void Drain()
        {
            while (IoTGameServiceImpl.EnvironmentQueue.TryDequeue(out var evt))
                OnReceived?.Invoke(evt);
        }
#endif
    }
}
