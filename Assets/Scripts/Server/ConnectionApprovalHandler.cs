using System.Collections.Concurrent;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace Server
{
    // Phase 4: all Unity clients are spectators — approve unconditionally.
    // Player objects are server-spawned when gRPC events arrive, not on client connect.
    public static class ConnectionApprovalHandler
    {
        // Maps clientNetworkId → preferredPlayerId (may be empty string)
        private static readonly ConcurrentDictionary<ulong, string> _clientPreferences = new();

        public static void Register()
        {
            NetworkManager.Singleton.ConnectionApprovalCallback += Approve;
        }

        private static void Approve(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = true;
            response.CreatePlayerObject = false;

            string payload;
            try { payload = Encoding.UTF8.GetString(request.Payload); }
            catch { payload = "spectator"; }

            string preferredPlayerId = "";
            if (payload.StartsWith("spectator:"))
                preferredPlayerId = payload.Substring("spectator:".Length);

            _clientPreferences[request.ClientNetworkId] = preferredPlayerId;
            Debug.Log($"[ConnectionApproval] Spectator approved — clientId={request.ClientNetworkId} preferred='{preferredPlayerId}'");
        }

        /// <summary>Returns the preferredPlayerId for a connected client, or empty string if none.</summary>
        public static string GetPreferredPlayerId(ulong clientNetworkId) =>
            _clientPreferences.TryGetValue(clientNetworkId, out var p) ? p : "";

        public static void OnClientDisconnected(ulong clientNetworkId) =>
            _clientPreferences.TryRemove(clientNetworkId, out _);
    }
}
