using BossFight.Boss;
using BossFight.Combat;
using BossFight.Core;
using UnityEngine;

//Author: Andre Mata Assis

namespace BossFight.Player.Bots
{
    /// <summary>
    /// This PlayerBot acts the same as DefaultPlayerBot, with the only difference being the IfBossAttacking behavior:
    /// AggressivePlayerBot will always dodge towards the boss, but at a 45 degree angle. He is always charging towards the boss,
    /// with the only exception being when the boss quick attacks or AOE, which causes the bot to dodge away.
    /// This leaves the bot susceptible to well-timed AOE moves and quick attacks, and even projectiles because he's always too close to dodge.
    /// With that said, if the Boss doesn't play correctly, the bot quickly wins due to his aggressive playstyle.
    /// </summary>
    public class AggressivePlayerBot : DefaultPlayerBot
    {
        protected override void IfBossAttacking(BossMoveData boss_attack_data, AttackPhase boss_attack_phase)
        {
            //Get all relevant boss data
            Vector3 toBoss = (boss_transform.position - transform.position).normalized;
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
                //Dodge away on quick attacks and AOE
                case BossMove.QuickAttack:
                    if (boss_attack_phase == AttackPhase.Windup)
                    {
                        m_queuedRoll = true;
                        m_queuedMove = toBoss * -1;
                    }
                    else m_queuedMove = toBoss;
                        break;
                case BossMove.AoeBurst:
                    if (boss_attack_phase == AttackPhase.Windup)
                    {
                        m_queuedRoll = true;
                        m_queuedMove = toBoss * -1;
                    }
                    else m_queuedMove = toBoss;
                        m_queuedRoll = true;
                    m_queuedMove = toBoss * -1;
                    break;
                //Otherwise, always dodge toward the boss
                default:
                    m_queuedRoll = true;
                    m_queuedMove = Quaternion.AngleAxis(45, Vector3.up) * toBoss;
                    break;
            }
        }
    }
}
