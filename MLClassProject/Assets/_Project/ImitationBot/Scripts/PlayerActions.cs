using BossFight.Core;
using UnityEngine;

namespace BossFight.ImitationBot
{
    /// <summary>
    /// The player agent's actions, and how they turn into an <see cref="Intent"/> and back. Movement is two continuous
    /// values in the boss's direction: toward it, and to the right of the line to it. That way "circle left" means
    /// the same thing wherever the fight is. Then one branch for the attack button held until the next decision
    /// (none, light, heavy) and one for a roll tap.
    /// </summary>
    public struct PlayerActions
    {
        public const int ContinuousCount = 2;
        public static readonly int[] BranchSizes = { 3, 2 };

        public enum Attack { None, Light, Heavy }

        public float Toward;    // -1..1, toward the boss
        public float Right;     // -1..1, to the right of the line to the boss
        public Attack Button;
        public bool Roll;

        /// <summary>The boss's direction on the floor, and the right of it. Falls back to world forward when on top of it.</summary>
        public static void Frame(Vector3 from, Vector3 to, out Vector3 forward, out Vector3 right)
        {
            var flat = to - from;
            flat.y = 0f;
            forward = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.forward;
            right = Vector3.Cross(Vector3.up, forward);
        }

        /// <summary>What a person's intent looks like as actions, for recording demonstrations.</summary>
        public static PlayerActions FromIntent(Intent intent, Vector3 forward, Vector3 right)
        {
            var move = intent.Move;
            move.y = 0f;
            return new PlayerActions
            {
                Toward = Mathf.Clamp(Vector3.Dot(move, forward), -1f, 1f),
                Right = Mathf.Clamp(Vector3.Dot(move, right), -1f, 1f),
                // PlayerBody handles a light press before a heavy one, so a light press wins here too
                Button = intent.LightAttack ? Attack.Light : intent.HeavyAttack ? Attack.Heavy : Attack.None,
                Roll = intent.Roll,
            };
        }

        /// <summary>The intent these actions ask for. Movement longer than 1 is scaled back to 1, like a stick.</summary>
        public Intent ToIntent(Vector3 forward, Vector3 right)
        {
            var move = Vector3.ClampMagnitude(forward * Mathf.Clamp(Toward, -1f, 1f) + right * Mathf.Clamp(Right, -1f, 1f), 1f);
            return new Intent
            {
                Move = move,
                LightAttack = Button == Attack.Light,
                HeavyAttack = Button == Attack.Heavy,
                Roll = Roll,
            };
        }
    }
}
