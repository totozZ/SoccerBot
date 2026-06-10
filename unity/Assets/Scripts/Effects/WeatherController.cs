// WeatherController.cs - Simple weather toggle for demo atmosphere.
// DuskHaze is the default show look; Rain remains available as a mood variant.
// Attach to any persistent GO (e.g., GameManager).

using UnityEngine;

namespace SoccerBot
{
    public enum WeatherMode { Sunny, DuskHaze, Rain }

    public class WeatherController : MonoBehaviour
    {
        [Header("Mode")]
        [SerializeField] private WeatherMode _startMode = WeatherMode.DuskHaze;

        [Header("Rain Particles")]
        [SerializeField] private int   _rainParticleCount = 300;
        [SerializeField] private float _rainAreaWidth      = 16f;
        [SerializeField] private float _rainAreaLength     = 22f;
        [SerializeField] private float _rainFallSpeed      = 6f;
        [SerializeField] private float _rainStartY         = 8f;
        [SerializeField] private float _rainLifetime       = 1.0f;
        [SerializeField] private Color _rainColor          = new Color(0.7f, 0.78f, 0.9f, 0.5f);

        [Header("Fog")]
        [SerializeField] private float _duskFogDensity     = 0.005f;
        [SerializeField] private Color _duskFogColor       = new Color(0.20f, 0.16f, 0.125f);
        [SerializeField] private float _rainFogDensity     = 0.015f;
        [SerializeField] private Color _rainFogColor       = new Color(0.53f, 0.6f, 0.67f);

        [Header("Grass Darkening")]
        [SerializeField] private Color _sunnyGrassColor    = new Color(0.18f, 0.55f, 0.22f);
        [SerializeField] private Color _duskGrassColor     = new Color(0.16f, 0.46f, 0.19f);
        [SerializeField] private Color _rainGrassColor     = new Color(0.10f, 0.32f, 0.13f);

        private ParticleSystem _rainPs;
        private Material       _grassMaterial;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");

        void Awake()
        {
            _rainPs = BuildRainSystem();
            SetWeather(_startMode);
        }

        public void SetWeather(WeatherMode mode)
        {
            if (mode == WeatherMode.Sunny)
            {
                if (_rainPs != null) _rainPs.Stop();
                RenderSettings.fog = false;
                SetGrassColor(_sunnyGrassColor);
            }
            else if (mode == WeatherMode.DuskHaze)
            {
                if (_rainPs != null) _rainPs.Stop();
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = _duskFogDensity;
                RenderSettings.fogColor = _duskFogColor;
                SetGrassColor(_duskGrassColor);
            }
            else
            {
                if (_rainPs != null) _rainPs.Play();
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = _rainFogDensity;
                RenderSettings.fogColor = _rainFogColor;
                SetGrassColor(_rainGrassColor);
            }
        }

        public void ToggleWeather()
        {
            if (!RenderSettings.fog)
            {
                SetWeather(WeatherMode.DuskHaze);
                return;
            }

            bool isRainColor = Approximately(RenderSettings.fogColor, _rainFogColor);
            SetWeather(isRainColor ? WeatherMode.Sunny : WeatherMode.Rain);
        }

        private ParticleSystem BuildRainSystem()
        {
            var go = new GameObject("RainSystem");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, _rainStartY, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = _rainLifetime;
            main.startSpeed = _rainFallSpeed;
            main.startSize = 0.04f;
            main.startColor = _rainColor;
            main.maxParticles = _rainParticleCount * 2;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1f;
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = _rainParticleCount;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(_rainAreaWidth, 0.1f, _rainAreaLength);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.material = new Material(Shader.Find("Sprites/Default"));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 0.8f;
            r.velocityScale = 0.25f;
            return ps;
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.02f
                && Mathf.Abs(a.g - b.g) < 0.02f
                && Mathf.Abs(a.b - b.b) < 0.02f;
        }

        private void SetGrassColor(Color target)
        {
            if (_grassMaterial == null)
            {
                var field = GameObject.Find("Field");
                if (field != null)
                {
                    var grass = field.transform.Find("Grass");
                    if (grass != null)
                    {
                        var r = grass.GetComponent<Renderer>();
                        if (r != null) _grassMaterial = r.material;
                    }
                }
            }
            if (_grassMaterial == null) return;
            if (_grassMaterial.HasProperty(BaseColorId))
                _grassMaterial.SetColor(BaseColorId, target);
            else if (_grassMaterial.HasProperty(ColorId))
                _grassMaterial.SetColor(ColorId, target);
        }
    }
}
