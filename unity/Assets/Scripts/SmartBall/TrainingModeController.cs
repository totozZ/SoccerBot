using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SoccerBot
{
    public class TrainingModeController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MainMenuPanel _mainMenu;
        [SerializeField] private CanvasGroup _trainingCanvasGroup;
        [SerializeField] private GameObject _trainingGoalFrame;
        [SerializeField] private TextMeshProUGUI _trainingBodyLabel;

        private enum SmartBallInputMode
        {
            Mock,
            Ble
        }

        [Header("Training Ball")]
        [SerializeField] private SmartBallController _smartBallController;
        [SerializeField] private MockSmartBallSource _mockSource;
        [SerializeField] private BleSmartBallSource _bleSource;
        [SerializeField] private SmartBallInputMode _inputMode = SmartBallInputMode.Mock;

        [Header("Layout")]
        [SerializeField] private Vector3 _ballPosition = new Vector3(0f, 0.22f, 0f);
        [SerializeField] private Vector3 _goalPosition = new Vector3(0f, 1.2f, 5.4f);
        [SerializeField] private Vector3 _fieldCenter = new Vector3(0f, 0f, 0f);
        [SerializeField] private float _fieldWidth = 8f;
        [SerializeField] private float _fieldLength = 12f;
        [SerializeField] private float _fieldForwardOffset = 6f;

        private GameObject _trainingRoot;
        private GameObject _fieldRoot;
        private readonly List<GameObject> _suppressedObjects = new();
        private bool _isActive;

        void Awake()
        {
            AutoResolveRefs();
            EnsureTrainingObjects();
            SetTrainingObjectsActive(false);
        }

        void Update()
        {
            if (!_isActive) return;

            _smartBallController?.Tick();

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                _smartBallController?.ResetOrientation();

            RefreshStatusText();
        }

        public void EnterTrainingMode()
        {
            AutoResolveRefs();
            AlignTrainingLayoutToPlayer();
            EnsureTrainingObjects();
            SuppressNonTrainingSceneObjects();

            _isActive = true;
            SetTrainingObjectsActive(true);
            _smartBallController?.ResetOrientation();
            RefreshStatusText();
        }

        public void ExitTrainingMode()
        {
            _isActive = false;
            RestoreSuppressedSceneObjects();
            SetTrainingObjectsActive(false);
        }

        public void Configure(CanvasGroup trainingCanvasGroup, GameObject trainingGoalFrame, TextMeshProUGUI trainingBodyLabel, MainMenuPanel mainMenu)
        {
            _trainingCanvasGroup = trainingCanvasGroup;
            _trainingGoalFrame = trainingGoalFrame;
            _trainingBodyLabel = trainingBodyLabel;
            _mainMenu = mainMenu;
            AutoResolveRefs();
            EnsureTrainingObjects();
            RefreshStatusText();
        }

        private void AutoResolveRefs()
        {
            if (_mainMenu == null)
                _mainMenu = FindFirstObjectByType<MainMenuPanel>(FindObjectsInactive.Include);
        }

        private void AlignTrainingLayoutToPlayer()
        {
            var player = GameObject.Find("Player");
            if (player == null) return;

            Vector3 forward = player.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            _fieldCenter = player.transform.position + forward * _fieldForwardOffset;
            _ballPosition = _fieldCenter + new Vector3(0f, 0.22f, 0f);
            _goalPosition = _fieldCenter + forward * (_fieldLength * 0.45f) + Vector3.up * 1.2f;
        }

        private void EnsureTrainingObjects()
        {
            if (_trainingRoot == null)
            {
                _trainingRoot = GameObject.Find("TrainingRoot");
                if (_trainingRoot == null)
                    _trainingRoot = new GameObject("TrainingRoot");
            }

            _trainingRoot.transform.position = Vector3.zero;
            EnsureTrainingField();

            if (_trainingGoalFrame != null)
                _trainingGoalFrame.transform.position = _goalPosition;

            if (_smartBallController == null)
            {
                Transform existing = _trainingRoot.transform.Find("SmartBall");
                GameObject smartBallGo = existing != null ? existing.gameObject : CreateSmartBall();
                _smartBallController = smartBallGo.GetComponent<SmartBallController>();
                _mockSource = smartBallGo.GetComponent<MockSmartBallSource>();
                _bleSource = smartBallGo.GetComponent<BleSmartBallSource>();
                ApplySelectedSource();
            }
            else
            {
                _smartBallController.transform.position = _ballPosition;
                _mockSource ??= _smartBallController.GetComponent<MockSmartBallSource>();
                _bleSource ??= _smartBallController.GetComponent<BleSmartBallSource>();
                ApplySelectedSource();
            }
        }

        private void EnsureTrainingField()
        {
            if (_fieldRoot == null)
            {
                Transform existing = _trainingRoot.transform.Find("TrainingField");
                if (existing != null)
                    _fieldRoot = existing.gameObject;
            }

            if (_fieldRoot != null)
            {
                _fieldRoot.transform.position = _fieldCenter;
                return;
            }

            _fieldRoot = new GameObject("TrainingField");
            _fieldRoot.transform.SetParent(_trainingRoot.transform, false);
            _fieldRoot.transform.position = _fieldCenter;

            CreateFieldGround();
            CreateFieldLines();
            CreateFieldCenterCircle();
            CreateFieldSpot();
        }

        private void CreateFieldGround()
        {
            var grass = GameObject.CreatePrimitive(PrimitiveType.Cube);
            grass.name = "Grass";
            grass.transform.SetParent(_fieldRoot.transform, false);
            grass.transform.localPosition = new Vector3(0f, -0.03f, 0f);
            grass.transform.localScale = new Vector3(_fieldWidth, 0.06f, _fieldLength);

            var renderer = grass.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.material.color = new Color(0.18f, 0.55f, 0.22f, 1f);
            }

            int stripeCount = 8;
            float stripeLength = _fieldLength / stripeCount;
            for (int i = 0; i < stripeCount; i++)
            {
                if (i % 2 == 0) continue;

                var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stripe.name = $"GrassStripe_{i}";
                stripe.transform.SetParent(_fieldRoot.transform, false);
                stripe.transform.localPosition = new Vector3(0f, 0.001f, -_fieldLength * 0.5f + stripeLength * (i + 0.5f));
                stripe.transform.localScale = new Vector3(_fieldWidth, 0.002f, stripeLength);

                var stripeRenderer = stripe.GetComponent<Renderer>();
                if (stripeRenderer != null)
                    stripeRenderer.material.color = new Color(0.21f, 0.62f, 0.27f, 1f);

                Destroy(stripe.GetComponent<Collider>());
            }
        }

        private void CreateFieldLines()
        {
            float halfW = _fieldWidth * 0.5f;
            float halfL = _fieldLength * 0.5f;
            float lineY = 0.01f;
            float lineWidth = 0.08f;

            CreateLineBar("TouchLineTop", new Vector3(0f, lineY, halfL), new Vector3(_fieldWidth, 0.01f, lineWidth));
            CreateLineBar("TouchLineBottom", new Vector3(0f, lineY, -halfL), new Vector3(_fieldWidth, 0.01f, lineWidth));
            CreateLineBar("SideLineLeft", new Vector3(-halfW, lineY, 0f), new Vector3(lineWidth, 0.01f, _fieldLength));
            CreateLineBar("SideLineRight", new Vector3(halfW, lineY, 0f), new Vector3(lineWidth, 0.01f, _fieldLength));
            CreateLineBar("HalfwayLine", new Vector3(0f, lineY, 0f), new Vector3(_fieldWidth, 0.01f, lineWidth));
        }

        private void CreateFieldCenterCircle()
        {
            var circle = new GameObject("CenterCircle");
            circle.transform.SetParent(_fieldRoot.transform, false);

            var renderer = circle.AddComponent<LineRenderer>();
            renderer.useWorldSpace = false;
            renderer.loop = true;
            renderer.positionCount = 48;
            renderer.startWidth = 0.06f;
            renderer.endWidth = 0.06f;
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.startColor = Color.white;
            renderer.endColor = Color.white;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            float radius = 1f;
            for (int i = 0; i < 48; i++)
            {
                float t = i / 48f * Mathf.PI * 2f;
                renderer.SetPosition(i, new Vector3(Mathf.Cos(t) * radius, 0.015f, Mathf.Sin(t) * radius));
            }
        }

        private void CreateFieldSpot()
        {
            var spot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spot.name = "CenterSpot";
            spot.transform.SetParent(_fieldRoot.transform, false);
            spot.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            spot.transform.localScale = new Vector3(0.08f, 0.005f, 0.08f);
            Destroy(spot.GetComponent<Collider>());

            var renderer = spot.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = Color.white;
        }

        private void CreateLineBar(string name, Vector3 localPosition, Vector3 localScale)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = name;
            bar.transform.SetParent(_fieldRoot.transform, false);
            bar.transform.localPosition = localPosition;
            bar.transform.localScale = localScale;
            Destroy(bar.GetComponent<Collider>());

            var renderer = bar.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = Color.white;
        }

        private GameObject CreateSmartBall()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = "SmartBall";
            root.transform.SetParent(_trainingRoot.transform, false);
            root.transform.position = _ballPosition;

            var renderer = root.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.material.color = new Color(0.96f, 0.96f, 0.94f, 1f);
            }

            BuildSoccerBallPatches(root.transform);

            _mockSource = root.AddComponent<MockSmartBallSource>();
            _bleSource = root.AddComponent<BleSmartBallSource>();
            _smartBallController = root.AddComponent<SmartBallController>();
            ApplySelectedSource();
            return root;
        }

        private void ApplySelectedSource()
        {
            if (_smartBallController == null) return;

            ISmartBallSource source = _inputMode == SmartBallInputMode.Ble
                ? _bleSource as ISmartBallSource
                : _mockSource;

            _smartBallController.SetSource(source);
        }

        private void BuildSoccerBallPatches(Transform ballRoot)
        {
            Vector3[] patchDirs =
            {
                Vector3.up,
                Vector3.down,
                Vector3.forward,
                Vector3.back,
                Vector3.left,
                Vector3.right,
                new Vector3(1f, 1f, 0f).normalized,
                new Vector3(-1f, 1f, 0f).normalized,
                new Vector3(0f, 1f, 1f).normalized,
                new Vector3(0f, 1f, -1f).normalized,
            };

            for (int i = 0; i < patchDirs.Length; i++)
            {
                var patch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                patch.name = $"Patch_{i}";
                patch.transform.SetParent(ballRoot, false);
                patch.transform.localScale = new Vector3(0.12f, 0.012f, 0.12f);
                patch.transform.localPosition = patchDirs[i] * 0.47f;
                patch.transform.localRotation = Quaternion.FromToRotation(Vector3.up, patchDirs[i]);
                Destroy(patch.GetComponent<Collider>());

                var renderer = patch.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material.color = new Color(0.08f, 0.08f, 0.08f, 1f);
            }
        }

        private void SuppressNonTrainingSceneObjects()
        {
            RestoreSuppressedSceneObjects();

            string[] names =
            {
                "Robot",
                "Teammate",
                "Opponent",
                "Ball",
                "ScenarioPlayer",
                "GameManager"
            };

            foreach (string name in names)
            {
                var go = GameObject.Find(name);
                if (go != null && go.activeSelf)
                {
                    _suppressedObjects.Add(go);
                    go.SetActive(false);
                }
            }
        }

        private void RestoreSuppressedSceneObjects()
        {
            foreach (var go in _suppressedObjects)
            {
                if (go != null)
                    go.SetActive(true);
            }
            _suppressedObjects.Clear();
        }

        private void SetTrainingObjectsActive(bool active)
        {
            if (_trainingRoot != null)
                _trainingRoot.SetActive(active);
            if (_trainingGoalFrame != null)
                _trainingGoalFrame.SetActive(active);
            if (_trainingCanvasGroup != null)
                _trainingCanvasGroup.gameObject.SetActive(active);
        }

        private void RefreshStatusText()
        {
            if (_trainingBodyLabel == null || _smartBallController == null) return;

            SmartBallData data = _smartBallController.CurrentData;
            Vector3 euler = data.EulerAngles;
            Vector3 spin = data.angularVelocity;
            Vector3 pos = data.position;

            _trainingBodyLabel.text =
                "Smart football training mode is active.\n" +
                $"Source: {data.sourceName}\n" +
                $"Status: {(data.isConnected ? "Connected" : "Disconnected")}\n" +
                $"Position: {pos.x:0.00}, {pos.y:0.00}, {pos.z:0.00}\n" +
                $"Rotation: {euler.x:0}/{euler.y:0}/{euler.z:0} deg\n" +
                $"Spin: {spin.x:0}/{spin.y:0}/{spin.z:0} deg/s\n" +
                $"Input mode: {_inputMode}\n" +
                "Press R to reset orientation.\n" +
                "Training mode now owns its own field and moving mock ball.";
        }
    }
}
