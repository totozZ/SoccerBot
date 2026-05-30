using UnityEngine;

namespace SoccerBot
{
    // Generates a short referee-whistle AudioClip procedurally (no audio file needed).
    // Call WhistleGenerator.Create() to get a one-shot clip.
    public static class WhistleGenerator
    {
        public static AudioClip Create(float duration = 0.35f, float freq = 3200f, float freq2 = 3800f)
        {
            int sampleRate = AudioSettings.outputSampleRate;
            int samples    = Mathf.RoundToInt(sampleRate * duration);
            var data       = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t    = (float)i / sampleRate;
                float env  = Mathf.Sin(Mathf.PI * t / duration); // fade in/out
                // Two close frequencies create a slight beating effect
                float wave = Mathf.Sin(2f * Mathf.PI * freq  * t) * 0.5f
                           + Mathf.Sin(2f * Mathf.PI * freq2 * t) * 0.5f;
                // Add a tiny bit of noise for realism
                wave += Random.Range(-0.04f, 0.04f);
                data[i] = wave * env * 0.7f;
            }

            var clip = AudioClip.Create("Whistle", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
