using BossFight.Boss;
using BossFight.Combat;
using BossFight.Core;
using UnityEngine;

//Author: Andre Mata Assis

namespace BossFight.Player.Bots
{
    /// <summary>
    /// Moves in a circle around the Boss, and tries to maintain distance (m_comfortDistance). Shoots raycasts to avoid walls,
    /// tries to get around the boss when its cornered (m_corneredDistance). 
    /// Will only attack when there is a chance to stun, or while Boss stunned. This bot's priority is to survive.
    /// </summary>
    public class CowardlyPlayerBot : DefaultPlayerBot
    {
        //Current direction of rotating around the boss (-90 or 90)
        protected float m_rotateDir = -90;
        
        //If the boss is closer than this, the bot begins running away
        protected float m_comfortDistance = 8f;

        //If the boss is closer than this, the bot thinks its cornered and starts dodging towards the boss
        protected float m_corneredDistance = 1f;
        protected override void IfBossIdle()
        {
            //Get all relevant boss data
            Vector3 toBoss = GetVectorToBoss();
            toBoss.y = 0f;
            float distanceToBoss = GetDistanceToBoss();

            bool movingTowardsWall = ShootRaycast();
            float rotateMod = 0f;

            //Towards boss if cornered
            if (distanceToBoss < m_corneredDistance)
            {
                m_queuedRoll = true;
                m_queuedMove = Quaternion.AngleAxis(25, Vector3.up) * toBoss;
            }
            //Away from boss if too close
            else if (distanceToBoss < m_comfortDistance)
            {
                if (movingTowardsWall)
                {
                    m_queuedRoll = true;
                    m_queuedMove = Quaternion.AngleAxis(m_rotateDir, Vector3.up) * toBoss;
                    return;
                }
                m_queuedRoll = true;
                m_queuedMove = -1 * toBoss;
            }
            //Default: rotates and avoids walls
            else
            {
                if (movingTowardsWall) { m_rotateDir *= -1; rotateMod = m_rotateDir; }
                m_queuedMove = Quaternion.AngleAxis(m_rotateDir + rotateMod, Vector3.up) * toBoss;
            }
        }
        protected override void IfBossAttacking(BossMoveData boss_attack_data, AttackPhase boss_attack_phase)
        {
            //Get all relevant boss data
            Vector3 toBoss = GetVectorToBoss();
            toBoss.y = 0f;

            switch (boss_attack_data.Move)
            {
                //Try to punish super attacks
                case BossMove.SuperAttack:
                    if (boss_attack_phase == AttackPhase.Windup)
                    {
                        RunAtAndAttack();
                    }
                    else
                    {
                        m_queuedMove = -1 * toBoss;
                    }
                    break;
                //Otherwise, always dodge away strategically
                default:
                    m_queuedRoll = true;
                    m_queuedMove = Quaternion.AngleAxis(m_rotateDir, Vector3.up) * toBoss;
                    break;
            }
        }
    }
}
