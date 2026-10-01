using BossFight.Core;
using NUnit.Framework;
using UnityEngine;

namespace BossFight.ImitationBot.Tests
{
    public class PlayerActionsTests
    {
        // Player at the origin, boss due east: forward is +x, and right of that line is -z.
        static void EastFrame(out Vector3 forward, out Vector3 right) =>
            PlayerActions.Frame(Vector3.zero, new Vector3(5f, 1.2f, 0f), out forward, out right);

        static PlayerActions.Move Walking(Vector3 move)
        {
            EastFrame(out var forward, out var right);
            return PlayerActions.FromIntent(new Intent { Move = move }, forward, right).Walk;
        }

        [Test]
        public void TheFrameLooksAtTheBossOnTheFloor()
        {
            EastFrame(out var forward, out var right);
            Assert.AreEqual(1f, forward.x, 0.0001f);
            Assert.AreEqual(0f, forward.y, 0.0001f, "the boss's height does not tilt the frame");
            Assert.AreEqual(-1f, right.z, 0.0001f);
        }

        [Test]
        public void OnTopOfTheBossTheFrameFallsBackToWorldForward()
        {
            PlayerActions.Frame(Vector3.one, Vector3.one, out var forward, out _);
            Assert.AreEqual(1f, forward.z, 0.0001f);
        }

        [Test]
        public void WalkingIsTheNearestOfEightDirectionsAroundTheBoss()
        {
            Assert.AreEqual(PlayerActions.Move.Toward, Walking(Vector3.right));
            Assert.AreEqual(PlayerActions.Move.Away, Walking(Vector3.left));
            Assert.AreEqual(PlayerActions.Move.Right, Walking(Vector3.back), "circling keeps its side wherever the boss is");
            Assert.AreEqual(PlayerActions.Move.Left, Walking(Vector3.forward));
            Assert.AreEqual(PlayerActions.Move.TowardRight, Walking(new Vector3(1f, 0f, -1f).normalized));
            Assert.AreEqual(PlayerActions.Move.AwayLeft, Walking(new Vector3(-1f, 0f, 1f).normalized));
            Assert.AreEqual(PlayerActions.Move.TowardLeft, Walking(new Vector3(0.6f, 0f, 0.8f)), "53 degrees left rounds to 45");
        }

        [Test]
        public void ASmallNudgeIsStandingStill()
        {
            Assert.AreEqual(PlayerActions.Move.None, Walking(Vector3.zero));
            Assert.AreEqual(PlayerActions.Move.None, Walking(new Vector3(0.1f, 0f, 0.1f)));
        }

        [Test]
        public void EveryDirectionPlaysBackAsItself()
        {
            EastFrame(out var forward, out var right);
            for (var walk = PlayerActions.Move.None; walk <= PlayerActions.Move.TowardLeft; walk++)
            {
                var played = new PlayerActions { Walk = walk }.ToIntent(forward, right);
                Assert.AreEqual(walk, PlayerActions.FromIntent(played, forward, right).Walk);
                Assert.AreEqual(walk == PlayerActions.Move.None ? 0f : 1f, played.Move.magnitude, 0.0001f, "always full speed");
            }
        }

        [Test]
        public void ButtonsBecomeBranches()
        {
            EastFrame(out var forward, out var right);
            Assert.AreEqual(PlayerActions.Attack.Heavy, PlayerActions.FromIntent(new Intent { HeavyAttack = true }, forward, right).Button);
            Assert.AreEqual(PlayerActions.Attack.Light, PlayerActions.FromIntent(new Intent { LightAttack = true, HeavyAttack = true }, forward, right).Button,
                "PlayerBody handles light before heavy");
            Assert.IsTrue(PlayerActions.FromIntent(new Intent { Roll = true }, forward, right).Roll);
        }

        [Test]
        public void ARecordedIntentPlaysBackClose()
        {
            EastFrame(out var forward, out var right);
            var person = new Intent { Move = new Vector3(0.6f, 0f, 0.8f), HeavyAttack = true, Roll = true };
            var played = PlayerActions.FromIntent(person, forward, right).ToIntent(forward, right);
            Assert.Less(Vector3.Angle(person.Move, played.Move), 22.6f, "within half a sector");
            Assert.IsTrue(played.HeavyAttack);
            Assert.IsFalse(played.LightAttack);
            Assert.IsTrue(played.Roll);
        }
    }
}
