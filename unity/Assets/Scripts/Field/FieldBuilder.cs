// FieldBuilder.cs — Builds a procedural soccer field at runtime.
// Style matches CharacterBuilder: pure primitives + LineRenderer, zero external assets.
//
// Layout (centered on origin, robot starts at z = -1.5):
//   width (x):  ±halfWidth   default 2.0
//   length (z): ±halfLength  default 3.0
//   goals at z = +halfLength and z = -halfLength
//
// Attach to an empty GameObject "Field" or use SoccerBot/Build Field menu.

using UnityEngine;

namespace SoccerBot
{
    public class FieldBuilder : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private const string BaseMapName = "_BaseMap";
        private const string MainTexName = "_MainTex";

        [Header("Dimensions (meters)")]
        [SerializeField] public float _halfWidth = 6.0f;
        [SerializeField] public float _halfLength = 9.0f;
        [SerializeField] private float _centerCircleRadius = 1.8f;

        [Header("Colors")]
        [SerializeField] private Color _grassColor      = new Color(0.18f, 0.55f, 0.22f);
        [SerializeField] private Color _grassStripeColor = new Color(0.22f, 0.62f, 0.26f);
        [SerializeField] private Color _lineColor       = Color.white;
        [SerializeField] private Color _goalColor       = Color.white;
        [SerializeField] private Color _goalNetColor    = new Color(0.85f, 0.85f, 0.85f, 0.4f);

        [Header("Visual")]
        [SerializeField] private float _lineWidth = 0.12f;
        [SerializeField] private float _lineY     = 0.011f;   // Just above ground
        [SerializeField] private int   _circleSegments = 36;
        [SerializeField] private int   _stripeCount = 16;

        [Header("Goal")]
        [SerializeField] private float _goalWidth     = 3.5f;
        [SerializeField] private float _goalHeight    = 1.5f;
        [SerializeField] private float _goalDepth     = 1.0f;
        [SerializeField] private float _postThickness = 0.12f;

        [Header("Grass Material (optional)")]
        [Tooltip("有就用这个材质（保留贴图），没有就退回纯色 _grassColor。")]
        [SerializeField] public Material _grassMaterial;

        void Awake() => Build();

        public void Build()
        {
            // Clear any previous build (idempotent — supports rebuilding in editor)
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying) Destroy(transform.GetChild(i).gameObject);
                else DestroyImmediate(transform.GetChild(i).gameObject);
            }

            BuildGrass();
            BuildStripes();
            BuildLines();
            BuildGoal(+_halfLength, 0f);          // far goal (robot shoots at this)
            BuildGoal(-_halfLength, 180f);        // near goal (behind robot)
        }

        // ── Grass plane ─────────────────────────────────────

        void BuildGrass()
        {
            var grass = GameObject.CreatePrimitive(PrimitiveType.Cube);
            grass.name = "Grass";
            grass.transform.SetParent(transform, false);
            // Slightly thick slab so shadows look right; top at y=0
            float thickness = 0.02f;
            grass.transform.localPosition = new Vector3(0f, -thickness * 0.5f, 0f);
            grass.transform.localScale = new Vector3(_halfWidth * 2f, thickness, _halfLength * 2f);
            var r = grass.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = _grassMaterial != null
                    ? _grassMaterial
                    : CreateLitMaterial("Generated Grass", _grassColor, 0f, 0.22f);
                ConfigureGrassMaterial(r.sharedMaterial);
            }
            // Keep the collider — ground physics
            grass.isStatic = true;
        }

        // ── Mowed stripes (alternating shade quads) ─────────

        void BuildStripes()
        {
            float stripeLength = (_halfLength * 2f) / _stripeCount;
            Material stripeMat = CreateTransparentMaterial("Mowed Stripe", _grassStripeColor, 0.18f);
            for (int i = 0; i < _stripeCount; i++)
            {
                if (i % 2 == 0) continue;   // every other strip
                var stripe = GameObject.CreatePrimitive(PrimitiveType.Quad);
                stripe.name = $"Stripe_{i}";
                stripe.transform.SetParent(transform, false);
                Destroy(stripe.GetComponent<Collider>());

                float zStart = -_halfLength + i * stripeLength;
                float zCenter = zStart + stripeLength * 0.5f;

                stripe.transform.localPosition = new Vector3(0f, _lineY * 0.5f, zCenter);
                stripe.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                stripe.transform.localScale = new Vector3(_halfWidth * 2f, stripeLength, 1f);

                var rend = stripe.GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial = stripeMat;
            }
        }

        // ── White lines ─────────────────────────────────────

        void BuildLines()
        {
            // Outer rectangle (closed loop)
            DrawLine("Sideline", new[]
            {
                new Vector3(-_halfWidth, _lineY, -_halfLength),
                new Vector3( _halfWidth, _lineY, -_halfLength),
                new Vector3( _halfWidth, _lineY,  _halfLength),
                new Vector3(-_halfWidth, _lineY,  _halfLength),
                new Vector3(-_halfWidth, _lineY, -_halfLength),
            });

            // Halfway line
            DrawLine("Halfway", new[]
            {
                new Vector3(-_halfWidth, _lineY, 0f),
                new Vector3( _halfWidth, _lineY, 0f),
            });

            // Center circle
            var circle = new Vector3[_circleSegments + 1];
            for (int i = 0; i <= _circleSegments; i++)
            {
                float a = (i / (float)_circleSegments) * Mathf.PI * 2f;
                circle[i] = new Vector3(
                    Mathf.Cos(a) * _centerCircleRadius,
                    _lineY,
                    Mathf.Sin(a) * _centerCircleRadius);
            }
            DrawLine("CenterCircle", circle);

            // Center spot
            var spot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spot.name = "CenterSpot";
            spot.transform.SetParent(transform, false);
            spot.transform.localPosition = new Vector3(0f, _lineY, 0f);
            spot.transform.localScale = new Vector3(0.1f, 0.005f, 0.1f);
            Destroy(spot.GetComponent<Collider>());
            var sr = spot.GetComponent<Renderer>();
            if (sr != null) sr.material.color = _lineColor;
        }

        void DrawLine(string name, Vector3[] points)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = points.Length;
            lr.SetPositions(points);
            lr.startWidth = _lineWidth;
            lr.endWidth = _lineWidth;
            lr.material = CreateUnlitMaterial("Pitch Line", _lineColor);
            lr.startColor = _lineColor;
            lr.endColor = _lineColor;
            lr.numCornerVertices = 2;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
        }

        // ── Goal frames ─────────────────────────────────────
        // posZ: where the goal sits along Z. yawDeg: 0 means goal opening faces -Z.

        void BuildGoal(float posZ, float yawDeg)
        {
            var goal = new GameObject($"Goal_{(posZ > 0 ? "Far" : "Near")}");
            goal.transform.SetParent(transform, false);
            goal.transform.localPosition = new Vector3(0f, 0f, posZ);
            goal.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);

            float halfW = _goalWidth * 0.5f;
            float t = _postThickness;

            // Two upright posts
            Bar(goal, "PostL", new Vector3(-halfW, _goalHeight * 0.5f, 0f),
                new Vector3(t, _goalHeight, t), _goalColor);
            Bar(goal, "PostR", new Vector3( halfW, _goalHeight * 0.5f, 0f),
                new Vector3(t, _goalHeight, t), _goalColor);

            // Crossbar
            Bar(goal, "Crossbar", new Vector3(0f, _goalHeight, 0f),
                new Vector3(_goalWidth + t, t, t), _goalColor);

            // Back depth: top-back and bottom-back rails on each side, plus rear top bar
            Bar(goal, "BackTopL", new Vector3(-halfW, _goalHeight, _goalDepth * 0.5f),
                new Vector3(t, t, _goalDepth), _goalColor);
            Bar(goal, "BackTopR", new Vector3( halfW, _goalHeight, _goalDepth * 0.5f),
                new Vector3(t, t, _goalDepth), _goalColor);
            Bar(goal, "BackPostL", new Vector3(-halfW, _goalHeight * 0.5f, _goalDepth),
                new Vector3(t, _goalHeight, t), _goalColor);
            Bar(goal, "BackPostR", new Vector3( halfW, _goalHeight * 0.5f, _goalDepth),
                new Vector3(t, _goalHeight, t), _goalColor);
            Bar(goal, "BackCrossbar", new Vector3(0f, _goalHeight, _goalDepth),
                new Vector3(_goalWidth + t, t, t), _goalColor);

            // Net hint: a translucent slab inside the goal box (cheap, no mesh tessellation)
            var net = GameObject.CreatePrimitive(PrimitiveType.Cube);
            net.name = "NetHint";
            net.transform.SetParent(goal.transform, false);
            net.transform.localPosition = new Vector3(0f, _goalHeight * 0.5f, _goalDepth * 0.95f);
            net.transform.localScale = new Vector3(_goalWidth, _goalHeight - t, 0.02f);
            Destroy(net.GetComponent<Collider>());
            var nr = net.GetComponent<Renderer>();
            if (nr != null)
            {
                nr.sharedMaterial = CreateTransparentMaterial("Goal Net Hint", _goalNetColor, _goalNetColor.a);
            }
        }

        static void Bar(GameObject parent, string name, Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = CreateLitMaterial("Goal Frame", color, 0.15f, 0.38f);
            // Keep collider on posts so the ball can bounce
        }

        private static void ConfigureGrassMaterial(Material mat)
        {
            if (mat == null) return;
            SetColor(mat, _StaticGrassTint);
            SetTextureScale(mat, BaseMapName, new Vector2(6f, 6f));
            SetTextureScale(mat, MainTexName, new Vector2(6f, 6f));
            SetFloat(mat, BumpScaleId, 0.45f);
            SetFloat(mat, SmoothnessId, 0.22f);
            SetFloat(mat, GlossinessId, 0.22f);
            SetFloat(mat, MetallicId, 0f);
        }

        private static readonly Color _StaticGrassTint = new(0.18f, 0.55f, 0.22f, 1f);

        private static Material CreateLitMaterial(string name, Color color, float metallic, float smoothness)
        {
            Shader shader = null;
            if (!UsingBuiltInRenderPipeline())
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            }
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            Material mat = new Material(shader) { name = name };
            SetColor(mat, color);
            SetFloat(mat, MetallicId, metallic);
            SetFloat(mat, SmoothnessId, smoothness);
            SetFloat(mat, GlossinessId, smoothness);
            return mat;
        }

        private static Material CreateUnlitMaterial(string name, Color color)
        {
            Shader shader = null;
            if (!UsingBuiltInRenderPipeline())
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            Material mat = new Material(shader) { name = name };
            SetColor(mat, color);
            return mat;
        }

        private static Material CreateTransparentMaterial(string name, Color color, float alpha)
        {
            Shader shader = Shader.Find("Sprites/Default");
            Material mat = new Material(shader) { name = name };
            color.a = alpha;
            SetColor(mat, color);
            mat.renderQueue = 3000;
            return mat;
        }

        private static void SetColor(Material mat, Color color)
        {
            if (mat == null) return;
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, color);
            if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, color);
        }

        private static void SetFloat(Material mat, int propertyId, float value)
        {
            if (mat != null && mat.HasProperty(propertyId))
                mat.SetFloat(propertyId, value);
        }

        private static void SetTextureScale(Material mat, string propertyName, Vector2 value)
        {
            if (mat != null && mat.HasProperty(propertyName))
                mat.SetTextureScale(propertyName, value);
        }

        private static bool UsingBuiltInRenderPipeline()
        {
            return UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null
                && QualitySettings.renderPipeline == null;
        }
    }
}
