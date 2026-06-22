using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SoccerBot
{
    // Keyboard-first arena runtime; installed without scene mutations by GameplayModeBootstrap.
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class ArenaAttackController : MonoBehaviour
    {
        public event Action<ArenaRoundState> RoundStateChanged;
        public event Action<int> ScoreChanged;

        [Header("Session")]
        [SerializeField] private float _sessionDuration = 90f;
        [SerializeField] private float _resetDelay = 1.5f;
        [SerializeField] private float _serveSettleDelay = 0.55f;
        [SerializeField] private float _opponentControlDuration = 0.8f;
        [SerializeField] private float _stuckDuration = 2.5f;
        [SerializeField] private float _stuckSpeed = 0.08f;

        [Header("Arena")]
        [SerializeField] private float _wallHeight = 2.2f;
        [SerializeField] private float _wallThickness = 0.18f;
        [SerializeField] private float _wallBounce = 0.7f;
        [SerializeField] private float _goalWidth = 3.5f;
        [SerializeField] private float _goalHeight = 1.6f;

        [Header("AI")]
        [SerializeField] private float _teammateSpeed = 2.5f;
        [SerializeField] private float _opponentSpeed = 2.35f;
        [SerializeField] private float _goalkeeperSpeed = 2.8f;
        [SerializeField] private float _aiActionCooldown = 0.75f;

        public ArenaRoundState State { get; private set; } = ArenaRoundState.Resetting;
        public int Score { get; private set; }
        public float RemainingSeconds => _clock != null ? _clock.RemainingSeconds : _sessionDuration;
        public PossessionOwner Possession => _ballMotor != null ? _ballMotor.Owner : PossessionOwner.Free;

        private FieldBuilder _field;
        private BallController _ball;
        private ArenaBallMotor _ballMotor;
        private ArenaPlayerController _playerController;
        private Transform _player;
        private Transform _teammate;
        private Transform _opponent;
        private Transform _goalkeeper;
        private Transform _robot;
        private Transform _goalTarget;
        private ArenaSessionClock _clock;
        private Coroutine _resetRoutine;
        private float _opponentControlTimer;
        private float _stuckTimer;
        private float _teammateActionReadyAt;
        private float _opponentActionReadyAt;
        private float _goalkeeperActionReadyAt;
        private bool _initialized;
        private bool _showDebug = true;
        private string _roundMessage = "ARENA ATTACK";
        private PhysicsMaterial _wallMaterial;
        private Material _wallVisualMaterial;
        private NpcAnimationPresenter _goalkeeperAnimation;
        private GUIStyle _titleStyle;
        private GUIStyle _hudStyle;
        private GUIStyle _smallStyle;

        private IEnumerator Start()
        {
            // Let FieldBuilder and BallController finish their own Start methods first.
            yield return null;
            ResolveSceneReferences();
            if (_field == null || _ball == null || _player == null)
            {
                Debug.LogError("[Arena] Missing Field, Ball, or Player. Arena mode cannot start.");
                enabled = false;
                yield break;
            }

            DisableTrainingPresentation();
            ConfigureActors();
            BuildArenaBoundary();
            ConfigurePlayer();
            ConfigureBall();
            ConfigureNpcAnimation();

            _clock = new ArenaSessionClock(_sessionDuration);
            _initialized = true;
            BeginReset("GET READY", true);
            Debug.Log($"[Arena] Started profile={GameplayModeBootstrap.CurrentProfile} duration={_sessionDuration:0}s");
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            if (_playerController != null)
                _playerController.BallActionRequested -= HandlePlayerBallAction;
            if (_wallMaterial != null)
                Destroy(_wallMaterial);
            if (_wallVisualMaterial != null)
                Destroy(_wallVisualMaterial);
        }

        private void Update()
        {
            if (!_initialized)
                return;

            if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
                _showDebug = !_showDebug;

            bool paused = _playerController != null && _playerController.Paused;
            Time.timeScale = paused ? 0f : 1f;
            if (paused)
                return;

            if (State == ArenaRoundState.Live)
            {
                if (_clock.Tick(Time.deltaTime, State))
                {
                    FinishSession();
                    return;
                }

                TickAI();
                MonitorRoundResolution();
            }
        }

        private void ResolveSceneReferences()
        {
            _field = FindFirstObjectByType<FieldBuilder>(FindObjectsInactive.Include);
            _ball = FindFirstObjectByType<BallController>(FindObjectsInactive.Include);
            _player = FindNamedTransform("Player");
            _teammate = FindNamedTransform("Teammate");
            _opponent = FindNamedTransform("Opponent");
            _robot = FindNamedTransform("Robot");
            _goalkeeper = FindNamedTransform("ArenaGoalkeeper") ?? FindNamedTransform("Goalkeeper");
        }

        private void DisableTrainingPresentation()
        {
            MainMenuPanel menu = FindFirstObjectByType<MainMenuPanel>(FindObjectsInactive.Include);
            if (menu != null)
                menu.gameObject.SetActive(false);
            ScorePanel scorePanel = FindFirstObjectByType<ScorePanel>(FindObjectsInactive.Include);
            if (scorePanel != null)
                scorePanel.gameObject.SetActive(false);

            GameObject oldBoundary = GameObject.Find("MatchFlowBoundary");
            if (oldBoundary != null)
                oldBoundary.SetActive(false);
        }

        private void ConfigureActors()
        {
            if (_teammate == null)
                _teammate = CreateSimpleActor("Teammate", new Color(0.15f, 0.35f, 0.95f));
            if (_opponent == null)
                _opponent = CreateSimpleActor("Opponent", new Color(0.9f, 0.15f, 0.15f));
            if (_goalkeeper == null)
                _goalkeeper = CreateSimpleActor("ArenaGoalkeeper", new Color(0.95f, 0.7f, 0.1f));
            if (_robot == null)
                _robot = CreateSimpleActor("Robot", new Color(0.2f, 0.2f, 0.25f), 0.65f);

            EnsurePlayerVisual();

            float halfLength = _field._halfLength;
            bool vr = GameplayModeBootstrap.CurrentProfile == ControlProfile.VrStriker ||
                      GameplayModeBootstrap.CurrentProfile == ControlProfile.XrSimulator;
            SetFieldLocalPosition(_robot, new Vector3(0f, 0f, -halfLength + 1.05f));
            SetFieldLocalPosition(_player, new Vector3(0f, 0f, vr ? halfLength * 0.34f : -halfLength * 0.36f));
            SetFieldLocalPosition(_teammate, new Vector3(-2.1f, 0f, -0.4f));
            SetFieldLocalPosition(_opponent, new Vector3(1.45f, 0f, 1.65f));
            SetFieldLocalPosition(_goalkeeper, new Vector3(0f, 0f, halfLength - 0.78f));

            GameObject target = new GameObject("ArenaGoalTarget");
            target.transform.SetParent(_field.transform, false);
            target.transform.localPosition = new Vector3(0f, 0.75f, halfLength + 0.15f);
            _goalTarget = target.transform;
        }

        private void ConfigurePlayer()
        {
#if UNITY_EDITOR
            EnsureXrDeviceSimulator();
#endif
            _playerController = _player.GetComponent<ArenaPlayerController>();
            if (_playerController == null)
                _playerController = _player.gameObject.AddComponent<ArenaPlayerController>();
            _playerController.Configure(GameplayModeBootstrap.CurrentProfile, _field, _teammate, _goalTarget);
            _playerController.GameplayEnabled = false;
            _playerController.BallActionRequested -= HandlePlayerBallAction;
            _playerController.BallActionRequested += HandlePlayerBallAction;
        }

#if UNITY_EDITOR
        private static void EnsureXrDeviceSimulator()
        {
            if (GameplayModeBootstrap.CurrentProfile != ControlProfile.XrSimulator)
                return;
            if (GameObject.Find("Arena XR Device Simulator") != null)
                return;

            const string prefabPath = "Assets/Samples/XR Interaction Toolkit/3.4.1/XR Device Simulator/XR Device Simulator.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[Arena] XR Device Simulator prefab not found at {prefabPath}.");
                return;
            }

            GameObject simulator = Instantiate(prefab);
            simulator.name = "Arena XR Device Simulator";
            Debug.Log("[Arena] XR Device Simulator instantiated for no-headset smoke testing.");
        }
#endif

        private void ConfigureBall()
        {
            _ballMotor = _ball.GetComponent<ArenaBallMotor>();
            if (_ballMotor == null)
                _ballMotor = _ball.gameObject.AddComponent<ArenaBallMotor>();
            _ballMotor.Configure(_player, _teammate, _opponent, _goalkeeper, _goalTarget);
            _ballMotor.SetLive(false);
        }

        private void ConfigureNpcAnimation()
        {
            EnsureAnimationPresenter(_player);
            EnsureAnimationPresenter(_teammate);
            EnsureAnimationPresenter(_opponent);
            _goalkeeperAnimation = EnsureAnimationPresenter(_goalkeeper);
        }

        private void BuildArenaBoundary()
        {
            GameObject rootObject = new GameObject("ArenaBoundary");
            Transform root = rootObject.transform;
            root.SetPositionAndRotation(_field.transform.position, _field.transform.rotation);

            _wallMaterial = new PhysicsMaterial("Arena Rebound")
            {
                dynamicFriction = 0.04f,
                staticFriction = 0.04f,
                bounciness = Mathf.Clamp01(_wallBounce),
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader != null)
            {
                _wallVisualMaterial = new Material(shader);
                _wallVisualMaterial.color = new Color(0.04f, 0.28f, 0.42f, 1f);
            }

            float halfWidth = _field._halfWidth;
            float halfLength = _field._halfLength;
            float height = Mathf.Max(0.5f, _wallHeight);
            float thickness = Mathf.Max(0.05f, _wallThickness);
            CreateWall(root, "LeftBoard", new Vector3(-halfWidth - thickness * 0.5f, height * 0.5f, 0f), new Vector3(thickness, height, halfLength * 2f + thickness));
            CreateWall(root, "RightBoard", new Vector3(halfWidth + thickness * 0.5f, height * 0.5f, 0f), new Vector3(thickness, height, halfLength * 2f + thickness));
            CreateWall(root, "BackBoard", new Vector3(0f, height * 0.5f, -halfLength - thickness * 0.5f), new Vector3(halfWidth * 2f + thickness, height, thickness));

            float gap = Mathf.Clamp(_goalWidth, 0.5f, halfWidth * 2f - thickness * 2f);
            float segmentWidth = (halfWidth * 2f - gap) * 0.5f;
            float centerOffset = gap * 0.5f + segmentWidth * 0.5f;
            CreateWall(root, "FrontBoardLeft", new Vector3(-centerOffset, height * 0.5f, halfLength + thickness * 0.5f), new Vector3(segmentWidth, height, thickness));
            CreateWall(root, "FrontBoardRight", new Vector3(centerOffset, height * 0.5f, halfLength + thickness * 0.5f), new Vector3(segmentWidth, height, thickness));

            GameObject triggerObject = new GameObject("ArenaGoalTrigger");
            triggerObject.transform.SetParent(root, false);
            triggerObject.transform.localPosition = new Vector3(0f, _goalHeight * 0.5f, halfLength + 0.32f);
            var trigger = triggerObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(gap, _goalHeight, 0.7f);
            triggerObject.AddComponent<ArenaGoalTrigger>().Configure(this, _ball);
        }

        private void CreateWall(Transform parent, string name, Vector3 localPosition, Vector3 localScale)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.layer = 2;
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = localPosition;
            wall.transform.localScale = localScale;
            Collider collider = wall.GetComponent<Collider>();
            if (collider != null)
                collider.sharedMaterial = _wallMaterial;
            Renderer renderer = wall.GetComponent<Renderer>();
            if (renderer != null && _wallVisualMaterial != null)
                renderer.sharedMaterial = _wallVisualMaterial;
            wall.AddComponent<ArenaBoundaryWall>().Configure(this, _ball);
        }

        public void NotifyGoal(BallController ball)
        {
            if (State != ArenaRoundState.Live || ball == null || ball != _ball)
                return;
            Score++;
            ScoreChanged?.Invoke(Score);
            _roundMessage = "GOAL!";
            NpcAnimationPresenter teammateAnimation = _teammate != null ? _teammate.GetComponent<NpcAnimationPresenter>() : null;
            teammateAnimation?.TriggerCelebrate();
            BeginReset("GOAL +1", false);
        }

        public void NotifyWallHit(BallController ball)
        {
            if (State == ArenaRoundState.Live && ball == _ball)
                _roundMessage = "WALL REBOUND";
        }

        private void HandlePlayerBallAction(BallActionRequest request)
        {
            if (_ballMotor != null)
                _ballMotor.Execute(request);
        }

        private void TickAI()
        {
            if (_ballMotor == null || _ballMotor.Body == null)
                return;

            Vector3 ballPosition = _ball.transform.position;
            TickTeammate(ballPosition);
            TickOpponent(ballPosition);
            TickGoalkeeper(ballPosition);
        }

        private void TickTeammate(Vector3 ballPosition)
        {
            if (_teammate == null || _player == null)
                return;

            Vector3 support = _player.position + FieldRight() * -2.2f + FieldForward() * 2.7f;
            support = ClampActorPosition(support, 0.7f);
            bool chasePass = _ballMotor.Owner == PossessionOwner.Teammate ||
                             (FlatDistance(_teammate.position, ballPosition) < 2.2f && _ballMotor.Owner == PossessionOwner.Free);
            MoveActor(_teammate, chasePass ? ballPosition : support, _teammateSpeed, ballPosition);

            if (Time.time < _teammateActionReadyAt || FlatDistance(_teammate.position, ballPosition) > 1.1f)
                return;
            if (_ballMotor.Body.linearVelocity.magnitude > 7f)
                return;

            _teammateActionReadyAt = Time.time + _aiActionCooldown;
            Vector3 local = _field.transform.InverseTransformPoint(_teammate.position);
            bool shoot = local.z > _field._halfLength * 0.28f;
            Vector3 direction = shoot
                ? _goalTarget.position - ballPosition
                : _player.position - ballPosition;
            var request = new BallActionRequest(
                shoot ? BallActionKind.Shot : BallActionKind.Pass,
                BallActionSource.AI,
                _teammate,
                direction,
                shoot ? 0.62f : 0.45f);
            _ballMotor.Execute(request);
        }

        private void TickOpponent(Vector3 ballPosition)
        {
            if (_opponent == null)
                return;

            MoveActor(_opponent, ClampActorPosition(ballPosition, 0.6f), _opponentSpeed, ballPosition);
            if (Time.time < _opponentActionReadyAt || FlatDistance(_opponent.position, ballPosition) > 0.9f)
                return;
            if (_ballMotor.Body.linearVelocity.magnitude > 6.5f)
                return;

            _opponentActionReadyAt = Time.time + _aiActionCooldown;
            Vector3 clearDirection = -FieldForward() + FieldRight() * Mathf.Sign(_opponent.position.x + 0.01f) * 0.25f;
            _ballMotor.Execute(new BallActionRequest(
                BallActionKind.Tackle,
                BallActionSource.AI,
                _opponent,
                clearDirection,
                1f));
        }

        private void TickGoalkeeper(Vector3 ballPosition)
        {
            if (_goalkeeper == null)
                return;

            Vector3 localBall = _field.transform.InverseTransformPoint(ballPosition);
            Vector3 targetLocal = new Vector3(
                Mathf.Clamp(localBall.x, -_goalWidth * 0.38f, _goalWidth * 0.38f),
                0f,
                _field._halfLength - 0.78f);
            Vector3 target = _field.transform.TransformPoint(targetLocal);
            MoveActor(_goalkeeper, target, _goalkeeperSpeed, ballPosition);

            if (Time.time < _goalkeeperActionReadyAt || FlatDistance(_goalkeeper.position, ballPosition) > 1.2f)
                return;
            Vector3 velocity = _ballMotor.Body.linearVelocity;
            if (velocity.magnitude < 1.2f || Vector3.Dot(velocity.normalized, FieldForward()) < 0.15f)
                return;

            _goalkeeperActionReadyAt = Time.time + _aiActionCooldown;
            Vector3 deflect = -FieldForward() + FieldRight() * Mathf.Sign(localBall.x + 0.01f) * 0.65f;
            _ballMotor.Body.linearVelocity *= 0.2f;
            _ballMotor.Body.AddForce((deflect.normalized + Vector3.up * 0.12f).normalized * 4.4f, ForceMode.Impulse);
            _goalkeeperAnimation?.TriggerSave(ballPosition - _goalkeeper.position);
            _roundMessage = "KEEPER DEFLECT";
        }

        private void MonitorRoundResolution()
        {
            if (_ball == null || _ballMotor == null)
                return;

            if (_ball.transform.position.y < -0.75f)
            {
                BeginReset("BALL LOST", false);
                return;
            }

            Vector3 local = _field.transform.InverseTransformPoint(_ball.transform.position);
            if (Mathf.Abs(local.x) > _field._halfWidth + 2f || Mathf.Abs(local.z) > _field._halfLength + 2f)
            {
                BeginReset("BALL ESCAPED", false);
                return;
            }

            bool opponentOwns = _ballMotor.Owner == PossessionOwner.Opponent ||
                                 _ballMotor.Owner == PossessionOwner.Goalkeeper;
            _opponentControlTimer = opponentOwns
                ? _opponentControlTimer + Time.deltaTime
                : 0f;
            if (_opponentControlTimer >= _opponentControlDuration)
            {
                BeginReset("TURNOVER", false);
                return;
            }

            bool nobodyNear = _ballMotor.Owner == PossessionOwner.Free;
            if (nobodyNear && _ballMotor.Speed <= _stuckSpeed)
                _stuckTimer += Time.deltaTime;
            else
                _stuckTimer = 0f;
            if (_stuckTimer >= _stuckDuration)
                BeginReset("BALL STUCK", false);
        }

        private void BeginReset(string message, bool initial)
        {
            if (State == ArenaRoundState.Finished)
                return;
            if (_resetRoutine != null)
                StopCoroutine(_resetRoutine);
            _resetRoutine = StartCoroutine(ResetAndServe(message, initial));
        }

        private IEnumerator ResetAndServe(string message, bool initial)
        {
            SetState(ArenaRoundState.Resetting);
            _roundMessage = message;
            _playerController.GameplayEnabled = false;
            _ballMotor.SetLive(false);
            _opponentControlTimer = 0f;
            _stuckTimer = 0f;

            Vector3 servePosition = _robot.position + FieldForward() * 0.5f + Vector3.up * 0.3f;
            _ballMotor.ResetBall(servePosition);
            yield return new WaitForSeconds(initial ? 0.75f : _resetDelay);

            SetState(ArenaRoundState.Serving);
            Vector3 target = _player.position + Vector3.up * 0.25f;
            Vector3 direction = target - _ball.transform.position;
            float distance = direction.magnitude;
            Vector3 velocity = direction.normalized * Mathf.Clamp(distance / 0.85f, 4.2f, 7f) + Vector3.up * 0.85f;
            _ballMotor.Serve(velocity);
            yield return new WaitForSeconds(_serveSettleDelay);

            SetState(ArenaRoundState.Live);
            _roundMessage = "PLAY";
            _ballMotor.SetLive(true);
            _playerController.GameplayEnabled = true;
            _resetRoutine = null;
        }

        private void FinishSession()
        {
            SetState(ArenaRoundState.Finished);
            _roundMessage = $"FULL TIME  SCORE {Score}";
            _playerController.GameplayEnabled = false;
            _ballMotor.SetLive(false);
            _ball.SetPhysicalSimulation(false, true);
        }

        private void SetState(ArenaRoundState state)
        {
            if (State == state)
                return;
            State = state;
            RoundStateChanged?.Invoke(state);
        }

        private void MoveActor(Transform actor, Vector3 target, float speed, Vector3 lookTarget)
        {
            if (actor == null)
                return;
            target.y = actor.position.y;
            actor.position = Vector3.MoveTowards(actor.position, target, Mathf.Max(0f, speed) * Time.deltaTime);
            Vector3 look = lookTarget - actor.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.0001f)
                actor.rotation = Quaternion.Slerp(actor.rotation, Quaternion.LookRotation(look), 8f * Time.deltaTime);
        }

        private Vector3 ClampActorPosition(Vector3 world, float padding)
        {
            Vector3 local = _field.transform.InverseTransformPoint(world);
            local.x = Mathf.Clamp(local.x, -_field._halfWidth + padding, _field._halfWidth - padding);
            local.z = Mathf.Clamp(local.z, -_field._halfLength + padding, _field._halfLength - padding);
            return _field.transform.TransformPoint(local);
        }

        private Vector3 FieldForward() => _field != null ? _field.transform.forward : Vector3.forward;
        private Vector3 FieldRight() => _field != null ? _field.transform.right : Vector3.right;

        private void SetFieldLocalPosition(Transform actor, Vector3 localPosition)
        {
            if (actor == null)
                return;
            actor.position = _field.transform.TransformPoint(localPosition);
            actor.rotation = _field.transform.rotation;
            actor.gameObject.SetActive(true);
        }

        private Transform CreateSimpleActor(string actorName, Color color, float scale = 1f)
        {
            GameObject root = new GameObject(actorName);
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.up * 0.9f * scale;
            visual.transform.localScale = new Vector3(0.65f, 0.9f, 0.65f) * scale;
            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
                Destroy(visualCollider);
            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = color;
            return root.transform;
        }

        private void EnsurePlayerVisual()
        {
            if (_player.GetComponentInChildren<Renderer>(true) != null)
                return;

            Transform source = FindVisualSource(_teammate);
            if (source != null)
            {
                GameObject visual = Instantiate(source.gameObject, _player);
                visual.name = "ArenaPlayerVisual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
                    Destroy(collider);
                foreach (Rigidbody body in visual.GetComponentsInChildren<Rigidbody>(true))
                    Destroy(body);
                foreach (MonoBehaviour behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
                    Destroy(behaviour);
                return;
            }

            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            fallback.name = "ArenaPlayerVisual";
            fallback.transform.SetParent(_player, false);
            fallback.transform.localPosition = Vector3.up * 0.9f;
            fallback.transform.localScale = new Vector3(0.65f, 0.9f, 0.65f);
            Collider fallbackCollider = fallback.GetComponent<Collider>();
            if (fallbackCollider != null)
                Destroy(fallbackCollider);
            Renderer renderer = fallback.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(0.12f, 0.36f, 0.95f);
        }

        private static Transform FindVisualSource(Transform actor)
        {
            if (actor == null)
                return null;
            Animator animator = actor.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.transform != actor)
                return animator.transform;
            Transform model = actor.Find("Model");
            if (model != null)
                return model;
            foreach (Transform child in actor)
            {
                if (child.GetComponentInChildren<Renderer>(true) != null)
                    return child;
            }
            return null;
        }

        private static NpcAnimationPresenter EnsureAnimationPresenter(Transform actor)
        {
            if (actor == null)
                return null;
            NpcAnimationPresenter presenter = actor.GetComponent<NpcAnimationPresenter>();
            if (presenter == null)
                presenter = actor.gameObject.AddComponent<NpcAnimationPresenter>();
            return presenter;
        }

        private static Transform FindNamedTransform(string objectName)
        {
            GameObject found = GameObject.Find(objectName);
            return found != null ? found.transform : null;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void OnGUI()
        {
            if (!_initialized)
                return;
            EnsureGuiStyles();

            float width = Mathf.Min(620f, Screen.width - 30f);
            float left = (Screen.width - width) * 0.5f;
            GUI.Box(new Rect(left, 14f, width, 76f), GUIContent.none);
            string time = TimeSpan.FromSeconds(Mathf.CeilToInt(RemainingSeconds)).ToString(@"mm\:ss");
            GUI.Label(new Rect(left + 10f, 18f, width - 20f, 32f), $"ARENA ATTACK   {time}   SCORE {Score}", _titleStyle);
            GUI.Label(new Rect(left + 10f, 52f, width - 20f, 28f), $"{State}  |  {_roundMessage}", _hudStyle);

            float charge = _playerController != null ? _playerController.Charge01 : 0f;
            GUI.Box(new Rect(left + 80f, 96f, width - 160f, 16f), GUIContent.none);
            GUI.Box(new Rect(left + 82f, 98f, (width - 164f) * charge, 12f), GUIContent.none);

            string controls = GameplayModeBootstrap.CurrentProfile switch
            {
                ControlProfile.Gamepad => "LS Move | RS Look | LT Sprint | A Control | X Pass | RT Shot | B Tackle",
                ControlProfile.VrStriker => "Room-scale striker | Physical feet: control / pass intent / shoot",
                ControlProfile.XrSimulator => "XR Simulator striker | Simulated feet route through VR ball actions",
                _ => "WASD Move | Mouse Look | Shift Sprint | E Control | RMB Pass | LMB Shot | Space Tackle"
            };
            GUI.Box(new Rect(20f, Screen.height - 54f, Screen.width - 40f, 36f), GUIContent.none);
            GUI.Label(new Rect(30f, Screen.height - 48f, Screen.width - 60f, 24f), controls, _smallStyle);

            if (!_showDebug)
                return;
            GUI.Box(new Rect(16f, 126f, 318f, 170f), GUIContent.none);
            string debug =
                $"Mode: {GameplayModeBootstrap.CurrentMode}\n" +
                $"Profile: {GameplayModeBootstrap.CurrentProfile}\n" +
                $"State: {State}\n" +
                $"Possession: {Possession}\n" +
                $"Ball speed: {(_ballMotor != null ? _ballMotor.Speed : 0f):0.00} m/s\n" +
                $"Last action: {(_ballMotor != null ? _ballMotor.LastAction : "-")}\n" +
                "F7 Profile | F8 Mode | F9 Debug";
            GUI.Label(new Rect(28f, 136f, 294f, 150f), debug, _smallStyle);
        }

        private void EnsureGuiStyles()
        {
            if (_titleStyle != null)
                return;
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _hudStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                normal = { textColor = new Color(0.75f, 0.92f, 1f) }
            };
            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 14,
                normal = { textColor = Color.white }
            };
        }
    }

    [DisallowMultipleComponent]
    public sealed class ArenaBoundaryWall : MonoBehaviour
    {
        private ArenaAttackController _owner;
        private BallController _ball;

        public void Configure(ArenaAttackController owner, BallController ball)
        {
            _owner = owner;
            _ball = ball;
        }

        private void OnCollisionEnter(Collision collision)
        {
            BallController ball = collision.rigidbody != null
                ? collision.rigidbody.GetComponent<BallController>()
                : null;
            if (ball != null && ball == _ball)
                _owner?.NotifyWallHit(ball);
        }
    }

    [DisallowMultipleComponent]
    public sealed class ArenaGoalTrigger : MonoBehaviour
    {
        private ArenaAttackController _owner;
        private BallController _ball;

        public void Configure(ArenaAttackController owner, BallController ball)
        {
            _owner = owner;
            _ball = ball;
        }

        private void OnTriggerEnter(Collider other)
        {
            BallController ball = other != null ? other.GetComponentInParent<BallController>() : null;
            if (ball != null && ball == _ball)
                _owner?.NotifyGoal(ball);
        }
    }
}
