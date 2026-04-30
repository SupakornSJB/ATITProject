using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Players
{
    public class PlayerShipUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _playerLabel;
        [SerializeField] private Image           _healthFill;

        private PlayerShip _ship;

        private void Awake()
        {
            _ship = GetComponentInParent<PlayerShip>();
        }

        private void Start()
        {
            if (_ship == null) return;
            _ship.ShipColor.OnValueChanged  += (_, c) => ApplyColor(c);
            _ship.PlayerIndex.OnValueChanged += (_, i) => ApplyLabel(i);
            ApplyColor(_ship.ShipColor.Value);
            ApplyLabel(_ship.PlayerIndex.Value);
        }

        private void OnDestroy()
        {
            if (_ship == null) return;
            _ship.ShipColor.OnValueChanged  -= (_, c) => ApplyColor(c);
            _ship.PlayerIndex.OnValueChanged -= (_, i) => ApplyLabel(i);
        }

        private void ApplyLabel(int index)
        {
            if (_playerLabel == null) return;
            _playerLabel.text = index == 1 ? "P1" : index == 2 ? "P2" : "";
        }

        private void ApplyColor(Color color)
        {
            if (_playerLabel != null) _playerLabel.color = color;
            if (_healthFill  != null) _healthFill.color  = color;
        }

        private void Update()
        {
            if (_ship == null) return;

            if (_healthFill != null)
                _healthFill.fillAmount = _ship.ShipHealth.Value / 100f;

            if (Camera.main != null)
                transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward,
                                 Camera.main.transform.rotation * Vector3.up);
        }
    }
}