using System.Collections.Generic;
using Shared;
using UnityEngine;

namespace Managers
{
    [System.Serializable]
    public struct PlayerColorEntry
    {
        public string playerId;
        public Color color;
    }

    public class PlayerDistinguisherManager : NetworkSingleton<PlayerDistinguisherManager>
    {
        [SerializeField] private List<PlayerColorEntry> _playerColorConfig = new()
        {
            new PlayerColorEntry { playerId = "player-1", color = Color.red },
            new PlayerColorEntry { playerId = "player-2", color = Color.blue },
            new PlayerColorEntry { playerId = "player-3", color = Color.green },
            new PlayerColorEntry { playerId = "player-4", color = Color.yellow },
        };

        private readonly List<Color> _fallbackPool = new() { Color.cyan, Color.magenta, Color.white, Color.grey };
        private readonly HashSet<Color> _assignedFallback = new();
        private Dictionary<string, Color> _colorMap;

        private void Awake()
        {
            _colorMap = new Dictionary<string, Color>();
            foreach (var entry in _playerColorConfig)
                if (!string.IsNullOrEmpty(entry.playerId))
                    _colorMap[entry.playerId] = entry.color;
        }

        public Color RequestColor(string playerId)
        {
            if (_colorMap.TryGetValue(playerId, out var color))
                return color;

            foreach (var fallback in _fallbackPool)
            {
                if (_assignedFallback.Add(fallback))
                    return fallback;
            }
            return Color.white;
        }

        public void ReleaseColor(Color color) => _assignedFallback.Remove(color);
    }
}