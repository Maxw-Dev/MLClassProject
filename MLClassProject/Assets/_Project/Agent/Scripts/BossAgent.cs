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
    /// episode ends, and the next episode starts a new fight, so the arena's <see cref="FightManager"/> must have
    /// Auto Restart off. Lives on <c>Agent/Prefabs/BossAgent.prefab</c> next to Behavior Parameters and a Decision
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
            + 7                   // where the opponent is
            + (5 + PhaseCount)    // what the opponent is doing
            + 3;                  // the arena

        [Header("Arena")]
        [Tooltip("This arena's FightManager, with Auto Restart off: the agent starts every fight.")]
        [SerializeField] FightManager arena;
        [Tooltip("The other fighter in this arena. Needs a Health on its root.")]
        [SerializeField] GameObject opponent;
        [Tooltip("The middle of the arena floor.")]
        [SerializeField] Transform arenaCenter;
        [Tooltip("Meters from the middle of the arena to a wall. Positions and distances are scaled by it.")]
        [SerializeField, Min(1f)] float arenaHalfSize = 19f;

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
        float maxStun = 0.01f;
        float roundLength = 1f;
        float fightStartedAt;
        Vector3 lastOpponentPosition;
        float lastObservedAt;
        BossMove heldAttack;
        bool fightOver;
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
            bossHealthSeen = health.Current;
            opponentHealthSeen = opponentHealth != null ? opponentHealth.Current : 0f;
            foreach (var move in body.Moves)
                if (move != null) maxStun = Mathf.Max(maxStun, move.WindupHitStunSeconds);

            if (arena == null || opponentHealth == null)
                Debug.LogError($"{name}: set Arena and an Opponent with a Health on this BossAgent (it belongs in an arena).", this);
            if (heuristicInput != null && keyboard == null)
                Debug.LogWarning($"{name}: Heuristic Input is not an IIntentSource, hand play will stand still.", this);
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
        }

        protected override void OnDisable()
        {
            if (arena != null) arena.FightEnded -= OnFightEnded;
            if (health != null) health.Damaged -= OnDamaged;
            if (opponentHealth != null) opponentHealth.Damaged -= OnOpponentDamaged;
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

            fightOver = false;
            heldAttack = BossMove.None;
            if (arena != null)
            {
                // The arena starts its first fight by itself in Start. Every fight after that starts here.
                if (!firstEpisode || !arena.IsFightActive) arena.StartNewFight();
                roundLength = Mathf.Max(arena.TimeRemaining, 0.01f);
            }
            firstEpisode = false;
            // The arena refills health and moves the fighters. A player that died last fight also needs its own reset
            // to come back to life, which FightManager does not do yet.
            if (opponentBody != null) opponentBody.Reset();
            body.ResetForEpisode();   // after the arena's reset, which starts a cooldown when it cuts a swing short
            if (opponent != null) body.Target = opponent.transform;
            Physics.SyncTransforms();   // a CharacterController ignores a teleport until physics has seen it
            fightStartedAt = Time.time;
            lastObservedAt = Time.time;
            lastOpponentPosition = opponent != null ? opponent.transform.position : transform.position;
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
                sensor.AddObservation(data != null && data.CooldownSeconds > 0f ? body.CooldownRemaining(attack) / data.CooldownSeconds : 0f);
            }
            sensor.AddObservation(Mathf.Clamp01(body.StunRemaining / maxStun));
            sensor.AddOneHotObservation(Array.IndexOf(LocomotionMoves, body.Locomotion) + 1, LocomotionMoves.Length + 1);

            // Where the opponent is, in the boss's frame
            var here = transform.position;
            var there = opponent.transform.position;
            var toOpponent = there - here;
            toOpponent.y = 0f;
            float distance = toOpponent.magnitude;
            var towardOpponent = distance > 0.001f ? toOpponent / distance : transform.forward;
            sensor.AddObservation(Mathf.Clamp01(distance / (2f * arenaHalfSize)));
            sensor.AddObservation(Flat(transform.InverseTransformDirection(towardOpponent)));
            sensor.AddObservation(Flat(transform.InverseTransformDirection(opponent.transform.forward)));
            float elapsed = Time.time - lastObservedAt;
            var velocity = elapsed > 0.0001f ? (there - lastOpponentPosition) / elapsed : Vector3.zero;
            lastOpponentPosition = there;
            lastObservedAt = Time.time;
            sensor.AddObservation(Vector2.ClampMagnitude(Flat(transform.InverseTransformDirection(velocity)) / MaxOpponentSpeed, 1f));

            // What the opponent is doing
            sensor.AddObservation(opponentHealth.Normalized);
            sensor.AddObservation(opponentStamina != null ? opponentStamina.Normalized : 0f);
            var phase = opponentRunner != null ? opponentRunner.Phase : AttackPhase.Idle;
            var swing = opponentRunner != null ? opponentRunner.CurrentAttack : null;
            sensor.AddOneHotObservation((int)phase, PhaseCount);
            sensor.AddObservation(Progress(swing, phase, opponentRunner != null ? opponentRunner.TimeInPhase : 0f));
            sensor.AddObservation(swing != null ? Mathf.Clamp01(swing.Damage / MaxAttackDamage) : 0f);
            sensor.AddObservation(opponentHealth.IsInvulnerable);

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
            for (int i = 1; i < AllMoves.Length; i++)
                actionMask.SetActionEnabled(0, i, body.CanPerform(AllMoves[i]));
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            int choice = actions.DiscreteActions[0];
            LastMove = choice >= 0 && choice < AllMoves.Length ? AllMoves[choice] : BossMove.None;
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
                EndEpisode();   // OnEpisodeBegin starts the next fight
                return;
            }
            if (arena != null && arena.IsFightActive) AddReward(-perSecond * Time.fixedDeltaTime);
            // Remember an attack key between decisions, so a quick tap is not lost in the 0.1 s gap.
            if (keyboard != null && heldAttack == BossMove.None) heldAttack = RequestedAttack(keyboard.GetIntent());
        }

        void OnFightEnded(GameObject winner)
        {
            bool bossWon = winner == gameObject;
            bool timedOut = winner == null;
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
