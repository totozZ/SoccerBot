// BallController.cs — Controls the soccer ball GameObject.
// Attach to the ball GameObject in the scene.

using UnityEngine;

namespace SoccerBot
{
    public class BallController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Optional: manually assign ball transform.")]
        [SerializeField] private Transform _ballTransform;

        [Header("Visuals")]
        [SerializeField] private bool _showWhileIdle = false;
        [SerializeField] private Vector3 _idlePosition = new Vector3(0f, -10f, 0f);

        [Header("Trajectory")]
        [SerializeField] private bool _drawTrajectory = true;

        private TrajectoryRenderer _trajectoryRenderer;
        private bool _ballActive;

        // ────────────────────────────────────────────────────
        void Start()
        {
            if (_ballTransform == null)
                _ballTransform = transform;

            _trajectoryRenderer = GetComponent<TrajectoryRenderer>();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnShotFired += OnShotFired;
            }

            // Hide ball initially
            SetBallVisible(_showWhileIdle);
            if (!_showWhileIdle)
                _ballTransform.position = _idlePosition;
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnShotFired -= OnShotFired;
            }
        }

        void Update()
        {
            if (GameManager.Instance == null) return;

            var ballData = GameManager.Instance.Ball;

            if (ballData.justFired)
            {
                SetBallVisible(true);
                _ballActive = true;

                if (_trajectoryRenderer != null && ballData.trajectory != null)
                {
                    _trajectoryRenderer.RenderTrajectory(ballData.trajectory);
                }
            }

            if (_ballActive)
            {
                _ballTransform.position = ballData.position;
            }
        }

        private void SetBallVisible(bool visible)
        {
            var renderer = GetComponent<Renderer>();
            if (renderer != null)
                renderer.enabled = visible;
        }

        private void OnShotFired()
        {
            SetBallVisible(true);
            _ballActive = true;
        }
    }
}
