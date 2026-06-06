using UnityEngine;

namespace SoccerBot
{
    public readonly struct ReceptionTuning
    {
        public readonly float WindowStart01;
        public readonly float Perfect01;
        public readonly float WindowEnd01;
        public readonly float FacingFullScoreAngle;
        public readonly float FacingFailAngle;

        public ReceptionTuning(
            float windowStart01,
            float perfect01,
            float windowEnd01,
            float facingFullScoreAngle,
            float facingFailAngle)
        {
            WindowStart01 = windowStart01;
            Perfect01 = perfect01;
            WindowEnd01 = windowEnd01;
            FacingFullScoreAngle = facingFullScoreAngle;
            FacingFailAngle = facingFailAngle;
        }
    }

    public static class ReceptionRules
    {
        public static float EvaluateQuality(
            float passProgress01,
            Vector3 playerFacing,
            Vector3 incomingDirection,
            ReceptionTuning tuning)
        {
            float start = Mathf.Min(tuning.WindowStart01, tuning.WindowEnd01);
            float end = Mathf.Max(tuning.WindowStart01, tuning.WindowEnd01);
            float perfect = Mathf.Clamp(tuning.Perfect01, start, end);
            if (passProgress01 < start || passProgress01 > end)
                return 0.05f;

            float span = passProgress01 <= perfect
                ? Mathf.Max(0.0001f, perfect - start)
                : Mathf.Max(0.0001f, end - perfect);
            float timingScore = 1f - Mathf.Clamp01(Mathf.Abs(passProgress01 - perfect) / span);

            Vector3 facing = Flatten(playerFacing);
            Vector3 incoming = Flatten(incomingDirection);

            float facingScore = 1f;
            if (facing.sqrMagnitude > 0.0001f && incoming.sqrMagnitude > 0.0001f)
            {
                float angle = Vector3.Angle(facing.normalized, incoming.normalized);
                facingScore = Mathf.InverseLerp(
                    tuning.FacingFailAngle,
                    tuning.FacingFullScoreAngle,
                    angle);
            }

            return Mathf.Clamp01(timingScore * 0.75f + facingScore * 0.25f);
        }

        public static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
