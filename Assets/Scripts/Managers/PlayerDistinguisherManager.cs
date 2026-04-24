using System.Collections.Generic;
using Shared;
using UnityEngine;

namespace Managers
{
    public class PlayerDistinguisherManager : NetworkSingleton<PlayerDistinguisherManager>
    {
        private readonly List<Color> _pool = new() { Color.red, Color.blue, Color.green, Color.yellow };
        private readonly HashSet<Color> _assigned = new();

        public Color RequestColor()
        {
            foreach (var color in _pool)
            {
                if (_assigned.Add(color))
                    return color;
            }
            return Color.white;
        }

        public void ReleaseColor(Color color) => _assigned.Remove(color);
    }
}