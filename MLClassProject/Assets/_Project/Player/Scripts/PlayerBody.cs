using System.Collections;
using System.Collections.Generic;
using BossFight.Combat;
using BossFight.Core;
using UnityEngine;

// Author: James Prendergast

namespace BossFight.Player
{
    public class PlayerBody : MonoBehaviour
    {
        private Animator m_animator;
        private AttackRunner m_runner;
        private Health health;

        #region Player Params

        // amount of time left until action ready, see ActionReady()
        // set when an action is taken depending on its duration
        // also to be set when taking damage or other interventions
        private float m_cooldownTimer = 0f;

        // state based controller
        // Movement is handled independently of the buffer system
        // serves as system state definition
        public enum Action {
            None,
            Roll,
            LightAttack,
            HeavyAttack,
            Dead
        }
        private Action m_currentAction = Action.None;

        /// <summary>
        /// Gets the current action of the player.
        /// </summary>
        /// <returns>The current actio</returns>
        public Action GetCurrentAction() {
            return m_currentAction;
        }

        // Base movement speed walking around
        [SerializeField] private float m_speed = 5f;
        [SerializeField] private float m_rotationSpeed = 720f;
        [SerializeField] private float m_midActionRotationSpeed = 270f;

        [Header("Roll")]
        [SerializeField] private float m_rollDistance = 3f;
        [SerializeField] private float m_rollDuration = 1f;

        [Header("Light Attack")]
        // TODO: add damage or otherwise
        [SerializeField] private float m_lightAttackDuration = .5f;
        [SerializeField] private AttackData m_lightAttack;

        [Header("Heavy Attack")]
        // TODO: add damage or otherwise
        [SerializeField] private float m_heavyAttackDuration = 1.5f;
        [SerializeField] private AttackData m_heavyAttack;

        #endregion

        /// <summary>
        /// Gets references to components
        /// </summary>
        void Start() {
            m_runner = GetComponent<AttackRunner>();
            m_intentSource = GetComponent<IIntentSource>();
            m_animator = GetComponent<Animator>();
        }

        /// <summary>
        /// Calls corresponding handlers
        /// </summary>
        void FixedUpdate() {
            // don't do anything if dead
            if (m_currentAction == Action.Dead) {
                return;
            }

            // reduce cooldown timer
            if (m_cooldownTimer > 0f) {
                m_cooldownTimer -= Time.fixedDeltaTime;
            }

            // Process this frame's intent and resolve buffer
            ProcessIntent();

            // Always rotate towards direction of input
            Quaternion targetRotation = Quaternion.LookRotation(m_lastDirection, Vector3.up);
            float rotationSpeed = ActionReady() ? m_rotationSpeed : m_midActionRotationSpeed;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation,
                (ActionReady() ? m_rotationSpeed : m_midActionRotationSpeed) * Time.fixedDeltaTime);
        }

        #region Input/Intent

        // if input is recieved while action is unavailable
        // it will be stored in a buffer for this many seconds before being discarded
        [Header("Input General")]
        [SerializeField] private float m_inputBuffer = .25f;
        private Vector3 m_movementInput = Vector3.zero, m_lastDirection = Vector3.forward;
        private IIntentSource m_intentSource;

        // Requested input is processed through a buffer
        struct bufferedInput {
            public Action action;
            public float timeOfRequest;
        }
        private List<bufferedInput> m_bufferedInputs = new List<bufferedInput>();

        /// <summary>
        /// Called in update loop
        /// Reads intent into buffered input
        /// enacts upon (or cleans) buffered input if possible
        /// </summary>
        private void ProcessIntent() {
            // read this frame's intent
            Intent intent = m_intentSource.GetIntent();

            // record this frame's movement input and direction
            m_movementInput = intent.Move;
            if (m_movementInput != Vector3.zero)
                m_lastDirection = m_movementInput.normalized;

            // resolves action request in the order of events decided below
            if (intent.Roll)
                BufferInput(Action.Roll);
            if (intent.LightAttack)
                BufferInput(Action.LightAttack);
            if (intent.HeavyAttack)
                BufferInput(Action.HeavyAttack);
        
            // process buffered input
            for (int i = 0; i < m_bufferedInputs.Count; i++) {
                bufferedInput input = m_bufferedInputs[i];

                // discard expired input
                if (Time.time - input.timeOfRequest > m_inputBuffer) {
                    m_bufferedInputs.RemoveAt(i);
                    i--;
                    continue;
                }
                
                // try to resolve valid buffered input
                if (ActionReady()) {
                    // enact valid input
                    switch (input.action) {
                        case Action.Roll:
                            Roll();
                            break;
                        case Action.LightAttack:
                            LightAttack();
                            break;
                        case Action.HeavyAttack:
                            HeavyAttack();
                            break;
                    }
                    // remove this action from the buffer
                    m_bufferedInputs.RemoveAt(i);
                    i--;
                } else {
                    // no need to continue resolving buffered input if no action can be made
                    break;
                }
            }

            // move the player accordingly
            // check if an action has not just been taken
            if (ActionReady()) {
                transform.position += m_movementInput * m_speed * Time.fixedDeltaTime;
            }
        }

        /// <summary>
        /// buffers the desired action at the time of calling
        /// </summary>
        /// <param name="action">what action to buffer</param>
        private void BufferInput(Action action) {
            m_bufferedInputs.Add(new bufferedInput { action = action, timeOfRequest = Time.time });
        }
        
        #endregion

        #region Actions

        /// <summary>
        /// Checks if the player is ready to perform an action
        /// </summary>
        /// <returns>if there is no action cooldown left</returns>
        private bool ActionReady() {
            return m_cooldownTimer <= 0f;
        }
        /// <summary>
        /// Rolls the player in the current direction of movement
        /// initiates an action cooldown
        /// </summary>
        private void Roll() {
            // stop any existing coroutines and start a new one
            StopAllCoroutines();
            StartCoroutine(RollSequence());
        }

        IEnumerator RollSequence() {
            // set action cooldown timer
            m_cooldownTimer = m_rollDuration; // reduced in update loop

            m_animator.SetTrigger("Roll");

            // Trigger invulnerability
            health.GrantInvulnerability(m_rollDuration);

            // move the player accordingly
            while (m_cooldownTimer > 0f) {
                float t = m_cooldownTimer / m_rollDuration; // inverted 
                float distance = m_rollDistance * 2 * t; // integral of this from 0 to 1 is m_rollDistance
                transform.position += m_lastDirection * distance * Time.fixedDeltaTime;
                
                yield return new WaitForFixedUpdate();
            }
        }

        /// <summary>
        /// Conducts a light attack
        /// initiates an action cooldown
        /// </summary>
        private void LightAttack() {
            // try to start the attack. If fails then exit
            if (!m_runner.TryStart(m_lightAttack)) {
                return;
            }

            m_cooldownTimer = m_lightAttackDuration;
            m_animator.SetTrigger("Light");

            Debug.Log("Light Attack Triggered");
        }

        /// <summary>
        /// Conducts a heavy attack
        /// initiates an action cooldown
        /// </summary>
        private void HeavyAttack() {
            // try to start the attack. If fails then exit
            if (!m_runner.TryStart(m_heavyAttack)) {
                return;
            }

            m_cooldownTimer = m_heavyAttackDuration;
            m_animator.SetTrigger("Heavy");
        }

        #endregion
        #region Combat

        void OnEnable() {
            health = GetComponent<Health>();
            health.Damaged += OnDamaged;
            health.Died += OnDeath;
        }

        void OnDisable() {
            health.Damaged -= OnDamaged;
            health.Died -= OnDeath;
        }

        void OnDamaged(DamageInfo info) {
            // stop any existing coroutines and start a new one
            StopAllCoroutines();
            StartCoroutine(DamageSequence());
        }

        IEnumerator DamageSequence() {

            // Temporary damaged effect
            transform.Rotate(Vector3.one * 45f);

            yield return new WaitForSeconds(.5f); // to be used in a more sophisitcaed fashion later

            // Reset rotation automatically

        }

        void OnDeath() {
            // stop any existing coroutines and start a new one
            StopAllCoroutines();
            
            // Temporary death effect
            m_currentAction = Action.Dead;
        }

        /// <summary>
        /// Called to reset player state
        /// Revives and clears lingering input information
        /// </summary>
        public void Reset() {
            m_currentAction = Action.None;
            m_cooldownTimer = 0f;
            m_bufferedInputs.Clear();
        }

        #endregion
    }
}