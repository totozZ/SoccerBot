using NUnit.Framework;

namespace SoccerBot.Tests
{
    public class RecoveryMashStateTests
    {
        [Test]
        public void RegisterPress_ReachesSuccessAtTarget()
        {
            var state = new RecoveryMashState(3, 0.2f, 1.5f);

            state.RegisterPress();
            state.RegisterPress();
            Assert.That(state.Succeeded, Is.False);

            state.RegisterPress();
            Assert.That(state.Succeeded, Is.True);
            Assert.That(state.PressCount, Is.EqualTo(3));
        }

        [Test]
        public void RegisterPress_AddsPushAndPulse()
        {
            var state = new RecoveryMashState(4, 0.14f, 1.9f);

            state.RegisterPress();

            Assert.That(state.PersistentPush, Is.EqualTo(0.14f).Within(0.0001f));
            Assert.That(state.PressPulse, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Tick_DecaysPulseAndClampsPushBeforeSuccess()
        {
            var state = new RecoveryMashState(20, 1f, 2f);

            state.RegisterPress();
            state.RegisterPress();
            state.Tick(0.1f);

            Assert.That(state.PressPulse, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(state.PersistentPush, Is.EqualTo(1.7f).Within(0.0001f));
        }

        [Test]
        public void Reset_ClearsRuntimeState()
        {
            var state = new RecoveryMashState(1, 0.5f, 2f);
            state.RegisterPress();

            state.Reset();

            Assert.That(state.PressCount, Is.Zero);
            Assert.That(state.PersistentPush, Is.Zero);
            Assert.That(state.PressPulse, Is.Zero);
            Assert.That(state.Succeeded, Is.False);
        }
    }
}
