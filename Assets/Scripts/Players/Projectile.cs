using UnityEngine;

namespace Players
{
    // Visual-only projectile fired by PlayerShip — not a NetworkObject.
    // Spawned locally on every client via PlayerShip ClientRpc.
    // Server hit detection is handled by raycast in PlayerShip.Fire().
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float _speed = 12f;
        [SerializeField] private float _despawnX = 14f;

        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb != null) _rb.useGravity = false;
        }

        private void Start()
        {
            if (_rb != null)
                _rb.linearVelocity = Vector3.right * _speed;
        }

        private void Update()
        {
            if (transform.position.x >= _despawnX)
                Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<EnemyBird>() != null)
                Destroy(gameObject);
        }
    }
}