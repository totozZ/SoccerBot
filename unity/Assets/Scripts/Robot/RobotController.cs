// RobotController.cs — Updates robot GameObject transform from GameManager data.
// Attach to the root GameObject of the robot model in the scene.

using UnityEngine;

namespace SoccerBot
{
    public class RobotController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Optional: manually assign robot transform. If empty, uses this GameObject.")]
        [SerializeField] private Transform _robotRoot;

        [Header("Smoothing")]
        [SerializeField] private bool _smoothPosition = true;
        [SerializeField] private float _positionSmoothTime = 0.1f;
        [SerializeField] private bool _smoothRotation = true;
        [SerializeField] private float _rotationSmoothTime = 0.08f;

        // Velocity-based smoothing state
        private Vector3 _velocityRef;
        private float   _angleVelocityRef;

        private Transform _target;

        // ────────────────────────────────────────────────────
        void Start()
        {
            _target = _robotRoot != null ? _robotRoot : transform;

            if (GameManager.Instance == null)
            {
                Debug.LogError("[RobotController] GameManager not found in scene!");
            }
        }

        void Update()
        {
            if (GameManager.Instance == null) return;

            var robotData = GameManager.Instance.Robot;

            // Position with smooth damping
            Vector3 targetPos = robotData.position;
            if (_smoothPosition)
            {
                _target.position = Vector3.SmoothDamp(
                    _target.position, targetPos,
                    ref _velocityRef, _positionSmoothTime);
            }
            else
            {
                _target.position = targetPos;
            }

            // Rotation with smooth damping
            if (_smoothRotation)
            {
                float targetAngle = robotData.rotation.eulerAngles.y;
                float currentAngle = _target.eulerAngles.y;
                float smoothed = Mathf.SmoothDampAngle(
                    currentAngle, targetAngle,
                    ref _angleVelocityRef, _rotationSmoothTime);
                _target.rotation = Quaternion.Euler(0f, smoothed, 0f);
            }
            else
            {
                _target.rotation = robotData.rotation;
            }
        }
    }
}
