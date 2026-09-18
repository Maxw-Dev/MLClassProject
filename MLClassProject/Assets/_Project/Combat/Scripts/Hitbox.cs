using System;
using System.Collections.Generic;
using BossFight.Core;
using UnityEngine;

namespace BossFight.Combat
{
    /// <summary>
    /// A sphere of damage. <see cref="AttackRunner"/> arms it for the attack's active window and disarms it after.
    /// While armed it runs one overlap query per physics step and hits each target at most once per swing.
    /// Needs no collider or rigidbody. Put it on a child of the body (one per weapon or fist, more if a move
    /// needs coverage) on the PlayerHitbox or BossHitbox layer. The layer collision matrix decides what it can hit,
    /// and the mask is read once in Awake, so set the layer on the prefab rather than at spawn time.
    /// </summary>
    public class Hitbox : MonoBehaviour
    {
        [Tooltip("Sphere center in this object's local space.")]
        [SerializeField] Vector3 offset = Vector3.zero;
        [SerializeField, Min(0.01f)] float radius = 0.5f;

        GameObject attacker;
        AttackData attack;
        int layerMask;
        readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();
        readonly Collider[] buffer = new Collider[32];

        public bool IsArmed { get; private set; }

        /// <summary>This hitbox reached someone. They may still have i-framed it; <see cref="Health"/> decides that.</summary>
        public event Action<IDamageable, DamageInfo> Hit;
        public Vector3 Offset { get => offset; set => offset = value; }
        public float Radius { get => radius; set => radius = Mathf.Max(0.01f, value); }

        void Awake() => layerMask = MaskFromCollisionMatrix(gameObject.layer);

        void OnValidate()
        {
            if (!LayerMask.LayerToName(gameObject.layer).EndsWith("Hitbox"))
                Debug.LogWarning($"{name}: Hitbox is on layer '{LayerMask.LayerToName(gameObject.layer)}'. It should be on PlayerHitbox or BossHitbox, or it will hit everything.", this);
        }

        /// <summary>Everything the collision matrix lets this layer touch.</summary>
        static int MaskFromCollisionMatrix(int layer)
        {
            int mask = 0;
            for (int i = 0; i < 32; i++)
                if (!Physics.GetIgnoreLayerCollision(layer, i)) mask |= 1 << i;
            return mask;
        }

        public void Arm(AttackData data, GameObject attackerObject)
        {
            attack = data;
            attacker = attackerObject;
            hitThisSwing.Clear();
            IsArmed = true;
        }

        public void Disarm()
        {
            IsArmed = false;
            attack = null;
        }

        void OnDisable() => Disarm();

        void FixedUpdate()
        {
            if (!IsArmed || attack == null) return;
            int count = Overlap();
            for (int i = 0; i < count && IsArmed; i++) TryHit(buffer[i]);
        }

        int Overlap()
        {
            var center = transform.TransformPoint(offset);
            var s = transform.lossyScale;
            float worldRadius = radius * Mathf.Max(s.x, s.y, s.z);
            return Physics.OverlapSphereNonAlloc(center, worldRadius, buffer, layerMask, QueryTriggerInteraction.Collide);
        }

        void TryHit(Collider other)
        {
            var hurt = other.GetComponentInParent<Hurtbox>();
            if (hurt == null || hurt.Owner == null) return;
            if (attacker != null && hurt.transform.IsChildOf(attacker.transform)) return;   // never hit yourself
            if (!hitThisSwing.Add(hurt.Owner)) return;

            var info = new DamageInfo(attack.Damage, attacker);
            hurt.Owner.TakeDamage(info);
            Hit?.Invoke(hurt.Owner, info);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = IsArmed ? new Color(1f, 0.2f, 0.1f, 0.6f) : new Color(1f, 0.6f, 0.1f, 0.15f);
            var s = transform.lossyScale;
            Gizmos.DrawSphere(transform.TransformPoint(offset), radius * Mathf.Max(s.x, s.y, s.z));
        }
    }
}
