using BossFight.Combat;
using BossFight.Core;
using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>
    /// Sandbox only: drives the boss from an <see cref="IIntentSource"/> on the same object, so a human can play the boss
    /// through T7's UserInput. Stick toward the player is Advance, away is Retreat, sideways is a strafe.
    /// Light attack is Quick, heavy is Slam, debug keys 1, 2, 3 are Super, Ranged, AoE. Held keys repeat as soon as allowed.
    /// Debug key 4 makes the sandbox dummy swing, to test what a hit during the boss's windup does.
    /// The key mapping is public and static so the agent's hand-play heuristic (T6) uses the same keys.
    /// </summary>
    public class BossIntentDriver : MonoBehaviour
    {
        /// <summary>Every attack, in the order they are tried when several keys are held.</summary>
        public static readonly BossMove[] Attacks =
            { BossMove.QuickAttack, BossMove.HeavySlam, BossMove.SuperAttack, BossMove.RangedShot, BossMove.AoeBurst };

        [SerializeField] BossBody body;

        [Header("Sandbox dummy (key 4 makes it swing)")]
        [SerializeField] AttackRunner dummyRunner;
        [SerializeField] AttackData dummyAttack;

        IIntentSource source;

        void Awake()
        {
            if (body == null) body = GetComponent<BossBody>();
            source = GetComponent<IIntentSource>();
            if (source == null) Debug.LogWarning($"{name}: no IIntentSource on this object, nothing will drive the boss.", this);
        }

        void Update()
        {
            if (source == null || body == null) return;
            var intent = source.GetIntent();

            var target = body.Target;
            body.TryPerform(target != null ? LocomotionFor(intent.Move, body.transform.position, target.position) : BossMove.None);
            foreach (var attack in Attacks)
                if (Requests(intent, attack)) body.TryPerform(attack);
            if ((intent.Debug & 8) != 0 && dummyRunner != null && dummyAttack != null) dummyRunner.TryStart(dummyAttack);
        }

        /// <summary>Is this attack's key held? Light is Quick, heavy is Slam, debug 1, 2, 3 are Super, Ranged, AoE.</summary>
        public static bool Requests(Intent intent, BossMove attack)
        {
            switch (attack)
            {
                case BossMove.QuickAttack: return intent.LightAttack;
                case BossMove.HeavySlam: return intent.HeavyAttack;
                case BossMove.SuperAttack: return (intent.Debug & 1) != 0;
                case BossMove.RangedShot: return (intent.Debug & 2) != 0;
                case BossMove.AoeBurst: return (intent.Debug & 4) != 0;
                default: return false;
            }
        }

        /// <summary>A world-space stick direction as locomotion relative to the target: toward is Advance, away is Retreat, sideways strafes.</summary>
        public static BossMove LocomotionFor(Vector3 move, Vector3 bossPosition, Vector3 targetPosition)
        {
            if (move.sqrMagnitude < 0.01f) return BossMove.None;
            var toTarget = targetPosition - bossPosition;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f) return BossMove.None;
            toTarget.Normalize();
            float along = Vector3.Dot(move, toTarget);
            float across = Vector3.Dot(move, Vector3.Cross(Vector3.up, toTarget));
            if (Mathf.Abs(along) >= Mathf.Abs(across)) return along > 0f ? BossMove.Advance : BossMove.Retreat;
            return across > 0f ? BossMove.StrafeRight : BossMove.StrafeLeft;
        }
    }
}
