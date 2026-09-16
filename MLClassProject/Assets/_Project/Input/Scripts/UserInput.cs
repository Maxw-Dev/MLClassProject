using BossFight.Core;
using UnityEngine;
using UnityEngine.InputSystem;

// Author: James Prendergast

namespace BossFight.Input
{
    [RequireComponent(typeof(PlayerInput))]

    /// <summary>
    /// Reads input from the user and formats it into an Intent to be sent to a body
    /// Also resolve camera guidance through Cinemachine
    /// Input:
    ///     Move - vector2
    ///     Roll - bool, tap only
    ///     LightAttack - bool
    ///     HeavyAttack - bool
    ///     Debug - bits, 4 inputs
    ///         debug4 - tap only
    /// </summary>

    public class UserInput : MonoBehaviour, IIntentSource
    {

        #region Queued Input

        private Vector3 m_queuedMove = Vector3.zero;
        private bool m_queuedRoll = false;
        private bool m_queuedLightAttack = false;
        private bool m_queuedHeavyAttack = false;
        private int m_queuedDebug = 0; // debug 4 is tap only

        private Intent m_intent;

        #endregion

        /// <summary>
        /// Reads and caches the inputed movement value
        /// contextualizes raw input into world space
        /// </summary>
        private void LateUpdate() {
            // compiles intent
            m_intent = new Intent {
                Move = m_queuedMove,
                Roll = m_queuedRoll,
                LightAttack = m_queuedLightAttack,
                HeavyAttack = m_queuedHeavyAttack,
                Debug = m_queuedDebug
            };

            // Assure tap only inputs
            m_queuedRoll = false;
            m_queuedDebug &= ~8; // debug4 is tap only
        }

        /// <summary>
        /// Interface override
        /// Returns the current stored intent
        /// At least one frame behind
        /// </summary>
        /// <returns></returns>
        public Intent GetIntent() {
            return m_intent;
        }

        #region Input Patching

        /// <summary>
        /// Automatic unity event patching
        /// </summary>
        private void OnEnable() {
            PlayerInput input = GetComponent<PlayerInput>();

            // Move
            input.actions["Move"].performed += OnMove;
            input.actions["Move"].canceled += OnMove;
            // Roll
            input.actions["Roll"].performed += OnRoll;
            input.actions["Roll"].canceled += OnRoll;
            // LightAttack
            input.actions["LightAttack"].performed += OnLightAttack;
            input.actions["LightAttack"].canceled += OnLightAttack;
            // HeavyAttack
            input.actions["HeavyAttack"].performed += OnHeavyAttack;
            input.actions["HeavyAttack"].canceled += OnHeavyAttack;

            // Debug
            input.actions["Debug1"].performed += OnDebug1;
            input.actions["Debug2"].performed += OnDebug2;  
            input.actions["Debug3"].performed += OnDebug3;
            input.actions["Debug4"].performed += OnDebug4;
            input.actions["Debug1"].canceled += OnDebug1;
            input.actions["Debug2"].canceled += OnDebug2;  
            input.actions["Debug3"].canceled += OnDebug3;
            input.actions["Debug4"].canceled += OnDebug4;
        }

        /// <summary>
        /// Removes event tie ins on disable to prevent memory leaks
        /// </summary>
        void OnDisable() {
            PlayerInput input = GetComponent<PlayerInput>();

            // Move
            input.actions["Move"].performed -= OnMove;
            input.actions["Move"].canceled -= OnMove;
            // Roll
            input.actions["Roll"].performed -= OnRoll;
            input.actions["Roll"].canceled -= OnRoll;
            // LightAttack
            input.actions["LightAttack"].performed -= OnLightAttack;
            input.actions["LightAttack"].canceled -= OnLightAttack;
            // HeavyAttack
            input.actions["HeavyAttack"].performed -= OnHeavyAttack;
            input.actions["HeavyAttack"].canceled -= OnHeavyAttack;

            // Debug
            input.actions["Debug1"].performed -= OnDebug1;
            input.actions["Debug2"].performed -= OnDebug2;  
            input.actions["Debug3"].performed -= OnDebug3;
            input.actions["Debug4"].performed -= OnDebug4;
            input.actions["Debug1"].canceled -= OnDebug1;
            input.actions["Debug2"].canceled -= OnDebug2;  
            input.actions["Debug3"].canceled -= OnDebug3;
            input.actions["Debug4"].canceled -= OnDebug4;
        }

        #endregion

        #region Input Reading

        /// <summary>
        /// Reads and caches the inputed movement value
        /// contextualizes raw input into world space
        /// </summary>
        public void OnMove(InputAction.CallbackContext context) {
            Vector2 inputVector = context.ReadValue<Vector2>();

            // queue input relative to camera orientation on XZ plane
            Vector3 facing = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(Camera.main.transform.right, Vector3.up).normalized;
            m_queuedMove = facing * inputVector.y + right * inputVector.x;
        }

        /// <summary>
        /// queues a roll input for the next frame
        /// </summary>
        public void OnRoll(InputAction.CallbackContext context) {
            m_queuedRoll = context.performed;
        }

        /// <summary>
        /// queues a light attack input for the next frame
        /// </summary>
        public void OnLightAttack(InputAction.CallbackContext context) {
            m_queuedLightAttack = context.performed;
        }

        /// <summary>
        /// queues a heavy attack input for the next frame
        /// </summary>
        public void OnHeavyAttack(InputAction.CallbackContext context) {
            m_queuedHeavyAttack = context.performed;
        }

        #region Debug Input

        /// <summary>
        /// Helper function used by the following debug input functions
        /// toggles the specified bit to relate to the pressed parameter
        /// </summary>
        /// <param name="pressed">if the bit should be toggled on</param>
        /// <param name="bit">which bit(s) is being affected</param>
        private void UpdateDebug(bool pressed, int bit) {
            if (pressed)
                m_queuedDebug |= bit;
            else
                m_queuedDebug &= ~bit;
        }

        public void OnDebug1(InputAction.CallbackContext context) {
            UpdateDebug(context.performed, 1);
        }
        public void OnDebug2(InputAction.CallbackContext context) {
            UpdateDebug(context.performed, 2);
        }
        public void OnDebug3(InputAction.CallbackContext context) {
            UpdateDebug(context.performed, 4);
        }
        public void OnDebug4(InputAction.CallbackContext context) {
            UpdateDebug(context.performed, 8);
        }

        #endregion

        #endregion
    }
}
