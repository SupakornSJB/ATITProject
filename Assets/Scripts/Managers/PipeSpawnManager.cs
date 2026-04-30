using System.Collections.Generic;
using Players;
using Shared;
using Unity.Netcode;
using UnityEngine;

namespace Managers
{
    public class PipeSpawnManager : NetworkSingleton<PipeSpawnManager>
    {
        [SerializeField] private GameObject _pipeBarrierPrefab;
        [SerializeField] private float _spawnInterval = 8f;
        [SerializeField] private float _spawnY = 8f;

        private float _spawnTimer;
        private bool _stageDevicePresent;

        private readonly List<NetworkObject> _livePipes = new();

        public void RegisterStageDevice()
        {
            _stageDevicePresent = true;
            Debug.Log("[PipeSpawnManager] Stage device registered — pipes enabled");
        }

        private void Update()
        {
            if (!IsServer) return;
            if (!_stageDevicePresent) return;
            if (GameSessionManager.Instance?.CurrentPhase.Value != SessionPhase.Active) return;

            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = _spawnInterval;
                SpawnPipe();
            }
        }

        private void SpawnPipe()
        {
            if (_pipeBarrierPrefab == null) return;
            var go = Instantiate(_pipeBarrierPrefab, new Vector3(0f, _spawnY, 0f), Quaternion.identity);
            var netObj = go.GetComponent<NetworkObject>();
            netObj.Spawn();
            _livePipes.Add(netObj);
        }

        public void FlipGap()
        {
            if (!IsServer || _livePipes.Count == 0) return;
            var latest = _livePipes[_livePipes.Count - 1];
            if (latest != null && latest.IsSpawned)
                latest.GetComponent<PipeBarrier>()?.FlipGap();
        }

        public void RemovePipe(NetworkObject netObj)
        {
            _livePipes.Remove(netObj);
        }

        public void DespawnAll()
        {
            if (!IsServer) return;
            foreach (var netObj in _livePipes)
                if (netObj != null && netObj.IsSpawned)
                    netObj.Despawn();
            _livePipes.Clear();
        }
    }
}