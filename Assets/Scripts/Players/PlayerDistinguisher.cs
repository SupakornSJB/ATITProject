using Managers;
using Unity.Netcode;
using UnityEngine;

namespace Players
{
    public class PlayerDistinguisher : NetworkBehaviour
    {
        private PlayerDistinguisherManager _playerDistinguisherManager; 
        private NetworkVariable<Color> _color = new ();
        [SerializeField] private MeshRenderer _meshRenderer;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _color.OnValueChanged += SetColor;
            
            // Server only logic here
            GetColorForPlayer();
        }

        private void GetColorForPlayer()
        {
            if (!IsOwner || IsClient) return;
            _color.Value = PlayerDistinguisherManager.Instance.RequestColor();
        }
        
        private void SetColor(Color _, Color newColor)
        {
            _meshRenderer.material.color = newColor;
        }
    }
}
