using System;
using System.Linq;
using BossFight.Arena;
using BossFight.Boss;
using BossFight.Combat;
using BossFight.Core;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace BossFight.ImitationBot
{
    /// <summary>
    /// The player's side as an ML-Agents Agent (T18). It sees what a person can see on screen, with the boss a
    /// reaction time late, and its actions become the player's <see cref="Intent"/> (see <see cref="PlayerActions"/>).
    /// Two ways to use it:
    /// <list type="bullet">
    /// <item>Recording: set Human Input to the player's UserInput and Behavior Type to Heuristic Only. The person drives
    /// the player exactly as in Agent_Play, and with a Demonstration Recorder on the player every decision is saved
    /// as what the agent saw and what the person did.</item>
    /// <item>As a bot: leave Human Input empty. The agent is the player's only IIntentSource and drives it.</item>
    /// </list>
    /// It never starts fights: the arena does (Auto Restart on) or the BossAgent does (training). Between fights it
    /// stands still and makes no decisions, so the recordings hold only fighting. ImitationBot/README.md describes
    /// the observations and how to record.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerAgent : Agent, IIntentSource
    {
        static readonly BossMove[] AttackMoves = ((BossMove[])Enum.GetValues(typeof(BossMove))).Where(BossMoveSet.IsAttack).ToArray();
        static readonly int StateCount = Enum.GetValues(typeof(BossState)).Length;
        static readonly int PhaseCount = Enum.GetValues(typeof(AttackPhase)).Length;
        const float MaxSpeed = 10f;          // m/s, velocities are divided by this
        const float MaxAttackDamage = 50f;   // the player's current swing damage is divided by this

        /// <summary>Floats written by <see cref="CollectObservations"/>. Behavior Parameters needs this Vector Observation size.</summary>
        public static readonly int ObservationSize =
            (5 + PhaseCount)                                          // the player itself
            + (1 + StateCount + (AttackMoves.Length + 1) + PhaseCount + 2)   // the boss
            + 9                                                       // where things are
            + 3                                                       // the boss's shot
            + 1;                                                      // the round clock

        [Header("Arena")]
        [Tooltip("This arena's FightManager. The agent ends an episode when its fight ends and waits for the next one.")]
        [SerializeField] FightManager arena;
        [Tooltip("The boss in this arena.")]
        [SerializeField] BossBody boss;
        [Tooltip("The middle of the arena floor.")]
        [SerializeField] Transform arenaCenter;
        [Tooltip("Meters from the middle of the arena to a wall. Positions and distances are scaled by it.")]
        [SerializeField, Min(1f)] float arenaHalfSize = 19f;

        [Header("Deciding")]
        [Tooltip("Physics steps between decisions. 5: every 0.1 s, like the boss. The last action is repeated in between.")]
        [SerializeField, Min(1)] int decisionPeriod = 5;
        [Tooltip("Seconds late the agent sees the boss, like a person's reaction time.")]
        [SerializeField, Range(0f, DelayLine<BossSighting>.MaxDelay)] float reactionDelay = 0.15f;

        [Header("Recording")]
        [Tooltip("A person's input, such as the player's UserInput. When set, the person drives the player and the agent only records what they do (Behavior Type Heuristic Only). Leave empty for the bot.")]
        [SerializeField] MonoBehaviour humanInput;

        [Header("Rewards")]
        [SerializeField] float winReward = 1f;
        [SerializeField, Range(0f, 1f)] float winTimeDecay = 0.5f;
        [SerializeField] float lossPenalty = 1f;
        [SerializeField] float drawPenalty = 0.5f;
        [SerializeField] float damageDealtReward = 0.5f;
        [SerializeField] float damageTakenPenalty = 0.5f;

        /// <summary>The boss at one physics step, as the agent will see it a reaction time later.</summary>
        public struct BossSighting
        {
            public Vector3 Position, Forward, Velocity;
            public float Health;
            public BossState State;
            public BossMove Attack;
            public AttackPhase Phase;
            public float PhaseProgress, Stun;   // 0..1
            public bool ShotInFlight;
            public Vector3 ShotPosition, ShotDirection;
        }

        Health health, bossHealth;
        Stamina stamina;
        AttackRunner runner;
        IIntentSource human;
        DelayLine<BossSighting> sightings;
        Vector3 lastBossPosition, lastOwnPosition, ownVelocity;
        float lastSampleTime;
        bool sampledBefore;
        float maxStun = 0.01f;
        float roundLength = 1f;
        float fightStartedAt;
        float bossHealthSeen, healthSeen;
        Vector3 decisionForward = Vector3.forward, decisionRight = Vector3.right;
        Intent intent;
        int stepsSinceDecision;
        bool fightOver, waitingForArena;
        string outcome;
        // A person's presses since the last decision, so a quick tap between two decisions still gets recorded.
        bool heldLight, heldHeavy, tappedRoll;

        /// <summary>The actions of the last decision.</summary>
        public PlayerActions LastActions { get; private set; }
        /// <summary>How the last fight ended and what that episode earned.</summary>
        public string LastResult { get; private set; } = "none yet";

        public override void Initialize()
        {
            health = GetComponent<Health>();
            stamina = GetComponent<Stamina>();
            runner = GetComponent<AttackRunner>();
            human = humanInput as IIntentSource;
            sightings = new DelayLine<BossSighting>(Time.fixedDeltaTime);
            if (boss != null)
            {
                bossHealth = boss.GetComponent<Health>();
                foreach (var move in boss.Moves)
                    if (move != null) maxStun = Mathf.Max(maxStun, move.WindupHitStunSeconds);
            }

            if (arena == null || boss == null || bossHealth == null)
                Debug.LogError($"{name}: set Arena and a Boss with a Health on this PlayerAgent.", this);
            if (humanInput != null && human == null)
                Debug.LogError($"{name}: Human Input is not an IIntentSource.", this);
            var brain = GetComponent<BehaviorParameters>().BrainParameters;
            if (brain.VectorObservationSize != ObservationSize)
                Debug.LogError($"{name}: Behavior Parameters has Vector Observation Space Size {brain.VectorObservationSize}, PlayerAgent writes {ObservationSize}.", this);
            var spec = brain.ActionSpec;
            if (spec.NumContinuousActions != PlayerActions.ContinuousCount || spec.BranchSizes == null || !spec.BranchSizes.SequenceEqual(PlayerActions.BranchSizes))
                Debug.LogError($"{name}: Behavior Parameters needs {PlayerActions.ContinuousCount} continuous actions and discrete branches {string.Join(", ", PlayerActions.BranchSizes)}.", this);
        }

        protected override void OnEnable()
        {
            base.OnEnable();   // runs Initialize the first time
            if (arena != null) arena.FightEnded += OnFightEnded;
            if (health != null) health.Damaged += OnDamaged;
            if (bossHealth != null) bossHealth.Damaged += OnBossDamaged;
        }

        protected override void OnDisable()
        {
            if (arena != null) arena.FightEnded -= OnFightEnded;
            if (health != null) health.Damaged -= OnDamaged;
            if (bossHealth != null) bossHealth.Damaged -= OnBossDamaged;
            base.OnDisable();
        }

        public override void OnEpisodeBegin()
        {
            fightOver = false;
            intent = default;
            // The arena or the BossAgent starts fights. Stand still until this one is on.
            waitingForArena = arena == null || !arena.IsFightActive;
            if (!waitingForArena) BeginFight();
        }

        void BeginFight()
        {
            if (arena != null) roundLength = Mathf.Max(arena.TimeRemaining, 0.01f);
            fightStartedAt = Time.time;
            stepsSinceDecision = 0;
            sightings.Clear();   // nothing from the last fight
            sampledBefore = false;
            ClearHeld();
            Sample();
            healthSeen = health.Current;
            bossHealthSeen = bossHealth != null ? bossHealth.Current : 0f;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            if (boss == null)
            {
                for (int i = 0; i < ObservationSize; i++) sensor.AddObservation(0f);
                return;
            }
            Sample();
            var seen = sightings.Seen(Time.fixedTime - reactionDelay);

            // The player itself
            sensor.AddObservation(health.Normalized);
            sensor.AddObservation(stamina != null ? stamina.Normalized : 0f);
            var phase = runner != null ? runner.Phase : AttackPhase.Idle;
            var swing = runner != null ? runner.CurrentAttack : null;
            sensor.AddOneHotObservation((int)phase, PhaseCount);
            sensor.AddObservation(Progress(swing, phase, runner != null ? runner.TimeInPhase : 0f));
            sensor.AddObservation(swing != null ? Mathf.Clamp01(swing.Damage / MaxAttackDamage) : 0f);
            sensor.AddObservation(health.IsInvulnerable);

            // The boss, a reaction time ago
            sensor.AddObservation(seen.Health);
            sensor.AddOneHotObservation((int)seen.State, StateCount);
            sensor.AddOneHotObservation(Array.IndexOf(AttackMoves, seen.Attack) + 1, AttackMoves.Length + 1);
            sensor.AddOneHotObservation((int)seen.Phase, PhaseCount);
            sensor.AddObservation(seen.PhaseProgress);
            sensor.AddObservation(seen.Stun);

            // Where things are, in the boss's direction (forward is toward it). Actions use the same frame.
            var here = transform.position;
            PlayerActions.Frame(here, seen.Position, out decisionForward, out decisionRight);
            var toBoss = seen.Position - here;
            toBoss.y = 0f;
            sensor.AddObservation(Mathf.Clamp01(toBoss.magnitude / (2f * arenaHalfSize)));
            sensor.AddObservation(InFrame(seen.Forward));
            sensor.AddObservation(Vector2.ClampMagnitude(InFrame(seen.Velocity) / MaxSpeed, 1f));
            sensor.AddObservation(Vector2.ClampMagnitude(InFrame(ownVelocity) / MaxSpeed, 1f));
            var fromCenter = arenaCenter != null ? InFrame(here - arenaCenter.position) / arenaHalfSize : Vector2.zero;
            sensor.AddObservation(new Vector2(Mathf.Clamp(fromCenter.x, -1f, 1f), Mathf.Clamp(fromCenter.y, -1f, 1f)));

            // The boss's shot nearest the player: is one flying, how far from the player, is it heading at them
            sensor.AddObservation(seen.ShotInFlight);
            var shotToHere = here - seen.ShotPosition;
            shotToHere.y = 0f;
            sensor.AddObservation(seen.ShotInFlight ? Mathf.Clamp01(shotToHere.magnitude / (2f * arenaHalfSize)) : 0f);
            var shotDirection = seen.ShotDirection;
            shotDirection.y = 0f;
            bool aimable = seen.ShotInFlight && shotToHere.sqrMagnitude > 0.0001f && shotDirection.sqrMagnitude > 0.0001f;
            sensor.AddObservation(aimable ? Vector3.Dot(shotDirection.normalized, shotToHere.normalized) : 0f);

            // The round clock
            sensor.AddObservation(arena != null ? Mathf.Clamp01(arena.TimeRemaining / roundLength) : 0f);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            var discrete = actions.DiscreteActions;
            var chosen = new PlayerActions
            {
                Walk = (PlayerActions.Move)Mathf.Clamp(discrete[0], 0, PlayerActions.BranchSizes[0] - 1),
                Button = (PlayerActions.Attack)Mathf.Clamp(discrete[1], 0, PlayerActions.BranchSizes[1] - 1),
                Roll = discrete[2] == 1,
            };
            LastActions = chosen;
            intent = waitingForArena ? default : chosen.ToIntent(decisionForward, decisionRight);
        }

        /// <summary>Records a person: what they are pressing now, plus any press since the last decision.</summary>
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var pressed = human != null ? human.GetIntent() : default;
            pressed.LightAttack |= heldLight;
            pressed.HeavyAttack |= heldHeavy;
            pressed.Roll |= tappedRoll;
            ClearHeld();
            var asActions = PlayerActions.FromIntent(pressed, decisionForward, decisionRight);
            var discrete = actionsOut.DiscreteActions;
            discrete[0] = (int)asActions.Walk;
            discrete[1] = (int)asActions.Button;
            discrete[2] = asActions.Roll ? 1 : 0;
        }

        /// <summary>The player's intent. A person drives directly while recording, the agent's choice otherwise.</summary>
        public Intent GetIntent() => human != null ? human.GetIntent() : intent;

        void Update() => LatchHuman();   // UserInput's roll lasts one rendered frame, so look every frame

        void FixedUpdate()
        {
            // A fight ends in the middle of a hit. Ending the episode one step later keeps the reset out of it.
            if (fightOver)
            {
                fightOver = false;
                LastResult = $"{outcome}, episode reward {GetCumulativeReward():+0.00;-0.00}";
                EndEpisode();   // OnEpisodeBegin waits for the next fight
                return;
            }
            if (waitingForArena)
            {
                if (arena == null || !arena.IsFightActive) return;   // between fights: no decisions, nothing recorded
                waitingForArena = false;
                BeginFight();
            }
            Sample();
            LatchHuman();
            if (stepsSinceDecision % decisionPeriod == 0) RequestDecision();
            else RequestAction();   // repeat the last decision's actions
            stepsSinceDecision++;
        }

        void LatchHuman()
        {
            if (human == null) return;
            var pressed = human.GetIntent();
            heldLight |= pressed.LightAttack;
            heldHeavy |= pressed.HeavyAttack;
            tappedRoll |= pressed.Roll;
        }

        void ClearHeld() => heldLight = heldHeavy = tappedRoll = false;

        // Once per physics step: how the boss looks right now (for later) and how fast the player is moving.
        void Sample()
        {
            float now = Time.fixedTime;
            if (boss == null || sightings.Has(now)) return;
            var bossPosition = boss.transform.position;
            var here = transform.position;
            float step = now - lastSampleTime;
            var bossVelocity = sampledBefore && step > 0f ? (bossPosition - lastBossPosition) / step : Vector3.zero;
            ownVelocity = sampledBefore && step > 0f ? (here - lastOwnPosition) / step : Vector3.zero;

            var sighting = new BossSighting
            {
                Position = bossPosition,
                Forward = boss.transform.forward,
                Velocity = bossVelocity,
                Health = bossHealth != null ? bossHealth.Normalized : 0f,
                State = boss.State,
                Attack = boss.CurrentAttack,
                Phase = boss.Phase,
                PhaseProgress = Progress(boss.CurrentAttackData, boss.Phase, boss.TimeInPhase),
                Stun = Mathf.Clamp01(boss.StunRemaining / maxStun),
            };
            float nearest = float.MaxValue;
            foreach (var shot in boss.Projectiles)
            {
                if (shot == null) continue;
                var gap = here - shot.transform.position;
                gap.y = 0f;
                if (gap.magnitude >= nearest) continue;
                nearest = gap.magnitude;
                sighting.ShotInFlight = true;
                sighting.ShotPosition = shot.transform.position;
                sighting.ShotDirection = shot.Direction;
            }
            sightings.Record(now, sighting);

            lastBossPosition = bossPosition;
            lastOwnPosition = here;
            lastSampleTime = now;
            sampledBefore = true;
        }

        void OnFightEnded(GameObject bossObject, GameObject player, bool bossWon, float duration)
        {
            bool bossDown = bossHealth != null && bossHealth.IsDead;
            bool won = !bossWon && bossDown;
            bool timedOut = !bossWon && !bossDown;
            float roundUsed = arena != null ? Mathf.Clamp01(1f - arena.TimeRemaining / roundLength) : 0f;
            if (won) AddReward(winReward * (1f - winTimeDecay * roundUsed));
            else if (timedOut) AddReward(-drawPenalty);
            else AddReward(-lossPenalty);
            outcome = won ? "Player won" : timedOut ? "Draw, time ran out" : "Player lost";

            var stats = Academy.Instance.StatsRecorder;
            stats.Add("Player/WinRate", won ? 1f : 0f);
            stats.Add("Player/DrawRate", timedOut ? 1f : 0f);
            stats.Add("Player/FightLength", Time.time - fightStartedAt);
            fightOver = true;
        }

        // Only what comes off a health bar counts, so the overkill on a killing blow earns nothing extra.
        void OnDamaged(DamageInfo hit)
        {
            float lost = healthSeen - health.Current;
            healthSeen = health.Current;
            if (lost > 0f) AddReward(-damageTakenPenalty * lost / health.Max);
        }

        void OnBossDamaged(DamageInfo hit)
        {
            float lost = bossHealthSeen - bossHealth.Current;
            bossHealthSeen = bossHealth.Current;
            if (lost > 0f && hit.Source == gameObject) AddReward(damageDealtReward * lost / bossHealth.Max);
        }

        // A direction on the floor in the boss's frame: x to the right of the line to the boss, y toward it.
        Vector2 InFrame(Vector3 v) => new Vector2(Vector3.Dot(v, decisionRight), Vector3.Dot(v, decisionForward));

        static float Progress(AttackData attack, AttackPhase phase, float timeInPhase)
        {
            if (attack == null) return 0f;
            float length = phase == AttackPhase.Windup ? attack.WindupSeconds
                : phase == AttackPhase.Active ? attack.ActiveSeconds
                : phase == AttackPhase.Recovery ? attack.RecoverySeconds
                : 0f;
            return length > 0f ? Mathf.Clamp01(timeInPhase / length) : 0f;
        }
    }
}
