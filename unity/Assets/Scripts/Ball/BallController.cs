// BallController.cs — Controls the soccer ball GameObject.
// Comet trail (TrailRenderer) = where the ball HAS been.
// Prediction line (LineRenderer) = where the ball WILL go.

using UnityEngine;

namespace SoccerBot
{
    [RequireComponent(typeof(TrailRenderer))]
    public class BallController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _ballTransform;

        [Header("Comet Tail — where the ball HAS been")]
        [SerializeField] private float _trailTime = 0.8f;
        [SerializeField] private float _trailStartWidth = 0.18f;
        [SerializeField] private float _trailEndWidth = 0.0f;
        [SerializeField] private Color _trailColor = new Color(1f, 0.85f, 0.1f, 1f);

        [Header("Visuals")]
        [SerializeField] private bool _showWhileIdle = false;
        [SerializeField] private Vector3 _idlePosition = new Vector3(0f, -10f, 0f);

        [Header("Prediction Line — where the ball WILL go")]
        [SerializeField] private bool _drawPrediction = true;

        private TrailRenderer _cometTrail;
        private TrajectoryRenderer _trajectoryRenderer;
        private bool _ballActive;
        private bool _externalControl;

        void Awake()
        {
            if (_ballTransform == null) _ballTransform = transform;

            _cometTrail = GetComponent<TrailRenderer>();
            _cometTrail.time = _trailTime;
            _cometTrail.startWidth = _trailStartWidth;
            _cometTrail.endWidth = _trailEndWidth;
            _cometTrail.startColor = _trailColor;
            _cometTrail.endColor = new Color(_trailColor.r, _trailColor.g, _trailColor.b, 0f);
            _cometTrail.material = new Material(Shader.Find("Sprites/Default"));
            _cometTrail.minVertexDistance = 0.05f;
            _cometTrail.emitting = false;

            _trajectoryRenderer = GetComponent<TrajectoryRenderer>();
        }

        void Start()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnShotFired += OnShotFired;

            SetBallVisible(_showWhileIdle);
            if (!_showWhileIdle)
                _ballTransform.position = _idlePosition;
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnShotFired -= OnShotFired;
        }

        void Update()
        {
            if (_externalControl) return;
            if (GameManager.Instance == null) return;

            var ballData = GameManager.Instance.Ball;

            // New shot: only show prediction line.
            // Comet trail is handled by OnShotFired — avoids stale-position trail.
            if (ballData.justFired)
            {
                SetBallVisible(true);
                _ballActive = true;
                if (_drawPrediction && _trajectoryRenderer != null && ballData.trajectory != null)
                    _trajectoryRenderer.ShowPrediction(ballData.trajectory);
            }

            if (_ballActive)
                _ballTransform.position = ballData.position;
        }

        public void HideBall()
        {
            _ballActive = false;
            SetBallVisible(false);
            _cometTrail.emitting = false;
            _cometTrail.Clear();
            if (_trajectoryRenderer != null) _trajectoryRenderer.Clear();
        }

        // ── Possession (MatchFlow) ──────────────────────────
        // Lightweight parenting helpers so MatchFlowController can pin the ball
        // to the robot or the player without going through ScenarioPlayer.

        public void AttachTo(Transform parent, Vector3 localOffset)
        {
            transform.SetParent(parent, true);
            transform.localPosition = localOffset;
            transform.localRotation = Quaternion.identity;
            SetBallVisible(true);
            _cometTrail.Clear();
            _cometTrail.emitting = false;
        }

        public void Detach()
        {
            transform.SetParent(null, true);
        }

        // ── External control (ScenarioPlayer) ───────────────
        // While external control is active, Update() suppresses GameManager-driven
        // pose updates. ScenarioPlayer drives _ballTransform directly.

        public void BeginExternalControl()
        {
            _externalControl = true;
            _ballActive = false;
            SetBallVisible(true);
            _cometTrail.Clear();
            _cometTrail.emitting = true;
            if (_trajectoryRenderer != null) _trajectoryRenderer.Clear();
        }

        public void EndExternalControl()
        {
            _externalControl = false;
            _cometTrail.emitting = false;
            HideBall();
            _ballTransform.position = _idlePosition;
        }

        private void SetBallVisible(bool visible)
        {
            var r = GetComponent<Renderer>();
            if (r != null) r.enabled = visible;
        }

        // Called by GameManager when isFiring rising edge (AFTER data is set).
        private void OnShotFired()
        {
            if (_externalControl) return;
            SetBallVisible(true);
            _ballActive = true;

            // Immediately jump to launch position — kills any trail from old position
            if (GameManager.Instance != null)
                _ballTransform.position = GameManager.Instance.Ball.position;

            _cometTrail.Clear();
            _cometTrail.emitting = true;
        }
    }
}
