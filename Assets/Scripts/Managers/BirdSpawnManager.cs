using System.Collections.Generic;
using Players;
using Shared;
using Unity.Netcode;
using UnityEngine;

namespace Managers
{
    public class BirdSpawnManager : NetworkSingleton<BirdSpawnManager>
    {
        [SerializeField] private GameObject _shipPrefab;
        [SerializeField] private GameObject _bossBirdPrefab;

        private readonly Dictionary<string, NetworkObject> _ships = new();
        private readonly Dictionary<string, NetworkObject> _bossBirds = new();

        private static readonly Dictionary<string, Vector3> ShipSpawns = new()
        {
            { "player-1", new Vector3(-3f, -5f, 0f) },
            { "player-2", new Vector3(3f, -5f, 0f) },
        };

        private static readonly Dictionary<string, Vector3> BossSpawns = new()
        {
            { "player-3", new Vector3(-3f, 5f, 0f) },
            { "player-4", new Vector3(3f, 5f, 0f) },
        };

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

        // Idempotent: spawns the correct NetworkObject for a given playerId role.
        // Called from ConnectionApprovalHandler on NGO connect, and from IoTInputManager on first input.
        public void SpawnForPlayer(string playerId)
        {
            if (!IsServer) return;
            if (playerId is "player-1" or "player-2")
                SpawnShip(playerId);
            else if (playerId is "player-3" or "player-4")
                SpawnBossBird(playerId);
            // "stage" intentionally gets no NetworkObject
        }

        private void SpawnShip(string playerId)
        {
            if (_ships.ContainsKey(playerId) || _shipPrefab == null) return;
            var pos = ShipSpawns.GetValueOrDefault(playerId, Vector3.zero);
            var go = Instantiate(_shipPrefab, pos, Quaternion.identity);
            var netObj = go.GetComponent<NetworkObject>();
            netObj.Spawn();
            var ship = go.GetComponent<PlayerShip>();
            ship.ShipColor.Value = PlayerDistinguisherManager.Instance.RequestColor(playerId);
            ship.Init(playerId);
            _ships[playerId] = netObj;
            Debug.Log($"[BirdSpawnManager] Spawned ship for '{playerId}'");
        }

        private void SpawnBossBird(string playerId)
        {
            if (_bossBirds.ContainsKey(playerId) || _bossBirdPrefab == null) return;
            var pos = BossSpawns.GetValueOrDefault(playerId, Vector3.zero);
            var go = Instantiate(_bossBirdPrefab, pos, Quaternion.identity);
            var netObj = go.GetComponent<NetworkObject>();
            netObj.Spawn();
            var boss = go.GetComponent<BossEnemyBird>();
            boss.BossColor.Value = PlayerDistinguisherManager.Instance.RequestColor(playerId);
            boss.Init(playerId);
            _bossBirds[playerId] = netObj;
            Debug.Log($"[BirdSpawnManager] Spawned boss bird for '{playerId}'");
        }

        public void HandleBossBirdDeath(string playerId)
        {
            if (!_bossBirds.TryGetValue(playerId, out var netObj)) return;
            _bossBirds.Remove(playerId);
            if (netObj != null && netObj.IsSpawned)
                netObj.Despawn();
            Debug.Log($"[BirdSpawnManager] Boss bird despawned for '{playerId}'");
        }

        public void HandleShipDeath(string playerId)
        {
            if (!_ships.TryGetValue(playerId, out var netObj)) return;
            _ships.Remove(playerId);
            if (netObj != null && netObj.IsSpawned)
                netObj.Despawn();
            Debug.Log($"[BirdSpawnManager] Ship despawned for '{playerId}'");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            // Clients are spectators — no avatar cleanup needed here.
            // Player avatars are server-owned and persist until GameRoundManager handles disconnect.
        }

        public bool TryGetShip(string playerId, out PlayerShip ship)
        {
            ship = null;
            if (!_ships.TryGetValue(playerId, out var netObj)) return false;
            ship = netObj != null ? netObj.GetComponent<PlayerShip>() : null;
            return ship != null;
        }

        public bool TryGetBossBird(string playerId, out BossEnemyBird boss)
        {
            boss = null;
            if (!_bossBirds.TryGetValue(playerId, out var netObj)) return false;
            boss = netObj != null ? netObj.GetComponent<BossEnemyBird>() : null;
            return boss != null;
        }

        public PlayerShip GetShip(string playerId)
        {
            TryGetShip(playerId, out var ship);
            return ship;
        }

        public BossEnemyBird GetBossBird(string playerId)
        {
            TryGetBossBird(playerId, out var boss);
            return boss;
        }

        // Phase 2 backward compat stubs
        public bool TryGetBird(string playerId, out PlayerBird bird)
        {
            bird = null;
            return false;
        }

        public void HandleBirdDeath(string playerId) { }
    }
}