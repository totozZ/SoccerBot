// MatchFlowController.cs — One-shot match loop driver.

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SoccerBot
{
    public class MatchFlowController : MonoBehaviour
    {
        public enum Phase { Idle, Setup, Pass, Recovery, Possession, Shot, Score, Cooldown }

        public event Action<Phase> PhaseChanged;
        public event Action<Vector3, Vector3, float> PassStarted;
        public event Action<float, float, bool> ReceiveResolved;
        public event Action RecoveryTriggered;
        public event Action<bool> RecoveryResolved;
        public event Action<float, Vector3> ShotAttempted;
        public event Action<FootContactData> FootContactRecorded;
        public event Action<Scenario, string> RoundResolved;

        [Header("Scene References")]
        [SerializeField] private Transform _robotTransform;
        [SerializeField] private Transform _playerTransform;
        [SerializeField] private Transform _teammateTransform;
        [SerializeField] private Transform _opponentTransform;
        [SerializeField] private BallController _ball;
        [SerializeField] private FPSPlayerController _player;
        [SerializeField] private ScenarioTrigger _scenarioTrigger;
        [SerializeField] private ScenarioPlayer _scenarioPlayer;
        [SerializeField] private Transform _fpsCamera;
        [SerializeField] private Transform _fpsAnchor;
        [SerializeField] private ScorePanel _scorePanel;
        [SerializeField] private ScoreBoard _scoreBoard;
        [SerializeField] private GameObject _hudRoot;
        [SerializeField] private ReceptionPromptPresenter _receptionPrompt;
        [SerializeField] private ReceptionTargetIndicator _receptionTargetIndicator;
        [SerializeField] private FieldAIController _fieldAI;

        [Header("Ball Offsets")]
        [SerializeField] private Vector3 _ballOffsetRobot = new(0f, 1.0f, 0.4f);
        [SerializeField] private Vector3 _ballOffsetPlayer = new(0f, 0.3f, 0.6f);
        [SerializeField] private float _passApex = 2.2f;

        [Header("NPC Setup Stance (Player local-space)")]
        [SerializeField] private Vector3 _teammateSetupOffset = new(-1.5f, 0f, 1.5f);
        [SerializeField] private Vector3 _opponentSetupOffset = new(1.5f, 0f, 1.5f);

        [Header("Timing (seconds)")]
        [SerializeField] private float _setupDuration = 2.0f;
        [SerializeField] private float _passFlightTime = 1.6f;
        [SerializeField] private float _cooldownDuration = 3.0f;

        [Header("Reception")]
        [SerializeField, Range(0f, 1f)] private float _receiveWindowStart01 = 0.55f;
        [SerializeField, Range(0f, 1f)] private float _receivePerfect01 = 0.78f;
        [SerializeField, Range(0f, 1f)] private float _receiveWindowEnd01 = 0.95f;
        [SerializeField, Range(1f, 90f)] private float _receiveFacingFullScoreAngle = 12f;
        [SerializeField, Range(1f, 140f)] private float _receiveFacingFailAngle = 65f;
        [SerializeField, Range(0f, 1f)] private float _minimumPossessionReceiveQuality = 0.20f;
        [SerializeField, Range(0f, 0.4f)] private float _receivePowerBonus = 0.18f;
        [SerializeField, Range(0f, 0.4f)] private float _poorReceivePowerPenalty = 0.12f;

        [Header("Foot Contacts")]
        [SerializeField] private bool _acceptFootContacts = true;
        [SerializeField] private bool _requireRightTriggerForFootShot = true;
        [SerializeField] private float _footReceivePerfectSpeed = 0.45f;
        [SerializeField] private float _footReceiveFailSpeed = 2.8f;
        [SerializeField, Range(0f, 1f)] private float _minimumFootShotPower = 0.12f;
        [SerializeField, Range(0f, 1f)] private float _footShotSwingDirectionWeight = 0.65f;
        [SerializeField, Range(0f, 0.5f)] private float _footShotLift = 0.08f;
        [SerializeField] private bool _enableInstepShotAssist = true;
        [SerializeField, Range(0f, 0.6f)] private float _instepShotAssistMax = 0.28f;
        [SerializeField, Range(0f, 1f)] private float _instepShotAssistMinPower = 0.18f;
        [SerializeField, Range(0f, 1f)] private float _instepShotAssistFullPower = 0.72f;
        [SerializeField, Range(5f, 120f)] private float _instepShotAssistMaxAngle = 72f;

        [Header("Player Pass / Shot Resolution")]
        [SerializeField] private bool _enableTeammatePassByAim = true;
        [SerializeField, Range(-1f, 1f)] private float _teammatePassAimDot = 0.82f;
        [SerializeField] private float _teammatePassArrivalRadius = 1.15f;
        [SerializeField, Range(0f, 1f)] private float _teammatePassSlowPower = 0.34f;
        [SerializeField, Range(0f, 1f)] private float _teammatePassFastPower = 0.84f;
        [SerializeField, Range(0f, 1f)] private float _teammatePassGoodScoreChance = 0.62f;

        [Header("Recovery Mash")]
        [SerializeField] private bool _enableRecoveryMash = true;
        [SerializeField, Range(0.8f, 5f)] private float _recoveryDuration = 2.4f;
        [SerializeField, Range(4, 40)] private int _recoveryPressTarget = 14;
        [SerializeField, Range(0.03f, 0.5f)] private float _recoveryPushPerPress = 0.14f;
        [SerializeField, Range(0f, 1.5f)] private float _recoveryHeldPush = 0.36f;
        [SerializeField, Range(0.5f, 12f)] private float _recoveryOpponentLerpSpeed = 8f;
        [SerializeField, Range(0.5f, 4f)] private float _recoverySuccessKnockback = 1.9f;
        [SerializeField, Range(0.2f, 3f)] private float _recoveryFailSurgeDistance = 0.9f;
        [SerializeField, Range(0f, 0.15f)] private float _recoveryShakeAmplitude = 0.035f;
        [SerializeField, Range(4f, 60f)] private float _recoveryShakeFrequency = 32f;

        [Header("Power Routing")]
        [SerializeField, Range(0f, 0.3f)] private float _randomJitter = 0.10f;

        [Header("Teammate Shot Targets (world space)")]
        [SerializeField] private Transform _goalTargetIn;
        [SerializeField] private Transform _goalTargetMiss;
        [SerializeField] private Vector3 _goalTargetInPos = new(0f, 0.8f, 8.9f);
        [SerializeField] private Vector3 _goalTargetMissPos = new(3.5f, 0.5f, 8.6f);

        [Header("Goal / Field Boundary")]
        [SerializeField] private FieldBuilder _fieldBuilder;
        [SerializeField] private bool _ensureMatchBoundaryWalls = true;
        [SerializeField] private bool _showMatchBoundaryWalls = false;
        [SerializeField] private float _matchBoundaryPadding = 0.45f;
        [SerializeField] private float _matchBoundaryWallHeight = 1.8f;
        [SerializeField] private float _matchBoundaryWallThickness = 0.14f;
        [SerializeField] private float _ballOutOfBoundsMargin = 0.25f;
        [SerializeField] private float _ballFallY = -0.75f;
        [SerializeField] private float _opponentGoalWidth = 3.5f;
        [SerializeField] private float _opponentGoalHeight = 1.55f;
        [SerializeField] private float _opponentGoalDepth = 0.6f;
        [SerializeField] private float _goalMouthBoundaryGapPadding = 0.35f;

        [Header("Teammate Shot Animation")]
        [SerializeField] private float _shotPassToTeammate = 0.6f;
        [SerializeField] private float _teammateAimDuration = 0.5f;
        [SerializeField] private float _teammateAimHold = 0.5f;
        [SerializeField] private float _shotFlightTime = 1.4f;
        [SerializeField] private float _shotApex = 2.5f;
        [SerializeField] private float _teammateRunDistance = 2.0f;
        [SerializeField] private float _teammateRunFraction = 0.3f;
        [SerializeField] private float _resultHoldDelay = 0.6f;

        [Header("NPC Reactions")]
        [SerializeField] private bool _opponentTracksBall = true;
        [SerializeField, Range(1f, 10f)] private float _opponentLookSpeed = 4f;
        [SerializeField] private bool _teammateCelebrates = true;

        [Header("Demo Scene Polish")]
        [SerializeField] private bool _enableDemoScenePolish = true;
        [SerializeField] private bool _replaceRobotWithTeammateVisual = true;
        [Tooltip("Global down-scale applied to scene-authored NPCs (1 = original size).")]
        [SerializeField] private float _npcScaleMultiplier = 0.8f;
        [Tooltip("Vertical nudge for the FPS eye height (metres, negative = lower view).")]
        [SerializeField] private float _eyeHeightAdjust = -0.25f;
        [SerializeField] private Vector3 _robotDemoPosition = new(-2.4f, 0f, -3.6f);
        [SerializeField] private Vector3 _robotVisualLocalOffset = Vector3.zero;
        [SerializeField] private Vector3 _robotVisualLocalScale = Vector3.one * 0.576f;
        [SerializeField] private Vector3 _goalkeeperPosition = new(0f, 0f, 8.15f);
        [SerializeField] private Vector3 _goalkeeperLookAt = new(0f, 0f, 2f);
        [SerializeField] private Vector3[] _blueBackgroundNpcPositions =
        {
            new(-2.2f, 0f, 5.8f)
        };
        [SerializeField] private Vector3[] _redBackgroundNpcPositions =
        {
            new(2.4f, 0f, 6.9f)
        };
        [SerializeField] private float _supportNpcForwardDistance = 4.8f;
        [SerializeField] private float _supportNpcBackDistance = 3.6f;
        [SerializeField] private float _goalkeeperForwardDistance = 6.8f;
        [SerializeField] private float _backgroundNpcScale = 0.56f;
        [SerializeField] private float _goalkeeperScale = 0.64f;
        [SerializeField] private float _lampHeadRuntimeZ = 0.24f;
        [SerializeField] private Vector3 _goalkeeperGoalCenter = new(0f, 0.0055f, 8.15f);

        [Header("Whistle")]
        [SerializeField] private AudioSource _whistleSource;
        [SerializeField] private AudioSource _ballReceiveSource;

        [Header("Score Display Data")]
        [SerializeField] private Scenario _scoreSuccessData;
        [SerializeField] private Scenario _shotMissedData;

        private Phase _currentPhase = Phase.Idle;
        public Phase CurrentPhase
        {
            get => _currentPhase;
            private set
            {
                if (_currentPhase == value)
                    return;

                _currentPhase = value;
                PhaseChanged?.Invoke(_currentPhase);
            }
        }
        public enum BoundaryExitKind { Unknown, Sideline, GoalLine, Fall }

        private Coroutine _loop;
        private bool _isMatchRunning;
        private InputAction _menuAction;
        private MainMenuPanel _mainMenu;
        private Transform _goalkeeperTransform;
        private Transform _attackArrowTransform;
        private TextMeshPro _attackArrowLabel;
        private Canvas _fallbackHudCanvas;
        private TextMeshProUGUI _fallbackHudArrowLabel;
        private TextMeshProUGUI _fallbackHudStatusLabel;
        private float _passProgress01;
        private float _receiveQuality = 1f;
        private bool _receiveAttempted;
        private Vector3 _currentPassStart;
        private Vector3 _currentPassEnd;
        private RecoveryMashState _recoveryMash;
        private Transform _recoveryShakeTarget;
        private Vector3 _recoveryShakeBaseLocalPos;
        private CanvasGroup _recoveryHudGroup;
        private RectTransform _recoveryHudRoot;
        private RectTransform _recoveryButtonRect;
        private TextMeshProUGUI _recoveryPromptLabel;
        private TextMeshProUGUI _recoveryMeterLabel;
        private GameObject _matchBoundaryRoot;
        private Coroutine _shotRoutine;
        private bool _rallyResolved;
        private bool _passJudgementActive;
        private float _passJudgementPower01;
        private float _passJudgementAimDot = 1f;
        private float _passJudgementStartedAt = -999f;
        private readonly System.Collections.Generic.List<Image> _recoveryBorderImages = new();
        private readonly System.Collections.Generic.List<Transform> _backgroundNpcTransforms = new();
        private static readonly Color BlueTeamColor = new(0.1f, 0.3f, 0.9f, 1f);
        private static readonly Color RedTeamColor = new(0.9f, 0.1f, 0.1f, 1f);

        void Start()
        {
            ApplyDemoOverrides();
            AutoResolveRefs();
            EnsureAICoachRuntime();
            EnsureFarGoalTargets();
            EnsureMatchBoundaryWalls();
            EnsureDemoScenePolish();
            EnsureFieldAIController();

            if (_player != null)
            {
                _player.OnShoot += HandlePlayerShot;
                _player.OnReceiveAttempt += HandleReceiveAttempt;
            }
            if (_scenarioPlayer != null) _scenarioPlayer.OnScenarioComplete += HandleScenarioComplete;

            _menuAction = new InputAction("OpenMenu", InputActionType.Button);
            _menuAction.AddBinding("<Keyboard>/escape");
            _menuAction.AddBinding("<XRController>{LeftHand}/menuButton");
            _menuAction.AddBinding("<XRController>{RightHand}/secondaryButton");
            _menuAction.Enable();

            ResetForMenu();
        }

        void OnDestroy()
        {
            if (_player != null)
            {
                _player.OnShoot -= HandlePlayerShot;
                _player.OnReceiveAttempt -= HandleReceiveAttempt;
            }
            if (_scenarioPlayer != null) _scenarioPlayer.OnScenarioComplete -= HandleScenarioComplete;
            if (_menuAction != null)
            {
                _menuAction.Disable();
                _menuAction.Dispose();
                _menuAction = null;
            }
        }

        public void PrepareForMatchStart()
        {
            ResetForMenu();
            _scoreBoard?.ResetCounts();
        }

        public void BeginMatch()
        {
            PrepareForMatchStart();
            _isMatchRunning = true;
            if (_hudRoot != null) _hudRoot.SetActive(true);
            if (_mainMenu != null) _mainMenu.gameObject.SetActive(false);
            if (_loop != null) StopCoroutine(_loop);
            _loop = StartCoroutine(MatchLoop());
        }

        public void ResetForMenu()
        {
            _isMatchRunning = false;
            if (_loop != null)
            {
                StopCoroutine(_loop);
                _loop = null;
            }
            if (_shotRoutine != null)
            {
                StopCoroutine(_shotRoutine);
                _shotRoutine = null;
            }

            CurrentPhase = Phase.Idle;
            _rallyResolved = false;
            ResetPassJudgement();
            if (_player != null)
            {
                _player.ShootingEnabled = false;
                _player.MovementEnabled = false;
                _player.ReceptionEnabled = false;
            }
            ResetReceptionState(1f);
            HideRecoveryHud();
            RestoreRecoveryShakeTarget();

            if (_hudRoot != null) _hudRoot.SetActive(false);
            _receptionPrompt?.Hide();
            _receptionTargetIndicator?.Hide();
            _scorePanel?.HideImmediate();
            if (_robotTransform != null) _robotTransform.gameObject.SetActive(true);
            if (_teammateTransform != null) _teammateTransform.gameObject.SetActive(true);
            if (_opponentTransform != null) _opponentTransform.gameObject.SetActive(true);
            if (_ball != null && _robotTransform != null)
                _ball.AttachTo(_robotTransform, _ballOffsetRobot);
        }

        void Update()
        {
            if (_menuAction == null || !_menuAction.WasPressedThisFrame())
            {
                UpdateAttackArrow();
                GuardGoalAndBoundary();
            }
            else if (_mainMenu != null)
            {
                bool menuOpen = _mainMenu.gameObject.activeSelf && _mainMenu.gameObject.activeInHierarchy;
                if (!menuOpen)
                {
                    ResetForMenu();
                    _mainMenu.ShowMenu();
                }
            }
        }

        private void AutoResolveRefs()
        {
            if (_robotTransform == null)
            {
                var go = GameObject.Find("Robot");
                if (go != null) _robotTransform = go.transform;
            }
            {
                var go = GameObject.Find("Player");
                if (go != null) _playerTransform = go.transform;
            }
            {
                var go = GameObject.Find("Teammate");
                if (go != null) _teammateTransform = go.transform;
            }
            if (_opponentTransform == null)
            {
                var go = GameObject.Find("Opponent");
                if (go != null) _opponentTransform = go.transform;
            }
            if (_ball == null) _ball = FindFirstObjectByType<BallController>();
            if (_fieldBuilder == null) _fieldBuilder = FindFirstObjectByType<FieldBuilder>(FindObjectsInactive.Include);
            if (_playerTransform != null)
            {
                var pc = _playerTransform.GetComponent<FPSPlayerController>();
                if (pc != null) _player = pc;
            }
            if (_player == null) _player = FindFirstObjectByType<FPSPlayerController>();
            if (_scenarioTrigger == null) _scenarioTrigger = FindFirstObjectByType<ScenarioTrigger>();
            if (_scenarioPlayer == null) _scenarioPlayer = FindFirstObjectByType<ScenarioPlayer>();
            if (_opponentTransform == null && _scenarioPlayer != null)
                _opponentTransform = _scenarioPlayer.OpponentTransform;
            if (_teammateTransform == null && _scenarioPlayer != null)
                _teammateTransform = _scenarioPlayer.TeammateTransform;
            if (_playerTransform != null)
            {
                var t = _playerTransform.Find("FpsAnchor");
                if (t != null) _fpsAnchor = t;
            }
            if (_teammateTransform != null)
            {
                _teammateSetupOffset = new Vector3(-2.2f, 0f, 2.6f);
            }
            if (_opponentTransform != null)
            {
                _opponentSetupOffset = new Vector3(2.0f, 0f, 3.2f);
            }
            if (_fpsAnchor != null)
            {
                var t = _fpsAnchor.Find("FpsCamera");
                if (t != null) _fpsCamera = t;
            }
            if (_goalTargetIn == null)
            {
                var go = GameObject.Find("GoalTarget_In");
                if (go != null) _goalTargetIn = go.transform;
            }
            if (_goalTargetMiss == null)
            {
                var go = GameObject.Find("GoalTarget_Miss");
                if (go != null) _goalTargetMiss = go.transform;
            }
            if (_scorePanel == null) _scorePanel = FindFirstObjectByType<ScorePanel>(FindObjectsInactive.Include);
            if (_scoreBoard == null) _scoreBoard = FindFirstObjectByType<ScoreBoard>(FindObjectsInactive.Include);
            if (_hudRoot == null)
            {
                var hud = GameObject.Find("HUD");
                if (hud != null) _hudRoot = hud;
            }
            if (_receptionPrompt == null)
                _receptionPrompt = FindFirstObjectByType<ReceptionPromptPresenter>(FindObjectsInactive.Include);
            if (_receptionPrompt == null)
                _receptionPrompt = gameObject.AddComponent<ReceptionPromptPresenter>();
            _receptionPrompt.Configure(_fpsCamera != null ? _fpsCamera : _fpsAnchor);
            if (_receptionTargetIndicator == null)
                _receptionTargetIndicator = FindFirstObjectByType<ReceptionTargetIndicator>(FindObjectsInactive.Include);
            if (_receptionTargetIndicator == null)
            {
                var indicatorGo = new GameObject("ReceptionTargetIndicator");
                _receptionTargetIndicator = indicatorGo.AddComponent<ReceptionTargetIndicator>();
            }
            _receptionTargetIndicator.Hide();
            if (_mainMenu == null) _mainMenu = FindFirstObjectByType<MainMenuPanel>(FindObjectsInactive.Include);
        }

        private void EnsureAICoachRuntime()
        {
            AICoachClient coachClient = FindAnyObjectByType<AICoachClient>(FindObjectsInactive.Include);
            if (coachClient == null)
                coachClient = gameObject.AddComponent<AICoachClient>();

            GameEventRecorder recorder = FindAnyObjectByType<GameEventRecorder>(FindObjectsInactive.Include);
            if (recorder == null)
                recorder = gameObject.AddComponent<GameEventRecorder>();

            recorder.Configure(this, _ball, _scorePanel, coachClient);
        }

        private void EnsureFieldAIController()
        {
            if (_fieldAI == null)
                _fieldAI = FindFirstObjectByType<FieldAIController>(FindObjectsInactive.Include);
            if (_fieldAI == null)
                _fieldAI = gameObject.AddComponent<FieldAIController>();

            _fieldAI.Configure(
                this,
                _ball,
                _playerTransform,
                _teammateTransform,
                _opponentTransform,
                _goalkeeperTransform);
        }

        private void PlayWhistle()
        {
            if (_whistleSource == null)
            {
                _whistleSource = gameObject.AddComponent<AudioSource>();
                _whistleSource.playOnAwake = false;
                _whistleSource.spatialBlend = 0f;
            }
            var clip = WhistleGenerator.Create();
            _whistleSource.PlayOneShot(clip, 0.8f);
        }

        private void PlayBallReceive()
        {
            if (_ballReceiveSource == null)
            {
                _ballReceiveSource = gameObject.AddComponent<AudioSource>();
                _ballReceiveSource.playOnAwake = false;
                _ballReceiveSource.spatialBlend = 0f;
            }
            var clip = ThudGenerator.Create();
            _ballReceiveSource.PlayOneShot(clip, 0.9f);
        }

        private void ApplyDemoOverrides()
        {
            _goalTargetInPos = new Vector3(1.1f, 0.75f, 8.9f);
            _goalTargetMissPos = new Vector3(3.8f, 0.5f, 8.6f);
        }

        private Vector3 GetAttackForward()
        {
            if (_playerTransform != null)
            {
                Vector3 forward = _playerTransform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.001f)
                    return forward.normalized;
            }

            if (_playerTransform != null && _goalTargetIn != null)
            {
                Vector3 forward = _goalTargetIn.position - _playerTransform.position;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.001f)
                    return forward.normalized;
            }

            return Vector3.forward;
        }

        private Vector3 GetAttackRight()
        {
            Vector3 forward = GetAttackForward();
            return new Vector3(forward.z, 0f, -forward.x).normalized;
        }

        private static bool UseQuestPromptText()
        {
            return Application.platform == RuntimePlatform.Android;
        }

        private void EnsureFarGoalTargets()
        {
            if (_goalTargetIn == null)
            {
                var go = new GameObject("GoalTarget_In");
                go.transform.position = _goalTargetInPos;
                _goalTargetIn = go.transform;
            }
            else
            {
                _goalTargetIn.position = _goalTargetInPos;
            }

            if (_goalTargetMiss == null)
            {
                var go = new GameObject("GoalTarget_Miss");
                go.transform.position = _goalTargetMissPos;
                _goalTargetMiss = go.transform;
            }
            else
            {
                _goalTargetMiss.position = _goalTargetMissPos;
            }
        }

        private void EnsureMatchBoundaryWalls()
        {
            if (!_ensureMatchBoundaryWalls)
                return;

            if (_fieldBuilder == null)
                _fieldBuilder = FindFirstObjectByType<FieldBuilder>(FindObjectsInactive.Include);

            Vector3 center = _fieldBuilder != null ? _fieldBuilder.transform.position : Vector3.zero;
            Quaternion rotation = _fieldBuilder != null ? _fieldBuilder.transform.rotation : Quaternion.identity;
            float width = GetFieldHalfWidth() * 2f + _matchBoundaryPadding * 2f;
            float length = GetFieldHalfLength() * 2f + _matchBoundaryPadding * 2f;
            float thickness = Mathf.Max(0.02f, _matchBoundaryWallThickness);
            float height = Mathf.Max(0.2f, _matchBoundaryWallHeight);

            if (_matchBoundaryRoot == null)
                _matchBoundaryRoot = new GameObject("MatchFlowBoundary");

            center.y += height * 0.5f;
            _matchBoundaryRoot.SetActive(true);
            _matchBoundaryRoot.transform.SetPositionAndRotation(center, rotation);

            DisableLegacyBoundaryWall("BackWall");
            DisableLegacyBoundaryWall("FrontWall");

            EnsureGoalLineBoundaryWalls("BackWall", -length * 0.5f, width, height, thickness);
            EnsureGoalLineBoundaryWalls("FrontWall", length * 0.5f, width, height, thickness);
            EnsureMatchBoundaryWall("LeftWall", new Vector3(-width * 0.5f, 0f, 0f), new Vector3(thickness, height, length), BoundaryExitKind.Sideline);
            EnsureMatchBoundaryWall("RightWall", new Vector3(width * 0.5f, 0f, 0f), new Vector3(thickness, height, length), BoundaryExitKind.Sideline);
            EnsureOpponentGoalTrigger(height);
        }

        private void EnsureGoalLineBoundaryWalls(string prefix, float localZ, float width, float height, float thickness)
        {
            float gapWidth = Mathf.Clamp(
                _opponentGoalWidth + _goalMouthBoundaryGapPadding * 2f,
                0f,
                Mathf.Max(0f, width - thickness * 4f));
            float segmentWidth = Mathf.Max(0.02f, (width - gapWidth) * 0.5f);
            float centerOffset = gapWidth * 0.5f + segmentWidth * 0.5f;

            EnsureMatchBoundaryWall(
                $"{prefix}Left",
                new Vector3(-centerOffset, 0f, localZ),
                new Vector3(segmentWidth, height, thickness),
                BoundaryExitKind.GoalLine);
            EnsureMatchBoundaryWall(
                $"{prefix}Right",
                new Vector3(centerOffset, 0f, localZ),
                new Vector3(segmentWidth, height, thickness),
                BoundaryExitKind.GoalLine);
        }

        private void DisableLegacyBoundaryWall(string wallName)
        {
            if (_matchBoundaryRoot == null)
                return;

            Transform wall = _matchBoundaryRoot.transform.Find(wallName);
            if (wall != null)
                wall.gameObject.SetActive(false);
        }

        private void EnsureMatchBoundaryWall(string wallName, Vector3 localPosition, Vector3 localScale, BoundaryExitKind exitKind)
        {
            Transform wall = _matchBoundaryRoot.transform.Find(wallName);
            if (wall == null)
            {
                GameObject wallGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wallGo.name = wallName;
                wallGo.layer = 2;
                wallGo.transform.SetParent(_matchBoundaryRoot.transform, false);

                var reporter = wallGo.GetComponent<MatchBoundaryWall>();
                if (reporter == null)
                    reporter = wallGo.AddComponent<MatchBoundaryWall>();
                reporter.Configure(this, exitKind);

                Renderer renderer = wallGo.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = new Color(0.08f, 0.22f, 0.35f, 0.35f);
                    renderer.enabled = _showMatchBoundaryWalls;
                }

                wall = wallGo.transform;
            }
            else
            {
                var reporter = wall.GetComponent<MatchBoundaryWall>();
                if (reporter == null)
                    reporter = wall.gameObject.AddComponent<MatchBoundaryWall>();
                reporter.Configure(this, exitKind);
            }

            if (wall == null)
            {
                Debug.LogWarning($"[MatchFlow] Boundary wall '{wallName}' could not be created.");
                return;
            }

            wall.localPosition = localPosition;
            wall.localRotation = Quaternion.identity;
            wall.localScale = localScale;

            Renderer existingRenderer = wall.GetComponent<Renderer>();
            if (existingRenderer != null)
                existingRenderer.enabled = _showMatchBoundaryWalls;
        }

        private void EnsureOpponentGoalTrigger(float boundaryHeight)
        {
            if (_matchBoundaryRoot == null)
                return;

            Transform trigger = _matchBoundaryRoot.transform.Find("OpponentGoalTrigger");
            if (trigger == null)
            {
                GameObject triggerGo = new GameObject("OpponentGoalTrigger", typeof(BoxCollider), typeof(MatchGoalTrigger));
                triggerGo.layer = 2;
                triggerGo.transform.SetParent(_matchBoundaryRoot.transform, false);
                trigger = triggerGo.transform;
            }

            float halfLength = GetFieldHalfLength();
            float goalDepth = Mathf.Max(0.05f, _opponentGoalDepth);
            float goalHeight = Mathf.Max(0.1f, _opponentGoalHeight);
            float triggerDepth = goalDepth * 2f + _ballOutOfBoundsMargin;
            float centerZ = halfLength + _ballOutOfBoundsMargin * 0.5f;
            float centerY = (goalHeight - Mathf.Max(0.2f, boundaryHeight)) * 0.5f;

            trigger.gameObject.SetActive(true);
            trigger.localPosition = new Vector3(0f, centerY, centerZ);
            trigger.localRotation = Quaternion.identity;
            trigger.localScale = Vector3.one;

            BoxCollider collider = trigger.GetComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.center = Vector3.zero;
            collider.size = new Vector3(
                Mathf.Max(0.1f, _opponentGoalWidth),
                goalHeight,
                Mathf.Max(0.1f, triggerDepth));

            MatchGoalTrigger reporter = trigger.GetComponent<MatchGoalTrigger>();
            reporter.Configure(this);
        }

        private void EnsureDemoScenePolish()
        {
            if (!_enableDemoScenePolish) return;

            EnsureAtmosphereControllers();
            EnsureRobotVisualSwap();
            EnsureGoalkeeper();
            EnsureBackgroundNpcs();
            EnsureAttackArrow();
            EnsureFallbackHud();
            ApplyNpcDownscale();
            ApplyEyeHeight();
        }

        private void EnsureAtmosphereControllers()
        {
            if (FindFirstObjectByType<WeatherController>(FindObjectsInactive.Include) == null)
                gameObject.AddComponent<WeatherController>();

            var lighting = FindFirstObjectByType<LightingConfigurator>(FindObjectsInactive.Include);
            if (lighting != null)
                lighting.Apply();
        }

        // Lowers (or raises) the FPS eye height by a fixed nudge, once.
        private bool _eyeHeightApplied;
        private void ApplyEyeHeight()
        {
            if (_eyeHeightApplied || Mathf.Approximately(_eyeHeightAdjust, 0f)) return;
            if (_fpsAnchor == null) return;
            _eyeHeightApplied = true;

            Vector3 p = _fpsAnchor.localPosition;
            p.y += _eyeHeightAdjust;
            _fpsAnchor.localPosition = p;
        }

        // Down-scales the scene-authored NPCs (the runtime-spawned robot /
        // goalkeeper / support NPCs are already scaled at creation time).
        // The ball keeps its authored scale because its visible mesh already
        // matches the collision/trajectory offsets.
        // Guarded so it only ever runs once, even if polish is re-invoked.
        private bool _npcDownscaleApplied;
        private void ApplyNpcDownscale()
        {
            if (_npcDownscaleApplied || Mathf.Approximately(_npcScaleMultiplier, 1f)) return;
            _npcDownscaleApplied = true;

            ScaleTransform(_teammateTransform);
            ScaleTransform(_opponentTransform);
        }

        private void ScaleTransform(Transform t)
        {
            if (t == null) return;
            t.localScale *= _npcScaleMultiplier;
        }

        private void EnsureRobotVisualSwap()
        {
            if (!_replaceRobotWithTeammateVisual || _robotTransform == null) return;

            _robotTransform.position = _robotDemoPosition;
            _robotTransform.rotation = Quaternion.LookRotation(Vector3.forward);

            var builder = _robotTransform.GetComponent<CharacterBuilder>();
            if (builder != null) builder.enabled = false;

            Transform existingModel = _robotTransform.Find("Model");
            if (existingModel != null)
            {
                existingModel.localPosition = _robotVisualLocalOffset;
                existingModel.localRotation = Quaternion.identity;
                existingModel.localScale = _robotVisualLocalScale * _npcScaleMultiplier;
                TintRenderers(existingModel.gameObject, BlueTeamColor);
                return;
            }

            Transform sourceModel = FindVisualTemplate(_teammateTransform);
            if (sourceModel == null) sourceModel = FindVisualTemplate(_opponentTransform);
            if (sourceModel == null) return;

            var model = Instantiate(sourceModel.gameObject, _robotTransform);
            model.name = "Model";
            model.transform.localPosition = _robotVisualLocalOffset;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = _robotVisualLocalScale * _npcScaleMultiplier;
            TintRenderers(model, BlueTeamColor);
        }

        private void EnsureGoalkeeper()
        {
            Vector3 forward = GetAttackForward();
            _goalkeeperPosition = _goalkeeperGoalCenter;
            _goalkeeperLookAt = _goalkeeperGoalCenter - forward * _goalkeeperForwardDistance;
            _goalkeeperLookAt.y = _goalkeeperGoalCenter.y;

            var existing = GameObject.Find("Goalkeeper");
            if (existing != null)
            {
                _goalkeeperTransform = existing.transform;
            }
            else if (_goalkeeperTransform == null)
            {
                _goalkeeperTransform = CreateNpcFromPrefab(
                    "Goalkeeper",
                    "strong man b",
                    _goalkeeperPosition,
                    RedTeamColor,
                    Vector3.one * (_goalkeeperScale * _npcScaleMultiplier));
            }

            if (_goalkeeperTransform == null) return;

            _goalkeeperTransform.gameObject.SetActive(true);
            _goalkeeperTransform.position = _goalkeeperPosition;
            Vector3 toLook = _goalkeeperLookAt - _goalkeeperTransform.position;
            toLook.y = 0f;
            if (toLook.sqrMagnitude > 0.001f)
                _goalkeeperTransform.rotation = Quaternion.LookRotation(toLook);
        }

        private void EnsureBackgroundNpcs()
        {
            if (_backgroundNpcTransforms.Count == 0)
            {
                SpawnBackgroundNpcSet("BlueSupport", "normal man a", _blueBackgroundNpcPositions, BlueTeamColor);
                SpawnBackgroundNpcSet("RedSupport", "normal woman b", _redBackgroundNpcPositions, RedTeamColor);
            }

            Vector3 forward = GetAttackForward();
            Vector3 right = GetAttackRight();
            Vector3 center = (_playerTransform != null ? _playerTransform.position : Vector3.zero) - forward * _supportNpcBackDistance;
            center.y = 0f;

            for (int i = 0; i < _backgroundNpcTransforms.Count; i++)
            {
                Transform npc = _backgroundNpcTransforms[i];
                if (npc == null) continue;

                float side = i % 2 == 0 ? -1f : 1f;
                float depth = i < 2 ? 0f : -1.2f;
                npc.position = center + right * side * 2.6f + forward * depth;

                Vector3 toLook = (_playerTransform != null ? _playerTransform.position : _goalkeeperLookAt) - npc.position;
                toLook.y = 0f;
                if (toLook.sqrMagnitude > 0.001f)
                    npc.rotation = Quaternion.LookRotation(toLook);
            }
        }

        private void SpawnBackgroundNpcSet(string prefix, string prefabName, Vector3[] positions, Color color)
        {
            if (positions == null) return;

            for (int i = 0; i < positions.Length; i++)
            {
                var npc = CreateNpcFromPrefab(
                    $"{prefix}_{i + 1}",
                    prefabName,
                    positions[i],
                    color,
                    Vector3.one * (_backgroundNpcScale * _npcScaleMultiplier));

                if (npc == null) continue;

                Vector3 toLook = _goalkeeperLookAt - npc.position;
                toLook.y = 0f;
                if (toLook.sqrMagnitude > 0.001f)
                    npc.rotation = Quaternion.LookRotation(toLook);

                _backgroundNpcTransforms.Add(npc);
            }
        }

        private Transform CreateNpcFromPrefab(string objectName, string prefabName, Vector3 position, Color color, Vector3 scale)
        {
            var existing = GameObject.Find(objectName);
            if (existing != null) return existing.transform;

            Transform sourceModel = ResolveNpcVisualTemplate(prefabName, color);
            if (sourceModel == null) return null;

            var root = new GameObject(objectName).transform;
            root.position = position;
            var model = Instantiate(sourceModel.gameObject, root);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = scale;
            TintRenderers(model, color);
            return root;
        }

        private Transform ResolveNpcVisualTemplate(string prefabName, Color color)
        {
            Transform preferred = FindVisualTemplateByName(prefabName, _teammateTransform, _opponentTransform, _robotTransform);
            if (preferred != null) return preferred;

            bool useBlueSource = color == BlueTeamColor;
            if (useBlueSource)
            {
                preferred = FindVisualTemplate(_teammateTransform);
                if (preferred == null) preferred = FindVisualTemplate(_opponentTransform);
            }
            else
            {
                preferred = FindVisualTemplate(_opponentTransform);
                if (preferred == null) preferred = FindVisualTemplate(_teammateTransform);
            }

            if (preferred != null) return preferred;
            return FindVisualTemplate(_robotTransform);
        }

        private static Transform FindVisualTemplateByName(string prefabName, params Transform[] roots)
        {
            if (string.IsNullOrWhiteSpace(prefabName) || roots == null) return null;

            string normalizedPrefabName = NormalizeVisualName(prefabName);
            foreach (Transform root in roots)
            {
                if (root == null) continue;

                foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                {
                    if (NormalizeVisualName(candidate.name) != normalizedPrefabName) continue;
                    Transform template = FindVisualTemplate(candidate);
                    if (template != null) return template;
                }

                if (NormalizeVisualName(root.name) == normalizedPrefabName)
                {
                    Transform template = FindVisualTemplate(root);
                    if (template != null) return template;
                }
            }

            return null;
        }

        private static string NormalizeVisualName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return value.Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
        }

        private static void TintRenderers(GameObject root, Color color)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(block);
            }
        }

        private static Transform FindVisualTemplate(Transform root)
        {
            if (root == null) return null;

            var directModel = root.Find("Model");
            if (directModel != null) return directModel;

            foreach (Transform child in root)
            {
                if (child.GetComponentInChildren<Renderer>(true) != null)
                    return child;
            }

            return root.GetComponentInChildren<Renderer>(true) != null ? root : null;
        }

        private void EnsureAttackArrow()
        {
            if (_attackArrowTransform != null) return;

            var root = new GameObject("AttackDirectionArrow").transform;
            _attackArrowTransform = root;
            root.localScale = Vector3.one * 0.08f;

            var label = root.gameObject.AddComponent<TextMeshPro>();
            label.text = "→";
            label.fontSize = 12f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 0.82f, 0.12f, 1f);
            label.outlineWidth = 0.25f;
            label.enableWordWrapping = false;
            _attackArrowLabel = label;

            UpdateAttackArrow();
        }

        private void UpdateAttackArrow()
        {
            if (_playerTransform == null) return;

            bool showHud = _isMatchRunning && CurrentPhase != Phase.Idle;
            if (_fallbackHudCanvas != null)
                _fallbackHudCanvas.gameObject.SetActive(showHud);
            if (_attackArrowTransform != null)
                _attackArrowTransform.gameObject.SetActive(showHud);
            if (!showHud) return;

            Transform view = _fpsCamera != null ? _fpsCamera : _playerTransform;
            Vector3 targetPos = _goalkeeperTransform != null ? _goalkeeperTransform.position : _goalkeeperGoalCenter;
            Vector3 local = view.InverseTransformPoint(targetPos);

            string horizontal = local.x > 0.35f ? "→" : local.x < -0.35f ? "←" : string.Empty;
            string vertical = local.y > 0.2f ? "↑" : local.y < -0.15f ? "↓" : string.Empty;
            string arrowText = string.IsNullOrEmpty(vertical + horizontal) ? "↑" : vertical + horizontal;

            if (_attackArrowLabel != null)
                _attackArrowLabel.text = arrowText;

            if (_fallbackHudArrowLabel != null)
                _fallbackHudArrowLabel.text = arrowText;
            if (_fallbackHudStatusLabel != null)
                _fallbackHudStatusLabel.text = "ATTACK";

            if (_attackArrowTransform != null)
            {
                Vector3 clampedLocal = new Vector3(
                    Mathf.Clamp(local.x * 0.16f, -0.55f, 0.55f),
                    Mathf.Clamp(local.y * 0.14f + 0.18f, -0.28f, 0.38f),
                    1.0f);

                _attackArrowTransform.position = view.TransformPoint(clampedLocal);
                _attackArrowTransform.rotation = Quaternion.LookRotation(view.forward, view.up);
            }

            if (_fallbackHudCanvas != null)
            {
                Transform hud = _fallbackHudCanvas.transform;
                hud.position = view.position + view.forward * 1.0f + view.up * 0.22f + view.right * -0.42f;
                hud.rotation = Quaternion.LookRotation(view.forward, view.up);
            }
        }

        private void EnsureFallbackHud()
        {
            if (_fallbackHudCanvas != null) return;

            var root = new GameObject("FallbackDirectionHUD");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 999;
            root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 30f;
            root.AddComponent<GraphicRaycaster>();

            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(420f, 180f);
            rt.localScale = Vector3.one * 0.0022f;

            var arrowGo = new GameObject("Arrow", typeof(RectTransform), typeof(TextMeshProUGUI));
            arrowGo.transform.SetParent(root.transform, false);
            var arrowRt = arrowGo.GetComponent<RectTransform>();
            arrowRt.anchorMin = new Vector2(0.5f, 0.5f);
            arrowRt.anchorMax = new Vector2(0.5f, 0.5f);
            arrowRt.sizeDelta = new Vector2(260f, 100f);
            arrowRt.anchoredPosition = new Vector2(0f, 18f);
            _fallbackHudArrowLabel = arrowGo.GetComponent<TextMeshProUGUI>();
            _fallbackHudArrowLabel.text = "↑";
            _fallbackHudArrowLabel.fontSize = 64f;
            _fallbackHudArrowLabel.alignment = TextAlignmentOptions.Center;
            _fallbackHudArrowLabel.color = new Color(1f, 0.82f, 0.12f, 1f);

            var statusGo = new GameObject("Status", typeof(RectTransform), typeof(TextMeshProUGUI));
            statusGo.transform.SetParent(root.transform, false);
            var statusRt = statusGo.GetComponent<RectTransform>();
            statusRt.anchorMin = new Vector2(0.5f, 0.5f);
            statusRt.anchorMax = new Vector2(0.5f, 0.5f);
            statusRt.sizeDelta = new Vector2(260f, 40f);
            statusRt.anchoredPosition = new Vector2(0f, -42f);
            _fallbackHudStatusLabel = statusGo.GetComponent<TextMeshProUGUI>();
            _fallbackHudStatusLabel.text = "ATTACK";
            _fallbackHudStatusLabel.fontSize = 24f;
            _fallbackHudStatusLabel.alignment = TextAlignmentOptions.Center;
            _fallbackHudStatusLabel.color = Color.white;

            _fallbackHudCanvas = canvas;
            root.SetActive(false);
            UpdateAttackArrow();
        }

        private void EnsureLampHeadHeight()
        {
            var lampHeads = FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in lampHeads)
            {
                if (t == null || t.name != "LampHead") continue;
                Vector3 localPos = t.localPosition;
                localPos.z = _lampHeadRuntimeZ;
                t.localPosition = localPos;
            }
        }

        private void UpdateTeammateLookAtPlayer()
        {
            if (_teammateTransform == null || _playerTransform == null) return;

            Vector3 toPlayer = _playerTransform.position - _teammateTransform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.0001f)
                _teammateTransform.rotation = Quaternion.LookRotation(toPlayer);
        }

        private IEnumerator MatchLoop()
        {
            while (_isMatchRunning)
            {
                yield return DoSetup();
                if (!_isMatchRunning) yield break;
                yield return DoPass();
                if (!_isMatchRunning) yield break;
                if (CurrentPhase == Phase.Shot)
                {
                    yield return DoShotAndScore();
                    if (!_isMatchRunning) yield break;
                    yield return DoCooldown();
                    continue;
                }
                yield return DoPossession();
                if (!_isMatchRunning) yield break;
                yield return DoShotAndScore();
                if (!_isMatchRunning) yield break;
                yield return DoCooldown();
            }

            _loop = null;
        }

        private IEnumerator DoSetup()
        {
            CurrentPhase = Phase.Setup;
            _rallyResolved = false;
            ResetPassJudgement();
            EnsureMatchBoundaryWalls();
            if (_player != null)
            {
                _player.ShootingEnabled = false;
                _player.MovementEnabled = false;
                _player.ReceptionEnabled = false;
            }
            if (_teammateTransform != null && _playerTransform != null)
            {
                _teammateTransform.gameObject.SetActive(true);
                _teammateTransform.position = _playerTransform.TransformPoint(_teammateSetupOffset);
                UpdateTeammateLookAtPlayer();
            }
            if (_opponentTransform != null && _playerTransform != null)
            {
                _opponentTransform.gameObject.SetActive(true);
                _opponentTransform.position = _playerTransform.TransformPoint(_opponentSetupOffset);
                Vector3 toPlayer = _playerTransform.position - _opponentTransform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.0001f)
                    _opponentTransform.rotation = Quaternion.LookRotation(toPlayer);
            }
            if (_ball != null && _robotTransform != null)
                _ball.AttachTo(_robotTransform, _ballOffsetRobot);
            yield return new WaitForSeconds(_setupDuration);
        }

        private IEnumerator DoPass()
        {
            CurrentPhase = Phase.Pass;
            PlayWhistle();

            if (_ball == null || _robotTransform == null || _playerTransform == null)
            {
                yield return null;
                yield break;
            }

            _ball.Detach();
            Vector3 startPos = _robotTransform.TransformPoint(_ballOffsetRobot);
            Vector3 endPos = _playerTransform.TransformPoint(_ballOffsetPlayer);
            ResetReceptionState(0f);
            _currentPassStart = startPos;
            _currentPassEnd = endPos;
            PassStarted?.Invoke(startPos, endPos, _passFlightTime);
            _receptionTargetIndicator?.Show(endPos);
            _receptionPrompt?.ShowPassProgress(
                _passProgress01,
                _receiveWindowStart01,
                _receivePerfect01,
                _receiveWindowEnd01,
                UseQuestPromptText());
            if (_player != null)
            {
                _player.ReceptionEnabled = true;
                _player.ShootingEnabled = false;
                _player.MovementEnabled = false;
            }

            float t = 0f;
            while (t < _passFlightTime)
            {
                if (CurrentPhase != Phase.Pass || _rallyResolved)
                    yield break;

                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / _passFlightTime);
                _passProgress01 = u;
                if (!_receiveAttempted)
                {
                    _receptionPrompt?.ShowPassProgress(
                        _passProgress01,
                        _receiveWindowStart01,
                        _receivePerfect01,
                        _receiveWindowEnd01,
                        UseQuestPromptText());
                    _receptionTargetIndicator?.UpdateProgress(
                        _passProgress01,
                        _receiveWindowStart01,
                        _receivePerfect01,
                        _receiveWindowEnd01);
                }
                Vector3 pos = Vector3.Lerp(startPos, endPos, u);
                pos.y += _passApex * 4f * u * (1f - u);
                _ball.transform.position = pos;
                yield return null;
            }
            if (CurrentPhase != Phase.Pass || _rallyResolved)
                yield break;

            _ball.transform.position = endPos;
            _passProgress01 = 1f;
            if (_player != null) _player.ReceptionEnabled = false;
            if (!_receiveAttempted)
            {
                _receiveQuality = 0.12f;
                ReceiveResolved?.Invoke(_receiveQuality, Mathf.Abs(1f - _receivePerfect01), false);
                _receptionPrompt?.ShowReceiveFeedback(_receiveQuality, false, 1.5f);
                _receptionTargetIndicator?.ShowFeedback(_receiveQuality, false);
                Debug.Log("[MatchFlow] Receive missed: no player input during pass.");
            }
        }

        private IEnumerator DoPossession()
        {
            CurrentPhase = Phase.Possession;
            if (_receiveQuality < _minimumPossessionReceiveQuality)
            {
                Debug.Log($"[MatchFlow] Poor first touch ({_receiveQuality:0.00}); opponent wins the ball.");
                _receptionPrompt?.ShowRecovery();
                RecoveryTriggered?.Invoke();
                if (_enableRecoveryMash)
                {
                    yield return DoRecoveryBattle();
                    if (_recoveryMash == null || !_recoveryMash.Succeeded) yield break;
                }
                else
                {
                    ResolveRecoveryFailure();
                    yield break;
                }
            }

            CurrentPhase = Phase.Possession;
            if (_ball != null && _playerTransform != null)
            {
                _ball.AttachTo(_playerTransform, _ballOffsetPlayer);
                PlayBallReceive();   // heavy "哒" trap thud as the player receives the pass
            }
            if (_player != null)
            {
                _player.ShootingEnabled = true;
                _player.MovementEnabled = true;
            }
            _receptionPrompt?.ShowPossession(_receiveQuality, UseQuestPromptText());

            while (CurrentPhase == Phase.Possession)
            {
                UpdateTeammateLookAtPlayer();

                if (_opponentTracksBall && _opponentTransform != null && _ball != null)
                {
                    Vector3 toBall = _ball.transform.position - _opponentTransform.position;
                    toBall.y = 0f;
                    if (toBall.sqrMagnitude > 0.01f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(toBall);
                        _opponentTransform.rotation = Quaternion.Slerp(
                            _opponentTransform.rotation,
                            targetRot,
                            Time.deltaTime * _opponentLookSpeed);
                    }
                }
                yield return null;
            }
        }

        private IEnumerator DoShotAndScore()
        {
            float timeout = 12f;
            float t = 0f;
            while (CurrentPhase != Phase.Score && t < timeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator DoCooldown()
        {
            CurrentPhase = Phase.Cooldown;
            yield return new WaitForSeconds(_cooldownDuration);
        }

        public void NotifyBoundaryHit(BallController ball, BoundaryExitKind exitKind = BoundaryExitKind.Unknown)
        {
            if (ball == null || _ball == null || ball != _ball)
                return;

            ResolveBallOutOfPlay(exitKind, "[MatchFlow] Ball hit field boundary.");
        }

        public void NotifyOpponentGoal(BallController ball)
        {
            if (ball == null || _ball == null || ball != _ball)
                return;

            ResolveSelfGoal();
        }

        public bool TryResolveAiInterception(string reason)
        {
            if (!_isMatchRunning || _rallyResolved)
                return false;
            if (CurrentPhase != Phase.Pass && CurrentPhase != Phase.Possession && CurrentPhase != Phase.Shot)
                return false;

            Phase interceptedFrom = CurrentPhase;
            StopActiveShotRoutine();
            ResetPassJudgement();
            CurrentPhase = Phase.Shot;
            if (_player != null)
            {
                _player.ShootingEnabled = false;
                _player.MovementEnabled = false;
                _player.ReceptionEnabled = false;
            }
            if (_ball != null)
            {
                _ball.Detach();
                _ball.SetPhysicalSimulation(false, true);
            }

            Transform origin = interceptedFrom == Phase.Pass ? _robotTransform : _playerTransform;
            if (_scenarioTrigger != null)
            {
                PlayInterceptedScenario(origin);
            }
            else
            {
                if (_scorePanel != null && _shotMissedData != null)
                    _scorePanel.Show(_shotMissedData, "INTERCEPTED", "STOPPED", "The opponent read the pass lane and won the ball.");
                if (_scoreBoard != null)
                    _scoreBoard.Record(ScenarioOutcome.Intercepted);
                HandleShotResolved();
                NotifyRoundResolved(_shotMissedData, "INTERCEPTED");
            }
            Debug.Log(string.IsNullOrWhiteSpace(reason) ? "[MatchFlow] AI interception resolved." : reason);
            return true;
        }

        public bool TryResolveGoalkeeperSave(string reason)
        {
            if (!_isMatchRunning || _rallyResolved || CurrentPhase != Phase.Shot)
                return false;

            StopActiveShotRoutine();
            ResetPassJudgement();
            CurrentPhase = Phase.Shot;
            if (_player != null)
            {
                _player.ShootingEnabled = false;
                _player.MovementEnabled = false;
                _player.ReceptionEnabled = false;
            }
            if (_ball != null)
            {
                _ball.Detach();
                _ball.SetPhysicalSimulation(false, true);
                if (_goalkeeperTransform != null)
                    _ball.transform.position = _goalkeeperTransform.position - GetAttackForward() * 0.45f + Vector3.up * 0.35f;
            }

            if (_scorePanel != null && _shotMissedData != null)
                _scorePanel.Show(_shotMissedData, "SAVED", "KEEPER SAVE", "The goalkeeper tracked the shot and kicked it away.");
            if (_scoreBoard != null)
                _scoreBoard.Record(ScenarioOutcome.Missed);

            Debug.Log(string.IsNullOrWhiteSpace(reason) ? "[MatchFlow] Goalkeeper save resolved." : reason);
            HandleShotResolved();
            NotifyRoundResolved(_shotMissedData, "SAVED");
            return true;
        }

        private void GuardGoalAndBoundary()
        {
            if (!_isMatchRunning || _rallyResolved || _ball == null)
                return;
            if (CurrentPhase != Phase.Possession && CurrentPhase != Phase.Shot)
                return;

            if (CurrentPhase == Phase.Possession &&
                _passJudgementActive &&
                Time.time - _passJudgementStartedAt > 6f)
            {
                ResetPassJudgement();
            }

            if (IsBallInOpponentGoal())
            {
                ResolveSelfGoal();
                return;
            }

            if (CurrentPhase == Phase.Possession && _passJudgementActive && IsBallNearTeammate(out float teammateDistance))
            {
                ResolveTeammatePassArrival(teammateDistance);
                return;
            }

            if (IsBallOutOfBounds(out BoundaryExitKind exitKind))
                ResolveBallOutOfPlay(exitKind, "[MatchFlow] Ball left field bounds.");
        }

        private bool IsBallInOpponentGoal()
        {
            if (_ball == null)
                return false;

            Vector3 local = GetFieldLocalBallPosition();
            float halfLength = GetFieldHalfLength();
            float halfGoalWidth = Mathf.Max(0.1f, _opponentGoalWidth * 0.5f);
            float goalDepth = Mathf.Max(0.05f, _opponentGoalDepth);
            float goalHeight = Mathf.Max(0.1f, _opponentGoalHeight);

            return local.z >= halfLength - goalDepth
                && local.z <= halfLength + goalDepth + _ballOutOfBoundsMargin
                && Mathf.Abs(local.x) <= halfGoalWidth
                && local.y >= -0.05f
                && local.y <= goalHeight;
        }

        private bool IsBallOutOfBounds(out BoundaryExitKind exitKind)
        {
            exitKind = BoundaryExitKind.Unknown;
            if (_ball == null)
                return false;

            Vector3 local = GetFieldLocalBallPosition();
            float halfWidth = GetFieldHalfWidth() + _ballOutOfBoundsMargin;
            float halfLength = GetFieldHalfLength() + _ballOutOfBoundsMargin;

            if (_ball.transform.position.y < _ballFallY)
            {
                exitKind = BoundaryExitKind.Fall;
                return true;
            }

            if (Mathf.Abs(local.x) > halfWidth)
            {
                exitKind = BoundaryExitKind.Sideline;
                return true;
            }

            if (local.z > halfLength || local.z < -halfLength)
            {
                exitKind = BoundaryExitKind.GoalLine;
                return true;
            }

            return false;
        }

        private Vector3 GetFieldLocalBallPosition()
        {
            if (_fieldBuilder == null)
                _fieldBuilder = FindFirstObjectByType<FieldBuilder>(FindObjectsInactive.Include);

            if (_fieldBuilder != null)
                return _fieldBuilder.transform.InverseTransformPoint(_ball.transform.position);

            return _ball.transform.position;
        }

        private float GetFieldHalfWidth()
        {
            if (_fieldBuilder == null)
                _fieldBuilder = FindFirstObjectByType<FieldBuilder>(FindObjectsInactive.Include);
            return _fieldBuilder != null ? Mathf.Max(0.5f, _fieldBuilder._halfWidth) : 6f;
        }

        private float GetFieldHalfLength()
        {
            if (_fieldBuilder == null)
                _fieldBuilder = FindFirstObjectByType<FieldBuilder>(FindObjectsInactive.Include);
            return _fieldBuilder != null ? Mathf.Max(0.5f, _fieldBuilder._halfLength) : 9f;
        }

        private void ResolveSelfGoal()
        {
            if (_rallyResolved)
                return;

            StopActiveShotRoutine();
            ResetPassJudgement();
            CurrentPhase = Phase.Shot;
            if (_player != null)
            {
                _player.ShootingEnabled = false;
                _player.MovementEnabled = false;
                _player.ReceptionEnabled = false;
            }
            if (_ball != null) _ball.SetPhysicalSimulation(false, true);
            if (_scorePanel != null && _scoreSuccessData != null) _scorePanel.Show(_scoreSuccessData);
            if (_scoreBoard != null) _scoreBoard.Record(ScenarioOutcome.Score);
            if (_teammateCelebrates && _teammateTransform != null)
                StartCoroutine(CelebrationBounce(_teammateTransform));

            Debug.Log("[MatchFlow] Player shot scored in opponent goal.");
            HandleShotResolved();
            NotifyRoundResolved(_scoreSuccessData, null);
        }

        private void ResolveBallOutOfPlay(BoundaryExitKind exitKind, string reason)
        {
            if (!_isMatchRunning || _rallyResolved)
                return;
            if (CurrentPhase == Phase.Idle || CurrentPhase == Phase.Setup || CurrentPhase == Phase.Cooldown)
                return;

            StopActiveShotRoutine();
            ResetPassJudgement();
            CurrentPhase = Phase.Shot;
            if (_player != null)
            {
                _player.ShootingEnabled = false;
                _player.MovementEnabled = false;
                _player.ReceptionEnabled = false;
            }
            if (_ball != null) _ball.SetPhysicalSimulation(false, true);

            string label = exitKind == BoundaryExitKind.GoalLine ? "JUST MISSED" : "OUT OF BOUNDS";
            if (_scorePanel != null && _shotMissedData != null) _scorePanel.Show(_shotMissedData, label);
            if (_scoreBoard != null) _scoreBoard.Record(ScenarioOutcome.Missed);

            Debug.Log($"{reason} kind={exitKind}");
            HandleShotResolved();
            NotifyRoundResolved(_shotMissedData, label);
        }

        private void StopActiveShotRoutine()
        {
            if (_shotRoutine == null)
                return;

            StopCoroutine(_shotRoutine);
            _shotRoutine = null;
        }

        private IEnumerator DoRecoveryBattle()
        {
            CurrentPhase = Phase.Recovery;
            _recoveryMash = new RecoveryMashState(
                _recoveryPressTarget,
                _recoveryPushPerPress,
                _recoverySuccessKnockback);
            _recoveryMash.Reset();

            if (_player != null)
            {
                _player.ReceptionEnabled = true;
                _player.ShootingEnabled = false;
                _player.MovementEnabled = false;
            }
            if (_ball != null) _ball.Detach();

            EnsureRecoveryHud();
            ShowRecoveryHud();
            CaptureRecoveryShakeTarget();

            Vector3 backDir = GetRecoveryBackDirection();
            Vector3 opponentStart = _opponentTransform != null ? _opponentTransform.position : Vector3.zero;
            float elapsed = 0f;

            while (elapsed < _recoveryDuration && !_recoveryMash.Succeeded)
            {
                elapsed += Time.deltaTime;

                bool held = _player != null && _player.ReceiveInputHeld;
                _recoveryMash.Tick(Time.deltaTime);
                float heldPush = held ? _recoveryHeldPush : 0f;
                Vector3 targetOpponentPos = opponentStart + backDir * (_recoveryMash.PersistentPush + heldPush);

                if (_opponentTransform != null)
                {
                    _opponentTransform.position = Vector3.Lerp(
                        _opponentTransform.position,
                        targetOpponentPos,
                        Time.deltaTime * _recoveryOpponentLerpSpeed);
                }

                UpdateContestedBall(backDir);
                UpdateRecoveryHud(elapsed / _recoveryDuration, held, false, false);
                ApplyRecoveryShake(Mathf.Clamp01(0.35f + _recoveryMash.PressPulse));
                yield return null;
            }

            if (_player != null) _player.ReceptionEnabled = false;
            RestoreRecoveryShakeTarget();

            if (_recoveryMash.Succeeded)
            {
                yield return PlayRecoverySuccess(backDir);
                _receiveQuality = Mathf.Max(_minimumPossessionReceiveQuality + 0.18f, 0.45f);
                RecoveryResolved?.Invoke(true);
                CurrentPhase = Phase.Possession;
            }
            else
            {
                yield return PlayRecoveryFailure(backDir);
                RecoveryResolved?.Invoke(false);
                ResolveRecoveryFailure();
            }

            HideRecoveryHud();
        }

        public bool HandleFootBallContact(FootContactData data)
        {
            if (!_acceptFootContacts || !_isMatchRunning)
                return false;

            FootContactRecorded?.Invoke(data);

            if (CurrentPhase == Phase.Recovery)
            {
                HandleRecoveryPress();
                return true;
            }

            if (CurrentPhase == Phase.Pass)
            {
                if (_receiveAttempted)
                    return true;

                _receiveAttempted = true;
                _receiveQuality = EvaluateFootReceiveQuality(data);
                ReceiveResolved?.Invoke(_receiveQuality, Mathf.Abs(_passProgress01 - _receivePerfect01), true);
                if (_player != null) _player.ReceptionEnabled = false;
                _receptionPrompt?.ShowReceiveFeedback(_receiveQuality, true, 1.2f);
                _receptionTargetIndicator?.ShowFeedback(_receiveQuality, true);
                Debug.Log($"[MatchFlow] Foot receive ({data.Foot}) quality={_receiveQuality:0.00} speed={data.ContactSpeed:0.00} accuracy={data.Accuracy01:0.00}");
                return true;
            }

            if (CurrentPhase == Phase.Possession)
            {
                Vector3 direction = BuildFootShotDirection(data);
                if (!_requireRightTriggerForFootShot || data.ShootIntentHeld)
                {
                    float passAimDot = 1f;
                    float passArrivalDistance = float.MaxValue;
                    if (_enableTeammatePassByAim)
                        IsAimedAtTeammate(direction, out passAimDot, out passArrivalDistance);
                    BeginPassJudgement(data.Power01, passAimDot);
                    ShotAttempted?.Invoke(data.Power01, direction);
                    Debug.Log($"[MatchFlow] Foot pass intent ({data.Foot}) power={data.Power01:0.00} speed={data.ContactSpeed:0.00} aimDot={passAimDot:0.00} expectedArrival={passArrivalDistance:0.00}");
                }

                return false;
            }

            return false;
        }

        public bool TryAssistPhysicalShotDirection(FootContactData data, Vector3 rawDirection, out Vector3 assistedDirection, out float assist01)
        {
            assistedDirection = rawDirection;
            assist01 = 0f;

            if (!_enableInstepShotAssist || !_isMatchRunning || _rallyResolved)
                return false;
            if (CurrentPhase != Phase.Possession || _ball == null)
                return false;

            Vector3 flatRaw = Flatten(rawDirection);
            if (flatRaw.sqrMagnitude < 0.0001f)
                return false;

            Vector3 goalDirection = Flatten(GetOpponentGoalAimPoint() - _ball.transform.position);
            if (goalDirection.sqrMagnitude < 0.0001f)
                goalDirection = GetAttackForward();
            goalDirection.Normalize();

            float rawToGoalDot = Vector3.Dot(flatRaw.normalized, goalDirection);
            float minDot = Mathf.Cos(_instepShotAssistMaxAngle * Mathf.Deg2Rad);
            float angleScore = Mathf.InverseLerp(minDot, 1f, rawToGoalDot);
            if (angleScore <= 0f)
                return false;

            float powerScore = Mathf.InverseLerp(_instepShotAssistMinPower, _instepShotAssistFullPower, data.Power01);
            if (powerScore <= 0f)
                return false;

            float techniqueScore = EvaluateInstepShotTechnique(data, goalDirection);
            if (techniqueScore <= 0f)
                return false;

            assist01 = Mathf.Clamp01(_instepShotAssistMax * angleScore * powerScore * techniqueScore);
            if (assist01 <= 0f)
                return false;

            Vector3 flatAssisted = Vector3.Slerp(flatRaw.normalized, goalDirection, assist01);
            assistedDirection = new Vector3(flatAssisted.x, rawDirection.y, flatAssisted.z);
            if (assistedDirection.sqrMagnitude < 0.0001f)
                assistedDirection = rawDirection;
            assistedDirection.Normalize();
            return true;
        }

        private float EvaluateInstepShotTechnique(FootContactData data, Vector3 goalDirection)
        {
            string zone = data.ContactZone ?? string.Empty;
            float zoneScore = 0.2f;
            if (zone.IndexOf("Instep", StringComparison.OrdinalIgnoreCase) >= 0)
                zoneScore = 1f;
            else if (zone.IndexOf("Toe", StringComparison.OrdinalIgnoreCase) >= 0)
                zoneScore = 0.45f;
            else if (zone.IndexOf("FootBox", StringComparison.OrdinalIgnoreCase) >= 0)
                zoneScore = 0.35f;
            else if (zone.IndexOf("Sole", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     zone.IndexOf("Shin", StringComparison.OrdinalIgnoreCase) >= 0)
                zoneScore = 0.05f;

            Vector3 face = Flatten(data.FootForward);
            Vector3 swing = Flatten(data.SwingDirection);
            if (face.sqrMagnitude < 0.0001f || swing.sqrMagnitude < 0.0001f)
                return zoneScore;

            face.Normalize();
            swing.Normalize();
            Vector3 right = GetAttackRight();
            float diagonalFaceScore = Mathf.InverseLerp(0.22f, 0.68f, Mathf.Abs(Vector3.Dot(face, right)));
            float swingGoalScore = Mathf.InverseLerp(0.05f, 0.72f, Vector3.Dot(swing, goalDirection));
            float diagonalInstepScore = diagonalFaceScore * swingGoalScore;

            return Mathf.Clamp01(Mathf.Max(zoneScore, diagonalInstepScore * 0.8f));
        }

        private Vector3 GetOpponentGoalAimPoint()
        {
            if (_fieldBuilder == null)
                _fieldBuilder = FindFirstObjectByType<FieldBuilder>(FindObjectsInactive.Include);

            float y = Mathf.Clamp(_opponentGoalHeight * 0.45f, 0.25f, 1.0f);
            if (_fieldBuilder != null)
                return _fieldBuilder.transform.TransformPoint(new Vector3(0f, y, GetFieldHalfLength() + _opponentGoalDepth * 0.25f));

            if (_goalTargetIn != null)
                return _goalTargetIn.position;

            return _goalTargetInPos;
        }

        private void HandleReceiveAttempt(Vector3 direction)
        {
            if (!_isMatchRunning) return;
            if (CurrentPhase == Phase.Recovery)
            {
                HandleRecoveryPress();
                return;
            }

            if (CurrentPhase != Phase.Pass) return;
            if (_receiveAttempted) return;

            _receiveAttempted = true;
            _receiveQuality = EvaluateReceiveQuality(direction);
            ReceiveResolved?.Invoke(_receiveQuality, Mathf.Abs(_passProgress01 - _receivePerfect01), false);
            if (_player != null) _player.ReceptionEnabled = false;
            _receptionPrompt?.ShowReceiveFeedback(_receiveQuality, true, 1.2f);
            _receptionTargetIndicator?.ShowFeedback(_receiveQuality, true);
            Debug.Log($"[MatchFlow] Receive quality: {_receiveQuality:0.00}");
        }

        private float EvaluateFootReceiveQuality(FootContactData data)
        {
            Vector3 incoming = Vector3.zero;
            if (_playerTransform != null && _ball != null)
                incoming = Flatten(_ball.transform.position - _playerTransform.position);
            if (incoming.sqrMagnitude < 0.0001f)
                incoming = Flatten(_currentPassStart - _currentPassEnd);

            Vector3 footFacing = data.FootForward.sqrMagnitude > 0.0001f
                ? data.FootForward
                : (_playerTransform != null ? _playerTransform.forward : Vector3.forward);

            float timingAndFacing = ReceptionRules.EvaluateQuality(
                _passProgress01,
                footFacing,
                incoming,
                new ReceptionTuning(
                    _receiveWindowStart01,
                    _receivePerfect01,
                    _receiveWindowEnd01,
                    _receiveFacingFullScoreAngle,
                    _receiveFacingFailAngle));

            float speedScore = 1f - Mathf.InverseLerp(
                Mathf.Max(0.001f, _footReceivePerfectSpeed),
                Mathf.Max(_footReceivePerfectSpeed + 0.001f, _footReceiveFailSpeed),
                data.ContactSpeed);

            return Mathf.Clamp01(timingAndFacing * 0.62f + speedScore * 0.25f + data.Accuracy01 * 0.13f);
        }

        private Vector3 BuildFootShotDirection(FootContactData data)
        {
            Vector3 swing = Flatten(data.SwingDirection);
            Vector3 face = Flatten(data.FootForward);
            Vector3 aim = GetAttackForward();

            Vector3 contactDirection = swing.sqrMagnitude > 0.0001f
                ? swing.normalized
                : (face.sqrMagnitude > 0.0001f ? face.normalized : aim);

            Vector3 direction = Vector3.Slerp(aim, contactDirection, _footShotSwingDirectionWeight);
            direction.y = Mathf.Clamp(data.SwingDirection.y * 0.35f + _footShotLift, 0.02f, 0.45f);
            if (direction.sqrMagnitude < 0.0001f)
                direction = aim;
            direction.Normalize();
            return direction;
        }

        private bool IsAimedAtTeammate(Vector3 shotDirection, out float aimDot, out float arrivalDistance)
        {
            aimDot = -1f;
            arrivalDistance = float.MaxValue;
            if (_teammateTransform == null || _ball == null)
                return false;

            Vector3 start = _ball.transform.position;
            Vector3 toTeammate = Flatten(_teammateTransform.position - start);
            Vector3 flatShot = Flatten(shotDirection);
            if (toTeammate.sqrMagnitude < 0.0001f || flatShot.sqrMagnitude < 0.0001f)
                return false;

            Vector3 teammateDir = toTeammate.normalized;
            Vector3 shotDir = flatShot.normalized;
            aimDot = Vector3.Dot(shotDir, teammateDir);

            float projectedDistance = Mathf.Max(0f, Vector3.Dot(toTeammate, shotDir));
            Vector3 closestOnShotLine = start + shotDir * projectedDistance;
            arrivalDistance = Flatten(_teammateTransform.position - closestOnShotLine).magnitude;

            return aimDot >= _teammatePassAimDot || arrivalDistance <= _teammatePassArrivalRadius;
        }

        private void BeginPassJudgement(float power01, float aimDot)
        {
            if (!_isMatchRunning || CurrentPhase != Phase.Possession || _rallyResolved)
                return;
            if (power01 < _minimumFootShotPower)
                return;

            _passJudgementActive = true;
            _passJudgementPower01 = Mathf.Clamp01(power01);
            _passJudgementAimDot = Mathf.Clamp(aimDot, -1f, 1f);
            _passJudgementStartedAt = Time.time;
        }

        private void ResetPassJudgement()
        {
            _passJudgementActive = false;
            _passJudgementPower01 = 0f;
            _passJudgementAimDot = 1f;
            _passJudgementStartedAt = -999f;
        }

        private bool IsBallNearTeammate(out float distance)
        {
            distance = float.MaxValue;
            if (_ball == null || _teammateTransform == null)
                return false;

            distance = Flatten(_ball.transform.position - _teammateTransform.position).magnitude;
            return distance <= _teammatePassArrivalRadius;
        }

        private float GetBallHorizontalSpeed()
        {
            if (_ball == null)
                return 0f;

            Rigidbody body = _ball.GetComponent<Rigidbody>();
            if (body == null)
                return 0f;

            Vector3 velocity = body.linearVelocity;
            velocity.y = 0f;
            return velocity.magnitude;
        }

        private void ResolveTeammatePassArrival(float arrivalDistance)
        {
            if (!_isMatchRunning || CurrentPhase != Phase.Possession || _rallyResolved)
                return;

            float passPower01 = _passJudgementPower01;
            float passAimDot = _passJudgementAimDot;
            ResetPassJudgement();
            CurrentPhase = Phase.Shot;
            if (_player != null)
            {
                _player.ShootingEnabled = false;
                _player.MovementEnabled = false;
                _player.ReceptionEnabled = false;
            }
            if (_ball != null) _ball.Detach();

            float receiveBias = Mathf.Lerp(-_poorReceivePowerPenalty, _receivePowerBonus, _receiveQuality);
            float arrivalSpeed01 = Mathf.InverseLerp(0.6f, 5.5f, GetBallHorizontalSpeed());
            float aiPressure = _fieldAI != null ? _fieldAI.PassPressure01 : 0f;
            float aiSupport = _fieldAI != null ? _fieldAI.TeammateSupport01 : 0f;
            float effectivePower = Mathf.Clamp01(
                passPower01 * 0.55f +
                arrivalSpeed01 * 0.45f +
                receiveBias +
                aiSupport * 0.08f -
                aiPressure * 0.14f +
                UnityEngine.Random.Range(-_randomJitter, _randomJitter));
            _scorePanel?.SetFirstTouchContext(_receiveQuality, receiveBias);
            _receptionPrompt?.ShowShotBias(receiveBias);

            bool offTarget = arrivalDistance > _teammatePassArrivalRadius;
            if (offTarget || effectivePower >= _teammatePassFastPower)
            {
                StartShotRoutine(DoTeammateShot(GetMissTarget(), _shotMissedData));
                return;
            }

            bool pressureForcedTurnover = aiPressure > 0.82f && effectivePower < _teammatePassSlowPower + 0.10f;
            if (effectivePower < _teammatePassSlowPower || pressureForcedTurnover)
            {
                PlayInterceptedScenario();
                return;
            }

            float aiScoreMultiplier = Mathf.Lerp(0.72f, 1.12f, aiSupport) * Mathf.Lerp(1.05f, 0.76f, aiPressure);
            float scoreChance = Mathf.Clamp01(_teammatePassGoodScoreChance * Mathf.Clamp01(0.55f + passAimDot * 0.45f) * aiScoreMultiplier);
            bool scored = UnityEngine.Random.value <= scoreChance;
            StartShotRoutine(DoTeammateShot(scored ? GetScoreTarget() : GetMissTarget(), scored ? _scoreSuccessData : _shotMissedData));
        }

        private Vector3 GetScoreTarget()
        {
            return _goalTargetIn != null ? _goalTargetIn.position : _goalTargetInPos;
        }

        private Vector3 GetMissTarget()
        {
            return _goalTargetMiss != null ? _goalTargetMiss.position : _goalTargetMissPos;
        }

        private void StartShotRoutine(IEnumerator routine)
        {
            if (_shotRoutine != null)
                StopCoroutine(_shotRoutine);
            _shotRoutine = StartCoroutine(routine);
        }

        private void PlayInterceptedScenario(Transform origin = null)
        {
            if (_scenarioPlayer != null) _scenarioPlayer.SetOrigin(origin != null ? origin : _playerTransform);
            if (_scenarioTrigger != null) _scenarioTrigger.ForcePlay(0);
        }

        private void HandleRecoveryPress()
        {
            if (CurrentPhase != Phase.Recovery || _recoveryMash == null) return;
            _recoveryMash.RegisterPress();
        }

        private void ResolveRecoveryFailure()
        {
            CurrentPhase = Phase.Shot;
            if (_player != null)
            {
                _player.ShootingEnabled = false;
                _player.MovementEnabled = false;
                _player.ReceptionEnabled = false;
            }
            if (_ball != null) _ball.Detach();
            if (_scenarioPlayer != null) _scenarioPlayer.SetOrigin(_playerTransform);
            if (_scenarioTrigger != null) _scenarioTrigger.ForcePlay(0);
        }

        private void ResetReceptionState(float defaultQuality)
        {
            _passProgress01 = 0f;
            _receiveAttempted = false;
            _receiveQuality = Mathf.Clamp01(defaultQuality);
            _currentPassStart = Vector3.zero;
            _currentPassEnd = Vector3.zero;
        }

        private float EvaluateReceiveQuality(Vector3 playerFacing)
        {
            Vector3 incoming = Vector3.zero;
            if (_playerTransform != null && _ball != null)
                incoming = Flatten(_ball.transform.position - _playerTransform.position);
            if (incoming.sqrMagnitude < 0.0001f)
                incoming = Flatten(_currentPassStart - _currentPassEnd);

            return ReceptionRules.EvaluateQuality(
                _passProgress01,
                playerFacing,
                incoming,
                new ReceptionTuning(
                    _receiveWindowStart01,
                    _receivePerfect01,
                    _receiveWindowEnd01,
                    _receiveFacingFullScoreAngle,
                    _receiveFacingFailAngle));
        }

        private static Vector3 Flatten(Vector3 v)
        {
            return ReceptionRules.Flatten(v);
        }

        private Vector3 GetRecoveryBackDirection()
        {
            if (_opponentTransform != null && _playerTransform != null)
            {
                Vector3 away = Flatten(_opponentTransform.position - _playerTransform.position);
                if (away.sqrMagnitude > 0.0001f)
                    return away.normalized;
            }

            return GetAttackForward();
        }

        private void UpdateContestedBall(Vector3 backDir)
        {
            if (_ball == null || _playerTransform == null || _opponentTransform == null) return;

            Vector3 playerBallPos = _playerTransform.TransformPoint(_ballOffsetPlayer);
            Vector3 opponentBallPos = _opponentTransform.position + Vector3.up * 0.35f - backDir * 0.25f;
            int pressCount = _recoveryMash != null ? _recoveryMash.PressCount : 0;
            float pressPulse = _recoveryMash != null ? _recoveryMash.PressPulse : 0f;
            float win01 = Mathf.Clamp01((float)pressCount / Mathf.Max(1, _recoveryPressTarget));
            Vector3 target = Vector3.Lerp(opponentBallPos, playerBallPos, win01 * 0.82f);
            target.y += Mathf.Sin(Time.time * 24f) * 0.035f * pressPulse;
            _ball.transform.position = Vector3.Lerp(_ball.transform.position, target, Time.deltaTime * 12f);
        }

        private IEnumerator PlayRecoverySuccess(Vector3 backDir)
        {
            UpdateRecoveryHud(1f, false, true, false);

            Vector3 opponentFrom = _opponentTransform != null ? _opponentTransform.position : Vector3.zero;
            Vector3 opponentTo = opponentFrom + backDir * _recoverySuccessKnockback;
            Vector3 ballFrom = _ball != null ? _ball.transform.position : Vector3.zero;
            Vector3 ballTo = _playerTransform != null ? _playerTransform.TransformPoint(_ballOffsetPlayer) : ballFrom;

            float t = 0f;
            const float duration = 0.45f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                float eased = 1f - Mathf.Pow(1f - u, 3f);
                if (_opponentTransform != null)
                    _opponentTransform.position = Vector3.Lerp(opponentFrom, opponentTo, eased);
                if (_ball != null)
                    _ball.transform.position = Vector3.Lerp(ballFrom, ballTo, eased);
                ApplyRecoveryShake(1f - u);
                yield return null;
            }
            RestoreRecoveryShakeTarget();
        }

        private IEnumerator PlayRecoveryFailure(Vector3 backDir)
        {
            UpdateRecoveryHud(1f, false, false, true);

            Vector3 opponentFrom = _opponentTransform != null ? _opponentTransform.position : Vector3.zero;
            Vector3 opponentTo = opponentFrom - backDir * _recoveryFailSurgeDistance;
            Vector3 ballFrom = _ball != null ? _ball.transform.position : Vector3.zero;
            Vector3 ballTo = opponentTo + Vector3.up * 0.35f - backDir * 0.15f;

            float t = 0f;
            const float duration = 0.38f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                float eased = 1f - Mathf.Pow(1f - u, 2f);
                if (_opponentTransform != null)
                    _opponentTransform.position = Vector3.Lerp(opponentFrom, opponentTo, eased);
                if (_ball != null)
                    _ball.transform.position = Vector3.Lerp(ballFrom, ballTo, eased);
                ApplyRecoveryShake(1f);
                yield return null;
            }
            RestoreRecoveryShakeTarget();
            yield return new WaitForSeconds(0.15f);
        }

        private void CaptureRecoveryShakeTarget()
        {
            _recoveryShakeTarget = _fpsCamera != null ? _fpsCamera : null;
            if (_recoveryShakeTarget != null)
                _recoveryShakeBaseLocalPos = _recoveryShakeTarget.localPosition;
        }

        private void ApplyRecoveryShake(float intensity)
        {
            if (_recoveryShakeTarget == null) return;

            float z = Mathf.Sin(Time.unscaledTime * _recoveryShakeFrequency) * _recoveryShakeAmplitude * intensity;
            _recoveryShakeTarget.localPosition = _recoveryShakeBaseLocalPos + Vector3.forward * z;
        }

        private void RestoreRecoveryShakeTarget()
        {
            if (_recoveryShakeTarget != null)
                _recoveryShakeTarget.localPosition = _recoveryShakeBaseLocalPos;
            _recoveryShakeTarget = null;
        }

        private void EnsureRecoveryHud()
        {
            if (_recoveryHudRoot != null) return;

            var root = new GameObject("RecoveryMashHUD", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1200;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            root.AddComponent<GraphicRaycaster>();

            _recoveryHudGroup = root.AddComponent<CanvasGroup>();
            _recoveryHudGroup.alpha = 0f;
            _recoveryHudGroup.interactable = false;
            _recoveryHudGroup.blocksRaycasts = false;
            _recoveryHudRoot = root.GetComponent<RectTransform>();

            _recoveryBorderImages.Clear();
            _recoveryBorderImages.Add(CreateRecoveryImage("TopSpikeBorder", root.transform, new Color(1f, 0.08f, 0.02f, 0.35f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -34f), new Vector2(0f, 0f)));
            _recoveryBorderImages.Add(CreateRecoveryImage("BottomSpikeBorder", root.transform, new Color(1f, 0.08f, 0.02f, 0.35f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 34f)));
            _recoveryBorderImages.Add(CreateRecoveryImage("LeftSpikeBorder", root.transform, new Color(1f, 0.08f, 0.02f, 0.28f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(28f, 0f)));
            _recoveryBorderImages.Add(CreateRecoveryImage("RightSpikeBorder", root.transform, new Color(1f, 0.08f, 0.02f, 0.28f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-28f, 0f), new Vector2(0f, 0f)));

            _recoveryPromptLabel = CreateRecoveryText("RecoveryPrompt", root.transform, "MASH TO WIN THE BALL", 46, new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(900f, 80f));
            _recoveryMeterLabel = CreateRecoveryText("RecoveryMeter", root.transform, "0/0", 30, new Vector2(0.5f, 1f), new Vector2(0f, -282f), new Vector2(900f, 60f));

            var buttonGo = new GameObject("RecoveryButton", typeof(RectTransform), typeof(Image));
            buttonGo.transform.SetParent(root.transform, false);
            _recoveryButtonRect = buttonGo.GetComponent<RectTransform>();
            _recoveryButtonRect.anchorMin = new Vector2(0.5f, 1f);
            _recoveryButtonRect.anchorMax = new Vector2(0.5f, 1f);
            _recoveryButtonRect.sizeDelta = new Vector2(150f, 150f);
            _recoveryButtonRect.anchoredPosition = new Vector2(0f, -190f);
            var buttonImage = buttonGo.GetComponent<Image>();
            buttonImage.color = new Color(1f, 0.18f, 0.05f, 0.78f);
            buttonImage.raycastTarget = false;

            var buttonText = CreateRecoveryText("RecoveryButtonText", buttonGo.transform, "TAP", 40, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 90f));
            buttonText.color = Color.white;

            root.SetActive(false);
        }

        private Image CreateRecoveryImage(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private TextMeshProUGUI CreateRecoveryText(string name, Transform parent, string text, int fontSize, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private void ShowRecoveryHud()
        {
            if (_recoveryHudRoot == null) return;
            _recoveryHudRoot.gameObject.SetActive(true);
            if (_recoveryHudGroup != null) _recoveryHudGroup.alpha = 1f;
            UpdateRecoveryHud(0f, false, false, false);
        }

        private void HideRecoveryHud()
        {
            if (_recoveryHudGroup != null) _recoveryHudGroup.alpha = 0f;
            if (_recoveryHudRoot != null)
            {
                _recoveryHudRoot.localScale = Vector3.one;
                _recoveryHudRoot.gameObject.SetActive(false);
            }
        }

        private void UpdateRecoveryHud(float time01, bool held, bool success, bool failed)
        {
            if (_recoveryHudRoot == null) return;

            int pressCount = _recoveryMash != null ? _recoveryMash.PressCount : 0;
            float pressPulse = _recoveryMash != null ? _recoveryMash.PressPulse : 0f;
            float progress01 = Mathf.Clamp01((float)pressCount / Mathf.Max(1, _recoveryPressTarget));
            if (_recoveryPromptLabel != null)
            {
                _recoveryPromptLabel.text = success
                    ? "BALL WON"
                    : failed
                        ? "BALL LOST"
                        : "MASH TO WIN THE BALL";
            }
            if (_recoveryMeterLabel != null)
            {
                int filledBars = Mathf.Clamp(Mathf.RoundToInt(progress01 * 20f), 0, 20);
                string meter = new string('|', filledBars) + new string('.', 20 - filledBars);
                _recoveryMeterLabel.text = $"{pressCount}/{_recoveryPressTarget}  [{meter}]";
            }

            float pulse = 1f + pressPulse * 0.22f + (held ? 0.10f : 0f);
            if (_recoveryButtonRect != null)
                _recoveryButtonRect.localScale = Vector3.one * pulse;

            float borderAlpha = success ? 0.65f : failed ? 0.85f : 0.35f + Mathf.Sin(Time.unscaledTime * 28f) * 0.12f + pressPulse * 0.18f;
            for (int i = 0; i < _recoveryBorderImages.Count; i++)
            {
                if (_recoveryBorderImages[i] == null) continue;
                Color c = success ? new Color(0.1f, 0.95f, 0.35f, borderAlpha) : new Color(1f, 0.08f, 0.02f, borderAlpha);
                _recoveryBorderImages[i].color = c;
            }

            float scalePulse = 1f + Mathf.Sin(Time.unscaledTime * _recoveryShakeFrequency) * _recoveryShakeAmplitude * 0.35f;
            _recoveryHudRoot.localScale = Vector3.one * scalePulse;
        }

        private void HandlePlayerShot(float power01, Vector3 direction)
        {
            if (!_isMatchRunning || CurrentPhase != Phase.Possession) return;
            IsAimedAtTeammate(direction, out float aimDot, out _);
            BeginPassJudgement(power01, aimDot);
            ShotAttempted?.Invoke(power01, direction);
        }

        private IEnumerator DoTeammateShot(Vector3 targetWorld, Scenario panelData)
        {
            if (_ball != null && _teammateTransform != null)
            {
                Vector3 receivePos = _teammateTransform.position + new Vector3(0f, 0.3f, 0f) + _teammateTransform.forward * 0.4f;
                yield return ParabolicLerp(_ball.transform, _ball.transform.position, receivePos, _shotPassToTeammate, 0.8f);
            }

            if (_teammateTransform != null)
            {
                Vector3 toTarget = targetWorld - _teammateTransform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    Quaternion startRot = _teammateTransform.rotation;
                    Quaternion targetRot = Quaternion.LookRotation(toTarget);
                    float rt = 0f;
                    while (rt < _teammateAimDuration)
                    {
                        rt += Time.deltaTime;
                        _teammateTransform.rotation = Quaternion.Slerp(startRot, targetRot, Mathf.Clamp01(rt / _teammateAimDuration));
                        yield return null;
                    }
                    _teammateTransform.rotation = targetRot;
                }
            }
            yield return new WaitForSeconds(_teammateAimHold);

            Vector3 teammateStart = _teammateTransform != null ? _teammateTransform.position : Vector3.zero;
            Vector3 teammateEnd = _teammateTransform != null ? teammateStart + _teammateTransform.forward * _teammateRunDistance : Vector3.zero;
            Vector3 ballStart = _ball != null ? _ball.transform.position : Vector3.zero;

            float t = 0f;
            float runWindow = Mathf.Max(0.01f, _shotFlightTime * _teammateRunFraction);
            while (t < _shotFlightTime)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / _shotFlightTime);

                if (_teammateTransform != null)
                {
                    float runU = Mathf.Clamp01(t / runWindow);
                    _teammateTransform.position = Vector3.Lerp(teammateStart, teammateEnd, runU);
                }

                if (_ball != null)
                {
                    Vector3 pos = Vector3.Lerp(ballStart, targetWorld, u);
                    pos.y += _shotApex * 4f * u * (1f - u);
                    _ball.transform.position = pos;
                }
                yield return null;
            }
            if (_ball != null) _ball.transform.position = targetWorld;

            yield return new WaitForSeconds(_resultHoldDelay);
            if (_scorePanel != null && panelData != null) _scorePanel.Show(panelData);
            if (_scoreBoard != null && panelData != null) _scoreBoard.Record(panelData.outcome);

            if (_teammateCelebrates && panelData != null && panelData.outcome == ScenarioOutcome.Score && _teammateTransform != null)
                StartCoroutine(CelebrationBounce(_teammateTransform));

            _shotRoutine = null;
            HandleShotResolved();
            NotifyRoundResolved(panelData, null);
        }

        private IEnumerator CelebrationBounce(Transform npc)
        {
            Vector3 groundPos = npc.position;
            float bounceHeight = 0.3f;
            float bounceDuration = 0.25f;
            const int bounces = 3;
            for (int i = 0; i < bounces; i++)
            {
                float t = 0f;
                while (t < bounceDuration)
                {
                    t += Time.deltaTime;
                    float u = t / bounceDuration;
                    npc.position = groundPos + Vector3.up * (bounceHeight * 4f * u * (1f - u));
                    yield return null;
                }
                npc.position = groundPos;
            }
        }

        private IEnumerator ParabolicLerp(Transform target, Vector3 start, Vector3 end, float duration, float apex)
        {
            float u = 0f;
            while (u < duration)
            {
                u += Time.deltaTime;
                float p = Mathf.Clamp01(u / duration);
                Vector3 pos = Vector3.Lerp(start, end, p);
                pos.y += apex * 4f * p * (1f - p);
                target.position = pos;
                yield return null;
            }
            target.position = end;
        }

        private void HandleShotResolved()
        {
            if (CurrentPhase != Phase.Shot) return;
            _rallyResolved = true;
            CurrentPhase = Phase.Score;
        }

        private void HandleScenarioComplete(Scenario s)
        {
            HandleShotResolved();
            NotifyRoundResolved(s, null);
        }

        private void NotifyRoundResolved(Scenario scenario, string outcomeLabelOverride)
        {
            RoundResolved?.Invoke(scenario, outcomeLabelOverride);
        }
    }

    [DisallowMultipleComponent]
    public class MatchBoundaryWall : MonoBehaviour
    {
        private MatchFlowController _owner;
        private MatchFlowController.BoundaryExitKind _exitKind = MatchFlowController.BoundaryExitKind.Unknown;

        public void Configure(MatchFlowController owner, MatchFlowController.BoundaryExitKind exitKind)
        {
            _owner = owner;
            _exitKind = exitKind;
        }

        private void OnCollisionEnter(Collision collision)
        {
            Notify(collision.rigidbody != null ? collision.rigidbody.GetComponent<BallController>() : null);
        }

        private void OnTriggerEnter(Collider other)
        {
            Notify(other != null ? other.GetComponentInParent<BallController>() : null);
        }

        private void Notify(BallController ball)
        {
            if (_owner != null && ball != null)
                _owner.NotifyBoundaryHit(ball, _exitKind);
        }
    }

    [DisallowMultipleComponent]
    public class MatchGoalTrigger : MonoBehaviour
    {
        private MatchFlowController _owner;

        public void Configure(MatchFlowController owner)
        {
            _owner = owner;
        }

        private void OnTriggerEnter(Collider other)
        {
            Notify(other != null ? other.GetComponentInParent<BallController>() : null);
        }

        private void Notify(BallController ball)
        {
            if (_owner != null && ball != null)
                _owner.NotifyOpponentGoal(ball);
        }
    }
}
