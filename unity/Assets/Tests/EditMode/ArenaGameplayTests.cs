#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace SoccerBot.Tests
{
    public class ArenaGameplayTests
    {
        [Test]
        public void CalculateCharge_UsesMinimumAndReachesFullPower()
        {
            Assert.That(ArenaGameplayRules.CalculateCharge01(0f, 1f, 0.2f), Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(ArenaGameplayRules.CalculateCharge01(1.5f, 1f, 0.2f), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void CalculateRebound_ReflectsAndRetainsRequestedSpeed()
        {
            Vector3 incoming = new Vector3(4f, 0f, 2f);
            Vector3 rebound = ArenaGameplayRules.CalculateReboundVelocity(incoming, Vector3.left, 0.7f);

            Assert.That(rebound.x, Is.EqualTo(-2.8f).Within(0.0001f));
            Assert.That(rebound.z, Is.EqualTo(1.4f).Within(0.0001f));
        }

        [Test]
        public void SessionClock_OnlyTicksDuringLivePlay()
        {
            var clock = new ArenaSessionClock(90f);

            clock.Tick(10f, ArenaRoundState.Resetting);
            Assert.That(clock.RemainingSeconds, Is.EqualTo(90f).Within(0.0001f));

            clock.Tick(10f, ArenaRoundState.Live);
            Assert.That(clock.RemainingSeconds, Is.EqualTo(80f).Within(0.0001f));
        }

        [Test]
        public void ContestRule_RequiresOpponentToBeCloseAndCompetitive()
        {
            Assert.That(ArenaGameplayRules.IsContested(0.8f, 0.7f, 1.05f), Is.True);
            Assert.That(ArenaGameplayRules.IsContested(0.5f, 1f, 1.05f), Is.False);
            Assert.That(ArenaGameplayRules.IsContested(0.8f, 1.2f, 1.05f), Is.False);
        }
    }
}
#endif
