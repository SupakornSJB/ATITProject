using System.Collections.Generic;

namespace Players
{
    public struct PlayerInputState
    {
        public Dictionary<string, float> Values;

        public float Get(string inputName) =>
            Values != null && Values.TryGetValue(inputName, out var v) ? v : 0f;

        public void Set(string inputName, float value)
        {
            Values ??= new Dictionary<string, float>();
            Values[inputName] = value;
        }

        public bool IsPressed(string inputName) => Get(inputName) >= 0.5f;
    }
}