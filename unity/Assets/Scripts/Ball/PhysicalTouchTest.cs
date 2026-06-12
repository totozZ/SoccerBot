using UnityEngine;
using UnityEngine.InputSystem;

namespace SoccerBot
{
    [DefaultExecutionOrder(-10)]
    public class PhysicalTouchTest : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _player;
        [SerializeField] private QuestControllerLegRig _legRig;
        [SerializeField] private BallController _ball;
        [SerializeField] private PhysicalBallInteractor _ballInteractor;

        [Header("Mode")]
        [SerializeField] private bool _enterOnStart = true;
        [SerializeField] private bool _createRigIfMissing = true;
        [SerializeField] private bool _createBallIfMissing = true;
        [SerializeField] private bool _routeContactsToMatchFlow = false;
        [SerializeField] private bool _debugLogs = true;
        [SerializeField] private bool _showOverlay = true;
        [SerializeField] private bool _ensureTuningController = true;
        [SerializeField] private bool _showTuningOverlay = true;

        [Header("Reset Layout")]
        [SerializeField] private Vector3 _ballLocalOffset = new Vector3(0f, 0.35f, 1.15f);
        [SerializeField] private float _resetCooldown = 0.15f;
        [SerializeField] private bool _ensureTestGround = true;
        [SerializeField] private bool _useFieldGroundWhenAvailable = true;
        [SerializeField] private Vector3 _groundSize = new Vector3(4f, 0.08f, 5f);
        [SerializeField] private float _groundForwardOffset = 1.4f;
        [SerializeField] private bool _ensureBoundaryWalls = true;
        [SerializeField] private bool _resetOutOfBoundsBall = true;
        [SerializeField] private bool _showBoundaryWalls = false;
        [SerializeField] private float _boundaryPadding = 0.45f;
        [SerializeField] private float _boundaryWallHeight = 0.55f;
        [SerializeField] private float _boundaryWallThickness = 0.12f;
        [SerializeField] private float _fallResetY = -0.75f;
        [SerializeField] private float _maxBallDistanceFromPlayer = 20f;

        [Header("Debug Drawing")]
        [SerializeField] private float _contactRayDuration = 2f;
        [SerializeField] private float _contactRayLength = 0.7f;

        private Rigidbody _ballBody;
        private GameObject _testGround;
        private GameObject _boundaryRoot;
        private PhysicalBallInteractor _subscribedInteractor;
        private FootBallTuningController _tuningController;
        private float _boundaryHalfWidth = 2f;
        private float _boundaryHalfLength = 2.5f;
        private float _lastResetTime = -999f;
        private float _lastContactTime = -999f;
        private float _lastImpulseTime = -999f;
        private Vector3 _lastContactPoint;
        private Vector3 _lastFootClosestPoint;
        private Vector3 _lastBallClosestPoint;
        private Vector3 _lastSwingDirection;
        private Vector3 _lastImpulseDirection;
        private string _lastContactSummary = "No foot contact yet.";
        private string _lastClosestSummary = "Closest points: n/a";
        private string _lastImpulseSummary = "No physical impulse yet.";
        private bool _isActive;

        private void Start()
        {
            if (_enterOnStart)
                EnterTestMode();
        }

        private void OnEnable()
        {
            TrackedLegController.GlobalFootContact += HandleFootContact;
            BindInteractorEvents();
        }

        private void OnDisable()
        {
            TrackedLegController.GlobalFootContact -= HandleFootContact;
            UnbindInteractorEvents();
        }

        private void Update()
        {
            if (!_isActive)
                return;

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                ResetBall();

            GuardBallBounds();
            DrawRecentContact();
        }

        private void OnGUI()
        {
            if (!_isActive || !_showOverlay)
                return;

            const float width = 560f;
            const float height = 144f;
            Rect rect = new Rect(18f, 18f, width, height);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 10f, width - 24f, 22f), "Physical Touch Test  |  Press R to reset ball");
            GUI.Label(new Rect(rect.x + 12f, rect.y + 38f, width - 24f, 22f), _lastContactSummary);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 66f, width - 24f, 22f), _lastClosestSummary);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 94f, width - 24f, 22f), _lastImpulseSummary);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 122f, width - 24f, 18f), $"Ball velocity: {FormatVector(_ballBody != null ? _ballBody.linearVelocity : Vector3.zero)}");
        }

        [ContextMenu("Enter Test Mode")]
        public void EnterTestMode()
        {
            ResolveReferences();
            if (_legRig == null || _ball == null)
            {
                Debug.LogWarning("[PhysicalTouchTest] Missing player leg rig or ball. Test mode was not entered.");
                return;
            }

            LayerMask ballLayer = 1 << _ball.gameObject.layer;
            _legRig.SetFootBallInteraction(true, true, ballLayer);
            _ballInteractor = _ball.GetComponent<PhysicalBallInteractor>();
            if (_ballInteractor == null)
                _ballInteractor = _ball.gameObject.AddComponent<PhysicalBallInteractor>();
            _ballInteractor.ConfigureRouting(_routeContactsToMatchFlow, true, _debugLogs);
            BindInteractorEvents();
            EnsureTuningController();

            _ballBody = _ball.EnsurePhysicsComponents();
            EnsureGround();
            EnsureBoundaryWalls();
            ResetBall();
            _isActive = true;

            if (_debugLogs)
                Debug.Log("[PhysicalTouchTest] Active. Kick the physical ball with tracked feet; press R to reset.");
        }

        [ContextMenu("Exit Test Mode")]
        public void ExitTestMode()
        {
            _isActive = false;
            if (_ball != null)
                _ball.SetPhysicalSimulation(false);
            if (_testGround != null)
                _testGround.SetActive(false);
            if (_boundaryRoot != null)
                _boundaryRoot.SetActive(false);
        }

        [ContextMenu("Reset Ball")]
        public void ResetBall()
        {
            if (Time.time - _lastResetTime < _resetCooldown)
                return;

            ResolveReferences();
            if (_ball == null)
                return;

            _lastResetTime = Time.time;
            _ballBody = _ball.EnsurePhysicsComponents();

            Vector3 position = GetWorldBallResetPosition();
            _ball.SetPhysicalSimulation(true, true);
            _ball.transform.position = position;
            _ball.transform.rotation = Quaternion.identity;
            if (_ballBody != null)
            {
                _ballBody.position = position;
                _ballBody.rotation = Quaternion.identity;
                _ballBody.linearVelocity = Vector3.zero;
                _ballBody.angularVelocity = Vector3.zero;
            }

            EnsureGround();
            EnsureBoundaryWalls();
            _lastContactSummary = "Ball reset. Waiting for foot contact.";
            _lastClosestSummary = "Closest points: n/a";
            _lastImpulseSummary = "No physical impulse yet.";
        }

        private void ResolveReferences()
        {
            if (_player == null)
            {
                GameObject playerGo = GameObject.Find("Player");
                if (playerGo != null)
                    _player = playerGo.transform;
            }

            if (_legRig == null && _player != null)
                _legRig = _player.GetComponent<QuestControllerLegRig>();
            if (_legRig == null && _createRigIfMissing && _player != null)
                _legRig = _player.gameObject.AddComponent<QuestControllerLegRig>();
            if (_legRig == null)
                _legRig = FindAnyObjectByType<QuestControllerLegRig>(FindObjectsInactive.Include);

            if (_ball == null)
                _ball = FindAnyObjectByType<BallController>(FindObjectsInactive.Include);
            if (_ball == null && _createBallIfMissing)
                _ball = CreateTestBall();

            if (_ball != null)
            {
                _ballInteractor = _ball.GetComponent<PhysicalBallInteractor>();
                _ballBody = _ball.GetComponent<Rigidbody>();
            }
        }

        private void EnsureTuningController()
        {
            if (!_ensureTuningController)
                return;

            if (_tuningController == null)
                _tuningController = FindAnyObjectByType<FootBallTuningController>(FindObjectsInactive.Include);

            if (_tuningController == null)
            {
                var tuningGo = new GameObject("FootBallTuningController");
                _tuningController = tuningGo.AddComponent<FootBallTuningController>();
            }

            _tuningController.Configure(_legRig, _ball, _ballInteractor, _showTuningOverlay);
        }

        private BallController CreateTestBall()
        {
            GameObject ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballGo.name = "Ball";
            ballGo.transform.localScale = Vector3.one * 0.24f;

            Renderer renderer = ballGo.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(0.96f, 0.95f, 0.9f, 1f);

            if (ballGo.GetComponent<TrailRenderer>() == null)
                ballGo.AddComponent<TrailRenderer>();

            return ballGo.AddComponent<BallController>();
        }

        private Vector3 GetWorldBallResetPosition()
        {
            Transform basis = _player != null ? _player : transform;
            Vector3 forward = basis.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            Vector3 right = new Vector3(forward.z, 0f, -forward.x).normalized;
            return basis.position
                + right * _ballLocalOffset.x
                + Vector3.up * _ballLocalOffset.y
                + forward * _ballLocalOffset.z;
        }

        private void EnsureGround()
        {
            if (!_ensureTestGround)
                return;

            if (_useFieldGroundWhenAvailable && FindAnyObjectByType<FieldBuilder>(FindObjectsInactive.Include) != null)
            {
                if (_testGround != null)
                    _testGround.SetActive(false);
                return;
            }

            if (_testGround == null)
            {
                _testGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _testGround.name = "PhysicalTouchTestGround";
                _testGround.layer = 2;
                Renderer renderer = _testGround.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material.color = new Color(0.12f, 0.45f, 0.18f, 1f);
            }

            Transform basis = _player != null ? _player : transform;
            Vector3 forward = basis.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            _testGround.SetActive(true);
            _testGround.transform.position = basis.position + forward * _groundForwardOffset + Vector3.down * (_groundSize.y * 0.5f);
            _testGround.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            _testGround.transform.localScale = _groundSize;
        }

        private void EnsureBoundaryWalls()
        {
            if (!_ensureBoundaryWalls)
                return;

            if (_boundaryRoot == null)
                _boundaryRoot = new GameObject("PhysicalTouchTestBoundary");

            ResolveBoundaryBasis(out Vector3 fieldCenter, out Quaternion fieldRotation, out float fieldWidth, out float fieldLength);

            Vector3 center = fieldCenter;
            center.y += Mathf.Max(0.05f, _boundaryWallHeight) * 0.5f;
            _boundaryRoot.SetActive(true);
            _boundaryRoot.transform.SetPositionAndRotation(center, fieldRotation);

            float width = Mathf.Max(0.5f, fieldWidth + _boundaryPadding * 2f);
            float length = Mathf.Max(0.5f, fieldLength + _boundaryPadding * 2f);
            float thickness = Mathf.Max(0.02f, _boundaryWallThickness);
            float height = Mathf.Max(0.05f, _boundaryWallHeight);
            _boundaryHalfWidth = width * 0.5f;
            _boundaryHalfLength = length * 0.5f;

            EnsureBoundaryWall("BackWall", new Vector3(0f, 0f, -length * 0.5f), new Vector3(width + thickness * 2f, height, thickness));
            EnsureBoundaryWall("FrontWall", new Vector3(0f, 0f, length * 0.5f), new Vector3(width + thickness * 2f, height, thickness));
            EnsureBoundaryWall("LeftWall", new Vector3(-width * 0.5f, 0f, 0f), new Vector3(thickness, height, length));
            EnsureBoundaryWall("RightWall", new Vector3(width * 0.5f, 0f, 0f), new Vector3(thickness, height, length));
        }

        private void EnsureBoundaryWall(string wallName, Vector3 localPosition, Vector3 localScale)
        {
            Transform wall = _boundaryRoot.transform.Find(wallName);
            if (wall == null)
            {
                GameObject wallGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wallGo.name = wallName;
                wallGo.layer = 2;
                wallGo.transform.SetParent(_boundaryRoot.transform, false);

                Renderer renderer = wallGo.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = new Color(0.08f, 0.22f, 0.35f, 0.35f);
                    renderer.enabled = _showBoundaryWalls;
                }
            }

            wall.localPosition = localPosition;
            wall.localRotation = Quaternion.identity;
            wall.localScale = localScale;

            Renderer existingRenderer = wall.GetComponent<Renderer>();
            if (existingRenderer != null)
                existingRenderer.enabled = _showBoundaryWalls;
        }

        private void ResolveBoundaryBasis(out Vector3 center, out Quaternion rotation, out float width, out float length)
        {
            FieldBuilder field = FindAnyObjectByType<FieldBuilder>(FindObjectsInactive.Include);
            if (field != null)
            {
                center = field.transform.position;
                rotation = field.transform.rotation;
                width = field._halfWidth * 2f;
                length = field._halfLength * 2f;
                return;
            }

            GameObject fieldGo = GameObject.Find("Field");
            Renderer grassRenderer = fieldGo != null ? fieldGo.GetComponentInChildren<Renderer>() : null;
            if (grassRenderer != null)
            {
                Bounds bounds = grassRenderer.bounds;
                center = bounds.center;
                center.y = 0f;
                rotation = Quaternion.identity;
                width = bounds.size.x;
                length = bounds.size.z;
                return;
            }

            Transform basis = _player != null ? _player : transform;
            Vector3 forward = basis.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            center = basis.position + forward * _groundForwardOffset;
            center.y = 0f;
            rotation = Quaternion.LookRotation(forward, Vector3.up);
            width = _groundSize.x;
            length = _groundSize.z;
        }

        private void GuardBallBounds()
        {
            if (!_resetOutOfBoundsBall || _ball == null)
                return;

            Vector3 ballPosition = _ball.transform.position;
            bool fell = ballPosition.y < _fallResetY;
            bool tooFar;

            if (_boundaryRoot != null && _boundaryRoot.activeInHierarchy)
            {
                Vector3 local = _boundaryRoot.transform.InverseTransformPoint(ballPosition);
                tooFar = Mathf.Abs(local.x) > _boundaryHalfWidth + 0.5f || Mathf.Abs(local.z) > _boundaryHalfLength + 0.5f;
            }
            else if (_player != null)
            {
                Vector3 flatDelta = ballPosition - _player.position;
                flatDelta.y = 0f;
                tooFar = flatDelta.magnitude > _maxBallDistanceFromPlayer;
            }
            else
            {
                tooFar = false;
            }

            if (!fell && !tooFar)
                return;

            if (_debugLogs)
                Debug.Log($"[PhysicalTouchTest] Ball bounds guard reset. fell={fell} tooFar={tooFar} position={FormatVector(ballPosition)}");

            _lastResetTime = -999f;
            ResetBall();
        }

        private void BindInteractorEvents()
        {
            if (_subscribedInteractor == _ballInteractor)
                return;

            UnbindInteractorEvents();
            _subscribedInteractor = _ballInteractor;
            if (_subscribedInteractor != null)
                _subscribedInteractor.PhysicalImpulseApplied += HandlePhysicalImpulse;
        }

        private void UnbindInteractorEvents()
        {
            if (_subscribedInteractor != null)
                _subscribedInteractor.PhysicalImpulseApplied -= HandlePhysicalImpulse;
            _subscribedInteractor = null;
        }

        private void HandleFootContact(FootContactData data)
        {
            if (!_isActive || !IsThisBall(data))
                return;

            _lastContactTime = Time.time;
            _lastContactPoint = data.ContactPoint;
            _lastFootClosestPoint = data.FootClosestPoint;
            _lastBallClosestPoint = data.BallClosestPoint;
            _lastSwingDirection = data.SwingDirection.sqrMagnitude > 0.0001f ? data.SwingDirection.normalized : Vector3.forward;
            _lastContactSummary =
                $"Contact {data.Foot}/{data.ContactZone}: speed={data.ContactSpeed:0.00} power={data.Power01:0.00} accuracy={data.Accuracy01:0.00} point={FormatVector(data.ContactPoint)}";
            _lastClosestSummary =
                $"Closest distance={data.ClosestPointDistance:0.000} foot={FormatVector(data.FootClosestPoint)} ball={FormatVector(data.BallClosestPoint)}";

            if (_debugLogs)
                Debug.Log($"[PhysicalTouchTest] {_lastContactSummary}");
        }

        private void HandlePhysicalImpulse(FootContactData data, Vector3 direction, float impulse)
        {
            if (!_isActive || !IsThisBall(data))
                return;

            _lastImpulseTime = Time.time;
            _lastImpulseDirection = direction;
            _lastImpulseSummary = $"Impulse: {impulse:0.00} dir={FormatVector(direction)}";
        }

        private bool IsThisBall(FootContactData data)
        {
            if (_ball == null || data.BallCollider == null)
                return false;

            if (_ballBody != null && data.BallBody == _ballBody)
                return true;

            return data.BallCollider.transform == _ball.transform || data.BallCollider.transform.IsChildOf(_ball.transform);
        }

        private void DrawRecentContact()
        {
            if (Time.time - _lastContactTime <= _contactRayDuration)
            {
                Debug.DrawRay(_lastContactPoint, _lastSwingDirection * _contactRayLength, Color.yellow);
                Debug.DrawLine(_lastFootClosestPoint, _lastBallClosestPoint, Color.magenta);
            }
            if (Time.time - _lastImpulseTime <= _contactRayDuration)
                Debug.DrawRay(_lastContactPoint, _lastImpulseDirection * _contactRayLength, Color.green);
        }

        private static string FormatVector(Vector3 value)
        {
            return $"{value.x:0.00},{value.y:0.00},{value.z:0.00}";
        }
    }

    [DefaultExecutionOrder(-8)]
    public class FootBallTuningController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private QuestControllerLegRig _legRig;
        [SerializeField] private BallController _ball;
        [SerializeField] private PhysicalBallInteractor _ballInteractor;

        [Header("Overlay")]
        [SerializeField] private bool _showOverlay = true;
        [SerializeField] private Rect _windowRect = new Rect(18f, 176f, 420f, 540f);

        [Header("Leg Pose")]
        [SerializeField] private Vector3 _leftPoseOffset = new Vector3(0f, -0.15f, 0.16f);
        [SerializeField] private Vector3 _rightPoseOffset = new Vector3(0f, -0.15f, 0.16f);
        [SerializeField] private float _legScale = 0.5f;

        [Header("Foot Collider")]
        [SerializeField] private Vector3 _footColliderCenter = new Vector3(0f, -0.03f, 0.18f);
        [SerializeField] private Vector3 _footSize = new Vector3(0.2f, 0.11f, 0.58f);

        [Header("Shin Collider")]
        [SerializeField] private Vector3 _shinColliderCenter = new Vector3(0f, 0.22f, -0.08f);
        [SerializeField] private float _shinRadius = 0.055f;
        [SerializeField] private float _shinHeight = 0.5f;

        [Header("Ground Alignment")]
        [SerializeField] private bool _lockFeetToGroundPlane = true;
        [SerializeField] private float _groundPlaneY = 0f;
        [SerializeField] private float _soleGroundClearance = 0.025f;

        [Header("Impulse")]
        [SerializeField] private float _minImpulseSpeed = 0.3f;
        [SerializeField] private float _minImpulse = 0.9f;
        [SerializeField] private float _maxImpulse = 5.2f;
        [SerializeField, Range(0f, 1f)] private float _swingDirectionWeight = 0.75f;
        [SerializeField] private float _lift = 0.09f;
        [SerializeField] private float _spinTorque = 0.06f;
        [SerializeField] private float _impulseCooldown = 0.12f;

        private const int WindowId = 982731;
        private bool _hasCapturedInitialValues;

        public void Configure(
            QuestControllerLegRig legRig,
            BallController ball,
            PhysicalBallInteractor ballInteractor,
            bool showOverlay)
        {
            _legRig = legRig;
            _ball = ball;
            _ballInteractor = ballInteractor;
            _showOverlay = showOverlay;
            CaptureCurrentValues();
            ApplyTuning();
        }

        private void Start()
        {
            ResolveReferences();
            CaptureCurrentValues();
            ApplyTuning();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
                _showOverlay = !_showOverlay;
        }

        private void OnGUI()
        {
            if (!_showOverlay)
                return;

            _windowRect = GUI.Window(WindowId, _windowRect, DrawWindow, "Foot Ball Tuning");
        }

        private void DrawWindow(int windowId)
        {
            GUI.changed = false;

            GUILayout.Label("F2 toggles this panel. Values apply live.");
            GUILayout.Space(4f);

            _legScale = DrawSlider("Leg scale", _legScale, 0.2f, 1.2f);
            _leftPoseOffset = DrawVector3("Left offset", _leftPoseOffset, -0.5f, 0.5f);
            _rightPoseOffset = DrawVector3("Right offset", _rightPoseOffset, -0.5f, 0.5f);

            GUILayout.Space(6f);
            _footColliderCenter = DrawVector3("Foot center", _footColliderCenter, -0.35f, 0.35f);
            _footSize = DrawVector3("Foot size", _footSize, 0.02f, 0.7f);

            GUILayout.Space(6f);
            _shinColliderCenter = DrawVector3("Shin center", _shinColliderCenter, -0.4f, 0.6f);
            _shinRadius = DrawSlider("Shin radius", _shinRadius, 0.01f, 0.15f);
            _shinHeight = DrawSlider("Shin height", _shinHeight, 0.08f, 0.9f);

            GUILayout.Space(6f);
            _lockFeetToGroundPlane = GUILayout.Toggle(_lockFeetToGroundPlane, "Lock feet to ground");
            _groundPlaneY = DrawSlider("Ground Y", _groundPlaneY, -0.2f, 0.3f);
            _soleGroundClearance = DrawSlider("Sole clearance", _soleGroundClearance, 0f, 0.12f);

            GUILayout.Space(6f);
            _minImpulseSpeed = DrawSlider("Min speed", _minImpulseSpeed, 0f, 2f);
            _minImpulse = DrawSlider("Min impulse", _minImpulse, 0f, 8f);
            _maxImpulse = DrawSlider("Max impulse", _maxImpulse, 0f, 14f);
            _swingDirectionWeight = DrawSlider("Swing weight", _swingDirectionWeight, 0f, 1f);
            _lift = DrawSlider("Lift", _lift, 0f, 0.8f);
            _spinTorque = DrawSlider("Spin torque", _spinTorque, 0f, 0.5f);
            _impulseCooldown = DrawSlider("Cooldown", _impulseCooldown, 0f, 0.5f);

            if (GUI.changed)
                ApplyTuning();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Re-read current"))
            {
                _hasCapturedInitialValues = false;
                CaptureCurrentValues();
            }

            if (GUILayout.Button("Apply"))
                ApplyTuning();
            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void ResolveReferences()
        {
            if (_legRig == null)
                _legRig = FindAnyObjectByType<QuestControllerLegRig>(FindObjectsInactive.Include);
            if (_ball == null)
                _ball = FindAnyObjectByType<BallController>(FindObjectsInactive.Include);
            if (_ballInteractor == null && _ball != null)
                _ballInteractor = _ball.GetComponent<PhysicalBallInteractor>();
        }

        private void CaptureCurrentValues()
        {
            ResolveReferences();
            if (_hasCapturedInitialValues)
                return;

            if (_legRig != null)
            {
                _leftPoseOffset = _legRig.LeftPoseOffsetPosition;
                _rightPoseOffset = _legRig.RightPoseOffsetPosition;
                _legScale = _legRig.LegScale;
                _footColliderCenter = _legRig.FootColliderCenter;
                _footSize = _legRig.FootSize;
                _shinColliderCenter = _legRig.ShinColliderCenter;
                _shinRadius = _legRig.ShinRadius;
                _shinHeight = _legRig.ShinHeight;
                _lockFeetToGroundPlane = _legRig.LockFeetToGroundPlane;
                _groundPlaneY = _legRig.GroundPlaneY;
                _soleGroundClearance = _legRig.SoleGroundClearance;
            }

            if (_ballInteractor != null)
            {
                _minImpulseSpeed = _ballInteractor.MinImpulseSpeed;
                _minImpulse = _ballInteractor.MinImpulse;
                _maxImpulse = _ballInteractor.MaxImpulse;
                _swingDirectionWeight = _ballInteractor.SwingDirectionWeight;
                _lift = _ballInteractor.Lift;
                _spinTorque = _ballInteractor.SpinTorque;
                _impulseCooldown = _ballInteractor.ImpulseCooldown;
            }

            _hasCapturedInitialValues = true;
        }

        private void ApplyTuning()
        {
            ResolveReferences();

            if (_legRig != null)
            {
                _legRig.ConfigureRuntimeTuning(
                    _leftPoseOffset,
                    _rightPoseOffset,
                    _legScale,
                    _footColliderCenter,
                    _footSize,
                    _shinColliderCenter,
                    _shinRadius,
                    _shinHeight,
                    _lockFeetToGroundPlane,
                    _groundPlaneY,
                    _soleGroundClearance);
            }

            if (_ballInteractor != null)
            {
                _ballInteractor.ConfigurePhysicalImpulse(
                    _minImpulseSpeed,
                    _minImpulse,
                    _maxImpulse,
                    _swingDirectionWeight,
                    _lift,
                    _spinTorque,
                    _impulseCooldown);
            }
        }

        private static Vector3 DrawVector3(string label, Vector3 value, float min, float max)
        {
            GUILayout.Label($"{label}: {FormatVector(value)}");
            value.x = DrawSlider("  X", value.x, min, max);
            value.y = DrawSlider("  Y", value.y, min, max);
            value.z = DrawSlider("  Z", value.z, min, max);
            return value;
        }

        private static float DrawSlider(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label} {value:0.000}", GUILayout.Width(150f));
            value = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.EndHorizontal();
            return value;
        }

        private static string FormatVector(Vector3 value)
        {
            return $"{value.x:0.000}, {value.y:0.000}, {value.z:0.000}";
        }
    }
}
