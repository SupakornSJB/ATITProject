using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace Server
{
    public static class ConnectionApprovalHandler
    {
        private static readonly HashSet<string> ValidPlayerIds = new()
            { "player-1", "player-2", "player-3", "player-4", "stage" };

        public static void Register()
        {
            NetworkManager.Singleton.ConnectionApprovalCallback += Approve;
        }

        private static void Approve(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            string clientLabel;
            try { clientLabel = Encoding.UTF8.GetString(request.Payload); }
            catch
            {
                Reject(response, "Invalid payload encoding");
                return;
            }

            response.Approved = true;
            response.CreatePlayerObject = false;
            Debug.Log($"[ConnectionApproval] Approved: '{clientLabel}' (clientId={request.ClientNetworkId})");

            if (clientLabel == "stage")
            {
                Managers.PipeSpawnManager.Instance?.RegisterStageDevice();
            }
            else if (ValidPlayerIds.Contains(clientLabel))
            {
                // Spawn the player's NetworkObject immediately on connection.
                // SpawnForPlayer is idempotent — safe to call again from IoTInputManager.
                Managers.BirdSpawnManager.Instance?.SpawnForPlayer(clientLabel);
            }
        }

        public static void ReleasePlayer(string playerId) { }

        private static void Reject(NetworkManager.ConnectionApprovalResponse r, string reason)
        {
            r.Approved = false;
            r.Reason = reason;
            Debug.LogWarning($"[ConnectionApproval] Rejected — {reason}");
        }
    }
}