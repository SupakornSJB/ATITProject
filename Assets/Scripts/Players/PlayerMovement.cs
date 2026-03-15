using Unity.Netcode;
using UnityEngine;

namespace Players
{
    public class PlayerMovement : NetworkBehaviour 
    {
        private CharacterController m_CharacterController; 
        private float movementSpeed = 10f;
    
        void Awake()
        {
            m_CharacterController = GetComponent<CharacterController>();
        }

        void Update()
        {
            if (!IsOwner) return;
            m_CharacterController.SimpleMove(transform.forward * (movementSpeed * Time.deltaTime));
        }
    }
}
