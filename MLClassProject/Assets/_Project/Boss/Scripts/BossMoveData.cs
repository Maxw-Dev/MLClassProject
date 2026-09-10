using BossFight.Core;
using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>
    /// Everything about one boss attack: the timing and damage (it is an <see cref="AttackData"/>, which Combat runs),
    /// plus the cooldown, the hit shape, the stun it leaves the boss in, and the projectile if it fires one.
    /// One asset per attack in <c>Boss/Data</c>. Tune here, not in code.
    /// </summary>
    [CreateAssetMenu(menuName = "BossFight/Boss Move", fileName = "BossMove_")]
    public class BossMoveData : AttackData
    {
        [Header("Boss")]
        [Tooltip("Which BossMove this asset is for. One asset per attack move.")]
        public BossMove Move = BossMove.QuickAttack;

        [Tooltip("Seconds after this move ends (stun included) before it can start again.")]
        [Min(0f)] public float CooldownSeconds = 1f;

        [Header("Hit shape (melee and AoE)")]
        [Tooltip("Sphere center in the boss's local space. Forward is +Z.")]
        public Vector3 HitOffset = new Vector3(0f, 1f, 1.5f);
        [Min(0.01f)] public float HitRadius = 1f;

        [Header("Stun afterwards")]
        [Tooltip("Seconds the boss stands helpless after this move. 0 for none.")]
        [Min(0f)] public float StunSeconds = 0f;
        [Tooltip("Damage taken is multiplied by this while stunned.")]
        [Min(1f)] public float StunDamageMultiplier = 2f;

        [Header("Projectile (ranged only)")]
        [Tooltip("When set, the move fires this from the muzzle at the start of Active instead of arming the melee hitbox.")]
        public BossProjectile ProjectilePrefab;
        [Min(0.1f)] public float ProjectileSpeed = 12f;
        [Min(0.1f)] public float ProjectileRange = 20f;

        public bool FiresProjectile => ProjectilePrefab != null;
    }
}
