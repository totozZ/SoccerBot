// RobotVisuals.cs — Animates shooter sub-parts (Hood, Flywheels) from shooter data.
// Attach to the robot root. Assign child Transforms in the Inspector.

using UnityEngine;

namespace SoccerBot
{
    public class RobotVisuals : MonoBehaviour
    {
        [Header("Hood (Shooter Angle)")]
        [Tooltip("The hood/pitch GameObject. Rotates around its local X axis.")]
        [SerializeField] private Transform _hoodTransform;
        [SerializeField] private Vector3 _hoodRotationAxis = Vector3.right;
        [SerializeField] private float _hoodSmoothTime = 0.1f;

        [Header("Flywheels (Spinning)")]
        [Tooltip("Flywheel transforms (will spin around their local Z or X).")]
        [SerializeField] private Transform _topFlywheel;
        [SerializeField] private Transform _bottomFlywheel;
        [SerializeField] private Vector3 _flywheelRotationAxis = Vector3.right;
        [Tooltip("Visual RPM multiplier (how fast the model spins relative to actual RPM).")]
        [SerializeField] private float _flywheelVisualMultiplier = 0.1f;

        [Header("Ball Loaded Indicator")]
        [Tooltip("Optional: a GameObject that appears when ball is loaded.")]
        [SerializeField] private GameObject _ballLoadedIndicator;

        [Header("Fire Effects")]
        [Tooltip("Optional: ParticleSystem triggered on fire.")]
        [SerializeField] private ParticleSystem _fireParticles;

        // ── Internal ────────────────────────────────────────
        private float _hoodAngleVelocity;
        private float _currentHoodAngle;

        void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnShotFired += OnShotFired;
            }
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

            var shooter = GameManager.Instance.Shooter;

            // ── Hood Angle ──────────────────────────────────
            if (_hoodTransform != null)
            {
                float targetAngle = shooter.angle;
                _currentHoodAngle = Mathf.SmoothDampAngle(
                    _currentHoodAngle, targetAngle,
                    ref _hoodAngleVelocity, _hoodSmoothTime);
                _hoodTransform.localRotation = Quaternion.AngleAxis(
                    _currentHoodAngle, _hoodRotationAxis);
            }

            // ── Flywheel Spinning ───────────────────────────
            float rotationSpeed = shooter.speed * _flywheelVisualMultiplier * Time.deltaTime;

            if (_topFlywheel != null)
            {
                _topFlywheel.Rotate(_flywheelRotationAxis, rotationSpeed, Space.Self);
            }
            if (_bottomFlywheel != null)
            {
                _bottomFlywheel.Rotate(_flywheelRotationAxis, rotationSpeed, Space.Self);
            }

            // ── Ball Loaded Indicator ───────────────────────
            if (_ballLoadedIndicator != null)
            {
                _ballLoadedIndicator.SetActive(shooter.isLoaded);
            }
        }

        // ── Shot Fired ──────────────────────────────────────
        private void OnShotFired()
        {
            if (_fireParticles != null)
            {
                _fireParticles.Play();
            }
        }
    }
}
