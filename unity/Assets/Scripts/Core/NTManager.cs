// NTManager.cs — NetworkTables client data source.
// Connects to a RoboRIO / WPILib Simulation NetworkTables server.
// Falls back to FakeDataGenerator if connection fails.
//
// NOTE: This is a skeleton. Actual NT integration requires:
//   - FRC.NetworkTables NuGet package (via NuGetForUnity), OR
//   - WPILib ntcore native DLL loaded as Unity Plugin
// The structure is ready; fill in ReadNT* methods with actual NT calls.

using UnityEngine;

#if USE_NETWORKTABLES
// using NetworkTables;  // Uncomment when FRC.NetworkTables is installed
#endif

namespace SoccerBot
{
    public class NTManager : MonoBehaviour, IDataSource
    {
        // ── IDataSource ─────────────────────────────────────
        public string SourceName => "NetworkTables";
        public bool IsConnected => _connected;

        public RobotData   RobotData   { get; private set; }
        public ShooterData ShooterData { get; private set; }
        public BallData    BallData    { get; private set; }
        public VisionData  VisionData  { get; private set; }
        public SystemState State       { get; private set; }

        // ── Connection Settings ─────────────────────────────
        [Header("NetworkTables")]
        [SerializeField] private string _serverIP = "127.0.0.1";
        [SerializeField] private int    _port = 1735;
        [SerializeField] private float  _reconnectInterval = 2.0f;

        [Header("Fallback")]
        [Tooltip("If true, auto-enable FakeDataGenerator when NT connection fails.")]
        [SerializeField] private bool _autoFallback = true;

        // ── Internal ────────────────────────────────────────
        private bool   _connected;
        private float  _reconnectTimer;
        private FakeDataGenerator _fakeGen;

        // NT Topic paths (mirrors robot Constants.h)
        private const string KPoseX        = "/SmartDashboard/robot/pose/x";
        private const string KPoseY        = "/SmartDashboard/robot/pose/y";
        private const string KPoseRotation = "/SmartDashboard/robot/pose/rotation";
        private const string KVelVx        = "/SmartDashboard/robot/velocity/vx";
        private const string KVelVy        = "/SmartDashboard/robot/velocity/vy";
        private const string KVelOmega     = "/SmartDashboard/robot/velocity/omega";
        private const string KShooterAngle = "/SmartDashboard/shooter/angle";
        private const string KShooterSpeed = "/SmartDashboard/shooter/speed";
        private const string KShooterLoaded = "/SmartDashboard/shooter/is_loaded";
        private const string KShooterFiring = "/SmartDashboard/shooter/is_firing";
        private const string KBallX        = "/SmartDashboard/ball/pos/x";
        private const string KBallY        = "/SmartDashboard/ball/pos/y";
        private const string KBallZ        = "/SmartDashboard/ball/pos/z";
        private const string KTargetDetected = "/SmartDashboard/vision/target_detected";
        private const string KTargetX      = "/SmartDashboard/vision/target_x";
        private const string KTargetY      = "/SmartDashboard/vision/target_y";
        private const string KTargetDist   = "/SmartDashboard/vision/target_distance";
        private const string KSystemState  = "/SmartDashboard/system/state";

        void Start()
        {
            _fakeGen = GetComponent<FakeDataGenerator>();
            TryConnect();
        }

        public void UpdateData()
        {
            if (_connected)
            {
                ReadFromNT();
            }
            else
            {
                // Attempt reconnection
                _reconnectTimer += Time.deltaTime;
                if (_reconnectTimer >= _reconnectInterval)
                {
                    _reconnectTimer = 0f;
                    TryConnect();
                }

                // Use fake data as fallback
                if (_autoFallback && _fakeGen != null)
                {
                    _fakeGen.UpdateData();
                    RobotData   = _fakeGen.RobotData;
                    ShooterData = _fakeGen.ShooterData;
                    BallData    = _fakeGen.BallData;
                    VisionData  = _fakeGen.VisionData;
                    State       = _fakeGen.State;
                }
            }
        }

        // ── Connection ──────────────────────────────────────

        private void TryConnect()
        {
            // TODO: Replace with actual NetworkTables client connection
            // Example with FRC.NetworkTables:
            //   var client = new Nt4Client(_serverIP, _port);
            //   client.Connected += () => _connected = true;
            //   client.Start();

            // For now, never auto-connect (simulation mode)
            _connected = false;

            if (!_connected && _autoFallback && _fakeGen != null)
            {
                Debug.Log($"[NTManager] Cannot connect to {_serverIP}:{_port}. " +
                          "Using FakeDataGenerator.");
            }
        }

        // ── Data Reading ────────────────────────────────────

        private void ReadFromNT()
        {
            // TODO: Populate from actual NetworkTables topic subscriptions

            // Robot pose
            RobotData = new RobotData
            {
                position = new Vector3(
                    ReadNTFloat(KPoseX),
                    0f,
                    ReadNTFloat(KPoseY)
                ),
                rotation = Quaternion.Euler(0f, ReadNTFloat(KPoseRotation), 0f),
                velocity = new Vector3(
                    ReadNTFloat(KVelVx),
                    0f,
                    ReadNTFloat(KVelVy)
                ),
                angularVelocity = ReadNTFloat(KVelOmega)
            };

            // Shooter
            ShooterData = new ShooterData
            {
                angle = ReadNTFloat(KShooterAngle),
                speed = ReadNTFloat(KShooterSpeed),
                isLoaded = ReadNTBool(KShooterLoaded),
                isFiring = ReadNTBool(KShooterFiring)
            };

            // Ball
            BallData = new BallData
            {
                position = new Vector3(
                    ReadNTFloat(KBallX),
                    ReadNTFloat(KBallY),
                    ReadNTFloat(KBallZ)
                ),
                justFired = false,
                trajectory = null
            };

            // Vision
            VisionData = new VisionData
            {
                hasTarget = ReadNTBool(KTargetDetected),
                targetX = ReadNTFloat(KTargetX),
                targetY = ReadNTFloat(KTargetY),
                distance = ReadNTFloat(KTargetDist)
            };

            // System state
            string stateStr = ReadNTString(KSystemState);
            State = stateStr switch
            {
                "moving"   => SystemState.Moving,
                "shooting" => SystemState.Shooting,
                "error"    => SystemState.Error,
                _          => SystemState.Idle
            };
        }

        // ── NT Helper Methods (stubs — fill with real NT calls) ──

        private float ReadNTFloat(string key)
        {
            // TODO: return ntTable.GetNumber(key, 0.0f);
            return 0f;
        }

        private bool ReadNTBool(string key)
        {
            // TODO: return ntTable.GetBoolean(key, false);
            return false;
        }

        private string ReadNTString(string key)
        {
            // TODO: return ntTable.GetString(key, "");
            return "idle";
        }

        private double[] ReadNTDoubleArray(string key)
        {
            // TODO: return ntTable.GetNumberArray(key, new double[0]);
            return new double[0];
        }
    }
}
