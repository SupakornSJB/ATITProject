using Managers;
using Unity.Netcode;
using UnityEngine;

namespace Players
{
    public class BossEnemyBird : NetworkBehaviour
    {
        [SerializeField] private float _flapForce = 15f;
        [SerializeField] private float _bottomY = -20f;
        [SerializeField] private int _fallDamage = 25;
        [SerializeField] private int _pipeDamage = 10;

        private Rigidbody _rb;
        private MeshRenderer _meshRenderer;
        private string _playerId;
        private Vector3 _spawnPosition;
        private bool _criticalCalloutFired;
        private bool _pipeDamageCooldown;

        public NetworkVariable<int> BossHealth = new(
            100,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<Color> BossColor = new(
            Color.white,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _meshRenderer = GetComponentInChildren<MeshRenderer>();
            _rb.useGravity = false;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            BossColor.OnValueChanged += OnBossColorChanged;
            OnBossColorChanged(Color.white, BossColor.Value);
            if (IsServer) EnvironmentController.Instance?.RegisterGravityBody(_rb);
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) EnvironmentController.Instance?.UnregisterGravityBody(_rb);
            BossColor.OnValueChanged -= OnBossColorChanged;
            base.OnNetworkDespawn();
        }

        public void Init(string playerId, Vector3 spawnPosition)
        {
            _playerId = playerId;
            _spawnPosition = spawnPosition;
            _criticalCalloutFired = false;
        }

        public void Flap()
        {
            if (!IsServer) return;
            float bonus = EnvironmentController.Instance?.GetBossFlapBonus() ?? 1f;
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, _flapForce * bonus, 0f);
        }

        public void TakeDamage(int amount)
        {
            if (!IsServer) return;
            BossHealth.Value = Mathf.Max(0, BossHealth.Value - amount);

            if (!_criticalCalloutFired && BossHealth.Value <= 25 && BossHealth.Value > 0)
            {
                _criticalCalloutFired = true;
                CalloutManager.Instance?.TryShowCallout("BOSS CRITICAL", 2f);
            }

            if (BossHealth.Value <= 0)
            {
                GameRoundManager.Instance?.CheckWinConditions();
                BirdSpawnManager.Instance?.HandleBossBirdDeath(_playerId);
            }
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;
            if (GameRoundManager.Instance?.CurrentPhase.Value != RoundPhase.Active) return;

            if (transform.position.y < _bottomY)
            {
                TakeDamage(_fallDamage);
                if (BossHealth.Value > 0)
                    ResetToSpawn();
            }
        }

        private void OnCollisionEnter(Collision other)
        {
            if (!IsServer) return;
            if (_pipeDamageCooldown) return;
            if (other.gameObject.GetComponentInParent<PipeBarrier>() != null)
            {
                TakeDamage(_pipeDamage);
                // Brief cooldown so one collision doesn't fire many times in a single frame
                _pipeDamageCooldown = true;
                Invoke(nameof(ResetPipeCooldown), 0.5f);
            }
        }

        private void ResetPipeCooldown() => _pipeDamageCooldown = false;

        private void ResetToSpawn()
        {
            _rb.linearVelocity = Vector3.zero;
            transform.position = _spawnPosition;
        }

        private void OnBossColorChanged(Color _, Color newColor)
        {
            if (_meshRenderer != null)
                _meshRenderer.material.color = newColor;
        }
    }
}