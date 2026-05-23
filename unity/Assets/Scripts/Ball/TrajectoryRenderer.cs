// TrajectoryRenderer.cs — Renders the predicted ball trajectory as a line.
// Uses Unity's LineRenderer. Attach to the ball GameObject.

using System.Collections.Generic;
using UnityEngine;

namespace SoccerBot
{
    [RequireComponent(typeof(LineRenderer))]
    public class TrajectoryRenderer : MonoBehaviour
    {
        [Header("Line Settings")]
        [SerializeField] private Color _trajectoryColor = new Color(1f, 0.8f, 0f, 0.6f);
        [SerializeField] private float _lineWidth = 0.08f;
        [SerializeField] private float _fadeDuration = 1.5f;

        private LineRenderer _lineRenderer;
        private float _fadeTimer;
        private bool _isVisible;

        // ────────────────────────────────────────────────────
        void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            _lineRenderer.startColor = _trajectoryColor;
            _lineRenderer.endColor = _trajectoryColor;
            _lineRenderer.startWidth = _lineWidth;
            _lineRenderer.endWidth = _lineWidth;
            _lineRenderer.positionCount = 0;
            _lineRenderer.enabled = false;
        }

        /// <summary>Render a trajectory from a list of points.</summary>
        public void RenderTrajectory(List<Vector3> points)
        {
            if (points == null || points.Count < 2) return;

            _lineRenderer.positionCount = points.Count;
            _lineRenderer.SetPositions(points.ToArray());
            _lineRenderer.enabled = true;
            _isVisible = true;
            _fadeTimer = _fadeDuration;
        }

        /// <summary>Clear the trajectory line.</summary>
        public void Clear()
        {
            _lineRenderer.positionCount = 0;
            _lineRenderer.enabled = false;
            _isVisible = false;
        }

        void Update()
        {
            if (!_isVisible) return;

            // Fade out trajectory over time
            _fadeTimer -= Time.deltaTime;
            if (_fadeTimer <= 0f)
            {
                Clear();
            }
            else
            {
                float alpha = Mathf.Clamp01(_fadeTimer / _fadeDuration);
                Color c = _trajectoryColor;
                c.a *= alpha;
                _lineRenderer.startColor = c;
                _lineRenderer.endColor = c;
            }
        }
    }
}
