using Managers;
using Unity.Netcode;
using UnityEngine;

namespace Players
{
    // Server-controlled minion bird. Flaps randomly; damages ships on collision.
    public class EnemyBird : NetworkBehaviour
    {
        [SerializeField] private float _flapForce = 4f;
        [SerializeField] private float _flapIntervalMin = 1f;
        [SerializeField] private float _flapIntervalMax = 3f;

        private Rigidbody _rb;
        private float _flapTimer;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;
            _flapTimer = Random.Range(_flapIntervalMin, _flapIntervalMax);
            EnvironmentController.Instance?.RegisterGravityBody(_rb);
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) EnvironmentController.Instance?.UnregisterGravityBody(_rb);
            base.OnNetworkDespawn();
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;

            float speed = EnvironmentController.Instance?.GlobalGameSpeed.Value ?? 1f;
            _flapTimer -= Time.fixedDeltaTime;
            if (_flapTimer <= 0f)
            {
                _flapTimer = Random.Range(_flapIntervalMin, _flapIntervalMax);
                _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, _flapForce * speed, 0f);
            }
        }

        private void OnCollisionEnter(Collision other)
        {
            if (!IsServer) return;
            var ship = other.gameObject.GetComponentInParent<PlayerShip>();
            if (ship != null)
            {
                ship.TakeDamage(10);
                EnemySpawnManager.Instance?.RemoveMinion(GetComponent<NetworkObject>());
                GetComponent<NetworkObject>().Despawn();
            }
        }
    }
}