using UnityEngine;
using BossFight.Core;
using BossFight.Combat;
using System;

namespace BossFight.Arena
{
    public class FightManager : MonoBehaviour
    {
        
        // This manager manages the arena fight. 

        // Sets the Fighers of the arena
        [Header("Fighters")]
        [SerializeField] private GameObject fighter1;
        [SerializeField] private GameObject fighter2;

        // Sets the spawnpoints of the fighters
        [Header("Spawn Points")]
        [SerializeField] private Transform spawnPoint1;
        [SerializeField] private Transform spawnPoint2;

        // Time limit of each round
        [SerializeField] private float roundTime = 60f;
        // Time delay between each round
        [SerializeField] private float resetDelay = 2f;

        // The state of is a fight going on now
        private bool isFightActive = false;

        // The remaining time of the fight
        private float timeRemaining = 60f;

        // Fight ended event 
        // Sends the winner
        public static event Action<GameObject> OnFightEnded;

        public bool GetIsFightActive()
        {
            return isFightActive;
        }
        private void SetIsFightActive(bool value)
        {
            isFightActive = value;
        }

        public float GetTimeRemaining()
        {
            return timeRemaining;
        }
        private void SetTimeRemaining(float value)
        {
            timeRemaining = value;
        }


        void OnEnable()
        {
            // Subcribe for the death event to call HandleDeath
            FightEvents.OnDeath += HandleDeath;
        }
        void onDisable()
        {
            // Unsubcribe from the death event 
            FightEvents.OnDeath -= HandleDeath;
        }


        void Start()
        {
            StartNewFight();
        }

        void Update()
        {
            // Return if no fight is happening
            if (!isFightActive) return;

            // Calculate the remaining time
            timeRemaining -= Time.deltaTime;

            // Time out if run out of time
            if (timeRemaining <= 0)
            {
                TimeOut();
            }

        }


        void StartNewFight()
        {
            // Reset positions
            fighter1.transform.position = spawnPoint1.position;
            fighter1.transform.rotation = spawnPoint1.rotation;
            fighter2.transform.position = spawnPoint2.position;
            fighter2.transform.rotation = spawnPoint2.rotation;

            // Reset Health
            ResetHealth(fighter1);
            ResetHealth(fighter2);

            // Start round
            timeRemaining = roundTime;
            isFightActive = true;
            Debug.Log("Arena Fight Start!");
        }

        // When someone dies
        void HandleDeath(GameObject loser)
        {
            if(!isFightActive) return;

            isFightActive = false;

            // Determine winner
            GameObject winner = (loser == fighter1) ? fighter2 : fighter1;
            Debug.Log($"{winner.name} won! {loser.name} lost!");

            // Trigger fight end event
            OnFightEnded?.Invoke(winner);

            // Auto restart after delay
            Invoke(nameof(StartNewFight), resetDelay);

        }

        // Reset health
        void ResetHealth(GameObject fighter)
        {
            Health health = fighter.GetComponent<Health>();
            if (health != null)
            {
                health.ResetToFull();
                Debug.Log($"{fighter.name} health resets to {health.Current}.");
            }
            else
            {
                Debug.Log($"{fighter.name} has no Health component.");
            }
        }

        // Time out
        void TimeOut()
        {
            isFightActive = false;
            Debug.Log("Times Up! Draw!");

            // Trigger fight end event
            OnFightEnded?.Invoke(null);

            // Auto restart after delay
            Invoke(nameof(StartNewFight), resetDelay);

        }
    }
}
