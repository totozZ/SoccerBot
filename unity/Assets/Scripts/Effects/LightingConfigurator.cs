// LightingConfigurator.cs — Runtime lighting setup for "match day evening" atmosphere.
// Adjusts main directional light, ambient, and shadows. Attach to any persistent GO.

using UnityEngine;

namespace SoccerBot
{
    public class LightingConfigurator : MonoBehaviour
    {
        [Header("Directional Light")]
        [SerializeField] private Color _lightColor    = new Color(1f, 0.75f, 0.5f);
        [SerializeField] private float _lightIntensity = 1.2f;
        [SerializeField] private float _lightYAngle    = -20f;
        [SerializeField] private float _shadowStrength = 0.65f;

        [Header("Ambient")]
        [SerializeField] private Color _ambientColor   = new Color(0.1f, 0.08f, 0.06f);

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

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = _ambientColor;
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
