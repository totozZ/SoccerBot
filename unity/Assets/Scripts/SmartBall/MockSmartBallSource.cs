using UnityEngine;

namespace SoccerBot
{
    public class MockSmartBallSource : MonoBehaviour, ISmartBallSource
    {
        [Header("Mock Rotation")]
        [SerializeField] private Vector3 _baseAngularVelocity = new Vector3(25f, 110f, 35f);
        [SerializeField] private Vector3 _wobbleAmplitude = new Vector3(12f, 18f, 10f);
        [SerializeField] private float _wobbleFrequency = 0.8f;

        [Header("Mock Motion")]
        [SerializeField] private Vector3 _centerPosition = new Vector3(0f, 0.22f, -2.2f);
        [SerializeField] private Vector3 _positionAmplitude = new Vector3(1.4f, 0.18f, 1.2f);
        [SerializeField] private float _positionFrequency = 0.55f;
        [SerializeField] private bool _alwaysConnected = true;

        private Quaternion _rotation = Quaternion.identity;
        private SmartBallData _data;

        public string SourceName => "Mock SmartBall";
        public bool IsConnected => _alwaysConnected;
        public SmartBallData Data => _data;

        void Awake()
        {
            ResetOrientation();
        }

        public void UpdateData()
        {
            float t = Time.time;
            Vector3 wobble = new Vector3(
                Mathf.Sin(t * _wobbleFrequency * 0.91f) * _wobbleAmplitude.x,
                Mathf.Cos(t * _wobbleFrequency * 1.17f) * _wobbleAmplitude.y,
                Mathf.Sin(t * _wobbleFrequency * 1.43f) * _wobbleAmplitude.z);

            Vector3 angularVelocity = _baseAngularVelocity + wobble;
            _rotation = Quaternion.Euler(angularVelocity * Time.deltaTime) * _rotation;

            Vector3 position = _centerPosition + new Vector3(
                Mathf.Sin(t * _positionFrequency) * _positionAmplitude.x,
                Mathf.Abs(Mathf.Sin(t * _positionFrequency * 1.6f)) * _positionAmplitude.y,
                Mathf.Cos(t * _positionFrequency * 0.83f) * _positionAmplitude.z);

            _data = new SmartBallData
            {
                position = position,
                rotation = _rotation,
                angularVelocity = angularVelocity,
                timestamp = Time.time,
                isConnected = IsConnected,
                sourceName = SourceName
            };
        }

        public void ResetOrientation()
        {
            _rotation = Quaternion.identity;
            _data = new SmartBallData
            {
                position = _centerPosition,
                rotation = _rotation,
                angularVelocity = Vector3.zero,
                timestamp = Time.time,
                isConnected = IsConnected,
                sourceName = SourceName
            };
        }
    }
}
