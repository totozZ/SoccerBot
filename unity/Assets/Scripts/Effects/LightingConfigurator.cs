// LightingConfigurator.cs - Runtime lighting setup for match-day dusk atmosphere.
// Adjusts main directional light, skybox, ambient, reflection, and shadows.

using UnityEngine;

namespace SoccerBot
{
    public class LightingConfigurator : MonoBehaviour
    {
        [Header("Directional Light")]
        [SerializeField] private Color _lightColor    = new Color(1f, 0.67f, 0.38f);
        [SerializeField] private float _lightIntensity = 1.2f;
        [SerializeField] private float _lightYAngle    = -8f;
        [SerializeField] private float _shadowStrength = 0.45f;

        [Header("Dusk Skybox")]
        [SerializeField] private bool _applyDuskSkybox = true;
        [SerializeField] private Color _skyTint = new Color(0.52f, 0.42f, 0.78f);
        [SerializeField] private Color _groundTint = new Color(0.16f, 0.10f, 0.06f);
        [SerializeField, Range(0f, 8f)] private float _skyAtmosphereThickness = 1.45f;
        [SerializeField, Range(0f, 8f)] private float _skyExposure = 1.15f;
        [SerializeField, Range(0f, 1f)] private float _skySunSize = 0.06f;

        [Header("Ambient")]
        [SerializeField] private Color _ambientSkyColor = new Color(0.22f, 0.27f, 0.36f);
        [SerializeField] private Color _ambientEquatorColor = new Color(0.58f, 0.44f, 0.28f);
        [SerializeField] private Color _ambientGroundColor = new Color(0.055f, 0.050f, 0.045f);
        [SerializeField, Range(0f, 1f)] private float _reflectionIntensity = 0.42f;

        [Header("Shadow (PC default)")]
        [SerializeField] private float _shadowDistance = 30f;
        [SerializeField] private UnityEngine.Rendering.LightShadowResolution _shadowResolution = UnityEngine.Rendering.LightShadowResolution.VeryHigh;

        private Material _runtimeSkybox;

        void Awake()
        {
            Apply();
        }

        public void Apply()
        {
            var sun = FindMainDirectionalLight();
            if (sun != null)
            {
                sun.color = _lightColor;
                sun.intensity = _lightIntensity;
                sun.transform.rotation = Quaternion.Euler(_lightYAngle, 30f, 0f);
                sun.shadowStrength = _shadowStrength;

#if UNITY_ANDROID && !UNITY_EDITOR
                sun.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Medium;
                QualitySettings.shadowDistance = 8f;
#else
                sun.shadowResolution = _shadowResolution;
                QualitySettings.shadowDistance = _shadowDistance;
#endif
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = _ambientSkyColor;
            RenderSettings.ambientEquatorColor = _ambientEquatorColor;
            RenderSettings.ambientGroundColor = _ambientGroundColor;
            RenderSettings.reflectionIntensity = _reflectionIntensity;

            if (_applyDuskSkybox)
                ApplyDuskSkybox();
        }

        private void ApplyDuskSkybox()
        {
            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
                return;

            if (_runtimeSkybox == null || _runtimeSkybox.shader != shader)
            {
                _runtimeSkybox = new Material(shader)
                {
                    name = "Runtime Dusk Procedural Skybox"
                };
            }

            SetColorIfPresent(_runtimeSkybox, "_SkyTint", _skyTint);
            SetColorIfPresent(_runtimeSkybox, "_GroundColor", _groundTint);
            SetFloatIfPresent(_runtimeSkybox, "_AtmosphereThickness", _skyAtmosphereThickness);
            SetFloatIfPresent(_runtimeSkybox, "_Exposure", _skyExposure);
            SetFloatIfPresent(_runtimeSkybox, "_SunSize", _skySunSize);
            SetFloatIfPresent(_runtimeSkybox, "_SunSizeConvergence", 7.5f);

            RenderSettings.skybox = _runtimeSkybox;
        }

        private static void SetColorIfPresent(Material mat, string property, Color value)
        {
            if (mat != null && mat.HasProperty(property))
                mat.SetColor(property, value);
        }

        private static void SetFloatIfPresent(Material mat, string property, float value)
        {
            if (mat != null && mat.HasProperty(property))
                mat.SetFloat(property, value);
        }

        private Light FindMainDirectionalLight()
        {
            var sunGO = GameObject.Find("Directional Light");
            if (sunGO != null)
            {
                var l = sunGO.GetComponent<Light>();
                if (l != null && l.type == LightType.Directional) return l;
            }

            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) return l;

            return null;
        }
    }
}
