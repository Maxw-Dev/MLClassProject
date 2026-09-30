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
        public void LogEpisode(GameObject player, GameObject boss, bool bossWon, float roundDuration) {
            Debug.Log(player.name);
            PlayerInfo playerInfo = player.GetComponent<PlayerInfo>();
            Debug.Log(playerInfo);
            Health bossHealth = boss?.GetComponent<Health>();
            float damageTaken = bossHealth.Max - bossHealth.Current;
            float damageDealt = playerInfo.GetHealthMax() - playerInfo.GetHealth();
            
            int policyVer = 0; // Temp; needs to be provided from  
            string playerName = player.GetComponent<PlayerInfo>().GetBehaviorSource();
            
            string path = Application.persistentDataPath + "/Episode Logs/";
            string fileName = "log.csv";
            string filePath = path + fileName;
            string newLogDetails = String.Format("{0},{1},{2},{3},{4},{5}\n", policyVer, bossWon, roundDuration, damageDealt, damageTaken, playerName);

            if (!File.Exists(filePath))
            {
                // Format of CSV
                string logHeader = "Policy Version,Boss Won,Fight Duration (s),Damage Dealt,Damage Taken,Player Fighter\n";
                File.WriteAllText(filePath, logHeader);
                Debug.Log("File Created: " + filePath);
            }
            File.AppendAllText(filePath, newLogDetails);
            Debug.Log("Data appended to csv:\n" + newLogDetails);
        }
    }


}
