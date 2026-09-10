namespace BossFight.Core
{
    /// <summary>
    /// The boss's move list, as agreed on the T4 ticket.
    /// The RL agent's action space is the size of this enum, so change it here and nowhere else.
    /// Locomotion moves are held for one decision tick. Attacks run until their recovery ends.
    /// </summary>
    public enum BossMove
    {
        None = 0,

        // Locomotion, relative to the player.
        Advance,
        Retreat,
        StrafeLeft,
        StrafeRight,

        // Attacks. Each has a visible windup.

        /// <summary>Short and fast. Punishes players who stay in and keep swinging.</summary>
        QuickAttack,

        /// <summary>Long windup, big hit in front. Punishes rolling too early.</summary>
        HeavySlam,

        /// <summary>
        /// Longest windup, huge reach and damage. Leaves the boss stunned and taking extra damage afterwards.
        /// Punishes players too scared to move in.
        /// </summary>
        SuperAttack,

        /// <summary>A projectile straight ahead. Punishes standing far away without strafing.</summary>
        RangedShot,

        /// <summary>Big burst around the boss, the "get off me" move. Punishes dodging too often.</summary>
        AoeBurst,
    }
}
