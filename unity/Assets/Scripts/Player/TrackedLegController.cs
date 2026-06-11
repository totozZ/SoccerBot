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
        public readonly float ContactSpeed;
        public readonly float Power01;
        public readonly float Accuracy01;
        public readonly bool ShootIntentHeld;

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
            float contactSpeed,
            float power01,
            float accuracy01,
            bool shootIntentHeld)
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
            ContactSpeed = contactSpeed;
            Power01 = power01;
            Accuracy01 = accuracy01;
            ShootIntentHeld = shootIntentHeld;
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
        [SerializeField] private Vector3 _poseOffsetPosition = new Vector3(0f, 0.3f, 0.1f);
        [SerializeField] private Vector3 _poseOffsetEuler = Vector3.zero;

        [Header("Body")]
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private Rigidbody _footBody;
        [SerializeField] private Collider _footCollider;
        [SerializeField] private CapsuleCollider _shinCollider;
        [SerializeField] private Vector3 _footColliderCenter = new Vector3(0f, -0.03f, 0.08f);
        [SerializeField] private Vector3 _footColliderSize = new Vector3(0.18f, 0.11f, 0.36f);
        [SerializeField] private Vector3 _shinColliderCenter = new Vector3(0f, 0.22f, -0.08f);
        [SerializeField] private float _shinColliderRadius = 0.055f;
        [SerializeField] private float _shinColliderHeight = 0.5f;
        [SerializeField] private bool _buildDefaultVisual = true;

        [Header("Interaction")]
        [SerializeField] private LayerMask _ballLayer = ~0;
        [SerializeField] private float _minInteractionSpeed = 0.15f;
        [SerializeField] private float _fullPowerSpeed = 4.0f;
        [SerializeField] private float _contactLogInterval = 0.25f;
        [SerializeField] private bool _debugContacts = true;

        private InputAction _positionAction;
        private InputAction _rotationAction;
        private InputAction _velocityAction;
        private InputAction _angularVelocityAction;
        private InputAction _shootIntentAction;

        private Vector3 _latestWorldPosition;
        private Quaternion _latestWorldRotation = Quaternion.identity;
        private Vector3 _latestWorldVelocity;
        private Vector3 _latestWorldAngularVelocity;
        private Vector3 _lastSampleWorldPosition;
        private Quaternion _lastSampleWorldRotation = Quaternion.identity;
        private bool _hasTracking;
        private bool _hasSample;
        private float _lastSampleTime;
        private float _lastContactLogTime;

        public TrackedLegHandedness Handedness => _handedness;
        public Vector3 FootVelocity => _latestWorldVelocity;
        public Vector3 FootAngularVelocity => _latestWorldAngularVelocity;
        public bool HasTracking => _hasTracking;
        public bool ShootIntentHeld => _shootIntentAction != null && _shootIntentAction.IsPressed();

        public void Configure(
            TrackedLegHandedness handedness,
            Transform trackingSpaceRoot,
            Vector3 poseOffsetPosition,
            Vector3 poseOffsetEuler,
            LayerMask ballLayer,
            bool buildDefaultVisual)
        {
            _handedness = handedness;
            _trackingSpaceRoot = trackingSpaceRoot;
            _poseOffsetPosition = poseOffsetPosition;
            _poseOffsetEuler = poseOffsetEuler;
            _ballLayer = ballLayer;
            _buildDefaultVisual = buildDefaultVisual;
            name = handedness == TrackedLegHandedness.Left ? "LeftTrackedLeg" : "RightTrackedLeg";
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
            _hasTracking = false;
            _hasSample = false;
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
                return;

            var origin = FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
            if (origin != null)
            {
                if (origin.CameraFloorOffsetObject != null)
                    _trackingSpaceRoot = origin.CameraFloorOffsetObject.transform;
                else
                    _trackingSpaceRoot = origin.transform;
            }
        }

        private void EnsureBody()
        {
            if (_footBody == null)
                _footBody = GetComponent<Rigidbody>();

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
                footBox.isTrigger = false;
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
            _shinCollider.isTrigger = false;
        }

        private void EnsureDefaultVisual()
        {
            if (_visualRoot != null)
                return;

            var existing = transform.Find("TrackedLegVisual");
            if (existing != null)
            {
                _visualRoot = existing;
                return;
            }

            _visualRoot = new GameObject("TrackedLegVisual").transform;
            _visualRoot.SetParent(transform, false);

            Material bootMaterial = CreateMaterial(
                _handedness == TrackedLegHandedness.Left ? "Left Tracked Boot" : "Right Tracked Boot",
                _handedness == TrackedLegHandedness.Left ? new Color(0.05f, 0.28f, 0.95f, 1f) : new Color(0.95f, 0.12f, 0.08f, 1f));
            Material sockMaterial = CreateMaterial("Tracked Sock", new Color(0.92f, 0.95f, 1f, 1f));
            Material accentMaterial = CreateMaterial("Tracked Boot Stripe", new Color(1f, 0.86f, 0.12f, 1f));

            var foot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            foot.name = "Foot";
            foot.transform.SetParent(_visualRoot, false);
            foot.transform.localPosition = _footColliderCenter;
            foot.transform.localScale = _footColliderSize;
            RemoveCollider(foot);
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

        private void ApplyDefaultVisualMaterials()
        {
            if (_visualRoot == null || _visualRoot.name != "TrackedLegVisual")
                return;

            Material bootMaterial = CreateMaterial(
                _handedness == TrackedLegHandedness.Left ? "Left Tracked Boot" : "Right Tracked Boot",
                _handedness == TrackedLegHandedness.Left ? new Color(0.05f, 0.28f, 0.95f, 1f) : new Color(0.95f, 0.12f, 0.08f, 1f));
            Material sockMaterial = CreateMaterial("Tracked Sock", new Color(0.92f, 0.95f, 1f, 1f));
            Material accentMaterial = CreateMaterial("Tracked Boot Stripe", new Color(1f, 0.86f, 0.12f, 1f));

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

            if (_handedness == TrackedLegHandedness.Right)
            {
                _shootIntentAction = new InputAction("RightFootShootIntent", InputActionType.Button);
                _shootIntentAction.AddBinding("<XRController>{RightHand}/triggerPressed");
                _shootIntentAction.Enable();
            }
        }

        private void ReadTrackingPose()
        {
            if (_positionAction == null || _rotationAction == null)
                return;
            if (_positionAction.controls.Count == 0 || _rotationAction.controls.Count == 0)
            {
                _hasTracking = false;
                SetTrackingObjectsActive(false);
                return;
            }

            SetTrackingObjectsActive(true);

            Vector3 localPosition = _positionAction.ReadValue<Vector3>();
            Quaternion localRotation = _rotationAction.ReadValue<Quaternion>();
            if (localRotation.x == 0f && localRotation.y == 0f && localRotation.z == 0f && localRotation.w == 0f)
                localRotation = Quaternion.identity;

            Quaternion offsetRotation = Quaternion.Euler(_poseOffsetEuler);
            Vector3 offsetLocalPosition = localPosition + localRotation * _poseOffsetPosition;
            Quaternion offsetLocalRotation = localRotation * offsetRotation;

            Vector3 worldPosition = _trackingSpaceRoot != null
                ? _trackingSpaceRoot.TransformPoint(offsetLocalPosition)
                : offsetLocalPosition;
            Quaternion worldRotation = _trackingSpaceRoot != null
                ? _trackingSpaceRoot.rotation * offsetLocalRotation
                : offsetLocalRotation;

            float now = Time.time;
            float dt = _hasSample ? Mathf.Max(now - _lastSampleTime, 0.0001f) : Mathf.Max(Time.deltaTime, 0.0001f);

            Vector3 worldVelocity = ReadWorldVelocity(worldPosition, dt);
            Vector3 worldAngularVelocity = ReadWorldAngularVelocity(worldRotation, dt);

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

        private Vector3 ReadWorldVelocity(Vector3 worldPosition, float dt)
        {
            if (_velocityAction != null && _velocityAction.controls.Count > 0)
            {
                Vector3 localVelocity = _velocityAction.ReadValue<Vector3>();
                if (localVelocity.sqrMagnitude > 0.000001f)
                {
                    return _trackingSpaceRoot != null
                        ? _trackingSpaceRoot.TransformDirection(localVelocity)
                        : localVelocity;
                }
            }

            return _hasSample ? (worldPosition - _lastSampleWorldPosition) / dt : Vector3.zero;
        }

        private Vector3 ReadWorldAngularVelocity(Quaternion worldRotation, float dt)
        {
            if (_angularVelocityAction != null && _angularVelocityAction.controls.Count > 0)
            {
                Vector3 localAngularVelocity = _angularVelocityAction.ReadValue<Vector3>();
                if (localAngularVelocity.sqrMagnitude > 0.000001f)
                {
                    return _trackingSpaceRoot != null
                        ? _trackingSpaceRoot.TransformDirection(localAngularVelocity)
                        : localAngularVelocity;
                }
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
            if (collision == null || collision.collider == null || !IsCandidateBall(collision.collider))
                return;

            ContactPoint contact = collision.contactCount > 0
                ? collision.GetContact(0)
                : default;
            Vector3 point = collision.contactCount > 0 ? contact.point : collision.collider.ClosestPoint(transform.position);
            Vector3 normal = collision.contactCount > 0 ? contact.normal : (transform.position - point).normalized;
            PublishContact(collision.collider, point, normal);
        }

        private void PublishTriggerContact(Collider other)
        {
            if (other == null || !IsCandidateBall(other))
                return;

            Vector3 point = other.ClosestPoint(transform.position);
            Vector3 normal = (transform.position - point).normalized;
            PublishContact(other, point, normal);
        }

        private void PublishContact(Collider ballCollider, Vector3 contactPoint, Vector3 contactNormal)
        {
            Rigidbody ballBody = ballCollider.attachedRigidbody;
            Vector3 swingDirection = _latestWorldVelocity.sqrMagnitude > 0.0001f
                ? _latestWorldVelocity.normalized
                : transform.forward;
            Vector3 footForward = transform.forward;
            Vector3 ballDirection = ballBody != null
                ? (ballBody.worldCenterOfMass - _latestWorldPosition)
                : (ballCollider.bounds.center - _latestWorldPosition);
            if (ballDirection.sqrMagnitude < 0.0001f)
                ballDirection = swingDirection;

            float contactSpeed = _latestWorldVelocity.magnitude;
            if (contactSpeed < _minInteractionSpeed)
                return;

            float power01 = Mathf.InverseLerp(_minInteractionSpeed, _fullPowerSpeed, contactSpeed);
            float swingAccuracy = Mathf.Clamp01(Vector3.Dot(swingDirection, ballDirection.normalized) * 0.5f + 0.5f);
            float faceAccuracy = Mathf.Clamp01(Vector3.Dot(footForward.normalized, ballDirection.normalized) * 0.5f + 0.5f);
            float accuracy01 = Mathf.Clamp01(swingAccuracy * 0.65f + faceAccuracy * 0.35f);

            var data = new FootContactData(
                _handedness,
                this,
                _footBody,
                _footCollider,
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
                contactSpeed,
                Mathf.Clamp01(power01),
                accuracy01,
                ShootIntentHeld);

            GlobalFootContact?.Invoke(data);

            if (_debugContacts && Time.time - _lastContactLogTime >= _contactLogInterval)
            {
                _lastContactLogTime = Time.time;
                Debug.Log($"[TrackedLeg] {_handedness} contact '{ballCollider.name}' speed={contactSpeed:0.00} power={power01:0.00} accuracy={accuracy01:0.00} point={contactPoint}");
            }
        }

        private bool IsCandidateBall(Collider other)
        {
            if (((1 << other.gameObject.layer) & _ballLayer.value) == 0)
                return false;
            if (_footBody != null && other.attachedRigidbody == _footBody)
                return false;
            return true;
        }

        private void SetTrackingObjectsActive(bool active)
        {
            if (_footCollider != null && _footCollider.enabled != active)
                _footCollider.enabled = active;
            if (_shinCollider != null && _shinCollider.enabled != active)
                _shinCollider.enabled = active;
            if (_visualRoot != null && _visualRoot.gameObject.activeSelf != active)
                _visualRoot.gameObject.SetActive(active);
        }

        private static Material CreateMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader) { name = name, color = color };
            return material;
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

        private static void DisposeAction(ref InputAction action)
        {
            if (action == null)
                return;

            action.Disable();
            action.Dispose();
            action = null;
        }
    }
}
