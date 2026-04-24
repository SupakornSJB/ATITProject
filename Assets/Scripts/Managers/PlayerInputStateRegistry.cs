using System.Collections.Generic;
using Players;
using Shared;
using UnityEngine;

namespace Managers
{
    public class PlayerInputStateRegistry : NetworkSingleton<PlayerInputStateRegistry>
    {
        private readonly Dictionary<string, PlayerInputState> _states = new();
        private readonly Dictionary<string, long> _lastSequenceNumbers = new();

        public PlayerInputState GetOrCreate(string playerId)
        {
            if (!_states.TryGetValue(playerId, out var state))
            {
                state = new PlayerInputState();
                _states[playerId] = state;
            }
            return state;
        }

        public void UpdateState(string playerId, string inputName, float value)
        {
            var state = GetOrCreate(playerId);
            state.Set(inputName, value);
            _states[playerId] = state;
        }

        public void CheckSequenceNumber(string deviceId, long sequenceNumber)
        {
            if (_lastSequenceNumbers.TryGetValue(deviceId, out var last) && sequenceNumber != last + 1)
                Debug.LogWarning($"[PlayerInputStateRegistry] Sequence gap for deviceId={deviceId}: expected {last + 1}, got {sequenceNumber}");
            _lastSequenceNumbers[deviceId] = sequenceNumber;
        }

        public void RemovePlayer(string playerId) => _states.Remove(playerId);
    }
}