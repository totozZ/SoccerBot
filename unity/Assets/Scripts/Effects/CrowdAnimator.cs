// CrowdAnimator.cs — Makes the StadiumBuilder's seating bowl feel alive.
// - Floating colored dots above seat blocks (sin-wave bobbing)
// - Confetti burst on goal
// - Spot light flash on goal
// - Triggers crowd cheer audio on goal
//
// Attach to the Stadium GameObject.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoccerBot
{
    public class CrowdAnimator : MonoBehaviour
    {
        [Header("Floating Dots")]
        [SerializeField] private int   _dotsPerSeat       = 2;
        [SerializeField] private float _dotRadius          = 0.06f;
        [SerializeField] private float _dotHeightAboveSeat = 0.3f;
        [SerializeField] private float _bobAmplitude       = 0.08f;
        [SerializeField] private float _bobFrequency       = 2.5f;
        [SerializeField] private float _dotSpawnRadius     = 0.2f;

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

        private List<Transform>   _dotTransforms = new List<Transform>();
        private List<float>       _dotPhases     = new List<float>();
        private ParticleSystem    _confettiPs;
        private Light[]           _spotLights;
        private float[]           _spotBaseIntensities;
        private AudioSource       _sfxSource;
        private Coroutine         _flashRoutine;
        private bool              _crowdBuilt;

        void Awake()
        {
            _sfxSource = gameObject.GetComponent<AudioSource>();
            if (_sfxSource == null)
                _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.spatialBlend = 1f;
            _sfxSource.maxDistance = 50f;

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
            for (int i = 0; i < _dotTransforms.Count; i++)
            {
                if (_dotTransforms[i] == null) continue;
                float phase = _dotPhases[i];
                float y = Mathf.Sin(Time.time * _bobFrequency + phase) * _bobAmplitude;
                var pos = _dotTransforms[i].localPosition;
                pos.y = y;
                _dotTransforms[i].localPosition = pos;
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
            }
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

        private void EnsureCrowdBuilt()
        {
            if (_crowdBuilt) return;
            var bowl = transform.Find("SeatingBowl");
            if (bowl == null) return;
            BuildFloatingDots(bowl);
            _crowdBuilt = true;
        }

        private void BuildFloatingDots(Transform bowl)
        {
            if (bowl == null) return;

            var existingPool = transform.Find("CrowdDots");
            if (existingPool != null)
            {
                for (int i = existingPool.childCount - 1; i >= 0; i--)
                    Destroy(existingPool.GetChild(i).gameObject);
                Destroy(existingPool.gameObject);
            }

            _dotTransforms.Clear();
            _dotPhases.Clear();

            var pool = new GameObject("CrowdDots");
            pool.transform.SetParent(transform, false);

            var allSeats = bowl.GetComponentsInChildren<Transform>();
            foreach (var seat in allSeats)
            {
                if (!seat.name.StartsWith("Seat_")) continue;

                var rend = seat.GetComponent<Renderer>();
                Color seatColor = Color.gray;
                if (rend != null && rend.material != null)
                    seatColor = rend.material.color;

                for (int d = 0; d < _dotsPerSeat; d++)
                {
                    var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    dot.name = $"Dot_{seat.name}_{d}";
                    dot.transform.SetParent(pool.transform, false);

                    Vector3 seatWorldPos = seat.position;
                    Vector3 outward = seatWorldPos.normalized;
                    float scatterX = (Random.value - 0.5f) * _dotSpawnRadius * 2f;
                    float scatterZ = (Random.value - 0.5f) * _dotSpawnRadius * 2f;
                    dot.transform.position = seatWorldPos
                        + outward * _dotHeightAboveSeat * 0.3f
                        + Vector3.up * _dotHeightAboveSeat
                        + new Vector3(scatterX, 0f, scatterZ);
                    dot.transform.localScale = Vector3.one * _dotRadius;

                    Destroy(dot.GetComponent<Collider>());
                    var r = dot.GetComponent<Renderer>();
                    if (r != null)
                        r.material.color = seatColor * Random.Range(0.7f, 1.3f);

                    _dotTransforms.Add(dot.transform);
                    _dotPhases.Add(Random.Range(0f, Mathf.PI * 2f));
                }
            }
            Debug.Log($"[CrowdAnimator] Generated {_dotTransforms.Count} crowd dots.");
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
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            return ps;
        }
    }
}
