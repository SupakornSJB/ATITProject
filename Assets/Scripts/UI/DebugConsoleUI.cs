using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace UI
{
    public class DebugConsoleUI : MonoBehaviour
    {
        public static DebugConsoleUI Instance { get; private set; }

        [SerializeField] private TextMeshProUGUI _logText;
        [SerializeField] private int _maxLines = 12;

        private readonly Queue<string> _lines = new();

        private void Awake()
        {
            Instance = this;
        }

        public void AddMessage(string message)
        {
            _lines.Enqueue($"[{System.DateTime.Now:HH:mm:ss}] {message}");
            while (_lines.Count > _maxLines)
                _lines.Dequeue();
            _logText.text = string.Join("\n", _lines);
        }
    }
}