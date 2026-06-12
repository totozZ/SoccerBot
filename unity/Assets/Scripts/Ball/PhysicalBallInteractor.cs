using System;
using UnityEngine;

namespace SoccerBot
{
    [DefaultExecutionOrder(-20)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BallController))]
    public class PhysicalBallInteractor : MonoBehaviour
    {
        public event Action<FootContactData, Vector3, float> PhysicalImpulseApplied;

        [Header("References")]
        [SerializeField] private BallController _ball;
        [SerializeField] private Rigidbody _ballBody;
        [SerializeField] private MatchFlowController _matchFlow;

        [Header("Routing")]
        [SerializeField] private bool _routeToMatchFlow = true;
        [SerializeField] private bool _applyImpulseOutsideMatchFlow = true;

        [Header("Physical Impulse")]
        [SerializeField] private float _minImpulseSpeed = 0.3f;
        [SerializeField] private float _minImpulse = 0.9f;
        [SerializeField] private float _maxImpulse = 5.2f;
        [SerializeField, Range(0f, 1f)] private float _swingDirectionWeight = 0.75f;
        [SerializeField] private float _lift = 0.09f;
        [SerializeField] private float _spinTorque = 0.06f;
        [SerializeField] private float _impulseCooldown = 0.12f;
        [SerializeField] private bool _debugImpulses = true;

        [Header("Passive Control")]
        [SerializeField] private bool _enablePassiveControl = false;
        [SerializeField] private float _passiveControlCooldown = 0.025f;
        [SerializeField, Range(0f, 1f)] private float _passiveVelocityDamping = 0.45f;
        [SerializeField] private float _passiveStopSpeed = 0.18f;
        [SerializeField] private float _passiveSeparationImpulse = 0.04f;

        private float _lastImpulseTime = -999f;
        private float _lastPassiveControlTime = -999f;

        public float MinImpulseSpeed => _minImpulseSpeed;
        public float MinImpulse => _minImpulse;
        public float MaxImpulse => _maxImpulse;
        public float SwingDirectionWeight => _swingDirectionWeight;
        public float Lift => _lift;
        public float SpinTorque => _spinTorque;
        public float ImpulseCooldown => _impulseCooldown;

        public void ConfigureRouting(bool routeToMatchFlow, bool applyImpulseOutsideMatchFlow, bool debugImpulses)
        {
            _routeToMatchFlow = routeToMatchFlow;
            _applyImpulseOutsideMatchFlow = applyImpulseOutsideMatchFlow;
            _debugImpulses = debugImpulses;
            ResolveReferences();
        }

        public void ConfigurePhysicalImpulse(
            float minImpulseSpeed,
            float minImpulse,
            float maxImpulse,
            float swingDirectionWeight,
            float lift,
            float spinTorque,
            float impulseCooldown)
        {
            _minImpulseSpeed = Mathf.Max(0f, minImpulseSpeed);
            _minImpulse = Mathf.Max(0f, minImpulse);
            _maxImpulse = Mathf.Max(_minImpulse, maxImpulse);
            _swingDirectionWeight = Mathf.Clamp01(swingDirectionWeight);
            _lift = Mathf.Max(0f, lift);
            _spinTorque = Mathf.Max(0f, spinTorque);
            _impulseCooldown = Mathf.Max(0f, impulseCooldown);
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            TrackedLegController.GlobalFootContact += HandleFootContact;
        }

        private void OnDisable()
        {
            TrackedLegController.GlobalFootContact -= HandleFootContact;
        }

        private void ResolveReferences()
        {
            if (_ball == null)
                _ball = GetComponent<BallController>();
            if (_ball != null)
            {
                _ballBody = _ball.EnsurePhysicsComponents();
                _ball.SetPhysicalSimulation(false);
            }
            if (_matchFlow == null)
                _matchFlow = FindFirstObjectByType<MatchFlowController>(FindObjectsInactive.Include);
        }

        private void HandleFootContact(FootContactData data)
        {
            if (!IsThisBall(data))
                return;

            if (_routeToMatchFlow && _matchFlow != null && _matchFlow.HandleFootBallContact(data))
                return;

            if (!_applyImpulseOutsideMatchFlow || data.ContactSpeed < _minImpulseSpeed)
            {
                ApplyPassiveControl(data);
                return;
            }

            if (Time.time - _lastImpulseTime < _impulseCooldown)
                return;

            ApplyPhysicalImpulse(data);
        }

        private bool IsThisBall(FootContactData data)
        {
            if (data.BallCollider == null)
                return false;
            if (_ballBody != null && data.BallBody == _ballBody)
                return true;
            return data.BallCollider.transform == transform || data.BallCollider.transform.IsChildOf(transform);
        }

        private void ApplyPhysicalImpulse(FootContactData data)
        {
            if (_ball == null || _ballBody == null)
                return;

            _lastImpulseTime = Time.time;
            _ball.SetPhysicalSimulation(true, false);

            Vector3 swing = data.SwingDirection.sqrMagnitude > 0.0001f ? data.SwingDirection.normalized : transform.forward;
            Vector3 face = data.FootForward.sqrMagnitude > 0.0001f ? data.FootForward.normalized : swing;
            Vector3 direction = Vector3.Slerp(face, swing, _swingDirectionWeight);
            direction.y += _lift;
            if (direction.sqrMagnitude < 0.0001f)
                direction = swing;
            direction.Normalize();

            float shotAssist01 = 0f;
            if (_matchFlow != null &&
                _matchFlow.TryAssistPhysicalShotDirection(data, direction, out Vector3 assistedDirection, out shotAssist01))
            {
                direction = assistedDirection;
            }

            float impulse = Mathf.Lerp(_minImpulse, _maxImpulse, Mathf.Clamp01(data.Power01));
            impulse *= Mathf.Lerp(0.75f, 1.2f, Mathf.Clamp01(data.Accuracy01));

            _ballBody.AddForceAtPosition(direction * impulse, data.ContactPoint, ForceMode.Impulse);
            Vector3 torqueAxis = Vector3.Cross(Vector3.up, direction);
            if (torqueAxis.sqrMagnitude > 0.0001f)
                _ballBody.AddTorque(torqueAxis.normalized * impulse * _spinTorque, ForceMode.Impulse);

            if (_debugImpulses)
                Debug.Log($"[PhysicalBall] {data.Foot} impulse={impulse:0.00} dir={direction} speed={data.ContactSpeed:0.00} assist={shotAssist01:0.00}");

            PhysicalImpulseApplied?.Invoke(data, direction, impulse);
        }

        private void ApplyPassiveControl(FootContactData data)
        {
            if (!_enablePassiveControl || _ball == null || _ballBody == null)
                return;
            if (Time.time - _lastPassiveControlTime < _passiveControlCooldown)
                return;

            _lastPassiveControlTime = Time.time;
            _ball.SetPhysicalSimulation(true, false);

            Vector3 velocity = _ballBody.linearVelocity;
            Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
            if (horizontalVelocity.magnitude <= _passiveStopSpeed)
            {
                horizontalVelocity = Vector3.zero;
            }
            else
            {
                horizontalVelocity *= Mathf.Clamp01(_passiveVelocityDamping);
            }

            _ballBody.linearVelocity = new Vector3(horizontalVelocity.x, Mathf.Min(velocity.y, 0f), horizontalVelocity.z);
            _ballBody.angularVelocity *= Mathf.Clamp01(_passiveVelocityDamping);

            Vector3 away = data.BallClosestPoint - data.FootClosestPoint;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
            {
                away = _ballBody.worldCenterOfMass - data.FootPosition;
                away.y = 0f;
            }

            if (away.sqrMagnitude > 0.0001f && _passiveSeparationImpulse > 0f)
                _ballBody.AddForce(away.normalized * _passiveSeparationImpulse, ForceMode.Impulse);
        }
    }
}
