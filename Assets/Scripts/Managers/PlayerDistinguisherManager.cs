using System.Collections.Generic;
using Shared;
using Unity.Netcode;
using UnityEngine;

namespace Managers
{
    public class PlayerDistinguisherManager : NetworkSingleton<PlayerDistinguisherManager>
    {
        public NetworkVariable<List<Color>> allPossibleColorList = new ();

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            allPossibleColorList.Value = new List<Color> {Color.red, Color.blue, Color.green, Color.yellow};
        }

        public Color RequestColor()
        {
            return allPossibleColorList.Value[Random.Range(0, allPossibleColorList.Value.Count)];
        }

        private void ReEvaluateColor()
        {
        }
    }
}
