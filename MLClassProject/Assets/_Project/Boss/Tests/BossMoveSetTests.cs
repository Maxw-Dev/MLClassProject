using BossFight.Core;
using NUnit.Framework;

namespace BossFight.Boss.Tests
{
    public class BossMoveSetTests
    {
        static BossMoveSet Set() => new BossMoveSet(new[]
        {
            (BossMove.QuickAttack, 0.5f),
            (BossMove.HeavySlam, 3f),
            (BossMove.SuperAttack, 12f),
        });

        [Test]
        public void StartsIdleAndAllowsLocomotionAndKnownAttacks()
        {
            var set = Set();
            Assert.AreEqual(BossState.Idle, set.State);
            Assert.IsTrue(set.CanPerform(BossMove.None));
            Assert.IsTrue(set.CanPerform(BossMove.Advance));
            Assert.IsTrue(set.CanPerform(BossMove.StrafeLeft));
            Assert.IsTrue(set.CanPerform(BossMove.QuickAttack));
        }

        [Test]
        public void AttacksWithoutDataAreNeverAllowed()
        {
            var set = Set();
            Assert.IsFalse(set.CanPerform(BossMove.RangedShot));
            Assert.IsFalse(set.CanPerform(BossMove.AoeBurst));
        }

        [Test]
        public void AnAttackInProgressBlocksEverythingButNone()
        {
            var set = Set();
            set.BeginAttack(BossMove.QuickAttack);
            Assert.AreEqual(BossState.Attacking, set.State);
            Assert.AreEqual(BossMove.QuickAttack, set.CurrentAttack);
            Assert.IsTrue(set.CanPerform(BossMove.None));
            Assert.IsFalse(set.CanPerform(BossMove.Advance));
            Assert.IsFalse(set.CanPerform(BossMove.HeavySlam));
            Assert.IsFalse(set.CanPerform(BossMove.QuickAttack));
        }

        [Test]
        public void CooldownCountsFromTheEndOfTheMove()
        {
            var set = Set();
            set.BeginAttack(BossMove.HeavySlam);
            set.Tick(5f);                       // however long the attack took
            set.EndAttack(0f);

            Assert.AreEqual(BossState.Idle, set.State);
            Assert.IsFalse(set.CanPerform(BossMove.HeavySlam));
            Assert.AreEqual(3f, set.CooldownRemaining(BossMove.HeavySlam), 0.0001f);
            set.Tick(2.99f);
            Assert.IsFalse(set.CanPerform(BossMove.HeavySlam));
            set.Tick(0.02f);
            Assert.IsTrue(set.CanPerform(BossMove.HeavySlam));
            Assert.AreEqual(0f, set.CooldownRemaining(BossMove.HeavySlam));
        }

        [Test]
        public void CooldownsArePerMove()
        {
            var set = Set();
            set.BeginAttack(BossMove.QuickAttack);
            set.EndAttack(0f);
            Assert.IsFalse(set.CanPerform(BossMove.QuickAttack));
            Assert.IsTrue(set.CanPerform(BossMove.HeavySlam));
        }

        [Test]
        public void StunBlocksMovesUntilItRunsOut()
        {
            var set = Set();
            set.BeginAttack(BossMove.SuperAttack);
            set.EndAttack(3f);

            Assert.AreEqual(BossState.Stunned, set.State);
            Assert.AreEqual(3f, set.StunRemaining);
            Assert.IsTrue(set.CanPerform(BossMove.None));
            Assert.IsFalse(set.CanPerform(BossMove.Advance));
            Assert.IsFalse(set.CanPerform(BossMove.QuickAttack));

            set.Tick(2.5f);
            Assert.AreEqual(BossState.Stunned, set.State);
            set.Tick(0.5f);
            Assert.AreEqual(BossState.Idle, set.State);
            Assert.AreEqual(0f, set.StunRemaining);
            Assert.IsTrue(set.CanPerform(BossMove.Advance));
        }

        [Test]
        public void CancelAttackReturnsToIdleWithoutACooldown()
        {
            var set = Set();
            set.BeginAttack(BossMove.HeavySlam);
            set.CancelAttack();
            Assert.AreEqual(BossState.Idle, set.State);
            Assert.IsTrue(set.CanPerform(BossMove.HeavySlam));
        }

        [Test]
        public void DeadAllowsNothing()
        {
            var set = Set();
            set.Kill();
            Assert.AreEqual(BossState.Dead, set.State);
            Assert.IsFalse(set.CanPerform(BossMove.None));
            Assert.IsFalse(set.CanPerform(BossMove.Advance));
            Assert.IsFalse(set.CanPerform(BossMove.QuickAttack));
        }

        [Test]
        public void ResetClearsStateAndCooldowns()
        {
            var set = Set();
            set.BeginAttack(BossMove.SuperAttack);
            set.EndAttack(3f);
            set.Reset();
            Assert.AreEqual(BossState.Idle, set.State);
            Assert.AreEqual(0f, set.CooldownRemaining(BossMove.SuperAttack));
            Assert.IsTrue(set.CanPerform(BossMove.SuperAttack));
        }
    }
}
