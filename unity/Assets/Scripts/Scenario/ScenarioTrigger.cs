// ScenarioTrigger.cs — Listens to GameManager.OnShotFired and asks ScenarioPlayer
// to play one of the registered scenarios.
//
// Input (Input System):
//   Keyboard: 1/2/3 = force scenario, Space = next
//   Quest:    right trigger or A/X = next scenario

using UnityEngine;
using UnityEngine.InputSystem;

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
        private InputAction _triggerAction;
        private InputAction _primaryAction;
        private InputAction _secondaryAction;

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

            // XR controller actions for Quest trigger / A / B buttons.
            _triggerAction = new InputAction(binding: "<XRController>{RightHand}/triggerButton");
            _primaryAction = new InputAction(binding: "<XRController>{RightHand}/primaryButton");
            _secondaryAction = new InputAction(binding: "<XRController>{RightHand}/secondaryButton");
            _triggerAction.Enable();
            _primaryAction.Enable();
            _secondaryAction.Enable();
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnShotFired -= HandleShotFired;

            _triggerAction?.Disable();
            _primaryAction?.Disable();
            _secondaryAction?.Disable();
        }

        void Update()
        {
            var kb = Keyboard.current;

            // Keyboard: 1/2/3 = force scenario, Space = next (Editor debug).
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) ForcePlay(0);
                else if (kb.digit2Key.wasPressedThisFrame) ForcePlay(1);
                else if (kb.digit3Key.wasPressedThisFrame) ForcePlay(2);
                else if (kb.spaceKey.wasPressedThisFrame && !_player.IsPlaying)
                {
                    var s = PickScenario();
                    if (s != null) _player.Play(s);
                }
            }

            // Quest controller: trigger, A/X, or B/Y → next scenario.
            if (!_player.IsPlaying)
            {
                if (_triggerAction.WasPressedThisFrame() ||
                    _primaryAction.WasPressedThisFrame() ||
                    _secondaryAction.WasPressedThisFrame())
                {
                    ForcePlay(_seqIndex % _scenarios.Length);
                }
            }
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
