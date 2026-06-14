// ScorePanel.cs — Pops up the score / outcome / flavor when a Scenario completes.
// Subscribe to ScenarioPlayer.OnScenarioComplete. Fades in, holds, fades out.
//
// Hookup in Editor:
//   - Drop on a Canvas child GameObject with a CanvasGroup
//   - Wire 4 TMP_Text references (name, score, outcome, flavor)
//   - Drop the scene's ScenarioPlayer into _player

using System.Collections;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SoccerBot
{
    public class ScorePanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private ScenarioPlayer _player;

        [Header("Text")]
        [SerializeField] private TMP_Text _scenarioNameText;
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private TMP_Text _outcomeText;
        [SerializeField] private TMP_Text _flavorText;
        [SerializeField] private TMP_Text _aiCoachFeedbackText;

        [Header("Timing")]
        [SerializeField] private float _fadeDuration = 0.5f;
        [SerializeField] private float _holdDuration = 4.5f;

        [Header("Outcome Colors")]
        [SerializeField] private Color _scoreColor       = new Color(0.2f, 0.9f, 0.3f);
        [SerializeField] private Color _interceptedColor = new Color(0.95f, 0.3f, 0.3f);
        [SerializeField] private Color _missedColor      = new Color(0.95f, 0.75f, 0.2f);

        [Header("Score Text Pools")]
        [Tooltip("Random text for finalScore <= 30")]
        [SerializeField] private string[] _scoreTexts30 = new string[]
        {
            "MISSED THE MOMENT!",
            "BAD TIMING!",
            "OFF TARGET!",
            "NOT QUITE!",
            "MISSED IT!",
            "POOR TIMING!",
            "NEEDS WORK!",
            "TRY AGAIN!"
        };

        [Tooltip("Random text for finalScore <= 50")]
        [SerializeField] private string[] _scoreTexts50 = new string[]
        {
            "CLOSE ONE!",
            "ALMOST!",
            "SO CLOSE!",
            "JUST MISSED!",
            "NEARLY THERE!",
            "OFF BY A MOMENT!",
            "BETTER LUCK NEXT TIME!"
        };

        [Tooltip("Random text for finalScore > 50")]
        [SerializeField] private string[] _scoreTexts100 = new string[]
        {
            "PERFECT SHOT!",
            "BULLSEYE!",
            "TOP CORNER!",
            "WORLD CLASS!",
            "UNSTOPPABLE!",
            "WHAT A GOAL!",
            "CLINICAL FINISH!"
        };

        private string _firstTouchSummary = string.Empty;
        private string _currentScenarioFlavor = string.Empty;

        void Start()
        {
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            EnsureAICoachFeedbackText();
            if (_player != null) _player.OnScenarioComplete += Show;
            else Debug.LogWarning("[ScorePanel] No ScenarioPlayer reference.");
        }

        void OnDestroy()
        {
            if (_player != null) _player.OnScenarioComplete -= Show;
        }

        public void Show(Scenario s)
        {
            Show(s, null, null, null);
        }

        public void Show(Scenario s, string outcomeLabelOverride, string scoreTextOverride = null, string flavorTextOverride = null)
        {
            if (s == null) return;
            StopAllCoroutines();
            StartCoroutine(ShowRoutine(s, outcomeLabelOverride, scoreTextOverride, flavorTextOverride));
        }

        public void SetFirstTouchContext(float quality01, float receiveBias)
        {
            if (quality01 >= 0.78f)
                _firstTouchSummary = $"First Touch: excellent  +{receiveBias * 100f:0}% shot chance";
            else if (quality01 >= 0.45f)
                _firstTouchSummary = "First Touch: stable  neutral shot chance";
            else
                _firstTouchSummary = $"First Touch: pressured  {receiveBias * 100f:0}% shot chance";
        }

        public void ShowAICoachAnalyzing()
        {
            EnsureAICoachFeedbackText();
            string text = "AI Coach Feedback\nAnalyzing this round...";
            if (_aiCoachFeedbackText != null)
                SetText(_aiCoachFeedbackText, text);
            else
                SetText(_flavorText, $"{ComposeFlavor(_currentScenarioFlavor)}\n\n{text}");
        }

        public void ShowAICoachFeedback(AICoachFeedbackResponse feedback)
        {
            EnsureAICoachFeedbackText();
            feedback ??= AICoachFeedbackResponse.Fallback(null, "missing feedback");
            feedback.Validate();

            string text = FormatAICoachFeedback(feedback);
            if (_aiCoachFeedbackText != null)
                SetText(_aiCoachFeedbackText, text);
            else
                SetText(_flavorText, $"{ComposeFlavor(_currentScenarioFlavor)}\n\n{text}");
        }

        public void HideImmediate()
        {
            StopAllCoroutines();
            transform.localScale = Vector3.one;
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            SetText(_aiCoachFeedbackText, string.Empty);
        }

        private IEnumerator ShowRoutine(Scenario s, string outcomeLabelOverride, string scoreTextOverride, string flavorTextOverride)
        {
            EnsureAICoachFeedbackText();
            SetText(_aiCoachFeedbackText, string.Empty);
            _currentScenarioFlavor = string.IsNullOrWhiteSpace(flavorTextOverride) ? s.flavorText : flavorTextOverride;

            SetText(_scenarioNameText, s.scenarioName);
            SetText(_scoreText, string.IsNullOrWhiteSpace(scoreTextOverride) ? GetRandomScoreText(s.finalScore) : scoreTextOverride);
            SetText(_outcomeText, string.IsNullOrWhiteSpace(outcomeLabelOverride) ? OutcomeLabel(s.outcome) : outcomeLabelOverride);
            SetText(_flavorText, ComposeFlavor(_currentScenarioFlavor));

            var c = OutcomeColor(s.outcome);
            ApplyColor(_scenarioNameText, c);
            ApplyColor(_scoreText, c);
            ApplyColor(_outcomeText, c);

            // Pop-in: fade + scale from 0.8 → 1.0
            transform.localScale = Vector3.one * 0.8f;
            float t = 0f;
            while (t < _fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / _fadeDuration);
                if (_canvasGroup != null) _canvasGroup.alpha = p;
                transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, p);
                yield return null;
            }
            if (_canvasGroup != null) _canvasGroup.alpha = 1f;
            transform.localScale = Vector3.one;

            yield return new WaitForSecondsRealtime(_holdDuration);
            yield return Fade(1f, 0f, _fadeDuration);
            transform.localScale = Vector3.one;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (_canvasGroup == null) yield break;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            _canvasGroup.alpha = to;
        }

        private Color OutcomeColor(ScenarioOutcome o) => o switch
        {
            ScenarioOutcome.Score       => _scoreColor,
            ScenarioOutcome.Intercepted => _interceptedColor,
            ScenarioOutcome.Missed      => _missedColor,
            _                           => Color.white
        };

        private static string OutcomeLabel(ScenarioOutcome o) => o switch
        {
            ScenarioOutcome.Score       => "GOAL",
            ScenarioOutcome.Intercepted => "INTERCEPTED",
            ScenarioOutcome.Missed      => "MISSED",
            _                           => o.ToString()
        };

        private static void SetText(TMP_Text t, string v) { if (t != null) t.text = v; }
        private static void ApplyColor(TMP_Text t, Color c) { if (t != null) t.color = c; }

        private string ComposeFlavor(string scenarioFlavor)
        {
            if (string.IsNullOrWhiteSpace(_firstTouchSummary))
                return scenarioFlavor;
            if (string.IsNullOrWhiteSpace(scenarioFlavor))
                return _firstTouchSummary;
            return $"{scenarioFlavor}\n{_firstTouchSummary}";
        }

        private void EnsureAICoachFeedbackText()
        {
            if (_aiCoachFeedbackText != null)
                return;

            Transform parent = _flavorText != null && _flavorText.transform.parent != null
                ? _flavorText.transform.parent
                : transform;

            var go = new GameObject("AICoachFeedback", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = string.Empty;
            text.fontSize = 20f;
            text.color = new Color(0.85f, 0.95f, 1f, 1f);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(760f, 150f);
            rt.anchoredPosition = new Vector2(0f, 18f);

            _aiCoachFeedbackText = text;
        }

        private static string FormatAICoachFeedback(AICoachFeedbackResponse feedback)
        {
            return "AI Coach Feedback\n"
                + $"Summary: {feedback.summary}\n"
                + $"Problem: {feedback.mainProblem}\n"
                + $"Advice: {feedback.advice}\n"
                + $"Next drill: {feedback.nextDrillSuggestion}";
        }

        private string GetRandomScoreText(int finalScore)
        {
            string[] pool = finalScore switch
            {
                <= 30 => _scoreTexts30,
                <= 50 => _scoreTexts50,
                _     => _scoreTexts100
            };
            if (pool == null || pool.Length == 0) return finalScore.ToString();
            return pool[Random.Range(0, pool.Length)];
        }
    }
}
