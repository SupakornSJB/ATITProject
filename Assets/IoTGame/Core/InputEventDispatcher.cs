using System;
using System.Collections.Generic;
using IoTGame.Models;

namespace IoTGame.Core
{
    /// <summary>
    /// Drains the gRPC input queue on the Unity main thread and fires OnReceived.
    /// Called every frame by IoTInputManager. Developer code subscribes to OnReceived.
    /// </summary>
    public static class InputEventDispatcher
    {
        public static event Action<IoTInputEvent> OnReceived;

        private static readonly IReadOnlyDictionary<string, float> EmptyAuxiliary =
            new Dictionary<string, float>();

#if UNITY_SERVER
        internal static void Drain()
        {
            while (IoTGameServiceImpl.InputQueue.TryDequeue(out var batch))
                foreach (var evt in batch.Events)
                {
                    UnityEngine.Debug.Log($"[InputEventDispatcher] Drain — player={evt.PlayerId} input={evt.InputName} subscribers={OnReceived?.GetInvocationList().Length ?? 0}");
                    OnReceived?.Invoke(new IoTInputEvent(
                        evt.PlayerId,
                        evt.DeviceId,
                        evt.InputName,
                        evt.InputType,
                        evt.EventType,
                        evt.Value,
                        evt.Timestamp,
                        evt.SequenceNumber,
                        EmptyAuxiliary));
                }
        }
#endif
    }
}
