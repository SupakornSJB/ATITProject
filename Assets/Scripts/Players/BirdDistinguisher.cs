using Managers;
using Unity.Netcode;
using UnityEngine;

namespace Players
{
    public class BirdDistinguisher : NetworkBehaviour
    {
        [SerializeField] private MeshRenderer _meshRenderer;

        private NetworkVariable<Color> _color = new();

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _color.OnValueChanged += OnColorChanged;
            AssignColor();
        }

        public override void OnNetworkDespawn()
        {
            _color.OnValueChanged -= OnColorChanged;
            base.OnNetworkDespawn();
        }

        private void AssignColor()
        {
            // Color is assigned externally by BirdSpawnManager via PlayerBird.PlayerColor
        }

        private void OnColorChanged(Color _, Color newColor)
        {
            _meshRenderer.material.color = newColor;
        }
    }
}