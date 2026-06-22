using System;
using UnityEngine;

namespace SoccerBot
{
    public enum GameplayMode
    {
        Training,
        ArenaAttack
    }

    public enum ControlProfile
    {
        KeyboardMouse,
        Gamepad,
        VrStriker,
        XrSimulator
    }

    public enum ArenaRoundState
    {
        Serving,
        Live,
        Resetting,
        Finished
    }

    public enum BallActionKind
    {
        Control,
        Pass,
        Shot,
        Tackle
    }

    public enum BallActionSource
    {
        KeyboardMouse,
        Gamepad,
        VrPhysical,
        XrSimulator,
        AI
    }

    public enum PossessionOwner
    {
        Free,
        Player,
        Teammate,
        Opponent,
        Goalkeeper
    }

    [Serializable]
    public struct PlayerIntentState
    {
        public Vector2 Move;
        public Vector2 Look;
        public bool SprintHeld;
        public bool ControlPressed;
        public float PassCharge01;
        public bool PassReleased;
        public float ShotCharge01;
        public bool ShotReleased;
        public bool TacklePressed;
    }

    public readonly struct BallActionRequest
    {
        public readonly BallActionKind Kind;
        public readonly BallActionSource Source;
        public readonly Transform Actor;
        public readonly Vector3 Direction;
        public readonly float Power01;

        public BallActionRequest(
            BallActionKind kind,
            BallActionSource source,
            Transform actor,
            Vector3 direction,
            float power01)
        {
            Kind = kind;
            Source = source;
            Actor = actor;
            Direction = ArenaGameplayRules.FlattenDirection(direction, Vector3.forward);
            Power01 = Mathf.Clamp01(power01);
        }
    }

    public static class ArenaGameplayRules
    {
        public static Vector3 FlattenDirection(Vector3 direction, Vector3 fallback)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                return direction.normalized;

            fallback.y = 0f;
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
        }

        public static float CalculateCharge01(float heldSeconds, float fullChargeSeconds, float minimum01)
        {
            float safeDuration = Mathf.Max(0.01f, fullChargeSeconds);
            return Mathf.Lerp(Mathf.Clamp01(minimum01), 1f, Mathf.Clamp01(heldSeconds / safeDuration));
        }

        public static Vector3 CalculateReboundVelocity(Vector3 incoming, Vector3 normal, float retention01)
        {
            Vector3 safeNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            return Vector3.Reflect(incoming, safeNormal) * Mathf.Clamp01(retention01);
        }

        public static bool IsContested(float playerDistance, float opponentDistance, float contestRadius)
        {
            return opponentDistance <= Mathf.Max(0f, contestRadius) && opponentDistance <= playerDistance + 0.15f;
        }
    }

    public sealed class ArenaSessionClock
    {
        public float RemainingSeconds { get; private set; }

        public ArenaSessionClock(float durationSeconds)
        {
            RemainingSeconds = Mathf.Max(0f, durationSeconds);
        }

        public bool Tick(float deltaTime, ArenaRoundState state)
        {
            if (state == ArenaRoundState.Live)
                RemainingSeconds = Mathf.Max(0f, RemainingSeconds - Mathf.Max(0f, deltaTime));
            return RemainingSeconds <= 0f;
        }
    }
}
