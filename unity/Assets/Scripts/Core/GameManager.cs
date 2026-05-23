// GameManager.cs — Central singleton hub for robot data.
// Attach to a GameObject in the scene (e.g. "GameManager").
// All other scripts read from GameManager.Instance.

using UnityEngine;

namespace SoccerBot
{
    public class GameManager : MonoBehaviour
    {
        // ── Singleton ───────────────────────────────────────
        public static GameManager Instance { get; private set; }

        // ── Data Source ─────────────────────────────────────
        [Header("Data Source")]
        [Tooltip("Auto-detected at startup. Set manually for testing.")]
        [SerializeField] private MonoBehaviour _dataSourceOverride;

        private IDataSource _dataSource;
        public IDataSource DataSource => _dataSource;

        // ── Current Data (read-only for other scripts) ──────
        public RobotData   Robot   { get; private set; }
        public ShooterData Shooter { get; private set; }
        public BallData    Ball    { get; private set; }
        public VisionData  Vision  { get; private set; }
        public SystemState State   { get; private set; }

        // ── Events ──────────────────────────────────────────
        /// <summary>Fired when robot pose changes (every frame).</summary>
        public System.Action<RobotData> OnRobotUpdated;
        /// <summary>Fired when shooter state changes.</summary>
        public System.Action<ShooterData> OnShooterUpdated;
        /// <summary>Fired on the rising edge of a shot (isFiring: false→true).</summary>
        public System.Action OnShotFired;
        /// <summary>Fired when data source connection status changes.</summary>
        public System.Action<bool> OnConnectionChanged;

        // ── Internals ───────────────────────────────────────
        private bool _wasFiring;

        // ────────────────────────────────────────────────────
        void Awake()
        {
            // Singleton setup
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Pick data source
            if (_dataSourceOverride != null)
            {
                _dataSource = _dataSourceOverride as IDataSource;
            }
            else
            {
                _dataSource = GetComponent<IDataSource>();
            }

            // If no IDataSource found on this GameObject, try to find FakeDataGenerator
            if (_dataSource == null)
            {
                var fakeGen = FindObjectOfType<FakeDataGenerator>();
                if (fakeGen != null)
                {
                    _dataSource = fakeGen;
                }
            }

            if (_dataSource == null)
            {
                Debug.LogWarning("[GameManager] No IDataSource found. " +
                    "Add FakeDataGenerator or NTManager to the scene.");
            }
            else
            {
                Debug.Log($"[GameManager] Using data source: {_dataSource.SourceName}");
            }
        }

        void Update()
        {
            if (_dataSource == null) return;

            // Poll data source
            _dataSource.UpdateData();

            // Cache local copies
            bool prevConnected = _dataSource.IsConnected;
            Robot   = _dataSource.RobotData;
            Shooter = _dataSource.ShooterData;
            Ball    = _dataSource.BallData;
            Vision  = _dataSource.VisionData;
            State   = _dataSource.State;

            // Fire events
            OnRobotUpdated?.Invoke(Robot);
            OnShooterUpdated?.Invoke(Shooter);

            // Rising edge detection for shot fired
            if (!_wasFiring && Shooter.isFiring)
            {
                OnShotFired?.Invoke();
            }
            _wasFiring = Shooter.isFiring;

            // Connection change
            if (prevConnected != _dataSource.IsConnected)
            {
                OnConnectionChanged?.Invoke(_dataSource.IsConnected);
            }
        }

        // ── Public helpers ──────────────────────────────────

        /// <summary>Returns the connection status string for UI.</summary>
        public string GetStatusString()
        {
            if (_dataSource == null) return "No Data Source";
            var conn = _dataSource.IsConnected ? "Connected" : "Disconnected";
            return $"{_dataSource.SourceName} | {conn} | State: {State}";
        }
    }
}
