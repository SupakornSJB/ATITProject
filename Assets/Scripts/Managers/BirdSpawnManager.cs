using System.Collections.Generic;
using Players;
using Shared;
using Unity.Netcode;
using UnityEngine;

namespace Managers
{
    public class BirdSpawnManager : NetworkSingleton<BirdSpawnManager>
    {
        [SerializeField] private GameObject _birdPrefab;

        private readonly Dictionary<string, NetworkObject> _playerBirds = new();
        private readonly Dictionary<string, Vector3> _spawnPositions = new();
        private readonly Dictionary<string, Color> _playerColors = new();
        private int _spawnIndex;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            base.OnNetworkDespawn();
        }

        // Assigns a slot and color the first time a playerId is seen — idempotent
        private void EnsurePlayerSlot(string playerId)
        {
            if (_spawnPositions.ContainsKey(playerId)) return;
            _spawnPositions[playerId] = new Vector3(_spawnIndex * 2f, 0f, 0f);
            _playerColors[playerId] = PlayerDistinguisherManager.Instance.RequestColor(playerId);
            _spawnIndex++;
            Debug.Log($"[BirdSpawnManager] Allocated slot for '{playerId}'");
        }

        // Spawns the bird for playerId if not already alive. Call from IoTInputManager on first input.
        public void SpawnBirdForPlayer(string playerId)
        {
            if (_playerBirds.ContainsKey(playerId)) return;

            EnsurePlayerSlot(playerId);

            var spawnPos = new Vector3(_spawnPositions[playerId].x, 0f, 0f);
            var go = Instantiate(_birdPrefab, spawnPos, Quaternion.identity);
            var netObj = go.GetComponent<NetworkObject>();
            netObj.Spawn(); // server-owned — no NGO client needed

            var bird = go.GetComponent<PlayerBird>();
            bird.PlayerColor.Value = _playerColors[playerId];
            bird.Init(playerId);

            _playerBirds[playerId] = netObj;
            Debug.Log($"[BirdSpawnManager] Spawned bird for '{playerId}'");
        }

        // Called by PlayerBird.Die()
        public void HandleBirdDeath(string playerId)
        {
            if (!_playerBirds.TryGetValue(playerId, out var netObj)) return;
            _playerBirds.Remove(playerId);
            if (netObj != null)
                netObj.Despawn();
            Debug.Log($"[BirdSpawnManager] Bird despawned for '{playerId}' — will respawn on next input");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            // Spectator clients disconnecting — no bird cleanup needed
        }

        public bool TryGetBird(string playerId, out PlayerBird bird)
        {
            bird = null;
            if (!_playerBirds.TryGetValue(playerId, out var netObj)) return false;
            bird = netObj != null ? netObj.GetComponent<PlayerBird>() : null;
            return bird != null;
        }
    }
}