using UnityEngine;

namespace SoccerBot
{
    [System.Serializable]
    public struct SmartBallData
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 angularVelocity;
        public float timestamp;
        public bool isConnected;
        public string sourceName;

        public Vector3 EulerAngles => rotation.eulerAngles;
    }
}
