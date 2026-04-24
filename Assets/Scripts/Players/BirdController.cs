using Unity.Netcode;
using UnityEngine;

namespace Players
{
    public class BirdController : NetworkBehaviour
    {
        [SerializeField] private float _flapForce = 5f;
        [SerializeField] private float _deathBoundaryY = -6f;

        private Rigidbody _rb;

        public NetworkVariable<int> Score = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<bool> IsAlive = new(
            true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            IsAlive.OnValueChanged += OnAliveChanged;
            if (!IsOwner)
                _rb.isKinematic = true;
        }

        public override void OnNetworkDespawn()
        {
            IsAlive.OnValueChanged -= OnAliveChanged;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsOwner || !IsAlive.Value) return;

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
                _rb.linearVelocity = new Vector3(0f, _flapForce, 0f);

            if (transform.position.y < _deathBoundaryY)
                DieServerRpc();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsOwner || !IsAlive.Value) return;

            if (other.CompareTag("Pipe"))
                DieServerRpc();
            else if (other.CompareTag("ScoreZone"))
                IncrementScoreServerRpc();
        }

        private void OnAliveChanged(bool _, bool isAlive)
        {
            _rb.isKinematic = !isAlive || !IsOwner;
        }

        [ServerRpc]
        private void DieServerRpc()
        {
            if (!IsAlive.Value) return;
            IsAlive.Value = false;
        }

        [ServerRpc]
        private void IncrementScoreServerRpc()
        {
            Score.Value++;
        }
    }
}