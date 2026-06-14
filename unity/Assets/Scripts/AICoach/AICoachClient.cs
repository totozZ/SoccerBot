using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace SoccerBot
{
    [DisallowMultipleComponent]
    public class AICoachClient : MonoBehaviour
    {
        [Header("Endpoint")]
        [SerializeField] private string _analyzeEndpoint = "http://localhost:8000/analyze";
        [SerializeField, Range(1, 15)] private int _timeoutSeconds = 3;
        [SerializeField] private bool _logJsonPayloads = false;

        public AICoachFeedbackResponse LastFeedback { get; private set; }
        public AICoachNextScenarioConfig LastValidatedNextScenarioConfig { get; private set; }

        public void Analyze(TrainingSummaryJson summary, Action<AICoachFeedbackResponse> onComplete)
        {
            if (!isActiveAndEnabled)
            {
                onComplete?.Invoke(AICoachFeedbackResponse.Fallback(summary, "coach client disabled"));
                return;
            }

            StartCoroutine(AnalyzeRoutine(summary, onComplete));
        }

        private IEnumerator AnalyzeRoutine(TrainingSummaryJson summary, Action<AICoachFeedbackResponse> onComplete)
        {
            if (summary == null)
            {
                var emptyFallback = AICoachFeedbackResponse.Fallback(null, "missing training summary");
                StoreFeedback(emptyFallback);
                onComplete?.Invoke(emptyFallback);
                yield break;
            }

            string payload = summary.ToJson(false);
            if (_logJsonPayloads)
                Debug.Log($"[AICoach] POST {_analyzeEndpoint}\n{payload}");

            using var request = new UnityWebRequest(_analyzeEndpoint, UnityWebRequest.kHttpVerbPOST);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(payload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Mathf.Max(1, _timeoutSeconds);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            AICoachFeedbackResponse response;
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[AICoach] Request failed: {request.error}");
                response = AICoachFeedbackResponse.Fallback(summary, "AI server offline");
            }
            else
            {
                response = ParseResponse(request.downloadHandler.text, summary);
            }

            StoreFeedback(response);
            onComplete?.Invoke(response);
        }

        private AICoachFeedbackResponse ParseResponse(string json, TrainingSummaryJson summary)
        {
            if (_logJsonPayloads)
                Debug.Log($"[AICoach] Response\n{json}");

            try
            {
                var response = JsonUtility.FromJson<AICoachFeedbackResponse>(json);
                if (response == null || !response.HasAnyFeedback())
                    return AICoachFeedbackResponse.Fallback(summary, "empty AI response");

                response.Validate();
                return response;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AICoach] Could not parse response JSON: {ex.Message}");
                return AICoachFeedbackResponse.Fallback(summary, "invalid AI response");
            }
        }

        private void StoreFeedback(AICoachFeedbackResponse response)
        {
            LastFeedback = response;
            LastValidatedNextScenarioConfig = response?.nextScenarioConfig;
        }
    }
}
