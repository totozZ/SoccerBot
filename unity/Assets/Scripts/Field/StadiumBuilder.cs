// StadiumBuilder.cs — Builds a procedural mini ring stadium around the pitch.
// Same philosophy as FieldBuilder: pure primitives, zero external assets, runs at
// runtime (Awake) or from the SoccerBot/Build Stadium editor menu.
//
// Layout (centered on origin, matching the Field):
//   An elliptical bowl of raked seating tiers encircles the pitch, with a low
//   perimeter wall (advertising boards), four floodlight pylons at the corners,
//   and a thin canopy roof ring. Seat blocks are tinted from a fan-colour palette
//   so the stands read as a crowd without any extra spectator meshes — cheap
//   enough for Quest, where the SRP Batcher folds the lit cubes into few draws.
//
// Attach to an empty GameObject "Stadium" or use SoccerBot/Build Stadium menu.

using UnityEngine;

namespace SoccerBot
{
    public class StadiumBuilder : MonoBehaviour
    {
        [Header("Footprint (meters) — should clear the pitch")]
        [Tooltip("Pitch half-width (X). Field default is 6.")]
        [SerializeField] private float _pitchHalfWidth = 6.0f;
        [Tooltip("Pitch half-length (Z). Field default is 9.")]
        [SerializeField] private float _pitchHalfLength = 9.0f;
        [Tooltip("Flat gap between the pitch edge and the first seating tier (the 'track').")]
        [SerializeField] private float _trackGap = 1.6f;

        [Header("Seating Bowl")]
        [SerializeField] private int   _segments  = 40;   // cubes per ring (higher = smoother oval)
        [SerializeField] private int   _tiers     = 4;    // rows of seating, each higher + further back
        [SerializeField] private float _tierRise  = 0.9f; // vertical step per tier
        [SerializeField] private float _tierDepth = 1.3f; // radial depth of each seat row
        [SerializeField] private float _rakeDeg   = 16f;  // backward lean of each seat block
        [SerializeField] private float _segOverlap = 1.12f; // widen blocks slightly so the ring has no gaps

        [Header("Perimeter Wall (advertising boards)")]
        [SerializeField] private float _wallHeight = 0.5f;
        [SerializeField] private Color _wallColor  = new Color(0.92f, 0.92f, 0.95f);

        [Header("Floodlight Pylons")]
        [SerializeField] private bool  _buildPylons   = true;
        [SerializeField] private float _pylonHeight   = 8.5f;
        [SerializeField] private float _lampHeadLift  = 2.0f;
        [SerializeField] private float _pylonInset    = 1.2f;  // how far outside the top tier
        [SerializeField] private Color _pylonColor    = new Color(0.25f, 0.27f, 0.3f);
        [SerializeField] private Color _lampColor     = new Color(1f, 0.96f, 0.8f);

        [Header("Canopy Roof")]
        [SerializeField] private bool  _buildRoof  = true;
        [SerializeField] private Color _roofColor  = new Color(0.18f, 0.2f, 0.24f);

        [Header("Crowd Palette (seat tints)")]
        [Tooltip("Seat blocks are randomly tinted from these to fake a packed crowd.")]
        [SerializeField] private Color[] _crowdPalette =
        {
            new Color(0.20f, 0.35f, 0.75f),  // blue fans
            new Color(0.80f, 0.20f, 0.22f),  // red fans
            new Color(0.90f, 0.90f, 0.92f),  // white shirts
            new Color(0.95f, 0.78f, 0.15f),  // gold/yellow
            new Color(0.30f, 0.32f, 0.36f),  // empty/dark seats
        };

        void Awake() => Build();

        public void Build()
        {
            // Idempotent: clear any previous build so re-running replaces cleanly.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying) Destroy(transform.GetChild(i).gameObject);
                else DestroyImmediate(transform.GetChild(i).gameObject);
            }

            float baseRx = _pitchHalfWidth  + _trackGap;
            float baseRz = _pitchHalfLength + _trackGap;

            BuildWall(baseRx, baseRz);
            BuildBowl(baseRx, baseRz);
            if (_buildRoof)   BuildRoof(baseRx, baseRz);
            if (_buildPylons) BuildPylons(baseRx, baseRz);
        }

        // ── Seating bowl: stacked elliptical rings of raked blocks ──

        void BuildBowl(float baseRx, float baseRz)
        {
            var bowl = new GameObject("SeatingBowl");
            bowl.transform.SetParent(transform, false);

            for (int t = 0; t < _tiers; t++)
            {
                float rx = baseRx + t * _tierDepth;
                float rz = baseRz + t * _tierDepth;
                float y  = _wallHeight + t * _tierRise + _tierRise * 0.5f;

                var tierGO = new GameObject($"Tier_{t}");
                tierGO.transform.SetParent(bowl.transform, false);

                for (int s = 0; s < _segments; s++)
                {
                    float a  = (s / (float)_segments) * Mathf.PI * 2f;
                    float an = ((s + 1) / (float)_segments) * Mathf.PI * 2f;
                    Vector3 p     = new Vector3(Mathf.Cos(a)  * rx, y, Mathf.Sin(a)  * rz);
                    Vector3 pNext = new Vector3(Mathf.Cos(an) * rx, y, Mathf.Sin(an) * rz);

                    float segWidth = Vector3.Distance(p, pNext) * _segOverlap;
                    Vector3 outward = new Vector3(p.x, 0f, p.z).normalized;
                    if (outward == Vector3.zero) outward = Vector3.forward;

                    // LookRotation(outward): local +Z faces out, local +X runs tangentially.
                    // Extra rake tilts the block's top backward for a real stadium lean.
                    Quaternion rot = Quaternion.LookRotation(outward, Vector3.up)
                                     * Quaternion.Euler(_rakeDeg, 0f, 0f);

                    Color seat = _crowdPalette != null && _crowdPalette.Length > 0
                        ? _crowdPalette[SeatHash(t, s) % _crowdPalette.Length]
                        : new Color(0.3f, 0.32f, 0.36f);

                    var seg = Block(tierGO, $"Seat_{t}_{s}", p, rot,
                        new Vector3(segWidth, _tierRise * 1.15f, _tierDepth), seat);
                    Destroy(seg.GetComponent<Collider>());   // no physics needed on seats
                }
            }
        }

        // ── Low perimeter wall just outside the pitch ──

        void BuildWall(float baseRx, float baseRz)
        {
            var wall = new GameObject("PerimeterWall");
            wall.transform.SetParent(transform, false);

            float rx = baseRx - 0.3f;
            float rz = baseRz - 0.3f;
            for (int s = 0; s < _segments; s++)
            {
                float a  = (s / (float)_segments) * Mathf.PI * 2f;
                float an = ((s + 1) / (float)_segments) * Mathf.PI * 2f;
                Vector3 p     = new Vector3(Mathf.Cos(a)  * rx, _wallHeight * 0.5f, Mathf.Sin(a)  * rz);
                Vector3 pNext = new Vector3(Mathf.Cos(an) * rx, 0f, Mathf.Sin(an) * rz);

                float segWidth = Vector3.Distance(new Vector3(p.x,0,p.z), pNext) * _segOverlap;
                Vector3 outward = new Vector3(p.x, 0f, p.z).normalized;
                if (outward == Vector3.zero) outward = Vector3.forward;
                Quaternion rot = Quaternion.LookRotation(outward, Vector3.up);

                Block(wall, $"Board_{s}", p, rot,
                    new Vector3(segWidth, _wallHeight, 0.15f), _wallColor);
                // Keep the collider so the ball bounces off the boards.
            }
        }

        // ── Thin canopy ring leaning in over the top tier ──

        void BuildRoof(float baseRx, float baseRz)
        {
            var roof = new GameObject("Canopy");
            roof.transform.SetParent(transform, false);

            float rx = baseRx + (_tiers - 0.2f) * _tierDepth;
            float rz = baseRz + (_tiers - 0.2f) * _tierDepth;
            float y  = _wallHeight + _tiers * _tierRise + 0.6f;

            for (int s = 0; s < _segments; s++)
            {
                float a  = (s / (float)_segments) * Mathf.PI * 2f;
                float an = ((s + 1) / (float)_segments) * Mathf.PI * 2f;
                Vector3 p     = new Vector3(Mathf.Cos(a)  * rx, y, Mathf.Sin(a)  * rz);
                Vector3 pNext = new Vector3(Mathf.Cos(an) * rx, y, Mathf.Sin(an) * rz);

                float segWidth = Vector3.Distance(p, pNext) * _segOverlap;
                Vector3 outward = new Vector3(p.x, 0f, p.z).normalized;
                if (outward == Vector3.zero) outward = Vector3.forward;
                // Tilt the canopy down-and-inward over the seats.
                Quaternion rot = Quaternion.LookRotation(outward, Vector3.up)
                                 * Quaternion.Euler(-35f, 0f, 0f);

                var slab = Block(roof, $"Roof_{s}", p, rot,
                    new Vector3(segWidth, 0.1f, _tierDepth * 1.6f), _roofColor);
                Destroy(slab.GetComponent<Collider>());
            }
        }

        // ── Four floodlight pylons at the diagonal corners ──

        void BuildPylons(float baseRx, float baseRz)
        {
            var pylons = new GameObject("Pylons");
            pylons.transform.SetParent(transform, false);

            float rx = baseRx + _tiers * _tierDepth + _pylonInset;
            float rz = baseRz + _tiers * _tierDepth + _pylonInset;
            float headY = _wallHeight + _tiers * _tierRise + 1.0f + _lampHeadLift;

            // Corners at ±45°-ish around the ellipse.
            float[] angles = { 45f, 135f, 225f, 315f };
            foreach (float deg in angles)
            {
                float a = deg * Mathf.Deg2Rad;
                Vector3 basePos = new Vector3(Mathf.Cos(a) * rx, 0f, Mathf.Sin(a) * rz);
                var mast = new GameObject($"Pylon_{deg:F0}");
                mast.transform.SetParent(pylons.transform, false);
                mast.transform.localPosition = basePos;

                // Mast (tapered look via a thin tall cube)
                Block(mast, "Mast", new Vector3(0f, _pylonHeight * 0.5f, 0f),
                    Quaternion.identity, new Vector3(0.3f, _pylonHeight, 0.3f), _pylonColor);

                // Lamp head: a box angled to face the pitch center.
                Vector3 toCenter = (-basePos); toCenter.y = 0f;
                Quaternion headRot = toCenter.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(toCenter.normalized, Vector3.up) * Quaternion.Euler(25f, 0f, 0f)
                    : Quaternion.identity;
                var head = Block(mast, "LampHead", new Vector3(0f, headY, 0f), headRot,
                    new Vector3(1.6f, 0.8f, 0.25f), _pylonColor);
                Destroy(head.GetComponent<Collider>());

                // Glowing lamp face (emissive-ish bright panel) on the pitch side.
                var lamp = Block(head, "Lamps", new Vector3(0f, 0f, 0.16f), Quaternion.identity,
                    new Vector3(1.4f, 0.6f, 0.05f), _lampColor);
                Destroy(lamp.GetComponent<Collider>());
                var lr = lamp.GetComponent<Renderer>();
                if (lr != null)
                {
                    lr.material.color = _lampColor;
                    lr.material.EnableKeyword("_EMISSION");
                    lr.material.SetColor("_EmissionColor", _lampColor * 1.5f);
                }

                var floodLight = lamp.AddComponent<Light>();
                floodLight.type = LightType.Spot;
                floodLight.color = _lampColor;
                floodLight.intensity = 1.25f;
                floodLight.range = 18f;
                floodLight.spotAngle = 75f;
                floodLight.innerSpotAngle = 40f;
                floodLight.shadows = LightShadows.None;
                floodLight.transform.localPosition = new Vector3(0f, 0f, 0.12f);
                floodLight.transform.localRotation = Quaternion.identity;
            }
        }

        // ── Helpers ──

        // Deterministic pseudo-random seat index so the crowd pattern is stable
        // across rebuilds but still looks scattered.
        static int SeatHash(int tier, int seg)
        {
            int h = (tier * 73856093) ^ (seg * 19349663);
            return Mathf.Abs(h);
        }

        static GameObject Block(GameObject parent, string name, Vector3 localPos,
                                Quaternion localRot, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            go.isStatic = true;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.material.color = color;
            return go;
        }
    }
}
