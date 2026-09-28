using System;
using System.Linq;
using BossFight.Core;
using NUnit.Framework;
using UnityEngine;

namespace BossFight.Boss.Tests
{
    /// <summary>The hand-play key mapping shared by the Boss sandbox and the agent's heuristic.</summary>
    public class BossIntentMappingTests
    {
        static readonly Vector3 Boss = Vector3.zero;
        static readonly Vector3 Target = new Vector3(0f, 0f, 5f);   // straight ahead along +Z

        [Test]
        public void StickTowardTheTargetAdvancesAndAwayRetreats()
        {
            Assert.AreEqual(BossMove.Advance, BossIntentDriver.LocomotionFor(Vector3.forward, Boss, Target));
            Assert.AreEqual(BossMove.Retreat, BossIntentDriver.LocomotionFor(Vector3.back, Boss, Target));
        }

        [Test]
        public void StickSidewaysStrafesToThatSide()
        {
            Assert.AreEqual(BossMove.StrafeRight, BossIntentDriver.LocomotionFor(Vector3.right, Boss, Target));
            Assert.AreEqual(BossMove.StrafeLeft, BossIntentDriver.LocomotionFor(Vector3.left, Boss, Target));
        }

        [Test]
        public void AnExactDiagonalCountsAsAdvance()
        {
            Assert.AreEqual(BossMove.Advance, BossIntentDriver.LocomotionFor(new Vector3(1f, 0f, 1f).normalized, Boss, Target));
        }

        [Test]
        public void NoStickOrNoDistanceIsNone()
        {
            Assert.AreEqual(BossMove.None, BossIntentDriver.LocomotionFor(Vector3.zero, Boss, Target));
            Assert.AreEqual(BossMove.None, BossIntentDriver.LocomotionFor(Vector3.forward, Boss, Boss));
        }

        [Test]
        public void EachAttackKeyRequestsItsOwnMoveOnly()
        {
            AssertOnly(new Intent { LightAttack = true }, BossMove.QuickAttack);
            AssertOnly(new Intent { HeavyAttack = true }, BossMove.HeavySlam);
            AssertOnly(new Intent { Debug = 1 }, BossMove.SuperAttack);
            AssertOnly(new Intent { Debug = 2 }, BossMove.RangedShot);
            AssertOnly(new Intent { Debug = 4 }, BossMove.AoeBurst);
        }

        [Test]
        public void NoKeysAndDebugKeyFourRequestNoAttack()
        {
            foreach (var attack in BossIntentDriver.Attacks)
            {
                Assert.IsFalse(BossIntentDriver.Requests(Intent.None, attack), $"{attack}");
                Assert.IsFalse(BossIntentDriver.Requests(new Intent { Debug = 8 }, attack), $"{attack}");
            }
        }

        [Test]
        public void EveryAttackHasAKey()
        {
            var attacks = ((BossMove[])Enum.GetValues(typeof(BossMove))).Where(BossMoveSet.IsAttack);
            CollectionAssert.AreEquivalent(attacks, BossIntentDriver.Attacks);
        }

        static void AssertOnly(Intent intent, BossMove expected)
        {
            foreach (var attack in BossIntentDriver.Attacks)
                Assert.AreEqual(attack == expected, BossIntentDriver.Requests(intent, attack), $"{attack}");
        }
    }
}
