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
    /// </summary>
    public class BossIntentDriver : MonoBehaviour
    {
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

            body.TryPerform(LocomotionFor(intent.Move));
            if (intent.LightAttack) body.TryPerform(BossMove.QuickAttack);
            if (intent.HeavyAttack) body.TryPerform(BossMove.HeavySlam);
            if ((intent.Debug & 1) != 0) body.TryPerform(BossMove.SuperAttack);
            if ((intent.Debug & 2) != 0) body.TryPerform(BossMove.RangedShot);
            if ((intent.Debug & 4) != 0) body.TryPerform(BossMove.AoeBurst);
            if ((intent.Debug & 8) != 0 && dummyRunner != null && dummyAttack != null) dummyRunner.TryStart(dummyAttack);
        }

        BossMove LocomotionFor(Vector3 move)
        {
            if (move.sqrMagnitude < 0.01f || body.Target == null) return BossMove.None;
            var toTarget = body.Target.position - body.transform.position;
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
