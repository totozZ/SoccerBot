// RobotData.cs — Data models shared across all Unity scripts.
// These structs mirror the NetworkTables schema defined in the robot Constants.h.

using System.Collections.Generic;
using UnityEngine;

namespace SoccerBot
{
    /// <summary>Robot pose and velocity on the field.</summary>
    [System.Serializable]
    public struct RobotData
    {
        /// <summary>Position in field coordinates (meters).
        /// x=right, y=up (always 0 on flat field), z=forward.</summary>
        public Vector3 position;
        /// <summary>Rotation quaternion. Y-axis = robot heading.</summary>
        public Quaternion rotation;
        /// <summary>Velocity in m/s.</summary>
        public Vector3 velocity;
        /// <summary>Angular velocity in deg/s around Y axis.</summary>
        public float angularVelocity;
    }

    /// <summary>Shooter subsystem state.</summary>
    [System.Serializable]
    public struct ShooterData
    {
        /// <summary>Hood angle in degrees.</summary>
        public float angle;
        /// <summary>Flywheel speed in RPM.</summary>
        public float speed;
        /// <summary>True if a ball is detected in the loader.</summary>
        public bool isLoaded;
        /// <summary>True while the firing sequence is active.</summary>
        public bool isFiring;
    }

    /// <summary>Soccer ball state.</summary>
    [System.Serializable]
    public struct BallData
    {
        /// <summary>Current ball position in world space.</summary>
        public Vector3 position;
        /// <summary>True when a new shot is fired (rising edge).</summary>
        public bool justFired;
        /// <summary>Predicted trajectory points (for visualization).</summary>
        public List<Vector3> trajectory;
    }

    /// <summary>Vision / Limelight data.</summary>
    [System.Serializable]
    public struct VisionData
    {
        /// <summary>True if Limelight has a valid target locked.</summary>
        public bool hasTarget;
        /// <summary>Horizontal offset from crosshair in degrees.</summary>
        public float targetX;
        /// <summary>Vertical offset from crosshair in degrees.</summary>
        public float targetY;
        /// <summary>Estimated distance to target in meters.</summary>
        public float distance;
    }

    /// <summary>Overall system state string (matches C++ side).</summary>
    public enum SystemState
    {
        Idle,
        Moving,
        Shooting,
        Error,
        Simulation
    }
}
