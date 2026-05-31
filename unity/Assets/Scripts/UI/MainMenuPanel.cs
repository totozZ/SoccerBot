// MainMenuPanel.cs — Full-screen black main menu shown on app launch.
// Three buttons: START (→ IntroManager.BeginIntro), HOW TO PLAY (→ HowToPlayPanel), EXIT.
// World Space Canvas so Quest XR Ray Interactor can click buttons via ray casting.
// Manages its own menu BGM via an independent AudioSource (does not touch AudioManager).

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SoccerBot
{
    public class MainMenuPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _howToPlayButton;
        [SerializeField] private Button _exitButton;
        [SerializeField] private HowToPlayPanel _howToPlayPanel;

        [Header("Flow")]
        [SerializeField] private IntroManager _introManager;

        [Header("Menu BGM")]
        [SerializeField] private AudioSource _bgmSource;
        [SerializeField] private AudioClip _menuBGM;
        [SerializeField, Range(0f, 1f)] private float _bgmVolume = 0.6f;
        [SerializeField] private float _bgmFadeOutTime = 0.8f;

        [Header("Timing")]
        [SerializeField] private float _fadeInTime  = 0.6f;
        [SerializeField] private float _fadeOutTime = 0.5f;

        void Awake()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        void Start()
        {
            AutoResolveRefs();

            if (_startButton    != null) _startButton.onClick.AddListener(OnStartClicked);
            if (_howToPlayButton != null) _howToPlayButton.onClick.AddListener(OnHowToPlayClicked);
            if (_exitButton     != null) _exitButton.onClick.AddListener(OnExitClicked);

            StartCoroutine(FadeIn());
            PlayBGM();
        }

        private void AutoResolveRefs()
        {
            if (_introManager == null)
                _introManager = FindFirstObjectByType<IntroManager>(FindObjectsInactive.Include);
            if (_howToPlayPanel == null)
                _howToPlayPanel = FindFirstObjectByType<HowToPlayPanel>(FindObjectsInactive.Include);
            if (_bgmSource == null)
            {
                _bgmSource = gameObject.AddComponent<AudioSource>();
                _bgmSource.playOnAwake = false;
                _bgmSource.loop = true;
                _bgmSource.spatialBlend = 0f;
            }
        }

        private void PlayBGM()
        {
            if (_bgmSource == null || _menuBGM == null) return;
            _bgmSource.clip = _menuBGM;
            _bgmSource.volume = _bgmVolume;
            _bgmSource.Play();
        }

        private void OnStartClicked()
        {
            StartCoroutine(StartSequence());
        }

        private void OnHowToPlayClicked()
        {
            if (_howToPlayPanel != null) _howToPlayPanel.Show();
        }

        private void OnExitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private IEnumerator StartSequence()
        {
            // Disable buttons immediately to prevent double-click.
            SetButtonsInteractable(false);

            // Fade out menu and BGM in parallel.
            StartCoroutine(FadeBGMOut());
            yield return FadeOut();

            gameObject.SetActive(false);

            // MainMenuPanel is disabled — no-op until re-enabled
        }

        private IEnumerator FadeIn()
        {
            float t = 0f;
            while (t < _fadeInTime)
            {
                t += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(t / _fadeInTime);
                yield return null;
            }
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }

        private IEnumerator FadeOut()
        {
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            float t = 0f;
            while (t < _fadeOutTime)
            {
                t += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(1f - t / _fadeOutTime);
                yield return null;
            }
            _canvasGroup.alpha = 0f;
        }

        private IEnumerator FadeBGMOut()
        {
            if (_bgmSource == null) yield break;
            float startVol = _bgmSource.volume;
            float t = 0f;
            while (t < _bgmFadeOutTime)
            {
                t += Time.unscaledDeltaTime;
                _bgmSource.volume = Mathf.Lerp(startVol, 0f, t / _bgmFadeOutTime);
                yield return null;
            }
            _bgmSource.Stop();
        }

        private void SetButtonsInteractable(bool value)
        {
            if (_startButton     != null) _startButton.interactable     = value;
            if (_howToPlayButton != null) _howToPlayButton.interactable = value;
            if (_exitButton      != null) _exitButton.interactable      = value;
        }
    }
}
