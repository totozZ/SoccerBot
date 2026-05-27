// ScoreBoard.cs — Top-of-screen running tally of match outcomes.
// Subscribes to ScenarioPlayer.OnScenarioComplete and accumulates counts per outcome.
// Self-locates a TMP_Text in its children if none is wired.

using TMPro;
using UnityEngine;

namespace SoccerBot
{
    public class ScoreBoard : MonoBehaviour
    {
        [Header("References (optional — auto-resolved if null)")]
        [SerializeField] private ScenarioPlayer _player;
        [SerializeField] private TMP_Text _scoreText;

        [Header("Format")]
        [SerializeField] private string _format = "GOALS {0}   MISSED {1}   STOP {2}";

        private int _goals;
        private int _missed;
        private int _intercepted;

        void Awake()
        {
            if (_scoreText == null)
                _scoreText = GetComponentInChildren<TMP_Text>();
        }

        void Start()
        {
            if (_player == null) _player = FindAnyObjectByType<ScenarioPlayer>();
            if (_player != null) _player.OnScenarioComplete += OnScenarioComplete;
            else Debug.LogWarning("[ScoreBoard] No ScenarioPlayer reference.");
            Refresh();
        }

        void OnDestroy()
        {
            if (_player != null) _player.OnScenarioComplete -= OnScenarioComplete;
        }

        private void OnScenarioComplete(Scenario s)
        {
            if (s == null) return;
            switch (s.outcome)
            {
                case ScenarioOutcome.Score:       _goals++;       break;
                case ScenarioOutcome.Missed:      _missed++;      break;
                case ScenarioOutcome.Intercepted: _intercepted++; break;
            }
            Refresh();
        }

        private void Refresh()
        {
            if (_scoreText != null)
                _scoreText.text = string.Format(_format, _goals, _missed, _intercepted);
        }

        public void ResetCounts()
        {
            _goals = _missed = _intercepted = 0;
            Refresh();
        }
    }
}
