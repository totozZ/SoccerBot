using System;
using UnityEngine;

namespace SoccerBot
{
    public class IntroManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private IntroPanel _introPanel;
        [SerializeField] private MatchFlowController _matchFlow;
        [SerializeField] private MainMenuPanel _mainMenu;
        [SerializeField] private AudioManager _audioManager;

        [Header("Scenario Text (edit per demo)")]
        [SerializeField] private string _line1 = "2014 FIFA World Cup Final";
        [SerializeField] private string _line2 = "Extra Time · 113' · Germany 0 – 0 Argentina";
        [SerializeField] private string _line3 = "The ball is at your feet.  Make history.";

        [Header("Behaviour")]
        [Tooltip("If true, skip intro and go straight to match once START is pressed.")]
        [SerializeField] private bool _skipIntro = false;

        private bool _isMatchStarting;

        void Start()
        {
            if (_introPanel == null)
                _introPanel = FindFirstObjectByType<IntroPanel>(FindObjectsInactive.Include);
            if (_matchFlow == null)
                _matchFlow = FindFirstObjectByType<MatchFlowController>(FindObjectsInactive.Include);
            if (_mainMenu == null)
                _mainMenu = FindFirstObjectByType<MainMenuPanel>(FindObjectsInactive.Include);
            if (_audioManager == null)
                _audioManager = FindFirstObjectByType<AudioManager>(FindObjectsInactive.Include);

            if (_matchFlow != null)
            {
                _matchFlow.ResetForMenu();
            }

            if (_introPanel != null)
            {
                _introPanel.OnIntroDone -= StartMatch;
                _introPanel.gameObject.SetActive(false);
            }

            if (_mainMenu == null)
            {
                var menuGo = new GameObject("MainMenu");
                _mainMenu = menuGo.AddComponent<MainMenuPanel>();
            }
        }

        void OnDestroy()
        {
            if (_introPanel != null) _introPanel.OnIntroDone -= StartMatch;
        }

        public void BeginMatchFromMenu()
        {
            if (_isMatchStarting) return;
            _isMatchStarting = true;

            if (_matchFlow != null)
                _matchFlow.PrepareForMatchStart();

            if (_skipIntro || _introPanel == null)
            {
                StartMatch();
                return;
            }

            _introPanel.OnIntroDone -= StartMatch;
            _introPanel.OnIntroDone += StartMatch;
            _introPanel.Show(_line1, _line2, _line3);
        }

        private void StartMatch()
        {
            if (_introPanel != null) _introPanel.OnIntroDone -= StartMatch;
            if (_audioManager != null) _audioManager.StopBGM();
            if (_matchFlow != null) _matchFlow.BeginMatch();
            _isMatchStarting = false;
        }
    }
}
