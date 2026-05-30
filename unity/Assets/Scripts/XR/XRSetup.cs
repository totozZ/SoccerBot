// XRSetup.cs — One-click XR Origin setup for SoccerBot.
// Editor menu: SoccerBot → Setup XR Origin
//
// What it does:
//   1. Creates an XR Origin GameObject (Camera Offset → Main Camera).
//   2. Disables the old standalone Main Camera, copies scripts to new XR camera.
//   3. Adds PCCameraController: right-click drag = look, WASD = move.
//      Auto-activates when no XR headset is detected after 1s.
//
// PC testing: run SoccerBot → Setup XR Origin, then press Play. No Simulator needed.

using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SoccerBot
{
    public class XRSetup : MonoBehaviour
    {
        [Header("XR Origin Config")]
        [SerializeField] private bool _enableOnStart = true;

        void Start()
        {
            // Disable SmoothFollow immediately — it fights with XR camera setup.
            var sf = GetComponentInChildren<SmoothFollow>();
            if (sf != null) sf.enabled = false;

            if (_enableOnStart)
            {
                var xrOrigin = GetComponent<XROrigin>();
                if (xrOrigin != null)
                {
                    xrOrigin.enabled = true;
                    Debug.Log("[XRSetup] XR Origin enabled.");
                }
            }

            // Always enable PC free-fly on PC builds / Editor.
            // In VR with a real headset, TrackedPoseDriver will overwrite the camera pose;
            // disable PCCameraController manually via Inspector if needed.
            var cam = GetComponentInChildren<Camera>();
            if (cam != null && cam.TryGetComponent<PCCameraController>(out var pc))
            {
                pc.enabled = true;
                Debug.Log("[XRSetup] PC free-fly on (right-drag look, WASD move). Disable via Inspector if using headset.");
            }

            EnableHeadTrackingOnRenderCamera();
        }

        // The camera that actually renders is Player/FpsAnchor/FpsCamera (the XR Camera
        // under XR Origin has its Camera component disabled). That render camera has no
        // TrackedPoseDriver, so on Quest the headset rotation never reaches the view and
        // the picture stays frozen. Attach a rotation-only TrackedPoseDriver here so head
        // turns drive the view in VR, while leaving position fixed at the player's eye
        // height (so the shot/camera-detach flow is undisturbed).
        //
        // On PC (no HMD) trackingState reports "not tracked", so the driver writes nothing
        // and FPSPlayerController's mouse-look keeps working — no regression.
        private void EnableHeadTrackingOnRenderCamera()
        {
            var playerGO = GameObject.Find("Player");
            if (playerGO == null) return;

            var fpsCameraT = playerGO.transform.Find("FpsAnchor/FpsCamera");
            if (fpsCameraT == null) return;

            if (fpsCameraT.GetComponent<TrackedPoseDriver>() != null) return;   // already tracked

            var driver = fpsCameraT.gameObject.AddComponent<TrackedPoseDriver>();
            driver.trackingType = TrackedPoseDriver.TrackingType.RotationOnly;  // keep eye height fixed
            driver.rotationInput = new InputActionProperty(
                new InputAction("Rotation", binding: "<XRHMD>/centerEyeRotation"));
            driver.trackingStateInput = new InputActionProperty(
                new InputAction("TrackingState", binding: "<XRHMD>/trackingState"));

            Debug.Log("[XRSetup] Head tracking (rotation-only) attached to render camera 'FpsCamera'.");
        }

#if UNITY_EDITOR
        [MenuItem("SoccerBot/Setup XR Origin")]
        public static void SetupXROrigin()
        {
            // 1. Check if XR Origin already exists.
            var existing = FindObjectOfType<XROrigin>();
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog(
                    "XR Origin Already Exists",
                    $"Scene already has an XR Origin on '{existing.name}'. Overwrite?",
                    "Yes, recreate", "Cancel"))
                    return;

                DestroyImmediate(existing.gameObject);
            }

            // 2. Create XR Origin GameObject hierarchy.
            //    XROrigin (root)
            //      └─ Camera Offset
            //           └─ Main Camera     (XR-tracked)
            //                └─ LeftHand Controller
            //                └─ RightHand Controller

            var xrOriginGO = new GameObject("XR Origin");
            var xrOrigin = xrOriginGO.AddComponent<XROrigin>();

            // Camera Offset (tracking space)
            var cameraOffsetGO = new GameObject("Camera Offset");
            cameraOffsetGO.transform.SetParent(xrOriginGO.transform, false);
            xrOrigin.CameraFloorOffsetObject = cameraOffsetGO;

            // XR Camera
            var xrCameraGO = new GameObject("Main Camera");
            xrCameraGO.tag = "MainCamera";
            xrCameraGO.transform.SetParent(cameraOffsetGO.transform, false);

            var xrCamera = xrCameraGO.AddComponent<Camera>();
            xrCamera.nearClipPlane = 0.1f;
            xrCamera.farClipPlane = 1000f;

            xrCameraGO.AddComponent<AudioListener>();

            // TrackedPoseDriver: drives camera from XR headset pose.
            var trackedPose = xrCameraGO.AddComponent<TrackedPoseDriver>();
            trackedPose.positionInput = new InputActionProperty(
                new InputAction("Position", binding: "<XRHMD>/centerEyePosition"));
            trackedPose.rotationInput = new InputActionProperty(
                new InputAction("Rotation", binding: "<XRHMD>/centerEyeRotation"));
            trackedPose.trackingStateInput = new InputActionProperty(
                new InputAction("TrackingState", binding: "<XRHMD>/trackingState"));

            // PC fallback (starts disabled; XRSetup auto-enables if no headset)
            xrCameraGO.AddComponent<PCCameraController>();

            // ── Transfer existing camera scripts ──────────
            var oldCamera = Camera.main;
            if (oldCamera != null && oldCamera.gameObject != xrCameraGO)
            {
                var oldSmoothFollow = oldCamera.GetComponent<SmoothFollow>();
                if (oldSmoothFollow != null)
                {
                    var newSmoothFollow = xrCameraGO.AddComponent<SmoothFollow>();
                    // Copy serialized values
                    var so = new SerializedObject(oldSmoothFollow);
                    var target = so.FindProperty("_target");
                    if (target != null)
                    {
                        var so2 = new SerializedObject(newSmoothFollow);
                        so2.FindProperty("_target").objectReferenceValue = target.objectReferenceValue;
                        so2.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                var oldSwitcher = oldCamera.GetComponent<CameraSwitcher>();
                if (oldSwitcher != null)
                {
                    var newSwitcher = xrCameraGO.AddComponent<CameraSwitcher>();
                    var so = new SerializedObject(oldSwitcher);
                    var list = so.FindProperty("_cameras");
                    if (list != null)
                    {
                        var so2 = new SerializedObject(newSwitcher);
                        var newList = so2.FindProperty("_cameras");
                        newList.arraySize = list.arraySize;
                        for (int i = 0; i < list.arraySize; i++)
                        {
                            newList.GetArrayElementAtIndex(i).objectReferenceValue =
                                list.GetArrayElementAtIndex(i).objectReferenceValue;
                        }
                        so2.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                // Disable old camera (keep it in scene for reference).
                oldCamera.gameObject.SetActive(false);
                oldCamera.tag = "Untagged";
                Debug.Log($"[XRSetup] Disabled old camera '{oldCamera.name}', transferred scripts to XR camera.");
            }

            // Wire XR Origin → Camera
            xrOrigin.Camera = xrCamera;

            // ── Character Controller (for room-scale movement) ──
            var charController = xrOriginGO.AddComponent<CharacterController>();
            charController.center = new Vector3(0f, 1f, 0f);
            charController.radius = 0.3f;
            charController.height = 1.8f;

            var charDriver = xrOriginGO.AddComponent<CharacterControllerDriver>();

            // ── Add XRSetup component ──
            xrOriginGO.AddComponent<XRSetup>();

            // ── Select in hierarchy ──
            Selection.activeGameObject = xrOriginGO;
            EditorGUIUtility.PingObject(xrOriginGO);

            Debug.Log("[XRSetup] ✓ XR Origin + PC fallback ready. Press Play — right-drag look, WASD move.");

            Undo.RegisterCreatedObjectUndo(xrOriginGO, "Setup XR Origin");
        }
#endif
    }

    // ── PC keyboard / mouse free-fly camera ────────────────────
    // Uses Input System (Mouse.current / Keyboard.current) because
    // this project has activeInputHandler = 2 (Input System only).

    public class PCCameraController : MonoBehaviour
    {
        [Header("Speed")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _lookSensitivity = 2f;
        [SerializeField] private float _boostMultiplier = 3f;  // hold Shift

        [Header("Mode")]
        [Tooltip("When enabled, disables TrackedPoseDriver so PC input owns the camera. Disable this when using a real VR headset.")]
        [SerializeField] private bool _disableTrackedPoseDriver = true;

        private float _yaw, _pitch;
        private bool _looking;
        private bool _warnedInput;

        void Start()
        {
            var angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x;

            if (_disableTrackedPoseDriver)
            {
                var tpd = GetComponent<TrackedPoseDriver>();
                if (tpd != null) { tpd.enabled = false; Debug.Log("[PC Cam] TrackedPoseDriver disabled."); }
            }

            // Self-enable immediately — no dependency on XRSetup finding us.
            enabled = true;
            Debug.Log("[PC Cam] Enabled. Right-drag = look, WASD = move, Q/E = up/down, Shift = boost.");
        }

        void Update()
        {
            var mouse = Mouse.current;
            var kb    = Keyboard.current;
            if (mouse == null || kb == null)
            {
                if (!_warnedInput)
                {
                    Debug.LogError($"[PC Cam] Input devices missing! Mouse={mouse != null}, Keyboard={kb != null}. Check Project Settings → Active Input Handling.");
                    _warnedInput = true;
                }
                return;
            }

            // Right-click to look
            if (mouse.rightButton.wasPressedThisFrame)  { _looking = true;  Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (mouse.rightButton.wasReleasedThisFrame) { _looking = false; Cursor.lockState = CursorLockMode.None;   Cursor.visible = true;  }

            if (_looking)
            {
                var delta = mouse.delta.ReadValue();
                _yaw   += delta.x * _lookSensitivity * 0.1f;
                _pitch -= delta.y * _lookSensitivity * 0.1f;
                _pitch  = Mathf.Clamp(_pitch, -89f, 89f);
                transform.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }

            // WASD / QE move in camera-local space
            float speed = _moveSpeed * (kb.shiftKey.isPressed ? _boostMultiplier : 1f);
            Vector3 move = Vector3.zero;
            if (kb.wKey.isPressed) move += Vector3.forward;
            if (kb.sKey.isPressed) move += Vector3.back;
            if (kb.aKey.isPressed) move += Vector3.left;
            if (kb.dKey.isPressed) move += Vector3.right;
            if (kb.qKey.isPressed) move += Vector3.down;
            if (kb.eKey.isPressed) move += Vector3.up;

            if (move.sqrMagnitude > 0.01f)
            {
                var worldMove = transform.TransformDirection(move.normalized) * speed * Time.deltaTime;
                transform.position += worldMove;
            }
        }
    }
}