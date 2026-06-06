using UnityEngine;

namespace SoccerBot
{
    // Generates a short, heavy percussive "哒" thud AudioClip procedurally
    // (no audio file needed). Used when a player traps / receives the ball.
    // Call ThudGenerator.Create() to get a one-shot clip.
    public static class ThudGenerator
    {
        public static AudioClip Create(float duration = 0.16f, float startFreq = 190f, float endFreq = 85f)
        {
            int sampleRate = AudioSettings.outputSampleRate;
            int samples    = Mathf.RoundToInt(sampleRate * duration);
            var data       = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float p = t / duration;                          // 0..1 progress

                // Fast exponential amplitude decay → punchy, percussive body.
                float env = Mathf.Exp(-9f * p);

                // Downward pitch glide gives the "heavy" weight of the trap.
                float freq = Mathf.Lerp(startFreq, endFreq, p);
                float body = Mathf.Sin(2f * Mathf.PI * freq * t);

                // Sharp click transient in the first few milliseconds — the "哒" attack.
                float click = (t < 0.006f) ? Random.Range(-1f, 1f) * (1f - t / 0.006f) : 0f;

                float wave = body * 0.9f + click * 0.6f;
                // Soft saturation for a fuller, heavier thud.
                wave = Mathf.Clamp(wave * 1.4f, -1f, 1f);
                data[i] = wave * env * 0.85f;
            }

            var clip = AudioClip.Create("BallReceiveThud", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
