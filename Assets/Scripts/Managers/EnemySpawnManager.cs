using System.Collections.Generic;
using Shared;
using Unity.Netcode;
using UnityEngine;

namespace Managers
{
    public class EnemySpawnManager : NetworkSingleton<EnemySpawnManager>
    {
        [SerializeField] private GameObject _enemyBirdPrefab;
        [SerializeField] private float _spawnXMin = -5f;
        [SerializeField] private float _spawnXMax = 5f;
        [SerializeField] private float _spawnY = 8f;

        // Base interval set by EnvironmentController via spawn_rate key.
        // Zone D halves it on top; floor is always 1s.
        private float _baseInterval = 5f;
        private float _spawnTimer;

        private readonly List<NetworkObject> _liveMinions = new();

        private void Update()
        {
            if (!IsServer) return;
            if (GameRoundManager.Instance?.CurrentPhase.Value != RoundPhase.Active) return;

            bool isZoneD = EnvironmentController.Instance?.UltrasonicZone.Value == 4;
            float effectiveInterval = Mathf.Max(1f, _baseInterval * (isZoneD ? 0.5f : 1.0f));

            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = effectiveInterval;
                SpawnMinion();
            }
        }

        private void SpawnMinion()
        {
            if (_enemyBirdPrefab == null) return;
            float x = Random.Range(_spawnXMin, _spawnXMax);
            var go = Instantiate(_enemyBirdPrefab, new Vector3(x, _spawnY, 0f), Quaternion.identity);
            var netObj = go.GetComponent<NetworkObject>();
            netObj.Spawn();
            _liveMinions.Add(netObj);
        }

        public void DespawnAll()
        {
            if (!IsServer) return;
            foreach (var netObj in _liveMinions)
                if (netObj != null && netObj.IsSpawned)
                    netObj.Despawn();
            _liveMinions.Clear();
        }

        public void SetSpawnInterval(float seconds)
        {
            _baseInterval = Mathf.Max(1f, seconds);
        }

        public void RemoveMinion(NetworkObject netObj)
        {
            _liveMinions.Remove(netObj);
        }
    }
}