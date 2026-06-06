// LightingConfigurator.cs — Runtime lighting setup for "match day evening" atmosphere.
// Adjusts main directional light, ambient, and shadows. Attach to any persistent GO.

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

        [Header("Ambient")]
        [SerializeField] private Color _ambientColor   = new Color(0.1f, 0.08f, 0.06f);
        [SerializeField] private Color _ambientSkyColor = new Color(0.12f, 0.16f, 0.24f);
        [SerializeField] private Color _ambientEquatorColor = new Color(0.32f, 0.26f, 0.16f);
        [SerializeField] private Color _ambientGroundColor = new Color(0.045f, 0.05f, 0.055f);
        [SerializeField, Range(0f, 1f)] private float _reflectionIntensity = 0.35f;

        [Header("Shadow (PC default)")]
        [SerializeField] private float _shadowDistance = 30f;
        [SerializeField] private UnityEngine.Rendering.LightShadowResolution _shadowResolution = UnityEngine.Rendering.LightShadowResolution.VeryHigh;

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
