using Managers;
using Unity.Netcode;
using UnityEngine;

namespace Players
{
    public class BossEnemyBird : NetworkBehaviour
    {
        [SerializeField] private float _flapForce = 5f;
        [SerializeField] private float _bottomY = -10f;

        private Rigidbody _rb;
        private MeshRenderer _meshRenderer;
        private string _playerId;
        private bool _criticalCalloutFired;

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
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            BossColor.OnValueChanged += OnBossColorChanged;
            OnBossColorChanged(Color.white, BossColor.Value);
        }

        public override void OnNetworkDespawn()
        {
            BossColor.OnValueChanged -= OnBossColorChanged;
            base.OnNetworkDespawn();
        }

        public void Init(string playerId)
        {
            _playerId = playerId;
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
                CalloutManager.Instance?.ShowCalloutClientRpc("BOSS CRITICAL", 2f);
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
                CalloutManager.Instance?.ShowCalloutClientRpc("BIRDS WIN", 8f);
                GameRoundManager.Instance?.TransitionTo(RoundPhase.Ended);
            }
        }

        private void OnBossColorChanged(Color _, Color newColor)
        {
            if (_meshRenderer != null)
                _meshRenderer.material.color = newColor;
        }
    }
}