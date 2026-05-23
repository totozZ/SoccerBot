// RobotPathTrail.cs — Renders a trailing line behind the robot to show movement history.
// Uses Unity's built-in TrailRenderer for simplicity.

using UnityEngine;

namespace SoccerBot
{
    public class RobotPathTrail : MonoBehaviour
    {
        [Header("Trail Settings")]
        [SerializeField] private float _trailTime = 3.0f;
        [SerializeField] private float _minDistance = 0.2f;
        [SerializeField] private Color _trailColor = new Color(0f, 1f, 1f, 0.5f);
        [SerializeField] private float _trailWidth = 0.15f;

        [Header("References")]
        [Tooltip("If empty, creates TrailRenderer on this GameObject.")]
        [SerializeField] private TrailRenderer _trailRenderer;

        void Start()
        {
            if (_trailRenderer == null)
            {
                _trailRenderer = GetComponent<TrailRenderer>();
                if (_trailRenderer == null)
                {
                    _trailRenderer = gameObject.AddComponent<TrailRenderer>();
                }
            }

            _trailRenderer.time = _trailTime;
            _trailRenderer.minVertexDistance = _minDistance;
            _trailRenderer.startColor = _trailColor;
            _trailRenderer.endColor = new Color(_trailColor.r, _trailColor.g, _trailColor.b, 0f);
            _trailRenderer.startWidth = _trailWidth;
            _trailRenderer.endWidth = 0f;
            _trailRenderer.material = new Material(Shader.Find("Sprites/Default"));
            _trailRenderer.emitting = true;
        }
    }
}
