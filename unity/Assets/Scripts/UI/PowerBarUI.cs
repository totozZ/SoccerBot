// PowerBarUI.cs — Visualizes the player's charge power as a bottom-screen bar.
// Subscribes to FPSPlayerController.OnChargeChanged.
//
// Hookup in Editor:
//   - Place on a Canvas child anchored bottom-center
//   - Add an Image with Type=Filled, FillMethod=Horizontal as _fillImage
//   - Wire _player to the Teammate's FPSPlayerController
//   - (optional) Wire _label TMP_Text saying "POWER"

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerBot
{
    public class PowerBarUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FPSPlayerController _player;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Image _fillImage;
        [SerializeField] private TMP_Text _label;

        [Header("Colors")]
        [SerializeField] private Color _lowColor  = new Color(0.30f, 0.85f, 0.40f);
        [SerializeField] private Color _midColor  = new Color(0.95f, 0.85f, 0.20f);
        [SerializeField] private Color _highColor = new Color(0.95f, 0.30f, 0.20f);

        [Header("Timing")]
        [SerializeField] private float _fadeIn = 0.1f;
        [SerializeField] private float _fadeOut = 0.4f;
        [SerializeField] private float _holdAfterRelease = 0.2f;

        private Coroutine _fadeRoutine;

        void Start()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_fillImage == null)
            {
                var t = transform.Find("PowerBarFill");
                if (t != null) _fillImage = t.GetComponent<Image>();
            }
            if (_label == null)
            {
                var t = transform.Find("PowerBarLabel");
                if (t != null) _label = t.GetComponent<TMP_Text>();
            }
            if (_player == null) _player = FindFirstObjectByType<FPSPlayerController>();

            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            if (_fillImage != null) _fillImage.fillAmount = 0f;
            if (_player != null)
            {
                _player.OnChargeBegin += HandleBegin;
                _player.OnChargeChanged += HandleCharge;
                _player.OnChargeCancel += HandleCancel;
                _player.OnShoot += HandleShot;
            }
            else
            {
                Debug.LogWarning("[PowerBarUI] No FPSPlayerController reference.");
            }
            if (_label != null) _label.text = "POWER";
        }

        void OnDestroy()
        {
            if (_player != null)
            {
                _player.OnChargeBegin -= HandleBegin;
                _player.OnChargeChanged -= HandleCharge;
                _player.OnChargeCancel -= HandleCancel;
                _player.OnShoot -= HandleShot;
            }
        }

        private void HandleBegin()
        {
            StartFade(1f, _fadeIn, 0f);
        }

        private void HandleCharge(float power01)
        {
            if (_fillImage == null) return;
            _fillImage.fillAmount = power01;
            _fillImage.color = power01 < 0.5f
                ? Color.Lerp(_lowColor, _midColor, power01 / 0.5f)
                : Color.Lerp(_midColor, _highColor, (power01 - 0.5f) / 0.5f);
        }

        private void HandleCancel()
        {
            StartFade(0f, _fadeOut, 0f);
        }

        private void HandleShot(float power01, Vector3 dir)
        {
            // Brief hold so the player sees their final power, then fade.
            StartFade(0f, _fadeOut, _holdAfterRelease);
        }

        private void StartFade(float targetAlpha, float duration, float delay)
        {
            if (_canvasGroup == null) return;
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeTo(targetAlpha, duration, delay));
        }

        private IEnumerator FadeTo(float target, float duration, float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            float start = _canvasGroup.alpha;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            _canvasGroup.alpha = target;
            if (target == 0f && _fillImage != null) _fillImage.fillAmount = 0f;
        }
    }
}
