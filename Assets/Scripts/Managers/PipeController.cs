using Unity.Netcode;
using UnityEngine;

namespace Managers
{
    public class PipeController : NetworkBehaviour
    {
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private float _despawnX = -12f;

        private void Update()
        {
            transform.position += Vector3.left * (_moveSpeed * Time.deltaTime);

            if (IsServer && transform.position.x < _despawnX)
                NetworkObject.Despawn();
        }
    }
}