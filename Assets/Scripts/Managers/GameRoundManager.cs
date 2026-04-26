using Shared;
using Unity.Netcode;
using UnityEngine;

namespace Managers
{
    public enum RoundPhase { Waiting, Countdown, Active, Ended }

    public class GameRoundManager : NetworkSingleton<GameRoundManager>
    {
        private const float CountdownDuration = 5f;
        private const float RoundDuration = 120f;
        private const float EndedDuration = 10f;

        public NetworkVariable<RoundPhase> CurrentPhase = new(
            RoundPhase.Waiting,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<float> RoundTimer = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> Ship1Ready = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> Ship2Ready = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> Bird1Ready = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> Bird2Ready = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private float _phaseTimer;
        private int _lastCalloutSecond = -1;

        private void Update()
        {
            if (!IsServer) return;

            switch (CurrentPhase.Value)
            {
                case RoundPhase.Waiting:
                    DrainAdminQueue();
                    CheckAllReady();
                    break;

                case RoundPhase.Countdown:
                    _phaseTimer -= Time.deltaTime;
                    RoundTimer.Value = _phaseTimer;
                    FireCountdownCallouts();
                    if (_phaseTimer <= 0f)
                        TransitionTo(RoundPhase.Active);
                    break;

                case RoundPhase.Active:
                    _phaseTimer -= Time.deltaTime;
                    RoundTimer.Value = _phaseTimer;
                    DrainAdminQueue();
                    CheckWinConditions();
                    if (_phaseTimer <= 0f)
                        TransitionTo(RoundPhase.Ended);
                    break;

                case RoundPhase.Ended:
                    _phaseTimer -= Time.deltaTime;
                    if (_phaseTimer <= 0f)
                        TransitionTo(RoundPhase.Waiting);
                    break;
            }
        }

        private void FireCountdownCallouts()
        {
            int sec = Mathf.CeilToInt(_phaseTimer);
            if (sec != _lastCalloutSecond && sec >= 1 && sec <= 3)
            {
                _lastCalloutSecond = sec;
                CalloutManager.Instance?.TryShowCallout(sec.ToString(), 0.8f);
            }
        }

        private void CheckAllReady()
        {
            bool shipSideReady = Ship1Ready.Value || Ship2Ready.Value;
            bool birdSideReady = Bird1Ready.Value || Bird2Ready.Value;
            if (shipSideReady && birdSideReady)
                TransitionTo(RoundPhase.Countdown);
        }

        public void CheckWinConditions()
        {
            if (!IsServer || CurrentPhase.Value != RoundPhase.Active) return;

            var spawnMgr = BirdSpawnManager.Instance;
            // Don't evaluate win conditions until at least one entity per side exists
            if (spawnMgr == null || !spawnMgr.HasAnyShip() || !spawnMgr.HasAnyBossBird()) return;

            bool boss1Alive = IsBossAlive("player-3");
            bool boss2Alive = IsBossAlive("player-4");
            bool ship1Alive = IsShipAlive("player-1");
            bool ship2Alive = IsShipAlive("player-2");

            if (!boss1Alive && !boss2Alive)
            {
                CalloutManager.Instance?.TryShowCallout("SHIPS WIN", 8f);
                TransitionTo(RoundPhase.Ended);
                return;
            }
            if (!ship1Alive && !ship2Alive)
            {
                CalloutManager.Instance?.TryShowCallout("BIRDS WIN", 8f);
                TransitionTo(RoundPhase.Ended);
            }
        }

        private bool IsShipAlive(string playerId)
        {
            if (!BirdSpawnManager.Instance.TryGetShip(playerId, out var ship)) return false;
            return ship != null && ship.ShipHealth.Value > 0;
        }

        private bool IsBossAlive(string playerId)
        {
            if (!BirdSpawnManager.Instance.TryGetBossBird(playerId, out var boss)) return false;
            return boss != null && boss.BossHealth.Value > 0;
        }

        public void TransitionTo(RoundPhase phase)
        {
            if (!IsServer) return;
            Debug.Log($"[GameRoundManager] {CurrentPhase.Value} → {phase}");
            CurrentPhase.Value = phase;

            switch (phase)
            {
                case RoundPhase.Waiting:
                    ResetReadyFlags();
                    BirdSpawnManager.Instance?.DespawnAllPlayerEntities();
                    EnemySpawnManager.Instance?.DespawnAll();
                    PipeSpawnManager.Instance?.DespawnAll();
                    EnvironmentController.Instance?.ResetEffects();
                    break;

                case RoundPhase.Countdown:
                    _phaseTimer = CountdownDuration;
                    _lastCalloutSecond = -1;
                    RoundTimer.Value = _phaseTimer;
                    break;

                case RoundPhase.Active:
                    _phaseTimer = RoundDuration;
                    RoundTimer.Value = _phaseTimer;
                    CalloutManager.Instance?.TryShowCallout("FIGHT!", 1.5f);
                    break;

                case RoundPhase.Ended:
                    _phaseTimer = EndedDuration;
                    EnemySpawnManager.Instance?.DespawnAll();
                    PipeSpawnManager.Instance?.DespawnAll();
                    break;
            }
        }

        public void ToggleShipReady(string playerId)
        {
            if (!IsServer || CurrentPhase.Value != RoundPhase.Waiting) return;
            if (playerId == "player-1") Ship1Ready.Value = !Ship1Ready.Value;
            else if (playerId == "player-2") Ship2Ready.Value = !Ship2Ready.Value;
        }

        public void ToggleBirdReady(string playerId)
        {
            if (!IsServer || CurrentPhase.Value != RoundPhase.Waiting) return;
            if (playerId == "player-3") Bird1Ready.Value = !Bird1Ready.Value;
            else if (playerId == "player-4") Bird2Ready.Value = !Bird2Ready.Value;
        }

        private void ResetReadyFlags()
        {
            Ship1Ready.Value = false;
            Ship2Ready.Value = false;
            Bird1Ready.Value = false;
            Bird2Ready.Value = false;
        }

        public void HandlePlayerDisconnect(string playerId)
        {
            if (!IsServer) return;

            if (CurrentPhase.Value == RoundPhase.Countdown)
            {
                TransitionTo(RoundPhase.Waiting);
                return;
            }

            if (CurrentPhase.Value != RoundPhase.Active) return;

            bool isShip = playerId is "player-1" or "player-2";
            bool isBird = playerId is "player-3" or "player-4";

            if (isShip && !IsShipAlive("player-1") && !IsShipAlive("player-2"))
            {
                CalloutManager.Instance?.TryShowCallout("BIRDS WIN", 8f);
                TransitionTo(RoundPhase.Ended);
            }
            else if (isBird && !IsBossAlive("player-3") && !IsBossAlive("player-4"))
            {
                CalloutManager.Instance?.TryShowCallout("SHIPS WIN", 8f);
                TransitionTo(RoundPhase.Ended);
            }
        }

        private void DrainAdminQueue()
        {
#if UNITY_SERVER
            while (Server.IoTGameServiceImpl.AdminCommandQueue.TryDequeue(out var cmd))
                HandleAdminCommand(cmd);
#endif
        }

        public void HandleAdminCommand(string cmd)
        {
            if (!IsServer) return;
            switch (cmd)
            {
                case "start_round":
                    if (CurrentPhase.Value == RoundPhase.Waiting)
                        TransitionTo(RoundPhase.Countdown);
                    break;
                case "end_round":
                    if (CurrentPhase.Value == RoundPhase.Active)
                        TransitionTo(RoundPhase.Ended);
                    break;
                case "reset":
                    TransitionTo(RoundPhase.Waiting);
                    break;
            }
        }
    }
}