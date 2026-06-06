using NUnit.Framework;
using UnityEngine;

namespace SoccerBot.Tests
{
    public class ReceptionRulesTests
    {
        private static readonly ReceptionTuning DefaultTuning = new(
            0.55f,
            0.78f,
            0.95f,
            12f,
            65f);

        [Test]
        public void EvaluateQuality_ReturnsMissValueOutsideTimingWindow()
        {
            float quality = ReceptionRules.EvaluateQuality(
                0.2f,
                Vector3.forward,
                Vector3.forward,
                DefaultTuning);

            Assert.That(quality, Is.EqualTo(0.05f).Within(0.0001f));
        }

        [Test]
        public void EvaluateQuality_PerfectTimingAndFacing_ReturnsFullQuality()
        {
            float quality = ReceptionRules.EvaluateQuality(
                0.78f,
                Vector3.forward,
                Vector3.forward,
                DefaultTuning);

            Assert.That(quality, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void EvaluateQuality_BadFacingKeepsTimingButLowersQuality()
        {
            float quality = ReceptionRules.EvaluateQuality(
                0.78f,
                Vector3.forward,
                Vector3.back,
                DefaultTuning);

            Assert.That(quality, Is.EqualTo(0.75f).Within(0.0001f));
        }

        [Test]
        public void EvaluateQuality_FlattensVerticalDirection()
        {
            float quality = ReceptionRules.EvaluateQuality(
                0.78f,
                new Vector3(0f, 5f, 1f),
                new Vector3(0f, -8f, 1f),
                DefaultTuning);

            Assert.That(quality, Is.EqualTo(1f).Within(0.0001f));
        }
    }
}
