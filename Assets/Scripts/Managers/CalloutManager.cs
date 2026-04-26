using Shared;
using Unity.Netcode;

namespace Managers
{
    public class CalloutManager : NetworkSingleton<CalloutManager>
    {
        [ClientRpc]
        public void ShowCalloutClientRpc(string text, float duration)
        {
#if !UNITY_SERVER
            UI.RoundUIManager.Instance?.ShowCallout(text, duration);
#endif
        }

        // Safe wrapper — drops the callout silently if the NetworkObject isn't spawned yet.
        // Use this instead of ShowCalloutClientRpc when calling from server-side game logic.
        public void TryShowCallout(string text, float duration)
        {
            if (!IsSpawned) return;
            ShowCalloutClientRpc(text, duration);
        }
    }
}