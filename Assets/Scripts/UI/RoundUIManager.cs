#if !UNITY_SERVER
using System.Collections;
using Managers;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UI
{
    public class RoundUIManager : MonoBehaviour
    {
        public static RoundUIManager Instance { get; private set; }

        [Header("Minimal Corner Overlays")]
        [SerializeField] private TextMeshProUGUI _roundTimerText;
        [SerializeField] private TextMeshProUGUI _zoneIndicatorText;

        [Header("Stage Info (always visible)")]
        [SerializeField] private TextMeshProUGUI _humidityText;
        [SerializeField] private TextMeshProUGUI _zoneDetailText;

        [Header("Survival Stopwatch")]
        [SerializeField] private TextMeshProUGUI _survivalTimerText;

        [Header("Waiting Screen")]
        [SerializeField] private TextMeshProUGUI _waitingText;

        [Header("Callout")]
        [SerializeField] private TextMeshProUGUI _calloutText;
        [SerializeField] private GameObject      _calloutPanel;

        [Header("Tutorial (F4 to toggle)")]
        [SerializeField] private GameObject _tutorialOverlay;

        private Coroutine _calloutCoroutine;
        private SessionPhase _currentPhase = (SessionPhase)(-1);

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (GameSessionManager.Instance == null) return;

            var phase = GameSessionManager.Instance.CurrentPhase.Value;
            if (phase != _currentPhase)
            {
                _currentPhase = phase;
                UpdateWaitingText(phase);
            }

            UpdateCornerOverlays(phase);
            UpdateSurvivalTimer(phase);
            UpdateStageInfo();

            if (Keyboard.current != null && Keyboard.current.f4Key.wasPressedThisFrame && _tutorialOverlay != null)
                _tutorialOverlay.SetActive(!_tutorialOverlay.activeSelf);
        }

        private void UpdateWaitingText(SessionPhase phase)
        {
            if (_waitingText == null) return;
            _waitingText.gameObject.SetActive(phase == SessionPhase.Waiting);
            if (phase == SessionPhase.Waiting)
                _waitingText.text = "Waiting for admin to start round...";
        }

        private void UpdateCornerOverlays(SessionPhase phase)
        {
            if (_roundTimerText != null)
            {
                bool showTimer = phase == SessionPhase.Active || phase == SessionPhase.Countdown;
                _roundTimerText.gameObject.SetActive(showTimer);
                if (showTimer)
                    _roundTimerText.text = Mathf.CeilToInt(GameSessionManager.Instance.SessionTimer.Value).ToString();
            }

            if (_zoneIndicatorText != null && EnvironmentController.Instance != null)
            {
                int zone = EnvironmentController.Instance.ActiveZoneEffect.Value;
                _zoneIndicatorText.text = zone switch
                {
                    1 => "Zone A",
                    2 => "Zone B",
                    3 => "Zone C",
                    4 => "Zone D",
                    _ => "Zone C"
                };
            }
        }

        private void UpdateSurvivalTimer(SessionPhase phase)
        {
            if (_survivalTimerText == null) return;
            bool show = phase == SessionPhase.Active || phase == SessionPhase.Ended;
            _survivalTimerText.gameObject.SetActive(show);
            if (!show) return;
            float t = GameSessionManager.Instance.SurvivalTime.Value;
            int minutes = (int)(t / 60f);
            int seconds = (int)(t % 60f);
            int millis  = (int)((t % 1f) * 100f);
            _survivalTimerText.text = $"Survived: {minutes:00}:{seconds:00}.{millis:00}";
        }

        private void UpdateStageInfo()
        {
            var env = EnvironmentController.Instance;
            if (env == null) return;

            if (_humidityText != null)
            {
                float h = env.Humidity.Value;
                string rate = h >= 70f ? "Fast" : h >= 50f ? "Normal" : "Slow";
                _humidityText.text = $"Humidity: {h:F0}%  Spawn: {rate}";
            }

            if (_zoneDetailText != null)
            {
                int zone = env.UltrasonicZone.Value;
                float speed = env.GlobalGameSpeed.Value;
                string label = zone switch
                {
                    1 => "Zone A — Very Slow",
                    2 => "Zone B — Slow",
                    3 => "Zone C — Normal",
                    4 => "Zone D — Fast",
                    _ => "Zone C — Normal"
                };
                _zoneDetailText.text = $"{label}  (x{speed:F2})";
            }
        }

        public void ShowCallout(string text, float duration)
        {
            if (_calloutCoroutine != null)
                StopCoroutine(_calloutCoroutine);
            _calloutCoroutine = StartCoroutine(CalloutRoutine(text, duration));
        }

        private IEnumerator CalloutRoutine(string text, float duration)
        {
            if (_calloutText != null) _calloutText.text = text;
            _calloutPanel?.SetActive(true);
            yield return new WaitForSeconds(duration);
            _calloutPanel?.SetActive(false);
        }
    }
}
#endif