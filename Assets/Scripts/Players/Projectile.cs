using Managers;
using Unity.Netcode;
using UnityEngine;

namespace Players
{
    public class Projectile : NetworkBehaviour
    {
        [SerializeField] private float _speed = 10f;
        [SerializeField] private float _topY = 12f;

        private Rigidbody _rb;
        private PlayerShip _owner;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        public void Init(PlayerShip owner)
        {
            _owner = owner;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer && _rb != null)
                _rb.linearVelocity = Vector3.up * _speed;
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;
            float speed = _speed * (EnvironmentController.Instance?.GlobalGameSpeed.Value ?? 1f);
            _rb.linearVelocity = Vector3.up * speed;

            if (transform.position.y >= _topY)
                DestroyProjectile();
        }

        // Projectile collider must be set as Trigger in the prefab
        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;
            var boss = other.GetComponentInParent<BossEnemyBird>();
            if (boss != null)
            {
                boss.TakeDamage(20);
                DestroyProjectile();
            }
        }

        private void DestroyProjectile()
        {
            _owner?.OnProjectileDestroyed();
            GetComponent<NetworkObject>().Despawn();
        }
    }
}