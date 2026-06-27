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
        public void ServeProtection_BlocksAiButAllowsPlayerSources()
        {
            Assert.That(ArenaGameplayRules.ShouldBlockServeProtectedAction(BallActionSource.AI, true), Is.True);
            Assert.That(ArenaGameplayRules.ShouldBlockServeProtectedAction(BallActionSource.VrPhysical, true), Is.False);
            Assert.That(ArenaGameplayRules.ShouldBlockServeProtectedAction(BallActionSource.KeyboardMouse, true), Is.False);
            Assert.That(ArenaGameplayRules.ShouldBlockServeProtectedAction(BallActionSource.AI, false), Is.False);
        }

        [Test]
        public void ReachRule_RequiresFlatRangeAndReachableHeight()
        {
            Vector3 actor = Vector3.zero;

            Assert.That(ArenaGameplayRules.CanReachBall(actor, new Vector3(0.6f, 0.25f, 0.4f), 0.9f, 0.85f), Is.True);
            Assert.That(ArenaGameplayRules.CanReachBall(actor, new Vector3(0.6f, 1.4f, 0.4f), 0.9f, 0.85f), Is.False);
            Assert.That(ArenaGameplayRules.CanReachBall(actor, new Vector3(1.2f, 0.2f, 0f), 0.9f, 0.85f), Is.False);
        }

        [Test]
        public void SupportPosition_StaysAheadAndToTheSideOfPlayer()
        {
            Vector3 support = ArenaGameplayRules.CalculateSupportPosition(
                Vector3.zero,
                Vector3.forward,
                Vector3.right,
                -2.2f,
                2.35f,
                new Vector3(0.25f, 0f, -0.1f));

            Assert.That(support.x, Is.EqualTo(-1.95f).Within(0.0001f));
            Assert.That(support.z, Is.EqualTo(2.25f).Within(0.0001f));
        }

        [Test]
        public void TeammateReceiveRule_WaitsUnlessBallIsPlayable()
        {
            Assert.That(ArenaGameplayRules.ShouldTeammateReceive(PossessionOwner.Teammate, 3f, 8f, 0f, 1.35f, 3.2f), Is.True);
            Assert.That(ArenaGameplayRules.ShouldTeammateReceive(PossessionOwner.Free, 2.1f, 0.5f, 0f, 1.35f, 3.2f), Is.False);
            Assert.That(ArenaGameplayRules.ShouldTeammateReceive(PossessionOwner.Free, 1f, 2.4f, 0f, 1.35f, 3.2f), Is.True);
            Assert.That(ArenaGameplayRules.ShouldTeammateReceive(PossessionOwner.Free, 1f, 4.6f, 0.6f, 1.35f, 3.2f), Is.True);
            Assert.That(ArenaGameplayRules.ShouldTeammateReceive(PossessionOwner.Opponent, 0.4f, 0.2f, 0f, 1.35f, 3.2f), Is.False);
        }

        [Test]
        public void RollingDamping_PreservesInitialSpeedThenDecaysAndStops()
        {
            Vector3 initial = new Vector3(3f, -0.05f, 0f);
            Vector3 protectedRoll = ArenaGameplayRules.ApplyRollingDamping(
                initial,
                true,
                0.1f,
                0.18f,
                3.2f,
                0.18f,
                0.02f);

            Assert.That(protectedRoll.x, Is.EqualTo(initial.x).Within(0.0001f));

            Vector3 decayed = ArenaGameplayRules.ApplyRollingDamping(
                initial,
                true,
                0.25f,
                0.18f,
                3.2f,
                0.18f,
                0.2f);

            Assert.That(decayed.x, Is.LessThan(initial.x));
            Assert.That(decayed.y, Is.EqualTo(initial.y).Within(0.0001f));

            Vector3 stopped = ArenaGameplayRules.ApplyRollingDamping(
                new Vector3(0.12f, 0f, 0.04f),
                true,
                0.25f,
                0.18f,
                3.2f,
                0.18f,
                0.02f);

            Assert.That(stopped.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(stopped.z, Is.EqualTo(0f).Within(0.0001f));
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
