using Managers;
using Unity.Netcode;
using UnityEngine;

namespace Players
{
    public class PipeBarrier : NetworkBehaviour
    {
        [SerializeField] private float _descentSpeed = 2f;
        [SerializeField] private float _despawnY = -10f;
        [SerializeField] private GameObject _upperSection;
        [SerializeField] private GameObject _lowerSection;

        public NetworkVariable<bool> GapIsUpper = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            GapIsUpper.OnValueChanged += OnGapChanged;
            ApplyGapLayout(GapIsUpper.Value);
            if (IsServer && _rb != null)
                _rb.linearVelocity = Vector3.down * _descentSpeed;
        }

        public override void OnNetworkDespawn()
        {
            GapIsUpper.OnValueChanged -= OnGapChanged;
            base.OnNetworkDespawn();
        }

        public void FlipGap()
        {
            if (!IsServer) return;
            GapIsUpper.Value = !GapIsUpper.Value;
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;
            float speed = _descentSpeed * (EnvironmentController.Instance?.GlobalGameSpeed.Value ?? 1f);
            _rb.linearVelocity = Vector3.down * speed;

            if (transform.position.y <= _despawnY)
            {
                PipeSpawnManager.Instance?.RemovePipe(GetComponent<NetworkObject>());
                GetComponent<NetworkObject>().Despawn();
            }
        }

        private void OnGapChanged(bool _, bool gapIsUpper) => ApplyGapLayout(gapIsUpper);

        private void ApplyGapLayout(bool gapIsUpper)
        {
            if (_upperSection != null) _upperSection.SetActive(!gapIsUpper);
            if (_lowerSection != null) _lowerSection.SetActive(gapIsUpper);
        }
    }
}