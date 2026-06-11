using UnityEngine;
using Unity.XR.CoreUtils;

namespace SoccerBot
{
    [DefaultExecutionOrder(-45)]
    [DisallowMultipleComponent]
    public class QuestControllerLegRig : MonoBehaviour
    {
        [Header("Runtime Creation")]
        [SerializeField] private bool _createOnStart = true;
        [SerializeField] private bool _liveUpdateInPlayMode = true;
        [SerializeField] private bool _buildDefaultVisuals = true;
        [SerializeField] private bool _enableFootBallInteraction = true;
        [SerializeField] private bool _ensurePhysicalBallInteractor = true;

        [Header("Tracking Space")]
        [SerializeField] private Transform _trackingSpaceRoot;
        [SerializeField] private Transform _renderCamera;

        [Header("Left Foot Offset")]
        [SerializeField] private Vector3 _leftPoseOffsetPosition = new Vector3(0f, -0.15f, 0.1f);
        [SerializeField] private Vector3 _leftPoseOffsetEuler = Vector3.zero;

        [Header("Right Foot Offset")]
        [SerializeField] private Vector3 _rightPoseOffsetPosition = new Vector3(0f, -0.15f, 0.1f);
        [SerializeField] private Vector3 _rightPoseOffsetEuler = Vector3.zero;

        [Header("Leg Size")]
        [SerializeField] private float _legScale = 0.5f;
        [SerializeField] private Vector3 _footColliderCenter = new Vector3(0f, -0.03f, 0.08f);
        [SerializeField] private Vector3 _footSize = new Vector3(0.18f, 0.11f, 0.36f);
        [SerializeField] private Vector3 _shinColliderCenter = new Vector3(0f, 0.22f, -0.08f);
        [SerializeField] private float _shinRadius = 0.055f;
        [SerializeField] private float _shinHeight = 0.5f;

        [Header("Interaction")]
        [SerializeField] private LayerMask _ballLayer = ~0;

        [Header("Created Legs")]
        [SerializeField] private TrackedLegController _leftLeg;
        [SerializeField] private TrackedLegController _rightLeg;

        public TrackedLegController LeftLeg => _leftLeg;
        public TrackedLegController RightLeg => _rightLeg;

        private bool _settingsDirty;
        private bool _loggedRigDiagnostics;

        public void SetFootBallInteraction(bool enabled, bool ensurePhysicalBallInteractor)
        {
            _enableFootBallInteraction = enabled;
            _ensurePhysicalBallInteractor = ensurePhysicalBallInteractor;
            EnsureRig();
            ApplySettingsToExistingLegs();
        }

        public void SetFootBallInteraction(bool enabled, bool ensurePhysicalBallInteractor, LayerMask ballLayer)
        {
            _ballLayer = ballLayer;
            SetFootBallInteraction(enabled, ensurePhysicalBallInteractor);
        }

        private void Start()
        {
            if (_createOnStart)
                EnsureRig();

            _settingsDirty = false;
        }

        private void Update()
        {
            if (!_liveUpdateInPlayMode || !_createOnStart || !_settingsDirty)
                return;

            ApplySettingsToExistingLegs();
            _settingsDirty = false;
        }

        private void OnValidate()
        {
            _legScale = Mathf.Max(0.05f, _legScale);
            _shinRadius = Mathf.Max(0.005f, _shinRadius);
            _shinHeight = Mathf.Max(0.02f, _shinHeight);
            _settingsDirty = true;
        }

        public void EnsureRig()
        {
            ResolveTrackingSpace();
            ResolveRenderCamera();
            if (_leftLeg != null && _leftLeg == _rightLeg)
            {
                Debug.LogWarning("[QuestControllerLegRig] Left and right leg references pointed to the same TrackedLegController. Rebinding the right leg.");
                _rightLeg = null;
            }

            _leftLeg = FindOrCreateLeg(
                TrackedLegHandedness.Left,
                _leftLeg,
                _leftPoseOffsetPosition,
                _leftPoseOffsetEuler);
            _rightLeg = FindOrCreateLeg(
                TrackedLegHandedness.Right,
                _rightLeg,
                _rightPoseOffsetPosition,
                _rightPoseOffsetEuler);

            if (_ensurePhysicalBallInteractor)
                EnsurePhysicalBallInteractor();

            LogRigDiagnostics();
        }

        private void ResolveTrackingSpace()
        {
            if (_trackingSpaceRoot != null)
                return;

            var origin = FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
            if (origin != null)
            {
                _trackingSpaceRoot = origin.CameraFloorOffsetObject != null
                    ? origin.CameraFloorOffsetObject.transform
                    : origin.transform;
            }
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

        private TrackedLegController FindOrCreateLeg(
            TrackedLegHandedness handedness,
            TrackedLegController current,
            Vector3 poseOffsetPosition,
            Vector3 poseOffsetEuler)
        {
            if (current == null)
                current = FindExistingLeg(handedness);

            if (current == null)
            {
                string legName = handedness == TrackedLegHandedness.Left ? "LeftTrackedLeg" : "RightTrackedLeg";
                var legGo = new GameObject(legName);
                legGo.transform.SetParent(transform, false);
                current = legGo.AddComponent<TrackedLegController>();
            }

            ConfigureLeg(current, handedness, poseOffsetPosition, poseOffsetEuler);
            return current;
        }

        private void ApplySettingsToExistingLegs()
        {
            if (_leftLeg == null || _rightLeg == null)
            {
                EnsureRig();
                return;
            }

            ResolveTrackingSpace();
            ResolveRenderCamera();
            ConfigureLeg(_leftLeg, TrackedLegHandedness.Left, _leftPoseOffsetPosition, _leftPoseOffsetEuler);
            ConfigureLeg(_rightLeg, TrackedLegHandedness.Right, _rightPoseOffsetPosition, _rightPoseOffsetEuler);
        }

        private void ConfigureLeg(
            TrackedLegController current,
            TrackedLegHandedness handedness,
            Vector3 poseOffsetPosition,
            Vector3 poseOffsetEuler)
        {
            float legScale = Mathf.Max(0.05f, _legScale);
            current.Configure(
                handedness,
                _trackingSpaceRoot,
                _renderCamera,
                poseOffsetPosition,
                poseOffsetEuler,
                _footColliderCenter * legScale,
                _footSize * legScale,
                _shinColliderCenter * legScale,
                _shinRadius * legScale,
                _shinHeight * legScale,
                _ballLayer,
                _buildDefaultVisuals,
                _enableFootBallInteraction);
        }

        private static TrackedLegController FindExistingLeg(TrackedLegHandedness handedness)
        {
            var legs = FindObjectsByType<TrackedLegController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (var leg in legs)
            {
                if (leg != null && leg.Handedness == handedness)
                    return leg;
            }

            return null;
        }

        private static void EnsurePhysicalBallInteractor()
        {
            var ball = FindFirstObjectByType<BallController>(FindObjectsInactive.Include);
            if (ball == null)
                return;

            if (ball.GetComponent<PhysicalBallInteractor>() == null)
                ball.gameObject.AddComponent<PhysicalBallInteractor>();
        }

        private void LogRigDiagnostics()
        {
            if (_loggedRigDiagnostics)
                return;

            _loggedRigDiagnostics = true;
            string left = DescribeLeg(_leftLeg);
            string right = DescribeLeg(_rightLeg);
            Debug.Log($"[QuestControllerLegRig] Leg bindings left={left} right={right} same={(_leftLeg != null && _leftLeg == _rightLeg)}");
        }

        private static string DescribeLeg(TrackedLegController leg)
        {
            if (leg == null)
                return "null";

            return $"{leg.name} hand={leg.Handedness} ref={leg.GetHashCode()} path='{GetHierarchyPath(leg.transform)}'";
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
    }
}
