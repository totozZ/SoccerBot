// CharacterBuilder.cs — Builds a SpongeBob-style robot from Unity primitives.
// Attach to the Robot GameObject. No external models needed.
// Wires turret sub-parts to RobotVisuals automatically.

using UnityEngine;

namespace SoccerBot
{
    public class CharacterBuilder : MonoBehaviour
    {
        [Header("Body")]
        [SerializeField] private Color _bodyColor  = new Color(1f, 0.85f, 0.15f);
        [SerializeField] private Color _eyeColor   = Color.white;
        [SerializeField] private Color _pupilColor = new Color(0.2f, 0.5f, 1f);
        [SerializeField] private Color _pantsColor = new Color(0.6f, 0.3f, 0.1f);
        [SerializeField] private Color _turretColor = new Color(0.35f, 0.35f, 0.4f);

        [Header("Turret — auto-wired")]
        public Transform hoodTransform;
        public Transform topFlywheel;
        public Transform bottomFlywheel;

        [Header("Build")]
        [Tooltip("Robot=true (生成海绵宝宝). Teammate/Opponent=false (用导入的人物模型，不生成 primitive).")]
        [SerializeField] private bool _buildOnAwake = true;

        void Awake()
        {
            if (!_buildOnAwake) return;
            BuildSpongeBot();
            WireToRobotVisuals();
        }

        void BuildSpongeBot()
        {
            var y = _bodyColor;
            var w = _eyeColor;
            var b = _pupilColor;
            var br = _pantsColor;
            var g = _turretColor;
            var bk = Color.black;
            var r = new Color(0.9f, 0.2f, 0.2f);
            var gr = new Color(0f, 1f, 0.3f);

            // Body
            Box("Body", new Vector3(0f, 0.55f, 0f), new Vector3(0.7f, 0.7f, 0.35f), y);

            // Eyes
            var eL = Sphere("EyeL", new Vector3(-0.18f, 0.9f, 0.16f), 0.12f, w);
            Sphere("PupilL", Vector3.zero, 0.06f, b, eL.transform);
            var eR = Sphere("EyeR", new Vector3(0.18f, 0.9f, 0.16f), 0.12f, w);
            Sphere("PupilR", Vector3.zero, 0.06f, b, eR.transform);

            Sphere("Nose", new Vector3(0f, 0.78f, 0.18f), 0.09f, y);
            Box("Mouth", new Vector3(0f, 0.7f, 0.18f), new Vector3(0.2f, 0.04f, 0.02f), bk);
            Box("Tie", new Vector3(0f, 0.45f, 0.18f), new Vector3(0.08f, 0.2f, 0.03f), r);
            Box("Pants", new Vector3(0f, 0.2f, 0f), new Vector3(0.72f, 0.25f, 0.36f), br);

            Cyl("LegL", new Vector3(-0.15f, 0.05f, 0f), new Vector3(0.1f, 0.12f, 0.1f), w);
            Cyl("LegR", new Vector3(0.15f, 0.05f, 0f), new Vector3(0.1f, 0.12f, 0.1f), w);
            Cyl("ArmL", new Vector3(-0.42f, 0.6f, 0f), new Vector3(0.06f, 0.25f, 0.06f), y);
            Cyl("ArmR", new Vector3(0.42f, 0.6f, 0f), new Vector3(0.06f, 0.25f, 0.06f), y);

            // Turret base
            var turret = Cyl("Turret", new Vector3(0f, 0.95f, 0f), new Vector3(0.22f, 0.1f, 0.22f), g);

            // Hood (pitch pivot for RobotVisuals)
            var hood = Box("Hood", new Vector3(0f, 0.08f, 0.1f), new Vector3(0.18f, 0.06f, 0.18f), g, turret.transform);
            hoodTransform = hood.transform;
            Cyl("Barrel", new Vector3(0f, 0f, 0.12f), new Vector3(0.04f, 0.1f, 0.04f), g, hood.transform);

            // Flywheels
            topFlywheel    = Cyl("FlywheelTop",    new Vector3(0.06f, 0f, 0.1f),  new Vector3(0.06f, 0.02f, 0.06f), g, hood.transform).transform;
            bottomFlywheel = Cyl("FlywheelBottom", new Vector3(-0.06f, 0f, 0.1f), new Vector3(0.06f, 0.02f, 0.06f), g, hood.transform).transform;

            // Limelight dot
            Sphere("Limelight", new Vector3(0f, 0.85f, 0.2f), 0.04f, gr);
        }

        void WireToRobotVisuals()
        {
            var vis = GetComponent<RobotVisuals>();
            if (vis == null) return;
            var t = typeof(RobotVisuals);
            var f = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            t.GetField("_hoodTransform", f)?.SetValue(vis, hoodTransform);
            t.GetField("_topFlywheel", f)?.SetValue(vis, topFlywheel);
            t.GetField("_bottomFlywheel", f)?.SetValue(vis, bottomFlywheel);
        }

        // ── Primitive builders ──────────────────────────────

        GameObject Part(string name, PrimitiveType type, Vector3 pos, Vector3 scale, Color color, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.parent = parent ?? transform;
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;
            // Tint Unity's own default material
            var r = go.GetComponent<Renderer>();
            if (r != null) r.material.color = color;
            Destroy(go.GetComponent<Collider>());
            return go;
        }

        GameObject Box(string n, Vector3 p, Vector3 s, Color c, Transform parent = null)
            => Part(n, PrimitiveType.Cube, p, s, c, parent);

        GameObject Sphere(string n, Vector3 p, float rad, Color c, Transform parent = null)
            => Part(n, PrimitiveType.Sphere, p, Vector3.one * rad, c, parent);

        GameObject Cyl(string n, Vector3 p, Vector3 s, Color c, Transform parent = null)
            => Part(n, PrimitiveType.Cylinder, p, s, c, parent);
    }
}
