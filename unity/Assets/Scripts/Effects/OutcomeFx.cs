// OutcomeFx.cs — Spawns a particle burst at the ball position when a Scenario ends.
// Subscribes to ScenarioPlayer.OnScenarioComplete; chooses one of three procedural
// ParticleSystems based on Scenario.outcome (Score / Intercepted / Missed).
//
// All particle systems are built in code from a stock material so no asset wiring
// is required. Drop on a GameObject in the scene and assign _player + _ballTransform.

using UnityEngine;

namespace SoccerBot
{
    public class OutcomeFx : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ScenarioPlayer _player;
        [SerializeField] private Transform _ballTransform;

        [Header("Tuning")]
        [SerializeField] private float _yOffset = 0.4f;
        [SerializeField] private int   _scoreBurstCount       = 150;
        [SerializeField] private int   _interceptedBurstCount = 40;
        [SerializeField] private int   _missedBurstCount      = 30;

        private ParticleSystem _scoreFx;
        private ParticleSystem _interceptedFx;
        private ParticleSystem _missedFx;
        private Coroutine _shakeRoutine;

        void Awake()
        {
            _scoreFx       = BuildScoreFx();
            _interceptedFx = BuildInterceptedFx();
            _missedFx      = BuildMissedFx();
        }

        void Start()
        {
            if (_player == null) _player = FindAnyObjectByType<ScenarioPlayer>();
            if (_ballTransform == null)
            {
                var ball = GameObject.Find("Ball");
                if (ball != null) _ballTransform = ball.transform;
            }
            if (_player != null) _player.OnScenarioComplete += OnScenarioComplete;
            else Debug.LogWarning("[OutcomeFx] No ScenarioPlayer reference.");
        }

        void OnDestroy()
        {
            if (_player != null) _player.OnScenarioComplete -= OnScenarioComplete;
        }

        private void OnScenarioComplete(Scenario s)
        {
            if (s == null) return;

            Vector3 pos = _ballTransform != null
                ? _ballTransform.position + Vector3.up * _yOffset
                : transform.position;

            ParticleSystem fx = s.outcome switch
            {
                ScenarioOutcome.Score       => _scoreFx,
                ScenarioOutcome.Intercepted => _interceptedFx,
                ScenarioOutcome.Missed      => _missedFx,
                _                           => null,
            };
            if (fx == null) return;

            fx.transform.position = pos;
            fx.Clear();
            fx.Play();

            if (s.outcome == ScenarioOutcome.Score)
            {
                if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
                _shakeRoutine = StartCoroutine(ShakeCamera(0.25f, 0.04f));
            }
        }

        private System.Collections.IEnumerator ShakeCamera(float duration, float magnitude)
        {
            var cam = Camera.main;
            if (cam == null) yield break;
            var origin = cam.transform.localPosition;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float fade = 1f - (t / duration);
                cam.transform.localPosition = origin + Random.insideUnitSphere * magnitude * fade;
                yield return null;
            }
            cam.transform.localPosition = origin;
        }

        // ── Procedural ParticleSystem builders ──────────────

        private ParticleSystem BuildScoreFx()
        {
            // Gold burst + upward sparks
            var ps = NewSystem("Fx_Score",
                color: new Color(1f, 0.85f, 0.25f, 1f),
                size: 0.18f,
                lifetime: 1.8f,
                gravity: -0.8f);
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] {
                new ParticleSystem.Burst(0f, _scoreBurstCount),
            });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
            var main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
            main.maxParticles = 300;
            return ps;
        }

        private ParticleSystem BuildInterceptedFx()
        {
            // Red shockwave on the ground plane
            var ps = NewSystem("Fx_Intercepted",
                color: new Color(0.95f, 0.25f, 0.2f, 1f),
                size: 0.22f,
                lifetime: 0.8f,
                gravity: 0f);
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] {
                new ParticleSystem.Burst(0f, _interceptedBurstCount),
            });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.05f;
            var main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
            return ps;
        }

        private ParticleSystem BuildMissedFx()
        {
            // Orange dust puff — "so close" regret
            var ps = NewSystem("Fx_Missed",
                color: new Color(0.95f, 0.55f, 0.1f, 1f),
                size: 0.25f,
                lifetime: 1.4f,
                gravity: 0.2f);
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] {
                new ParticleSystem.Burst(0f, _missedBurstCount),
            });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.08f;
            var main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.5f);
            return ps;
        }

        private ParticleSystem NewSystem(string name, Color color, float size, float lifetime, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();

            // ParticleSystem starts playing the moment it's added; we have to stop
            // it before any main.* property assignment, or Unity logs warnings like
            // "Setting the duration is not supported while the system is playing."
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = lifetime;
            main.startSize = size;
            main.startColor = color;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;

            var emission = ps.emission;
            emission.enabled = true;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(color, 1f),
                },
                new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = grad;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var curve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0.2f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return ps;
        }
    }
}
