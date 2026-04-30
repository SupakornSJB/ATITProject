using UnityEngine;

namespace Players
{
    public class BirdProjectile : MonoBehaviour
    {
        [SerializeField] private float _maxTravelDistance = 28f;

        private Vector3 _direction = Vector3.left;
        private float _speed = 10f;
        private Vector3 _spawnPos;
        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb != null) _rb.useGravity = false;
        }

        public void Init(Vector3 direction, float speed)
        {
            _spawnPos = transform.position;
            _direction = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.left;
            _speed = speed;
        }

        private void FixedUpdate()
        {
            if (_rb != null)
                _rb.linearVelocity = _direction * _speed;

            if (Vector3.Distance(transform.position, _spawnPos) >= _maxTravelDistance)
                Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerShip>() != null)
                Destroy(gameObject);
        }
    }
}