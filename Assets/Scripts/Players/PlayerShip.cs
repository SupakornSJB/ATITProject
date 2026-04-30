using System.Collections;
using Managers;
using Unity.Netcode;
using UnityEngine;

namespace Players
{
    public class PlayerShip : NetworkBehaviour
    {
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private float _projectileSpeed = 12f;
        [SerializeField] private float _fireCooldown = 0.15f;

        private Rigidbody _rb;
        private MeshRenderer _meshRenderer;
        private string _playerId;
        private Vector2 _joystickInput;
        private float _lastFireTime = -999f;

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

        public NetworkVariable<int> PlayerIndex = new(
            0,
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
            PlayerIndex.Value = playerId == "player-1" ? 1 : 2;
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
            if (GameSessionManager.Instance?.CurrentPhase.Value != SessionPhase.Active) return;
            if (Time.time - _lastFireTime < _fireCooldown) return;
            _lastFireTime = Time.time;

            var firePos = transform.position + Vector3.right * 0.6f;

            // Server: simulate projectile traveling right, OverlapSphere each frame
            StartCoroutine(SimulateProjectile(firePos));

            // All clients: spawn visual projectile
            SpawnProjectileClientRpc(firePos);
        }

        private IEnumerator SimulateProjectile(Vector3 pos)
        {
            float despawnX = 14f;
            while (pos.x < despawnX)
            {
                float speed = _projectileSpeed * (EnvironmentController.Instance?.GlobalGameSpeed.Value ?? 1f);
                pos += Vector3.right * speed * Time.fixedDeltaTime;

                var hits = Physics.OverlapSphere(pos, 0.3f);
                foreach (var hit in hits)
                {
                    var enemy = hit.GetComponentInParent<EnemyBird>();
                    if (enemy != null && enemy.IsSpawned)
                    {
                        EnemySpawnManager.Instance?.RemoveMinion(enemy.GetComponent<NetworkObject>());
                        enemy.GetComponent<NetworkObject>()?.Despawn();
                        yield break;
                    }
                }
                yield return new WaitForFixedUpdate();
            }
        }

        [ClientRpc]
        private void SpawnProjectileClientRpc(Vector3 startPos)
        {
            if (_projectilePrefab == null) return;
            Instantiate(_projectilePrefab, startPos, Quaternion.identity);
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
                    CalloutManager.Instance?.TryShowCallout("SHIELD BROKEN", 2f);
                }
            }
            if (amount > 0)
            {
                ShipHealth.Value = Mathf.Max(0, ShipHealth.Value - amount);
                if (ShipHealth.Value == 0)
                {
                    CalloutManager.Instance?.TryShowCallout("SHIP DOWN", 2f);
                    GameSessionManager.Instance?.CheckWinConditions();
                    BirdSpawnManager.Instance?.HandleShipDeath(_playerId);
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