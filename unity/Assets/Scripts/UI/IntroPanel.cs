using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

namespace SoccerBot
{
    // Full-screen black intro canvas. IntroManager drives it via Show() / Hide().
    public class IntroPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TextMeshProUGUI _line1;   // match name
        [SerializeField] private TextMeshProUGUI _line2;   // time / context
        [SerializeField] private TextMeshProUGUI _line3;   // key player / hint

        [Header("Timing")]
        [SerializeField] private float _fadeInTime  = 0.8f;
        [SerializeField] private float _lineDelay   = 1.2f;   // gap between each line appearing
        [SerializeField] private float _holdTime    = 3.5f;   // how long all lines stay visible
        [SerializeField] private float _fadeOutTime = 0.8f;

        public event Action OnIntroDone;

        private Coroutine _routine;

        void Awake()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            SetAlpha(0f);
            // Do NOT SetActive(false) here — IntroManager.Start() calls Show() immediately,
            // and a Coroutine cannot start on an inactive GameObject.
        }

        public void Show(string line1, string line2, string line3)
        {
            if (_routine != null) StopCoroutine(_routine);
            gameObject.SetActive(true);   // must be active before StartCoroutine
            _routine = StartCoroutine(RunIntro(line1, line2, line3));
        }

        public void SkipToEnd()
        {
            if (_routine != null) StopCoroutine(_routine);
            StartCoroutine(FadeOut());
        }

        private IEnumerator RunIntro(string l1, string l2, string l3)
        {
            SetText("", "", "");
            SetAlpha(0f);

            // Fade in black background
            yield return Fade(0f, 1f, _fadeInTime);

            // Reveal lines one by one
            if (_line1 != null) _line1.text = l1;
            yield return new WaitForSecondsRealtime(_lineDelay);
            if (_line2 != null) _line2.text = l2;
            yield return new WaitForSecondsRealtime(_lineDelay);
            if (_line3 != null) _line3.text = l3;

            yield return new WaitForSecondsRealtime(_holdTime);

            yield return FadeOut();
        }

        private IEnumerator FadeOut()
        {
            yield return Fade(1f, 0f, _fadeOutTime);
            gameObject.SetActive(false);
            OnIntroDone?.Invoke();
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            SetAlpha(to);
        }

        private void SetAlpha(float a)
        {
            if (_canvasGroup != null) _canvasGroup.alpha = a;
        }

        private void SetText(string l1, string l2, string l3)
        {
            if (_line1 != null) _line1.text = l1;
            if (_line2 != null) _line2.text = l2;
            if (_line3 != null) _line3.text = l3;
        }

        private InputAction _skipAction;

        void OnEnable()
        {
            _skipAction = new InputAction("SkipIntro", InputActionType.Button);
            _skipAction.AddBinding("<Keyboard>/anyKey");
            _skipAction.AddBinding("<XRController>{RightHand}/triggerPressed");
            _skipAction.AddBinding("<XRController>{RightHand}/primaryButton");
            _skipAction.AddBinding("<XRController>{LeftHand}/triggerPressed");
            _skipAction.Enable();
        }

        void OnDisable()
        {
            if (_skipAction != null) { _skipAction.Disable(); _skipAction.Dispose(); _skipAction = null; }
        }

        void Update()
        {
            if (!gameObject.activeSelf) return;
            if (_skipAction != null && _skipAction.WasPressedThisFrame())
                SkipToEnd();
        }
    }
}
