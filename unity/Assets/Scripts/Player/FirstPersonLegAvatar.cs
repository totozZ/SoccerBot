using UnityEngine;
using UnityEngine.InputSystem;

namespace SoccerBot
{
    [DefaultExecutionOrder(80)]
    public class FirstPersonLegAvatar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FPSPlayerController _player;
        [SerializeField] private Camera _viewCamera;

        [Header("Body")]
        [SerializeField] private Vector3 _bodyOffset = new(0f, -0.78f, 0.1f);
        [SerializeField] private float _hipWidth = 0.34f;
        [SerializeField] private float _thighLength = 0.48f;
        [SerializeField] private float _shinLength = 0.48f;
        [SerializeField] private float _legRadius = 0.055f;
        [SerializeField] private float _footLength = 0.34f;
        [SerializeField] private float _footWidth = 0.14f;

        [Header("Motion Shot")]
        [SerializeField] private float _visualFullBackswing = 0.55f;
        [SerializeField] private float _visualFullForward = 0.42f;
        [SerializeField] private float _followThroughDuration = 0.32f;
        [SerializeField] private float _returnSpeed = 9f;

        [Header("Colors")]
        [SerializeField] private Color _shortsColor = new(0.08f, 0.22f, 0.85f, 1f);
        [SerializeField] private Color _sockColor = new(0.92f, 0.95f, 1f, 1f);
        [SerializeField] private Color _bootColor = new(0.04f, 0.04f, 0.045f, 1f);
        [SerializeField] private Color _accentColor = new(1f, 0.84f, 0.12f, 1f);

        private Transform _rigRoot;
        private Transform _leftThigh;
        private Transform _leftShin;
        private Transform _leftFoot;
        private Transform _rightThigh;
        private Transform _rightShin;
        private Transform _rightFoot;
        private Transform _rightToeAccent;

        private Material _shortsMaterial;
        private Material _sockMaterial;
        private Material _bootMaterial;
        private Material _accentMaterial;

        private InputAction _rightTriggerAction;
        private InputAction _rightHandPositionAction;
        private InputAction _rightHandVelocityAction;

        private bool _trackingMotionShot;
        private Vector3 _motionStartHandPos;
        private Vector3 _motionLastHandPos;
        private float _motionBackswing;
        private float _motionForwardTravel;
        private float _visualKick;
        private float _followThroughTimer;
        private float _chargePower;

        public void Configure(FPSPlayerController player, Camera viewCamera)
        {
            _player = player;
            _viewCamera = viewCamera;
        }

        private void Awake()
        {
            if (_player == null) _player = GetComponent<FPSPlayerController>();
            if (_viewCamera == null) _viewCamera = GetComponentInChildren<Camera>(true);
            BuildAvatar();
        }

        private void OnEnable()
        {
            if (_player != null)
            {
                _player.OnChargeChanged += HandleChargeChanged;
                _player.OnChargeCancel += HandleChargeCancel;
                _player.OnShoot += HandleShoot;
            }

            _rightTriggerAction = new InputAction("LegAvatarRightTrigger", InputActionType.Button);
            _rightTriggerAction.AddBinding("<XRController>{RightHand}/triggerPressed");
            _rightTriggerAction.Enable();

            _rightHandPositionAction = new InputAction("LegAvatarRightHandPosition", InputActionType.Value, expectedControlType: "Vector3");
            _rightHandPositionAction.AddBinding("<XRController>{RightHand}/devicePosition");
            _rightHandPositionAction.Enable();

            _rightHandVelocityAction = new InputAction("LegAvatarRightHandVelocity", InputActionType.Value, expectedControlType: "Vector3");
            _rightHandVelocityAction.AddBinding("<XRController>{RightHand}/deviceVelocity");
            _rightHandVelocityAction.Enable();
        }

        private void OnDisable()
        {
            if (_player != null)
            {
                _player.OnChargeChanged -= HandleChargeChanged;
                _player.OnChargeCancel -= HandleChargeCancel;
                _player.OnShoot -= HandleShoot;
            }

            DisposeAction(ref _rightTriggerAction);
            DisposeAction(ref _rightHandPositionAction);
            DisposeAction(ref _rightHandVelocityAction);
        }

        private void LateUpdate()
        {
            if (_viewCamera == null) return;

            UpdateRigPose();
            UpdateMotionInput();
            UpdateKickBlend();
            PoseLegs();
        }

        private void BuildAvatar()
        {
            if (_rigRoot != null) return;

            _shortsMaterial = CreateMaterial("FP Legs Shorts", _shortsColor);
            _sockMaterial = CreateMaterial("FP Legs Socks", _sockColor);
            _bootMaterial = CreateMaterial("FP Legs Boots", _bootColor);
            _accentMaterial = CreateMaterial("FP Legs Accent", _accentColor);

            _rigRoot = new GameObject("FirstPersonLegAvatar").transform;
            _rigRoot.SetParent(transform, false);

            _leftThigh = CreateLimb("LeftThigh", _shortsMaterial);
            _leftShin = CreateLimb("LeftShin", _sockMaterial);
            _leftFoot = CreateFoot("LeftBoot", false);
            _rightThigh = CreateLimb("RightThigh", _shortsMaterial);
            _rightShin = CreateLimb("RightShin", _sockMaterial);
            _rightFoot = CreateFoot("RightBoot", true);
            _rightToeAccent = CreateAccent("RightBootStrikeAccent");
        }

        private Transform CreateLimb(string name, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.SetParent(_rigRoot, false);
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            return go.transform;
        }

        private Transform CreateFoot(string name, bool right)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(_rigRoot, false);
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = _bootMaterial;

            var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = right ? "RightBootSideStripe" : "LeftBootSideStripe";
            stripe.transform.SetParent(go.transform, false);
            stripe.transform.localPosition = new Vector3(right ? -0.52f : 0.52f, 0.05f, 0.12f);
            stripe.transform.localScale = new Vector3(0.08f, 0.18f, 0.62f);
            var stripeCollider = stripe.GetComponent<Collider>();
            if (stripeCollider != null) Destroy(stripeCollider);
            var stripeRenderer = stripe.GetComponent<Renderer>();
            if (stripeRenderer != null) stripeRenderer.sharedMaterial = _accentMaterial;

            return go.transform;
        }

        private Transform CreateAccent(string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(_rigRoot, false);
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = _accentMaterial;
            return go.transform;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader) { name = name, color = color };
            return material;
        }

        private void UpdateRigPose()
        {
            Vector3 forward = _viewCamera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;
            forward.Normalize();

            Quaternion yaw = Quaternion.LookRotation(forward, Vector3.up);
            _rigRoot.position = _viewCamera.transform.position + yaw * _bodyOffset;
            _rigRoot.rotation = yaw;
        }

        private void UpdateMotionInput()
        {
            if (_rightTriggerAction == null)
                return;

            if (_rightTriggerAction.WasPressedThisFrame() && TryReadRightHandPosition(out Vector3 startPos))
            {
                _trackingMotionShot = true;
                _motionStartHandPos = startPos;
                _motionLastHandPos = startPos;
                _motionBackswing = 0f;
                _motionForwardTravel = 0f;
            }

            if (!_trackingMotionShot)
                return;

            if (TryReadRightHandPosition(out Vector3 handPos))
            {
                Vector3 aim = _viewCamera != null ? _viewCamera.transform.forward : transform.forward;
                aim.Normalize();

                float signedTravel = Vector3.Dot(handPos - _motionStartHandPos, aim);
                _motionBackswing = Mathf.Max(_motionBackswing, -signedTravel);
                _motionForwardTravel = Mathf.Max(_motionForwardTravel, signedTravel);

                Vector3 velocity = ReadRightHandVelocity(handPos);
                float forwardSpeedCue = Mathf.Max(0f, Vector3.Dot(velocity, aim)) * 0.16f;
                _motionForwardTravel = Mathf.Max(_motionForwardTravel, forwardSpeedCue);
                _motionLastHandPos = handPos;
            }

            if (_rightTriggerAction.WasReleasedThisFrame())
            {
                _trackingMotionShot = false;
                _followThroughTimer = _followThroughDuration;
            }
        }

        private void UpdateKickBlend()
        {
            float target = 0f;

            if (_trackingMotionShot)
            {
                float back = Mathf.InverseLerp(0f, _visualFullBackswing, _motionBackswing);
                float forward = Mathf.InverseLerp(0f, _visualFullForward, _motionForwardTravel);
                target = Mathf.Clamp(forward - back * 0.72f, -1f, 1f);
            }
            else if (_followThroughTimer > 0f)
            {
                _followThroughTimer -= Time.deltaTime;
                float u = 1f - Mathf.Clamp01(_followThroughTimer / _followThroughDuration);
                target = Mathf.Sin(u * Mathf.PI) * 1.15f;
            }
            else if (_player != null && _player.IsCharging)
            {
                target = -Mathf.Clamp01(_chargePower) * 0.55f;
            }

            _visualKick = Mathf.Lerp(_visualKick, target, Time.deltaTime * _returnSpeed);
        }

        private void PoseLegs()
        {
            float plantedSway = Mathf.Sin(Time.time * 2.2f) * 0.015f;
            PoseLeg(false, -_hipWidth * 0.5f, -_visualKick * 0.12f + plantedSway, 0.08f);
            PoseLeg(true, _hipWidth * 0.5f, _visualKick, 0f);
        }

        private void PoseLeg(bool right, float hipX, float kick, float plantedForward)
        {
            Vector3 hip = new(hipX, 0f, 0f);
            float absKick = Mathf.Abs(kick);
            float forwardKick = Mathf.Max(0f, kick);
            float backKick = Mathf.Max(0f, -kick);

            Vector3 knee = hip + new Vector3(
                0f,
                -_thighLength + absKick * 0.08f,
                plantedForward + 0.08f + forwardKick * 0.24f - backKick * 0.16f);

            Vector3 ankle = hip + new Vector3(
                0f,
                -_thighLength - _shinLength + forwardKick * 0.16f,
                plantedForward + 0.02f + forwardKick * 0.62f - backKick * 0.28f);

            Vector3 footCenter = ankle + new Vector3(0f, -0.035f + forwardKick * 0.025f, 0.16f + forwardKick * 0.16f);
            float footPitch = Mathf.Lerp(6f, -24f, forwardKick) + backKick * 18f;

            Transform thigh = right ? _rightThigh : _leftThigh;
            Transform shin = right ? _rightShin : _leftShin;
            Transform foot = right ? _rightFoot : _leftFoot;

            SetCapsuleBetween(thigh, hip, knee, _legRadius);
            SetCapsuleBetween(shin, knee, ankle, _legRadius * 0.92f);
            foot.localPosition = footCenter;
            foot.localRotation = Quaternion.Euler(footPitch, 0f, 0f);
            foot.localScale = new Vector3(_footWidth, 0.075f, _footLength);

            if (right && _rightToeAccent != null)
            {
                _rightToeAccent.localPosition = footCenter + foot.localRotation * new Vector3(0f, 0.045f, _footLength * 0.55f);
                _rightToeAccent.localRotation = foot.localRotation;
                _rightToeAccent.localScale = new Vector3(_footWidth * 0.72f, 0.025f, 0.055f);
            }
        }

        private static void SetCapsuleBetween(Transform capsule, Vector3 a, Vector3 b, float radius)
        {
            Vector3 delta = b - a;
            float length = Mathf.Max(delta.magnitude, 0.01f);
            capsule.localPosition = (a + b) * 0.5f;
            capsule.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            float diameter = radius * 2f;
            capsule.localScale = new Vector3(diameter, length * 0.5f, diameter);
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

        private void HandleChargeChanged(float power01)
        {
            _chargePower = power01;
        }

        private void HandleChargeCancel()
        {
            _trackingMotionShot = false;
            _motionBackswing = 0f;
            _motionForwardTravel = 0f;
            _chargePower = 0f;
            _followThroughTimer = 0f;
        }

        private void HandleShoot(float power01, Vector3 direction)
        {
            _trackingMotionShot = false;
            _chargePower = 0f;
            _followThroughTimer = Mathf.Lerp(_followThroughDuration * 0.65f, _followThroughDuration, Mathf.Clamp01(power01));
        }

        private static void DisposeAction(ref InputAction action)
        {
            if (action == null) return;
            action.Disable();
            action.Dispose();
            action = null;
        }
    }
}
