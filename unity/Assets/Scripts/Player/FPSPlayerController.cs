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

        [Header("Charge")]
        [Tooltip("Seconds of holding LMB to reach full power (1.0).")]
        [SerializeField] private float _maxChargeTime = 1.5f;

        [Header("Aim Feedback (optional)")]
        [SerializeField] private float _chargeRecoilOffset = 0.08f;

        public event Action<float> OnChargeChanged;          // 0..1 each frame while charging
        public event Action<float, Vector3> OnShoot;          // (power01, worldDirection)
        public event Action OnChargeBegin;
        public event Action OnChargeCancel;

        public bool IsCharging => _charging;
        public float CurrentPower01 => _charging ? Mathf.Clamp01(_chargeTime / _maxChargeTime) : 0f;
        public bool ShootingEnabled { get; set; } = false;   // gated by MatchFlowController
        public bool MovementEnabled { get; set; } = false;   // gated by MatchFlowController (only true during Possession)

        private float _yaw;
        private float _pitch;
        private bool  _charging;
        private float _chargeTime;
        private Vector3 _cameraRestLocalPos;

        // Multi-source shoot trigger so PC mouse, Quest trigger, and Quest A button
        // all work without an Input Actions asset. Created in OnEnable, disposed in OnDisable.
        private InputAction _shootAction;
        private InputAction _moveAction;   // Quest left thumbstick (Vector2)

        void OnEnable()
        {
            _shootAction = new InputAction("Shoot", InputActionType.Button);
            _shootAction.AddBinding("<Mouse>/leftButton");
            _shootAction.AddBinding("<XRController>{RightHand}/triggerPressed");
            _shootAction.AddBinding("<XRController>{RightHand}/primaryButton");   // Quest A button
            _shootAction.AddBinding("<XRController>{LeftHand}/triggerPressed");
            _shootAction.AddBinding("<XRController>{LeftHand}/primaryButton");    // Quest X button
            _shootAction.Enable();

            _moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _moveAction.AddBinding("<XRController>{LeftHand}/thumbstick");
            _moveAction.Enable();
        }

        void OnDisable()
        {
            if (_shootAction != null)
            {
                _shootAction.Disable();
                _shootAction.Dispose();
                _shootAction = null;
            }
            if (_moveAction != null)
            {
                _moveAction.Disable();
                _moveAction.Dispose();
                _moveAction = null;
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

            _yaw   = transform.eulerAngles.y;
            _pitch = 0f;
            if (_fpsCamera != null) _cameraRestLocalPos = _fpsCamera.transform.localPosition;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            // kb may be null on Quest standalone — that's fine, charge logic falls back to XR.

            HandleLook(mouse);
            HandleMove(kb);
            HandleCharge(kb);
            ApplyChargeRecoil();
        }

        // ── Look ─────────────────────────────────────────────

        private void HandleLook(Mouse mouse)
        {
            if (mouse == null || !mouse.rightButton.isPressed) return;
            Vector2 delta = mouse.delta.ReadValue();
            _yaw   += delta.x * _lookSensitivity * 0.1f;
            _pitch -= delta.y * _lookSensitivity * 0.1f;
            _pitch  = Mathf.Clamp(_pitch, _minPitch, _maxPitch);

            // Camera detached during Shot/Score — rotate it directly in world space.
            if (_fpsCamera != null && _fpsCamera.transform.parent == null)
            {
                _fpsCamera.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
                return;
            }

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
        }

        // ── Charge & Shoot ───────────────────────────────────

        private void HandleCharge(Keyboard kb)
        {
            if (_shootAction == null) return;

            // Cancel charge if disabled mid-way (e.g. scenario starts)
            if (!ShootingEnabled && _charging)
            {
                CancelCharge();
                return;
            }
            if (kb != null && kb.escapeKey.wasPressedThisFrame && _charging)
            {
                CancelCharge();
                return;
            }

            if (!ShootingEnabled) return;

            if (_shootAction.WasPressedThisFrame())
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

                if (_shootAction.WasReleasedThisFrame())
                {
                    Vector3 dir = _fpsCamera != null
                        ? _fpsCamera.transform.forward
                        : transform.forward;
                    dir.Normalize();
                    _charging = false;
                    OnShoot?.Invoke(power, dir);
                    OnChargeChanged?.Invoke(0f);
                }
            }
        }

        private void CancelCharge()
        {
            _charging = false;
            _chargeTime = 0f;
            OnChargeChanged?.Invoke(0f);
            OnChargeCancel?.Invoke();
        }

        // ── Subtle camera recoil while charging ──────────────

        private void ApplyChargeRecoil()
        {
            if (_fpsCamera == null) return;
            // Skip while detached during scenario playback — without a parent,
            // localPosition == position, so Lerping it pulls the camera in world
            // space toward origin (visible as a forward drift after the shot).
            if (_fpsCamera.transform.parent == null) return;
            float p = CurrentPower01;
            // Pull the camera back slightly as power builds — gives weight to the windup.
            Vector3 target = _cameraRestLocalPos + new Vector3(0f, 0f, -p * _chargeRecoilOffset);
            _fpsCamera.transform.localPosition = Vector3.Lerp(
                _fpsCamera.transform.localPosition, target, Time.deltaTime * 8f);
        }
    }
}
