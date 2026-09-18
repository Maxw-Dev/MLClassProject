using BossFight.Combat;
using BossFight.Core;
using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>
    /// The ranged attack. Flies straight from the muzzle, hits once through its <see cref="Hitbox"/> (on the BossHitbox
    /// layer), and disappears on the first hit or at the end of its range. <see cref="BossBody"/> spawns it when the
    /// ranged move enters Active.
    /// </summary>
    [RequireComponent(typeof(Hitbox))]
    public class BossProjectile : MonoBehaviour
    {
        Hitbox hitbox;
        Vector3 direction;
        float speed;
        float rangeLeft;
        bool launched;

        void Awake() => hitbox = GetComponent<Hitbox>();

        public void Launch(BossMoveData move, GameObject attacker, Vector3 dir, float metersPerSecond, float range)
        {
            direction = dir.normalized;
            speed = metersPerSecond;
            rangeLeft = range;
            hitbox.Offset = Vector3.zero;
            hitbox.Radius = move.HitRadius;
            hitbox.Hit += OnHit;
            hitbox.Arm(move, attacker);
            launched = true;
        }

        void FixedUpdate()
        {
            if (!launched) return;
            float step = speed * Time.fixedDeltaTime;
            transform.position += direction * step;
            rangeLeft -= step;
            if (rangeLeft <= 0f) Expire();
        }

        void OnHit(IDamageable victim, DamageInfo info) => Expire();

        void Expire()
        {
            if (!launched) return;
            launched = false;
            hitbox.Hit -= OnHit;
            hitbox.Disarm();
            Destroy(gameObject);
        }
    }
}
