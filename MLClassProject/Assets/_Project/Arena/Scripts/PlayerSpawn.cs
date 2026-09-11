
using UnityEngine;

namespace BossFight.Arena
{
    public class PlayerSpawn : MonoBehaviour
    {
        // This script is to spawn the player in the arena. 

        // Store the spawn point
        [SerializeField] private Transform respawnPoint;

        // Get the RigidBody component of the player
        private Rigidbody rb;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
            Respawn();
        }

        void Update()
        {
            
        }

        void Respawn()
        {
            // Reset player position
            transform.position = respawnPoint.position;
            transform.rotation = respawnPoint.rotation;

        }
    }
}
