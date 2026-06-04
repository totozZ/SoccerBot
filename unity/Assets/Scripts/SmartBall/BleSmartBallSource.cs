using UnityEngine;

namespace SoccerBot
{
    public class BleSmartBallSource : MonoBehaviour, ISmartBallSource
    {
        [Header("BLE Target")]
        [SerializeField] private string _deviceName = "BS-BT91";
        [SerializeField] private bool _autoConnectOnAndroid = true;

        [Header("Fallback Pose")]
        [SerializeField] private Vector3 _fallbackPosition = new Vector3(0f, 0.22f, -2.2f);

        private SmartBallData _data;
        private bool _initialized;

        public string SourceName => _initialized ? $"BLE SmartBall ({_deviceName})" : $"BLE SmartBall ({_deviceName}, waiting)";
        public bool IsConnected => false;
        public SmartBallData Data => _data;

        void Awake()
        {
            ResetOrientation();
        }

        public void UpdateData()
        {
            if (!_initialized)
            {
                _initialized = true;
#if UNITY_ANDROID && !UNITY_EDITOR
                if (_autoConnectOnAndroid)
                {
                    Debug.Log($"[BleSmartBallSource] Android BLE bridge is not implemented yet. Target device: {_deviceName}");
                }
#endif
            }

            _data.timestamp = Time.time;
            _data.isConnected = IsConnected;
            _data.sourceName = SourceName;
        }

        public void ResetOrientation()
        {
            _data = new SmartBallData
            {
                position = _fallbackPosition,
                rotation = Quaternion.identity,
                angularVelocity = Vector3.zero,
                timestamp = Time.time,
                isConnected = IsConnected,
                sourceName = SourceName
            };
        }
    }
}
