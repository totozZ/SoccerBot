using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoccerBot
{
    [DisallowMultipleComponent]
    public class GameEventRecorder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MatchFlowController _matchFlow;
        [SerializeField] private BallController _ball;
        [SerializeField] private PhysicalBallInteractor _ballInteractor;
        [SerializeField] private ScorePanel _scorePanel;
        [SerializeField] private AICoachClient _coachClient;

        [Header("Behavior")]
        [SerializeField] private bool _sendToAICoach = true;
        [SerializeField] private bool _logSummaryJson = false;

        private readonly List<string> _phaseTransitions = new();
        private DateTime _roundStartedUtc;
        private float _roundStartedTime;
        private string _roundId;
        private bool _roundActive;

        private Vector3 _passDirection;
        private float _passDistance;
        private float _estimatedPassBallSpeed;
        private float _receiveTimingError = -1f;
        private float _receiveQuality = -1f;
        private bool _receiveByFootContact;
        private bool _recoveryTriggered;
        private bool _recoverySucceeded;
        private float _shotPower = -1f;
        private Vector3 _shotDirection;
        private float _footVelocityAtTouch;
        private float _footContactPower;
        private float _footContactAccuracy;
        private string _footContactZone = string.Empty;

        public TrainingSummaryJson LastSummary { get; private set; }

        public void Configure(
            MatchFlowController matchFlow,
            BallController ball,
            ScorePanel scorePanel,
            AICoachClient coachClient)
        {
            Unsubscribe();
            _matchFlow = matchFlow != null ? matchFlow : _matchFlow;
            _ball = ball != null ? ball : _ball;
            _scorePanel = scorePanel != null ? scorePanel : _scorePanel;
            _coachClient = coachClient != null ? coachClient : _coachClient;
            ResolveReferences();
            Subscribe();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void ResolveReferences()
        {
            if (_matchFlow == null)
                _matchFlow = FindAnyObjectByType<MatchFlowController>(FindObjectsInactive.Include);
            if (_ball == null)
                _ball = FindAnyObjectByType<BallController>(FindObjectsInactive.Include);
            if (_ballInteractor == null && _ball != null)
                _ballInteractor = _ball.GetComponent<PhysicalBallInteractor>();
            if (_ballInteractor == null)
                _ballInteractor = FindAnyObjectByType<PhysicalBallInteractor>(FindObjectsInactive.Include);
            if (_scorePanel == null)
                _scorePanel = FindAnyObjectByType<ScorePanel>(FindObjectsInactive.Include);
            if (_coachClient == null)
                _coachClient = FindAnyObjectByType<AICoachClient>(FindObjectsInactive.Include);
        }

        private void Subscribe()
        {
            if (_matchFlow != null)
            {
                _matchFlow.PhaseChanged -= HandlePhaseChanged;
                _matchFlow.PassStarted -= HandlePassStarted;
                _matchFlow.ReceiveResolved -= HandleReceiveResolved;
                _matchFlow.RecoveryTriggered -= HandleRecoveryTriggered;
                _matchFlow.RecoveryResolved -= HandleRecoveryResolved;
                _matchFlow.ShotAttempted -= HandleShotAttempted;
                _matchFlow.FootContactRecorded -= HandleFootContactRecorded;
                _matchFlow.RoundResolved -= HandleRoundResolved;

                _matchFlow.PhaseChanged += HandlePhaseChanged;
                _matchFlow.PassStarted += HandlePassStarted;
                _matchFlow.ReceiveResolved += HandleReceiveResolved;
                _matchFlow.RecoveryTriggered += HandleRecoveryTriggered;
                _matchFlow.RecoveryResolved += HandleRecoveryResolved;
                _matchFlow.ShotAttempted += HandleShotAttempted;
                _matchFlow.FootContactRecorded += HandleFootContactRecorded;
                _matchFlow.RoundResolved += HandleRoundResolved;
            }

            if (_ballInteractor != null)
            {
                _ballInteractor.PhysicalImpulseApplied -= HandlePhysicalImpulseApplied;
                _ballInteractor.PhysicalImpulseApplied += HandlePhysicalImpulseApplied;
            }
        }

        private void Unsubscribe()
        {
            if (_matchFlow != null)
            {
                _matchFlow.PhaseChanged -= HandlePhaseChanged;
                _matchFlow.PassStarted -= HandlePassStarted;
                _matchFlow.ReceiveResolved -= HandleReceiveResolved;
                _matchFlow.RecoveryTriggered -= HandleRecoveryTriggered;
                _matchFlow.RecoveryResolved -= HandleRecoveryResolved;
                _matchFlow.ShotAttempted -= HandleShotAttempted;
                _matchFlow.FootContactRecorded -= HandleFootContactRecorded;
                _matchFlow.RoundResolved -= HandleRoundResolved;
            }

            if (_ballInteractor != null)
                _ballInteractor.PhysicalImpulseApplied -= HandlePhysicalImpulseApplied;
        }

        private void HandlePhaseChanged(MatchFlowController.Phase phase)
        {
            if (phase == MatchFlowController.Phase.Setup)
                BeginRound();

            if (_roundActive)
                _phaseTransitions.Add($"{Time.time:0.00}:{phase}");
        }

        private void BeginRound()
        {
            _roundActive = true;
            _roundStartedUtc = DateTime.UtcNow;
            _roundStartedTime = Time.time;
            _roundId = Guid.NewGuid().ToString("N");
            _phaseTransitions.Clear();
            _passDirection = Vector3.zero;
            _passDistance = 0f;
            _estimatedPassBallSpeed = 0f;
            _receiveTimingError = -1f;
            _receiveQuality = -1f;
            _receiveByFootContact = false;
            _recoveryTriggered = false;
            _recoverySucceeded = false;
            _shotPower = -1f;
            _shotDirection = Vector3.zero;
            _footVelocityAtTouch = 0f;
            _footContactPower = 0f;
            _footContactAccuracy = 0f;
            _footContactZone = string.Empty;
            LastSummary = null;
        }

        private void HandlePassStarted(Vector3 start, Vector3 end, float flightTime)
        {
            Vector3 pass = end - start;
            _passDistance = pass.magnitude;
            _passDirection = pass.sqrMagnitude > 0.0001f ? pass.normalized : Vector3.zero;
            _estimatedPassBallSpeed = flightTime > 0.001f ? _passDistance / flightTime : 0f;
        }

        private void HandleReceiveResolved(float quality01, float timingError01, bool byFootContact)
        {
            _receiveQuality = Mathf.Clamp01(quality01);
            _receiveTimingError = Mathf.Clamp01(timingError01);
            _receiveByFootContact = byFootContact;
        }

        private void HandleRecoveryTriggered()
        {
            _recoveryTriggered = true;
        }

        private void HandleRecoveryResolved(bool succeeded)
        {
            _recoveryTriggered = true;
            _recoverySucceeded = succeeded;
        }

        private void HandleShotAttempted(float power01, Vector3 direction)
        {
            _shotPower = Mathf.Clamp01(power01);
            _shotDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
        }

        private void HandlePhysicalImpulseApplied(FootContactData data, Vector3 direction, float impulse)
        {
            if (_ball != null && data.BallCollider != null)
            {
                bool sameBall = data.BallCollider.transform == _ball.transform ||
                                data.BallCollider.transform.IsChildOf(_ball.transform);
                if (!sameBall)
                    return;
            }

            RecordFootContact(data);
            if (_shotPower < 0f)
                HandleShotAttempted(data.Power01, direction);
        }

        private void HandleFootContactRecorded(FootContactData data)
        {
            RecordFootContact(data);
        }

        private void RecordFootContact(FootContactData data)
        {
            _footVelocityAtTouch = data.FootVelocity.magnitude;
            _footContactPower = data.Power01;
            _footContactAccuracy = data.Accuracy01;
            _footContactZone = data.ContactZone;
        }

        private void HandleRoundResolved(Scenario scenario, string outcomeLabelOverride)
        {
            if (!_roundActive)
                BeginRound();

            LastSummary = BuildSummary(scenario, outcomeLabelOverride);
            _roundActive = false;

            if (_logSummaryJson)
                Debug.Log($"[AICoach] Training summary\n{LastSummary.ToJson(true)}");

            if (_scorePanel != null)
                _scorePanel.ShowAICoachAnalyzing();

            if (_sendToAICoach && _coachClient != null)
            {
                _coachClient.Analyze(LastSummary, feedback =>
                {
                    if (_scorePanel != null)
                        _scorePanel.ShowAICoachFeedback(feedback);
                });
            }
            else if (_scorePanel != null)
            {
                _scorePanel.ShowAICoachFeedback(AICoachFeedbackResponse.Fallback(LastSummary, "coach disabled"));
            }
        }

        private TrainingSummaryJson BuildSummary(Scenario scenario, string outcomeLabelOverride)
        {
            string result = !string.IsNullOrWhiteSpace(outcomeLabelOverride)
                ? outcomeLabelOverride
                : (scenario != null ? scenario.outcome.ToString() : "Unknown");

            int score = scenario != null ? scenario.finalScore : 0;
            var summary = new TrainingSummaryJson
            {
                roundId = _roundId,
                startedAtUtc = _roundStartedUtc.ToString("o"),
                endedAtUtc = DateTime.UtcNow.ToString("o"),
                durationSeconds = Mathf.Max(0f, Time.time - _roundStartedTime),
                dataSource = GameManager.Instance != null && GameManager.Instance.DataSource != null
                    ? GameManager.Instance.DataSource.SourceName
                    : "Unknown",
                currentScenarioName = scenario != null ? scenario.scenarioName : "Physical Result",
                shotResult = result,
                finalScore = score,
                grade = GradeFromScore(score),
                passDirection = TrainingVector3Json.From(_passDirection),
                passDistance = Round(_passDistance),
                estimatedPassBallSpeed = Round(_estimatedPassBallSpeed),
                ballSpeedAtResult = Round(GetBallSpeed()),
                receiveTimingError = Round(_receiveTimingError),
                receiveQuality = Round(_receiveQuality),
                receiveByFootContact = _receiveByFootContact,
                footVelocityAtTouch = Round(_footVelocityAtTouch),
                footContactPower = Round(_footContactPower),
                footContactAccuracy = Round(_footContactAccuracy),
                footContactZone = _footContactZone,
                recoveryTriggered = _recoveryTriggered,
                recoverySucceeded = _recoverySucceeded,
                shotPower = Round(_shotPower),
                shotDirection = TrainingVector3Json.From(_shotDirection),
                phaseTransitions = _phaseTransitions.ToArray()
            };

            return summary;
        }

        private float GetBallSpeed()
        {
            if (_ball == null)
                return 0f;
            Rigidbody body = _ball.GetComponent<Rigidbody>();
            return body != null ? body.linearVelocity.magnitude : 0f;
        }

        private static string GradeFromScore(int score)
        {
            if (score >= 90) return "A";
            if (score >= 70) return "B";
            if (score >= 50) return "C";
            if (score >= 30) return "D";
            return "F";
        }

        private static float Round(float value)
        {
            return Mathf.Round(value * 1000f) / 1000f;
        }
    }
}
