using UnityEngine;

namespace SoccerBot
{
    public class SmartBallController : MonoBehaviour
    {
        [SerializeField] private Transform _ballTransform;
        [SerializeField, Range(0f, 20f)] private float _rotationSmoothing = 10f;
        [SerializeField, Range(0f, 20f)] private float _positionSmoothing = 8f;
        [SerializeField] private Vector3 _position = new Vector3(0f, 0.22f, -2.2f);
        [SerializeField] private Vector3 _scale = Vector3.one * 0.22f;

        private ISmartBallSource _source;
        private bool _hasSource;

        public SmartBallData CurrentData { get; private set; }

        void Awake()
        {
            if (_ballTransform == null) _ballTransform = transform;
            _ballTransform.position = _position;
            _ballTransform.localScale = _scale;
        }

        public void SetSource(ISmartBallSource source)
        {
            _source = source;
            _hasSource = source != null;
            if (!_hasSource) return;

            CurrentData = _source.Data;
            _ballTransform.position = CurrentData.position;
            _ballTransform.rotation = CurrentData.rotation;
        }

        public void Tick()
        {
            if (!_hasSource) return;

            _source.UpdateData();
            CurrentData = _source.Data;

            float posLerp = 1f - Mathf.Exp(-_positionSmoothing * Time.deltaTime);
            float rotLerp = 1f - Mathf.Exp(-_rotationSmoothing * Time.deltaTime);
            _ballTransform.position = Vector3.Lerp(_ballTransform.position, CurrentData.position, posLerp);
            _ballTransform.rotation = Quaternion.Slerp(_ballTransform.rotation, CurrentData.rotation, rotLerp);
        }

        public void ResetOrientation()
        {
            if (!_hasSource) return;
            _source.ResetOrientation();
            CurrentData = _source.Data;
            _ballTransform.position = CurrentData.position;
            _ballTransform.rotation = CurrentData.rotation;
        }
    }
}
