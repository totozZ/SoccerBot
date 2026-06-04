// PolishVolumeBuilder.cs — Configures a URP Global Volume profile at runtime.
// Drop on a GameObject with a Volume component (mode = Global). Builds Bloom,
// Vignette, ColorAdjustments, and Tonemapping in code so no .asset wiring is needed.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SoccerBot
{
    [RequireComponent(typeof(Volume))]
    public class PolishVolumeBuilder : MonoBehaviour
    {
        [Header("Bloom")]
        [SerializeField] private float _bloomIntensity = 0.6f;
        [SerializeField] private float _bloomThreshold = 1.0f;

        [Header("Vignette")]
        [SerializeField, Range(0f, 1f)] private float _vignetteIntensity = 0.3f;

        [Header("Color Adjustments")]
        [SerializeField, Range(-100f, 100f)] private float _contrast    = 15f;
        [SerializeField, Range(-100f, 100f)] private float _saturation  = 10f;
        [SerializeField, Range(-100f, 100f)] private float _postExposure = 0.2f;

        void Awake()
        {
            var volume = GetComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.weight = 1f;

            // profile is an instance accessor — creates a unique copy if missing
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "PolishProfile (runtime)";

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(_bloomIntensity);
            bloom.threshold.Override(_bloomThreshold);
            bloom.scatter.Override(0.7f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(_vignetteIntensity);
            vignette.smoothness.Override(0.4f);

            var colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.contrast.Override(_contrast);
            colorAdj.saturation.Override(_saturation);
            colorAdj.postExposure.Override(_postExposure);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            volume.sharedProfile = profile;
        }
    }
}
