using UnityEngine;

namespace SoccerBot
{
    public sealed class RecoveryMashState
    {
        private readonly int _pressTarget;
        private readonly float _pushPerPress;
        private readonly float _successKnockback;

        public RecoveryMashState(int pressTarget, float pushPerPress, float successKnockback)
        {
            _pressTarget = Mathf.Max(1, pressTarget);
            _pushPerPress = Mathf.Max(0f, pushPerPress);
            _successKnockback = Mathf.Max(0f, successKnockback);
        }

        public int PressCount { get; private set; }
        public float PersistentPush { get; private set; }
        public float PressPulse { get; private set; }
        public bool Succeeded { get; private set; }
        public float MaxPushBeforeSuccess => _successKnockback * 0.85f;

        public void Reset()
        {
            PressCount = 0;
            PersistentPush = 0f;
            PressPulse = 0f;
            Succeeded = false;
        }

        public void RegisterPress()
        {
            if (Succeeded) return;

            PressCount++;
            PressPulse = 1f;
            PersistentPush += _pushPerPress;

            if (PressCount >= _pressTarget)
                Succeeded = true;
        }

        public void Tick(float deltaTime)
        {
            PersistentPush = Mathf.Min(PersistentPush, MaxPushBeforeSuccess);
            PressPulse = Mathf.MoveTowards(PressPulse, 0f, Mathf.Max(0f, deltaTime) * 5f);
        }
    }
}
