using System.Collections;
using Managers;
using Unity.Netcode;
using UnityEngine;

namespace Players
{
    public class EnemyBird : NetworkBehaviour
    {
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private GameObject _birdProjectilePrefab;
        [SerializeField] private float _projectileSpeed = 10f;
        [SerializeField] private float _shootCooldownMin = 0.15f;
        [SerializeField] private float _shootCooldownMax = 0.4f;
        [SerializeField] private float _despawnX = -12f;

        private Rigidbody _rb;
        private bool _dead;
        private float _nextShootTime;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;
            _nextShootTime = Time.time + Random.Range(0.1f, 0.5f);
        }

        private void Update()
        {
            if (!IsServer || _dead) return;
            if (GameSessionManager.Instance?.CurrentPhase.Value != SessionPhase.Active) return;

            if (Time.time >= _nextShootTime)
            {
                float gameSpeed = EnvironmentController.Instance?.GlobalGameSpeed.Value ?? 1f;
                float cooldown = Random.Range(_shootCooldownMin, _shootCooldownMax) / Mathf.Max(0.1f, gameSpeed);
                _nextShootTime = Time.time + cooldown;
                FireAtNearestShip();
            }
        }

        private void FixedUpdate()
        {
            if (!IsServer || _dead) return;
            float speed = _moveSpeed * (EnvironmentController.Instance?.GlobalGameSpeed.Value ?? 1f);
            _rb.linearVelocity = new Vector3(-speed, 0f, 0f);

            if (transform.position.x <= _despawnX)
                DespawnWithDamage();
        }

        private void DespawnWithDamage()
        {
            if (_dead) return;
            _dead = true;
            var ships = BirdSpawnManager.Instance?.GetAllShips();
            if (ships != null)
            {
                PlayerShip nearest = null;
                float minDist = float.MaxValue;
                foreach (var ship in ships)
                {
                    float d = Vector3.Distance(transform.position, ship.transform.position);
                    if (d < minDist) { minDist = d; nearest = ship; }
                }
                nearest?.TakeDamage(15);
            }
            EnemySpawnManager.Instance?.RemoveMinion(GetComponent<NetworkObject>());
            GetComponent<NetworkObject>().Despawn();
        }

        private void FireAtNearestShip()
        {
            var ships = BirdSpawnManager.Instance?.GetAllShips();
            if (ships == null || ships.Count == 0) return;

            PlayerShip nearest = null;
            float minDist = float.MaxValue;
            foreach (var ship in ships)
            {
                if (ship == null || ship.ShipHealth.Value <= 0) continue;
                float d = Vector3.Distance(transform.position, ship.transform.position);
                if (d < minDist) { minDist = d; nearest = ship; }
            }
            if (nearest == null) return;

            var dir = nearest.transform.position - transform.position;
            dir.z = 0f;
            var direction = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.left;

            float gameSpeed = EnvironmentController.Instance?.GlobalGameSpeed.Value ?? 1f;
            float effectiveSpeed = _projectileSpeed * gameSpeed;
            float travelTime = dir.magnitude / effectiveSpeed;

            SpawnProjectileClientRpc(transform.position, direction, effectiveSpeed);
            StartCoroutine(DelayedHit(nearest, travelTime));
        }

        private IEnumerator DelayedHit(PlayerShip target, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (target != null && target.ShipHealth.Value > 0)
                target.TakeDamage(10);
        }

        [ClientRpc]
        private void SpawnProjectileClientRpc(Vector3 startPos, Vector3 direction, float speed)
        {
            if (_birdProjectilePrefab == null) return;
            var go = Instantiate(_birdProjectilePrefab, startPos, Quaternion.identity);
            go.GetComponent<BirdProjectile>()?.Init(direction, speed);
        }

        private void OnCollisionEnter(Collision other)
        {
            if (!IsServer || _dead) return;
            var ship = other.gameObject.GetComponentInParent<PlayerShip>();
            if (ship != null)
            {
                _dead = true;
                ship.TakeDamage(10);
                EnemySpawnManager.Instance?.RemoveMinion(GetComponent<NetworkObject>());
                GetComponent<NetworkObject>().Despawn();
            }
        }
    }
}