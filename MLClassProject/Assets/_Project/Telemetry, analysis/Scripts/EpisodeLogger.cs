using UnityEngine;
using BossFight.Core;
using BossFight.Arena;
using System;
using BossFight.Combat;


namespace BossFight.telemetryLogger
{
    public class EpisodeLogger : MonoBehaviour
    {
        /// <summary>Arena FightManager for passing fight end details</summary>
        [SerializeField] private FightManager m_fightManager;
        /// <summary></summary>
        void OnEnable()
        {
            if (m_fightManager == null)
            {
                Debug.LogError("No fight manager provided");
            }
            m_fightManager.FightEnded += LogEpisode;
        }

        void OnDisable()
        {
            m_fightManager.FightEnded -= LogEpisode;
        }

        /// <summary>
        /// Called when a fight ends
        /// gathers game result info and writes it into episodes/log.json
        /// one line per fight, logs the following:
        /// policy version, outcome, duration, damage dealt and taken, who was fighting.
        /// </summary>
        /// <param name="winner">The winner of the battle</param>
        public void LogEpisode(GameObject winner) {
            // Temp until LogEpisode returns these values
            GameObject player = winner;
            bool bossWon = false;
            GameObject boss = null;
            float fightDur = 100;
            // EndTemp

            Health bossHealth = boss.GetComponent<Health>();
            Health playerHealth = player.GetComponent<Health>();
            // Also need boss ML Model policy version, player bot (playerbot-[name of bot behavior])
            int policyVer = 0;
            string playerName = "playerbot-TempName";
            // EndTemp
            
            string path = Application.persistentDataPath + "/Episodes Logs/";
            string fileName = "log.csv";
            string filePath = path + fileName;
            string newDetails = "" + policyVer + bossWon + fightDur + playerHealth;
            
            //csv format: "Policy Version", "Boss Won", "Fight Duration (s)", "Damage Dealt", "Damage Taken", "Player Fighter"

        }
    }


}
