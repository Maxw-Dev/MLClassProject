using System;
using System.Collections;
using System.Collections.Generic;
using BossFight.Combat;
using BossFight.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BossFight.Boss.Tests
{
    /// <summary>
    /// A boss built in code hits a dummy on the Player layer with each attack, at time scale 1 and 20.
    /// Covers body → runner → hitbox or projectile → hurtbox → health, and the stun after the super.
    /// </summary>
    public class BossBodyTests
    {
        const float DummyHealth = 100f;

        readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            foreach (var o in cleanup) if (o != null) UnityEngine.Object.Destroy(o);
            cleanup.Clear();
        }

        BossMoveData Move(BossMove move, float windup, float active, float recovery, float damage, Vector3 offset, float radius,
            float cooldown, float stun = 0f, BossProjectile projectile = null)
        {
            var d = ScriptableObject.CreateInstance<BossMoveData>();
            d.name = move.ToString();
            d.DisplayName = move.ToString();
            d.Move = move;
            d.WindupSeconds = windup; d.ActiveSeconds = active; d.RecoverySeconds = recovery;
            d.Damage = damage; d.StaminaCost = 0f;
            d.HitOffset = offset; d.HitRadius = radius;
            d.CooldownSeconds = cooldown; d.StunSeconds = stun; d.StunDamageMultiplier = 2f;
            d.ProjectilePrefab = projectile; d.ProjectileSpeed = 12f; d.ProjectileRange = 20f;
            cleanup.Add(d);
            return d;
        }

        BossProjectile ProjectileTemplate()
        {
            var go = new GameObject("ProjectileTemplate");
            go.SetActive(false);
            go.layer = LayerMask.NameToLayer("BossHitbox");
            go.AddComponent<Hitbox>();
            cleanup.Add(go);
            return go.AddComponent<BossProjectile>();
        }

        GameObject Ground()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "Ground";
            go.transform.localScale = Vector3.one * 5f;
            cleanup.Add(go);
            return go;
        }

        GameObject Dummy(Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Dummy";
            go.layer = LayerMask.NameToLayer("Player");
            go.transform.position = position;
            go.AddComponent<Health>();
            go.AddComponent<Hurtbox>();
            cleanup.Add(go);
            return go;
        }

        BossBody Boss(Vector3 position, Transform target, params BossMoveData[] moves)
        {
            var go = new GameObject("Boss");
            go.layer = LayerMask.NameToLayer("Boss");
            var facing = target.position - position; facing.y = 0f;
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing));
            var controller = go.AddComponent<CharacterController>();
            controller.height = 2.4f; controller.radius = 0.6f; controller.center = new Vector3(0f, 1.2f, 0f);
            go.AddComponent<Health>();
            go.AddComponent<AttackRunner>();

            var hb = new GameObject("MeleeHitbox");
            hb.transform.SetParent(go.transform, false);
            hb.layer = LayerMask.NameToLayer("BossHitbox");
            var hitbox = hb.AddComponent<Hitbox>();

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(go.transform, false);
            muzzle.localPosition = new Vector3(0f, 1.2f, 1f);

            var body = go.AddComponent<BossBody>();
            body.Configure(moves, hitbox, muzzle, target);
            cleanup.Add(go);
            return body;
        }

        BossMoveData[] TheFiveMoves(BossProjectile projectile) => new[]
        {
            Move(BossMove.QuickAttack, 0.3f, 0.1f, 0.4f, 8f, new Vector3(0f, 1f, 1.2f), 0.8f, 0.5f),
            Move(BossMove.HeavySlam, 1.1f, 0.1f, 0.9f, 25f, new Vector3(0f, 1f, 2f), 2f, 3f),
            Move(BossMove.SuperAttack, 2f, 0.2f, 0.6f, 45f, new Vector3(0f, 1f, 3.5f), 4f, 12f, stun: 3f),
            Move(BossMove.RangedShot, 0.6f, 0.1f, 0.5f, 12f, Vector3.zero, 0.5f, 4f, projectile: projectile),
            Move(BossMove.AoeBurst, 0.7f, 0.1f, 1f, 15f, new Vector3(0f, 1f, 0f), 4f, 8f),
        };

        IEnumerator AttackLandsOnce(BossMove move, float timeScale)
        {
            Assert.IsTrue(LayerMask.NameToLayer("Player") >= 0 && LayerMask.NameToLayer("Boss") >= 0 && LayerMask.NameToLayer("BossHitbox") >= 0, "project layers missing");
            Ground();
            var dummy = Dummy(new Vector3(0f, 1f, 2f));
            var boss = Boss(Vector3.zero, dummy.transform, TheFiveMoves(ProjectileTemplate()));

            int hits = 0;
            float dealt = 0f;
            Action<GameObject, GameObject, DamageInfo> onHit = (attacker, victim, info) =>
            {
                if (victim != dummy) return;
                hits++;
                dealt += info.Amount;
                Assert.AreEqual(boss.gameObject, attacker, "hits should be credited to the boss root");
            };
            FightEvents.OnHit += onHit;
            Time.timeScale = timeScale;
            try
            {
                yield return new WaitForFixedUpdate();
                Assert.IsTrue(boss.CanPerform(move), $"{move} should be available");
                Assert.IsTrue(boss.TryPerform(move), $"{move} should start");
                Assert.AreEqual(BossState.Attacking, boss.State);

                var data = boss.DataFor(move);
                yield return new WaitForSeconds(data.TotalSeconds + 1f);   // scaled time

                Assert.AreEqual(1, hits, $"{move} at {timeScale}x should land exactly once");
                Assert.AreEqual(data.Damage, dealt, 0.001f, $"{move} damage");
                Assert.AreEqual(DummyHealth - data.Damage, dummy.GetComponent<Health>().Current, 0.001f);
            }
            finally
            {
                Time.timeScale = 1f;
                FightEvents.OnHit -= onHit;
            }
        }

        [UnityTest]
        public IEnumerator EveryAttackLandsExactlyOnceAtTimeScale1()
        {
            foreach (BossMove move in new[] { BossMove.QuickAttack, BossMove.HeavySlam, BossMove.SuperAttack, BossMove.RangedShot, BossMove.AoeBurst })
            {
                yield return AttackLandsOnce(move, 1f);
                TearDown();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator EveryAttackLandsExactlyOnceAtTimeScale20()
        {
            foreach (BossMove move in new[] { BossMove.QuickAttack, BossMove.HeavySlam, BossMove.SuperAttack, BossMove.RangedShot, BossMove.AoeBurst })
            {
                yield return AttackLandsOnce(move, 20f);
                TearDown();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator SuperLeavesTheBossStunnedAndTakingDoubleDamageThenRecovers()
        {
            Ground();
            var dummy = Dummy(new Vector3(0f, 1f, 2f));
            var super = Move(BossMove.SuperAttack, 0.2f, 0.1f, 0.1f, 45f, new Vector3(0f, 1f, 3.5f), 4f, 12f, stun: 1f);
            var boss = Boss(Vector3.zero, dummy.transform, super);
            var health = boss.GetComponent<Health>();

            yield return new WaitForFixedUpdate();
            Assert.IsTrue(boss.TryPerform(BossMove.SuperAttack));
            yield return new WaitForSeconds(super.TotalSeconds + 0.1f);

            Assert.AreEqual(BossState.Stunned, boss.State);
            Assert.IsFalse(boss.CanPerform(BossMove.Advance), "no moving while stunned");
            Assert.IsTrue(boss.CanPerform(BossMove.None));
            health.TakeDamage(new DamageInfo(10f, dummy));
            Assert.AreEqual(health.Max - 20f, health.Current, 0.001f, "double damage while stunned");

            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(BossState.Idle, boss.State);
            Assert.IsTrue(boss.CanPerform(BossMove.Advance));
            Assert.IsFalse(boss.CanPerform(BossMove.SuperAttack), "super is on cooldown after use");
            health.TakeDamage(new DamageInfo(10f, dummy));
            Assert.AreEqual(health.Max - 30f, health.Current, 0.001f, "normal damage again");
        }

        [UnityTest]
        public IEnumerator AdvanceClosesInAndStopsShortOfTheTarget()
        {
            Ground();
            var dummy = Dummy(new Vector3(0f, 1f, 8f));
            var boss = Boss(Vector3.zero, dummy.transform);

            yield return new WaitForFixedUpdate();
            Assert.IsTrue(boss.TryPerform(BossMove.Advance));
            Assert.AreEqual(BossMove.Advance, boss.Locomotion);
            yield return new WaitForSeconds(4f);

            float distance = Vector3.Distance(boss.transform.position, dummy.transform.position);
            Assert.Less(distance, 3f, "should have closed most of the gap");
            Assert.Greater(distance, 1.5f, "should stop short of the target");
        }
    }
}
