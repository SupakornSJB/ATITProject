using IoTGame.Core;
using IoTGame.Models;
using Shared;
using Unity.Netcode;
using UnityEngine;

namespace Managers
{
    // Library orchestrator — calls the 3 dispatchers each frame and broadcasts
    // all input events to clients via ClientRpc for debug display.
    public class IoTInputManager : NetworkSingleton<IoTInputManager>
    {
        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            InputEventDispatcher.OnReceived += BroadcastInputToClients;
        }

        public override void OnNetworkDespawn()
        {
            InputEventDispatcher.OnReceived -= BroadcastInputToClients;
        }

        private void Update()
        {
            if (!IsServer) return;
#if UNITY_SERVER
            InputEventDispatcher.Drain();
            EnvironmentEventDispatcher.Drain();
            AdminCommandDispatcher.Drain();
#endif
        }

        private void BroadcastInputToClients(IoTInputEvent evt) =>
            NotifyInputClientRpc(evt.PlayerId, evt.InputName, evt.EventType, evt.Value);

        [ClientRpc]
        private void NotifyInputClientRpc(string playerId, string inputName, string eventType, float value)
        {
#if !UNITY_SERVER
            UI.DebugConsoleUI.Instance?.AddMessage($"{playerId} | {inputName} | {eventType} | {value:F2}");
#endif
        }
    }
}
