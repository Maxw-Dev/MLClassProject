using UnityEngine;
using BossFight.Core;
using BossFight.Combat;
using System;
using System.Collections;
// TextMeshPro import does not work for some reason
using UnityEngine.UI;

namespace BossFight.Arena
{
    public class FightManager : MonoBehaviour
    {
        
        // This manager manages the arena fight. 

        // Sets the Fighters of the arena
        [Header("Fighters")]
        [SerializeField] private GameObject fighter1;
        [SerializeField] private GameObject fighter2;

        // Sets the spawnpoints of the fighters
        [Header("Spawn Points")]
        [SerializeField] private Transform spawnPoint1;
        [SerializeField] private Transform spawnPoint2;

        // UI
        [Header("Fight Start UI")]
        [SerializeField] private Text fightStartText;
        [SerializeField] private float fightStartTextDuration = 1.5f;

        [Header("Win Annoucement UI")]
        [SerializeField] private Text winText;
        [SerializeField] private float winTextDuration = 2f;

        [Header("Timer UI")]
        [SerializeField] private Text timerText;

         [Header("Tie Annoucement UI")]
        [SerializeField] private Text timesUpText;
        [SerializeField] private float timesUpTextDuration = 2f;
        
        [Header("Round Number UI")]
        [SerializeField] private Text roundText;

        // Score Board
        [Header("Scoreboard")]
        [SerializeField] private Text score1Text;
        [SerializeField] private Text score2Text;

        [Header("Round Setting")]
        // Time limit of each round
        [SerializeField] public float roundTime = 60f;
        // Time delay between each round
        [SerializeField] public float resetDelay = 5f;


        // The state of is there a fight going on now
        public bool isFightActive = false;

        // The remaining time of the fight
        public float timeRemaining = 60f;

        // Current round 
        public int currentRound = 0;

        // Scores
        public int score1 = 0;
        public int score2 = 0;

        // Fight ended event 
        // Sends the winner
        public static event Action<GameObject> OnFightEnded;


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
            // Initialize UI texts
            fightStartText.gameObject.SetActive(false);
            winText.gameObject.SetActive(false);
            timesUpText.gameObject.SetActive(false);
            UpdateScoreBoard();
            
            // Start a new fight
            StartNewFight();
        }

        void Update()
        {
            // Return if no fight is happening
            if (!isFightActive) return;

            // Countdown
            timeRemaining -= Time.deltaTime;

            // Update Timer text
            UpdateTimerText();

            // Time out if run out of time
            if (timeRemaining <= 0)
            {
                TimeOut();
            }
        }

        void StartNewFight()
        {
            // Reset fighter positions
            fighter1.transform.position = spawnPoint1.position;
            fighter1.transform.rotation = spawnPoint1.rotation;
            fighter2.transform.position = spawnPoint2.position;
            fighter2.transform.rotation = spawnPoint2.rotation;

            // Reset Health
            ResetHealth(fighter1);
            ResetHealth(fighter2);

            // Display Starting Texts
            StartCoroutine(ShowFightStartText());

            // Start round
            currentRound++;
            UpdateRoundText();
            timeRemaining = roundTime;
            isFightActive = true;
            Debug.Log("Arena Fight Start!");
        }


        // When Someone wins 
        void HandleDeath(GameObject loser)
        {
            if(!isFightActive) return;

            isFightActive = false;

            // Determine winner
            GameObject winner = (loser == fighter1) ? fighter2 : fighter1;
            Debug.Log($"{winner.name} won! {loser.name} lost!");
            UpdateScore(winner);

            // Trigger fight end event
            OnFightEnded?.Invoke(winner);

            // Clear timer
            timerText.text = "";

            // Show winner text
            StartCoroutine(ShowWinText(winner.name));

            // Auto restart after delay
            Invoke(nameof(StartNewFight), resetDelay);

        }

        // Update score after someone wins 
        void UpdateScore(GameObject winner)
        {
            if (winner == fighter1)
                score1++;
            else
                score2++;

            UpdateScoreBoard();
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

            // Clear timer
            timerText.text = "";

            // Show winner text
            StartCoroutine(ShowTimesUp());

            // Auto restart after delay
            Invoke(nameof(StartNewFight), resetDelay);

        }



        // Text Updates    

        IEnumerator ShowTimesUp()
        {
            timesUpText.gameObject.SetActive(true);
            yield return new WaitForSeconds(timesUpTextDuration);
            timesUpText.gameObject.SetActive(false);
        }

        void UpdateTimerText()
        {
            // Convert total seconds to minutes and seconds
            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(timeRemaining % 60f);

            // Dispplay timer
            timerText.text = $"{minutes:D2}:{seconds:D2}";
        }

        IEnumerator ShowWinText(string winnerName)
        {
            winText.text = $"{winnerName} WINS!";
            winText.gameObject.SetActive(true);
            yield return new WaitForSeconds(winTextDuration);
            winText.gameObject.SetActive(false);
        }

        IEnumerator ShowFightStartText()
        {
            fightStartText.gameObject.SetActive(true);
            yield return new WaitForSeconds(fightStartTextDuration);
            fightStartText.gameObject.SetActive(false);
        }

        void UpdateRoundText()
        {
            roundText.text = $"ROUND {currentRound}";
        }

        void UpdateScoreBoard()
        {
            score1Text.text = $"{fighter1.name} - {score1.ToString()}";
            score2Text.text = $"{fighter2.name} - {score2.ToString()}";
        }


    }

    
}
