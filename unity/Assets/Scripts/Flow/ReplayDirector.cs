using UnityEngine;

namespace SoccerBot
{
    // Watches MatchFlowController phase and switches cameras for the replay moment.
    // Shot phase  → cuts to SideCam
    // Score phase → holds SideCam briefly, then cuts back to FpsCamera
    public class ReplayDirector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MatchFlowController _matchFlow;
        [SerializeField] private CameraSwitcher _cameraSwitcher;

        [Header("Camera Names (must match GameObject names in scene)")]
        [SerializeField] private string _fpsCamName    = "FpsCamera";
        [SerializeField] private string _replayCamName = "SideCam";

        [Header("Timing")]
        [Tooltip("Seconds to hold replay cam after score before cutting back to FPS.")]
        [SerializeField] private float _holdAfterScore = 1.8f;

        private MatchFlowController.Phase _lastPhase = MatchFlowController.Phase.Idle;
        private float _scoreHoldTimer;
        private bool  _holdingScore;

        void Start()
        {
            if (_matchFlow == null)
                _matchFlow = FindFirstObjectByType<MatchFlowController>();
            if (_cameraSwitcher == null)
                _cameraSwitcher = FindFirstObjectByType<CameraSwitcher>();
        }

        void Update()
        {
            if (_matchFlow == null) return;

            var phase = _matchFlow.CurrentPhase;
            if (phase != _lastPhase)
            {
                OnPhaseChanged(phase);
                _lastPhase = phase;
            }

            if (_holdingScore)
            {
                _scoreHoldTimer -= Time.unscaledDeltaTime;
                if (_scoreHoldTimer <= 0f)
                {
                    _holdingScore = false;
                    SwitchTo(_fpsCamName);
                }
            }
        }

        private void OnPhaseChanged(MatchFlowController.Phase to)
        {
            switch (to)
            {
                case MatchFlowController.Phase.Shot:
                    SwitchTo(_replayCamName);
                    _holdingScore = false;
                    break;

                case MatchFlowController.Phase.Score:
                    _holdingScore   = true;
                    _scoreHoldTimer = _holdAfterScore;
                    break;

                case MatchFlowController.Phase.Possession:
                    _holdingScore = false;
                    SwitchTo(_fpsCamName);
                    break;
            }
        }

        private void SwitchTo(string camName)
        {
            if (_cameraSwitcher != null)
                _cameraSwitcher.SwitchTo(camName);
        }
    }
}
