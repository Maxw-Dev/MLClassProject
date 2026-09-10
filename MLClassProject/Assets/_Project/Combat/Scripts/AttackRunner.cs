using System;
using BossFight.Core;
using UnityEngine;

namespace BossFight.Combat
{
    /// <summary>
    /// Plays an <see cref="AttackData"/> timeline: windup (the telegraph), active (hitbox live), recovery (punish window).
    /// Put it on the body root. Bodies call <see cref="TryStart"/>; it refuses while busy, disabled, or short on stamina.
    /// Phases advance in FixedUpdate, so timings are exact at any time scale. Keep ActiveSeconds at or above the fixed timestep.
    /// </summary>
    public class AttackRunner : MonoBehaviour
    {
        [Tooltip("Hitbox armed during the active window unless TryStart is given another one.")]
        [SerializeField] Hitbox defaultHitbox;
        [Tooltip("Optional. When set, attacks spend stamina and are refused when it is short.")]
        [SerializeField] Stamina stamina;

        readonly AttackTimeline timeline = new AttackTimeline();
        Hitbox activeHitbox;

        public AttackPhase Phase => timeline.Phase;
        public AttackData CurrentAttack => timeline.Attack;
        public bool IsBusy => timeline.IsBusy;
        public float TimeInPhase => timeline.TimeInPhase;

        /// <summary>The attack and the phase just entered: Windup, Active, Recovery, then Idle when it ends.</summary>
        public event Action<AttackData, AttackPhase> PhaseChanged;
        /// <summary>The attack that ended, and true if it was interrupted.</summary>
        public event Action<AttackData, bool> Finished;

        void Awake()
        {
            if (defaultHitbox == null) defaultHitbox = GetComponentInChildren<Hitbox>();
            if (stamina == null) stamina = GetComponent<Stamina>();
            timeline.PhaseEntered += OnPhaseEntered;
            timeline.Finished += (attack, interrupted) => Finished?.Invoke(attack, interrupted);
        }

        void Reset()
        {
            defaultHitbox = GetComponentInChildren<Hitbox>();
            stamina = GetComponent<Stamina>();
        }

        public bool CanStart(AttackData data) =>
            data != null && isActiveAndEnabled && !IsBusy && (stamina == null || stamina.CanAfford(data.StaminaCost));

        public bool TryStart(AttackData data, Hitbox hitbox = null)
        {
            if (!CanStart(data)) return false;
            activeHitbox = hitbox != null ? hitbox : defaultHitbox;
            if (stamina != null) stamina.TrySpend(data.StaminaCost);
            return timeline.Start(data);
        }

        /// <summary>Runs the timeline without arming any hitbox, for moves that bring their own damage (a projectile spawned on Active).</summary>
        public bool TryStartUnarmed(AttackData data)
        {
            if (!CanStart(data)) return false;
            activeHitbox = null;
            if (stamina != null) stamina.TrySpend(data.StaminaCost);
            return timeline.Start(data);
        }

        /// <summary>Stops the current attack immediately (stagger, death, episode reset). Fires PhaseChanged(Idle) and Finished(interrupted: true).</summary>
        public void Interrupt() => timeline.Interrupt();

        void FixedUpdate() => timeline.Tick(Time.fixedDeltaTime);

        void OnDisable() => Interrupt();

        void OnPhaseEntered(AttackData attack, AttackPhase phase)
        {
            if (activeHitbox != null)
            {
                if (phase == AttackPhase.Active) activeHitbox.Arm(attack, gameObject);
                else activeHitbox.Disarm();
            }
            if (phase == AttackPhase.Idle) activeHitbox = null;
            PhaseChanged?.Invoke(attack, phase);
        }
    }
}
