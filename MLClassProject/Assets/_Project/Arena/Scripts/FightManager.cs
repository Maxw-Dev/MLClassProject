using System;
using System.Collections;
using BossFight.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace BossFight.Arena
{
    /// <summary>
    /// Runs one arena's fight loop: puts both fighters on their spawn points at full health, runs the round clock,
    /// calls the result (a death, or a draw when time runs out), raises <see cref="FightEnded"/>, then restarts
    /// after a delay. One per arena, listening only to its own two fighters, so many arenas can run side by side
    /// for training. The clock and the restart delay tick in FixedUpdate, so rounds are the same length at any
    /// time scale. Every UI reference is optional: leave them empty on training arenas.
    /// </summary>
    public class FightManager : MonoBehaviour
    {
        [Header("Fighters")]
        [SerializeField] private GameObject fighter1;
        [SerializeField] private GameObject fighter2;

        [Header("Spawn Points")]
        [SerializeField] private Transform spawnPoint1;
        [SerializeField] private Transform spawnPoint2;

        [Header("Round Settings")]
        [Tooltip("Seconds a round lasts before it is called a draw.")]
        [SerializeField, Min(1f)] private float roundTime = 60f;
        [Tooltip("Seconds between a result and the next round.")]
        [SerializeField, Min(0f)] private float resetDelay = 5f;
        [Tooltip("Start the next round by itself. Turn off when something else, like the agent, calls StartNewFight.")]
        [SerializeField] private bool autoRestart = true;

        [Header("UI (optional)")]
        [SerializeField] private Text fightStartText;
        [SerializeField] private float fightStartTextDuration = 1.5f;
        [SerializeField] private Text winText;
        [SerializeField] private float winTextDuration = 2f;
        [SerializeField] private Text timesUpText;
        [SerializeField] private float timesUpTextDuration = 2f;
        [SerializeField] private Text timerText;
        [SerializeField] private Text roundText;
        [SerializeField] private Text score1Text;
        [SerializeField] private Text score2Text;

        public bool IsFightActive { get; private set; }
        public float TimeRemaining { get; private set; }
        public int CurrentRound { get; private set; }
        public int Score1 { get; private set; }
        public int Score2 { get; private set; }
        public bool AutoRestart { get => autoRestart; set => autoRestart = value; }

        /// <summary>The round is over. The winner, or null when time ran out.</summary>
        public event Action<GameObject> FightEnded;

        Health health1, health2;
        bool restartPending;
        float restartIn;    // seconds until the next round once a result is in

        void Awake()
        {
            health1 = RequireHealth(fighter1);
            health2 = RequireHealth(fighter2);
            if (health1 == null || health2 == null) enabled = false;
        }

        Health RequireHealth(GameObject fighter)
        {
            var health = fighter != null ? fighter.GetComponent<Health>() : null;
            if (health == null) Debug.LogError($"{name}: both fighters need a Health component on their root.", this);
            return health;
        }

        void OnEnable()
        {
            if (health1 != null) health1.Died += OnFighter1Died;
            if (health2 != null) health2.Died += OnFighter2Died;
        }

        void OnDisable()
        {
            if (health1 != null) health1.Died -= OnFighter1Died;
            if (health2 != null) health2.Died -= OnFighter2Died;
        }

        void Start()
        {
            Show(fightStartText, false);
            Show(winText, false);
            Show(timesUpText, false);
            UpdateScoreBoard();
            StartNewFight();
        }

        void FixedUpdate()
        {
            if (IsFightActive)
            {
                TimeRemaining -= Time.fixedDeltaTime;
                if (TimeRemaining <= 0f) EndRound(null);
            }
            else if (restartPending)
            {
                restartIn -= Time.fixedDeltaTime;
                if (restartIn <= 0f) StartNewFight();
            }
        }

        void Update()
        {
            if (IsFightActive) UpdateTimerText();
        }

        /// <summary>
        /// Resets both fighters and starts a round. Safe to call at any time: a round in progress is dropped without
        /// a result. The arena calls this by itself when Auto Restart is on; the agent can call it to reset an episode.
        /// </summary>
        public void StartNewFight()
        {
            restartPending = false;
            if (fighter1 == null || fighter2 == null)
            {
                Debug.LogWarning($"{name}: a fighter was destroyed, so the arena cannot start another round.", this);
                enabled = false;
                return;
            }

            ResetFighter(fighter1, spawnPoint1);
            ResetFighter(fighter2, spawnPoint2);

            CurrentRound++;
            TimeRemaining = roundTime;
            IsFightActive = true;

            SetText(roundText, $"ROUND {CurrentRound}");
            StartCoroutine(ShowBanner(fightStartText, fightStartTextDuration));
        }

        // Back on the spawn point at full health and stamina, with any swing in progress cut short.
        // This is the episode reset recipe from the Combat README.
        static void ResetFighter(GameObject fighter, Transform spawn)
        {
            fighter.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            if (fighter.TryGetComponent(out AttackRunner runner)) runner.Interrupt();
            if (fighter.TryGetComponent(out Health health)) health.ResetToFull();
            if (fighter.TryGetComponent(out Stamina stamina)) stamina.ResetToFull();
        }

        void OnFighter1Died() => EndRound(fighter2);
        void OnFighter2Died() => EndRound(fighter1);

        // Called with the winner, or null when time ran out.
        void EndRound(GameObject winner)
        {
            if (!IsFightActive) return;
            IsFightActive = false;

            if (winner == fighter1) Score1++;
            else if (winner == fighter2) Score2++;
            UpdateScoreBoard();

            SetText(timerText, "");
            if (winner != null)
            {
                SetText(winText, $"{winner.name} WINS!");
                StartCoroutine(ShowBanner(winText, winTextDuration));
            }
            else
            {
                StartCoroutine(ShowBanner(timesUpText, timesUpTextDuration));
            }

            // Arm the restart before telling listeners, so a listener that calls StartNewFight itself cancels it.
            restartPending = autoRestart;
            restartIn = resetDelay;
            FightEnded?.Invoke(winner);
        }

        // UI helpers. Every text is optional.

        IEnumerator ShowBanner(Text text, float seconds)
        {
            if (text == null) yield break;
            text.gameObject.SetActive(true);
            yield return new WaitForSeconds(seconds);
            text.gameObject.SetActive(false);
        }

        void UpdateTimerText()
        {
            if (timerText == null) return;
            int minutes = Mathf.FloorToInt(TimeRemaining / 60f);
            int seconds = Mathf.FloorToInt(TimeRemaining % 60f);
            timerText.text = $"{minutes:D2}:{seconds:D2}";
        }

        void UpdateScoreBoard()
        {
            SetText(score1Text, $"{fighter1.name} - {Score1}");
            SetText(score2Text, $"{fighter2.name} - {Score2}");
        }

        static void Show(Text text, bool on)
        {
            if (text != null) text.gameObject.SetActive(on);
        }

        static void SetText(Text text, string value)
        {
            if (text != null) text.text = value;
        }
    }
}
