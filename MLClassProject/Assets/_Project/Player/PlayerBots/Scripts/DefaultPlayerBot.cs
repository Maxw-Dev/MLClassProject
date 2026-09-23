using BossFight.Boss;
using BossFight.Core;
using BossFight.Combat;
using UnityEngine;

//Author: Andre Mata Assis

namespace BossFight.Player.Bots
{
    /// <summary>
    /// A default player bot. New bots should be subclasses of this class, since that'll save you a lot of time. This way, you can override just the behavior you need to change.
    /// Make sure to describe bot behavior when making new bots, feel free to follow this format. In DefaultPlayerBot's case:
    /// If Boss is Idle:
    ///     Run at boss and LightAttack
    /// If Boss is Attacking:
    ///     RangedShot: Run away and dodge perpendicular to bullet trajectory
    ///     SuperAttack: Run at boss and light attack in order to stun
    ///     Default: Run directly away from the boss
    /// If Boss is Stunned:
    ///     Run at boss and HeavyAttack
    /// </summary>
    public class DefaultPlayerBot : MonoBehaviour, IIntentSource
    {
        protected Vector3 m_queuedMove = Vector3.zero;
        protected bool m_queuedRoll = false;
        protected bool m_queuedLightAttack = false;
        protected bool m_queuedHeavyAttack = false;

        protected Intent m_intent;

        [Tooltip("How far the player needs to be before attacking the boss")]
        [SerializeField] protected float m_attack_distance = 2f;

        [Tooltip("A reference to the Boss in the scene, used for making decisions. PlayerBots automatically fill in this field by looking for a Transform with tag 'Boss'")]
        [SerializeField] Transform boss_transform;
        protected BossBody boss_body;

        protected void Start()
        {
            FindBoss();
        }

        //REPLACE POSTHASTE!!!! WILL NOT WORK IF THERE ARE MUTLIPLE BOSSES!!!! not my problem tho :)
        /// <summary>
        /// Called on Start() -- Finds the boss reference in the Scene and stores it for future reference.
        /// </summary>
        protected void FindBoss()
        {
            if (boss_transform != null) return;
            var boss = GameObject.FindWithTag("Boss");
            if (boss != null)
            {
                boss_transform = boss.transform;
                boss_body = boss.GetComponent<BossBody>();
            }
        }

        private void LateUpdate()
        {
            m_intent = new Intent
            {
                Move = m_queuedMove,
                Roll = m_queuedRoll,
                LightAttack = m_queuedLightAttack,
                HeavyAttack = m_queuedHeavyAttack
            };

            MakeNextDecision();
        }

        /// <summary>
        /// Sets queued move to zero and queued attacks / roll to false
        /// </summary>
        protected void ResetQueue()
        {
            m_queuedMove = Vector3.zero;
            m_queuedRoll = false;
            m_queuedLightAttack = false;
            m_queuedHeavyAttack = false;
        }

        /// <summary>
        /// This function is what drives all other decisions, calling functions based on BossState (Idle, Attacking, Stunned).
        /// It can be overriden, but I don't see why one would want to.
        /// </summary>
        protected virtual void MakeNextDecision()
        {
            //Reset everything
            ResetQueue();
            
            BossState boss_state = boss_body.State;

            switch (boss_state)
            {
                case BossState.Idle: IfBossIdle(); break;
                case BossState.Attacking:
                    BossMoveData atk_data = boss_body.CurrentAttackData;
                    AttackPhase attack_phase = boss_body.Phase;
                    IfBossAttacking(atk_data, attack_phase); 
                    break;
                case BossState.Stunned: IfBossStunned(); break;
                default: break;
            }
        }

        /// <summary>
        /// What this PlayerBot does if the Boss is Idle. Within it, you'll probably want to call RunAtAndAttack().
        /// </summary>
        protected virtual void IfBossIdle()
        {
            RunAtAndAttack();
        }

        /// <summary>
        /// What this PlayerBot does if the Boss is Attacking. MakeNextDecision() feeds this function the boss's attack data every fixed update.
        /// Look at BossMoveData and AttackPhase to see what they are.
        /// </summary>
        protected virtual void IfBossAttacking(BossMoveData boss_attack_data, AttackPhase boss_attack_phase)
        {
            //Get all relevant boss data
            Vector3 toBoss = (boss_transform.position - transform.position).normalized;
            toBoss.y = 0f;

            switch (boss_attack_data.Move)
            {
                //Try to punish super attacks
                case BossMove.SuperAttack:
                    RunAtAndAttack();
                    break;
                //Dodge ranged shots
                case BossMove.RangedShot:
                    m_queuedRoll = true;
                    m_queuedMove = new Vector3(-toBoss.z, 0, toBoss.x);
                    break;
                //default is to run away
                default:
                    m_queuedMove = toBoss * -1;
                    break;
            }
        }

        /// <summary>
        /// What this PlayerBot does if the Boss is Stunned. Within it, you'll probably want to call RunAtAndAttack().
        /// </summary>
        protected virtual void IfBossStunned()
        {
            RunAtAndAttack(true);
        }

        #region HELPER FUNCTIONS
        /// <summary>
        /// Queues an intent to run directly towards the boss and use a light or heavy attack if they are within m_attack_distance.
        /// The default is light attack.
        /// </summary>
        protected void RunAtAndAttack(bool heavy_attack = false)
        {
            //Calculate how far boss is
            float distance = GetDistanceToBoss();

            //Use info to decide whether to attack or move towards boss
            if (distance < m_attack_distance)
            {
                if (heavy_attack) m_queuedHeavyAttack = true;
                else m_queuedLightAttack = true;
            }
            else
            {
                Vector3 move = GetVectorToBoss();
                m_queuedMove = move;
            }
        }

        /// <summary>
        /// Helper function for writing PlayerBots
        /// </summary>
        /// <returns>Normalized Vector3 that points from this PlayerBot directly to the Boss.</returns>
        protected Vector3 GetVectorToBoss()
        {
            Vector3 move = (boss_transform.position - transform.position).normalized;
            move.y = 0f;
            return move;
        }

        /// <summary>
        /// Helper function for writing PlayerBots
        /// </summary>
        /// <returns>Distance from this PlayerBot to the Boss</returns>
        protected float GetDistanceToBoss()
        {
            var toBoss = boss_transform.position - transform.position;
            toBoss.y = 0f;
            float distance = toBoss.magnitude;
            return distance;
        }
        #endregion

        public Intent GetIntent()
        {
            return m_intent;
        }
    }
}
