using BossFight.RL;
using NUnit.Framework;
using UnityEngine;

namespace BossFight.Agent.Tests
{
    public class OpponentMemoryTests
    {
        const float Step = 0.02f;
        const OpponentMemory.Habit Roll = OpponentMemory.Habit.Roll;

        static OpponentMemory.Sighting At(float time, float x = 0f) =>
            new OpponentMemory.Sighting { Time = time, Position = new Vector3(x, 0f, 0f), Forward = Vector3.forward };

        // Steps first..last, the opponent standing at x = time, so a sighting's position says when it was taken.
        static void RecordSteps(OpponentMemory memory, int first, int last)
        {
            for (int i = first; i <= last; i++) memory.Record(At(i * Step, i * Step));
        }

        [Test]
        public void SeesTheNewestSightingFromBeforeTheDelay()
        {
            var memory = new OpponentMemory(Step);
            RecordSteps(memory, 0, 15);                     // 0.00 .. 0.30 s
            var seen = memory.Seen(0.30f - 0.15f);
            Assert.AreEqual(0.14f, seen.Time, 0.0001f);     // the 0.16 s one is too new
            Assert.AreEqual(0.14f, seen.Position.x, 0.0001f);
        }

        [Test]
        public void EarlyInAFightSeesTheFirstSighting()
        {
            var memory = new OpponentMemory(Step);
            memory.Record(At(10f));
            memory.Record(At(10f + Step));
            Assert.AreEqual(10f, memory.Seen(10f + Step - 0.15f).Time, 0.0001f);
        }

        [Test]
        public void KeepsEnoughSightingsForTheLongestDelay()
        {
            var memory = new OpponentMemory(Step);
            RecordSteps(memory, 0, 200);                    // 4 s, far more than it keeps
            Assert.AreEqual(4f - OpponentMemory.MaxDelay, memory.Seen(4f - OpponentMemory.MaxDelay).Time, 0.0001f);
        }

        [Test]
        public void RecordsOncePerStep()
        {
            var memory = new OpponentMemory(Step);
            memory.Record(At(1f, 5f));
            Assert.IsTrue(memory.Has(1f));
            memory.Record(At(1f, 9f));
            Assert.AreEqual(5f, memory.Seen(1f).Position.x, 0.0001f);
        }

        [Test]
        public void VelocityComesFromTheLastSighting()
        {
            var memory = new OpponentMemory(Step);
            memory.Record(At(0f, 0f));
            memory.Record(At(Step, 0.1f));
            Assert.AreEqual(5f, memory.Seen(Step).Velocity.x, 0.001f);
        }

        [Test]
        public void ClearForgetsSightingsAndTheTeleportToTheSpawn()
        {
            var memory = new OpponentMemory(Step);
            memory.Record(At(0f, 0f));
            memory.Clear();
            memory.Record(At(Step, 30f));                   // respawned far away
            Assert.AreEqual(0f, memory.Seen(Step).Velocity.x, 0.0001f);
            Assert.AreEqual(30f, memory.Seen(0f).Position.x, 0.0001f, "the old fight is gone");
        }

        [Test]
        public void HabitsStartAsNeverUsed()
        {
            var memory = new OpponentMemory(Step);
            memory.Record(At(0f));
            var seen = memory.Seen(0f);
            Assert.AreEqual(OpponentMemory.NeverUsed, seen.SinceLast(Roll), 0.0001f);
            Assert.AreEqual(OpponentMemory.NeverUsed, seen.UsualGap(OpponentMemory.Habit.HeavyAttack), 0.0001f);
        }

        [Test]
        public void UsingAHabitRestartsItsClockAndShrinksItsGap()
        {
            var memory = new OpponentMemory(Step);
            memory.Record(At(0f));
            memory.Used(Roll);
            memory.Record(At(Step));
            var seen = memory.Seen(Step);
            Assert.AreEqual(Step, seen.SinceLast(Roll), 0.0001f);
            Assert.AreEqual(OpponentMemory.NeverUsed / 3f + Step, seen.UsualGap(Roll), 0.0001f);
            Assert.AreEqual(OpponentMemory.NeverUsed + Step, seen.SinceLast(OpponentMemory.Habit.LightAttack), 0.0001f, "other habits keep counting");
        }

        [Test]
        public void TheUsualGapSettlesNearTheRealGap()
        {
            var memory = new OpponentMemory(Step);
            for (int i = 0; i <= 3000; i++)                 // 60 s, rolling every 2 s
            {
                if (i > 0 && i % 100 == 0) memory.Used(Roll);
                memory.Record(At(i * Step));
            }
            // Just after a roll it sits near half the real gap, just before the next near one and a half times it.
            Assert.AreEqual(1f, memory.Seen(3000 * Step).UsualGap(Roll), 0.05f);
            Assert.AreEqual(3f, memory.Seen(2999 * Step).UsualGap(Roll), 0.05f);
        }
    }
}
