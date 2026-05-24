// ScenarioTrigger.cs — Listens to GameManager.OnShotFired and asks ScenarioPlayer
// to play one of the registered scenarios. Selection is configurable.
//
// Demo keys (always active):
//   1 / 2 / 3  → force-play scenarios[0..2]

using UnityEngine;

namespace SoccerBot
{
    public class ScenarioTrigger : MonoBehaviour
    {
        public enum SelectionMode { Random, Sequential }

        [Header("References")]
        [SerializeField] private ScenarioPlayer _player;
        [SerializeField] private Scenario[] _scenarios;

        [Header("Selection")]
        [SerializeField] private SelectionMode _mode = SelectionMode.Random;
        [Tooltip("If true, ignores OnShotFired while a scenario is already playing.")]
        [SerializeField] private bool _suppressDuringPlayback = true;

        private int _seqIndex;

        void Start()
        {
            if (_player == null)
            {
                Debug.LogError("[ScenarioTrigger] ScenarioPlayer reference is missing.");
                enabled = false;
                return;
            }
            if (_scenarios == null || _scenarios.Length == 0)
            {
                Debug.LogWarning("[ScenarioTrigger] No scenarios assigned.");
            }
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnShotFired += HandleShotFired;
            }
            else
            {
                Debug.LogWarning("[ScenarioTrigger] GameManager.Instance not found at Start.");
            }
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnShotFired -= HandleShotFired;
            }
        }

        void Update()
        {
            // Demo override: 1 / 2 / 3 force a specific scenario.
            if (Input.GetKeyDown(KeyCode.Alpha1)) ForcePlay(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) ForcePlay(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) ForcePlay(2);
        }

        private void HandleShotFired()
        {
            if (_suppressDuringPlayback && _player.IsPlaying) return;
            var s = PickScenario();
            if (s != null) _player.Play(s);
        }

        public void ForcePlay(int index)
        {
            if (_scenarios == null || index < 0 || index >= _scenarios.Length) return;
            if (_suppressDuringPlayback && _player.IsPlaying) return;
            _player.Play(_scenarios[index]);
        }

        private Scenario PickScenario()
        {
            if (_scenarios == null || _scenarios.Length == 0) return null;

            switch (_mode)
            {
                case SelectionMode.Sequential:
                {
                    var s = _scenarios[_seqIndex % _scenarios.Length];
                    _seqIndex++;
                    return s;
                }
                case SelectionMode.Random:
                default:
                    return _scenarios[Random.Range(0, _scenarios.Length)];
            }
        }
    }
}
