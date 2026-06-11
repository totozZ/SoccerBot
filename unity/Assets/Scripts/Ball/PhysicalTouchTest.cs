using UnityEngine;
using UnityEngine.InputSystem;

namespace SoccerBot
{
    [DefaultExecutionOrder(-10)]
    public class PhysicalTouchTest : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _player;
        [SerializeField] private QuestControllerLegRig _legRig;
        [SerializeField] private BallController _ball;
        [SerializeField] private PhysicalBallInteractor _ballInteractor;

        [Header("Mode")]
        [SerializeField] private bool _enterOnStart = true;
        [SerializeField] private bool _createRigIfMissing = true;
        [SerializeField] private bool _createBallIfMissing = true;
        [SerializeField] private bool _routeContactsToMatchFlow = false;
        [SerializeField] private bool _debugLogs = true;
        [SerializeField] private bool _showOverlay = true;

        [Header("Reset Layout")]
        [SerializeField] private Vector3 _ballLocalOffset = new Vector3(0f, 0.35f, 1.15f);
        [SerializeField] private float _resetCooldown = 0.15f;
        [SerializeField] private bool _ensureTestGround = true;
        [SerializeField] private Vector3 _groundSize = new Vector3(4f, 0.08f, 5f);
        [SerializeField] private float _groundForwardOffset = 1.4f;

        [Header("Debug Drawing")]
        [SerializeField] private float _contactRayDuration = 2f;
        [SerializeField] private float _contactRayLength = 0.7f;

        private Rigidbody _ballBody;
        private GameObject _testGround;
        private PhysicalBallInteractor _subscribedInteractor;
        private float _lastResetTime = -999f;
        private float _lastContactTime = -999f;
        private float _lastImpulseTime = -999f;
        private Vector3 _lastContactPoint;
        private Vector3 _lastSwingDirection;
        private Vector3 _lastImpulseDirection;
        private string _lastContactSummary = "No foot contact yet.";
        private string _lastImpulseSummary = "No physical impulse yet.";
        private bool _isActive;

        private void Start()
        {
            if (_enterOnStart)
                EnterTestMode();
        }

        private void OnEnable()
        {
            TrackedLegController.GlobalFootContact += HandleFootContact;
            BindInteractorEvents();
        }

        private void OnDisable()
        {
            TrackedLegController.GlobalFootContact -= HandleFootContact;
            UnbindInteractorEvents();
        }

        private void Update()
        {
            if (!_isActive)
                return;

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                ResetBall();

            DrawRecentContact();
        }

        private void OnGUI()
        {
            if (!_isActive || !_showOverlay)
                return;

            const float width = 560f;
            const float height = 116f;
            Rect rect = new Rect(18f, 18f, width, height);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 10f, width - 24f, 22f), "Physical Touch Test  |  Press R to reset ball");
            GUI.Label(new Rect(rect.x + 12f, rect.y + 38f, width - 24f, 22f), _lastContactSummary);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 66f, width - 24f, 22f), _lastImpulseSummary);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 94f, width - 24f, 18f), $"Ball velocity: {FormatVector(_ballBody != null ? _ballBody.linearVelocity : Vector3.zero)}");
        }

        [ContextMenu("Enter Test Mode")]
        public void EnterTestMode()
        {
            ResolveReferences();
            if (_legRig == null || _ball == null)
            {
                Debug.LogWarning("[PhysicalTouchTest] Missing player leg rig or ball. Test mode was not entered.");
                return;
            }

            LayerMask ballLayer = 1 << _ball.gameObject.layer;
            _legRig.SetFootBallInteraction(true, true, ballLayer);
            _ballInteractor = _ball.GetComponent<PhysicalBallInteractor>();
            if (_ballInteractor == null)
                _ballInteractor = _ball.gameObject.AddComponent<PhysicalBallInteractor>();
            _ballInteractor.ConfigureRouting(_routeContactsToMatchFlow, true, _debugLogs);
            BindInteractorEvents();

            _ballBody = _ball.EnsurePhysicsComponents();
            EnsureGround();
            ResetBall();
            _isActive = true;

            if (_debugLogs)
                Debug.Log("[PhysicalTouchTest] Active. Kick the physical ball with tracked feet; press R to reset.");
        }

        [ContextMenu("Exit Test Mode")]
        public void ExitTestMode()
        {
            _isActive = false;
            if (_ball != null)
                _ball.SetPhysicalSimulation(false);
            if (_testGround != null)
                _testGround.SetActive(false);
        }

        [ContextMenu("Reset Ball")]
        public void ResetBall()
        {
            if (Time.time - _lastResetTime < _resetCooldown)
                return;

            ResolveReferences();
            if (_ball == null)
                return;

            _lastResetTime = Time.time;
            _ballBody = _ball.EnsurePhysicsComponents();

            Vector3 position = GetWorldBallResetPosition();
            _ball.SetPhysicalSimulation(true, true);
            _ball.transform.position = position;
            _ball.transform.rotation = Quaternion.identity;
            if (_ballBody != null)
            {
                _ballBody.position = position;
                _ballBody.rotation = Quaternion.identity;
                _ballBody.linearVelocity = Vector3.zero;
                _ballBody.angularVelocity = Vector3.zero;
            }

            EnsureGround();
            _lastContactSummary = "Ball reset. Waiting for foot contact.";
            _lastImpulseSummary = "No physical impulse yet.";
        }

        private void ResolveReferences()
        {
            if (_player == null)
            {
                GameObject playerGo = GameObject.Find("Player");
                if (playerGo != null)
                    _player = playerGo.transform;
            }

            if (_legRig == null && _player != null)
                _legRig = _player.GetComponent<QuestControllerLegRig>();
            if (_legRig == null && _createRigIfMissing && _player != null)
                _legRig = _player.gameObject.AddComponent<QuestControllerLegRig>();
            if (_legRig == null)
                _legRig = FindFirstObjectByType<QuestControllerLegRig>(FindObjectsInactive.Include);

            if (_ball == null)
                _ball = FindFirstObjectByType<BallController>(FindObjectsInactive.Include);
            if (_ball == null && _createBallIfMissing)
                _ball = CreateTestBall();

            if (_ball != null)
            {
                _ballInteractor = _ball.GetComponent<PhysicalBallInteractor>();
                _ballBody = _ball.GetComponent<Rigidbody>();
            }
        }

        private BallController CreateTestBall()
        {
            GameObject ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballGo.name = "Ball";
            ballGo.transform.localScale = Vector3.one * 0.24f;

            Renderer renderer = ballGo.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(0.96f, 0.95f, 0.9f, 1f);

            if (ballGo.GetComponent<TrailRenderer>() == null)
                ballGo.AddComponent<TrailRenderer>();

            return ballGo.AddComponent<BallController>();
        }

        private Vector3 GetWorldBallResetPosition()
        {
            Transform basis = _player != null ? _player : transform;
            Vector3 forward = basis.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            Vector3 right = new Vector3(forward.z, 0f, -forward.x).normalized;
            return basis.position
                + right * _ballLocalOffset.x
                + Vector3.up * _ballLocalOffset.y
                + forward * _ballLocalOffset.z;
        }

        private void EnsureGround()
        {
            if (!_ensureTestGround)
                return;

            if (_testGround == null)
            {
                _testGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _testGround.name = "PhysicalTouchTestGround";
                _testGround.layer = 2;
                Renderer renderer = _testGround.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material.color = new Color(0.12f, 0.45f, 0.18f, 1f);
            }

            Transform basis = _player != null ? _player : transform;
            Vector3 forward = basis.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            _testGround.SetActive(true);
            _testGround.transform.position = basis.position + forward * _groundForwardOffset + Vector3.down * (_groundSize.y * 0.5f);
            _testGround.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            _testGround.transform.localScale = _groundSize;
        }

        private void BindInteractorEvents()
        {
            if (_subscribedInteractor == _ballInteractor)
                return;

            UnbindInteractorEvents();
            _subscribedInteractor = _ballInteractor;
            if (_subscribedInteractor != null)
                _subscribedInteractor.PhysicalImpulseApplied += HandlePhysicalImpulse;
        }

        private void UnbindInteractorEvents()
        {
            if (_subscribedInteractor != null)
                _subscribedInteractor.PhysicalImpulseApplied -= HandlePhysicalImpulse;
            _subscribedInteractor = null;
        }

        private void HandleFootContact(FootContactData data)
        {
            if (!_isActive || !IsThisBall(data))
                return;

            _lastContactTime = Time.time;
            _lastContactPoint = data.ContactPoint;
            _lastSwingDirection = data.SwingDirection.sqrMagnitude > 0.0001f ? data.SwingDirection.normalized : Vector3.forward;
            _lastContactSummary =
                $"Contact {data.Foot}: speed={data.ContactSpeed:0.00} power={data.Power01:0.00} accuracy={data.Accuracy01:0.00} point={FormatVector(data.ContactPoint)}";

            if (_debugLogs)
                Debug.Log($"[PhysicalTouchTest] {_lastContactSummary}");
        }

        private void HandlePhysicalImpulse(FootContactData data, Vector3 direction, float impulse)
        {
            if (!_isActive || !IsThisBall(data))
                return;

            _lastImpulseTime = Time.time;
            _lastImpulseDirection = direction;
            _lastImpulseSummary = $"Impulse: {impulse:0.00} dir={FormatVector(direction)}";
        }

        private bool IsThisBall(FootContactData data)
        {
            if (_ball == null || data.BallCollider == null)
                return false;

            if (_ballBody != null && data.BallBody == _ballBody)
                return true;

            return data.BallCollider.transform == _ball.transform || data.BallCollider.transform.IsChildOf(_ball.transform);
        }

        private void DrawRecentContact()
        {
            if (Time.time - _lastContactTime <= _contactRayDuration)
                Debug.DrawRay(_lastContactPoint, _lastSwingDirection * _contactRayLength, Color.yellow);
            if (Time.time - _lastImpulseTime <= _contactRayDuration)
                Debug.DrawRay(_lastContactPoint, _lastImpulseDirection * _contactRayLength, Color.green);
        }

        private static string FormatVector(Vector3 value)
        {
            return $"{value.x:0.00},{value.y:0.00},{value.z:0.00}";
        }
    }
}
