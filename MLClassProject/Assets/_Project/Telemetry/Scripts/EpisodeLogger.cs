using UnityEngine;
using BossFight.Core;
using BossFight.Arena;
using System;
using BossFight.Combat;
using System.IO;
using BossFight.Player;

namespace BossFight.telemetryLogger
{
    public class EpisodeLogger : MonoBehaviour
    {
        [Tooltip("FightManager of the Arena (first arena's if multiple are in parallel)")]
        [SerializeField] private FightManager m_fightManager;  

        [Tooltip("The number of arenas in the scene running in parallel")]
        [SerializeField] private int parallelArenas = 1;

        [Tooltip("Name of the .csv log file to be produced")]
        [SerializeField] private string logName = "log";

        [Tooltip("Produce a log every x rounds")]
        [SerializeField] private int logFrequency = 10;
        private long roundCounter = 0;

        void OnEnable()
        {
            if (m_fightManager == null)
            {
                Debug.LogError("No fight manager provided");
            }
            string fileName = logName + ".csv";
            string path = Application.persistentDataPath + "/Episode Logs/";
            string filePath = path + fileName;
            if (File.Exists(filePath))
            {
                int copyNum = 1;
                string newName = path + logName + "_";
                while (File.Exists(newName + copyNum + ".csv") && copyNum < 100)
                {
                    Debug.Log(copyNum);
                    copyNum++;
                }
                newName += copyNum + ".csv";
                Debug.Log("File " + logName + " already exists; creating new log with name " + newName);
                logName += "_" + copyNum;
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
        public void LogEpisode(GameObject boss, GameObject player, bool bossWon, float roundDuration) {
            Debug.Log("Player: " + player.name);
            Debug.Log("Boss: " + boss.name);
            // Debug.Log("Round: " + roundCounter + ", " + roundCounter % logFrequency);
            if (roundCounter % logFrequency != 0)
            {
                roundCounter++;
                return;
            }
            PlayerInfo playerInfo = player.GetComponent<PlayerInfo>();
            Health bossHealth = boss?.GetComponent<Health>();
            float damageTaken = bossHealth.Max - bossHealth.Current;
            float damageDealt = playerInfo.GetHealthMax() - playerInfo.GetHealth();
            

            long iterNum = 1 + roundCounter * parallelArenas;
            string playerName = player.GetComponent<PlayerInfo>().GetBehaviorSource();
            
            string path = Application.persistentDataPath + "/Episode Logs/";
            string fileName = logName + ".csv";
            string filePath = path + fileName;
            string newLogDetails = String.Format("{0},{1},{2},{3},{4},{5}\n", iterNum, bossWon, roundDuration, damageDealt, damageTaken, playerName);
            if (!File.Exists(filePath))
            {
                // Format of CSV
                string logHeader = "Iteration Num,Boss Won,Fight Duration (s),Damage Dealt,Damage Taken,Player Fighter\n";
                File.WriteAllText(filePath, logHeader);
                Debug.Log("File Created: " + filePath);
            }
            File.AppendAllText(filePath, newLogDetails);
            Debug.Log("Data appended to csv:\n" + newLogDetails);
            roundCounter++;
        }
    }


}
