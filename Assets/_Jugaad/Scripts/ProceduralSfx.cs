using System;
using UnityEngine;

/// <summary>
/// Tiny code-generated sound effects (no audio files needed): typewriter tick,
/// success chime, error buzz and a dull 'dead switch' thunk. Clips are created once and cached.
/// </summary>
public static class ProceduralSfx
{
    private const int SampleRate = 44100;
    private static AudioClip _tick, _success, _error, _thunk;

    public static AudioClip Tick => _tick != null ? _tick : (_tick = Make("sfx_tick", 0.03f,
        t => Mathf.Sin(2f * Mathf.PI * 2300f * t) * Mathf.Exp(-t * 140f) * 0.5f));

    public static AudioClip Success => _success != null ? _success : (_success = Make("sfx_success", 0.42f,
        t => t < 0.14f
            ? Mathf.Sin(2f * Mathf.PI * 880f * t) * Mathf.Exp(-t * 10f)
            : Mathf.Sin(2f * Mathf.PI * 1320f * t) * Mathf.Exp(-(t - 0.14f) * 7f)));

    public static AudioClip Error => _error != null ? _error : (_error = Make("sfx_error", 0.28f,
        t => Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 170f * t)) * 0.35f * Mathf.Exp(-t * 6f)));

    public static AudioClip Thunk => _thunk != null ? _thunk : (_thunk = Make("sfx_thunk", 0.12f,
        t => (Mathf.Sin(2f * Mathf.PI * 110f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * 2400f * t) * Mathf.Exp(-t * 400f)) * Mathf.Exp(-t * 35f)));

    private static AudioClip _applause;

    /// <summary>~3 s of crowd clapping, built from hundreds of short filtered noise bursts.</summary>
    public static AudioClip Applause => _applause != null ? _applause : (_applause = MakeApplause(3.2f));

    private static AudioClip MakeApplause(float seconds)
    {
        int n = Mathf.CeilToInt(seconds * SampleRate);
        var data = new float[n];
        var rnd = new System.Random(1234);
        int claps = Mathf.CeilToInt(seconds * 150f);
        for (int c = 0; c < claps; c++)
        {
            float t = (float)rnd.NextDouble() * seconds;
            float crowd = Mathf.Clamp01(t / 0.25f) * Mathf.Clamp01((seconds - t) / 1.2f); // swell in, fade out
            if (rnd.NextDouble() > crowd) continue;
            int start = (int)(t * SampleRate);
            int len = (int)(SampleRate * (0.010f + 0.018f * (float)rnd.NextDouble()));
            float amp = 0.2f + 0.3f * (float)rnd.NextDouble();
            float smooth = 0.3f + 0.45f * (float)rnd.NextDouble(); // per-clap brightness
            float prev = 0f;
            for (int i = 0; i < len && start + i < n; i++)
            {
                float env = Mathf.Exp(-5f * i / (float)len);
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                prev += smooth * (noise - prev);
                data[start + i] += prev * env * amp;
            }
        }
        float peak = 0f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        if (peak > 0f) for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.8f;
        var clip = AudioClip.Create("sfx_applause", n, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip Make(string name, float seconds, Func<float, float> wave)
    {
        int n = Mathf.CeilToInt(seconds * SampleRate);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float fadeOut = Mathf.Clamp01((n - i) / (SampleRate * 0.005f)); // avoid end click
            data[i] = Mathf.Clamp(wave(t) * 0.8f * fadeOut, -1f, 1f);
        }
        var clip = AudioClip.Create(name, n, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
