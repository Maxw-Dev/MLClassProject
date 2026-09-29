using UnityEngine;
using BossFight.Player;
using BossFight.Core;
using BossFight.Combat;
using System;

namespace BossFight.Telemetry
{   
public class PlayerInfo : MonoBehaviour
{
        [SerializeField] private String m_behaviorSource = "Unspecified";

        private Health m_health;
        private PlayerBody m_playerBody;
        private CharacterController m_controller;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            m_health = GetComponent<Health>();
            m_playerBody = GetComponent<PlayerBody>();
            m_controller = GetComponent<CharacterController>();

            // check if we were on correct object
            if (m_playerBody == null) {
                Debug.LogError("Player info unable to find required information.");
            }
        }

        /// <summary>
        /// Current health of the player
        /// </summary>
        /// <returns>Current health as a float</returns>
        public float GetHealth()
        {
            return m_health.Current;
        }

        /// <summary>
        /// Current health of the player as a percentage of max health
        /// </summary>
        /// <returns>Current health as a percentage of max health</returns>
        public float GetPercentageHealth()
        {
            return m_health.Current / m_health.Max;
        }

        /// <summary>
        /// Returns the position of the player in world space.
        /// </summary>
        /// <returns>global position Vector3</returns>
        public Vector3 GetPosition() {
            return transform.position;
        }

        /// <summary>
        /// returns the current velocity of the player
        /// </summary>
        /// <returns>velocity as a vector3</returns>
        public Vector3 GetVelocity() {
            return m_controller.velocity;
        }


        // forwarding PlayerBody getters
        public Vector3 GetDirectionalInput()
        {
            return m_playerBody.GetDirectionalInput();
        }

        public Vector3 GetFacingDirection()
        {
            return m_playerBody.GetFacingDirection();
        }

        public PlayerBody.Action GetPlayerAction()
        {
            return m_playerBody.GetCurrentAction();
        }

        /// <summary>
        /// a string that can be set in editor
        /// this is stand in for a model version or something of the like
        /// Defaults to "Unspecified" if not set manually
        /// </summary>
        /// <returns>string identifier</returns>
        public String GetBehaviorSource()
        {
            return m_behaviorSource;
        }

    }
}