// TrajectoryRenderer.cs — Predicted trajectory preview line (thin, fading).
// Shows where the ball WILL go. The comet trail shows where it HAS been.
// Uses Unity's LineRenderer. Attach to the ball GameObject.

using System.Collections.Generic;
using UnityEngine;

namespace SoccerBot
{
    [RequireComponent(typeof(LineRenderer))]
    public class TrajectoryRenderer : MonoBehaviour
    {
        [Header("Prediction Line")]
        [SerializeField] private Color _predictionColor = new Color(1f, 0.75f, 0f, 0.25f);
        [SerializeField] private float _lineWidth = 0.04f;
        [SerializeField] private float _fadeDuration = 1.0f;

        private LineRenderer _lineRenderer;
        private float _fadeTimer;
        private bool _isVisible;

        void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            _lineRenderer.startColor = _predictionColor;
            _lineRenderer.endColor = _predictionColor;
            _lineRenderer.startWidth = _lineWidth;
            _lineRenderer.endWidth = _lineWidth * 0.3f;
            _lineRenderer.positionCount = 0;
            _lineRenderer.enabled = false;
            _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            _lineRenderer.sortingOrder = -1;
        }

        public void ShowPrediction(List<Vector3> points)
        {
            if (points == null || points.Count < 2) return;
            _lineRenderer.positionCount = points.Count;
            _lineRenderer.SetPositions(points.ToArray());
            _lineRenderer.enabled = true;
            _isVisible = true;
            _fadeTimer = _fadeDuration;
        }

        public void Clear()
        {
            _lineRenderer.positionCount = 0;
            _lineRenderer.enabled = false;
            _isVisible = false;
        }

        void Update()
        {
            if (!_isVisible) return;
            _fadeTimer -= Time.deltaTime;
            if (_fadeTimer <= 0f)
                Clear();
            else
            {
                float alpha = Mathf.Clamp01(_fadeTimer / _fadeDuration);
                Color c = _predictionColor;
                c.a *= alpha;
                _lineRenderer.startColor = c;
            }
        }
    }
}
