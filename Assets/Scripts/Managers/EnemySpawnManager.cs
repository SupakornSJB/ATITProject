using System.Collections.Generic;
using Players;
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

        private float _spawnInterval = 5f;
        private float _spawnTimer;
        private bool _frenzyActive;
        private float _frenzyTimer;
        private const float FrenzyDuration = 5f;

        private readonly List<NetworkObject> _liveMinions = new();

        private void Update()
        {
            if (!IsServer) return;
            if (GameRoundManager.Instance?.CurrentPhase.Value != RoundPhase.Active) return;

            if (_frenzyActive)
            {
                _frenzyTimer -= Time.deltaTime;
                if (_frenzyTimer <= 0f)
                    _frenzyActive = false;
            }

            float effectiveInterval = _frenzyActive
                ? Mathf.Max(1f, _spawnInterval * 0.5f)
                : _spawnInterval;

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
            _spawnInterval = Mathf.Max(1f, seconds);
        }

        public void TriggerFrenzy()
        {
            _frenzyActive = true;
            _frenzyTimer = FrenzyDuration;
        }

        public void RemoveMinion(NetworkObject netObj)
        {
            _liveMinions.Remove(netObj);
        }
    }
}