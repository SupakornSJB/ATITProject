using IoTGame.Core;
using IoTGame.Models;
using Shared;
using UnityEngine;

namespace Managers
{
    public class GameInputRouter : NetworkSingleton<GameInputRouter>
    {
        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            InputEventDispatcher.OnReceived += HandleInput;
        }

        public override void OnNetworkDespawn()
        {
            InputEventDispatcher.OnReceived -= HandleInput;
        }

        private void HandleInput(IoTInputEvent evt)
        {
            var phase = GameSessionManager.Instance?.CurrentPhase.Value ?? SessionPhase.Waiting;
            PlayerInputStateRegistry.Instance?.CheckSequenceNumber(evt.DeviceId, evt.SequenceNumber);

            if (phase != SessionPhase.Active)
            {
                // Only stage admin buttons work outside Active phase
                if (evt.PlayerId == "stage") RouteStageInput(evt);
                return;
            }

            HandleGameplay(evt);
        }

        private void HandleGameplay(IoTInputEvent evt)
        {
            Debug.Log($"[IoT] player={evt.PlayerId} input={evt.InputName} device={evt.DeviceId} value={evt.Value}");
            PlayerInputStateRegistry.Instance?.UpdateState(evt.PlayerId, evt.InputName, evt.Value);

            switch (evt.PlayerId)
            {
                case "player-1":
                case "player-2":
                    RouteShipInput(evt);
                    break;
                case "stage":
                    RouteStageInput(evt);
                    break;
            }
        }

        private void RouteShipInput(IoTInputEvent evt)
        {
            if (BirdSpawnManager.Instance == null) { Debug.LogWarning("[GameInputRouter] BirdSpawnManager.Instance is null"); return; }
            var ship = BirdSpawnManager.Instance.GetShip(evt.PlayerId);
            if (ship == null) { Debug.LogWarning($"[GameInputRouter] GetShip returned null for '{evt.PlayerId}'"); return; }

            switch (evt.InputName)
            {
                case "move-x":
                    ship.SetJoystickX(evt.Value);
                    break;
                case "move-y":
                    ship.SetJoystickY(evt.Value);
                    break;
                case "move-y-up":
                    ship.SetJoystickY(evt.EventType == "pressed" ? 1.0f : 0.5f);
                    break;
                case "move-y-down":
                    ship.SetJoystickY(evt.EventType == "pressed" ? 0.0f : 0.5f);
                    break;
                case "fire":
                    if (evt.EventType == "pressed") ship.Fire();
                    break;
            }
        }

        private void RouteStageInput(IoTInputEvent evt)
        {
            if (evt.EventType == "released") return;
            switch (evt.InputName)
            {
                case "admin-start": GameSessionManager.Instance?.StartRound(); break;
                case "admin-end":   GameSessionManager.Instance?.EndRound();   break;
                case "pipe-flip":   PipeSpawnManager.Instance?.FlipGap();      break;
            }
        }
    }
}