using IoTGame.Core;
using IoTGame.Models;
using Shared;
using Unity.Netcode;
using UnityEngine;

namespace Managers
{
    public enum SessionPhase { Waiting, Countdown, Active, Ended }

    public class GameSessionManager : NetworkSingleton<GameSessionManager>
    {
        private const float CountdownDuration    = 5f;
        private const float DefaultSessionDuration = 120f;
        private const float EndedDuration        = 10f;

        private float _sessionDuration = DefaultSessionDuration;
        private int   _maxMinions      = 20;
        private int   _playerCount     = 4;

        public NetworkVariable<SessionPhase> CurrentPhase = new(
            SessionPhase.Waiting,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<float> SessionTimer = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<float> SurvivalTime = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private float _phaseTimer;
        private int   _lastCalloutSecond = -1;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            AdminCommandDispatcher.OnReceived += HandleAdminCommand;
        }

        public override void OnNetworkDespawn()
        {
            AdminCommandDispatcher.OnReceived -= HandleAdminCommand;
        }

        private void Update()
        {
            if (!IsServer) return;

            switch (CurrentPhase.Value)
            {
                case SessionPhase.Countdown:
                    _phaseTimer -= Time.deltaTime;
                    SessionTimer.Value = _phaseTimer;
                    FireCountdownCallouts();
                    if (_phaseTimer <= 0f)
                        TransitionTo(SessionPhase.Active);
                    break;

                case SessionPhase.Active:
                    _phaseTimer -= Time.deltaTime;
                    SessionTimer.Value = _phaseTimer;
                    SurvivalTime.Value += Time.deltaTime;
                    CheckWinConditions();
                    if (_phaseTimer <= 0f)
                        TransitionTo(SessionPhase.Ended);
                    break;

                case SessionPhase.Ended:
                    _phaseTimer -= Time.deltaTime;
                    if (_phaseTimer <= 0f)
                        TransitionTo(SessionPhase.Waiting);
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

        public void CheckWinConditions()
        {
            if (!IsServer || CurrentPhase.Value != SessionPhase.Active) return;
            var ships = BirdSpawnManager.Instance?.GetAllShips();
            if (ships == null || ships.Count == 0) return;
            bool anyAlive = false;
            foreach (var s in ships)
                if (s != null && s.ShipHealth.Value > 0) { anyAlive = true; break; }
            if (!anyAlive)
            {
                CalloutManager.Instance?.TryShowCallout("SHIPS DESTROYED", 5f);
                TransitionTo(SessionPhase.Ended);
            }
        }

        public void StartRound()
        {
            if (!IsServer || CurrentPhase.Value != SessionPhase.Waiting) return;
            TransitionTo(SessionPhase.Countdown);
        }

        public void EndRound()
        {
            if (!IsServer || CurrentPhase.Value != SessionPhase.Active) return;
            TransitionTo(SessionPhase.Ended);
        }

        public void TransitionTo(SessionPhase phase)
        {
            if (!IsServer) return;
            Debug.Log($"[GameSessionManager] {CurrentPhase.Value} → {phase}");
            CurrentPhase.Value = phase;

            switch (phase)
            {
                case SessionPhase.Waiting:
                    ResetReadyFlags();
                    BirdSpawnManager.Instance?.DespawnAllPlayerEntities();
                    EnemySpawnManager.Instance?.DespawnAll();
                    PipeSpawnManager.Instance?.DespawnAll();
                    EnvironmentController.Instance?.ResetEffects();
                    break;

                case SessionPhase.Countdown:
                    _phaseTimer = CountdownDuration;
                    _lastCalloutSecond = -1;
                    SessionTimer.Value = _phaseTimer;
                    break;

                case SessionPhase.Active:
                    _phaseTimer = _sessionDuration;
                    SessionTimer.Value = _phaseTimer;
                    SurvivalTime.Value = 0f;
                    BirdSpawnManager.Instance?.SpawnForPlayer("player-1");
                    BirdSpawnManager.Instance?.SpawnForPlayer("player-2");
                    CalloutManager.Instance?.TryShowCallout("FIGHT!", 1.5f);
                    break;

                case SessionPhase.Ended:
                    _phaseTimer = EndedDuration;
                    EnemySpawnManager.Instance?.DespawnAll();
                    PipeSpawnManager.Instance?.DespawnAll();
                    break;
            }
        }

        private void ResetReadyFlags() { }

        public void HandlePlayerDisconnect(string playerId)
        {
            if (!IsServer) return;
            if (CurrentPhase.Value == SessionPhase.Countdown)
                TransitionTo(SessionPhase.Waiting);
            // In Active phase, win condition is checked each Update — disconnect is handled naturally
        }

        private void HandleAdminCommand(AdminCommand cmd)
        {
            if (!IsServer) return;
            switch (cmd.Command)
            {
                case "session_start":
                    _sessionDuration = cmd.Auxiliary.TryGetValue("session_duration", out var sd) ? sd : DefaultSessionDuration;
                    _maxMinions      = cmd.Auxiliary.TryGetValue("max_minions",      out var mm) ? (int)mm : 20;
                    _playerCount     = cmd.Auxiliary.TryGetValue("player_count",     out var pc) ? (int)pc : 4;
                    Debug.Log($"[GameSessionManager] session_start — duration={_sessionDuration} maxMinions={_maxMinions} playerCount={_playerCount}");
                    break;
                case "start_round":
                    StartRound();
                    break;
                case "end_round":
                    EndRound();
                    break;
                case "reset":
                    TransitionTo(SessionPhase.Waiting);
                    break;
            }
        }
    }
}
