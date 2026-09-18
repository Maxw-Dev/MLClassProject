using System;
using System.Collections.Generic;
using BossFight.Combat;
using BossFight.Core;
using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>
    /// The boss body. Whoever drives it (the RL agent, or a debug driver in the sandbox) asks <see cref="CanPerform"/>
    /// and calls <see cref="TryPerform"/> with a <see cref="BossMove"/>. The body never cares who asked.
    /// Locomotion moves stick until another move replaces them; None stops. Attacks run through Combat's
    /// <see cref="AttackRunner"/> and lock the body until recovery ends. A hit during the windup of a move that allows it
    /// (the super) interrupts the move and leaves the boss stunned and taking extra damage.
    /// Everything ticks in FixedUpdate, so it behaves the same at training time scale.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(AttackRunner), typeof(Health))]
    public class BossBody : MonoBehaviour
    {
        const float Gravity = 9.81f;

        [Header("Moves")]
        [Tooltip("One BossMoveData per attack. An attack without an asset can never be performed.")]
        [SerializeField] BossMoveData[] moves = Array.Empty<BossMoveData>();
        [Tooltip("The one hitbox child. It is reshaped from the move's data before each melee attack.")]
        [SerializeField] Hitbox meleeHitbox;
        [Tooltip("Where projectiles spawn. The body's own transform when empty.")]
        [SerializeField] Transform muzzle;

        [Header("Target")]
        [Tooltip("The player. Found by the Player tag when empty.")]
        [SerializeField] Transform target;
        [Tooltip("Advance stops this far from the target, center to center, so the boss does not shove into it.")]
        [SerializeField, Min(0f)] float stopDistance = 2f;

        [Header("Locomotion (m/s and deg/s)")]
        [SerializeField, Min(0f)] float advanceSpeed = 3.5f;
        [SerializeField, Min(0f)] float retreatSpeed = 2.5f;
        [SerializeField, Min(0f)] float strafeSpeed = 3f;
        [Tooltip("Turn rate while idle or moving.")]
        [SerializeField, Min(0f)] float turnSpeed = 360f;
        [Tooltip("Turn rate during a windup: the boss keeps tracking the player but can be outrun. No turning after that.")]
        [SerializeField, Min(0f)] float windupTurnSpeed = 90f;

        CharacterController controller;
        AttackRunner runner;
        Health health;
        readonly Dictionary<BossMove, BossMoveData> byMove = new Dictionary<BossMove, BossMoveData>();
        BossMoveSet moveSet = new BossMoveSet(Array.Empty<(BossMove, float)>());
        BossMove locomotion = BossMove.None;
        BossMoveData current;

        public BossState State => moveSet.State;
        public BossMove CurrentAttack => moveSet.CurrentAttack;
        public BossMoveData CurrentAttackData => current;
        public BossMove Locomotion => locomotion;
        public AttackPhase Phase => runner.Phase;
        public float TimeInPhase => runner.TimeInPhase;
        public float StunRemaining => moveSet.StunRemaining;
        public Transform Target { get => target; set => target = value; }
        public IReadOnlyList<BossMoveData> Moves => moves;

        /// <summary>An attack and the phase it just entered: Windup, Active, Recovery, then Idle when it ends.</summary>
        public event Action<BossMoveData, AttackPhase> AttackPhaseChanged;
        /// <summary>Idle, Attacking, Stunned or Dead. Fires after the phase event when an attack starts.</summary>
        public event Action<BossState> StateChanged;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            runner = GetComponent<AttackRunner>();
            health = GetComponent<Health>();
            if (meleeHitbox == null) meleeHitbox = GetComponentInChildren<Hitbox>();
            if (muzzle == null) muzzle = transform;
            BuildMoveSet();
            runner.PhaseChanged += OnPhaseChanged;
            runner.Finished += OnFinished;
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        void Start()
        {
            if (target != null) return;
            var player = GameObject.FindWithTag("Player");
            if (player != null) target = player.transform;
        }

        /// <summary>For tests and builders: what a prefab would wire up in the inspector.</summary>
        public void Configure(BossMoveData[] moveData, Hitbox melee, Transform projectileMuzzle, Transform targetTransform)
        {
            moves = moveData ?? Array.Empty<BossMoveData>();
            meleeHitbox = melee;
            muzzle = projectileMuzzle != null ? projectileMuzzle : transform;
            target = targetTransform;
            BuildMoveSet();
        }

        void BuildMoveSet()
        {
            byMove.Clear();
            var cooldowns = new List<(BossMove, float)>();
            foreach (var data in moves)
            {
                if (data == null) continue;
                if (!BossMoveSet.IsAttack(data.Move))
                {
                    Debug.LogWarning($"{name}: {data.name} is for {data.Move}, which is not an attack. Ignored.", this);
                    continue;
                }
                if (byMove.ContainsKey(data.Move))
                {
                    Debug.LogWarning($"{name}: two assets for {data.Move}. Keeping {byMove[data.Move].name}.", this);
                    continue;
                }
                byMove[data.Move] = data;
                cooldowns.Add((data.Move, data.CooldownSeconds));
            }
            moveSet = new BossMoveSet(cooldowns);
        }

        public float CooldownRemaining(BossMove move) => moveSet.CooldownRemaining(move);
        public BossMoveData DataFor(BossMove move) => byMove.TryGetValue(move, out var data) ? data : null;

        /// <summary>Can this move start right now: not busy, not stunned, not dead, and off cooldown.</summary>
        public bool CanPerform(BossMove move)
        {
            if (!moveSet.CanPerform(move)) return false;
            if (!BossMoveSet.IsAttack(move)) return true;
            var data = byMove[move];
            if (!data.FiresProjectile && meleeHitbox == null) return false;
            return runner.CanStart(data);
        }

        /// <summary>Starts the move if <see cref="CanPerform"/> allows it. Locomotion sticks until replaced; None stops.</summary>
        public bool TryPerform(BossMove move)
        {
            if (!CanPerform(move)) return false;
            if (move == BossMove.None)
            {
                locomotion = BossMove.None;
                return true;
            }
            if (BossMoveSet.IsLocomotion(move))
            {
                locomotion = move;
                return true;
            }
            return StartAttack(byMove[move]);
        }

        /// <summary>Episode reset: stop everything, full health, cooldowns cleared. Moving the body is the arena's job.</summary>
        public void ResetForEpisode()
        {
            runner.Interrupt();
            current = null;
            locomotion = BossMove.None;
            moveSet.Reset();
            health.ResetToFull();
            StateChanged?.Invoke(State);
        }

        bool StartAttack(BossMoveData data)
        {
            // State goes first: the runner reports Windup synchronously, and listeners read State and CurrentAttackData.
            locomotion = BossMove.None;
            current = data;
            moveSet.BeginAttack(data.Move);

            bool started;
            if (data.FiresProjectile)
            {
                started = runner.TryStartUnarmed(data);
            }
            else
            {
                meleeHitbox.Offset = data.HitOffset;
                meleeHitbox.Radius = data.HitRadius;
                started = runner.TryStart(data, meleeHitbox);
            }

            if (!started)
            {
                current = null;
                moveSet.CancelAttack();
                return false;
            }
            StateChanged?.Invoke(State);
            return true;
        }

        void OnPhaseChanged(AttackData attack, AttackPhase phase)
        {
            if (current == null) return;
            if (phase == AttackPhase.Active && current.FiresProjectile) Fire(current);
            AttackPhaseChanged?.Invoke(current, phase);
        }

        void OnFinished(AttackData attack, bool interrupted)
        {
            var data = current;
            current = null;
            if (data == null || moveSet.State != BossState.Attacking) return;   // death, reset or stun already settled the state
            moveSet.EndAttack();
            StateChanged?.Invoke(State);
        }

        /// <summary>A hit landed. During an interruptible windup that means the move is lost and the boss is stunned.</summary>
        void OnDamaged(DamageInfo info)
        {
            var data = current;
            if (data == null || moveSet.State != BossState.Attacking || runner.Phase != AttackPhase.Windup) return;
            if (data.WindupHitStunSeconds <= 0f) return;

            runner.Interrupt();                       // OnFinished ends the attack and starts its cooldown
            moveSet.Stun(data.WindupHitStunSeconds);
            health.IncomingDamageMultiplier = data.StunDamageMultiplier;
            StateChanged?.Invoke(State);
        }

        void OnDied()
        {
            moveSet.Kill();
            locomotion = BossMove.None;
            health.IncomingDamageMultiplier = 1f;
            runner.Interrupt();   // reports Idle and Finished; OnFinished sees Dead and leaves it
            current = null;
            StateChanged?.Invoke(State);
        }

        void Fire(BossMoveData data)
        {
            var projectile = Instantiate(data.ProjectilePrefab, muzzle.position, Quaternion.LookRotation(transform.forward));
            projectile.gameObject.SetActive(true);
            projectile.Launch(data, gameObject, transform.forward, data.ProjectileSpeed, data.ProjectileRange);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            bool wasStunned = moveSet.State == BossState.Stunned;
            moveSet.Tick(dt);
            if (wasStunned && moveSet.State == BossState.Idle)
            {
                health.IncomingDamageMultiplier = 1f;
                StateChanged?.Invoke(State);
            }
            Face(dt);
            Move(dt);
        }

        void Face(float dt)
        {
            if (target == null) return;
            float speed;
            switch (State)
            {
                case BossState.Idle: speed = turnSpeed; break;
                case BossState.Attacking: speed = Phase == AttackPhase.Windup ? windupTurnSpeed : 0f; break;
                default: speed = 0f; break;
            }
            if (speed <= 0f) return;

            var toTarget = target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toTarget), speed * dt);
        }

        void Move(float dt)
        {
            var velocity = Vector3.zero;
            if (State == BossState.Idle && locomotion != BossMove.None && target != null)
            {
                var toTarget = target.position - transform.position;
                toTarget.y = 0f;
                float distance = toTarget.magnitude;
                var forward = distance > 0.001f ? toTarget / distance : transform.forward;
                var right = Vector3.Cross(Vector3.up, forward);
                switch (locomotion)
                {
                    case BossMove.Advance: if (distance > stopDistance) velocity = forward * advanceSpeed; break;
                    case BossMove.Retreat: velocity = -forward * retreatSpeed; break;
                    case BossMove.StrafeLeft: velocity = -right * strafeSpeed; break;
                    case BossMove.StrafeRight: velocity = right * strafeSpeed; break;
                }
            }
            velocity.y = -Gravity;   // keeps the controller on the ground
            controller.Move(velocity * dt);
        }
    }
}
