using Managers;
using Unity.Netcode;
using UnityEngine;

namespace Players
{
    public class PlayerShip : NetworkBehaviour
    {
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private int _maxLiveProjectiles = 3;

        private Rigidbody _rb;
        private MeshRenderer _meshRenderer;
        private string _playerId;
        private Vector2 _joystickInput;
        private int _liveProjectileCount;

        public NetworkVariable<int> ShipHealth = new(
            100,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> ShieldActive = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<int> ShieldHealth = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<Color> ShipColor = new(
            Color.white,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _meshRenderer = GetComponentInChildren<MeshRenderer>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            ShipColor.OnValueChanged += OnShipColorChanged;
            OnShipColorChanged(Color.white, ShipColor.Value);
        }

        public override void OnNetworkDespawn()
        {
            ShipColor.OnValueChanged -= OnShipColorChanged;
            base.OnNetworkDespawn();
        }

        public void Init(string playerId)
        {
            _playerId = playerId;
        }

        // value is 0–1 normalized; 0.5 = center dead-zone maps to 0 velocity
        public void SetJoystickX(float value)
        {
            if (!IsServer) return;
            _joystickInput.x = (value - 0.5f) * 2f;
        }

        public void SetJoystickY(float value)
        {
            if (!IsServer) return;
            _joystickInput.y = (value - 0.5f) * 2f;
        }

        public void Fire()
        {
            if (!IsServer) return;
            if (GameRoundManager.Instance?.CurrentPhase.Value != RoundPhase.Active) return;
            if (_liveProjectileCount >= _maxLiveProjectiles) return;
            if (_projectilePrefab == null) return;

            var pos = transform.position + Vector3.up * 0.6f;
            var go = Instantiate(_projectilePrefab, pos, Quaternion.identity);
            var netObj = go.GetComponent<NetworkObject>();
            netObj.Spawn();
            go.GetComponent<Projectile>()?.Init(this);
            _liveProjectileCount++;
        }

        public void OnProjectileDestroyed()
        {
            if (!IsServer) return;
            _liveProjectileCount = Mathf.Max(0, _liveProjectileCount - 1);
        }

        public void TakeDamage(int amount)
        {
            if (!IsServer) return;
            if (ShieldActive.Value && ShieldHealth.Value > 0)
            {
                int absorbed = Mathf.Min(amount, ShieldHealth.Value);
                ShieldHealth.Value -= absorbed;
                amount -= absorbed;
                if (ShieldHealth.Value <= 0)
                {
                    ShieldActive.Value = false;
                    CalloutManager.Instance?.ShowCalloutClientRpc("SHIELD BROKEN", 2f);
                }
            }
            if (amount > 0)
            {
                ShipHealth.Value = Mathf.Max(0, ShipHealth.Value - amount);
                if (ShipHealth.Value == 0)
                {
                    CalloutManager.Instance?.ShowCalloutClientRpc("SHIP DOWN", 2f);
                    GameRoundManager.Instance?.CheckWinConditions();
                }
            }
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;
            float speed = _moveSpeed * (EnvironmentController.Instance?.GlobalGameSpeed.Value ?? 1f);
            _rb.linearVelocity = new Vector3(_joystickInput.x, _joystickInput.y, 0f) * speed;
        }

        private void OnShipColorChanged(Color _, Color newColor)
        {
            if (_meshRenderer != null)
                _meshRenderer.material.color = newColor;
        }
    }
}