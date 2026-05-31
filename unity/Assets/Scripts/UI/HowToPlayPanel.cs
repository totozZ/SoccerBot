// HowToPlayPanel.cs — Tutorial overlay shown from MainMenuPanel.
// Fades in/out over the main menu. Close button returns to menu.

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SoccerBot
{
    public class HowToPlayPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TextMeshProUGUI _contentText;

        [Header("Content (edit in Inspector)")]
        [SerializeField, TextArea(10, 20)] private string _howToPlayText =
            "HOW TO PLAY\n" +
            "─────────────────────────────\n\n" +
            "PC\n" +
            "  Hold LMB        Charge shot\n" +
            "  Release LMB     Shoot\n" +
            "  Right-click drag   Look around\n" +
            "  WASD            Move\n\n" +
            "VR (Quest)\n" +
            "  Hold A button   Charge shot\n" +
            "  Release A       Shoot\n" +
            "  Head tracking   Look around\n\n" +
            "─────────────────────────────\n" +
            "Press any key / A to skip intro";

        [Header("Timing")]
        [SerializeField] private float _fadeTime = 0.3f;

        void Awake()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        void Start()
        {
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
            if (_contentText != null) _contentText.text = _howToPlayText;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            if (gameObject.activeInHierarchy)
                StartCoroutine(Fade(0f, 1f, true));
        }

        public void Hide()
        {
            StartCoroutine(FadeAndHide());
        }

        private IEnumerator FadeAndHide()
        {
            yield return Fade(1f, 0f, false);
            gameObject.SetActive(false);
        }

        private IEnumerator Fade(float from, float to, bool interactive)
        {
            if (interactive)
            {
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }
            float t = 0f;
            while (t < _fadeTime)
            {
                t += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / _fadeTime));
                yield return null;
            }
            _canvasGroup.alpha = to;
            if (!interactive)
            {
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
        }
    }
}
