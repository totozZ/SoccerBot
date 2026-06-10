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
        [SerializeField] private Color _lampColor     = new Color(1f, 0.94f, 0.72f);
        [SerializeField, Range(0f, 1f)] private float _beamAlpha = 0.16f;

        [Header("Hexagram Dome Lights (six-point star, cold white)")]
        [SerializeField] private bool  _buildHexagramDome = true;
        [SerializeField] private float _domeApexHeight = 5.6f;   // highest point at dome centre
        [SerializeField] private float _domeRimDrop    = 1.5f;   // how much lower the star tips sit
        [SerializeField] private float _hexRadiusScale = 0.92f;  // star tip radius vs pitch footprint
        [SerializeField] private int   _hexArmDots     = 9;      // LED dots per radial arm
        [SerializeField] private int   _hexEdgeDots    = 7;      // LED dots per hexagram edge
        [SerializeField] private float _hexDotScale    = 0.16f;
        [SerializeField] private float _hexEmission    = 5f;      // self-glow brightness of each dot
        [SerializeField] private Color _hexLightColor = new Color(0.82f, 0.9f, 1f);        // cold white
        [SerializeField] private Color _hexGlowColor  = new Color(0.6f, 0.82f, 1f, 0.5f);
        [SerializeField] private float _hexWaveSpeed   = 2.6f;   // radial wave travel speed
        [SerializeField] private float _hexWaveDensity = 6.5f;   // radial wave band tightness
        [Tooltip("Soft, even pitch fill from the dome apex (0 = off). Separate from the dotted belt.")]
        [SerializeField] private float _domeFillIntensity = 1.3f;

        [Header("Canopy Roof")]
        [SerializeField] private bool  _buildRoof  = true;
        [SerializeField] private Color _roofColor  = new Color(0.18f, 0.2f, 0.24f);

        [Header("Crowd Palette (seat tints)")]
        [Tooltip("Seat blocks are randomly tinted from these to fake a packed crowd.")]
        [SerializeField] private Color[] _crowdPalette =
        {
            new Color(0.46f, 0.67f, 0.86f),  // Argentina sky blue
            new Color(0.92f, 0.94f, 0.96f),  // Germany / Argentina white
            new Color(0.08f, 0.09f, 0.10f),  // Germany black
            new Color(0.92f, 0.68f, 0.16f),  // warm stadium highlights
            new Color(0.26f, 0.28f, 0.33f),  // dark seats / shadow
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
            if (_buildHexagramDome) BuildHexagramDomeLights(baseRx, baseRz);
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
                    lr.material.SetColor("_EmissionColor", _lampColor * 2.5f);
                }

                var floodLight = lamp.AddComponent<Light>();
                floodLight.type = LightType.Spot;
                floodLight.color = _lampColor;
                floodLight.intensity = 4.2f;
                floodLight.range = 34f;
                floodLight.spotAngle = 95f;
                floodLight.innerSpotAngle = 45f;
                floodLight.shadows = LightShadows.None;
                floodLight.transform.localPosition = new Vector3(0f, 0f, 0.12f);
                floodLight.transform.localRotation = Quaternion.identity;

                BuildPylonBeam(lamp.transform, _lampColor);
            }
        }

        void BuildPylonBeam(Transform parent, Color sourceColor)
        {
            if (_beamAlpha <= 0f) return;

            BuildBeamQuad(parent, "BroadcastBeam_A", sourceColor, Quaternion.Euler(90f, 0f, 0f));
            BuildBeamQuad(parent, "BroadcastBeam_B", sourceColor, Quaternion.Euler(90f, 0f, 90f));
        }

        void BuildBeamQuad(Transform parent, string name, Color sourceColor, Quaternion localRotation)
        {
            var beam = GameObject.CreatePrimitive(PrimitiveType.Quad);
            beam.name = name;
            beam.transform.SetParent(parent, false);
            beam.transform.localPosition = new Vector3(0f, 0f, 3.8f);
            beam.transform.localRotation = localRotation;
            beam.transform.localScale = new Vector3(1.9f, 7.8f, 1f);
            Destroy(beam.GetComponent<Collider>());

            var renderer = beam.GetComponent<Renderer>();
            if (renderer == null) return;

            Color color = sourceColor;
            color.a = _beamAlpha;
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = color;
            mat.renderQueue = 3000;
            renderer.material = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // ── Hexagram dome: a six-point star of dotted LED clusters on the ceiling ──
        // Six radial arms blast out from a bright apex, and two interlocking
        // triangles connect the tips into a Star-of-David outline. Every dot rides
        // a radial wave so a band of cold-white light keeps expanding from centre.

        void BuildHexagramDomeLights(float baseRx, float baseRz)
        {
            var dome = new GameObject("HexagramDomeLights");
            dome.transform.SetParent(transform, false);

            float starRx = baseRx * _hexRadiusScale;
            float starRz = baseRz * _hexRadiusScale;
            float apexY  = _domeApexHeight;
            Vector3 pitchTarget = new Vector3(0f, 0.05f, 0f);

            // Six outer tips, one every 60°. Two triangles → {30,150,270} & {90,210,330}.
            float[] tipDeg = { 30f, 90f, 150f, 210f, 270f, 330f };
            Vector3[] tips = new Vector3[6];
            for (int k = 0; k < 6; k++)
                tips[k] = ArmPoint(tipDeg[k] * Mathf.Deg2Rad, 1f, starRx, starRz, apexY);

            // Bright apex cluster at the dome centre (self-glow only).
            PlaceLed(dome.transform, "HexApex", new Vector3(0f, apexY, 0f), 0f,
                     _hexDotScale * 1.6f, pitchTarget);

            // One soft, broad downlight from the apex gives the pitch an even fill
            // without the harsh glare of per-dot spotlights.
            if (_domeFillIntensity > 0f)
            {
                var fillGO = new GameObject("DomeFillLight");
                fillGO.transform.SetParent(dome.transform, false);
                fillGO.transform.localPosition = new Vector3(0f, apexY, 0f);
                fillGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // straight down
                var fill = fillGO.AddComponent<Light>();
                fill.type = LightType.Spot;
                fill.color = _hexLightColor;
                fill.intensity = _domeFillIntensity;
                fill.range = apexY + 10f;
                fill.spotAngle = 140f;
                fill.innerSpotAngle = 80f;
                fill.shadows = LightShadows.None;
            }

            // Six radial arms — the "radial" star burst from centre to each tip.
            int armDots = Mathf.Max(2, _hexArmDots);
            for (int arm = 0; arm < 6; arm++)
            {
                float ang = tipDeg[arm] * Mathf.Deg2Rad;
                for (int d = 1; d <= armDots; d++)
                {
                    float frac = d / (float)armDots;
                    Vector3 pos = ArmPoint(ang, frac, starRx, starRz, apexY);
                    float scale = Mathf.Lerp(_hexDotScale * 0.7f, _hexDotScale * 1.25f, frac);
                    PlaceLed(dome.transform, $"HexArm{arm}_{d}", pos, frac, scale, pitchTarget);
                }
            }

            // Two triangles connecting alternating tips → the hexagram outline.
            int[][] triangles =
            {
                new[] { 1, 3, 5 },   // tips at 90, 210, 330
                new[] { 0, 2, 4 },   // tips at 30, 150, 270
            };
            int edgeDots = Mathf.Max(2, _hexEdgeDots);
            for (int tri = 0; tri < triangles.Length; tri++)
            {
                int[] t = triangles[tri];
                for (int e = 0; e < 3; e++)
                {
                    Vector3 a = tips[t[e]];
                    Vector3 b = tips[t[(e + 1) % 3]];
                    for (int s = 1; s < edgeDots; s++)
                    {
                        float u = s / (float)edgeDots;
                        Vector3 pos = Vector3.Lerp(a, b, u);
                        float radialT = Mathf.Clamp01(
                            new Vector2(pos.x / Mathf.Max(0.001f, starRx),
                                        pos.z / Mathf.Max(0.001f, starRz)).magnitude);
                        pos.y = DomeHeight(radialT, apexY);   // re-seat onto the dome curve
                        PlaceLed(dome.transform, $"HexEdge{tri}_{e}_{s}", pos, radialT, _hexDotScale, pitchTarget);
                    }
                }
            }

            BuildDomeFog(dome.transform, starRx, starRz, apexY);
        }

        // Dome surface height: highest at centre, dipping toward the rim.
        float DomeHeight(float radialFrac, float apexY) => apexY - _domeRimDrop * radialFrac * radialFrac;

        Vector3 ArmPoint(float ang, float frac, float starRx, float starRz, float apexY)
        {
            return new Vector3(Mathf.Cos(ang) * starRx * frac,
                               DomeHeight(frac, apexY),
                               Mathf.Sin(ang) * starRz * frac);
        }

        // Emissive dots only — they glow locally (and bloom in post) but cast NO
        // real light, so the pitch never gets blown out by the ceiling lighting.
        void PlaceLed(Transform parent, string name, Vector3 pos, float radialT,
                      float scale, Vector3 target)
        {
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = name;
            dot.transform.SetParent(parent, false);
            dot.transform.localPosition = pos;
            dot.transform.localScale = Vector3.one * scale;
            Destroy(dot.GetComponent<Collider>());

            var renderer = dot.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = _hexLightColor;
                renderer.material.EnableKeyword("_EMISSION");
                renderer.material.SetColor("_EmissionColor", _hexLightColor * _hexEmission);
            }

            var pulse = dot.AddComponent<HexRadialPulse>();
            pulse.Configure(_hexLightColor, _hexEmission * 0.45f, _hexEmission * 1.3f,
                            radialT, _hexWaveSpeed, _hexWaveDensity);

            BuildDomeGlow(parent, pos, target, 1f - radialT * 0.3f);
        }

        void BuildDomeGlow(Transform parent, Vector3 pos, Vector3 target, float alphaScale)
        {
            var glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            glow.name = "HexGlow";
            glow.transform.SetParent(parent, false);
            glow.transform.localPosition = pos;
            Vector3 toTarget = target - pos;
            glow.transform.localRotation = toTarget.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(toTarget.normalized, Vector3.up)
                : Quaternion.identity;
            glow.transform.localScale = Vector3.one * 1.1f;
            Destroy(glow.GetComponent<Collider>());

            var renderer = glow.GetComponent<Renderer>();
            if (renderer == null) return;

            var color = _hexGlowColor;
            color.a *= alphaScale;
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = color;
            renderer.material = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // Glowing motes drifting in the air under the dome — fake volumetric fog.
        void BuildDomeFog(Transform parent, float starRx, float starRz, float apexY)
        {
            const int particleCount = 56;
            for (int i = 0; i < particleCount; i++)
            {
                float a = (i / (float)particleCount) * Mathf.PI * 2f;
                float radiusJitter = 0.25f + (SeatHash(7, i) % 100) / 130f;   // ~0.25..1.0
                Vector3 pos = new Vector3(
                    Mathf.Cos(a) * starRx * radiusJitter,
                    2.0f + (SeatHash(11, i) % 100) / 100f * Mathf.Max(0.5f, apexY - 2.4f),
                    Mathf.Sin(a) * starRz * radiusJitter);

                var particle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                particle.name = $"DomeMote_{i:00}";
                particle.transform.SetParent(parent, false);
                particle.transform.localPosition = pos;
                particle.transform.localScale = Vector3.one * (0.03f + (SeatHash(13, i) % 60) / 1200f);
                Destroy(particle.GetComponent<Collider>());

                var renderer = particle.GetComponent<Renderer>();
                if (renderer == null) continue;
                renderer.material.color = _hexLightColor;
                renderer.material.EnableKeyword("_EMISSION");
                renderer.material.SetColor("_EmissionColor", _hexLightColor * 2.2f);
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

    // Drives each LED dot's emission so a bright band of light keeps sweeping
    // outward from the dome centre — a radial wave keyed off the dot's radius.
    public class HexRadialPulse : MonoBehaviour
    {
        private Renderer _renderer;
        private Color _color;
        private float _minEmission;
        private float _maxEmission;
        private float _radialT;
        private float _speed;
        private float _density;

        public void Configure(Color color, float minEmission, float maxEmission,
                              float radialT, float speed, float density)
        {
            _color = color;
            _minEmission = minEmission;
            _maxEmission = maxEmission;
            _radialT = radialT;
            _speed = speed;
            _density = density;
            _renderer = GetComponent<Renderer>();
            Apply(0f);
        }

        void Awake()
        {
            if (_renderer == null) _renderer = GetComponent<Renderer>();
        }

        void Update()
        {
            Apply(Time.time);
        }

        private void Apply(float time)
        {
            if (_renderer == null || _renderer.material == null) return;
            // Phase advances with time but lags with radius, so the bright band
            // travels from the centre outward (an expanding radial wave).
            float wave = 0.5f + 0.5f * Mathf.Sin(time * _speed - _radialT * _density);
            float emission = Mathf.Lerp(_minEmission, _maxEmission, wave);
            _renderer.material.SetColor("_EmissionColor", _color * emission);
        }
    }
}
