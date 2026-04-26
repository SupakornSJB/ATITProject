using Shared;
using Unity.Netcode;

namespace Managers
{
    public class CalloutManager : NetworkSingleton<CalloutManager>
    {
        [ClientRpc]
        public void ShowCalloutClientRpc(string text, float duration)
        {
            UI.RoundUIManager.Instance?.ShowCallout(text, duration);
        }
    }
}