using System;
using System.Linq;
using BossFight.Arena;
using BossFight.Boss;
using BossFight.Combat;
using BossFight.Core;
using BossFight.Player;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace BossFight.RL
{
    /// <summary>
    /// The boss's brain. Each decision it picks one <see cref="BossMove"/> (one discrete branch, masked by
    /// <see cref="BossBody.CanPerform"/>) and is rewarded for damage and for winning. When its arena's fight ends the
    /// episode ends. With the arena's <see cref="FightManager"/> Auto Restart off (training) the next episode starts a
    /// new fight straight away. With it on (play scenes) the boss stands still until the arena starts the next round
    /// after its reset delay. Lives on <c>Agent/Prefabs/BossAgent.prefab</c> next to Behavior Parameters and a Decision
    /// Requester. Observations, rewards and the episode loop are described in Agent/README.md.
    /// </summary>
    [RequireComponent(typeof(BossBody))]
    public class BossAgent : Agent
    {
        static readonly BossMove[] AllMoves = (BossMove[])Enum.GetValues(typeof(BossMove));
        static readonly BossMove[] AttackMoves = AllMoves.Where(BossMoveSet.IsAttack).ToArray();
        static readonly BossMove[] LocomotionMoves = AllMoves.Where(BossMoveSet.IsLocomotion).ToArray();
        static readonly int StateCount = Enum.GetValues(typeof(BossState)).Length;
        static readonly int PhaseCount = Enum.GetValues(typeof(AttackPhase)).Length;
        const float MaxOpponentSpeed = 10f;   // m/s, the opponent's velocity is divided by this
        const float MaxAttackDamage = 50f;    // the opponent's current attack damage is divided by this

        /// <summary>Discrete actions: one per <see cref="BossMove"/>, None first. Behavior Parameters needs one branch this size.</summary>
        public static readonly int MoveCount = AllMoves.Length;

        /// <summary>Floats written by <see cref="CollectObservations"/>. Behavior Parameters needs this Vector Observation size.</summary>
        public static readonly int ObservationSize =
            (1 + StateCount + (AttackMoves.Length + 1) + PhaseCount + 1 + AttackMoves.Length + 1 + (LocomotionMoves.Length + 1))  // the boss
            + 7                                  // where the opponent is
            + (5 + PhaseCount)                   // what the opponent is doing
            + 2 * OpponentMemory.HabitCount      // the opponent's habits
            + 3                                  // the boss's shot
            + 3;                                 // the arena

        [Header("Arena")]
        [Tooltip("This arena's FightManager. Auto Restart off (training): the agent starts every fight. On (play scenes): the agent waits for the arena's next round.")]
        [SerializeField] FightManager arena;
        [Tooltip("The other fighter in this arena. Needs a Health on its root.")]
        [SerializeField] GameObject opponent;
        [Tooltip("The middle of the arena floor.")]
        [SerializeField] Transform arenaCenter;
        [Tooltip("Meters from the middle of the arena to a wall. Positions and distances are scaled by it.")]
        [SerializeField, Min(1f)] float arenaHalfSize = 19f;

        [Header("Seeing the opponent")]
        [Tooltip("Seconds late the boss sees everything about its opponent, like a person's reaction time. reaction_delay in the trainer config overrides it.")]
        [SerializeField, Range(0f, OpponentMemory.MaxDelay)] float reactionDelay = 0.15f;
        [Tooltip("The opponent's heavy attack, to tell it apart from its light attack in the habit observations.")]
        [SerializeField] AttackData opponentHeavyAttack;

        [Header("Rewards (environment_parameters in the trainer config override these)")]
        [Tooltip("Earned for a win at the very start of a round. Later wins earn less, see Win Time Decay.")]
        [SerializeField] float winReward = 1f;
        [Tooltip("Share of the win reward lost by winning at the last second. 0.5: a win at the buzzer is worth half.")]
        [SerializeField, Range(0f, 1f)] float winTimeDecay = 0.5f;
        [Tooltip("Lost for a loss, however long the fight took.")]
        [SerializeField] float lossPenalty = 1f;
        [Tooltip("Lost when time runs out. Keep it between the smallest win and the loss penalty, so every win beats a draw and every draw beats a loss.")]
        [SerializeField] float drawPenalty = 0.5f;
        [Tooltip("Earned for taking the opponent's whole health bar, in proportion for smaller hits. Only damage that comes off the bar counts.")]
        [SerializeField] float damageDealtReward = 0.5f;
        [Tooltip("Lost for losing the boss's whole health bar, in proportion for smaller hits. Hits while stunned count double.")]
        [SerializeField] float damageTakenPenalty = 0.5f;
        [Tooltip("Lost for every second a fight is on. Off by default: it also makes a slow loss cost more than a fast one.")]
        [SerializeField] float timePenaltyPerSecond = 0f;

        [Header("Hand play")]
        [Tooltip("An IIntentSource (such as UserInput) that the Heuristic behavior reads. Empty: the heuristic stands still.")]
        [SerializeField] MonoBehaviour heuristicInput;

        BossBody body;
        Health health;
        Health opponentHealth;
        Stamina opponentStamina;
        AttackRunner opponentRunner;
        PlayerBody opponentBody;
        IIntentSource keyboard;
        OpponentMemory memory;
        float delay;
        float maxStun = 0.01f;
        float roundLength = 1f;
        float fightStartedAt;
        BossMove heldAttack;
        bool fightOver;
        bool waitingForArena;   // between rounds in a play scene, until the arena starts the next one
        bool firstEpisode = true;
        string outcome;
        float bossHealthSeen, opponentHealthSeen;   // health after the last hit, so only damage that comes off the bar counts
        float win, winDecay, loss, draw, dealt, taken, perSecond;   // this episode's reward weights

        public BossBody Body => body;
        public FightManager Arena => arena;
        public GameObject Opponent => opponent;
        /// <summary>The move chosen at the last decision.</summary>
        public BossMove LastMove { get; private set; }
        /// <summary>How the last fight ended and what that episode earned.</summary>
        public string LastResult { get; private set; } = "none yet";

        public override void Initialize()
        {
            body = GetComponent<BossBody>();
            health = GetComponent<Health>();
            if (opponent != null)
            {
                opponentHealth = opponent.GetComponent<Health>();
                opponentStamina = opponent.GetComponent<Stamina>();
                opponentRunner = opponent.GetComponent<AttackRunner>();
                opponentBody = opponent.GetComponent<PlayerBody>();
            }
            keyboard = heuristicInput as IIntentSource;
            memory = new OpponentMemory(Time.fixedDeltaTime);
            bossHealthSeen = health.Current;
            opponentHealthSeen = opponentHealth != null ? opponentHealth.Current : 0f;
            foreach (var move in body.Moves)
                if (move != null) maxStun = Mathf.Max(maxStun, move.WindupHitStunSeconds);

            if (arena == null || opponentHealth == null)
                Debug.LogError($"{name}: set Arena and an Opponent with a Health on this BossAgent (it belongs in an arena).", this);
            if (heuristicInput != null && keyboard == null)
                Debug.LogWarning($"{name}: Heuristic Input is not an IIntentSource, hand play will stand still.", this);
            if (opponentRunner != null && opponentHeavyAttack == null)
                Debug.LogWarning($"{name}: set Opponent Heavy Attack, or every opponent attack counts as a light one in its habits.", this);
            var brain = GetComponent<BehaviorParameters>().BrainParameters;
            if (brain.VectorObservationSize != ObservationSize)
                Debug.LogError($"{name}: Behavior Parameters has Vector Observation Space Size {brain.VectorObservationSize}, BossAgent writes {ObservationSize}.", this);
            var branches = brain.ActionSpec.BranchSizes;
            if (brain.ActionSpec.NumContinuousActions != 0 || branches == null || branches.Length != 1 || branches[0] != MoveCount)
                Debug.LogError($"{name}: Behavior Parameters needs no continuous actions and one discrete branch of {MoveCount}.", this);
        }

        protected override void OnEnable()
        {
            base.OnEnable();   // runs Initialize the first time
            if (arena != null) arena.FightEnded += OnFightEnded;
            if (health != null) health.Damaged += OnDamaged;
            if (opponentHealth != null) opponentHealth.Damaged += OnOpponentDamaged;
            if (opponentHealth != null) opponentHealth.InvulnerabilityGranted += OnOpponentRolled;
            if (opponentRunner != null) opponentRunner.PhaseChanged += OnOpponentPhaseChanged;
        }

        protected override void OnDisable()
        {
            if (arena != null) arena.FightEnded -= OnFightEnded;
            if (health != null) health.Damaged -= OnDamaged;
            if (opponentHealth != null) opponentHealth.Damaged -= OnOpponentDamaged;
            if (opponentHealth != null) opponentHealth.InvulnerabilityGranted -= OnOpponentRolled;
            if (opponentRunner != null) opponentRunner.PhaseChanged -= OnOpponentPhaseChanged;
            base.OnDisable();
        }

        public override void OnEpisodeBegin()
        {
            var parameters = Academy.Instance.EnvironmentParameters;
            win = parameters.GetWithDefault("win_reward", winReward);
            winDecay = Mathf.Clamp01(parameters.GetWithDefault("win_time_decay", winTimeDecay));
            loss = parameters.GetWithDefault("loss_penalty", lossPenalty);
            draw = parameters.GetWithDefault("draw_penalty", drawPenalty);
            dealt = parameters.GetWithDefault("damage_dealt_reward", damageDealtReward);
            taken = parameters.GetWithDefault("damage_taken_penalty", damageTakenPenalty);
            perSecond = parameters.GetWithDefault("time_penalty_per_second", timePenaltyPerSecond);
            delay = Mathf.Clamp(parameters.GetWithDefault("reaction_delay", reactionDelay), 0f, OpponentMemory.MaxDelay);

            fightOver = false;
            heldAttack = BossMove.None;
            if (arena != null && arena.AutoRestart)
            {
                // A play scene: the arena shows the result and starts the next round by itself after its reset delay.
                // Stand still until then (FixedUpdate watches for it).
                waitingForArena = !arena.IsFightActive;
                firstEpisode = false;
                if (!waitingForArena) BeginFight();
                return;
            }
            // Training: the arena starts its first fight by itself in Start. Every fight after that starts here.
            if (arena != null && (!firstEpisode || !arena.IsFightActive)) arena.StartNewFight();
            firstEpisode = false;
            BeginFight();
        }

        // Everything a new fight needs once the arena has reset it.
        void BeginFight()
        {
            if (arena != null) roundLength = Mathf.Max(arena.TimeRemaining, 0.01f);
            // The arena refills health and moves the fighters. A player that died last fight also needs its own reset
            // to come back to life. FightManager does that too since T11, and a second reset does no harm.
            if (opponentBody != null) opponentBody.Reset();
            body.ResetForEpisode();   // after the arena's reset, which starts a cooldown when it cuts a swing short
            if (opponent != null) body.Target = opponent.transform;
            Physics.SyncTransforms();   // a CharacterController ignores a teleport until physics has seen it
            fightStartedAt = Time.time;
            memory.Clear();   // nothing from the last fight, not even the opponent's habits
            RecordOpponent();
            bossHealthSeen = health.Current;
            opponentHealthSeen = opponentHealth != null ? opponentHealth.Current : 0f;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            if (opponentHealth == null)
            {
                for (int i = 0; i < ObservationSize; i++) sensor.AddObservation(0f);
                return;
            }

            // The boss
            sensor.AddObservation(health.Normalized);
            sensor.AddOneHotObservation((int)body.State, StateCount);
            sensor.AddOneHotObservation(Array.IndexOf(AttackMoves, body.CurrentAttack) + 1, AttackMoves.Length + 1);
            sensor.AddOneHotObservation((int)body.Phase, PhaseCount);
            sensor.AddObservation(Progress(body.CurrentAttackData, body.Phase, body.TimeInPhase));
            foreach (var attack in AttackMoves)
            {
                var data = body.DataFor(attack);
                sensor.AddObservation(data != null && data.CooldownSeconds > 0f ? Mathf.Clamp01(body.CooldownRemaining(attack) / data.CooldownSeconds) : 0f);
            }
            sensor.AddObservation(Mathf.Clamp01(body.StunRemaining / maxStun));
            sensor.AddOneHotObservation(Array.IndexOf(LocomotionMoves, body.Locomotion) + 1, LocomotionMoves.Length + 1);

            // Everything about the opponent is as it was Reaction Delay seconds ago
            RecordOpponent();
            var seen = memory.Seen(Time.fixedTime - delay);

            // Where the opponent is, in the boss's frame
            var here = transform.position;
            var toOpponent = seen.Position - here;
            toOpponent.y = 0f;
            float distance = toOpponent.magnitude;
            var towardOpponent = distance > 0.001f ? toOpponent / distance : transform.forward;
            sensor.AddObservation(Mathf.Clamp01(distance / (2f * arenaHalfSize)));
            sensor.AddObservation(Flat(transform.InverseTransformDirection(towardOpponent)));
            sensor.AddObservation(Flat(transform.InverseTransformDirection(seen.Forward)));
            sensor.AddObservation(Vector2.ClampMagnitude(Flat(transform.InverseTransformDirection(seen.Velocity)) / MaxOpponentSpeed, 1f));

            // What the opponent is doing
            sensor.AddObservation(seen.Health);
            sensor.AddObservation(seen.Stamina);
            sensor.AddOneHotObservation((int)seen.Phase, PhaseCount);
            sensor.AddObservation(seen.PhaseProgress);
            sensor.AddObservation(Mathf.Clamp01(seen.AttackDamage / MaxAttackDamage));
            sensor.AddObservation(seen.Invulnerable);

            // The opponent's habits: seconds since its last roll, light attack and heavy attack, then the usual gap
            // between uses of each (lower: it does that more often)
            for (int i = 0; i < OpponentMemory.HabitCount; i++)
                sensor.AddObservation(Mathf.Clamp01(seen.SinceLast((OpponentMemory.Habit)i) / OpponentMemory.NeverUsed));
            for (int i = 0; i < OpponentMemory.HabitCount; i++)
                sensor.AddObservation(Mathf.Clamp01(seen.UsualGap((OpponentMemory.Habit)i) / OpponentMemory.NeverUsed));

            // The boss's own shot nearest the opponent: is one flying, how far from them, is it heading at them
            BossProjectile shot = null;
            float shotDistance = float.MaxValue;
            foreach (var projectile in body.Projectiles)
            {
                if (projectile == null) continue;
                var toTarget = seen.Position - projectile.transform.position;
                toTarget.y = 0f;
                if (toTarget.magnitude < shotDistance)
                {
                    shot = projectile;
                    shotDistance = toTarget.magnitude;
                }
            }
            sensor.AddObservation(shot != null);
            sensor.AddObservation(shot != null ? Mathf.Clamp01(shotDistance / (2f * arenaHalfSize)) : 0f);
            sensor.AddObservation(shot != null ? Heading(shot, seen.Position) : 0f);

            // The arena: where the boss stands, sideways and along the line to the opponent, then the round clock
            var fromCenter = arenaCenter != null ? here - arenaCenter.position : Vector3.zero;
            var sideways = Vector3.Cross(Vector3.up, towardOpponent);
            sensor.AddObservation(Mathf.Clamp(Vector3.Dot(fromCenter, sideways) / arenaHalfSize, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(Vector3.Dot(fromCenter, towardOpponent) / arenaHalfSize, -1f, 1f));
            sensor.AddObservation(arena != null ? Mathf.Clamp01(arena.TimeRemaining / roundLength) : 0f);
        }

        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
        {
            // AllMoves[0] is None, which stays allowed so there is always a legal choice, even when dead.
            // Between rounds of a play scene None is the only choice.
            for (int i = 1; i < AllMoves.Length; i++)
                actionMask.SetActionEnabled(0, i, !waitingForArena && body.CanPerform(AllMoves[i]));
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            int choice = actions.DiscreteActions[0];
            LastMove = !waitingForArena && choice >= 0 && choice < AllMoves.Length ? AllMoves[choice] : BossMove.None;
            body.TryPerform(LastMove);
        }

        /// <summary>Hand play with the Boss sandbox keys, read from Heuristic Input.</summary>
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var move = BossMove.None;
            if (keyboard != null)
            {
                var intent = keyboard.GetIntent();
                move = heldAttack != BossMove.None ? heldAttack : RequestedAttack(intent);
                if (move == BossMove.None && body.Target != null)
                    move = BossIntentDriver.LocomotionFor(intent.Move, transform.position, body.Target.position);
            }
            heldAttack = BossMove.None;
            var discrete = actionsOut.DiscreteActions;
            discrete[0] = Array.IndexOf(AllMoves, move);
        }

        void FixedUpdate()
        {
            // A fight ends in the middle of a hit. Ending the episode here, one step later, keeps the reset out of that
            // hit's own death handlers.
            if (fightOver)
            {
                fightOver = false;
                LastResult = $"{outcome}, episode reward {GetCumulativeReward():+0.00;-0.00}";
                EndEpisode();   // OnEpisodeBegin starts the next fight, or waits for the arena to
                return;
            }
            if (waitingForArena)
            {
                if (arena == null || !arena.IsFightActive) return;   // still between rounds
                waitingForArena = false;
                BeginFight();
            }
            RecordOpponent();
            if (arena != null && arena.IsFightActive) AddReward(-perSecond * Time.fixedDeltaTime);
            // Remember an attack key between decisions, so a quick tap is not lost in the 0.1 s gap.
            if (keyboard != null && heldAttack == BossMove.None) heldAttack = RequestedAttack(keyboard.GetIntent());
        }

        void OnFightEnded(GameObject boss, GameObject player, bool bossWon, float duration)
        {
            // The arena only says whether the boss won. If it didn't and is still standing, time ran out.
            bool timedOut = !bossWon && !health.IsDead;
            // A win is worth less the longer it took (Unity's Soccer example does the same), a loss always costs the same,
            // and a draw sits in between: every win beats every draw, every draw beats every loss.
            float roundUsed = arena != null ? Mathf.Clamp01(1f - arena.TimeRemaining / roundLength) : 0f;
            if (bossWon) AddReward(win * (1f - winDecay * roundUsed));
            else if (timedOut) AddReward(-draw);
            else AddReward(-loss);
            outcome = bossWon ? "Boss won" : timedOut ? "Draw, time ran out" : "Boss lost";

            var stats = Academy.Instance.StatsRecorder;
            stats.Add("Fight/BossWinRate", bossWon ? 1f : 0f);
            stats.Add("Fight/DrawRate", timedOut ? 1f : 0f);
            stats.Add("Fight/Length", Time.time - fightStartedAt);
            fightOver = true;
        }

        // Only what comes off a health bar counts, so the overkill on a killing blow earns nothing extra.
        void OnDamaged(DamageInfo hit)
        {
            float lost = bossHealthSeen - health.Current;
            bossHealthSeen = health.Current;
            if (lost > 0f) AddReward(-taken * lost / health.Max);
        }

        void OnOpponentDamaged(DamageInfo hit)
        {
            float lost = opponentHealthSeen - opponentHealth.Current;
            opponentHealthSeen = opponentHealth.Current;
            if (lost > 0f && hit.Source == gameObject) AddReward(dealt * lost / opponentHealth.Max);
        }

        // Only a roll grants the player i-frames.
        void OnOpponentRolled(float seconds) => memory?.Used(OpponentMemory.Habit.Roll);

        void OnOpponentPhaseChanged(AttackData attack, AttackPhase phase)
        {
            // Every attack starts with its windup.
            if (phase != AttackPhase.Windup || memory == null) return;
            memory.Used(attack == opponentHeavyAttack ? OpponentMemory.Habit.HeavyAttack : OpponentMemory.Habit.LightAttack);
        }

        // One sighting per physics step, the source of everything the boss observes about its opponent.
        void RecordOpponent()
        {
            if (memory == null || opponentHealth == null || memory.Has(Time.fixedTime)) return;
            var phase = opponentRunner != null ? opponentRunner.Phase : AttackPhase.Idle;
            var swing = opponentRunner != null ? opponentRunner.CurrentAttack : null;
            memory.Record(new OpponentMemory.Sighting
            {
                Time = Time.fixedTime,
                Position = opponent.transform.position,
                Forward = opponent.transform.forward,
                Health = opponentHealth.Normalized,
                Stamina = opponentStamina != null ? opponentStamina.Normalized : 0f,
                Phase = phase,
                PhaseProgress = Progress(swing, phase, opponentRunner != null ? opponentRunner.TimeInPhase : 0f),
                AttackDamage = swing != null ? swing.Damage : 0f,
                Invulnerable = opponentHealth.IsInvulnerable,
            });
        }

        // 1 when the shot is flying straight at the point, -1 when straight away from it.
        static float Heading(BossProjectile shot, Vector3 point)
        {
            var toPoint = point - shot.transform.position;
            var direction = shot.Direction;
            toPoint.y = 0f;
            direction.y = 0f;
            if (toPoint.sqrMagnitude < 0.0001f || direction.sqrMagnitude < 0.0001f) return 0f;
            return Vector3.Dot(direction.normalized, toPoint.normalized);
        }

        // The first attack whose key is down and that can start now.
        BossMove RequestedAttack(Intent intent)
        {
            foreach (var attack in BossIntentDriver.Attacks)
                if (BossIntentDriver.Requests(intent, attack) && body.CanPerform(attack)) return attack;
            return BossMove.None;
        }

        static float Progress(AttackData attack, AttackPhase phase, float timeInPhase)
        {
            if (attack == null) return 0f;
            float length = phase == AttackPhase.Windup ? attack.WindupSeconds
                : phase == AttackPhase.Active ? attack.ActiveSeconds
                : phase == AttackPhase.Recovery ? attack.RecoverySeconds
                : 0f;
            return length > 0f ? Mathf.Clamp01(timeInPhase / length) : 0f;
        }

        static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);
    }
}
