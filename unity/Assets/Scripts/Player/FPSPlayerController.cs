// FPSPlayerController.cs — First-person controller for the player (the Teammate).
// Drives the FPS camera and emits OnChargeChanged / OnShoot events that the
// MatchFlowController and PowerBarUI consume.
//
// Inputs (New Input System, multi-source — works on PC + Quest):
//   WASD / Quest left thumbstick      — strafe / forward, ground-locked
//   Mouse RMB                         — hold + drag to look around (PC only; VR uses head tracking)
//   Mouse LMB / Quest right trigger / — hold to charge a shot, release to fire
//   Quest right A button / Quest left trigger
//   Esc                               — release any active charge (safety)
//
// Attach this to the Teammate GameObject. Wire _cameraAnchor to a child
// transform at eye height (e.g. y=1.6). The FPS camera should be a child
// of the anchor; the controller rotates the anchor for yaw and the camera
// for pitch.

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SoccerBot
{
    public class FPSPlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _cameraAnchor;
        [SerializeField] private Camera _fpsCamera;

        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 4f;
        [SerializeField] private float _lookSensitivity = 1.5f;
        [SerializeField] private float _minPitch = -70f;
        [SerializeField] private float _maxPitch = 70f;
        [SerializeField] private float _stickLookYawSpeed = 95f;
        [SerializeField, Range(0f, 0.5f)] private float _stickLookDeadzone = 0.18f;

        [Header("Field Limits")]
        [SerializeField] private bool _constrainToField = true;
        [SerializeField] private Vector2 _fallbackFieldHalfExtents = new Vector2(5.8f, 8.8f);
        [SerializeField] private float _fieldBoundaryPadding = 0.35f;
        [SerializeField] private FieldBuilder _fieldBuilder;

        [Header("Charge")]
        [Tooltip("Seconds of holding LMB to reach full power (1.0).")]
        [SerializeField] private float _maxChargeTime = 1.5f;

        [Header("Motion Shot")]
        [Tooltip("Right-trigger motion shot: minimum pull-back distance before release can fire.")]
        [SerializeField] private float _motionShotMinBackswing = 0.12f;
        [Tooltip("Right-trigger motion shot: pull-back distance that counts as full backswing.")]
        [SerializeField] private float _motionShotFullBackswing = 0.55f;
        [Tooltip("Right-trigger motion shot: minimum forward hand speed before release can fire.")]
        [SerializeField] private float _motionShotMinForwardSpeed = 0.65f;
        [Tooltip("Right-trigger motion shot: forward hand speed that counts as full power.")]
        [SerializeField] private float _motionShotFullForwardSpeed = 3.2f;
        [SerializeField, Range(0f, 1f)] private float _motionShotSwingDirectionWeight = 0.35f;

        [Header("Aim Feedback (optional)")]
        [SerializeField] private float _chargeRecoilOffset = 0.08f;
        [Tooltip("Creates QuestControllerLegRig so Quest controller poses drive real tracked foot bodies.")]
        [SerializeField] private bool _showFirstPersonLegs = true;

        public event Action<float> OnChargeChanged;          // 0..1 each frame while charging
        public event Action<float, Vector3> OnShoot;          // (power01, worldDirection)
        public event Action<Vector3> OnReceiveAttempt;        // worldDirection
        public event Action OnChargeBegin;
        public event Action OnChargeCancel;

        public bool IsCharging => _charging || _motionCharging;
        public float CurrentPower01 => _charging
            ? Mathf.Clamp01(_chargeTime / _maxChargeTime)
            : (_motionCharging ? _motionPower01 : 0f);
        public bool ReceiveInputHeld => _receiveAction != null && _receiveAction.IsPressed();
        public bool ShootingEnabled { get; set; } = false;   // gated by MatchFlowController
        public bool MovementEnabled { get; set; } = false;   // gated by MatchFlowController (only true during Possession)
        public bool ReceptionEnabled { get; set; } = false;  // gated by MatchFlowController during Pass

        private float _yaw;
        private float _pitch;
        private bool  _charging;
        private bool _motionCharging;
        private float _chargeTime;
        private float _motionBackswing;
        private float _motionForwardSpeed;
        private float _motionPower01;
        private Vector3 _cameraRestLocalPos;
        private Vector3 _motionStartHandPos;
        private Vector3 _motionLastHandPos;
        private Vector3 _motionBestForwardVelocity;

        // Multi-source shoot trigger so PC mouse, Quest trigger, and Quest A button
        // all work without an Input Actions asset. Created in OnEnable, disposed in OnDisable.
        private InputAction _chargeAction;
        private InputAction _motionShootAction;
        private InputAction _rightHandPositionAction;
        private InputAction _rightHandVelocityAction;
        private InputAction _receiveAction;
        private InputAction _moveAction;   // Quest left thumbstick (Vector2)
        private InputAction _lookAction;   // Quest right thumbstick / gamepad right stick (Vector2)

        void OnEnable()
        {
            _chargeAction = new InputAction("ChargeShot", InputActionType.Button);
            _chargeAction.AddBinding("<Mouse>/leftButton");
            _chargeAction.AddBinding("<XRController>{RightHand}/primaryButton");   // Quest A button
            _chargeAction.AddBinding("<XRController>{LeftHand}/triggerPressed");
            _chargeAction.AddBinding("<XRController>{LeftHand}/primaryButton");    // Quest X button
            _chargeAction.Enable();

            _motionShootAction = new InputAction("MotionShot", InputActionType.Button);
            _motionShootAction.AddBinding("<XRController>{RightHand}/triggerPressed");
            _motionShootAction.Enable();

            _rightHandPositionAction = new InputAction("RightHandPosition", InputActionType.Value, expectedControlType: "Vector3");
            _rightHandPositionAction.AddBinding("<XRController>{RightHand}/devicePosition");
            _rightHandPositionAction.Enable();

            _rightHandVelocityAction = new InputAction("RightHandVelocity", InputActionType.Value, expectedControlType: "Vector3");
            _rightHandVelocityAction.AddBinding("<XRController>{RightHand}/deviceVelocity");
            _rightHandVelocityAction.Enable();

            _receiveAction = new InputAction("Receive", InputActionType.Button);
            _receiveAction.AddBinding("<Keyboard>/space");
            _receiveAction.AddBinding("<Mouse>/leftButton");
            _receiveAction.AddBinding("<XRController>{RightHand}/gripPressed");
            _receiveAction.AddBinding("<XRController>{LeftHand}/gripPressed");
            _receiveAction.AddBinding("<XRController>{RightHand}/triggerPressed");
            _receiveAction.Enable();

            _moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _moveAction.AddBinding("<XRController>{LeftHand}/thumbstick");
            _moveAction.Enable();

            _lookAction = new InputAction("Look", InputActionType.Value, expectedControlType: "Vector2");
            _lookAction.AddBinding("<XRController>{RightHand}/thumbstick");
            _lookAction.AddBinding("<Gamepad>/rightStick");
            _lookAction.Enable();
        }

        void OnDisable()
        {
            if (_chargeAction != null)
            {
                _chargeAction.Disable();
                _chargeAction.Dispose();
                _chargeAction = null;
            }
            if (_motionShootAction != null)
            {
                _motionShootAction.Disable();
                _motionShootAction.Dispose();
                _motionShootAction = null;
            }
            if (_rightHandPositionAction != null)
            {
                _rightHandPositionAction.Disable();
                _rightHandPositionAction.Dispose();
                _rightHandPositionAction = null;
            }
            if (_rightHandVelocityAction != null)
            {
                _rightHandVelocityAction.Disable();
                _rightHandVelocityAction.Dispose();
                _rightHandVelocityAction = null;
            }
            if (_receiveAction != null)
            {
                _receiveAction.Disable();
                _receiveAction.Dispose();
                _receiveAction = null;
            }
            if (_moveAction != null)
            {
                _moveAction.Disable();
                _moveAction.Dispose();
                _moveAction = null;
            }
            if (_lookAction != null)
            {
                _lookAction.Disable();
                _lookAction.Dispose();
                _lookAction = null;
            }
        }

        void Start()
        {
            // Auto-resolve children if not wired in Inspector. Convention:
            //   <self>/FpsAnchor          ← _cameraAnchor
            //   <self>/FpsAnchor/FpsCamera ← _fpsCamera
            if (_cameraAnchor == null)
            {
                var t = transform.Find("FpsAnchor");
                if (t != null) _cameraAnchor = t;
            }
            if (_cameraAnchor == null) _cameraAnchor = transform;
            if (_fpsCamera == null && _cameraAnchor != null)
                _fpsCamera = _cameraAnchor.GetComponentInChildren<Camera>(true);

            // Make sure the FPS camera is enabled — it may have been disabled
            // by CameraSwitcher earlier in scene-build wizardry.
            if (_fpsCamera != null) _fpsCamera.enabled = true;

            if (_fpsCamera != null)
            {
                var worldEuler = _fpsCamera.transform.eulerAngles;
                _yaw   = worldEuler.y;
                _pitch = worldEuler.x > 180f ? worldEuler.x - 360f : worldEuler.x;
                _cameraRestLocalPos = _fpsCamera.transform.localPosition;
            }
            else
            {
                _yaw   = transform.eulerAngles.y;
                _pitch = 0f;
            }

            EnsureFirstPersonLegs();
            ResolveFieldBuilder();
        }

        private void EnsureFirstPersonLegs()
        {
            if (!_showFirstPersonLegs) return;

            var legacyAvatar = GetComponent<FirstPersonLegAvatar>();
            if (legacyAvatar != null)
                legacyAvatar.enabled = false;

            var rig = GetComponent<QuestControllerLegRig>();
            if (rig == null)
                rig = gameObject.AddComponent<QuestControllerLegRig>();

            BallController ball = FindFirstObjectByType<BallController>(FindObjectsInactive.Include);
            if (ball != null)
                rig.SetFootBallInteraction(true, true, 1 << ball.gameObject.layer);
            else
                rig.SetFootBallInteraction(true, true);
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            // kb may be null on Quest standalone — that's fine, charge logic falls back to XR.

            HandleLook(mouse);
            HandleMove(kb);
            HandleReceive();
            HandleCharge(kb);
            ApplyChargeRecoil();
        }

        // ── Look ─────────────────────────────────────────────

        private void HandleLook(Mouse mouse)
        {
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _yaw   += delta.x * _lookSensitivity * 0.1f;
                _pitch -= delta.y * _lookSensitivity * 0.1f;
            }

            Vector2 stickLook = _lookAction != null ? _lookAction.ReadValue<Vector2>() : Vector2.zero;
            if (Mathf.Abs(stickLook.x) > _stickLookDeadzone)
            {
                _yaw += stickLook.x * _stickLookYawSpeed * Time.deltaTime;
            }

            _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_cameraAnchor != null)
                _cameraAnchor.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        // ── Move ─────────────────────────────────────────────

        private void HandleMove(Keyboard kb)
        {
            if (!MovementEnabled) return;

            // Combine WASD (PC) with Quest left thumbstick. Either source can drive movement.
            float x = 0f, z = 0f;
            if (kb != null)
            {
                x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
                z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            }
            if (_moveAction != null)
            {
                Vector2 stick = _moveAction.ReadValue<Vector2>();
                if (Mathf.Abs(stick.x) > 0.15f) x = stick.x;   // deadzone
                if (Mathf.Abs(stick.y) > 0.15f) z = stick.y;
            }
            if (x == 0f && z == 0f) return;

            // Direction follows the camera yaw so "forward" matches where the player is looking
            // (head tracking in VR, mouse-yaw on PC). Falls back to transform.forward.
            Transform dirRef = _fpsCamera != null ? _fpsCamera.transform : transform;
            Vector3 fwd = dirRef.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = dirRef.right; right.y = 0f; right.Normalize();
            Vector3 move = (right * x + fwd * z).normalized * _moveSpeed * Time.deltaTime;
            transform.position += move;
            ClampToFieldBounds();
        }

        private void ResolveFieldBuilder()
        {
            if (_fieldBuilder == null)
                _fieldBuilder = FindFirstObjectByType<FieldBuilder>(FindObjectsInactive.Include);
        }

        private void ClampToFieldBounds()
        {
            if (!_constrainToField)
                return;

            if (_fieldBuilder == null)
                ResolveFieldBuilder();

            Transform boundsRoot = _fieldBuilder != null ? _fieldBuilder.transform : null;
            Vector2 halfExtents = _fieldBuilder != null
                ? new Vector2(_fieldBuilder._halfWidth, _fieldBuilder._halfLength)
                : _fallbackFieldHalfExtents;
            halfExtents.x = Mathf.Max(0.5f, halfExtents.x - _fieldBoundaryPadding);
            halfExtents.y = Mathf.Max(0.5f, halfExtents.y - _fieldBoundaryPadding);

            if (boundsRoot != null)
            {
                Vector3 local = boundsRoot.InverseTransformPoint(transform.position);
                local.x = Mathf.Clamp(local.x, -halfExtents.x, halfExtents.x);
                local.z = Mathf.Clamp(local.z, -halfExtents.y, halfExtents.y);
                Vector3 clampedWorld = boundsRoot.TransformPoint(local);
                transform.position = new Vector3(clampedWorld.x, transform.position.y, clampedWorld.z);
                return;
            }

            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x, -halfExtents.x, halfExtents.x);
            pos.z = Mathf.Clamp(pos.z, -halfExtents.y, halfExtents.y);
            transform.position = pos;
        }

        // ── Charge & Shoot ───────────────────────────────────

        private void HandleReceive()
        {
            if (_receiveAction == null || !ReceptionEnabled) return;
            if (!_receiveAction.WasPressedThisFrame()) return;

            Vector3 dir = _fpsCamera != null
                ? _fpsCamera.transform.forward
                : transform.forward;
            dir.Normalize();
            OnReceiveAttempt?.Invoke(dir);
        }

        private void HandleCharge(Keyboard kb)
        {
            if (_chargeAction == null && _motionShootAction == null) return;

            // Cancel charge if disabled mid-way (e.g. scenario starts)
            if (!ShootingEnabled && IsCharging)
            {
                CancelCharge();
                return;
            }
            if (kb != null && kb.escapeKey.wasPressedThisFrame && IsCharging)
            {
                CancelCharge();
                return;
            }

            if (!ShootingEnabled) return;

            if (_motionShootAction != null && _motionShootAction.WasPressedThisFrame())
            {
                BeginMotionShot();
                return;
            }

            if (_motionCharging)
            {
                UpdateMotionShot();
                if (_motionShootAction != null && _motionShootAction.WasReleasedThisFrame())
                    ReleaseMotionShot();
                return;
            }

            if (_chargeAction != null && _chargeAction.WasPressedThisFrame())
            {
                _charging = true;
                _chargeTime = 0f;
                OnChargeBegin?.Invoke();
                OnChargeChanged?.Invoke(0f);
                return;
            }

            if (_charging)
            {
                _chargeTime += Time.deltaTime;
                float power = Mathf.Clamp01(_chargeTime / _maxChargeTime);
                OnChargeChanged?.Invoke(power);

                bool chargeReleased = _chargeAction != null && _chargeAction.WasReleasedThisFrame();
                bool motionTriggerReleased = _motionShootAction != null && _motionShootAction.WasReleasedThisFrame();
                if (chargeReleased || motionTriggerReleased)
                {
                    Vector3 dir = GetAimDirection();
                    _charging = false;
                    OnShoot?.Invoke(power, dir);
                    OnChargeChanged?.Invoke(0f);
                }
            }
        }

        private void CancelCharge()
        {
            _charging = false;
            _motionCharging = false;
            _chargeTime = 0f;
            _motionBackswing = 0f;
            _motionForwardSpeed = 0f;
            _motionPower01 = 0f;
            _motionBestForwardVelocity = Vector3.zero;
            OnChargeChanged?.Invoke(0f);
            OnChargeCancel?.Invoke();
        }

        private void BeginMotionShot()
        {
            if (!TryReadRightHandPosition(out Vector3 handPos))
            {
                _charging = true;
                _chargeTime = 0f;
                OnChargeBegin?.Invoke();
                OnChargeChanged?.Invoke(0f);
                return;
            }

            _motionCharging = true;
            _motionStartHandPos = handPos;
            _motionLastHandPos = handPos;
            _motionBackswing = 0f;
            _motionForwardSpeed = 0f;
            _motionPower01 = 0f;
            _motionBestForwardVelocity = Vector3.zero;
            OnChargeBegin?.Invoke();
            OnChargeChanged?.Invoke(0f);
        }

        private void UpdateMotionShot()
        {
            if (!TryReadRightHandPosition(out Vector3 handPos)) return;

            Vector3 aim = GetAimDirection();
            float signedTravel = Vector3.Dot(handPos - _motionStartHandPos, aim);
            _motionBackswing = Mathf.Max(_motionBackswing, -signedTravel);

            Vector3 velocity = ReadRightHandVelocity(handPos);
            float forwardSpeed = Mathf.Max(0f, Vector3.Dot(velocity, aim));
            if (_motionBackswing >= _motionShotMinBackswing && forwardSpeed > _motionForwardSpeed)
            {
                _motionForwardSpeed = forwardSpeed;
                _motionBestForwardVelocity = velocity;
            }

            _motionLastHandPos = handPos;
            _motionPower01 = CalculateMotionPower();
            OnChargeChanged?.Invoke(_motionPower01);
        }

        private void ReleaseMotionShot()
        {
            UpdateMotionShot();
            float power = CalculateMotionPower();
            if (_motionBackswing < _motionShotMinBackswing || _motionForwardSpeed < _motionShotMinForwardSpeed)
            {
                CancelCharge();
                return;
            }

            Vector3 dir = GetMotionShotDirection();
            _motionCharging = false;
            _motionPower01 = 0f;
            OnShoot?.Invoke(power, dir);
            OnChargeChanged?.Invoke(0f);
        }

        private float CalculateMotionPower()
        {
            float back01 = Mathf.InverseLerp(_motionShotMinBackswing, _motionShotFullBackswing, _motionBackswing);
            float speed01 = Mathf.InverseLerp(_motionShotMinForwardSpeed, _motionShotFullForwardSpeed, _motionForwardSpeed);
            return Mathf.Clamp01(back01 * 0.45f + speed01 * 0.55f);
        }

        private bool TryReadRightHandPosition(out Vector3 handPos)
        {
            handPos = Vector3.zero;
            if (_rightHandPositionAction == null || _rightHandPositionAction.controls.Count == 0)
                return false;

            handPos = _rightHandPositionAction.ReadValue<Vector3>();
            return true;
        }

        private Vector3 ReadRightHandVelocity(Vector3 currentHandPos)
        {
            if (_rightHandVelocityAction != null && _rightHandVelocityAction.controls.Count > 0)
            {
                Vector3 velocity = _rightHandVelocityAction.ReadValue<Vector3>();
                if (velocity.sqrMagnitude > 0.0001f)
                    return velocity;
            }

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            return (currentHandPos - _motionLastHandPos) / dt;
        }

        private Vector3 GetMotionShotDirection()
        {
            Vector3 aim = GetAimDirection();
            if (_motionBestForwardVelocity.sqrMagnitude < 0.0001f)
                return aim;

            Vector3 swingDir = _motionBestForwardVelocity.normalized;
            Vector3 dir = Vector3.Slerp(aim, swingDir, _motionShotSwingDirectionWeight);
            dir.Normalize();
            return dir;
        }

        private Vector3 GetAimDirection()
        {
            Vector3 dir = _fpsCamera != null
                ? _fpsCamera.transform.forward
                : transform.forward;
            dir.Normalize();
            return dir;
        }

        // ── Subtle camera recoil while charging ──────────────

        private void ApplyChargeRecoil()
        {
            if (_fpsCamera == null) return;
            float p = CurrentPower01;
            // Pull the camera back slightly as power builds — gives weight to the windup.
            Vector3 target = _cameraRestLocalPos + new Vector3(0f, 0f, -p * _chargeRecoilOffset);
            _fpsCamera.transform.localPosition = Vector3.Lerp(
                _fpsCamera.transform.localPosition, target, Time.deltaTime * 8f);
        }
    }
}
