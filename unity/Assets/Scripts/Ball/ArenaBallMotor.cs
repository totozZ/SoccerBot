using System;
using UnityEngine;

namespace SoccerBot
{
    [DefaultExecutionOrder(-5)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BallController))]
    public sealed class ArenaBallMotor : MonoBehaviour
    {
        public event Action<BallActionRequest, bool> ActionResolved;

        [Header("Action Range")]
        [SerializeField] private float _controlRange = 1.3f;
        [SerializeField] private float _kickRange = 1.55f;
        [SerializeField] private float _tackleRange = 1.8f;

        [Header("Physical Assist")]
        [SerializeField] private float _assistRange = 1.15f;
        [SerializeField] private float _contestRadius = 1.05f;
        [SerializeField] private float _dribbleForwardDistance = 0.72f;
        [SerializeField] private float _assistAcceleration = 18f;
        [SerializeField] private float _assistDamping = 4.2f;
        [SerializeField] private float _maxAssistAcceleration = 14f;
        [SerializeField] private float _assistMaximumBallSpeed = 8f;
        [SerializeField] private float _activeControlDuration = 0.35f;
        [SerializeField] private float _postKickAssistDelay = 0.5f;

        [Header("Serve Protection")]
        [SerializeField] private float _serveProtectionDuration = 1.2f;
        [SerializeField] private float _aiReachHeight = 0.85f;

        [Header("Impulse")]
        [SerializeField] private Vector2 _passImpulseRange = new(3.2f, 7f);
        [SerializeField] private Vector2 _shotImpulseRange = new(5.5f, 10.5f);
        [SerializeField] private float _shotLift = 0.08f;
        [SerializeField] private float _tackleImpulse = 2.6f;
        [SerializeField] private float _maximumBallSpeed = 18f;

        [Header("Rolling Drag")]
        [SerializeField] private float _rollingDamping = 3.2f;
        [SerializeField] private float _rollingFreeDelay = 0.18f;
        [SerializeField] private float _rollingStopSpeed = 0.18f;
        [SerializeField] private float _groundedTolerance = 0.06f;

        [Header("Aim Assist")]
        [SerializeField] private float _passAssistDegrees = 18f;
        [SerializeField] private float _shotAssistDegrees = 12f;

        public Rigidbody Body { get; private set; }
        public PossessionOwner Owner { get; private set; } = PossessionOwner.Free;
        public bool IsLive { get; private set; }
        public bool IsServeProtected => IsLive && Time.time < _serveProtectedUntil;
        public string LastAction { get; private set; } = "READY";
        public BallActionSource LastActionSource { get; private set; } = BallActionSource.KeyboardMouse;
        public float Speed => Body != null ? Body.linearVelocity.magnitude : 0f;

        private BallController _ball;
        private Transform _player;
        private Transform _teammate;
        private Transform _opponent;
        private Transform _goalkeeper;
        private Transform _goal;
        private PhysicalBallInteractor _physicalInteractor;
        private float _activeControlUntil;
        private float _assistSuppressedUntil;
        private float _pendingVrPassUntil;
        private float _serveProtectedUntil = -999f;
        private bool _serveProtectionPending;
        private float _lastImpulseAt = -999f;

        public void Configure(
            Transform player,
            Transform teammate,
            Transform opponent,
            Transform goalkeeper,
            Transform goal)
        {
            _ball = GetComponent<BallController>();
            _player = player;
            _teammate = teammate;
            _opponent = opponent;
            _goalkeeper = goalkeeper;
            _goal = goal;
            Body = _ball.EnsurePhysicsComponents();
            Body.mass = Mathf.Max(0.25f, Body.mass);
            Body.linearDamping = 0.08f;
            Body.angularDamping = 0.08f;
            EnsurePhysicalInteractor();
        }

        private void OnEnable()
        {
            TrackedLegController.GlobalFootContact += HandleFootContact;
        }

        private void OnDisable()
        {
            TrackedLegController.GlobalFootContact -= HandleFootContact;
            if (_physicalInteractor != null)
                _physicalInteractor.PhysicalImpulseApplied -= HandlePhysicalImpulse;
        }

        private void FixedUpdate()
        {
            if (Body == null)
                return;

            ClampBallSpeed();
            ApplyRollingDamping();
            UpdatePossessionOwner();
            if (!IsLive || _player == null || Time.time < _assistSuppressedUntil)
                return;

            float playerDistance = FlatDistance(_player.position, transform.position);
            float opponentDistance = _opponent != null
                ? FlatDistance(_opponent.position, transform.position)
                : float.PositiveInfinity;
            if (playerDistance > _assistRange || Body.linearVelocity.magnitude > _assistMaximumBallSpeed)
                return;
            if (ArenaGameplayRules.IsContested(playerDistance, opponentDistance, _contestRadius))
                return;

            Vector3 target = _player.position + _player.forward * _dribbleForwardDistance;
            target.y = Mathf.Max(transform.position.y, _player.position.y + 0.18f);
            Vector3 error = target - transform.position;
            Vector3 acceleration = error * _assistAcceleration - Body.linearVelocity * _assistDamping;
            acceleration = Vector3.ClampMagnitude(acceleration, _maxAssistAcceleration);
            Body.AddForce(acceleration, ForceMode.Acceleration);

            if (Time.time < _activeControlUntil)
            {
                Vector3 velocity = Body.linearVelocity;
                velocity.x *= 0.84f;
                velocity.z *= 0.84f;
                Body.linearVelocity = velocity;
                Body.angularVelocity *= 0.82f;
            }
        }

        public void SetLive(bool live)
        {
            IsLive = live;
            if (!live)
            {
                Owner = PossessionOwner.Free;
                _activeControlUntil = 0f;
                _assistSuppressedUntil = float.PositiveInfinity;
                _serveProtectionPending = false;
                _serveProtectedUntil = -999f;
            }
            else
            {
                _assistSuppressedUntil = Time.time + 0.15f;
                if (_serveProtectionPending)
                {
                    _serveProtectedUntil = Time.time + Mathf.Max(0f, _serveProtectionDuration);
                    _serveProtectionPending = false;
                }
            }
        }

        public void ResetBall(Vector3 worldPosition)
        {
            if (_ball == null)
                _ball = GetComponent<BallController>();
            Body = _ball.EnsurePhysicsComponents();
            _ball.Detach();
            _ball.SetPhysicalSimulation(false, true);
            transform.position = worldPosition;
            transform.rotation = Quaternion.identity;
            Owner = PossessionOwner.Free;
            LastAction = "ROBOT READY";
        }

        public void Serve(Vector3 velocity)
        {
            _ball.Detach();
            _ball.SetPhysicalSimulation(true, true);
            Body = _ball.EnsurePhysicsComponents();
            Body.linearVelocity = velocity;
            LastAction = "ROBOT SERVE";
            _serveProtectionPending = true;
            _assistSuppressedUntil = Time.time + 0.35f;
            StampImpulse();
        }

        public bool Execute(BallActionRequest request)
        {
            if (!IsLive || Body == null || request.Actor == null)
            {
                Resolve(request, false, "ACTION BLOCKED");
                return false;
            }

            if (ArenaGameplayRules.ShouldBlockServeProtectedAction(request.Source, IsServeProtected))
            {
                Resolve(request, false, "SERVE PROTECTED");
                return false;
            }

            float range = request.Kind == BallActionKind.Tackle ? _tackleRange
                : request.Kind == BallActionKind.Control ? _controlRange
                : _kickRange;
            if (FlatDistance(request.Actor.position, transform.position) > range)
            {
                Resolve(request, false, $"{request.Kind.ToString().ToUpperInvariant()} - NO BALL");
                return false;
            }

            if (request.Source == BallActionSource.AI &&
                !ArenaGameplayRules.CanReachBall(request.Actor.position, transform.position, range, _aiReachHeight))
            {
                Resolve(request, false, $"{request.Kind.ToString().ToUpperInvariant()} - TOO HIGH");
                return false;
            }

            switch (request.Kind)
            {
                case BallActionKind.Control:
                    Body.linearVelocity *= 0.32f;
                    Body.angularVelocity *= 0.4f;
                    _activeControlUntil = Time.time + _activeControlDuration;
                    EndServeProtection(request.Source);
                    Resolve(request, true, "CONTROL");
                    return true;

                case BallActionKind.Pass:
                    ApplyKick(request, _passImpulseRange, 0f, _teammate, _passAssistDegrees);
                    Resolve(request, true, "PASS");
                    return true;

                case BallActionKind.Shot:
                    ApplyKick(request, _shotImpulseRange, _shotLift, _goal, _shotAssistDegrees);
                    Resolve(request, true, "SHOT");
                    return true;

                case BallActionKind.Tackle:
                    Vector3 tackleDirection = ArenaGameplayRules.FlattenDirection(request.Direction, request.Actor.forward);
                    Body.AddForce((tackleDirection + Vector3.up * 0.03f).normalized * _tackleImpulse, ForceMode.Impulse);
                    _assistSuppressedUntil = Time.time + _postKickAssistDelay;
                    StampImpulse();
                    EndServeProtection(request.Source);
                    Resolve(request, true, "TACKLE");
                    return true;
            }

            Resolve(request, false, "UNKNOWN ACTION");
            return false;
        }

        private void ApplyKick(
            BallActionRequest request,
            Vector2 impulseRange,
            float lift,
            Transform assistTarget,
            float assistDegrees)
        {
            Vector3 direction = request.Direction;
            if (assistTarget != null)
            {
                Vector3 targetDirection = ArenaGameplayRules.FlattenDirection(
                    assistTarget.position - transform.position,
                    direction);
                if (Vector3.Angle(direction, targetDirection) <= assistDegrees)
                    direction = Vector3.Slerp(direction, targetDirection, 0.72f).normalized;
            }

            direction.y = lift;
            direction.Normalize();
            Body.linearVelocity *= 0.28f;
            Body.AddForce(direction * Mathf.Lerp(impulseRange.x, impulseRange.y, request.Power01), ForceMode.Impulse);
            _activeControlUntil = 0f;
            _assistSuppressedUntil = Time.time + _postKickAssistDelay;
            StampImpulse();
            EndServeProtection(request.Source);
        }

        private void UpdatePossessionOwner()
        {
            if (!IsLive)
            {
                Owner = PossessionOwner.Free;
                return;
            }

            float best = 1.05f;
            PossessionOwner owner = PossessionOwner.Free;
            ConsiderOwner(_player, PossessionOwner.Player, ref best, ref owner);
            if (!IsServeProtected)
            {
                ConsiderOwner(_teammate, PossessionOwner.Teammate, ref best, ref owner);
                ConsiderOwner(_opponent, PossessionOwner.Opponent, ref best, ref owner);
                ConsiderOwner(_goalkeeper, PossessionOwner.Goalkeeper, ref best, ref owner);
            }
            Owner = Body != null && Body.linearVelocity.magnitude > 9f ? PossessionOwner.Free : owner;
        }

        private void ConsiderOwner(Transform actor, PossessionOwner owner, ref float bestDistance, ref PossessionOwner bestOwner)
        {
            if (actor == null)
                return;
            float distance = FlatDistance(actor.position, transform.position);
            if (distance >= bestDistance)
                return;
            bestDistance = distance;
            bestOwner = owner;
        }

        private void ClampBallSpeed()
        {
            float maxSpeed = Mathf.Max(1f, _maximumBallSpeed);
            if (Body.linearVelocity.sqrMagnitude > maxSpeed * maxSpeed)
                Body.linearVelocity = Body.linearVelocity.normalized * maxSpeed;
        }

        private void ApplyRollingDamping()
        {
            if (Body == null || Body.isKinematic)
                return;

            Body.linearVelocity = ArenaGameplayRules.ApplyRollingDamping(
                Body.linearVelocity,
                IsRollingOnGround(),
                Time.time - _lastImpulseAt,
                _rollingFreeDelay,
                _rollingDamping,
                _rollingStopSpeed,
                Time.fixedDeltaTime);

            Vector3 horizontal = new Vector3(Body.linearVelocity.x, 0f, Body.linearVelocity.z);
            if (horizontal.sqrMagnitude <= _rollingStopSpeed * _rollingStopSpeed)
                Body.angularVelocity *= 0.5f;
        }

        private bool IsRollingOnGround()
        {
            if (Body == null)
                return false;
            if (Body.linearVelocity.y > 0.25f)
                return false;

            float radius = EstimateWorldRadius();
            return transform.position.y <= radius + Mathf.Max(0f, _groundedTolerance);
        }

        private float EstimateWorldRadius()
        {
            SphereCollider sphere = GetComponent<SphereCollider>();
            if (sphere == null)
                return 0.12f;

            Vector3 scale = transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            return Mathf.Max(0.03f, sphere.radius * maxScale);
        }

        private void StampImpulse()
        {
            _lastImpulseAt = Time.time;
        }

        private void EndServeProtection(BallActionSource source)
        {
            if (source == BallActionSource.AI)
                return;

            _serveProtectedUntil = -999f;
            _serveProtectionPending = false;
        }

        private void EnsurePhysicalInteractor()
        {
            _physicalInteractor = GetComponent<PhysicalBallInteractor>();
            if (_physicalInteractor == null)
                _physicalInteractor = gameObject.AddComponent<PhysicalBallInteractor>();
            _physicalInteractor.ConfigureRouting(false, true, false);
            _physicalInteractor.PhysicalImpulseApplied -= HandlePhysicalImpulse;
            _physicalInteractor.PhysicalImpulseApplied += HandlePhysicalImpulse;
        }

        private void HandleFootContact(FootContactData data)
        {
            if (!IsLive || Body == null || data.BallBody != Body)
                return;
            EndServeProtection(BallActionSource.VrPhysical);
            if (data.ShootIntentHeld)
                _pendingVrPassUntil = Time.time + 0.5f;
            if (data.ContactSpeed < 0.3f)
            {
                _activeControlUntil = Time.time + _activeControlDuration;
                LastAction = "VR CONTROL";
                LastActionSource = BallActionSource.VrPhysical;
            }
        }

        private void HandlePhysicalImpulse(FootContactData data, Vector3 direction, float impulse)
        {
            BallActionKind kind = Time.time <= _pendingVrPassUntil
                ? BallActionKind.Pass
                : BallActionKind.Shot;
            BallActionSource source = GameplayModeBootstrap.CurrentProfile == ControlProfile.XrSimulator
                ? BallActionSource.XrSimulator
                : BallActionSource.VrPhysical;
            var request = new BallActionRequest(kind, source, data.Source != null ? data.Source.transform : _player, direction, data.Power01);
            _assistSuppressedUntil = Time.time + _postKickAssistDelay;
            StampImpulse();
            EndServeProtection(source);
            Resolve(request, true, kind == BallActionKind.Pass ? "VR PASS" : "VR SHOT");
        }

        private void Resolve(BallActionRequest request, bool success, string label)
        {
            LastAction = label;
            LastActionSource = request.Source;
            ActionResolved?.Invoke(request, success);
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
