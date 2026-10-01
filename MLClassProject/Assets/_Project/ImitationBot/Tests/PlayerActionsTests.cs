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
        public void WalkingAtTheBossIsTowardOne()
        {
            EastFrame(out var forward, out var right);
            var actions = PlayerActions.FromIntent(new Intent { Move = Vector3.right }, forward, right);
            Assert.AreEqual(1f, actions.Toward, 0.0001f);
            Assert.AreEqual(0f, actions.Right, 0.0001f);
        }

        [Test]
        public void CirclingKeepsItsSideWhereverTheBossIs()
        {
            EastFrame(out var forward, out var right);
            var actions = PlayerActions.FromIntent(new Intent { Move = Vector3.back }, forward, right);   // -z
            Assert.AreEqual(1f, actions.Right, 0.0001f);
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
        public void ARecordedIntentPlaysBackTheSame()
        {
            EastFrame(out var forward, out var right);
            var person = new Intent { Move = new Vector3(0.6f, 0f, 0.8f), HeavyAttack = true, Roll = true };
            var played = PlayerActions.FromIntent(person, forward, right).ToIntent(forward, right);
            Assert.AreEqual(0.6f, played.Move.x, 0.0001f);
            Assert.AreEqual(0.8f, played.Move.z, 0.0001f);
            Assert.IsTrue(played.HeavyAttack);
            Assert.IsFalse(played.LightAttack);
            Assert.IsTrue(played.Roll);
        }

        [Test]
        public void MovementIsCappedLikeAStick()
        {
            EastFrame(out var forward, out var right);
            var played = new PlayerActions { Toward = 1f, Right = 1f }.ToIntent(forward, right);
            Assert.AreEqual(1f, played.Move.magnitude, 0.0001f);
        }
    }
}
