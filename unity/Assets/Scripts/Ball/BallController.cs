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
        [SerializeField] private GameObject _visualPrefab;
        [SerializeField] private float _visualScale = 1f;
        [SerializeField] private bool _hideSourceRendererWhenVisualPrefab = false;

        [Header("Prediction Line — where the ball WILL go")]
        [SerializeField] private bool _drawPrediction = true;

        private TrailRenderer _cometTrail;
        private TrajectoryRenderer _trajectoryRenderer;
        private MeshRenderer _sourceRenderer;
        private Renderer[] _visualRenderers;
        private Rigidbody _physicsBody;
        private SphereCollider _physicsCollider;
        private bool _ballActive;
        private bool _externalControl;
        private bool _physicsSimulation;

        void Awake()
        {
            if (_ballTransform == null) _ballTransform = transform;

            _sourceRenderer = GetComponent<MeshRenderer>();
            EnsureVisualModel();
            ResolvePhysicsComponents();

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
            if (_externalControl || _physicsSimulation) return;
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
            SetPhysicalSimulation(false);
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
            SetPhysicalSimulation(false);
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
            SetPhysicalSimulation(false);
            _externalControl = true;
            _ballActive = false;
            SetBallVisible(true);
            _cometTrail.Clear();
            _cometTrail.emitting = true;
            if (_trajectoryRenderer != null) _trajectoryRenderer.Clear();
        }

        public void EndExternalControl()
        {
            SetPhysicalSimulation(false);
            _externalControl = false;
            _cometTrail.emitting = false;
            HideBall();
            _ballTransform.position = _idlePosition;
        }

        private void SetBallVisible(bool visible)
        {
            if (_sourceRenderer != null)
                _sourceRenderer.enabled = visible && (_visualPrefab == null || !_hideSourceRendererWhenVisualPrefab);

            if (_visualRenderers == null) return;
            for (int i = 0; i < _visualRenderers.Length; i++)
            {
                if (_visualRenderers[i] != null)
                    _visualRenderers[i].enabled = visible;
            }
        }

        public Rigidbody EnsurePhysicsComponents()
        {
            if (_physicsBody == null)
                _physicsBody = GetComponent<Rigidbody>();
            if (_physicsBody == null)
                _physicsBody = gameObject.AddComponent<Rigidbody>();

            _physicsBody.useGravity = true;
            _physicsBody.interpolation = RigidbodyInterpolation.Interpolate;
            _physicsBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (_physicsCollider == null)
                _physicsCollider = GetComponent<SphereCollider>();
            if (_physicsCollider == null)
                _physicsCollider = gameObject.AddComponent<SphereCollider>();

            _physicsCollider.isTrigger = false;
            if (_physicsCollider.radius <= 0.0001f)
                _physicsCollider.radius = EstimateLocalBallRadius();

            return _physicsBody;
        }

        public void SetPhysicalSimulation(bool enabled, bool clearVelocity = true)
        {
            ResolvePhysicsComponents();
            if (_physicsBody == null)
                return;

            _physicsSimulation = enabled;
            _physicsBody.isKinematic = !enabled;
            _physicsBody.useGravity = enabled;
            _physicsBody.detectCollisions = true;

            if (clearVelocity)
            {
                _physicsBody.linearVelocity = Vector3.zero;
                _physicsBody.angularVelocity = Vector3.zero;
            }

            if (enabled)
            {
                transform.SetParent(null, true);
                SetBallVisible(true);
                _ballActive = true;
                if (_trajectoryRenderer != null) _trajectoryRenderer.Clear();
                if (_cometTrail != null) _cometTrail.emitting = true;
            }
        }

        private void ResolvePhysicsComponents()
        {
            if (_physicsBody == null)
                _physicsBody = GetComponent<Rigidbody>();
            if (_physicsCollider == null)
                _physicsCollider = GetComponent<SphereCollider>();
        }

        private float EstimateLocalBallRadius()
        {
            Renderer renderer = _sourceRenderer != null ? _sourceRenderer : GetComponentInChildren<Renderer>(true);
            if (renderer == null)
                return 0.12f;

            Vector3 extents = renderer.bounds.extents;
            float worldRadius = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));
            Vector3 scale = transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            if (maxScale <= 0.0001f)
                return 0.12f;

            return Mathf.Clamp(worldRadius / maxScale, 0.06f, 0.5f);
        }

        private void EnsureVisualModel()
        {
            if (_visualPrefab == null)
            {
                RemoveGeneratedVisualModel();
                return;
            }

            if (_sourceRenderer != null && !_hideSourceRendererWhenVisualPrefab)
            {
                RemoveGeneratedVisualModel();
                return;
            }

            if (_ballTransform.Find("SoccerBallVisual") != null)
            {
                _visualRenderers = _ballTransform.Find("SoccerBallVisual").GetComponentsInChildren<Renderer>(true);
                return;
            }

            GameObject visual = Instantiate(_visualPrefab, _ballTransform);
            visual.name = "SoccerBallVisual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * _visualScale;

            foreach (var col in visual.GetComponentsInChildren<Collider>(true))
                Destroy(col);
            foreach (var t in visual.GetComponentsInChildren<Transform>(true))
                t.gameObject.isStatic = false;

            _visualRenderers = visual.GetComponentsInChildren<Renderer>(true);
        }

        private void RemoveGeneratedVisualModel()
        {
            Transform existing = _ballTransform.Find("SoccerBallVisual");
            if (existing == null) return;

            if (Application.isPlaying)
                Destroy(existing.gameObject);
            else
                DestroyImmediate(existing.gameObject);

            _visualRenderers = null;
        }

        // Called by GameManager when isFiring rising edge (AFTER data is set).
        private void OnShotFired()
        {
            SetPhysicalSimulation(false);
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
