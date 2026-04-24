using Shared;
using Unity.Netcode;
using UnityEngine;

namespace Managers
{
    public class IoTInputManager : NetworkSingleton<IoTInputManager>
    {
        private void Update()
        {
            if (!IsServer) return;

#if UNITY_SERVER
            while (Server.IoTGameServiceImpl.InputQueue.TryDequeue(out var envelope))
            {
                foreach (var evt in envelope.Events)
                {
                    Debug.Log($"[IoT] player={evt.PlayerId} input={evt.InputName} device={evt.DeviceId} value={evt.Value}");
                    NotifyInputClientRpc(evt.PlayerId, evt.InputName, evt.EventType, evt.Value);

                    PlayerInputStateRegistry.Instance.CheckSequenceNumber(evt.DeviceId, evt.SequenceNumber);
                    PlayerInputStateRegistry.Instance.UpdateState(evt.PlayerId, evt.InputName, evt.Value);

                    // Spawn bird on first input if not already alive
                    BirdSpawnManager.Instance.SpawnBirdForPlayer(evt.PlayerId);
                    if (!BirdSpawnManager.Instance.TryGetBird(evt.PlayerId, out var bird))
                        continue;

                    switch (evt.InputType)
                    {
                        case "button":
                            if (evt.EventType == "pressed")
                                bird.OnButtonPressed(evt.InputName);
                            else if (evt.EventType == "released")
                                bird.OnButtonReleased(evt.InputName);
                            break;
                        case "knob":
                        case "sensor":
                            bird.OnAnalogChanged(evt.InputName, evt.Value);
                            break;
                    }
                }
            }
#endif
        }

        [ClientRpc]
        private void NotifyInputClientRpc(string playerId, string inputName, string eventType, float value)
        {
            UI.DebugConsoleUI.Instance?.AddMessage($"{playerId} | {inputName} | {eventType} | {value:F2}");
        }
    }
}