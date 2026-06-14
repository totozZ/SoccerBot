using System;
using UnityEngine;

namespace SoccerBot
{
    [Serializable]
    public class TrainingSummaryJson
    {
        public string schemaVersion = "1.0";
        public string project = "SoccerBot";
        public string roundId;
        public string startedAtUtc;
        public string endedAtUtc;
        public float durationSeconds;
        public string dataSource;

        public string currentScenarioName;
        public string shotResult;
        public int finalScore;
        public string grade;

        public TrainingVector3Json passDirection;
        public float passDistance;
        public float estimatedPassBallSpeed;
        public float ballSpeedAtResult;

        public float receiveTimingError;
        public float receiveQuality;
        public bool receiveByFootContact;
        public float footVelocityAtTouch;
        public float footContactPower;
        public float footContactAccuracy;
        public string footContactZone;

        public bool recoveryTriggered;
        public bool recoverySucceeded;

        public float shotPower;
        public TrainingVector3Json shotDirection;

        public string[] phaseTransitions = Array.Empty<string>();

        public string ToJson(bool prettyPrint = false)
        {
            return JsonUtility.ToJson(this, prettyPrint);
        }
    }

    [Serializable]
    public struct TrainingVector3Json
    {
        public float x;
        public float y;
        public float z;

        public TrainingVector3Json(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static TrainingVector3Json From(Vector3 value)
        {
            return new TrainingVector3Json(
                Round(value.x),
                Round(value.y),
                Round(value.z));
        }

        private static float Round(float value)
        {
            return Mathf.Round(value * 1000f) / 1000f;
        }
    }

    [Serializable]
    public class AICoachFeedbackResponse
    {
        public string summary;
        public string mainProblem;
        public string advice;
        public string nextDrillSuggestion;
        public AICoachNextScenarioConfig nextScenarioConfig;

        public bool HasAnyFeedback()
        {
            return !string.IsNullOrWhiteSpace(summary)
                || !string.IsNullOrWhiteSpace(mainProblem)
                || !string.IsNullOrWhiteSpace(advice)
                || !string.IsNullOrWhiteSpace(nextDrillSuggestion);
        }

        public void Validate()
        {
            summary = Clean(summary, "Round complete.");
            mainProblem = Clean(mainProblem, "No single major problem was detected.");
            advice = Clean(advice, "Repeat the drill and focus on a calmer first touch.");
            nextDrillSuggestion = Clean(nextDrillSuggestion, "Run the same pass again with a slightly cleaner touch.");
            nextScenarioConfig?.ValidateAndClamp();
        }

        public static AICoachFeedbackResponse Fallback(TrainingSummaryJson summary, string reason = null)
        {
            string result = string.IsNullOrWhiteSpace(summary?.shotResult) ? "round" : summary.shotResult;
            float quality = summary != null ? summary.receiveQuality : 0f;
            bool pressured = summary != null && summary.recoveryTriggered;

            var response = new AICoachFeedbackResponse
            {
                summary = $"Offline coach: {result} recorded. First touch quality {quality:0.00}.",
                mainProblem = pressured ? "The first touch put you under pressure." : "Keep the touch controlled before the shot.",
                advice = pressured ? "Meet the ball earlier and face the incoming pass before trying to shoot." : "Use the next rep to keep your body square and strike through the target.",
                nextDrillSuggestion = pressured ? "Repeat a medium-speed straight pass with a wider receive window." : "Repeat the same drill and aim for one cleaner first touch.",
                nextScenarioConfig = new AICoachNextScenarioConfig()
            };

            if (!string.IsNullOrWhiteSpace(reason))
                response.summary += $" ({reason})";

            response.Validate();
            return response;
        }

        private static string Clean(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;
            value = value.Trim();
            return value.Length <= 220 ? value : value.Substring(0, 220);
        }
    }

    [Serializable]
    public class AICoachNextScenarioConfig
    {
        public string difficulty = "same";
        public string passType = "straight";
        public string passDirection = "center";
        public float ballSpeed = 1.0f;
        public float defenderPressure = 0.35f;
        public float receiveWindow = 0.40f;
        public float shotWindow = 1.50f;
        public string successCondition = "clean first touch and shot on target";

        public void ValidateAndClamp()
        {
            difficulty = Choice(difficulty, "easier", "same", "harder");
            passType = Choice(passType, "straight", "diagonal", "lofted", "ground");
            passDirection = Choice(passDirection, "left", "center", "right");
            ballSpeed = Mathf.Clamp(ballSpeed, 0.2f, 2.5f);
            defenderPressure = Mathf.Clamp01(defenderPressure);
            receiveWindow = Mathf.Clamp(receiveWindow, 0.15f, 1.2f);
            shotWindow = Mathf.Clamp(shotWindow, 0.5f, 5f);
            if (string.IsNullOrWhiteSpace(successCondition))
                successCondition = "clean first touch and shot on target";
            if (successCondition.Length > 160)
                successCondition = successCondition.Substring(0, 160);
        }

        private static string Choice(string value, params string[] allowed)
        {
            if (string.IsNullOrWhiteSpace(value))
                return allowed.Length > 0 ? allowed[0] : string.Empty;

            string normalized = value.Trim().ToLowerInvariant();
            for (int i = 0; i < allowed.Length; i++)
            {
                if (normalized == allowed[i])
                    return allowed[i];
            }

            return allowed.Length > 0 ? allowed[0] : normalized;
        }
    }
}
