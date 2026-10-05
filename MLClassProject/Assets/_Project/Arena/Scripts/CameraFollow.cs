
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossFight.Arena
{
    public class CameraFollow : MonoBehaviour
    {
        /// <summary>
        /// Runs a 3D camera that follows the player.
        /// It locks on the boss at all times. 
        /// Attach the script to the camera of the scene
        /// </summary>
        
        [Header("Target")]
        [Tooltip("The boss the cameras looks at.")]
        [SerializeField] private Transform boss;
        [Tooltip("The player to position behind.")]
        [SerializeField] private Transform player;

        [Header("Positioning")]
        [Tooltip("The distance of the camera from the player.")]
        [SerializeField] private float distance = 6f;
        [Tooltip("Height above player")]
        [SerializeField] private float height = 2f;
        [Tooltip("Minimum distance from boss (prevents clipping).")]
        [SerializeField] private float minDistanceFromBoss = 2f;

        [Header("Smoothing")]
        [Tooltip("How fast camera catches up to player movement.")]
        [SerializeField] private float positionSmoothSpeed = 8f;
        [Tooltip("How fast camera rotates to look at target.")]
        [SerializeField] private float rotationSmoothSpeed = 10f;

        [Header("Vertical Offset")]
        [Tooltip("Look at the boss directly.")]
        [SerializeField] private float bossLookAtHeight = 1.5f;

        void LateUpdate()
        {

            if (boss == null || player == null) return;

            // Calculate direction from boss to player
            Vector3 bossToPlayer = player.position - boss.position;
            bossToPlayer.y = 0;

            // Normalize the direction
            if (bossToPlayer.magnitude < 0.01f)
            {
                bossToPlayer = Vector3.forward; // Default to forward when the player and boss overlap
            }
            else
            {
                bossToPlayer.Normalize();
            }

            // Calculate the desired position of the camera
            Vector3 desiredPosition = player.position + bossToPlayer * distance;
            desiredPosition.y = player.position.y + height;

            // Prevent camera from getting too close to boss
            float distanceToBoss = Vector3.Distance(desiredPosition, boss.position);
            if (distanceToBoss < minDistanceFromBoss)
            {
                Vector3 awayFromBoss = (desiredPosition - boss.position).normalized;
                desiredPosition = boss.position + awayFromBoss * minDistanceFromBoss;
            }

            // Smoothly move the camera to desired position 
            transform.position = Vector3.Lerp(transform.position, desiredPosition, positionSmoothSpeed * Time.deltaTime);

            // Smoothly look at the target
            Vector3 lookTarget = boss.position + Vector3.up * bossLookAtHeight;
            Quaternion targetRotation = Quaternion.LookRotation(lookTarget - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSmoothSpeed * Time.deltaTime);
        }

        // Some methods that can help the player movement script gets updated
        // Forward camera
        public Vector3 GetCameraForward()
        {
            Vector3 forward = transform.forward;
            forward.y = 0;
            return forward.normalized;
        }

        // Right camera
        public Vector3 GetCameraRight()
        {
            Vector3 right = transform.right;
            right.y = 0;
            return right.normalized;
        }

        // Direction from player to boss
        public Vector3 GetDirectionToBoss()
        {
            Vector3 dir = boss.position - player.position;
            dir.y = 0;
            return dir.normalized;
        }

        // Distance from player to boss
        public float GetDistanceToBoss()
        {
            Vector3 diff = boss.position - player.position;
            diff.y = 0;
            return diff.magnitude;
        }

        // Angle from player to boss, relative to world forward 
        public float GetAngleToBoss()
        {
            Vector3 dir = GetDirectionToBoss();
            return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        // Where the camera is looking 
        public Vector3 GetLookTarget()
        {
            return boss.position + Vector3.up * bossLookAtHeight;
        }


    }
}
