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

        private readonly Dictionary<string, NetworkObject> _ships = new();

        private static readonly Dictionary<string, Vector3> ShipSpawns = new()
        {
            { "player-1", new Vector3(-8f,  2f, 0f) },
            { "player-2", new Vector3(-8f, -2f, 0f) },
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
            // "stage" and bird players intentionally get no NetworkObject
        }

        private void SpawnShip(string playerId)
        {
            if (_ships.ContainsKey(playerId) || _shipPrefab == null) return;
            var pos = ShipSpawns.GetValueOrDefault(playerId, Vector3.zero);
            var go = Instantiate(_shipPrefab, pos, Quaternion.identity);
            var netObj = go.GetComponent<NetworkObject>();
            netObj.Spawn();
            var ship = go.GetComponent<PlayerShip>();
            var color = PlayerDistinguisherManager.Instance.RequestColor(playerId);
            ship.ShipColor.Value = color;
            ship.Init(playerId);
            _ships[playerId] = netObj;
            Debug.Log($"[BirdSpawnManager] Spawned ship for '{playerId}'");
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
            // Player avatars are server-owned and persist until GameSessionManager handles disconnect.
        }

        public bool TryGetShip(string playerId, out PlayerShip ship)
        {
            ship = null;
            if (!_ships.TryGetValue(playerId, out var netObj)) return false;
            ship = netObj != null ? netObj.GetComponent<PlayerShip>() : null;
            return ship != null;
        }

        public PlayerShip GetShip(string playerId)
        {
            TryGetShip(playerId, out var ship);
            return ship;
        }

        public List<PlayerShip> GetAllShips()
        {
            var result = new List<PlayerShip>();
            foreach (var netObj in _ships.Values)
            {
                var ship = netObj != null ? netObj.GetComponent<PlayerShip>() : null;
                if (ship != null) result.Add(ship);
            }
            return result;
        }

        public bool HasAnyShip() => _ships.Count > 0;

        // Called on round reset — despawns all player-owned entities so the next round
        // starts clean with fresh spawns at correct positions.
        public void DespawnAllPlayerEntities()
        {
            if (!IsServer) return;
            foreach (var netObj in _ships.Values)
                if (netObj != null && netObj.IsSpawned) netObj.Despawn();
            _ships.Clear();

        }

    }
}