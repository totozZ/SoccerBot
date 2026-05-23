// FakeDataGenerator.cs — Standalone simulation data source.
// Generates fake robot data so Unity can run without a real robot or WPILib Sim.
// Attach to the same GameObject as GameManager.

using System.Collections.Generic;
using UnityEngine;

namespace SoccerBot
{
    public class FakeDataGenerator : MonoBehaviour, IDataSource
    {
        // ── IDataSource ─────────────────────────────────────
        public string SourceName => "FakeData (Sim)";
        public bool IsConnected => true; // Always "connected" in sim mode

        public RobotData   RobotData   { get; private set; }
        public ShooterData ShooterData { get; private set; }
        public BallData    BallData    { get; private set; }
        public VisionData  VisionData  { get; private set; }
        public SystemState State       { get; private set; }

        // ── Simulation Parameters ───────────────────────────
        [Header("Robot Movement")]
        [SerializeField] private float _moveSpeed = 2.0f;
        [SerializeField] private float _turnSpeed = 45.0f;
        [SerializeField] private float _fieldBoundary = 8.0f;

        [Header("Shooter Simulation")]
        [SerializeField] private float _fireInterval = 5.0f;
        [SerializeField] private float _shooterRPM = 4000.0f;
        [SerializeField] private float _hoodAngle = 25.0f;

        [Header("Ball Trajectory")]
        [SerializeField] private float _launchSpeed = 15.0f;
        [SerializeField] private float _launchAngle = 30.0f;
        [SerializeField] private float _gravity = 9.81f;

        // ── Internal State ──────────────────────────────────
        private Vector3 _robotPosition = Vector3.zero;
        private float   _robotHeading = 0f;
        private float   _time;
        private float   _fireTimer;
        private bool    _ballInFlight;
        private Vector3 _ballPosition;
        private Vector3 _ballVelocity;
        private float   _ballFlightTime;

        void Start()
        {
            _robotPosition = Vector3.zero;
            _robotHeading = 0f;
            _fireTimer = _fireInterval;
            ShooterData = new ShooterData
            {
                angle = _hoodAngle,
                speed = _shooterRPM,
                isLoaded = true,
                isFiring = false
            };
            State = SystemState.Simulation;
        }

        public void UpdateData()
        {
            _time += Time.deltaTime;

            UpdateRobotMovement();
            UpdateShooterSimulation();
            UpdateBallPhysics();
            UpdateVisionSimulation();

            State = _ballInFlight ? SystemState.Shooting : SystemState.Moving;
        }

        // ── Robot Movement (circular patrol pattern) ────────
        private void UpdateRobotMovement()
        {
            // Move in a lazy figure-8 / patrol pattern
            float t = _time * 0.5f;
            float targetX = Mathf.Sin(t) * _fieldBoundary * 0.7f;
            float targetZ = Mathf.Cos(t * 0.7f) * _fieldBoundary * 0.5f;

            // Smooth movement toward target
            Vector3 target = new Vector3(targetX, 0f, targetZ);
            _robotPosition = Vector3.Lerp(_robotPosition, target, Time.deltaTime * 0.5f);

            // Heading: face direction of movement
            Vector3 moveDir = (target - _robotPosition).normalized;
            if (moveDir.magnitude > 0.01f)
            {
                float targetHeading = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
                _robotHeading = Mathf.LerpAngle(_robotHeading, targetHeading, Time.deltaTime * 2f);
            }

            // Build RobotData
            RobotData = new RobotData
            {
                position = _robotPosition,
                rotation = Quaternion.Euler(0f, _robotHeading, 0f),
                velocity = new Vector3(
                    Mathf.Sin(_robotHeading * Mathf.Deg2Rad) * _moveSpeed,
                    0f,
                    Mathf.Cos(_robotHeading * Mathf.Deg2Rad) * _moveSpeed
                ),
                angularVelocity = 0f
            };
        }

        // ── Shooter Simulation ──────────────────────────────
        private void UpdateShooterSimulation()
        {
            _fireTimer -= Time.deltaTime;

            var sd = ShooterData;

            if (_fireTimer <= 0f && !_ballInFlight && sd.isLoaded)
            {
                // Start firing sequence
                sd.isFiring = true;
                sd.isLoaded = false;

                // Launch ball
                FireBall();

                _fireTimer = _fireInterval;
            }
            else if (_fireTimer <= -0.5f)
            {
                // End firing sequence after 0.5s
                sd.isFiring = false;
                sd.isLoaded = true;
            }

            ShooterData = sd;
        }

        private void FireBall()
        {
            _ballInFlight = true;
            _ballFlightTime = 0f;

            // Launch from robot position + forward offset + height
            Vector3 launchPos = _robotPosition +
                new Vector3(0f, 0.5f, 0f) +
                (Quaternion.Euler(0f, _robotHeading, 0f) * Vector3.forward * 0.3f);

            _ballPosition = launchPos;

            // Calculate initial velocity
            float radHeading = _robotHeading * Mathf.Deg2Rad;
            float radLaunch = _launchAngle * Mathf.Deg2Rad;
            float vx = Mathf.Sin(radHeading) * Mathf.Cos(radLaunch) * _launchSpeed;
            float vy = Mathf.Sin(radLaunch) * _launchSpeed;
            float vz = Mathf.Cos(radHeading) * Mathf.Cos(radLaunch) * _launchSpeed;
            _ballVelocity = new Vector3(vx, vy, vz);

            // Generate trajectory preview
            var trajectory = new List<Vector3>();
            Vector3 trajPos = launchPos;
            Vector3 trajVel = _ballVelocity;
            float dt = 0.05f;
            for (int i = 0; i < 100; i++)
            {
                trajPos += trajVel * dt;
                trajVel.y -= _gravity * dt;
                trajectory.Add(trajPos);
                if (trajPos.y < 0f) break;
            }

            BallData = new BallData
            {
                position = launchPos,
                justFired = true,
                trajectory = trajectory
            };
        }

        // ── Ball Physics ────────────────────────────────────
        private void UpdateBallPhysics()
        {
            if (!_ballInFlight) return;

            _ballFlightTime += Time.deltaTime;
            _ballVelocity.y -= _gravity * Time.deltaTime;
            _ballPosition += _ballVelocity * Time.deltaTime;

            var bd = BallData;
            bd.position = _ballPosition;
            bd.justFired = false;

            // Ball hit ground → stop
            if (_ballPosition.y < 0.05f)
            {
                _ballPosition.y = 0.05f;
                _ballInFlight = false;
            }

            BallData = bd;
        }

        // ── Vision Simulation ───────────────────────────────
        private void UpdateVisionSimulation()
        {
            // Fake target detection: sometimes sees target, sometimes not
            bool hasTarget = (Mathf.Sin(_time * 0.7f) > 0.2f);

            VisionData = new VisionData
            {
                hasTarget = hasTarget,
                targetX = hasTarget ? Mathf.Sin(_time * 1.3f) * 5f : 0f,
                targetY = hasTarget ? Mathf.Cos(_time * 0.9f) * 3f : 0f,
                distance = hasTarget ? 3.0f + Mathf.Sin(_time * 0.3f) * 2f : 0f
            };
        }

        // ── Debug Visualization ─────────────────────────────
        void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            // Draw robot
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(_robotPosition, new Vector3(0.8f, 0.2f, 0.8f));

            // Draw heading
            Vector3 forward = Quaternion.Euler(0f, _robotHeading, 0f) * Vector3.forward;
            Gizmos.color = Color.red;
            Gizmos.DrawRay(_robotPosition, forward * 1.5f);

            // Draw ball
            if (_ballInFlight)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(_ballPosition, 0.15f);

                // Draw trajectory
                Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
                var traj = BallData.trajectory;
                if (traj != null && traj.Count > 1)
                {
                    for (int i = 0; i < traj.Count - 1; i++)
                    {
                        Gizmos.DrawLine(traj[i], traj[i + 1]);
                    }
                }
            }
        }
    }
}
