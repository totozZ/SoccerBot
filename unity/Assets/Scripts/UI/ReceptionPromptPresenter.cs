using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerBot
{
    public class ReceptionPromptPresenter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _viewTransform;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _body;
        [SerializeField] private Image _backing;

        [Header("Layout")]
        [SerializeField] private bool _followViewInWorldSpace = true;
        [SerializeField] private Vector3 _viewOffset = new(0f, -0.22f, 1.05f);
        [SerializeField] private float _worldScale = 0.0016f;
        [SerializeField] private Vector2 _panelSize = new(720f, 168f);

        [Header("Timing")]
        [SerializeField] private float _fadeSpeed = 10f;
        [SerializeField] private float _feedbackHoldSeconds = 1.2f;

        private Coroutine _hideRoutine;
        private Color _targetColor = Color.white;

        public void Configure(Transform viewTransform)
        {
            if (_viewTransform == null)
                _viewTransform = viewTransform;
        }

        public void ShowPassProgress(float progress01, float windowStart01, float perfect01, float windowEnd01, bool questControls)
        {
            EnsureUi();
            CancelHide();

            string inputText = questControls ? "Grip or Trigger" : "Space / Left Click";
            if (progress01 < windowStart01)
            {
                SetPrompt("READY TO RECEIVE", inputText, new Color(0.65f, 0.85f, 1f, 1f));
            }
            else if (progress01 <= windowEnd01)
            {
                float perfectPulse = 1f - Mathf.Clamp01(Mathf.Abs(progress01 - perfect01) / 0.12f);
                Color c = Color.Lerp(new Color(1f, 0.92f, 0.25f, 1f), new Color(0.2f, 1f, 0.42f, 1f), perfectPulse);
                SetPrompt("CATCH NOW", inputText, c);
            }
            else
            {
                SetPrompt("MISSED WINDOW", "Recover the ball", new Color(1f, 0.28f, 0.22f, 1f));
            }

            Show();
        }

        public void ShowReceiveFeedback(float quality01, bool attempted, float holdSeconds = -1f)
        {
            EnsureUi();
            CancelHide();

            if (!attempted)
            {
                SetPrompt("MISSED TOUCH", "No receive input", new Color(1f, 0.25f, 0.18f, 1f));
            }
            else if (quality01 >= 0.78f)
            {
                SetPrompt("PERFECT FIRST TOUCH", "Shot chance boosted", new Color(1f, 0.84f, 0.18f, 1f));
            }
            else if (quality01 >= 0.45f)
            {
                SetPrompt("STABLE RECEIVE", "Keep possession", new Color(0.35f, 0.8f, 1f, 1f));
            }
            else
            {
                SetPrompt("POOR TOUCH", "Opponent pressure", new Color(1f, 0.32f, 0.22f, 1f));
            }

            Show();
            _hideRoutine = StartCoroutine(HideAfter(holdSeconds > 0f ? holdSeconds : _feedbackHoldSeconds));
        }

        public void ShowPossession(float quality01, bool questControls)
        {
            EnsureUi();
            CancelHide();

            string body = questControls
                ? "Hold RT, pull back, swing forward"
                : "Hold shot input, release to fire";
            string bonus = quality01 >= 0.78f ? " + first-touch boost" : string.Empty;
            SetPrompt("POSSESSION", body + bonus, new Color(0.25f, 1f, 0.48f, 1f));
            Show();
            _hideRoutine = StartCoroutine(HideAfter(2.0f));
        }

        public void ShowRecovery()
        {
            EnsureUi();
            CancelHide();
            SetPrompt("FIGHT FOR IT", "Mash the receive input", new Color(1f, 0.25f, 0.18f, 1f));
            Show();
        }

        public void ShowShotBias(float receiveBias)
        {
            EnsureUi();
            CancelHide();

            if (receiveBias > 0.03f)
                SetPrompt("FIRST TOUCH BOOST", $"+{receiveBias * 100f:0}% shot chance", new Color(1f, 0.84f, 0.18f, 1f));
            else if (receiveBias < -0.03f)
                SetPrompt("PRESSURED SHOT", $"{receiveBias * 100f:0}% shot chance", new Color(1f, 0.35f, 0.22f, 1f));
            else
                SetPrompt("SHOT RELEASED", "Neutral first touch", new Color(0.65f, 0.85f, 1f, 1f));

            Show();
            _hideRoutine = StartCoroutine(HideAfter(1.0f));
        }

        public void Hide()
        {
            EnsureUi();
            CancelHide();
            if (_group != null) _group.alpha = 0f;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (_canvas == null || !_canvas.gameObject.activeSelf) return;

            if (_followViewInWorldSpace && _viewTransform != null)
            {
                Transform canvasTransform = _canvas.transform;
                canvasTransform.position = _viewTransform.position
                    + _viewTransform.forward * _viewOffset.z
                    + _viewTransform.right * _viewOffset.x
                    + _viewTransform.up * _viewOffset.y;
                canvasTransform.rotation = Quaternion.LookRotation(_viewTransform.forward, _viewTransform.up);
            }

            if (_group != null)
                _group.alpha = Mathf.MoveTowards(_group.alpha, 1f, Time.unscaledDeltaTime * _fadeSpeed);
        }

        private void EnsureUi()
        {
            if (_canvas != null) return;

            var root = new GameObject("ReceptionPromptCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            _canvas = root.GetComponent<Canvas>();
            _group = root.GetComponent<CanvasGroup>();
            _group.alpha = 0f;

            if (_followViewInWorldSpace)
            {
                _canvas.renderMode = RenderMode.WorldSpace;
                root.transform.localScale = Vector3.one * _worldScale;
            }
            else
            {
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            _canvas.sortingOrder = 40;
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = _panelSize;
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = new Vector2(0f, -180f);

            var backingGo = new GameObject("Backing", typeof(RectTransform), typeof(Image));
            backingGo.transform.SetParent(root.transform, false);
            RectTransform backingRect = backingGo.GetComponent<RectTransform>();
            backingRect.anchorMin = Vector2.zero;
            backingRect.anchorMax = Vector2.one;
            backingRect.offsetMin = Vector2.zero;
            backingRect.offsetMax = Vector2.zero;
            _backing = backingGo.GetComponent<Image>();
            _backing.color = new Color(0f, 0f, 0f, 0.58f);

            _title = CreateText("Title", root.transform, 42f, new Vector2(0f, 24f), new Vector2(_panelSize.x - 44f, 64f), FontStyles.Bold);
            _body = CreateText("Body", root.transform, 25f, new Vector2(0f, -38f), new Vector2(_panelSize.x - 44f, 54f), FontStyles.Normal);
            _canvas.gameObject.SetActive(false);
        }

        private TMP_Text CreateText(string name, Transform parent, float size, Vector2 pos, Vector2 rectSize, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = rectSize;
            rt.anchoredPosition = pos;

            TMP_Text text = go.GetComponent<TMP_Text>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = size;
            text.fontStyle = style;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = Color.white;
            return text;
        }

        private void SetPrompt(string title, string body, Color color)
        {
            _targetColor = color;
            if (_title != null)
            {
                _title.text = title;
                _title.color = _targetColor;
            }
            if (_body != null)
            {
                _body.text = body;
                _body.color = Color.white;
            }
            if (_backing != null)
                _backing.color = new Color(color.r * 0.08f, color.g * 0.08f, color.b * 0.08f, 0.62f);
        }

        private void Show()
        {
            if (_canvas != null && !_canvas.gameObject.activeSelf)
                _canvas.gameObject.SetActive(true);
        }

        private IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (_group != null)
                _group.alpha = 0f;
            if (_canvas != null)
                _canvas.gameObject.SetActive(false);
            _hideRoutine = null;
        }

        private void CancelHide()
        {
            if (_hideRoutine == null) return;
            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }
    }
}
