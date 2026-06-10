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
        [SerializeField, Range(0.5f, 1f)] private float _scoreThreshold = 0.7f;
        [SerializeField, Range(0.1f, 0.7f)] private float _missThreshold = 0.4f;
        [SerializeField, Range(0f, 0.3f)] private float _randomJitter = 0.10f;

        [Header("Teammate Shot Targets (world space)")]
        [SerializeField] private Transform _goalTargetIn;
        [SerializeField] private Transform _goalTargetMiss;
        [SerializeField] private Vector3 _goalTargetInPos = new(0f, 0.8f, 8.9f);
        [SerializeField] private Vector3 _goalTargetMissPos = new(3.5f, 0.5f, 8.6f);

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

        public Phase CurrentPhase { get; private set; } = Phase.Idle;

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
        private readonly System.Collections.Generic.List<Image> _recoveryBorderImages = new();
        private readonly System.Collections.Generic.List<Transform> _backgroundNpcTransforms = new();
        private static readonly Color BlueTeamColor = new(0.1f, 0.3f, 0.9f, 1f);
        private static readonly Color RedTeamColor = new(0.9f, 0.1f, 0.1f, 1f);

        void Start()
        {
            ApplyDemoOverrides();
            AutoResolveRefs();
            EnsureFarGoalTargets();
            EnsureDemoScenePolish();

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

            CurrentPhase = Phase.Idle;
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
            if (_ball != null && _robotTransform != null)
                _ball.AttachTo(_robotTransform, _ballOffsetRobot);
        }

        void Update()
        {
            if (_menuAction == null || !_menuAction.WasPressedThisFrame())
            {
                UpdateAttackArrow();
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
            _ball.transform.position = endPos;
            _passProgress01 = 1f;
            if (_player != null) _player.ReceptionEnabled = false;
            if (!_receiveAttempted)
            {
                _receiveQuality = 0.12f;
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
                CurrentPhase = Phase.Possession;
            }
            else
            {
                yield return PlayRecoveryFailure(backDir);
                ResolveRecoveryFailure();
            }

            HideRecoveryHud();
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
            if (_player != null) _player.ReceptionEnabled = false;
            _receptionPrompt?.ShowReceiveFeedback(_receiveQuality, true, 1.2f);
            _receptionTargetIndicator?.ShowFeedback(_receiveQuality, true);
            Debug.Log($"[MatchFlow] Receive quality: {_receiveQuality:0.00}");
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

            CurrentPhase = Phase.Shot;
            if (_player != null)
            {
                _player.ShootingEnabled = false;
                _player.MovementEnabled = false;
                _player.ReceptionEnabled = false;
            }
            if (_ball != null) _ball.Detach();

            float receiveBias = Mathf.Lerp(-_poorReceivePowerPenalty, _receivePowerBonus, _receiveQuality);
            float effectivePower = Mathf.Clamp01(power01 + receiveBias + UnityEngine.Random.Range(-_randomJitter, _randomJitter));
            _scorePanel?.SetFirstTouchContext(_receiveQuality, receiveBias);
            _receptionPrompt?.ShowShotBias(receiveBias);

            if (effectivePower >= _scoreThreshold)
            {
                Vector3 target = _goalTargetIn != null ? _goalTargetIn.position : _goalTargetInPos;
                StartCoroutine(DoTeammateShot(target, _scoreSuccessData));
            }
            else if (effectivePower >= _missThreshold)
            {
                Vector3 target = _goalTargetMiss != null ? _goalTargetMiss.position : _goalTargetMissPos;
                StartCoroutine(DoTeammateShot(target, _shotMissedData));
            }
            else
            {
                if (_scenarioPlayer != null) _scenarioPlayer.SetOrigin(_playerTransform);
                if (_scenarioTrigger != null) _scenarioTrigger.ForcePlay(0);
            }
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

            HandleShotResolved();
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
            CurrentPhase = Phase.Score;
        }

        private void HandleScenarioComplete(Scenario s)
        {
            HandleShotResolved();
        }
    }
}
