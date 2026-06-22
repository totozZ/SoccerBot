using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SoccerBot
{
    [DefaultExecutionOrder(-10)]
    [DisallowMultipleComponent]
    public sealed class ArenaPlayerController : MonoBehaviour
    {
        public event Action<BallActionRequest> BallActionRequested;
        public event Action<PlayerIntentState> IntentUpdated;

        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 4.5f;
        [SerializeField] private float _sprintMultiplier = 1.45f;
        [SerializeField] private float _turnSpeed = 13f;
        [SerializeField] private float _tackleSpeed = 8.5f;
        [SerializeField] private float _tackleDuration = 0.22f;
        [SerializeField] private float _tackleCooldown = 1f;

        [Header("Camera")]
        [SerializeField] private float _cameraDistance = 4.2f;
        [SerializeField] private float _cameraHeight = 1.8f;
        [SerializeField] private float _mouseSensitivity = 0.12f;
        [SerializeField] private float _gamepadLookSpeed = 150f;
        [SerializeField] private float _minPitch = -20f;
        [SerializeField] private float _maxPitch = 55f;

        [Header("Actions")]
        [SerializeField] private float _passFullChargeSeconds = 1f;
        [SerializeField] private float _shotFullChargeSeconds = 1.2f;

        public ControlProfile Profile { get; private set; }
        public bool GameplayEnabled { get; set; }
        public bool Paused { get; private set; }
        public float Charge01 => Mathf.Max(_passCharge01, _shotCharge01);
        public float CurrentMoveSpeed { get; private set; }
        public PlayerIntentState CurrentIntent { get; private set; }
        public Transform AimReference => _camera != null ? _camera.transform : transform;

        private FieldBuilder _field;
        private Transform _teammate;
        private Transform _goal;
        private Camera _camera;
        private Transform _cameraRig;
        private float _yaw;
        private float _pitch = 16f;
        private float _passHeldSeconds;
        private float _shotHeldSeconds;
        private float _passCharge01;
        private float _shotCharge01;
        private float _tackleRemaining;
        private float _tackleReadyAt;
        private Animator _animator;
        private readonly HashSet<int> _animatorParameters = new();

        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _sprintAction;
        private InputAction _controlAction;
        private InputAction _passAction;
        private InputAction _shotAction;
        private InputAction _tackleAction;
        private InputAction _pauseAction;

        public void Configure(ControlProfile profile, FieldBuilder field, Transform teammate, Transform goal)
        {
            Profile = profile;
            _field = field;
            _teammate = teammate;
            _goal = goal;
            ResolveCamera();
            ResolveAnimator();
            ConfigurePresentation();
        }

        private void OnEnable()
        {
            CreateActions();
        }

        private void OnDisable()
        {
            DisposeAction(ref _moveAction);
            DisposeAction(ref _lookAction);
            DisposeAction(ref _sprintAction);
            DisposeAction(ref _controlAction);
            DisposeAction(ref _passAction);
            DisposeAction(ref _shotAction);
            DisposeAction(ref _tackleAction);
            DisposeAction(ref _pauseAction);
            SetCursorLocked(false);
        }

        private void Update()
        {
            HandlePause();
            if (Paused || !GameplayEnabled)
            {
                CurrentMoveSpeed = 0f;
                PublishIntent(default);
                return;
            }

            PlayerIntentState intent = ReadIntent();
            if (Profile != ControlProfile.VrStriker && Profile != ControlProfile.XrSimulator)
            {
                UpdateLook(intent.Look);
                UpdateMovement(intent);
            }
            else
            {
                CurrentMoveSpeed = 0f;
            }

            UpdateActions(ref intent);
            UpdateAnimator(intent);
            CurrentIntent = intent;
            IntentUpdated?.Invoke(intent);
        }

        private void LateUpdate()
        {
            if (_cameraRig == null || _camera == null)
                return;

            _cameraRig.position = transform.position + Vector3.up * _cameraHeight;
            _cameraRig.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            _camera.transform.localPosition = new Vector3(0f, 0f, -_cameraDistance);
            _camera.transform.localRotation = Quaternion.identity;
        }

        private PlayerIntentState ReadIntent()
        {
            return new PlayerIntentState
            {
                Move = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero,
                Look = _lookAction != null ? _lookAction.ReadValue<Vector2>() : Vector2.zero,
                SprintHeld = _sprintAction != null && _sprintAction.IsPressed(),
                ControlPressed = _controlAction != null && _controlAction.WasPressedThisFrame(),
                TacklePressed = _tackleAction != null && _tackleAction.WasPressedThisFrame()
            };
        }

        private void UpdateLook(Vector2 look)
        {
            bool gamepadLook = Gamepad.current != null && Gamepad.current.rightStick.ReadValue().sqrMagnitude > 0.001f;
            float scale = gamepadLook ? _gamepadLookSpeed * Time.deltaTime : _mouseSensitivity;
            _yaw += look.x * scale;
            _pitch = Mathf.Clamp(_pitch - look.y * scale, _minPitch, _maxPitch);
        }

        private void UpdateMovement(PlayerIntentState intent)
        {
            Vector3 forward = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;
            Vector3 direction = right * intent.Move.x + forward * intent.Move.y;
            if (direction.sqrMagnitude > 1f)
                direction.Normalize();

            if (_tackleRemaining > 0f)
            {
                _tackleRemaining -= Time.deltaTime;
                direction = transform.forward;
                CurrentMoveSpeed = _tackleSpeed;
            }
            else
            {
                CurrentMoveSpeed = direction.magnitude * _moveSpeed * (intent.SprintHeld ? _sprintMultiplier : 1f);
            }

            if (direction.sqrMagnitude > 0.0001f)
            {
                Vector3 move = direction.normalized * CurrentMoveSpeed * Time.deltaTime;
                transform.position += move;
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(direction.normalized),
                    _turnSpeed * Time.deltaTime);
                ClampToField();
            }
        }

        private void UpdateActions(ref PlayerIntentState intent)
        {
            if (intent.ControlPressed)
                Emit(BallActionKind.Control, 1f);

            if (_passAction != null && _passAction.IsPressed())
            {
                _passHeldSeconds += Time.deltaTime;
                _passCharge01 = ArenaGameplayRules.CalculateCharge01(_passHeldSeconds, _passFullChargeSeconds, 0.2f);
            }
            if (_passAction != null && _passAction.WasReleasedThisFrame())
            {
                intent.PassReleased = true;
                intent.PassCharge01 = _passCharge01;
                Emit(BallActionKind.Pass, _passCharge01);
                _passHeldSeconds = 0f;
                _passCharge01 = 0f;
            }
            else
            {
                intent.PassCharge01 = _passCharge01;
            }

            if (_shotAction != null && _shotAction.IsPressed())
            {
                _shotHeldSeconds += Time.deltaTime;
                _shotCharge01 = ArenaGameplayRules.CalculateCharge01(_shotHeldSeconds, _shotFullChargeSeconds, 0.15f);
            }
            if (_shotAction != null && _shotAction.WasReleasedThisFrame())
            {
                intent.ShotReleased = true;
                intent.ShotCharge01 = _shotCharge01;
                Emit(BallActionKind.Shot, _shotCharge01);
                _shotHeldSeconds = 0f;
                _shotCharge01 = 0f;
            }
            else
            {
                intent.ShotCharge01 = _shotCharge01;
            }

            if (intent.TacklePressed && Time.time >= _tackleReadyAt)
            {
                _tackleReadyAt = Time.time + _tackleCooldown;
                _tackleRemaining = _tackleDuration;
                Emit(BallActionKind.Tackle, 1f);
            }
        }

        private void Emit(BallActionKind kind, float power01)
        {
            BallActionSource source = Profile switch
            {
                ControlProfile.Gamepad => BallActionSource.Gamepad,
                ControlProfile.VrStriker => BallActionSource.VrPhysical,
                ControlProfile.XrSimulator => BallActionSource.XrSimulator,
                _ => BallActionSource.KeyboardMouse
            };
            SetAnimatorTrigger(kind.ToString());
            BallActionRequested?.Invoke(new BallActionRequest(kind, source, transform, GetAimDirection(), power01));
        }

        private void ResolveAnimator()
        {
            _animator = GetComponentInChildren<Animator>(true);
            _animatorParameters.Clear();
            if (_animator == null)
                return;
            foreach (AnimatorControllerParameter parameter in _animator.parameters)
                _animatorParameters.Add(parameter.nameHash);
        }

        private void UpdateAnimator(PlayerIntentState intent)
        {
            if (_animator == null)
                return;
            int speedHash = Animator.StringToHash("Speed");
            int sprintHash = Animator.StringToHash("Sprinting");
            if (_animatorParameters.Contains(speedHash))
                _animator.SetFloat(speedHash, CurrentMoveSpeed);
            if (_animatorParameters.Contains(sprintHash))
                _animator.SetBool(sprintHash, intent.SprintHeld && CurrentMoveSpeed > _moveSpeed);
        }

        private void SetAnimatorTrigger(string parameterName)
        {
            if (_animator == null)
                return;
            int hash = Animator.StringToHash(parameterName);
            if (_animatorParameters.Contains(hash))
                _animator.SetTrigger(hash);
        }

        private Vector3 GetAimDirection()
        {
            Transform reference = AimReference;
            return ArenaGameplayRules.FlattenDirection(reference.forward, transform.forward);
        }

        private void HandlePause()
        {
            if (_pauseAction == null || !_pauseAction.WasPressedThisFrame())
                return;

            Paused = !Paused;
            SetCursorLocked(!Paused && Profile != ControlProfile.VrStriker && Profile != ControlProfile.XrSimulator);
            if (Paused)
            {
                _passHeldSeconds = 0f;
                _shotHeldSeconds = 0f;
                _passCharge01 = 0f;
                _shotCharge01 = 0f;
            }
        }

        private void ConfigurePresentation()
        {
            bool vrProfile = Profile == ControlProfile.VrStriker || Profile == ControlProfile.XrSimulator;
            if (vrProfile)
            {
                SetCursorLocked(false);
                QuestControllerLegRig rig = GetComponent<QuestControllerLegRig>();
                if (rig == null)
                    rig = gameObject.AddComponent<QuestControllerLegRig>();
                BallController ball = FindFirstObjectByType<BallController>(FindObjectsInactive.Include);
                if (ball != null)
                    rig.SetFootBallInteraction(true, true, 1 << ball.gameObject.layer);
                return;
            }

            SetCursorLocked(true);
            GameObject rigObject = new GameObject("ArenaThirdPersonCameraRig");
            _cameraRig = rigObject.transform;
            if (_camera != null)
                _camera.transform.SetParent(_cameraRig, false);
            _yaw = transform.eulerAngles.y;
        }

        private void ResolveCamera()
        {
            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null)
                _camera = FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
            if (_camera != null)
                _camera.enabled = true;
        }

        private void ClampToField()
        {
            float halfWidth = _field != null ? _field._halfWidth : 6f;
            float halfLength = _field != null ? _field._halfLength : 9f;
            Transform root = _field != null ? _field.transform : null;
            Vector3 local = root != null ? root.InverseTransformPoint(transform.position) : transform.position;
            local.x = Mathf.Clamp(local.x, -halfWidth + 0.45f, halfWidth - 0.45f);
            local.z = Mathf.Clamp(local.z, -halfLength + 0.45f, halfLength - 0.45f);
            Vector3 world = root != null ? root.TransformPoint(local) : local;
            transform.position = new Vector3(world.x, transform.position.y, world.z);
        }

        private void CreateActions()
        {
            _moveAction = new InputAction("ArenaMove", InputActionType.Value, expectedControlType: "Vector2");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _moveAction.AddBinding("<Gamepad>/leftStick");

            _lookAction = new InputAction("ArenaLook", InputActionType.Value, expectedControlType: "Vector2");
            _lookAction.AddBinding("<Mouse>/delta");
            _lookAction.AddBinding("<Gamepad>/rightStick");

            _sprintAction = CreateButton("ArenaSprint", "<Keyboard>/leftShift", "<Gamepad>/leftTrigger");
            _controlAction = CreateButton("ArenaControl", "<Keyboard>/e", "<Gamepad>/buttonSouth");
            _passAction = CreateButton("ArenaPass", "<Mouse>/rightButton", "<Gamepad>/buttonWest");
            _shotAction = CreateButton("ArenaShot", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            _tackleAction = CreateButton("ArenaTackle", "<Keyboard>/space", "<Gamepad>/buttonEast");
            _pauseAction = CreateButton("ArenaPause", "<Keyboard>/escape", "<Gamepad>/start");

            _moveAction.Enable();
            _lookAction.Enable();
        }

        private static InputAction CreateButton(string name, string firstBinding, string secondBinding)
        {
            var action = new InputAction(name, InputActionType.Button);
            action.AddBinding(firstBinding);
            action.AddBinding(secondBinding);
            action.Enable();
            return action;
        }

        private static void DisposeAction(ref InputAction action)
        {
            if (action == null)
                return;
            action.Disable();
            action.Dispose();
            action = null;
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void PublishIntent(PlayerIntentState intent)
        {
            CurrentIntent = intent;
            IntentUpdated?.Invoke(intent);
        }
    }
}
