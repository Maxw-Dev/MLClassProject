using BossFight.Core;
using NUnit.Framework;
using UnityEngine;

namespace BossFight.Combat.Tests
{
    public class HealthTests
    {
        GameObject go;
        Health health;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("health");
            health = go.AddComponent<Health>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        [Test]
        public void MultiplierScalesTheDamageAndTheReportedHit()
        {
            float reported = 0f;
            health.Damaged += info => reported = info.Amount;
            health.IncomingDamageMultiplier = 2f;

            health.TakeDamage(new DamageInfo(10f, null));

            Assert.AreEqual(health.Max - 20f, health.Current);
            Assert.AreEqual(20f, reported);
        }

        [Test]
        public void ResetToFullPutsTheMultiplierBackToOne()
        {
            health.IncomingDamageMultiplier = 3f;
            health.ResetToFull();

            Assert.AreEqual(1f, health.IncomingDamageMultiplier);
            health.TakeDamage(new DamageInfo(10f, null));
            Assert.AreEqual(health.Max - 10f, health.Current);
        }
    }
}
