#if !UNITY_SERVER
using System.Collections;
using Managers;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI
{
    public class RoundUIManager : MonoBehaviour
    {
        public static RoundUIManager Instance { get; private set; }

        [Header("Phase Panels")]
        [SerializeField] private GameObject _waitingPanel;
        [SerializeField] private GameObject _countdownPanel;
        [SerializeField] private GameObject _activeHudPanel;
        [SerializeField] private GameObject _endedPanel;

        [Header("Waiting UI")]
        [SerializeField] private Image _ship1ReadyIndicator;
        [SerializeField] private Image _ship2ReadyIndicator;
        [SerializeField] private Image _bird1ReadyIndicator;
        [SerializeField] private Image _bird2ReadyIndicator;

        [Header("Active HUD")]
        [SerializeField] private Slider _boss1HpBar;
        [SerializeField] private Slider _boss2HpBar;
        [SerializeField] private Slider _ship1HpBar;
        [SerializeField] private Slider _ship2HpBar;
        [SerializeField] private TextMeshProUGUI _roundTimerText;
        [SerializeField] private TextMeshProUGUI _zoneIndicatorText;

        [Header("Stage Info Panel (visible any phase)")]
        [SerializeField] private TextMeshProUGUI _temperatureText;
        [SerializeField] private TextMeshProUGUI _humidityText;
        [SerializeField] private TextMeshProUGUI _zoneDetailText;
        [SerializeField] private TextMeshProUGUI _cooldownText;

        [Header("Callout")]
        [SerializeField] private TextMeshProUGUI _calloutText;
        [SerializeField] private GameObject _calloutPanel;

        [Header("Tutorial (F4 to toggle)")]
        [SerializeField] private GameObject _tutorialOverlay;

        private Coroutine _calloutCoroutine;
        private RoundPhase _currentPhase = (RoundPhase)(-1);

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
            if (GameRoundManager.Instance == null) return;

            var phase = GameRoundManager.Instance.CurrentPhase.Value;
            if (phase != _currentPhase)
            {
                _currentPhase = phase;
                ShowPhasePanel(phase);
            }

            UpdateHudValues(phase);

            if (Keyboard.current != null && Keyboard.current.f4Key.wasPressedThisFrame && _tutorialOverlay != null)
                _tutorialOverlay.SetActive(!_tutorialOverlay.activeSelf);
        }

        private void ShowPhasePanel(RoundPhase phase)
        {
            _waitingPanel?.SetActive(phase == RoundPhase.Waiting);
            _countdownPanel?.SetActive(phase == RoundPhase.Countdown);
            _activeHudPanel?.SetActive(phase == RoundPhase.Active);
            _endedPanel?.SetActive(phase == RoundPhase.Ended);
        }

        private void UpdateHudValues(RoundPhase phase)
        {
            if (phase == RoundPhase.Waiting)
            {
                SetReadyColor(_ship1ReadyIndicator, GameRoundManager.Instance.Ship1Ready.Value);
                SetReadyColor(_ship2ReadyIndicator, GameRoundManager.Instance.Ship2Ready.Value);
                SetReadyColor(_bird1ReadyIndicator, GameRoundManager.Instance.Bird1Ready.Value);
                SetReadyColor(_bird2ReadyIndicator, GameRoundManager.Instance.Bird2Ready.Value);
            }

            if (phase == RoundPhase.Active || phase == RoundPhase.Countdown)
            {
                if (_roundTimerText != null)
                    _roundTimerText.text = Mathf.CeilToInt(GameRoundManager.Instance.RoundTimer.Value).ToString();

                UpdateHpBars();
                UpdateZoneIndicator();
            }

            UpdateStageInfo();
        }

        private void SetReadyColor(Image img, bool ready)
        {
            if (img != null)
                img.color = ready ? Color.green : Color.red;
        }

        private void UpdateHpBars()
        {
            var spawnMgr = BirdSpawnManager.Instance;
            if (spawnMgr == null) return;

            if (_boss1HpBar != null && spawnMgr.TryGetBossBird("player-3", out var boss1))
                _boss1HpBar.value = boss1 != null ? boss1.BossHealth.Value / 100f : 0f;

            if (_boss2HpBar != null && spawnMgr.TryGetBossBird("player-4", out var boss2))
                _boss2HpBar.value = boss2 != null ? boss2.BossHealth.Value / 100f : 0f;

            if (_ship1HpBar != null && spawnMgr.TryGetShip("player-1", out var ship1))
                _ship1HpBar.value = ship1 != null ? ship1.ShipHealth.Value / 100f : 0f;

            if (_ship2HpBar != null && spawnMgr.TryGetShip("player-2", out var ship2))
                _ship2HpBar.value = ship2 != null ? ship2.ShipHealth.Value / 100f : 0f;
        }

        private void UpdateZoneIndicator()
        {
            if (_zoneIndicatorText == null || EnvironmentController.Instance == null) return;
            int zone = EnvironmentController.Instance.ActiveZoneEffect.Value;
            _zoneIndicatorText.text = zone switch
            {
                1 => "Zone A (Shield)",
                2 => "Zone B (Slow)",
                3 => "Zone C (Normal)",
                4 => "Zone D (Frenzy)",
                _ => "Zone C (Normal)"
            };
        }

        private void UpdateStageInfo()
        {
            var env = EnvironmentController.Instance;
            if (env == null) return;

            if (_temperatureText != null)
            {
                float g = env.GlobalGravity.Value;
                _temperatureText.text = $"Gravity: {g:F1}  ({env.Temperature.Value:F1}°C)";
            }

            if (_humidityText != null)
            {
                float h = env.Humidity.Value;
                string rate = h >= 70f ? "fast" : h >= 50f ? "normal" : "slow";
                _humidityText.text = $"Spawn rate: {rate}  ({h:F0}% humid)";
            }

            if (_zoneDetailText != null)
            {
                int zone = env.UltrasonicZone.Value;
                string zoneLabel = zone == 1 ? "Zone A — SHIELD" :
                                   zone == 2 ? "Zone B — TIME SLOW" :
                                   zone == 4 ? "Zone D — FRENZY" : "Zone C — Normal";
                _zoneDetailText.text = $"Zone: {zoneLabel}";
            }

            if (_cooldownText != null)
            {
                float cd = env.GetCooldownRemaining();
                _cooldownText.text = cd > 0f ? $"Cooldown: {cd:F0}s" : "Ready";
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