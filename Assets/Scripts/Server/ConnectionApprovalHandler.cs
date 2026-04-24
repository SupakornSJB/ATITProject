using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace Server
{
    // Approves Unity spectator clients. Birds are now spawned from gRPC input,
    // not from NGO client connections — any client with a valid payload is a spectator.
    public static class ConnectionApprovalHandler
    {
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
                Reject(response, "Invalid payload");
                return;
            }

            response.Approved = true;
            response.CreatePlayerObject = false;
            Debug.Log($"[ConnectionApproval] Approved spectator: '{clientLabel}' (clientId={request.ClientNetworkId})");
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