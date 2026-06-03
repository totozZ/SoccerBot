// MatchFlowController.cs — One-shot match loop driver.

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SoccerBot
{
    public class MatchFlowController : MonoBehaviour
    {
        public enum Phase { Idle, Setup, Pass, Possession, Shot, Score, Cooldown }

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

        [Header("Power Routing")]
        [SerializeField, Range(0.5f, 1f)] private float _scoreThreshold = 0.7f;
        [SerializeField, Range(0.1f, 0.7f)] private float _missThreshold = 0.4f;
        [SerializeField, Range(0f, 0.3f)] private float _randomJitter = 0.10f;

        [Header("Teammate Shot Targets (world space)")]
        [SerializeField] private Transform _goalTargetIn;
        [SerializeField] private Transform _goalTargetMiss;
        [SerializeField] private Vector3 _goalTargetInPos = new(0f, 0.8f, -9.5f);
        [SerializeField] private Vector3 _goalTargetMissPos = new(3.5f, 0.5f, -9.5f);

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

        [Header("Whistle")]
        [SerializeField] private AudioSource _whistleSource;

        [Header("Score Display Data")]
        [SerializeField] private Scenario _scoreSuccessData;
        [SerializeField] private Scenario _shotMissedData;

        public Phase CurrentPhase { get; private set; } = Phase.Idle;

        private Coroutine _loop;
        private bool _isMatchRunning;
        private InputAction _menuAction;
        private MainMenuPanel _mainMenu;

        void Start()
        {
            AutoResolveRefs();

            if (_player != null) _player.OnShoot += HandlePlayerShot;
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
            if (_player != null) _player.OnShoot -= HandlePlayerShot;
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
            }

            if (_hudRoot != null) _hudRoot.SetActive(false);
            _scorePanel?.HideImmediate();
            if (_ball != null && _robotTransform != null)
                _ball.AttachTo(_robotTransform, _ballOffsetRobot);
        }

        void Update()
        {
            if (_menuAction == null || !_menuAction.WasPressedThisFrame()) return;
            if (_mainMenu == null) return;

            bool menuOpen = _mainMenu.gameObject.activeSelf && _mainMenu.gameObject.activeInHierarchy;
            if (menuOpen) return;

            ResetForMenu();
            _mainMenu.ShowMenu();
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
            }
            if (_teammateTransform != null && _playerTransform != null)
            {
                _teammateTransform.gameObject.SetActive(true);
                _teammateTransform.position = _playerTransform.TransformPoint(_teammateSetupOffset);
                _teammateTransform.rotation = _playerTransform.rotation;
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

            float t = 0f;
            while (t < _passFlightTime)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / _passFlightTime);
                Vector3 pos = Vector3.Lerp(startPos, endPos, u);
                pos.y += _passApex * 4f * u * (1f - u);
                _ball.transform.position = pos;
                yield return null;
            }
            _ball.transform.position = endPos;
        }

        private IEnumerator DoPossession()
        {
            CurrentPhase = Phase.Possession;
            if (_ball != null && _playerTransform != null)
                _ball.AttachTo(_playerTransform, _ballOffsetPlayer);
            if (_player != null)
            {
                _player.ShootingEnabled = true;
                _player.MovementEnabled = true;
            }

            while (CurrentPhase == Phase.Possession)
            {
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

        private void HandlePlayerShot(float power01, Vector3 direction)
        {
            if (!_isMatchRunning || CurrentPhase != Phase.Possession) return;

            CurrentPhase = Phase.Shot;
            if (_player != null)
            {
                _player.ShootingEnabled = false;
                _player.MovementEnabled = false;
            }
            if (_ball != null) _ball.Detach();

            float effectivePower = Mathf.Clamp01(power01 + UnityEngine.Random.Range(-_randomJitter, _randomJitter));

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
