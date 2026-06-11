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
        [SerializeField] private bool _buildDefaultVisuals = true;
        [SerializeField] private bool _ensurePhysicalBallInteractor = true;

        [Header("Tracking Space")]
        [SerializeField] private Transform _trackingSpaceRoot;

        [Header("Left Foot Offset")]
        [SerializeField] private Vector3 _leftPoseOffsetPosition = new Vector3(0f, 0.3f, 0.1f);
        [SerializeField] private Vector3 _leftPoseOffsetEuler = Vector3.zero;

        [Header("Right Foot Offset")]
        [SerializeField] private Vector3 _rightPoseOffsetPosition = new Vector3(0f, 0.3f, 0.1f);
        [SerializeField] private Vector3 _rightPoseOffsetEuler = Vector3.zero;

        [Header("Interaction")]
        [SerializeField] private LayerMask _ballLayer = ~0;

        [Header("Created Legs")]
        [SerializeField] private TrackedLegController _leftLeg;
        [SerializeField] private TrackedLegController _rightLeg;

        public TrackedLegController LeftLeg => _leftLeg;
        public TrackedLegController RightLeg => _rightLeg;

        private void Start()
        {
            if (_createOnStart)
                EnsureRig();
        }

        public void EnsureRig()
        {
            ResolveTrackingSpace();
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
                Transform parent = _trackingSpaceRoot != null ? _trackingSpaceRoot : transform;
                string legName = handedness == TrackedLegHandedness.Left ? "LeftTrackedLeg" : "RightTrackedLeg";
                var legGo = new GameObject(legName);
                legGo.transform.SetParent(parent, false);
                current = legGo.AddComponent<TrackedLegController>();
            }

            current.Configure(
                handedness,
                _trackingSpaceRoot,
                poseOffsetPosition,
                poseOffsetEuler,
                _ballLayer,
                _buildDefaultVisuals);
            return current;
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
    }
}
