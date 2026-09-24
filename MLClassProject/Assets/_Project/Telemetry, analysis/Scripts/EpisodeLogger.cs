using UnityEngine;
using BossFight.Core;
using BossFight.Arena;
using System;


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
            


            string path = Application.persistentDataPath + "/episodes/" + "log.json";
        }

        static void LogDeath(GameObject victim) => Debug.Log($"[Fight] {victim.name} died");
    }


}
