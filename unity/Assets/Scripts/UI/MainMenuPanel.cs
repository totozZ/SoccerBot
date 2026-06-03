using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace SoccerBot
{
    public class MainMenuPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _trainingButton;
        [SerializeField] private Button _exitButton;

        [Header("Training Placeholder")]
        [SerializeField] private CanvasGroup _trainingCanvasGroup;
        [SerializeField] private Button _trainingBackButton;
        [SerializeField] private GameObject _trainingGoalFrame;
        [SerializeField] private TextMeshProUGUI _trainingStatusLabel;
        [SerializeField] private TrainingModeController _trainingModeController;

        [Header("Flow")]
        [SerializeField] private IntroManager _introManager;

        [Header("Menu BGM")]
        [SerializeField] private AudioSource _bgmSource;
        [SerializeField] private AudioClip _menuBGM;
        [SerializeField, Range(0f, 1f)] private float _bgmVolume = 0.6f;
        [SerializeField] private float _bgmFadeOutTime = 0.8f;

        [Header("Menu SFX")]
        [SerializeField] private AudioSource _uiSfxSource;
        [SerializeField, Range(0f, 1f)] private float _clickVolume = 0.2f;

        [Header("Timing")]
        [SerializeField] private float _fadeInTime = 0.6f;
        [SerializeField] private float _fadeOutTime = 0.5f;

        private bool _isBuilt;

        void Awake()
        {
            AutoResolveRefs();
            EnsureRuntimeUi();
            HideTrainingImmediate();
            SetMenuAlpha(0f, false);
        }

        void Start()
        {
            AutoResolveRefs();
            BindButtons();
            ShowMenu();
        }

        private void AutoResolveRefs()
        {
            if (_introManager == null)
                _introManager = FindFirstObjectByType<IntroManager>(FindObjectsInactive.Include);

            if (_trainingModeController == null)
                _trainingModeController = FindFirstObjectByType<TrainingModeController>(FindObjectsInactive.Include);

            if (_bgmSource == null)
            {
                _bgmSource = gameObject.GetComponent<AudioSource>();
                if (_bgmSource == null) _bgmSource = gameObject.AddComponent<AudioSource>();
                _bgmSource.playOnAwake = false;
                _bgmSource.loop = true;
                _bgmSource.spatialBlend = 0f;
            }

            if (_backgroundImage == null)
                _backgroundImage = gameObject.GetComponent<Image>();

            if (_uiSfxSource == null)
            {
                _uiSfxSource = gameObject.AddComponent<AudioSource>();
                _uiSfxSource.playOnAwake = false;
                _uiSfxSource.loop = false;
                _uiSfxSource.spatialBlend = 0f;
                _uiSfxSource.volume = _clickVolume;
            }
        }

        private void EnsureRuntimeUi()
        {
            if (_isBuilt) return;

            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            if (GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            var background = gameObject.GetComponent<Image>();
            if (background == null) background = gameObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.88f);
            _backgroundImage = background;

            if (_canvasGroup == null)
                _canvasGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            var root = EnsureRectTransform(gameObject);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            if (_startButton == null) _startButton = CreateMenuButton("StartButton", "START", new Vector2(0f, 90f));
            if (_trainingButton == null) _trainingButton = CreateMenuButton("TrainingButton", "TRAINING", new Vector2(0f, 0f));
            if (_exitButton == null) _exitButton = CreateMenuButton("ExitButton", "EXIT", new Vector2(0f, -90f));

            if (_trainingCanvasGroup == null || _trainingBackButton == null)
                BuildTrainingPlaceholder();

            if (_trainingGoalFrame == null)
                _trainingGoalFrame = BuildTrainingGoalFrame();

            EnsureTrainingModeController();
            _isBuilt = true;
        }

        private void BindButtons()
        {
            BindButton(_startButton, OnStartClicked);
            BindButton(_trainingButton, OnTrainingClicked);
            BindButton(_exitButton, OnExitClicked);
            BindButton(_trainingBackButton, OnTrainingBackClicked);
        }

        private void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        public void ShowMenu()
        {
            gameObject.SetActive(true);
            HideTrainingImmediate();
            SetButtonsVisible(true);
            SetBackgroundVisible(true);
            SetButtonsInteractable(true);
            StopAllCoroutines();
            StartCoroutine(FadeInMenu());
            PlayBGM();
        }

        public void ShowTrainingPlaceholder()
        {
            StopAllCoroutines();
            SetMenuAlpha(1f, false);
            SetButtonsVisible(false);
            SetBackgroundVisible(false);
            if (_trainingGoalFrame != null) _trainingGoalFrame.SetActive(true);
            if (_trainingCanvasGroup != null)
            {
                _trainingCanvasGroup.gameObject.SetActive(true);
                _trainingCanvasGroup.alpha = 1f;
                _trainingCanvasGroup.interactable = true;
                _trainingCanvasGroup.blocksRaycasts = true;
            }
            if (_trainingBackButton != null) _trainingBackButton.interactable = true;
        }

        public void HideTrainingImmediate()
        {
            if (_trainingGoalFrame != null) _trainingGoalFrame.SetActive(false);
            if (_trainingCanvasGroup != null)
            {
                _trainingCanvasGroup.alpha = 0f;
                _trainingCanvasGroup.interactable = false;
                _trainingCanvasGroup.blocksRaycasts = false;
                _trainingCanvasGroup.gameObject.SetActive(false);
            }

            _trainingModeController?.ExitTrainingMode();
        }

        private void PlayBGM()
        {
            if (_bgmSource == null || _menuBGM == null) return;
            _bgmSource.clip = _menuBGM;
            _bgmSource.volume = _bgmVolume;
            if (!_bgmSource.isPlaying) _bgmSource.Play();
        }

        private void PlayClick()
        {
            if (_uiSfxSource == null) return;
            _uiSfxSource.volume = _clickVolume;
            _uiSfxSource.PlayOneShot(CreateClickClip(), 1f);
        }

        private void OnStartClicked()
        {
            PlayClick();
            StartCoroutine(StartSequence());
        }

        private void OnTrainingClicked()
        {
            PlayClick();
            StartCoroutine(TrainingSequence());
        }

        private void OnTrainingBackClicked()
        {
            PlayClick();
            HideTrainingImmediate();
            ShowMenu();
        }

        private void OnExitClicked()
        {
            PlayClick();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private IEnumerator StartSequence()
        {
            SetButtonsInteractable(false);
            StartCoroutine(FadeBGMOut());
            yield return FadeOutMenu();
            HideTrainingImmediate();
            _introManager?.BeginMatchFromMenu();
            gameObject.SetActive(false);
        }

        private IEnumerator TrainingSequence()
        {
            SetButtonsInteractable(false);
            StartCoroutine(FadeBGMOut());
            yield return FadeOutMenu();
            ShowTrainingPlaceholder();
            _trainingModeController?.EnterTrainingMode();
        }

        private IEnumerator FadeInMenu()
        {
            SetButtonsInteractable(false);
            float t = 0f;
            while (t < _fadeInTime)
            {
                t += Time.unscaledDeltaTime;
                SetMenuAlpha(Mathf.Clamp01(t / _fadeInTime), false);
                yield return null;
            }
            SetMenuAlpha(1f, true);
            SetButtonsInteractable(true);
        }

        private IEnumerator FadeOutMenu()
        {
            SetMenuAlpha(_canvasGroup != null ? _canvasGroup.alpha : 1f, false);
            float startAlpha = _canvasGroup != null ? _canvasGroup.alpha : 1f;
            float t = 0f;
            while (t < _fadeOutTime)
            {
                t += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(t / _fadeOutTime));
                SetMenuAlpha(alpha, false);
                yield return null;
            }
            SetMenuAlpha(0f, false);
        }

        private void SetMenuAlpha(float alpha, bool interactive)
        {
            if (_canvasGroup == null) return;
            _canvasGroup.alpha = alpha;
            _canvasGroup.interactable = interactive;
            _canvasGroup.blocksRaycasts = interactive;
        }

        private IEnumerator FadeBGMOut()
        {
            if (_bgmSource == null || !_bgmSource.isPlaying) yield break;
            float startVol = _bgmSource.volume;
            float t = 0f;
            while (t < _bgmFadeOutTime)
            {
                t += Time.unscaledDeltaTime;
                _bgmSource.volume = Mathf.Lerp(startVol, 0f, Mathf.Clamp01(t / _bgmFadeOutTime));
                yield return null;
            }
            _bgmSource.Stop();
            _bgmSource.volume = _bgmVolume;
        }

        private void SetButtonsInteractable(bool value)
        {
            if (_startButton != null) _startButton.interactable = value;
            if (_trainingButton != null) _trainingButton.interactable = value;
            if (_exitButton != null) _exitButton.interactable = value;
            if (_trainingBackButton != null) _trainingBackButton.interactable = value;
        }

        private void SetButtonsVisible(bool value)
        {
            if (_startButton != null) _startButton.gameObject.SetActive(value);
            if (_trainingButton != null) _trainingButton.gameObject.SetActive(value);
            if (_exitButton != null) _exitButton.gameObject.SetActive(value);
        }

        private void SetBackgroundVisible(bool value)
        {
            if (_backgroundImage != null)
                _backgroundImage.enabled = value;
        }

        private Button CreateMenuButton(string name, string label, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(420f, 72f);
            rt.anchoredPosition = anchoredPos;

            var image = go.GetComponent<Image>();
            image.color = new Color(0.08f, 0.08f, 0.08f, 0.88f);

            CreateLabel(go.transform, label, 32f, FontStyles.Bold);
            return go.GetComponent<Button>();
        }

        private void BuildTrainingPlaceholder()
        {
            var panel = new GameObject("TrainingPlaceholder", typeof(RectTransform), typeof(CanvasGroup));
            panel.transform.SetParent(transform, false);

            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _trainingCanvasGroup = panel.GetComponent<CanvasGroup>();

            var infoCard = new GameObject("TrainingInfoCard", typeof(RectTransform), typeof(Image));
            infoCard.transform.SetParent(panel.transform, false);

            var cardRt = infoCard.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(1f, 1f);
            cardRt.anchorMax = new Vector2(1f, 1f);
            cardRt.pivot = new Vector2(1f, 1f);
            cardRt.sizeDelta = new Vector2(520f, 360f);
            cardRt.anchoredPosition = new Vector2(-48f, -48f);

            var cardImage = infoCard.GetComponent<Image>();
            cardImage.color = new Color(0f, 0f, 0f, 0.68f);

            CreateLabel(infoCard.transform, "TRAINING MODE", 34f, FontStyles.Bold, new Vector2(0f, 118f), new Vector2(440f, 56f));
            _trainingStatusLabel = CreateLabel(infoCard.transform,
                "Smart football mock stream is preparing...",
                22f, FontStyles.Normal, new Vector2(0f, 8f), new Vector2(440f, 180f));

            _trainingBackButton = CreatePanelButton(infoCard.transform, "BackButton", "BACK TO MENU", new Vector2(0f, -118f));
            EnsureTrainingModeController();
        }

        private Button CreatePanelButton(Transform parent, string name, string label, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(320f, 56f);
            rt.anchoredPosition = anchoredPos;

            var image = go.GetComponent<Image>();
            image.color = new Color(0.12f, 0.12f, 0.12f, 0.92f);

            CreateLabel(go.transform, label, 24f, FontStyles.Bold, Vector2.zero, new Vector2(300f, 48f));
            return go.GetComponent<Button>();
        }

        private TextMeshProUGUI CreateLabel(Transform parent, string text, float fontSize, FontStyles fontStyle,
            Vector2? anchoredPos = null, Vector2? size = null)
        {
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(parent, false);

            var rt = labelGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size ?? new Vector2(420f, 72f);
            rt.anchoredPosition = anchoredPos ?? Vector2.zero;

            var tmp = labelGo.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = fontStyle;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true;
            return tmp;
        }

        private void EnsureTrainingModeController()
        {
            if (_trainingModeController == null)
            {
                var controllerGo = GameObject.Find("TrainingModeController");
                if (controllerGo == null)
                    controllerGo = new GameObject("TrainingModeController");
                _trainingModeController = controllerGo.GetComponent<TrainingModeController>() ?? controllerGo.AddComponent<TrainingModeController>();
            }

            _trainingModeController.Configure(_trainingCanvasGroup, _trainingGoalFrame, _trainingStatusLabel, this);
        }

        private static RectTransform EnsureRectTransform(GameObject go)
        {
            return go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        }

        private GameObject BuildTrainingGoalFrame()
        {
            var root = new GameObject("TrainingGoalFrame");
            root.transform.position = new Vector3(0f, 1.2f, -8.8f);

            CreateGoalBar(root.transform, "LeftPost", new Vector3(-2f, 1.2f, 0f), new Vector3(0.12f, 2.4f, 0.12f));
            CreateGoalBar(root.transform, "RightPost", new Vector3(2f, 1.2f, 0f), new Vector3(0.12f, 2.4f, 0.12f));
            CreateGoalBar(root.transform, "Crossbar", new Vector3(0f, 2.4f, 0f), new Vector3(4.12f, 0.12f, 0.12f));
            root.SetActive(false);
            return root;
        }

        private void CreateGoalBar(Transform parent, string name, Vector3 localPos, Vector3 localScale)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = name;
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = localPos;
            bar.transform.localScale = localScale;

            var renderer = bar.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.material.color = Color.white;
            }
        }

        private AudioClip CreateClickClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.05f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float phase = Random.Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Exp(-45f * t);
                float freq = Mathf.Lerp(1500f, 700f, t / duration);
                phase += 2f * Mathf.PI * freq / sampleRate;
                samples[i] = Mathf.Sin(phase) * envelope * 0.25f;
            }

            var clip = AudioClip.Create("menu-click", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
