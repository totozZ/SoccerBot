using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;

namespace SoccerBot
{
    public enum TrackedLegHandedness
    {
        Left,
        Right
    }

    public readonly struct FootContactData
    {
        public readonly TrackedLegHandedness Foot;
        public readonly TrackedLegController Source;
        public readonly Rigidbody FootBody;
        public readonly Collider FootCollider;
        public readonly Collider BallCollider;
        public readonly Rigidbody BallBody;
        public readonly Vector3 FootPosition;
        public readonly Quaternion FootRotation;
        public readonly Vector3 FootVelocity;
        public readonly Vector3 FootAngularVelocity;
        public readonly Vector3 FootForward;
        public readonly Vector3 SwingDirection;
        public readonly Vector3 ContactPoint;
        public readonly Vector3 ContactNormal;
        public readonly Vector3 FootClosestPoint;
        public readonly Vector3 BallClosestPoint;
        public readonly float ClosestPointDistance;
        public readonly float ContactSpeed;
        public readonly float Power01;
        public readonly float Accuracy01;
        public readonly bool ShootIntentHeld;
        public readonly string ContactZone;

        public FootContactData(
            TrackedLegHandedness foot,
            TrackedLegController source,
            Rigidbody footBody,
            Collider footCollider,
            Collider ballCollider,
            Rigidbody ballBody,
            Vector3 footPosition,
            Quaternion footRotation,
            Vector3 footVelocity,
            Vector3 footAngularVelocity,
            Vector3 footForward,
            Vector3 swingDirection,
            Vector3 contactPoint,
            Vector3 contactNormal,
            Vector3 footClosestPoint,
            Vector3 ballClosestPoint,
            float closestPointDistance,
            float contactSpeed,
            float power01,
            float accuracy01,
            bool shootIntentHeld,
            string contactZone)
        {
            Foot = foot;
            Source = source;
            FootBody = footBody;
            FootCollider = footCollider;
            BallCollider = ballCollider;
            BallBody = ballBody;
            FootPosition = footPosition;
            FootRotation = footRotation;
            FootVelocity = footVelocity;
            FootAngularVelocity = footAngularVelocity;
            FootForward = footForward;
            SwingDirection = swingDirection;
            ContactPoint = contactPoint;
            ContactNormal = contactNormal;
            FootClosestPoint = footClosestPoint;
            BallClosestPoint = ballClosestPoint;
            ClosestPointDistance = closestPointDistance;
            ContactSpeed = contactSpeed;
            Power01 = power01;
            Accuracy01 = accuracy01;
            ShootIntentHeld = shootIntentHeld;
            ContactZone = contactZone;
        }
    }

    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class TrackedLegController : MonoBehaviour
    {
        public static event Action<FootContactData> GlobalFootContact;

        [Header("Tracking")]
        [SerializeField] private TrackedLegHandedness _handedness = TrackedLegHandedness.Right;
        [SerializeField] private Transform _trackingSpaceRoot;
        [SerializeField] private Transform _renderCamera;
        [SerializeField] private bool _useRenderCameraRelativePose = true;
        [SerializeField] private Vector3 _poseOffsetPosition = new Vector3(0f, -0.15f, 0.16f);
        [SerializeField] private Vector3 _poseOffsetEuler = Vector3.zero;

        [Header("Body")]
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private Rigidbody _footBody;
        [SerializeField] private Collider _footCollider;
        [SerializeField] private CapsuleCollider _shinCollider;
        [SerializeField] private Vector3 _footColliderCenter = new Vector3(0f, -0.03f, 0.18f);
        [SerializeField] private Vector3 _footColliderSize = new Vector3(0.2f, 0.11f, 0.58f);
        [SerializeField] private Vector3 _shinColliderCenter = new Vector3(0f, 0.29f, -0.08f);
        [SerializeField] private float _shinColliderRadius = 0.055f;
        [SerializeField] private float _shinColliderHeight = 0.68f;
        [SerializeField] private bool _buildDefaultVisual = true;
        [SerializeField] private bool _useFootContactZones = true;
        [SerializeField] private bool _lockFootToGroundPlane = true;
        [SerializeField] private float _groundPlaneY = 0f;
        [SerializeField] private float _soleGroundClearance = 0.025f;

        [Header("Interaction")]
        [SerializeField] private bool _interactionEnabled = false;
        [SerializeField] private LayerMask _ballLayer = ~0;
        [SerializeField] private float _minInteractionSpeed = 0.15f;
        [SerializeField] private float _fullPowerSpeed = 4.0f;
        [SerializeField] private float _contactPublishInterval = 0.03f;
        [SerializeField] private float _contactLogInterval = 0.25f;
        [SerializeField] private bool _debugContacts = true;

        [Header("Contact Assist")]
        [SerializeField] private bool _enableProximityContactProbe = true;
        [SerializeField] private float _proximityProbePadding = 0.045f;
        [SerializeField] private float _proximityProbeMaxDistance = 0.04f;
        [SerializeField] private float _proximityProbeMinSpeed = 0.04f;

        [Header("Debug Drawing")]
        [SerializeField] private bool _drawColliderGizmos = true;
        [SerializeField] private bool _drawContactDebug = true;
        [SerializeField] private float _contactDebugDuration = 0.18f;

        private InputAction _positionAction;
        private InputAction _rotationAction;
        private InputAction _velocityAction;
        private InputAction _angularVelocityAction;
        private InputAction _shootIntentAction;
        private InputAction _hmdPositionAction;
        private InputAction _hmdRotationAction;

        private Vector3 _latestWorldPosition;
        private Quaternion _latestWorldRotation = Quaternion.identity;
        private Vector3 _latestWorldVelocity;
        private Vector3 _latestWorldAngularVelocity;
        private Vector3 _lastSampleWorldPosition;
        private Quaternion _lastSampleWorldRotation = Quaternion.identity;
        private bool _hasTracking;
        private bool _hasSample;
        private bool _inputActionsMatchHandedness;
        private bool _loggedTrackingDiagnostics;
        private float _lastSampleTime;
        private float _lastContactLogTime;
        private float _lastPublishedContactTime = -999f;
        private Collider _lastPublishedBallCollider;
        private TrackedLegContactZone[] _contactZones = Array.Empty<TrackedLegContactZone>();
        private readonly Collider[] _proximityProbeResults = new Collider[8];
        private Material _bootMaterial;
        private Material _sockMaterial;
        private Material _accentMaterial;

        public TrackedLegHandedness Handedness => _handedness;
        public Vector3 FootVelocity => _latestWorldVelocity;
        public Vector3 FootAngularVelocity => _latestWorldAngularVelocity;
        public bool HasTracking => _hasTracking;
        public bool ShootIntentHeld => _shootIntentAction != null && _shootIntentAction.IsPressed();

        public void ConfigureContactProximityProbe(bool enabled, float padding, float maxDistance, float minSpeed)
        {
            _enableProximityContactProbe = enabled;
            _proximityProbePadding = Mathf.Max(0f, padding);
            _proximityProbeMaxDistance = Mathf.Max(0f, maxDistance);
            _proximityProbeMinSpeed = Mathf.Max(0f, minSpeed);
        }

        public void Configure(
            TrackedLegHandedness handedness,
            Transform trackingSpaceRoot,
            Transform renderCamera,
            Vector3 poseOffsetPosition,
            Vector3 poseOffsetEuler,
            Vector3 footColliderCenter,
            Vector3 footColliderSize,
            Vector3 shinColliderCenter,
            float shinColliderRadius,
            float shinColliderHeight,
            bool lockFootToGroundPlane,
            float groundPlaneY,
            float soleGroundClearance,
            LayerMask ballLayer,
            bool buildDefaultVisual,
            bool interactionEnabled)
        {
            bool handednessChanged = _handedness != handedness;
            _handedness = handedness;
            _trackingSpaceRoot = trackingSpaceRoot;
            _renderCamera = renderCamera;
            _poseOffsetPosition = poseOffsetPosition;
            _poseOffsetEuler = poseOffsetEuler;
            _footColliderCenter = footColliderCenter;
            _footColliderSize = ClampSize(footColliderSize, new Vector3(0.02f, 0.02f, 0.02f));
            _shinColliderCenter = shinColliderCenter;
            _shinColliderRadius = Mathf.Max(0.005f, shinColliderRadius);
            _shinColliderHeight = Mathf.Max(0.02f, shinColliderHeight);
            _lockFootToGroundPlane = lockFootToGroundPlane;
            _groundPlaneY = groundPlaneY;
            _soleGroundClearance = Mathf.Max(0f, soleGroundClearance);
            _ballLayer = ballLayer;
            _buildDefaultVisual = buildDefaultVisual;
            _interactionEnabled = interactionEnabled;
            name = handedness == TrackedLegHandedness.Left ? "LeftTrackedLeg" : "RightTrackedLeg";
            if (handednessChanged)
            {
                _loggedTrackingDiagnostics = false;
                RecreateInputActionsIfEnabled();
            }

            if (_trackingSpaceRoot == null || _renderCamera == null)
                ResolveTrackingSpace();

            EnsureBody();
            EnsureColliders();
            if (_buildDefaultVisual)
                EnsureDefaultVisual();
            ApplyDefaultVisualShape();
            ApplyColliderInteractionMode();
            ApplyDefaultVisualMaterials();
        }

        private void Awake()
        {
            ResolveTrackingSpace();
            EnsureBody();
            EnsureColliders();
            if (_buildDefaultVisual)
                EnsureDefaultVisual();
        }

        private void OnEnable()
        {
            CreateInputActions();
        }

        private void OnDisable()
        {
            DisposeAction(ref _positionAction);
            DisposeAction(ref _rotationAction);
            DisposeAction(ref _velocityAction);
            DisposeAction(ref _angularVelocityAction);
            DisposeAction(ref _shootIntentAction);
            DisposeAction(ref _hmdPositionAction);
            DisposeAction(ref _hmdRotationAction);
            _hasTracking = false;
            _hasSample = false;
        }

        private void OnDestroy()
        {
            DestroyTrackedMaterial(ref _bootMaterial);
            DestroyTrackedMaterial(ref _sockMaterial);
            DestroyTrackedMaterial(ref _accentMaterial);
        }

        private void Update()
        {
            ReadTrackingPose();
        }

        private void FixedUpdate()
        {
            if (!_hasTracking || _footBody == null)
                return;

            _footBody.MovePosition(_latestWorldPosition);
            _footBody.MoveRotation(_latestWorldRotation);
            ProbeNearbyBallContacts();
        }

        private void OnCollisionEnter(Collision collision)
        {
            PublishCollisionContact(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            PublishCollisionContact(collision);
        }

        private void OnTriggerEnter(Collider other)
        {
            PublishTriggerContact(other);
        }

        private void OnTriggerStay(Collider other)
        {
            PublishTriggerContact(other);
        }

        private void ResolveTrackingSpace()
        {
            if (_trackingSpaceRoot != null)
            {
                if (_renderCamera == null)
                    ResolveRenderCamera();
                return;
            }

            var origin = FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
            if (origin != null)
            {
                if (origin.CameraFloorOffsetObject != null)
                    _trackingSpaceRoot = origin.CameraFloorOffsetObject.transform;
                else
                    _trackingSpaceRoot = origin.transform;
            }

            ResolveRenderCamera();
        }

        private void ResolveRenderCamera()
        {
            if (_renderCamera != null)
                return;

            var player = GameObject.Find("Player");
            if (player != null)
            {
                var fpsCamera = player.transform.Find("FpsAnchor/FpsCamera");
                if (fpsCamera != null)
                {
                    _renderCamera = fpsCamera;
                    return;
                }
            }

            Camera main = Camera.main;
            if (main != null)
                _renderCamera = main.transform;
        }

        private void EnsureBody()
        {
            if (_footBody == null)
                _footBody = GetComponent<Rigidbody>();
            if (_footBody == null)
                _footBody = gameObject.AddComponent<Rigidbody>();

            _footBody.isKinematic = true;
            _footBody.useGravity = false;
            _footBody.interpolation = RigidbodyInterpolation.Interpolate;
            _footBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private void EnsureColliders()
        {
            if (_footCollider == null)
            {
                var box = GetComponent<BoxCollider>();
                if (box == null)
                    box = gameObject.AddComponent<BoxCollider>();
                _footCollider = box;
            }

            if (_footCollider is BoxCollider footBox)
            {
                footBox.center = _footColliderCenter;
                footBox.size = _footColliderSize;
                footBox.isTrigger = true;
            }

            if (_shinCollider == null)
            {
                var shin = transform.Find("ShinCollider");
                if (shin == null)
                {
                    var shinGo = new GameObject("ShinCollider");
                    shinGo.transform.SetParent(transform, false);
                    shin = shinGo.transform;
                }

                _shinCollider = shin.GetComponent<CapsuleCollider>();
                if (_shinCollider == null)
                    _shinCollider = shin.gameObject.AddComponent<CapsuleCollider>();
            }

            _shinCollider.transform.localPosition = Vector3.zero;
            _shinCollider.transform.localRotation = Quaternion.identity;
            _shinCollider.center = _shinColliderCenter;
            _shinCollider.radius = _shinColliderRadius;
            _shinCollider.height = _shinColliderHeight;
            _shinCollider.direction = 1;
            _shinCollider.isTrigger = true;

            EnsureContactZones();
        }

        private void EnsureContactZones()
        {
            Transform zonesRoot = transform.Find("FootContactZones");
            if (zonesRoot == null)
            {
                var zonesGo = new GameObject("FootContactZones");
                zonesGo.transform.SetParent(transform, false);
                zonesRoot = zonesGo.transform;
            }

            zonesRoot.localPosition = Vector3.zero;
            zonesRoot.localRotation = Quaternion.identity;
            zonesRoot.gameObject.SetActive(_useFootContactZones);

            _contactZones = new[]
            {
                EnsureContactZone(
                    zonesRoot,
                    "ToeZone",
                    _footColliderCenter + new Vector3(0f, 0f, _footColliderSize.z * 0.44f),
                    new Vector3(_footColliderSize.x * 0.95f, _footColliderSize.y * 1.18f, _footColliderSize.z * 0.28f)),
                EnsureContactZone(
                    zonesRoot,
                    "InstepZone",
                    _footColliderCenter + new Vector3((_handedness == TrackedLegHandedness.Left ? 0.26f : -0.26f) * _footColliderSize.x, 0f, _footColliderSize.z * 0.06f),
                    new Vector3(_footColliderSize.x * 0.52f, _footColliderSize.y * 1.18f, _footColliderSize.z * 0.56f)),
                EnsureContactZone(
                    zonesRoot,
                    "SoleZone",
                    _footColliderCenter + new Vector3(0f, -_footColliderSize.y * 0.43f, -_footColliderSize.z * 0.02f),
                    new Vector3(_footColliderSize.x * 0.98f, _footColliderSize.y * 0.32f, _footColliderSize.z * 0.82f))
            };
        }

        private TrackedLegContactZone EnsureContactZone(Transform parent, string zoneName, Vector3 center, Vector3 size)
        {
            Transform zoneTransform = parent.Find(zoneName);
            if (zoneTransform == null)
            {
                var zoneGo = new GameObject(zoneName);
                zoneGo.transform.SetParent(parent, false);
                zoneTransform = zoneGo.transform;
            }

            zoneTransform.localPosition = center;
            zoneTransform.localRotation = Quaternion.identity;
            zoneTransform.localScale = Vector3.one;

            var zoneCollider = zoneTransform.GetComponent<BoxCollider>();
            if (zoneCollider == null)
                zoneCollider = zoneTransform.gameObject.AddComponent<BoxCollider>();
            zoneCollider.center = Vector3.zero;
            zoneCollider.size = ClampSize(size, new Vector3(0.01f, 0.01f, 0.01f));
            zoneCollider.isTrigger = true;
            zoneCollider.enabled = _useFootContactZones;

            var zone = zoneTransform.GetComponent<TrackedLegContactZone>();
            if (zone == null)
                zone = zoneTransform.gameObject.AddComponent<TrackedLegContactZone>();
            zone.Configure(this, zoneCollider, zoneName);

            return zone;
        }

        private void EnsureDefaultVisual()
        {
            if (_visualRoot != null)
            {
                ApplyDefaultVisualShape();
                return;
            }

            var existing = transform.Find("TrackedLegVisual");
            if (existing != null)
            {
                _visualRoot = existing;
                ApplyDefaultVisualShape();
                return;
            }

            _visualRoot = new GameObject("TrackedLegVisual").transform;
            _visualRoot.SetParent(transform, false);

            Material bootMaterial = GetOrCreateTrackedMaterial(
                ref _bootMaterial,
                _handedness == TrackedLegHandedness.Left ? "Left Tracked Boot" : "Right Tracked Boot",
                _handedness == TrackedLegHandedness.Left ? new Color(0.05f, 0.28f, 0.95f, 1f) : new Color(0.95f, 0.12f, 0.08f, 1f));
            Material sockMaterial = GetOrCreateTrackedMaterial(ref _sockMaterial, "Tracked Sock", new Color(0.92f, 0.95f, 1f, 1f));
            Material accentMaterial = GetOrCreateTrackedMaterial(ref _accentMaterial, "Tracked Boot Stripe", new Color(1f, 0.86f, 0.12f, 1f));

            var foot = CreateBootVisual("Foot");
            foot.name = "Foot";
            foot.transform.SetParent(_visualRoot, false);
            foot.transform.localPosition = _footColliderCenter;
            SetMaterial(foot, bootMaterial);

            var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = "FootStrikeStripe";
            stripe.transform.SetParent(foot.transform, false);
            stripe.transform.localPosition = new Vector3(_handedness == TrackedLegHandedness.Left ? 0.54f : -0.54f, 0.12f, 0.16f);
            stripe.transform.localScale = new Vector3(0.08f, 0.18f, 0.56f);
            RemoveCollider(stripe);
            SetMaterial(stripe, accentMaterial);

            var shin = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            shin.name = "Shin";
            shin.transform.SetParent(_visualRoot, false);
            shin.transform.localPosition = _shinColliderCenter;
            shin.transform.localRotation = Quaternion.identity;
            shin.transform.localScale = new Vector3(_shinColliderRadius * 2f, _shinColliderHeight * 0.5f, _shinColliderRadius * 2f);
            RemoveCollider(shin);
            SetMaterial(shin, sockMaterial);
        }

        private void ApplyDefaultVisualShape()
        {
            if (_visualRoot == null || _visualRoot.name != "TrackedLegVisual")
                return;

            Transform foot = _visualRoot.Find("Foot");
            if (foot != null)
            {
                foot.localPosition = _footColliderCenter;
                foot.localScale = Vector3.one;
                ApplyBootMesh(foot.gameObject);

                Transform stripe = foot.Find("FootStrikeStripe");
                if (stripe != null)
                {
                    stripe.localPosition = new Vector3(
                        (_handedness == TrackedLegHandedness.Left ? 0.42f : -0.42f) * _footColliderSize.x,
                        0.28f * _footColliderSize.y,
                        0.08f * _footColliderSize.z);
                    stripe.localScale = new Vector3(
                        0.08f * _footColliderSize.x,
                        0.16f * _footColliderSize.y,
                        0.52f * _footColliderSize.z);
                }
            }

            Transform shin = _visualRoot.Find("Shin");
            if (shin != null)
            {
                shin.localPosition = _shinColliderCenter;
                shin.localRotation = Quaternion.identity;
                shin.localScale = new Vector3(_shinColliderRadius * 2f, _shinColliderHeight * 0.5f, _shinColliderRadius * 2f);
            }
        }

        private void ApplyDefaultVisualMaterials()
        {
            if (_visualRoot == null || _visualRoot.name != "TrackedLegVisual")
                return;

            Material bootMaterial = GetOrCreateTrackedMaterial(
                ref _bootMaterial,
                _handedness == TrackedLegHandedness.Left ? "Left Tracked Boot" : "Right Tracked Boot",
                _handedness == TrackedLegHandedness.Left ? new Color(0.05f, 0.28f, 0.95f, 1f) : new Color(0.95f, 0.12f, 0.08f, 1f));
            Material sockMaterial = GetOrCreateTrackedMaterial(ref _sockMaterial, "Tracked Sock", new Color(0.92f, 0.95f, 1f, 1f));
            Material accentMaterial = GetOrCreateTrackedMaterial(ref _accentMaterial, "Tracked Boot Stripe", new Color(1f, 0.86f, 0.12f, 1f));

            Transform foot = _visualRoot.Find("Foot");
            if (foot != null)
                SetMaterial(foot.gameObject, bootMaterial);

            Transform shin = _visualRoot.Find("Shin");
            if (shin != null)
                SetMaterial(shin.gameObject, sockMaterial);

            Transform stripe = foot != null ? foot.Find("FootStrikeStripe") : null;
            if (stripe != null)
                SetMaterial(stripe.gameObject, accentMaterial);
        }

        private void CreateInputActions()
        {
            DisposeAction(ref _positionAction);
            DisposeAction(ref _rotationAction);
            DisposeAction(ref _velocityAction);
            DisposeAction(ref _angularVelocityAction);
            DisposeAction(ref _shootIntentAction);
            DisposeAction(ref _hmdPositionAction);
            DisposeAction(ref _hmdRotationAction);

            string hand = _handedness == TrackedLegHandedness.Left ? "LeftHand" : "RightHand";

            _positionAction = new InputAction($"{hand}LegPosition", InputActionType.Value, expectedControlType: "Vector3");
            _positionAction.AddBinding($"<XRController>{{{hand}}}/devicePosition");
            _positionAction.Enable();

            _rotationAction = new InputAction($"{hand}LegRotation", InputActionType.Value, expectedControlType: "Quaternion");
            _rotationAction.AddBinding($"<XRController>{{{hand}}}/deviceRotation");
            _rotationAction.Enable();

            _velocityAction = new InputAction($"{hand}LegVelocity", InputActionType.Value, expectedControlType: "Vector3");
            _velocityAction.AddBinding($"<XRController>{{{hand}}}/deviceVelocity");
            _velocityAction.Enable();

            _angularVelocityAction = new InputAction($"{hand}LegAngularVelocity", InputActionType.Value, expectedControlType: "Vector3");
            _angularVelocityAction.AddBinding($"<XRController>{{{hand}}}/deviceAngularVelocity");
            _angularVelocityAction.Enable();

            _hmdPositionAction = new InputAction($"{hand}LegHmdPosition", InputActionType.Value, expectedControlType: "Vector3");
            _hmdPositionAction.AddBinding("<XRHMD>/centerEyePosition");
            _hmdPositionAction.Enable();

            _hmdRotationAction = new InputAction($"{hand}LegHmdRotation", InputActionType.Value, expectedControlType: "Quaternion");
            _hmdRotationAction.AddBinding("<XRHMD>/centerEyeRotation");
            _hmdRotationAction.Enable();

            if (_handedness == TrackedLegHandedness.Right)
            {
                _shootIntentAction = new InputAction("RightFootShootIntent", InputActionType.Button);
                _shootIntentAction.AddBinding("<XRController>{RightHand}/triggerPressed");
                _shootIntentAction.Enable();
            }

            _inputActionsMatchHandedness = true;
        }

        private void ReadTrackingPose()
        {
            if (!_inputActionsMatchHandedness)
                RecreateInputActionsIfEnabled();

            if (_positionAction == null || _rotationAction == null)
                return;
            if (_positionAction.controls.Count == 0 || _rotationAction.controls.Count == 0)
            {
                _hasTracking = false;
                SetTrackingObjectsActive(false);
                LogTrackingDiagnostics("no-controls");
                return;
            }

            SetTrackingObjectsActive(true);
            LogTrackingDiagnostics("tracking-controls-bound");

            Vector3 localPosition = _positionAction.ReadValue<Vector3>();
            Quaternion localRotation = _rotationAction.ReadValue<Quaternion>();
            if (localRotation.x == 0f && localRotation.y == 0f && localRotation.z == 0f && localRotation.w == 0f)
                localRotation = Quaternion.identity;

            Quaternion offsetRotation = Quaternion.Euler(_poseOffsetEuler);
            Vector3 offsetLocalPosition = localPosition + localRotation * _poseOffsetPosition;
            Quaternion offsetLocalRotation = localRotation * offsetRotation;

            Quaternion trackingToWorldRotation = ResolveTrackingToWorldRotation();
            Vector3 worldPosition = ResolveWorldPosition(offsetLocalPosition, trackingToWorldRotation);
            Quaternion worldRotation = trackingToWorldRotation * offsetLocalRotation;
            worldPosition = ApplyGroundPlaneLock(worldPosition);

            float now = Time.time;
            float dt = _hasSample ? Mathf.Max(now - _lastSampleTime, 0.0001f) : Mathf.Max(Time.deltaTime, 0.0001f);

            Vector3 worldVelocity = ReadWorldVelocity(worldPosition, dt, trackingToWorldRotation);
            Vector3 worldAngularVelocity = ReadWorldAngularVelocity(worldRotation, dt, trackingToWorldRotation);

            _latestWorldPosition = worldPosition;
            _latestWorldRotation = worldRotation;
            _latestWorldVelocity = worldVelocity;
            _latestWorldAngularVelocity = worldAngularVelocity;
            _hasTracking = true;

            _lastSampleWorldPosition = worldPosition;
            _lastSampleWorldRotation = worldRotation;
            _lastSampleTime = now;
            _hasSample = true;
        }

        private Quaternion ResolveTrackingToWorldRotation()
        {
            if (_useRenderCameraRelativePose && _renderCamera != null && TryReadHmdRotation(out Quaternion hmdLocalRotation))
                return _renderCamera.rotation * Quaternion.Inverse(hmdLocalRotation);

            return _trackingSpaceRoot != null ? _trackingSpaceRoot.rotation : Quaternion.identity;
        }

        private Vector3 ResolveWorldPosition(Vector3 offsetLocalPosition, Quaternion trackingToWorldRotation)
        {
            if (_useRenderCameraRelativePose && _renderCamera != null && TryReadHmdPosition(out Vector3 hmdLocalPosition))
                return _renderCamera.position + trackingToWorldRotation * (offsetLocalPosition - hmdLocalPosition);

            return _trackingSpaceRoot != null
                ? _trackingSpaceRoot.TransformPoint(offsetLocalPosition)
                : offsetLocalPosition;
        }

        private Vector3 ApplyGroundPlaneLock(Vector3 worldPosition)
        {
            if (!_lockFootToGroundPlane)
                return worldPosition;

            float colliderBottomLocalY = _footColliderCenter.y - _footColliderSize.y * 0.5f;
            worldPosition.y = _groundPlaneY + _soleGroundClearance - colliderBottomLocalY;
            return worldPosition;
        }

        private bool TryReadHmdPosition(out Vector3 position)
        {
            position = Vector3.zero;
            if (_hmdPositionAction == null || _hmdPositionAction.controls.Count == 0)
                return false;

            position = _hmdPositionAction.ReadValue<Vector3>();
            return true;
        }

        private bool TryReadHmdRotation(out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            if (_hmdRotationAction == null || _hmdRotationAction.controls.Count == 0)
                return false;

            rotation = _hmdRotationAction.ReadValue<Quaternion>();
            if (rotation.x == 0f && rotation.y == 0f && rotation.z == 0f && rotation.w == 0f)
                rotation = Quaternion.identity;
            return true;
        }

        private Vector3 ReadWorldVelocity(Vector3 worldPosition, float dt, Quaternion trackingToWorldRotation)
        {
            if (_velocityAction != null && _velocityAction.controls.Count > 0)
            {
                Vector3 localVelocity = _velocityAction.ReadValue<Vector3>();
                if (localVelocity.sqrMagnitude > 0.000001f)
                    return trackingToWorldRotation * localVelocity;
            }

            return _hasSample ? (worldPosition - _lastSampleWorldPosition) / dt : Vector3.zero;
        }

        private Vector3 ReadWorldAngularVelocity(Quaternion worldRotation, float dt, Quaternion trackingToWorldRotation)
        {
            if (_angularVelocityAction != null && _angularVelocityAction.controls.Count > 0)
            {
                Vector3 localAngularVelocity = _angularVelocityAction.ReadValue<Vector3>();
                if (localAngularVelocity.sqrMagnitude > 0.000001f)
                    return trackingToWorldRotation * localAngularVelocity;
            }

            if (!_hasSample)
                return Vector3.zero;

            Quaternion delta = worldRotation * Quaternion.Inverse(_lastSampleWorldRotation);
            delta.ToAngleAxis(out float angleDegrees, out Vector3 axis);
            if (angleDegrees > 180f)
                angleDegrees -= 360f;
            if (axis.sqrMagnitude < 0.000001f)
                return Vector3.zero;

            return axis.normalized * (angleDegrees * Mathf.Deg2Rad / dt);
        }

        private void PublishCollisionContact(Collision collision)
        {
            if (!_interactionEnabled)
                return;
            if (collision == null || collision.collider == null || !IsCandidateBall(collision.collider))
                return;

            ContactPoint contact = collision.contactCount > 0
                ? collision.GetContact(0)
                : default;
            Collider sourceCollider = collision.contactCount > 0 && contact.thisCollider != null
                ? contact.thisCollider
                : _footCollider;
            Vector3 point = collision.contactCount > 0 ? contact.point : collision.collider.ClosestPoint(transform.position);
            Vector3 normal = collision.contactCount > 0 ? contact.normal : (transform.position - point).normalized;
            PublishContact(collision.collider, sourceCollider, point, normal);
        }

        private void PublishTriggerContact(Collider other)
        {
            PublishTriggerContact(other, ResolveBestTriggerSource(other));
        }

        internal void PublishZoneTriggerContact(Collider sourceCollider, Collider other)
        {
            PublishTriggerContact(other, sourceCollider);
        }

        private void PublishTriggerContact(Collider other, Collider sourceCollider)
        {
            if (!_interactionEnabled)
                return;
            if (other == null || !IsCandidateBall(other))
                return;

            BuildClosestPointContact(sourceCollider, other, out Vector3 point, out Vector3 normal);
            PublishContact(other, sourceCollider, point, normal);
        }

        private void ProbeNearbyBallContacts()
        {
            if (!_interactionEnabled || !_enableProximityContactProbe || _footCollider == null)
                return;
            if (_latestWorldVelocity.magnitude < _proximityProbeMinSpeed)
                return;

            Vector3 halfExtents = _footColliderSize * 0.5f + Vector3.one * _proximityProbePadding;
            Vector3 boxCenter = _latestWorldPosition + _latestWorldRotation * _footColliderCenter;
            int count = Physics.OverlapBoxNonAlloc(
                boxCenter,
                halfExtents,
                _proximityProbeResults,
                _latestWorldRotation,
                _ballLayer,
                QueryTriggerInteraction.Collide);

            Vector3 contactHalfExtents = _footColliderSize * 0.5f;
            for (int i = 0; i < count; i++)
            {
                Collider ballCollider = _proximityProbeResults[i];
                if (ballCollider == null || !IsCandidateBall(ballCollider))
                    continue;

                BuildPredictedClosestPointPair(
                    ballCollider,
                    boxCenter,
                    _latestWorldRotation,
                    contactHalfExtents,
                    out Vector3 footClosestPoint,
                    out Vector3 ballClosestPoint,
                    out float distance);

                if (distance > _proximityProbeMaxDistance)
                    continue;

                Vector3 normal = footClosestPoint - ballClosestPoint;
                if (normal.sqrMagnitude < 0.000001f)
                {
                    Vector3 ballCenter = ballCollider.attachedRigidbody != null
                        ? ballCollider.attachedRigidbody.worldCenterOfMass
                        : ballCollider.bounds.center;
                    normal = footClosestPoint - ballCenter;
                }

                if (normal.sqrMagnitude < 0.000001f)
                    normal = -(_latestWorldRotation * Vector3.forward);

                Vector3 contactPoint = distance <= 0.0001f ? footClosestPoint : ballClosestPoint;
                PublishContact(
                    ballCollider,
                    _footCollider,
                    contactPoint,
                    normal.normalized,
                    footClosestPoint,
                    ballClosestPoint,
                    distance,
                    "FootBoxProximity");
            }
        }

        private Collider ResolveBestTriggerSource(Collider ballCollider)
        {
            if (!_useFootContactZones || _contactZones == null || ballCollider == null)
                return _footCollider;

            Vector3 ballCenter = ballCollider.attachedRigidbody != null
                ? ballCollider.attachedRigidbody.worldCenterOfMass
                : ballCollider.bounds.center;

            Collider best = _footCollider;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < _contactZones.Length; i++)
            {
                Collider zoneCollider = _contactZones[i] != null ? _contactZones[i].ZoneCollider : null;
                if (zoneCollider == null || !zoneCollider.enabled || !zoneCollider.gameObject.activeInHierarchy)
                    continue;

                float distance = Vector3.SqrMagnitude(zoneCollider.ClosestPoint(ballCenter) - ballCenter);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = zoneCollider;
                }
            }

            return best != null ? best : _footCollider;
        }

        private void PublishContact(Collider ballCollider, Collider sourceCollider, Vector3 contactPoint, Vector3 contactNormal)
        {
            Collider contactCollider = sourceCollider != null ? sourceCollider : _footCollider;
            BuildClosestPointPair(contactCollider, ballCollider, out Vector3 footClosestPoint, out Vector3 ballClosestPoint, out float closestPointDistance);
            PublishContact(
                ballCollider,
                contactCollider,
                contactPoint,
                contactNormal,
                footClosestPoint,
                ballClosestPoint,
                closestPointDistance,
                DescribeContactZone(contactCollider));
        }

        private void PublishContact(
            Collider ballCollider,
            Collider sourceCollider,
            Vector3 contactPoint,
            Vector3 contactNormal,
            Vector3 footClosestPoint,
            Vector3 ballClosestPoint,
            float closestPointDistance,
            string contactZone)
        {
            Rigidbody ballBody = ballCollider.attachedRigidbody;
            Collider contactCollider = sourceCollider != null ? sourceCollider : _footCollider;
            Vector3 swingDirection = _latestWorldVelocity.sqrMagnitude > 0.0001f
                ? _latestWorldVelocity.normalized
                : transform.forward;
            Vector3 footForward = transform.forward;
            if (contactNormal.sqrMagnitude < 0.000001f)
                contactNormal = -footForward;
            else
                contactNormal.Normalize();

            Vector3 ballDirection = ballBody != null
                ? (ballBody.worldCenterOfMass - _latestWorldPosition)
                : (ballCollider.bounds.center - _latestWorldPosition);
            if (ballDirection.sqrMagnitude < 0.0001f)
                ballDirection = swingDirection;

            float contactSpeed = _latestWorldVelocity.magnitude;
            if (_contactPublishInterval > 0f &&
                ballCollider == _lastPublishedBallCollider &&
                Time.time - _lastPublishedContactTime < _contactPublishInterval)
                return;

            float power01 = Mathf.InverseLerp(_minInteractionSpeed, _fullPowerSpeed, contactSpeed);
            float swingAccuracy = Mathf.Clamp01(Vector3.Dot(swingDirection, ballDirection.normalized) * 0.5f + 0.5f);
            float faceAccuracy = Mathf.Clamp01(Vector3.Dot(footForward.normalized, ballDirection.normalized) * 0.5f + 0.5f);
            float accuracy01 = Mathf.Clamp01(swingAccuracy * 0.65f + faceAccuracy * 0.35f);
            if (string.IsNullOrEmpty(contactZone))
                contactZone = DescribeContactZone(contactCollider);

            var data = new FootContactData(
                _handedness,
                this,
                _footBody,
                contactCollider,
                ballCollider,
                ballBody,
                _latestWorldPosition,
                _latestWorldRotation,
                _latestWorldVelocity,
                _latestWorldAngularVelocity,
                footForward,
                swingDirection,
                contactPoint,
                contactNormal,
                footClosestPoint,
                ballClosestPoint,
                closestPointDistance,
                contactSpeed,
                Mathf.Clamp01(power01),
                accuracy01,
                ShootIntentHeld,
                contactZone);

            _lastPublishedContactTime = Time.time;
            _lastPublishedBallCollider = ballCollider;
            GlobalFootContact?.Invoke(data);
            DrawContactDebug(data);

            if (_debugContacts && Time.time - _lastContactLogTime >= _contactLogInterval)
            {
                _lastContactLogTime = Time.time;
                Debug.Log(
                    $"[TrackedLeg] {_handedness} contact '{ballCollider.name}' zone={contactZone} " +
                    $"speed={contactSpeed:0.00} power={power01:0.00} accuracy={accuracy01:0.00} " +
                    $"closestDistance={closestPointDistance:0.000} footClosest={footClosestPoint} ballClosest={ballClosestPoint}");
            }
        }

        private void BuildClosestPointContact(Collider sourceCollider, Collider ballCollider, out Vector3 contactPoint, out Vector3 contactNormal)
        {
            BuildClosestPointPair(sourceCollider, ballCollider, out Vector3 footClosestPoint, out Vector3 ballClosestPoint, out float distance);
            contactPoint = ballClosestPoint;

            Vector3 normal = footClosestPoint - ballClosestPoint;
            if (normal.sqrMagnitude < 0.000001f)
            {
                Vector3 ballCenter = ballCollider.attachedRigidbody != null
                    ? ballCollider.attachedRigidbody.worldCenterOfMass
                    : ballCollider.bounds.center;
                normal = footClosestPoint - ballCenter;
            }

            if (normal.sqrMagnitude < 0.000001f)
                normal = -transform.forward;

            contactNormal = normal.normalized;
            if (distance <= 0.0001f)
                contactPoint = footClosestPoint;
        }

        private void BuildClosestPointPair(Collider footCollider, Collider ballCollider, out Vector3 footClosestPoint, out Vector3 ballClosestPoint, out float distance)
        {
            Vector3 ballCenter = ballCollider.attachedRigidbody != null
                ? ballCollider.attachedRigidbody.worldCenterOfMass
                : ballCollider.bounds.center;

            footClosestPoint = footCollider != null ? footCollider.ClosestPoint(ballCenter) : transform.position;
            ballClosestPoint = ballCollider.ClosestPoint(footClosestPoint);
            distance = Vector3.Distance(footClosestPoint, ballClosestPoint);
        }

        private static void BuildPredictedClosestPointPair(
            Collider ballCollider,
            Vector3 boxCenter,
            Quaternion boxRotation,
            Vector3 halfExtents,
            out Vector3 footClosestPoint,
            out Vector3 ballClosestPoint,
            out float distance)
        {
            Vector3 ballCenter = ballCollider.attachedRigidbody != null
                ? ballCollider.attachedRigidbody.worldCenterOfMass
                : ballCollider.bounds.center;

            footClosestPoint = ClosestPointOnOrientedBox(ballCenter, boxCenter, boxRotation, halfExtents);
            ballClosestPoint = ballCollider.ClosestPoint(footClosestPoint);
            distance = Vector3.Distance(footClosestPoint, ballClosestPoint);
        }

        private static Vector3 ClosestPointOnOrientedBox(Vector3 point, Vector3 boxCenter, Quaternion boxRotation, Vector3 halfExtents)
        {
            Quaternion inverseRotation = Quaternion.Inverse(boxRotation);
            Vector3 local = inverseRotation * (point - boxCenter);
            local.x = Mathf.Clamp(local.x, -halfExtents.x, halfExtents.x);
            local.y = Mathf.Clamp(local.y, -halfExtents.y, halfExtents.y);
            local.z = Mathf.Clamp(local.z, -halfExtents.z, halfExtents.z);
            return boxCenter + boxRotation * local;
        }

        private string DescribeContactZone(Collider contactCollider)
        {
            if (contactCollider == null)
                return "Unknown";
            if (contactCollider == _footCollider)
                return "FootBox";
            if (_shinCollider != null && contactCollider == _shinCollider)
                return "Shin";
            return contactCollider.name;
        }

        private void DrawContactDebug(FootContactData data)
        {
            if (!_drawContactDebug)
                return;

            Debug.DrawLine(data.FootClosestPoint, data.BallClosestPoint, Color.magenta, _contactDebugDuration);
            Debug.DrawRay(data.ContactPoint, data.ContactNormal * 0.22f, Color.cyan, _contactDebugDuration);
            Debug.DrawRay(data.ContactPoint, data.SwingDirection * 0.34f, Color.yellow, _contactDebugDuration);
        }

        private bool IsCandidateBall(Collider other)
        {
            if (((1 << other.gameObject.layer) & _ballLayer.value) == 0)
                return false;
            if (_footBody != null && other.attachedRigidbody == _footBody)
                return false;
            if (other.GetComponentInParent<BallController>() != null)
                return true;
            return other.attachedRigidbody != null && other.attachedRigidbody.GetComponent<BallController>() != null;
        }

        private void ApplyColliderInteractionMode()
        {
            if (_footCollider != null)
                _footCollider.isTrigger = true;
            if (_shinCollider != null)
                _shinCollider.isTrigger = true;
            if (_contactZones != null)
            {
                for (int i = 0; i < _contactZones.Length; i++)
                {
                    if (_contactZones[i] != null)
                        _contactZones[i].SetEnabled(_useFootContactZones);
                }
            }
        }

        private void SetTrackingObjectsActive(bool active)
        {
            if (_footCollider != null && _footCollider.enabled != active)
                _footCollider.enabled = active;
            if (_shinCollider != null && _shinCollider.enabled != active)
                _shinCollider.enabled = active;
            if (_visualRoot != null && _visualRoot.gameObject.activeSelf != active)
                _visualRoot.gameObject.SetActive(active);
            if (_contactZones != null)
            {
                for (int i = 0; i < _contactZones.Length; i++)
                {
                    if (_contactZones[i] != null)
                        _contactZones[i].SetEnabled(active && _useFootContactZones);
                }
            }
        }

        private static Material CreateMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader) { name = name, color = color };
            return material;
        }

        private static Material GetOrCreateTrackedMaterial(ref Material material, string name, Color color)
        {
            if (material == null)
            {
                material = CreateMaterial(name, color);
                return material;
            }

            material.name = name;
            material.color = color;
            return material;
        }

        private static void DestroyTrackedMaterial(ref Material material)
        {
            if (material == null)
                return;

            if (Application.isPlaying)
                Destroy(material);
            else
                DestroyImmediate(material);

            material = null;
        }

        private static void SetMaterial(GameObject go, Material material)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        private static void RemoveCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
        }

        private GameObject CreateBootVisual(string objectName)
        {
            var go = new GameObject(objectName);
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            ApplyBootMesh(go);
            return go;
        }

        private void ApplyBootMesh(GameObject go)
        {
            var filter = go.GetComponent<MeshFilter>();
            if (filter == null)
                filter = go.AddComponent<MeshFilter>();
            if (go.GetComponent<MeshRenderer>() == null)
                go.AddComponent<MeshRenderer>();

            filter.sharedMesh = BuildBootMesh(_footColliderSize);
            RemoveCollider(go);
        }

        private static Mesh BuildBootMesh(Vector3 size)
        {
            float halfLength = size.z * 0.5f;
            float bottom = -size.y * 0.5f;
            float top = size.y * 0.5f;
            float heelHalfWidth = size.x * 0.48f;
            float midHalfWidth = size.x * 0.56f;
            float toeHalfWidth = size.x * 0.22f;
            float heelZ = -halfLength;
            float midZ = size.z * 0.08f;
            float toeZ = halfLength;

            Vector3[] v =
            {
                new Vector3(-heelHalfWidth, bottom, heelZ),
                new Vector3(heelHalfWidth, bottom, heelZ),
                new Vector3(-midHalfWidth, bottom, midZ),
                new Vector3(midHalfWidth, bottom, midZ),
                new Vector3(-toeHalfWidth, bottom, toeZ),
                new Vector3(toeHalfWidth, bottom, toeZ),
                new Vector3(-heelHalfWidth * 0.9f, top, heelZ),
                new Vector3(heelHalfWidth * 0.9f, top, heelZ),
                new Vector3(-midHalfWidth, top, midZ),
                new Vector3(midHalfWidth, top, midZ),
                new Vector3(-toeHalfWidth * 0.7f, top * 0.82f, toeZ),
                new Vector3(toeHalfWidth * 0.7f, top * 0.82f, toeZ),
            };

            int[] triangles =
            {
                0, 2, 1, 1, 2, 3,
                2, 4, 3, 3, 4, 5,
                6, 7, 8, 7, 9, 8,
                8, 9, 10, 9, 11, 10,
                0, 1, 6, 1, 7, 6,
                4, 10, 5, 5, 10, 11,
                0, 6, 2, 2, 6, 8,
                2, 8, 4, 4, 8, 10,
                1, 3, 7, 3, 9, 7,
                3, 5, 9, 5, 11, 9,
            };

            var mesh = new Mesh { name = "ProceduralTrackedBoot" };
            mesh.vertices = v;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 ClampSize(Vector3 size, Vector3 minimum)
        {
            return new Vector3(
                Mathf.Max(minimum.x, size.x),
                Mathf.Max(minimum.y, size.y),
                Mathf.Max(minimum.z, size.z));
        }

        private static void DisposeAction(ref InputAction action)
        {
            if (action == null)
                return;

            action.Disable();
            action.Dispose();
            action = null;
        }

        private void RecreateInputActionsIfEnabled()
        {
            _inputActionsMatchHandedness = false;
            if (!isActiveAndEnabled)
                return;

            CreateInputActions();
        }

        private void LogTrackingDiagnostics(string reason)
        {
            if (_loggedTrackingDiagnostics)
                return;

            _loggedTrackingDiagnostics = true;
            string hand = _handedness == TrackedLegHandedness.Left ? "LeftHand" : "RightHand";
            string positionControl = DescribeFirstControl(_positionAction);
            string rotationControl = DescribeFirstControl(_rotationAction);
            string velocityControl = DescribeFirstControl(_velocityAction);
            string visualPath = _visualRoot != null ? GetHierarchyPath(_visualRoot) : "null";
            Debug.Log(
                $"[TrackedLeg] Diagnostics {name} hand={_handedness} expected={hand} reason={reason} " +
                $"ref={GetHashCode()} visual='{visualPath}' " +
                $"positionControl='{positionControl}' rotationControl='{rotationControl}' velocityControl='{velocityControl}'");
        }

        private static string DescribeFirstControl(InputAction action)
        {
            if (action == null)
                return "null-action";
            if (action.controls.Count == 0)
                return "none";
            return action.controls[0].path;
        }

        private static string GetHierarchyPath(Transform item)
        {
            if (item == null)
                return "null";

            string path = item.name;
            Transform current = item.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private void OnDrawGizmos()
        {
            if (!_drawColliderGizmos)
                return;

            DrawBoxGizmo(transform, _footColliderCenter, _footColliderSize, new Color(0.1f, 0.9f, 1f, 0.85f));
            DrawCapsuleGizmo(transform, _shinColliderCenter, _shinColliderRadius, _shinColliderHeight, new Color(1f, 0.8f, 0.1f, 0.85f));
            if (_enableProximityContactProbe && _proximityProbePadding > 0f)
                DrawBoxGizmo(transform, _footColliderCenter, _footColliderSize + Vector3.one * (_proximityProbePadding * 2f), new Color(1f, 1f, 1f, 0.45f));

            if (!_useFootContactZones)
                return;

            DrawBoxGizmo(transform, _footColliderCenter + new Vector3(0f, 0f, _footColliderSize.z * 0.44f), new Vector3(_footColliderSize.x * 0.95f, _footColliderSize.y * 1.18f, _footColliderSize.z * 0.28f), new Color(0f, 1f, 0.25f, 0.7f));
            DrawBoxGizmo(transform, _footColliderCenter + new Vector3((_handedness == TrackedLegHandedness.Left ? 0.26f : -0.26f) * _footColliderSize.x, 0f, _footColliderSize.z * 0.06f), new Vector3(_footColliderSize.x * 0.52f, _footColliderSize.y * 1.18f, _footColliderSize.z * 0.56f), new Color(1f, 0.25f, 0.95f, 0.7f));
            DrawBoxGizmo(transform, _footColliderCenter + new Vector3(0f, -_footColliderSize.y * 0.43f, -_footColliderSize.z * 0.02f), new Vector3(_footColliderSize.x * 0.98f, _footColliderSize.y * 0.32f, _footColliderSize.z * 0.82f), new Color(1f, 0.45f, 0.1f, 0.7f));
        }

        private static void DrawBoxGizmo(Transform basis, Vector3 center, Vector3 size, Color color)
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Color oldColor = Gizmos.color;
            Gizmos.matrix = basis.localToWorldMatrix;
            Gizmos.color = color;
            Gizmos.DrawWireCube(center, size);
            Gizmos.matrix = oldMatrix;
            Gizmos.color = oldColor;
        }

        private static void DrawCapsuleGizmo(Transform basis, Vector3 center, float radius, float height, Color color)
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Color oldColor = Gizmos.color;
            Gizmos.matrix = basis.localToWorldMatrix;
            Gizmos.color = color;

            float cylinderHalf = Mathf.Max(0f, height * 0.5f - radius);
            Vector3 top = center + Vector3.up * cylinderHalf;
            Vector3 bottom = center - Vector3.up * cylinderHalf;
            Gizmos.DrawWireSphere(top, radius);
            Gizmos.DrawWireSphere(bottom, radius);
            Gizmos.DrawLine(top + Vector3.forward * radius, bottom + Vector3.forward * radius);
            Gizmos.DrawLine(top - Vector3.forward * radius, bottom - Vector3.forward * radius);
            Gizmos.DrawLine(top + Vector3.right * radius, bottom + Vector3.right * radius);
            Gizmos.DrawLine(top - Vector3.right * radius, bottom - Vector3.right * radius);

            Gizmos.matrix = oldMatrix;
            Gizmos.color = oldColor;
        }
    }

    public sealed class TrackedLegContactZone : MonoBehaviour
    {
        private TrackedLegController _owner;
        private Collider _zoneCollider;

        public Collider ZoneCollider => _zoneCollider;

        public void Configure(TrackedLegController owner, Collider zoneCollider, string zoneName)
        {
            _owner = owner;
            _zoneCollider = zoneCollider;
            name = zoneName;
        }

        public void SetEnabled(bool enabled)
        {
            if (_zoneCollider != null)
                _zoneCollider.enabled = enabled;
            if (gameObject.activeSelf != enabled)
                gameObject.SetActive(enabled);
        }

        private void OnTriggerEnter(Collider other)
        {
            _owner?.PublishZoneTriggerContact(_zoneCollider, other);
        }

        private void OnTriggerStay(Collider other)
        {
            _owner?.PublishZoneTriggerContact(_zoneCollider, other);
        }
    }
}
