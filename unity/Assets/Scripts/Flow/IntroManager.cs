using System.Collections;
using UnityEngine;

namespace SoccerBot
{
    // Drives the intro sequence: show IntroPanel, then hand off to MatchFlowController.
    // Add this to the same GameObject as MatchFlowController (or any persistent GO).
    // Wire _introPanel in Inspector, or it will be found automatically.
    public class IntroManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private IntroPanel _introPanel;
        [SerializeField] private MatchFlowController _matchFlow;

        [Header("Scenario Text (edit per demo)")]
        [SerializeField] private string _line1 = "2014 FIFA World Cup Final";
        [SerializeField] private string _line2 = "Extra Time · 113' · Germany 0 – 0 Argentina";
        [SerializeField] private string _line3 = "The ball is at your feet.  Make history.";

        [Header("Behaviour")]
        [Tooltip("If true, skip intro and go straight to match (useful during dev).")]
        [SerializeField] private bool _skipIntro = false;

        void Start()
        {
            if (_introPanel == null)
                _introPanel = FindFirstObjectByType<IntroPanel>(FindObjectsInactive.Include);
            if (_matchFlow == null)
                _matchFlow = FindFirstObjectByType<MatchFlowController>();

            // Pause match loop until intro finishes
            if (_matchFlow != null) _matchFlow.enabled = false;

            if (_skipIntro)
            {
                StartMatch();
                return;
            }

            if (_introPanel != null)
            {
                _introPanel.OnIntroDone += StartMatch;
                _introPanel.Show(_line1, _line2, _line3);
            }
            else
            {
                StartMatch();
            }
        }

        private void StartMatch()
        {
            if (_introPanel != null) _introPanel.OnIntroDone -= StartMatch;
            if (_matchFlow != null) _matchFlow.enabled = true;
        }
    }
}
