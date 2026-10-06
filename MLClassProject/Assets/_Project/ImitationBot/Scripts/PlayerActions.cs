using BossFight.Core;
using UnityEngine;

namespace BossFight.ImitationBot
{
    /// <summary>
    /// The player agent's actions, and how they turn into an <see cref="Intent"/> and back. Three discrete branches:
    /// where to walk (stand still or one of 8 directions relative to the boss, always at full speed, like a keyboard),
    /// the attack button held until the next decision (none, light, heavy), and a roll tap.
    /// Movement is a choice rather than two numbers on purpose: from recordings, two numbers learn the average of what
    /// people did, and the average of "walk in" and "back off" is "barely move".
    /// </summary>
    public struct PlayerActions
    {
        public const int ContinuousCount = 0;
        public static readonly int[] BranchSizes = { 9, 3, 2 };

        /// <summary>Clockwise from straight at the boss, 45 degrees apart. Right is to the right of the line to the boss.</summary>
        public enum Move { None, Toward, TowardRight, Right, AwayRight, Away, AwayLeft, Left, TowardLeft }
        public enum Attack { None, Light, Heavy }

        /// <summary>A person's stick or keys count as walking above this much.</summary>
        public const float DeadZone = 0.25f;

        public Move Walk;
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

        /// <summary>The nearest of the 8 directions to a person's movement, or None below the dead zone.</summary>
        public static Move NearestMove(Vector3 move, Vector3 forward, Vector3 right)
        {
            float toward = Vector3.Dot(move, forward), side = Vector3.Dot(move, right);
            if (new Vector2(toward, side).magnitude < DeadZone) return Move.None;
            float degrees = Mathf.Atan2(side, toward) * Mathf.Rad2Deg;              // 0 toward, 90 right
            int sector = ((Mathf.RoundToInt(degrees / 45f) % 8) + 8) % 8;
            return (Move)(1 + sector);
        }

        /// <summary>The direction on the floor for a move, length 1 (zero for None).</summary>
        public static Vector3 Direction(Move walk, Vector3 forward, Vector3 right)
        {
            if (walk == Move.None) return Vector3.zero;
            float radians = ((int)walk - 1) * 45f * Mathf.Deg2Rad;
            return (forward * Mathf.Cos(radians) + right * Mathf.Sin(radians)).normalized;
        }

        /// <summary>What a person's intent looks like as actions, for recording demonstrations.</summary>
        public static PlayerActions FromIntent(Intent intent, Vector3 forward, Vector3 right)
        {
            var move = intent.Move;
            move.y = 0f;
            return new PlayerActions
            {
                Walk = NearestMove(move, forward, right),
                // PlayerBody handles a light press before a heavy one, so a light press wins here too
                Button = intent.LightAttack ? Attack.Light : intent.HeavyAttack ? Attack.Heavy : Attack.None,
                Roll = intent.Roll,
            };
        }

        /// <summary>The intent these actions ask for.</summary>
        public Intent ToIntent(Vector3 forward, Vector3 right) => new Intent
        {
            Move = Direction(Walk, forward, right),
            LightAttack = Button == Attack.Light,
            HeavyAttack = Button == Attack.Heavy,
            Roll = Roll,
        };
    }
}
