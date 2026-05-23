// SmoothFollow.cs — Third-person camera that smoothly follows the robot.
// Attach to the Main Camera.

using UnityEngine;

namespace SoccerBot
{
    public class SmoothFollow : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The robot transform to follow. Auto-detects if empty.")]
        [SerializeField] private Transform _target;

        [Header("Offset")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 5f, -8f);
        [SerializeField] private float _lookAtHeight = 0.5f;

        [Header("Smoothing")]
        [SerializeField] private float _positionSmoothTime = 0.2f;
        [SerializeField] private float _rotationSmoothTime = 0.15f;

        private Vector3 _velocityRef;
        private Vector3 _lookTarget;

        void Start()
        {
            if (_target == null)
            {
                // Try to find robot
                var robotCtrl = FindObjectOfType<RobotController>();
                if (robotCtrl != null)
                {
                    _target = robotCtrl.transform;
                }
                else
                {
                    // Fallback: find any GameObject named "Robot"
                    var robotGO = GameObject.Find("Robot");
                    if (robotGO != null)
                        _target = robotGO.transform;
                }
            }

            if (_target == null)
            {
                Debug.LogWarning("[SmoothFollow] No target found. Camera will stay in place.");
            }
        }

        void LateUpdate()
        {
            if (_target == null) return;

            // Desired position: target + offset (in world space)
            Vector3 desiredPos = _target.position + _offset;

            // Smooth move
            transform.position = Vector3.SmoothDamp(
                transform.position, desiredPos,
                ref _velocityRef, _positionSmoothTime);

            // Look at target (with height offset)
            _lookTarget = _target.position + Vector3.up * _lookAtHeight;
            Quaternion desiredRot = Quaternion.LookRotation(_lookTarget - transform.position);

            transform.rotation = Quaternion.Slerp(
                transform.rotation, desiredRot,
                Time.deltaTime / _rotationSmoothTime);
        }
    }
}
