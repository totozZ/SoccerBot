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

        [Header("Timing")]
        [SerializeField] private float _fadeDuration = 0.5f;
        [SerializeField] private float _holdDuration = 4.5f;

        [Header("Outcome Colors")]
        [SerializeField] private Color _scoreColor       = new Color(0.2f, 0.9f, 0.3f);
        [SerializeField] private Color _interceptedColor = new Color(0.95f, 0.3f, 0.3f);
        [SerializeField] private Color _missedColor      = new Color(0.95f, 0.75f, 0.2f);

        void Start()
        {
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            if (_player != null) _player.OnScenarioComplete += Show;
            else Debug.LogWarning("[ScorePanel] No ScenarioPlayer reference.");
        }

        void OnDestroy()
        {
            if (_player != null) _player.OnScenarioComplete -= Show;
        }

        public void Show(Scenario s)
        {
            if (s == null) return;
            StopAllCoroutines();
            StartCoroutine(ShowRoutine(s));
        }

        private IEnumerator ShowRoutine(Scenario s)
        {
            SetText(_scenarioNameText, s.scenarioName);
            SetText(_scoreText, $"{s.finalScore}");
            SetText(_outcomeText, OutcomeLabel(s.outcome));
            SetText(_flavorText, s.flavorText);

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
    }
}
