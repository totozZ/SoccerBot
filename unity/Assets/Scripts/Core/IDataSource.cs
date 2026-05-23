// IDataSource.cs — Abstraction for robot data sources.
// Implementations: NTManager (NetworkTables), FakeDataGenerator (standalone).

namespace SoccerBot
{
    public interface IDataSource
    {
        /// <summary>Human-readable source name for UI display.</summary>
        string SourceName { get; }

        /// <summary>True when the data source is actively receiving data.</summary>
        bool IsConnected { get; }

        /// <summary>Latest robot pose & velocity.</summary>
        RobotData RobotData { get; }

        /// <summary>Latest shooter state.</summary>
        ShooterData ShooterData { get; }

        /// <summary>Latest ball state.</summary>
        BallData BallData { get; }

        /// <summary>Latest vision data.</summary>
        VisionData VisionData { get; }

        /// <summary>Current system state.</summary>
        SystemState State { get; }

        /// <summary>Called each frame to update internal data.</summary>
        void UpdateData();
    }
}
