// CrowdAnimator.cs — Fills the StadiumBuilder's seating bowl with a packed,
// procedurally-built low-poly crowd (no textures, no imported models).
//
// - A single "seated figure" mesh (head + torso) is combined once from primitives.
// - One figure is placed on every seat slot across the bowl, coloured from the
//   seat's own fan-palette tint and bucketed by colour so the whole crowd draws in
//   ~5 GPU-instanced batches (cheap on Quest).
// - Subtle idle bob every frame; a "stand up and cheer" jump on goals.
// - Confetti burst, floodlight flash and crowd-cheer audio on goals (unchanged).
//
// Attach to the Stadium GameObject.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SoccerBot
{
    public class CrowdAnimator : MonoBehaviour
    {
        [Header("Crowd Figures")]
        [Tooltip("Spectators placed across each seat segment (higher = more packed).")]
        [SerializeField] private int   _peoplePerSeat = 6;
        [Tooltip("Height of a figure in metres (head-to-seat).")]
        [SerializeField] private float _figureScale   = 0.45f;
        [Tooltip("Random per-figure size variation (0 = uniform).")]
        [SerializeField] private float _scaleJitter   = 0.12f;
        [Tooltip("Sideways scatter along the seat row as a fraction of seat width.")]
        [SerializeField] private float _rowScatter    = 0.12f;
        [Tooltip("Also seat people on the dark 'empty' palette colour (off = sparser, realistic).")]
        [SerializeField] private bool  _fillDarkSeats = true;

        [Header("Idle Motion")]
        [SerializeField] private float _idleAmplitude = 0.025f;
        [SerializeField] private float _idleFrequency = 2.2f;

        [Header("Goal Cheer (stand up + jump)")]
        [SerializeField] private float _cheerJumpHeight = 0.18f;
        [SerializeField] private float _cheerDuration   = 1.2f;

        [Header("Confetti (on goal)")]
        [SerializeField] private int   _confettiCount      = 25;
        [SerializeField] private float _confettiLifetime    = 2.5f;
        [SerializeField] private float _confettiSpawnY      = 5f;
        [SerializeField] private float _confettiSpawnRadius = 4f;

        [Header("Spot Light Flash (on goal)")]
        [SerializeField] private float _flashDuration       = 0.5f;
        [SerializeField] private float _flashIntensityBoost = 0.6f;
        [SerializeField] private int   _flashCount          = 2;

        [Header("Audio")]
        [SerializeField] private AudioClip[] _crowdCheerClips;
        [SerializeField, Range(0f, 1f)] private float _cheerVolume = 0.8f;

        // One bucket per distinct seat colour; every bucket draws in instanced batches.
        private class CrowdBucket
        {
            public Color        color;
            public Material     material;
            public Vector3[]    basePos;
            public Quaternion[] rot;
            public Vector3[]    scale;
            public float[]      phase;
            public Matrix4x4[]  work;
            public int          count;
        }

        private const int InstanceBatch = 1023;          // GPU instancing per-call cap

        private Mesh                  _figureMesh;
        private Shader                _figureShader;      // reused from the seats (build-safe)
        private List<CrowdBucket>     _buckets   = new List<CrowdBucket>();
        private Matrix4x4[]           _drawBuffer = new Matrix4x4[InstanceBatch];
        private float                 _cheerLevel;        // 0..1 envelope, drives the goal jump
        private Coroutine             _cheerRoutine;
        private bool                  _crowdBuilt;

        private ParticleSystem    _confettiPs;
        private Light[]           _spotLights;
        private float[]           _spotBaseIntensities;
        private AudioSource       _sfxSource;
        private Coroutine         _flashRoutine;

        void Awake()
        {
            _sfxSource = gameObject.GetComponent<AudioSource>();
            if (_sfxSource == null)
                _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.spatialBlend = 1f;
            _sfxSource.maxDistance = 50f;

            _figureMesh = BuildSeatedFigureMesh();
            _confettiPs = BuildConfettiSystem();
        }

        void Start()
        {
            EnsureCrowdBuilt();
            var pylons = transform.Find("Pylons");
            if (pylons != null)
            {
                var all = pylons.GetComponentsInChildren<Light>();
                _spotLights = all;
                _spotBaseIntensities = new float[all.Length];
                for (int i = 0; i < all.Length; i++)
                    _spotBaseIntensities[i] = all[i].intensity;
            }

            var sp = FindFirstObjectByType<ScenarioPlayer>();
            if (sp != null)
                sp.OnScenarioComplete += OnScenarioComplete;
        }

        void OnDestroy()
        {
            var sp = FindFirstObjectByType<ScenarioPlayer>();
            if (sp != null)
                sp.OnScenarioComplete -= OnScenarioComplete;
        }

        void Update()
        {
            if (!_crowdBuilt || _figureMesh == null) return;

            float t = Time.time;
            for (int b = 0; b < _buckets.Count; b++)
            {
                var bucket = _buckets[b];
                for (int i = 0; i < bucket.count; i++)
                {
                    float ph  = bucket.phase[i];
                    float bob = Mathf.Sin(t * _idleFrequency + ph) * _idleAmplitude;
                    float cheer = _cheerLevel * _cheerJumpHeight
                                  * (0.5f + 0.5f * Mathf.Sin(t * 9f + ph * 1.7f));
                    Vector3 pos = bucket.basePos[i];
                    pos.y += bob + cheer;
                    bucket.work[i] = Matrix4x4.TRS(pos, bucket.rot[i], bucket.scale[i]);
                }
                DrawBucket(bucket);
            }
        }

        private void DrawBucket(CrowdBucket bucket)
        {
            int drawn = 0;
            while (drawn < bucket.count)
            {
                int n = Mathf.Min(InstanceBatch, bucket.count - drawn);
                Matrix4x4[] src;
                if (drawn == 0 && n == bucket.count)
                {
                    src = bucket.work;                 // common case: whole bucket fits one batch
                }
                else
                {
                    System.Array.Copy(bucket.work, drawn, _drawBuffer, 0, n);
                    src = _drawBuffer;
                }
                Graphics.DrawMeshInstanced(_figureMesh, 0, bucket.material, src, n,
                    null, ShadowCastingMode.Off, true);
                drawn += n;
            }
        }

        private void OnScenarioComplete(Scenario s)
        {
            if (s == null) return;
            if (s.outcome == ScenarioOutcome.Score)
            {
                TriggerConfetti();
                FlashSpotLights();
                PlayCrowdCheer();
                TriggerCrowdCheerJump();
            }
        }

        // ── Crowd construction ──────────────────────────────────────────────

        private void EnsureCrowdBuilt()
        {
            if (_crowdBuilt) return;
            var bowl = transform.Find("SeatingBowl");
            if (bowl == null) return;
            BuildCrowd(bowl);
            _crowdBuilt = true;
        }

        private void BuildCrowd(Transform bowl)
        {
            if (bowl == null) return;

            // Accumulate per-colour lists, then bake into arrays.
            var colors  = new List<Color>();
            var posL    = new List<List<Vector3>>();
            var rotL    = new List<List<Quaternion>>();
            var scaleL  = new List<List<Vector3>>();
            var phaseL  = new List<List<float>>();
            int total   = 0;

            var allSeats = bowl.GetComponentsInChildren<Transform>();
            foreach (var seat in allSeats)
            {
                if (!seat.name.StartsWith("Seat_")) continue;

                var rend = seat.GetComponent<Renderer>();
                if (rend == null || rend.sharedMaterial == null) continue;
                Color seatColor = rend.sharedMaterial.color;
                if (_figureShader == null) _figureShader = rend.sharedMaterial.shader;
                if (!_fillDarkSeats && IsDarkSeat(seatColor)) continue;

                // Find (or create) the colour bucket for this seat.
                int bi = -1;
                for (int c = 0; c < colors.Count; c++)
                    if (ColorClose(colors[c], seatColor)) { bi = c; break; }
                if (bi < 0)
                {
                    bi = colors.Count;
                    colors.Add(seatColor);
                    posL.Add(new List<Vector3>());
                    rotL.Add(new List<Quaternion>());
                    scaleL.Add(new List<Vector3>());
                    phaseL.Add(new List<float>());
                }

                Vector3 seatPos = seat.position;
                Vector3 right   = seat.right;                                  // tangential along the row
                Vector3 inward  = -new Vector3(seatPos.x, 0f, seatPos.z).normalized;
                if (inward == Vector3.zero) inward = Vector3.forward;
                Quaternion face = Quaternion.LookRotation(inward, Vector3.up); // spectators face the pitch
                float halfWidth = seat.lossyScale.x * 0.5f;
                float topY      = seatPos.y + seat.lossyScale.y * 0.4f;        // sit on the step top

                for (int j = 0; j < _peoplePerSeat; j++)
                {
                    float frac    = (j + 0.5f) / _peoplePerSeat - 0.5f;        // -0.5..0.5 across the row
                    float scatter = (Hash01(seat.name, j) - 0.5f) * _rowScatter * 2f * halfWidth;
                    Vector3 pos = seatPos
                                  + right * (frac * 2f * halfWidth * 0.82f + scatter)
                                  + inward * 0.12f;
                    pos.y = topY;

                    float jitter = 1f + (Hash01(seat.name, j + 97) - 0.5f) * 2f * _scaleJitter;
                    Vector3 scl  = Vector3.one * (_figureScale * jitter);
                    Quaternion r = face * Quaternion.Euler(0f, (Hash01(seat.name, j + 53) - 0.5f) * 30f, 0f);

                    posL[bi].Add(pos);
                    rotL[bi].Add(r);
                    scaleL[bi].Add(scl);
                    phaseL[bi].Add(Hash01(seat.name, j + 211) * Mathf.PI * 2f);
                    total++;
                }
            }

            _buckets.Clear();
            for (int c = 0; c < colors.Count; c++)
            {
                var bucket = new CrowdBucket
                {
                    color    = colors[c],
                    material = BuildInstancedMaterial(colors[c]),
                    basePos  = posL[c].ToArray(),
                    rot      = rotL[c].ToArray(),
                    scale    = scaleL[c].ToArray(),
                    phase    = phaseL[c].ToArray(),
                };
                bucket.count = bucket.basePos.Length;
                bucket.work  = new Matrix4x4[bucket.count];
                _buckets.Add(bucket);
            }

            Debug.Log($"[CrowdAnimator] Seated {total} spectators across {_buckets.Count} colour batches.");
        }

        // One head + torso primitive blob, merged into a single mesh (~a few hundred verts).
        private Mesh BuildSeatedFigureMesh()
        {
            var torsoSrc = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var headSrc  = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh torso = torsoSrc.GetComponent<MeshFilter>().sharedMesh;
            Mesh head  = headSrc.GetComponent<MeshFilter>().sharedMesh;

            // Local space: feet at y=0, total height ~1.0 (scaled by _figureScale at placement).
            var combine = new[]
            {
                new CombineInstance
                {
                    mesh = torso,
                    transform = Matrix4x4.TRS(new Vector3(0f, 0.30f, 0f),
                        Quaternion.identity, new Vector3(0.50f, 0.62f, 0.42f)),
                },
                new CombineInstance
                {
                    mesh = head,
                    transform = Matrix4x4.TRS(new Vector3(0f, 0.80f, 0f),
                        Quaternion.identity, new Vector3(0.40f, 0.40f, 0.40f)),
                },
            };

            var mesh = new Mesh { name = "SeatedFigure" };
            mesh.CombineMeshes(combine, true, true);   // single submesh → one material
            mesh.RecalculateBounds();

            DestroyImmediate(torsoSrc);   // drop the temp sources now (no 1-frame blip at origin)
            DestroyImmediate(headSrc);
            return mesh;
        }

        private Material BuildInstancedMaterial(Color color)
        {
            var shader = _figureShader != null ? _figureShader
                       : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");   // last-ditch fallback
            var mat = new Material(shader)
            {
                color = color,
                enableInstancing = true,
            };
            return mat;
        }

        private static bool ColorClose(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.02f
                && Mathf.Abs(a.g - b.g) < 0.02f
                && Mathf.Abs(a.b - b.b) < 0.02f;
        }

        // The 'empty/dark seat' palette entry (~0.30, 0.32, 0.36) reads as low brightness + near-grey.
        private static bool IsDarkSeat(Color c)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            return max < 0.45f && (max - min) < 0.12f;
        }

        // Deterministic 0..1 hash so the crowd layout is stable across rebuilds but scattered.
        private static float Hash01(string seed, int salt)
        {
            int h = seed.GetHashCode();
            h = (h * 73856093) ^ (salt * 19349663);
            return (Mathf.Abs(h) % 100000) / 100000f;
        }

        // ── Goal reactions ──────────────────────────────────────────────────

        private void TriggerCrowdCheerJump()
        {
            if (!_crowdBuilt) return;
            if (_cheerRoutine != null) StopCoroutine(_cheerRoutine);
            _cheerRoutine = StartCoroutine(CheerRoutine());
        }

        private IEnumerator CheerRoutine()
        {
            float t = 0f;
            while (t < _cheerDuration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / _cheerDuration);
                // Two decaying bounces — the stands surge up then settle.
                _cheerLevel = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 2f)) * (1f - u);
                yield return null;
            }
            _cheerLevel = 0f;
            _cheerRoutine = null;
        }

        public void TriggerConfetti()
        {
            if (_confettiPs == null) return;
            _confettiPs.transform.position = new Vector3(0f, _confettiSpawnY, 0f);
            _confettiPs.Clear();
            _confettiPs.Play();
        }

        public void PlayCrowdCheer()
        {
            if (_crowdCheerClips == null || _crowdCheerClips.Length == 0) return;
            if (_sfxSource == null) return;
            var clip = _crowdCheerClips[Random.Range(0, _crowdCheerClips.Length)];
            _sfxSource.PlayOneShot(clip, _cheerVolume);
        }

        private void FlashSpotLights()
        {
            if (_spotLights == null || _spotLights.Length == 0) return;
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            for (int f = 0; f < _flashCount; f++)
            {
                for (int i = 0; i < _spotLights.Length; i++)
                    _spotLights[i].intensity = _spotBaseIntensities[i] + _flashIntensityBoost;

                yield return new WaitForSeconds(_flashDuration * 0.5f);

                for (int i = 0; i < _spotLights.Length; i++)
                    _spotLights[i].intensity = _spotBaseIntensities[i];

                yield return new WaitForSeconds(_flashDuration * 0.5f);
            }
            _flashRoutine = null;
        }

        private ParticleSystem BuildConfettiSystem()
        {
            var go = new GameObject("ConfettiSystem");
            go.transform.SetParent(transform, false);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = _confettiLifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.maxParticles = _confettiCount * 2;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.3f;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, _confettiCount) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(_confettiSpawnRadius * 2f, 0.1f, _confettiSpawnRadius * 2f);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(new Color(0.2f, 0.35f, 0.75f), 0f),
                    new GradientColorKey(new Color(0.8f, 0.2f, 0.22f), 1f),
                },
                new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = grad;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.3f)));

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.material = new Material(Shader.Find("Sprites/Default"));
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;

            return ps;
        }
    }
}
