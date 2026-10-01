
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossFight.Arena
{
    public class CameraFollow : MonoBehaviour
    {
        /// <summary>
        /// Runs a 3D camera that follows the player.
        /// Attach the script to the camera of the scene
        /// </summary>
        
        [Header("Target")]
        [Tooltip("The target of the camera; it's usually the player.")]
        [SerializeField] private Transform target;

        [Header("Distance")]
        [Tooltip("The distance of the camera from the target.")]
        [SerializeField] private float distance = 8f;

        [Header("Rotation")]
        [SerializeField] private float mouseSensitivity = 3f;
        [Tooltip("The speed at which the camera catches up to the taget.")]
        [SerializeField] private float smoothSpeed = 5f;
        
        [Header("Vertical Limits")]
        [Tooltip("How far down the camera can look.")]
        [SerializeField] private float minY = -40f;
        [Tooltip("How far up the camera can look.")]
        [SerializeField] private float maxY = 20f;

        private float currentX = 0f;    // Angle of Horizontal rotation 
        private float currentY = 0f;    // Angle of Vertical rotation

        void Start()
        {
            Vector3 angles = transform.eulerAngles;
            currentX = angles.y;        // Rotate around the Y axis to look left/right
            currentY = angles.x;        // Rotate around the X axis to look up/down

            // Hide the cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }


        void LateUpdate()
        {
            // Adjust the camera based on mouse movement
            float mouseX = Mouse.current.delta.x.ReadValue() * Time.deltaTime;
            float mouseY = Mouse.current.delta.y.ReadValue() * Time.deltaTime;
            currentX += mouseX * mouseSensitivity;
            currentY -= mouseY * mouseSensitivity;

            // Locks the vertical angle to prevent upside down camera
            currentY = Mathf.Clamp(currentY, minY, maxY);

            // Rotate the camera
            Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, smoothSpeed * Time.deltaTime);

            // Move the camera  
            Vector3 position = rotation * new Vector3(0, 0, -distance) + target.position;
            transform.position = Vector3.Lerp(transform.position, position, smoothSpeed * Time.deltaTime);
        }
    }
}
