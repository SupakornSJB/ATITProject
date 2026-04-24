using System.Collections;
using Shared;
using Unity.Netcode;
using UnityEngine;

namespace Managers
{
    public class PipeSpawner : NetworkSingleton<PipeSpawner>
    {
        [SerializeField] private GameObject _pipePrefab;
        [SerializeField] private float _spawnInterval = 2.5f;
        [SerializeField] private float _spawnX = 10f;
        [SerializeField] private float _verticalRange = 2.5f;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
                StartCoroutine(SpawnLoop());
        }

        private IEnumerator SpawnLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(_spawnInterval);
                SpawnPipePair();
            }
        }

        private void SpawnPipePair()
        {
            float y = Random.Range(-_verticalRange, _verticalRange);
            var go = Instantiate(_pipePrefab, new Vector3(_spawnX, y, 0f), Quaternion.identity);
            go.GetComponent<NetworkObject>().Spawn();
        }
    }
}