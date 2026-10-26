using System.Collections;
using UnityEngine;

/// <summary>
/// Level 1 ending: once the last task is done, the doorbell rings and Mummy walks in. Her lines play
/// from 'voicePoint' (the corridor) with timed subtitles on the HUD. GameFlowManager waits for
/// Finished before it shows the score card. Without a voice clip the subtitles still run on their times.
/// </summary>
public class MummyArrival : MonoBehaviour
{
    [SerializeField] private TaskHUD hud;
    [SerializeField] private string speaker = "Mummy";
    [SerializeField] private Transform voicePoint;
    [SerializeField] private float startDelay = 1.2f;

    [Header("Audio")]
    [SerializeField] private AudioClip doorbell;               // optional: real recording
    [SerializeField, Range(0f, 1f)] private float doorbellVolume = 0.8f;
    [SerializeField] private float gapAfterBell = 1.6f;
    [SerializeField] private AudioClip voiceEnglish;
    [SerializeField] private AudioClip voiceHinglish;
    [SerializeField, Range(0f, 1f)] private float voiceVolume = 1f;

    [Header("Subtitles (timed to the voice clip)")]
    [SerializeField] private IntroPhoneCall.Line[] linesEnglish;
    [SerializeField] private IntroPhoneCall.Line[] linesHinglish;
    [SerializeField] private float tailWithoutClip = 3f;

    public bool Playing { get; private set; }
    public bool Finished { get; private set; }

    private AudioSource _src;
    private static AudioClip _bell;

    private void Awake()
    {
        _src = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.spatialBlend = 0.6f;              // comes from the corridor, but heard anywhere in the house
        _src.rolloffMode = AudioRolloffMode.Linear;
        _src.minDistance = 3f;
        _src.maxDistance = 25f;
        _src.dopplerLevel = 0f;
        if (doorbell == null && _bell == null) _bell = MakeDoorbell();
    }

    public void Play()
    {
        if (Playing || Finished) return;
        Playing = true;
        StartCoroutine(Routine());
    }

    private IEnumerator Routine()
    {
        if (voicePoint) transform.position = voicePoint.position;
        yield return new WaitForSeconds(startDelay);

        _src.PlayOneShot(doorbell ? doorbell : _bell, doorbellVolume);
        if (hud) hud.ShowToast(GameLanguage.Pick("Ding dong! Mummy is home!", "Ding dong! Mummy aa gayi!"), hud.GoodColor, false);
        yield return new WaitForSeconds(gapAfterBell);

        bool en = GameLanguage.IsEnglish;
        AudioClip clip = en ? (voiceEnglish ? voiceEnglish : voiceHinglish) : (voiceHinglish ? voiceHinglish : voiceEnglish);
        var lines = en ? linesEnglish : linesHinglish;
        if (lines == null || lines.Length == 0) lines = en ? linesHinglish : linesEnglish;

        float length = clip ? clip.length : (lines != null && lines.Length > 0 ? lines[lines.Length - 1].time + tailWithoutClip : 2f);
        if (clip) { _src.clip = clip; _src.volume = voiceVolume; _src.Play(); }

        float start = Time.time;
        int next = 0;
        while (Time.time - start < length)
        {
            float t = Time.time - start;
            while (lines != null && next < lines.Length && t >= lines[next].time)
            {
                if (hud) hud.ShowSubtitle(speaker, lines[next].text);
                next++;
            }
            yield return null;
        }
        yield return new WaitForSeconds(0.6f);
        if (hud) hud.ClearSubtitle();
        Playing = false;
        Finished = true;
    }

    /// <summary>Two-tone 'ding-dong' (E5 then C5) with a soft bell timbre.</summary>
    private static AudioClip MakeDoorbell()
    {
        const int sr = 44100;
        float len = 2.4f;
        var data = new float[(int)(sr * len)];
        AddTone(data, sr, 0f, 659.25f);
        AddTone(data, sr, 0.62f, 523.25f);
        float peak = 0f;
        foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
        if (peak > 0f) for (int i = 0; i < data.Length; i++) data[i] *= 0.85f / peak;
        var c = AudioClip.Create("sfx_doorbell", data.Length, 1, sr, false);
        c.SetData(data, 0);
        return c;
    }

    private static void AddTone(float[] d, int sr, float at, float f)
    {
        int s0 = (int)(at * sr);
        for (int i = s0; i < d.Length; i++)
        {
            float t = (i - s0) / (float)sr;
            float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 2.4f);
            float w = Mathf.Sin(2f * Mathf.PI * f * t)
                    + 0.35f * Mathf.Sin(2f * Mathf.PI * f * 2.01f * t) * Mathf.Exp(-t * 4f)
                    + 0.15f * Mathf.Sin(2f * Mathf.PI * f * 3.02f * t) * Mathf.Exp(-t * 7f);
            d[i] += w * env;
        }
    }
}
