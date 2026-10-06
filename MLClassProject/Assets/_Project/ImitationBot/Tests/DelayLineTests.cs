using NUnit.Framework;

namespace BossFight.ImitationBot.Tests
{
    public class DelayLineTests
    {
        const float Step = 0.02f;

        // Steps first..last, each value being its own time, so a value says when it was recorded.
        static DelayLine<float> Recorded(int first, int last)
        {
            var line = new DelayLine<float>(Step);
            for (int i = first; i <= last; i++) line.Record(i * Step, i * Step);
            return line;
        }

        [Test]
        public void SeesTheNewestValueFromBeforeTheDelay()
        {
            var line = Recorded(0, 15);                         // 0.00 .. 0.30 s
            Assert.AreEqual(0.14f, line.Seen(0.30f - 0.15f), 0.0001f);   // 0.16 s is too new
        }

        [Test]
        public void EarlyInAFightSeesTheFirstValue()
        {
            var line = new DelayLine<float>(Step);
            line.Record(10f, 1f);
            line.Record(10f + Step, 2f);
            Assert.AreEqual(1f, line.Seen(10f + Step - 0.15f), 0.0001f);
        }

        [Test]
        public void KeepsEnoughValuesForTheLongestDelay()
        {
            var line = Recorded(0, 200);                        // 4 s, far more than it keeps
            Assert.AreEqual(4f - DelayLine<float>.MaxDelay, line.Seen(4f - DelayLine<float>.MaxDelay), 0.0001f);
        }

        [Test]
        public void RecordsOncePerStep()
        {
            var line = new DelayLine<float>(Step);
            line.Record(1f, 5f);
            Assert.IsTrue(line.Has(1f));
            line.Record(1f, 9f);
            Assert.AreEqual(5f, line.Seen(1f), 0.0001f);
            Assert.AreEqual(1, line.Count);
        }

        [Test]
        public void ClearForgetsTheLastFight()
        {
            var line = Recorded(0, 10);
            line.Clear();
            Assert.AreEqual(0, line.Count);
            line.Record(1f, 30f);
            Assert.AreEqual(30f, line.Seen(0f), 0.0001f);
        }
    }
}
