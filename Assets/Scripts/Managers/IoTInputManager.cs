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
            var phase = GameRoundManager.Instance?.CurrentPhase.Value ?? RoundPhase.Waiting;

            if (phase == RoundPhase.Waiting)
            {
                DrainReadyUpInputs();
                return;
            }
            if (phase == RoundPhase.Active)
                DrainGameplayInputs();
#endif
        }

#if UNITY_SERVER
        private void DrainReadyUpInputs()
        {
            while (Server.IoTGameServiceImpl.InputQueue.TryDequeue(out var batch))
            {
                foreach (var evt in batch.Events)
                {
                    Debug.Log($"[IoT][ReadyUp] player={evt.PlayerId} input={evt.InputName} device={evt.DeviceId} value={evt.Value}");
                    NotifyInputClientRpc(evt.PlayerId, evt.InputName, evt.EventType, evt.Value);

                    if (evt.InputType != "button" || evt.EventType != "pressed") continue;

                    if (evt.InputName == "fire" && (evt.PlayerId is "player-1" or "player-2"))
                        GameRoundManager.Instance?.ToggleShipReady(evt.PlayerId);
                    else if (evt.InputName == "flap" && (evt.PlayerId is "player-3" or "player-4"))
                        GameRoundManager.Instance?.ToggleBirdReady(evt.PlayerId);
                }
            }
        }

        private void DrainGameplayInputs()
        {
            while (Server.IoTGameServiceImpl.InputQueue.TryDequeue(out var batch))
            {
                foreach (var evt in batch.Events)
                {
                    Debug.Log($"[IoT] player={evt.PlayerId} input={evt.InputName} device={evt.DeviceId} value={evt.Value}");
                    NotifyInputClientRpc(evt.PlayerId, evt.InputName, evt.EventType, evt.Value);

                    PlayerInputStateRegistry.Instance?.CheckSequenceNumber(evt.DeviceId, evt.SequenceNumber);
                    PlayerInputStateRegistry.Instance?.UpdateState(evt.PlayerId, evt.InputName, evt.Value);

                    RouteGameplayEvent(evt);
                }
            }
        }

        private void RouteGameplayEvent(GameController.V2.InputEventRequest evt)
        {
            switch (evt.PlayerId)
            {
                case "player-1":
                case "player-2":
                    RouteShipInput(evt);
                    break;
                case "player-3":
                case "player-4":
                    RouteBirdInput(evt);
                    break;
                case "stage":
                    RouteStageInput(evt);
                    break;
            }
        }

        private void RouteShipInput(GameController.V2.InputEventRequest evt)
        {
            BirdSpawnManager.Instance?.SpawnForPlayer(evt.PlayerId);
            var ship = BirdSpawnManager.Instance?.GetShip(evt.PlayerId);
            if (ship == null) return;

            switch (evt.InputName)
            {
                case "move-x": ship.SetJoystickX(evt.Value); break;
                case "move-y": ship.SetJoystickY(evt.Value); break;
                case "fire":
                    if (evt.InputType == "button" && evt.EventType == "pressed")
                        ship.Fire();
                    break;
            }
        }

        private void RouteBirdInput(GameController.V2.InputEventRequest evt)
        {
            BirdSpawnManager.Instance?.SpawnForPlayer(evt.PlayerId);
            var bird = BirdSpawnManager.Instance?.GetBossBird(evt.PlayerId);
            if (bird == null) return;

            if (evt.InputName == "flap" && evt.InputType == "button" && evt.EventType == "pressed")
                bird.Flap();
        }

        private void RouteStageInput(GameController.V2.InputEventRequest evt)
        {
            if (evt.InputName == "pipe-flip" && evt.EventType == "pressed")
                PipeSpawnManager.Instance?.FlipGap();
        }
#endif

        [ClientRpc]
        private void NotifyInputClientRpc(string playerId, string inputName, string eventType, float value)
        {
#if !UNITY_SERVER
            UI.DebugConsoleUI.Instance?.AddMessage($"{playerId} | {inputName} | {eventType} | {value:F2}");
#endif
        }
    }
}