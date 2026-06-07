using System.Collections;
using UnityEngine;

namespace SoccerBot
{
    public class ReceptionTargetIndicator : MonoBehaviour
    {
        [Header("Ring")]
        [SerializeField] private float _radius = 0.46f;
        [SerializeField] private float _lineWidth = 0.035f;
        [SerializeField] private int _segments = 64;
        [SerializeField] private float _heightOffset = 0.035f;
        [SerializeField] private float _pulseAmount = 0.12f;

        private LineRenderer _ring;
        private Coroutine _hideRoutine;

        public void Show(Vector3 worldPosition)
        {
            EnsureRing();
            CancelHide();
            transform.position = worldPosition + Vector3.up * _heightOffset;
            transform.rotation = Quaternion.identity;
            gameObject.SetActive(true);
            SetColor(new Color(0.65f, 0.85f, 1f, 0.95f));
            transform.localScale = Vector3.one;
        }

        public void UpdateProgress(float progress01, float windowStart01, float perfect01, float windowEnd01)
        {
            EnsureRing();
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            Color color;
            if (progress01 < windowStart01)
            {
                color = new Color(0.65f, 0.85f, 1f, 0.75f);
            }
            else if (progress01 <= windowEnd01)
            {
                float perfectPulse = 1f - Mathf.Clamp01(Mathf.Abs(progress01 - perfect01) / 0.12f);
                color = Color.Lerp(new Color(1f, 0.9f, 0.18f, 1f), new Color(0.18f, 1f, 0.35f, 1f), perfectPulse);
            }
            else
            {
                color = new Color(1f, 0.25f, 0.18f, 0.9f);
            }

            SetColor(color);
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 12f) * _pulseAmount * Mathf.Clamp01(progress01);
            transform.localScale = Vector3.one * pulse;
        }

        public void ShowFeedback(float quality01, bool attempted)
        {
            EnsureRing();
            CancelHide();

            Color color = !attempted || quality01 < 0.2f
                ? new Color(1f, 0.2f, 0.12f, 1f)
                : quality01 >= 0.78f
                    ? new Color(1f, 0.84f, 0.18f, 1f)
                    : new Color(0.28f, 0.95f, 1f, 1f);

            SetColor(color);
            transform.localScale = Vector3.one * (attempted && quality01 >= 0.78f ? 1.24f : 1.05f);
            gameObject.SetActive(true);
            _hideRoutine = StartCoroutine(HideAfter(1.1f));
        }

        public void Hide()
        {
            CancelHide();
            gameObject.SetActive(false);
        }

        private void EnsureRing()
        {
            if (_ring != null) return;

            _ring = gameObject.AddComponent<LineRenderer>();
            _ring.loop = true;
            _ring.useWorldSpace = false;
            _ring.positionCount = Mathf.Max(12, _segments);
            _ring.widthMultiplier = _lineWidth;
            _ring.numCornerVertices = 4;
            _ring.numCapVertices = 4;
            _ring.material = new Material(Shader.Find("Sprites/Default"));

            int count = _ring.positionCount;
            for (int i = 0; i < count; i++)
            {
                float angle = (Mathf.PI * 2f * i) / count;
                _ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * _radius, 0f, Mathf.Sin(angle) * _radius));
            }
        }

        private void SetColor(Color color)
        {
            if (_ring == null) return;
            _ring.startColor = color;
            _ring.endColor = color;
        }

        private IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            gameObject.SetActive(false);
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
