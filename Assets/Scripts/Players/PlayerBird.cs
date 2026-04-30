using Unity.Netcode;
using UnityEngine;

namespace Players
{
    public class PlayerBird : NetworkBehaviour
    {
        [SerializeField] private float _flapForce = 5f;
        [SerializeField] private float _maxHorizontalSpeed = 3f;
        [SerializeField] private float _deathY = -10f;

        private Rigidbody _rb;
        private MeshRenderer _meshRenderer;
        private string _playerId;

        public NetworkVariable<Color> PlayerColor = new(
            Color.white,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> IsAlive = new(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<float> HorizontalSpeed = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private float _currentHorizontalInput;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _meshRenderer = GetComponentInChildren<MeshRenderer>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            PlayerColor.OnValueChanged += OnPlayerColorChanged;
            OnPlayerColorChanged(Color.white, PlayerColor.Value);
        }

        public override void OnNetworkDespawn()
        {
            PlayerColor.OnValueChanged -= OnPlayerColorChanged;
            base.OnNetworkDespawn();
        }

        // Called by BirdSpawnManager immediately after spawning
        public void Init(string playerId)
        {
            _playerId = playerId;
        }

        public void OnButtonPressed(string inputName)
        {
            if (!IsServer) return;
            switch (inputName)
            {
                case "jump":
                    _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, _flapForce, 0f);
                    break;
                case "fire":
                    break;
            }
        }

        public void OnButtonReleased(string inputName) { }

        public void OnAnalogChanged(string inputName, float value)
        {
            if (!IsServer) return;
            if (inputName == "speed")
            {
                _currentHorizontalInput = value;
                HorizontalSpeed.Value = value;
            }
        }

        // Called by DeathBarrier on trigger — despawns this bird; respawns on next input
        public void Die()
        {
            if (!IsServer) return;
            Debug.Log($"[PlayerBird] '{_playerId}' hit the barrier");
            Managers.BirdSpawnManager.Instance.HandleShipDeath(_playerId);
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;
            if (_currentHorizontalInput != 0f)
                _rb.AddForce(Vector3.right * (_currentHorizontalInput * _maxHorizontalSpeed), ForceMode.Force);
            if (transform.position.y < _deathY)
                Die();
        }

        private void OnPlayerColorChanged(Color _, Color newColor)
        {
            if (_meshRenderer != null)
                _meshRenderer.material.color = newColor;
        }
    }
}