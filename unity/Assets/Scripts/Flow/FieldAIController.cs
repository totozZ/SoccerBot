using UnityEngine;

namespace SoccerBot
{
    [DefaultExecutionOrder(40)]
    [DisallowMultipleComponent]
    public class FieldAIController : MonoBehaviour
    {
        public enum OpponentState { HoldingShape, ClosingPassLane, MarkingReceiver, PressingBall, Intercepting }
        public enum TeammateState { HoldingSupport, OfferingAngle, ReceivingPass, PreparingShot, WatchingShot }
        public enum GoalkeeperState { Set, Shuffling, TrackingBall, Saving }

        [Header("References")]
        [SerializeField] private MatchFlowController _flow;
        [SerializeField] private BallController _ball;
        [SerializeField] private Transform _playerTransform;
        [SerializeField] private Transform _teammateTransform;
        [SerializeField] private Transform _opponentTransform;
        [SerializeField] private Transform _goalkeeperTransform;

        [Header("Opponent AI")]
        [SerializeField] private bool _enableOpponentAI = true;
        [SerializeField, Range(0.2f, 5f)] private float _opponentPressSpeed = 1.15f;
        [SerializeField, Range(0.2f, 5f)] private float _opponentPassLaneSpeed = 1.55f;
        [SerializeField, Range(1f, 14f)] private float _opponentTurnSpeed = 7f;
        [SerializeField, Range(0f, 1f)] private float _incomingPassInterceptChance = 0.18f;
        [SerializeField, Range(0f, 1f)] private float _loosePassInterceptChancePerSecond = 0.12f;
        [SerializeField, Range(0.2f, 2.5f)] private float _opponentInterceptRadius = 0.85f;
        [SerializeField, Range(0.2f, 2.5f)] private float _opponentMinPlayerDistance = 1.05f;
        [SerializeField, Range(0f, 1f)] private float _passLaneReactStart01 = 0.28f;
        [SerializeField, Range(0f, 1f)] private float _passLaneReactEnd01 = 0.9f;
        [SerializeField, Range(0.2f, 4f)] private float _interceptionCooldown = 1.25f;
        [SerializeField, Range(0.5f, 5f)] private float _passPressureLaneRadius = 2.4f;
        [SerializeField, Range(0.5f, 5f)] private float _passPressureReceiverRadius = 2.1f;

        [Header("Teammate AI")]
        [SerializeField] private bool _enableTeammateAI = true;
        [SerializeField, Range(0.2f, 5f)] private float _teammateSupportSpeed = 1.35f;
        [SerializeField, Range(0.2f, 5f)] private float _teammateReceiveSpeed = 2.0f;
        [SerializeField, Range(1f, 14f)] private float _teammateTurnSpeed = 7f;
        [SerializeField] private float _teammateSupportSideOffset = -2.2f;
        [SerializeField] private float _teammateSupportForwardOffset = 3.6f;
        [SerializeField, Range(0.1f, 2f)] private float _teammateBallCushion = 0.55f;
        [SerializeField, Range(0.2f, 3f)] private float _teammateSupportReadyRadius = 0.75f;

        [Header("Goalkeeper AI")]
        [SerializeField] private bool _enableGoalkeeperAI = true;
        [SerializeField, Range(0.2f, 6f)] private float _goalkeeperMoveSpeed = 2.2f;
        [SerializeField, Range(1f, 14f)] private float _goalkeeperTurnSpeed = 8f;
        [SerializeField, Range(0.4f, 3f)] private float _goalkeeperLaneHalfWidth = 1.35f;
        [SerializeField, Range(0.2f, 3f)] private float _goalkeeperSaveRadius = 1.15f;
        [SerializeField, Range(0.1f, 8f)] private float _goalkeeperSaveMinBallSpeed = 1.25f;
        [SerializeField, Range(0f, 1f)] private float _goalkeeperSaveChance = 0.28f;
        [SerializeField, Range(0.2f, 2f)] private float _goalkeeperCoverageSaveMultiplier = 1.25f;
        [SerializeField, Range(-1f, 1f)] private float _shotTowardGoalMinDot = 0.25f;

        [Header("State Machine")]
        [SerializeField, Range(0f, 1f)] private float _receiverMarkPressureThreshold = 0.42f;
        [SerializeField, Range(0.1f, 8f)] private float _teammatePassReadMinSpeed = 0.7f;
        [SerializeField, Range(-1f, 1f)] private float _teammatePassReadMinDot = 0.45f;

        [Header("Runtime Readouts")]
        [SerializeField, Range(0f, 1f)] private float _passPressure01;
        [SerializeField, Range(0f, 1f)] private float _teammateSupport01;
        [SerializeField, Range(0f, 1f)] private float _goalkeeperCoverage01;
        [SerializeField] private bool _debugStateChanges = false;

        public OpponentState CurrentOpponentState { get; private set; } = OpponentState.HoldingShape;
        public TeammateState CurrentTeammateState { get; private set; } = TeammateState.HoldingSupport;
        public GoalkeeperState CurrentGoalkeeperState { get; private set; } = GoalkeeperState.Set;
        public float PassPressure01 => _passPressure01;
        public float TeammateSupport01 => _teammateSupport01;
        public float GoalkeeperCoverage01 => _goalkeeperCoverage01;
        public float OpponentStateAge => Mathf.Max(0f, Time.time - _lastOpponentStateChangedAt);
        public float TeammateStateAge => Mathf.Max(0f, Time.time - _lastTeammateStateChangedAt);
        public float GoalkeeperStateAge => Mathf.Max(0f, Time.time - _lastGoalkeeperStateChangedAt);

        private MatchFlowController _subscribedFlow;
        private Vector3 _teammateHome;
        private Vector3 _opponentHome;
        private Vector3 _goalkeeperHome;
        private bool _homesCaptured;
        private Vector3 _currentPassStart;
        private Vector3 _currentPassEnd;
        private float _currentPassFlightTime = 1f;
        private float _passStartedAt = -999f;
        private bool _incomingPassInterceptionRolled;
        private bool _goalkeeperSaveRolled;
        private bool _roundInterceptionUsed;
        private float _lastInterceptionTime = -999f;
        private Vector3 _lastBallPosition;
        private Vector3 _estimatedBallVelocity;
        private bool _hasLastBallPosition;
        private float _lastOpponentStateChangedAt;
        private float _lastTeammateStateChangedAt;
        private float _lastGoalkeeperStateChangedAt;

        public void Configure(
            MatchFlowController flow,
            BallController ball,
            Transform player,
            Transform teammate,
            Transform opponent,
            Transform goalkeeper)
        {
            if (_subscribedFlow != null && _subscribedFlow != flow)
                Unsubscribe(_subscribedFlow);

            _flow = flow;
            _ball = ball;
            _playerTransform = player;
            _teammateTransform = teammate;
            _opponentTransform = opponent;
            _goalkeeperTransform = goalkeeper;

            if (_flow != null && _subscribedFlow != _flow)
            {
                Subscribe(_flow);
                _subscribedFlow = _flow;
            }

            CaptureHomes(true);
            ResetRoundState();
        }

        private void OnDisable()
        {
            if (_subscribedFlow != null)
            {
                Unsubscribe(_subscribedFlow);
                _subscribedFlow = null;
            }
        }

        private void Subscribe(MatchFlowController flow)
        {
            flow.PhaseChanged += HandlePhaseChanged;
            flow.PassStarted += HandlePassStarted;
            flow.ReceiveResolved += HandleReceiveResolved;
            flow.RecoveryTriggered += HandleRecoveryTriggered;
            flow.RecoveryResolved += HandleRecoveryResolved;
            flow.ShotAttempted += HandleShotAttempted;
            flow.FootContactRecorded += HandleFootContactRecorded;
            flow.RoundResolved += HandleRoundResolved;
        }

        private void Unsubscribe(MatchFlowController flow)
        {
            flow.PhaseChanged -= HandlePhaseChanged;
            flow.PassStarted -= HandlePassStarted;
            flow.ReceiveResolved -= HandleReceiveResolved;
            flow.RecoveryTriggered -= HandleRecoveryTriggered;
            flow.RecoveryResolved -= HandleRecoveryResolved;
            flow.ShotAttempted -= HandleShotAttempted;
            flow.FootContactRecorded -= HandleFootContactRecorded;
            flow.RoundResolved -= HandleRoundResolved;
        }

        private void Update()
        {
            UpdateBallVelocity();
            if (_flow == null)
                return;

            switch (_flow.CurrentPhase)
            {
                case MatchFlowController.Phase.Setup:
                    TickSetup();
                    break;
                case MatchFlowController.Phase.Pass:
                    TickIncomingPass();
                    break;
                case MatchFlowController.Phase.Recovery:
                    TickRecovery();
                    break;
                case MatchFlowController.Phase.Possession:
                    TickPossession();
                    break;
                case MatchFlowController.Phase.Shot:
                    TickShot();
                    break;
                default:
                    SetOpponentState(OpponentState.HoldingShape);
                    SetTeammateState(TeammateState.HoldingSupport);
                    SetGoalkeeperState(GoalkeeperState.Set);
                    break;
            }
        }

        private void HandlePhaseChanged(MatchFlowController.Phase phase)
        {
            if (phase == MatchFlowController.Phase.Setup)
            {
                _homesCaptured = false;
                ResetRoundState();
            }

            if (phase == MatchFlowController.Phase.Shot)
                _goalkeeperSaveRolled = false;
        }

        private void HandlePassStarted(Vector3 start, Vector3 end, float flightTime)
        {
            _currentPassStart = start;
            _currentPassEnd = end;
            _currentPassFlightTime = Mathf.Max(0.05f, flightTime);
            _passStartedAt = Time.time;
            _incomingPassInterceptionRolled = false;
            SetOpponentState(OpponentState.ClosingPassLane);
        }

        private void HandleReceiveResolved(float quality01, float timingError01, bool byFootContact)
        {
            if (quality01 < 0.35f)
                SetOpponentState(OpponentState.PressingBall);
        }

        private void HandleRecoveryTriggered()
        {
            SetOpponentState(OpponentState.PressingBall);
            SetTeammateState(TeammateState.HoldingSupport);
        }

        private void HandleRecoveryResolved(bool success)
        {
            if (success)
            {
                SetOpponentState(OpponentState.HoldingShape);
                SetTeammateState(TeammateState.OfferingAngle);
            }
            else
            {
                SetOpponentState(OpponentState.Intercepting);
                SetTeammateState(TeammateState.WatchingShot);
            }
        }

        private void HandleShotAttempted(float power01, Vector3 direction)
        {
            SetTeammateState(TeammateState.PreparingShot);
            SetGoalkeeperState(GoalkeeperState.TrackingBall);
            _goalkeeperSaveRolled = false;
        }

        private void HandleFootContactRecorded(FootContactData data)
        {
            if (_flow == null || _flow.CurrentPhase != MatchFlowController.Phase.Possession)
                return;

            if (IsShotThreat(data.FootVelocity, GetAttackForward(), _goalkeeperSaveMinBallSpeed, _shotTowardGoalMinDot) ||
                IsShotThreat(_estimatedBallVelocity, GetAttackForward(), _goalkeeperSaveMinBallSpeed, _shotTowardGoalMinDot))
            {
                SetGoalkeeperState(GoalkeeperState.TrackingBall);
            }
        }

        private void HandleRoundResolved(Scenario scenario, string outcomeLabelOverride)
        {
            ResetRoundState();
        }

        private void ResetRoundState()
        {
            _incomingPassInterceptionRolled = false;
            _goalkeeperSaveRolled = false;
            _roundInterceptionUsed = false;
            _currentPassStart = Vector3.zero;
            _currentPassEnd = Vector3.zero;
            _passStartedAt = -999f;
            _hasLastBallPosition = false;
            _estimatedBallVelocity = Vector3.zero;
            _passPressure01 = 0f;
            _teammateSupport01 = 0f;
            _goalkeeperCoverage01 = 0f;
        }

        private void CaptureHomes(bool force)
        {
            if (_homesCaptured && !force)
                return;

            if (_teammateTransform != null)
                _teammateHome = _teammateTransform.position;
            if (_opponentTransform != null)
                _opponentHome = _opponentTransform.position;
            if (_goalkeeperTransform != null)
                _goalkeeperHome = _goalkeeperTransform.position;

            _homesCaptured = _teammateTransform != null || _opponentTransform != null || _goalkeeperTransform != null;
        }

        private void TickSetup()
        {
            CaptureHomes(false);
            SetOpponentState(OpponentState.HoldingShape);
            SetTeammateState(TeammateState.HoldingSupport);
            SetGoalkeeperState(GoalkeeperState.Set);
            FaceTowards(_opponentTransform, _playerTransform != null ? _playerTransform.position : GetBallPosition(), _opponentTurnSpeed);
            FaceTowards(_teammateTransform, _playerTransform != null ? _playerTransform.position : GetBallPosition(), _teammateTurnSpeed);
            TickGoalkeeperTrack(false);
        }

        private void TickIncomingPass()
        {
            CaptureHomes(false);
            Vector3 ballPos = GetBallPosition();

            if (_enableOpponentAI && _opponentTransform != null)
            {
                float pass01 = GetPassProgress01();
                Vector3 lanePoint = ClosestPointOnSegment(_currentPassStart, _currentPassEnd, _opponentTransform.position);
                Vector3 target = Vector3.Lerp(lanePoint, ballPos, 0.45f);
                target.y = _opponentHome.y;
                UpdateIncomingPassPressure(ballPos);
                SetOpponentState(OpponentState.ClosingPassLane);
                MoveFlat(_opponentTransform, target, _opponentPassLaneSpeed, _opponentTurnSpeed, ballPos);
                TryIncomingPassInterception(pass01, ballPos);
            }
            else
            {
                _passPressure01 = 0f;
            }

            if (_enableTeammateAI)
                MoveTeammateToSupport(false);
            else
                _teammateSupport01 = 0f;

            TickGoalkeeperTrack(false);
        }

        private void TickRecovery()
        {
            SetOpponentState(OpponentState.PressingBall);
            FaceTowards(_opponentTransform, GetBallPosition(), _opponentTurnSpeed);
            FaceTowards(_teammateTransform, GetBallPosition(), _teammateTurnSpeed);
            TickGoalkeeperTrack(false);
        }

        private void TickPossession()
        {
            CaptureHomes(false);
            Vector3 ballPos = GetBallPosition();
            bool ballOwnedByPlayer = IsBallOwnedByPlayer();
            Vector3 ballVelocity = Flatten(_estimatedBallVelocity);
            bool teammatePassThreat = IsMovingTowardTarget(
                ballVelocity,
                ballPos,
                _teammateTransform != null ? _teammateTransform.position : ballPos,
                _teammatePassReadMinSpeed,
                _teammatePassReadMinDot);
            bool shotThreat = IsShotThreat(
                ballVelocity,
                GetAttackForward(),
                _goalkeeperSaveMinBallSpeed,
                _shotTowardGoalMinDot);

            if (_enableOpponentAI && _opponentTransform != null)
            {
                UpdatePassPressure(ballPos);
                bool markReceiver = !ballOwnedByPlayer && (teammatePassThreat || _passPressure01 > _receiverMarkPressureThreshold);
                SetOpponentState(markReceiver ? OpponentState.MarkingReceiver : OpponentState.PressingBall);
                Vector3 target = markReceiver ? BuildOpponentPassLaneTarget(ballPos) : ClampAwayFromPlayer(ballPos);
                target.y = _opponentHome.y;
                MoveFlat(_opponentTransform, target, _opponentPressSpeed, _opponentTurnSpeed, ballPos);
                TryLooseBallInterception(ballPos);
            }
            else
            {
                _passPressure01 = 0f;
            }

            if (_enableTeammateAI)
            {
                if (ballOwnedByPlayer)
                    MoveTeammateToSupport(true);
                else
                    MoveTeammateToReceive(ballPos);
            }
            else
            {
                _teammateSupport01 = 0f;
            }

            TickGoalkeeperTrack(shotThreat);
        }

        private void TickShot()
        {
            SetOpponentState(OpponentState.HoldingShape);
            SetTeammateState(TeammateState.WatchingShot);
            FaceTowards(_opponentTransform, GetBallPosition(), _opponentTurnSpeed);
            FaceTowards(_teammateTransform, GetBallPosition(), _teammateTurnSpeed);
            TickGoalkeeperTrack(true);
        }

        private void MoveTeammateToSupport(bool activeSupport)
        {
            if (_teammateTransform == null)
            {
                _teammateSupport01 = 0f;
                return;
            }

            SetTeammateState(activeSupport ? TeammateState.OfferingAngle : TeammateState.HoldingSupport);
            Vector3 origin = _playerTransform != null ? _playerTransform.position : _teammateHome;
            Vector3 target = GetTeammateSupportTarget(origin);
            UpdateTeammateSupport(target);
            MoveFlat(_teammateTransform, target, _teammateSupportSpeed, _teammateTurnSpeed, origin);
        }

        private void MoveTeammateToReceive(Vector3 ballPos)
        {
            if (_teammateTransform == null)
            {
                _teammateSupport01 = 0f;
                return;
            }

            SetTeammateState(TeammateState.ReceivingPass);
            Vector3 flatVelocity = Flatten(_estimatedBallVelocity);
            Vector3 target = ballPos;
            if (flatVelocity.sqrMagnitude > 0.01f)
                target += flatVelocity.normalized * _teammateBallCushion;
            target.y = _teammateHome.y;
            UpdateTeammateSupport(target);
            MoveFlat(_teammateTransform, target, _teammateReceiveSpeed, _teammateTurnSpeed, ballPos);
        }

        private void TickGoalkeeperTrack(bool allowSave)
        {
            if (!_enableGoalkeeperAI || _goalkeeperTransform == null)
            {
                _goalkeeperCoverage01 = 0f;
                if (!_enableGoalkeeperAI)
                    SetGoalkeeperState(GoalkeeperState.Set);
                return;
            }

            Vector3 ballPos = GetBallPosition();
            Vector3 right = GetAttackRight();
            Vector3 target = ClampGoalkeeperLane(_goalkeeperHome, right, _goalkeeperLaneHalfWidth, ballPos);
            target.y = _goalkeeperHome.y;
            UpdateGoalkeeperCoverage(target);
            SetGoalkeeperState(allowSave
                ? GoalkeeperState.TrackingBall
                : (_goalkeeperCoverage01 < 0.85f ? GoalkeeperState.Shuffling : GoalkeeperState.Set));
            MoveFlat(_goalkeeperTransform, target, _goalkeeperMoveSpeed, _goalkeeperTurnSpeed, ballPos);

            if (allowSave)
                TryGoalkeeperSave(ballPos);
        }

        private void TryIncomingPassInterception(float pass01, Vector3 ballPos)
        {
            if (_flow == null || _opponentTransform == null || _incomingPassInterceptionRolled || _roundInterceptionUsed)
                return;
            if (Time.time - _lastInterceptionTime < _interceptionCooldown)
                return;
            if (pass01 < _passLaneReactStart01 || pass01 > _passLaneReactEnd01)
                return;

            float distance = FlatDistance(_opponentTransform.position, ballPos);
            if (distance > _opponentInterceptRadius)
                return;

            _incomingPassInterceptionRolled = true;
            float effectiveChance = Mathf.Clamp01(_incomingPassInterceptChance * Mathf.Lerp(0.65f, 1.75f, _passPressure01));
            if (Random.value > effectiveChance)
                return;

            _roundInterceptionUsed = true;
            _lastInterceptionTime = Time.time;
            SetOpponentState(OpponentState.Intercepting);
            _flow.TryResolveAiInterception("[FieldAI] Opponent cut off the incoming pass.");
        }

        private void TryLooseBallInterception(Vector3 ballPos)
        {
            if (_flow == null || _opponentTransform == null || _roundInterceptionUsed)
                return;
            if (IsBallOwnedByPlayer())
                return;
            if (Time.time - _lastInterceptionTime < _interceptionCooldown)
                return;

            float distance = FlatDistance(_opponentTransform.position, ballPos);
            if (distance > _opponentInterceptRadius)
                return;

            float chance = Mathf.Clamp01(_loosePassInterceptChancePerSecond * Mathf.Lerp(0.5f, 2.25f, _passPressure01) * Time.deltaTime);
            if (Random.value > chance)
                return;

            _roundInterceptionUsed = true;
            _lastInterceptionTime = Time.time;
            SetOpponentState(OpponentState.Intercepting);
            _flow.TryResolveAiInterception("[FieldAI] Opponent stepped into the pass lane.");
        }

        private void TryGoalkeeperSave(Vector3 ballPos)
        {
            if (_flow == null || _goalkeeperTransform == null || _goalkeeperSaveRolled)
                return;

            if (!IsShotThreat(_estimatedBallVelocity, GetAttackForward(), _goalkeeperSaveMinBallSpeed, _shotTowardGoalMinDot))
                return;
            if (FlatDistance(_goalkeeperTransform.position, ballPos) > _goalkeeperSaveRadius)
                return;

            _goalkeeperSaveRolled = true;
            float effectiveChance = Mathf.Clamp01(_goalkeeperSaveChance * Mathf.Lerp(0.45f, _goalkeeperCoverageSaveMultiplier, _goalkeeperCoverage01));
            if (Random.value > effectiveChance)
                return;

            SetGoalkeeperState(GoalkeeperState.Saving);
            _flow.TryResolveGoalkeeperSave("[FieldAI] Goalkeeper saved the shot.");
        }

        private void UpdateBallVelocity()
        {
            if (_ball == null)
                return;

            Vector3 ballPos = _ball.transform.position;
            if (_hasLastBallPosition && Time.deltaTime > 0.0001f)
                _estimatedBallVelocity = (ballPos - _lastBallPosition) / Time.deltaTime;
            _lastBallPosition = ballPos;
            _hasLastBallPosition = true;
        }

        private Vector3 GetBallPosition()
        {
            return _ball != null ? _ball.transform.position : Vector3.zero;
        }

        private bool IsBallOwnedByPlayer()
        {
            return _ball != null &&
                   _playerTransform != null &&
                   (_ball.transform == _playerTransform || _ball.transform.IsChildOf(_playerTransform));
        }

        private float GetPassProgress01()
        {
            if (_passStartedAt < 0f)
                return 0f;
            return Mathf.Clamp01((Time.time - _passStartedAt) / Mathf.Max(0.05f, _currentPassFlightTime));
        }

        private Vector3 ClampAwayFromPlayer(Vector3 target)
        {
            if (_playerTransform == null)
                return target;

            Vector3 fromPlayer = Flatten(target - _playerTransform.position);
            if (fromPlayer.sqrMagnitude < 0.0001f)
                fromPlayer = GetAttackRight();

            float distance = fromPlayer.magnitude;
            if (distance >= _opponentMinPlayerDistance)
                return target;

            return _playerTransform.position + fromPlayer.normalized * _opponentMinPlayerDistance;
        }

        private void UpdateIncomingPassPressure(Vector3 ballPos)
        {
            if (_opponentTransform == null)
            {
                _passPressure01 = 0f;
                return;
            }

            Vector3 lanePoint = ClosestPointOnSegment(_currentPassStart, _currentPassEnd, _opponentTransform.position);
            float laneDistance = FlatDistance(_opponentTransform.position, lanePoint);
            float ballDistance = FlatDistance(_opponentTransform.position, ballPos);
            float laneRadius = Mathf.Max(_opponentInterceptRadius + 0.01f, _passPressureLaneRadius);
            float lanePressure = 1f - Mathf.InverseLerp(_opponentInterceptRadius, laneRadius, laneDistance);
            float ballPressure = 1f - Mathf.InverseLerp(_opponentInterceptRadius, laneRadius, ballDistance);
            _passPressure01 = Mathf.Clamp01(Mathf.Max(lanePressure, ballPressure));
        }

        private void UpdatePassPressure(Vector3 ballPos)
        {
            if (_opponentTransform == null || _teammateTransform == null)
            {
                _passPressure01 = 0f;
                return;
            }

            Vector3 lanePoint = ClosestPointOnSegment(ballPos, _teammateTransform.position, _opponentTransform.position);
            float laneDistance = FlatDistance(_opponentTransform.position, lanePoint);
            float receiverDistance = FlatDistance(_opponentTransform.position, _teammateTransform.position);
            float laneRadius = Mathf.Max(_opponentInterceptRadius + 0.01f, _passPressureLaneRadius);
            float receiverRadius = Mathf.Max(_opponentInterceptRadius + 0.01f, _passPressureReceiverRadius);
            float ballSpeed01 = Mathf.InverseLerp(0.2f, 3f, Flatten(_estimatedBallVelocity).magnitude);
            float ownershipScale = IsBallOwnedByPlayer() ? 0.55f : Mathf.Lerp(0.75f, 1.15f, ballSpeed01);
            _passPressure01 = EvaluatePassPressure(
                laneDistance,
                receiverDistance,
                _opponentInterceptRadius,
                laneRadius,
                receiverRadius,
                ownershipScale);
        }

        private Vector3 BuildOpponentPassLaneTarget(Vector3 ballPos)
        {
            if (_teammateTransform == null || _opponentTransform == null)
                return ClampAwayFromPlayer(ballPos);

            Vector3 lanePoint = ClosestPointOnSegment(ballPos, _teammateTransform.position, _opponentTransform.position);
            Vector3 target = Vector3.Lerp(ballPos, lanePoint, Mathf.Clamp01(_passPressure01));
            target.y = _opponentHome.y;
            return ClampAwayFromPlayer(target);
        }

        private Vector3 GetTeammateSupportTarget(Vector3 origin)
        {
            Vector3 forward = GetAttackForward();
            return BuildTeammateSupportTarget(
                origin,
                forward,
                _teammateSupportSideOffset,
                _teammateSupportForwardOffset,
                _teammateHome.y);
        }

        private void UpdateTeammateSupport(Vector3 target)
        {
            if (_teammateTransform == null)
            {
                _teammateSupport01 = 0f;
                return;
            }

            float distance = FlatDistance(_teammateTransform.position, target);
            _teammateSupport01 = Mathf.Clamp01(1f - Mathf.InverseLerp(
                _teammateSupportReadyRadius,
                _teammateSupportReadyRadius * 3.5f,
                distance));
        }

        private void UpdateGoalkeeperCoverage(Vector3 target)
        {
            if (_goalkeeperTransform == null)
            {
                _goalkeeperCoverage01 = 0f;
                return;
            }

            float distance = FlatDistance(_goalkeeperTransform.position, target);
            _goalkeeperCoverage01 = Mathf.Clamp01(1f - Mathf.InverseLerp(
                0.05f,
                Mathf.Max(0.2f, _goalkeeperLaneHalfWidth),
                distance));
        }

        private void SetOpponentState(OpponentState state)
        {
            if (CurrentOpponentState == state)
                return;

            CurrentOpponentState = state;
            _lastOpponentStateChangedAt = Time.time;
            if (_debugStateChanges)
                Debug.Log($"[FieldAI] Opponent -> {state}");
        }

        private void SetTeammateState(TeammateState state)
        {
            if (CurrentTeammateState == state)
                return;

            CurrentTeammateState = state;
            _lastTeammateStateChangedAt = Time.time;
            if (_debugStateChanges)
                Debug.Log($"[FieldAI] Teammate -> {state}");
        }

        private void SetGoalkeeperState(GoalkeeperState state)
        {
            if (CurrentGoalkeeperState == state)
                return;

            CurrentGoalkeeperState = state;
            _lastGoalkeeperStateChangedAt = Time.time;
            if (_debugStateChanges)
                Debug.Log($"[FieldAI] Goalkeeper -> {state}");
        }

        private Vector3 GetAttackForward()
        {
            if (_playerTransform != null && _goalkeeperTransform != null)
            {
                Vector3 forward = Flatten(_goalkeeperTransform.position - _playerTransform.position);
                if (forward.sqrMagnitude > 0.0001f)
                    return forward.normalized;
            }

            if (_playerTransform != null)
            {
                Vector3 forward = Flatten(_playerTransform.forward);
                if (forward.sqrMagnitude > 0.0001f)
                    return forward.normalized;
            }

            return Vector3.forward;
        }

        private Vector3 GetAttackRight()
        {
            Vector3 forward = GetAttackForward();
            return new Vector3(forward.z, 0f, -forward.x).normalized;
        }

        private void MoveFlat(Transform actor, Vector3 target, float speed, float turnSpeed, Vector3 lookTarget)
        {
            if (actor == null)
                return;

            target.y = actor.position.y;
            actor.position = Vector3.MoveTowards(actor.position, target, Mathf.Max(0f, speed) * Time.deltaTime);
            FaceTowards(actor, lookTarget, turnSpeed);
        }

        private void FaceTowards(Transform actor, Vector3 target, float turnSpeed)
        {
            if (actor == null)
                return;

            Vector3 toTarget = Flatten(target - actor.position);
            if (toTarget.sqrMagnitude < 0.0001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(toTarget);
            actor.rotation = Quaternion.Slerp(actor.rotation, targetRotation, Mathf.Max(0f, turnSpeed) * Time.deltaTime);
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            return Flatten(a - b).magnitude;
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }

        public static Vector3 ClosestPointOnSegment(Vector3 start, Vector3 end, Vector3 point)
        {
            Vector3 segment = end - start;
            float sqrLength = segment.sqrMagnitude;
            if (sqrLength <= 0.000001f)
                return start;

            float t = Vector3.Dot(point - start, segment) / sqrLength;
            return start + segment * Mathf.Clamp01(t);
        }

        public static Vector3 ClampGoalkeeperLane(Vector3 home, Vector3 lateralAxis, float halfWidth, Vector3 ballPosition)
        {
            Vector3 axis = Flatten(lateralAxis);
            if (axis.sqrMagnitude < 0.0001f)
                axis = Vector3.right;
            axis.Normalize();

            float offset = Vector3.Dot(Flatten(ballPosition - home), axis);
            return home + axis * Mathf.Clamp(offset, -Mathf.Abs(halfWidth), Mathf.Abs(halfWidth));
        }

        public static bool IsMovingTowardGoal(Vector3 velocity, Vector3 attackForward, float minDot)
        {
            Vector3 flatVelocity = Flatten(velocity);
            Vector3 flatForward = Flatten(attackForward);
            if (flatVelocity.sqrMagnitude < 0.0001f || flatForward.sqrMagnitude < 0.0001f)
                return false;

            return Vector3.Dot(flatVelocity.normalized, flatForward.normalized) >= minDot;
        }

        public static bool IsShotThreat(Vector3 velocity, Vector3 attackForward, float minSpeed, float minDot)
        {
            Vector3 flatVelocity = Flatten(velocity);
            if (flatVelocity.magnitude < Mathf.Max(0f, minSpeed))
                return false;

            return IsMovingTowardGoal(flatVelocity, attackForward, minDot);
        }

        public static bool IsMovingTowardTarget(Vector3 velocity, Vector3 origin, Vector3 target, float minSpeed, float minDot)
        {
            Vector3 flatVelocity = Flatten(velocity);
            Vector3 toTarget = Flatten(target - origin);
            if (flatVelocity.magnitude < Mathf.Max(0f, minSpeed) || toTarget.sqrMagnitude < 0.0001f)
                return false;

            return Vector3.Dot(flatVelocity.normalized, toTarget.normalized) >= minDot;
        }

        public static float EvaluatePassPressure(
            float laneDistance,
            float receiverDistance,
            float interceptRadius,
            float laneRadius,
            float receiverRadius,
            float ownershipScale)
        {
            float minRadius = Mathf.Max(0f, interceptRadius);
            float safeLaneRadius = Mathf.Max(minRadius + 0.01f, laneRadius);
            float safeReceiverRadius = Mathf.Max(minRadius + 0.01f, receiverRadius);
            float lanePressure = 1f - Mathf.InverseLerp(minRadius, safeLaneRadius, Mathf.Max(0f, laneDistance));
            float receiverPressure = 1f - Mathf.InverseLerp(minRadius, safeReceiverRadius, Mathf.Max(0f, receiverDistance));
            return Mathf.Clamp01(Mathf.Max(lanePressure, receiverPressure) * Mathf.Max(0f, ownershipScale));
        }

        public static Vector3 BuildTeammateSupportTarget(
            Vector3 origin,
            Vector3 attackForward,
            float sideOffset,
            float forwardOffset,
            float targetY)
        {
            Vector3 forward = Flatten(attackForward);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            Vector3 right = new Vector3(forward.z, 0f, -forward.x).normalized;
            Vector3 target = origin + right * sideOffset + forward * forwardOffset;
            target.y = targetY;
            return target;
        }
    }
}
