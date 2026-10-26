using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Turning the key with the battery in: the starter cranks ("ghrr-ghrr-ghrr"), the engine catches
/// and settles into an idle rumble, the instrument cluster lights up - and a red warning starts
/// blinking on the dashboard (the right rear tyre is flat). All engine sounds are generated here.
/// Put it on the car.
/// </summary>
public class CarEngine : MonoBehaviour
{
    [SerializeField] private CarIgnition ignition;
    [Tooltip("Where the engine sound comes from (engine bay).")]
    [SerializeField] private Transform engineSpot;
    [SerializeField] private float crankTime = 1.4f;
    [SerializeField, Range(0f, 1f)] private float idleVolume = 0.45f;

    [Header("Recorded sound (used instead of the generated one when set)")]
    [Tooltip("Crank + catch + idle, played once.")]
    [SerializeField] private AudioClip startClip;
    [Tooltip("Idle cut from the end of startClip, loops seamlessly.")]
    [SerializeField] private AudioClip idleLoop;
    [Tooltip("Seconds into startClip where the engine catches.")]
    [SerializeField] private float catchTime = 1.3f;
    [SerializeField] private float warningDelay = 1.2f;
    [SerializeField] private float crossfade = 0.35f;
    [SerializeField, Range(0f, 1f)] private float recordedVolume = 0.9f;

    [Header("Dashboard")]
    [SerializeField] private GameObject clusterLights;
    [SerializeField] private TMP_Text warningText;
    [SerializeField] private string warningEnglish = "(!) RIGHT REAR TYRE\nPUNCTURE";
    [SerializeField] private string warningHinglish = "(!) PEECHE DAAYA TYRE\nPUNCTURE HAI";
    [SerializeField] private string okEnglish = "TYRES OK";
    [SerializeField] private string okHinglish = "TYRE THEEK HAI";
    [SerializeField] private Color okColor = new Color(0.35f, 1f, 0.45f);
    [SerializeField] private TaskHUD hud;

    public bool Started { get; private set; }
    /// <summary>Seconds the puncture warning has been on (task completes once the player had time to read it).</summary>
    public float WarningShownFor => _warningSince < 0f ? 0f : Time.time - _warningSince;

    private AudioSource _src, _loopSrc;
    private bool _starting;
    private float _warningSince = -1f;
    private bool _tyreOk;
    private Coroutine _blink;
    private Color _warnColor;
    private static AudioClip _crank, _idle, _beep;

    private void Awake()
    {
        if (hud == null) hud = FindFirstObjectByType<TaskHUD>();
        var host = engineSpot != null ? engineSpot.gameObject : gameObject;
        _src = host.AddComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.spatialBlend = 0.85f;
        _src.minDistance = 1.2f;
        _src.maxDistance = 15f;
        _src.rolloffMode = AudioRolloffMode.Logarithmic;
        _src.dopplerLevel = 0f;
        _loopSrc = host.AddComponent<AudioSource>();
        _loopSrc.playOnAwake = false; _loopSrc.loop = true;
        _loopSrc.spatialBlend = _src.spatialBlend; _loopSrc.minDistance = _src.minDistance; _loopSrc.maxDistance = _src.maxDistance;
        _loopSrc.rolloffMode = _src.rolloffMode; _loopSrc.dopplerLevel = 0f;
        if (clusterLights != null) clusterLights.SetActive(false);
        if (warningText != null) { warningText.gameObject.SetActive(false); _warnColor = warningText.color; }
    }

    private void OnEnable() { if (ignition != null) ignition.TurnedWithBattery += OnKey; }
    private void OnDisable() { if (ignition != null) ignition.TurnedWithBattery -= OnKey; }

    private void OnKey()
    {
        if (Started || _starting) return;
        StartCoroutine(StartRoutine());
    }

    private IEnumerator StartRoutine()
    {
        _starting = true;
        if (startClip != null)
        {
            yield return RecordedStart();
            yield break;
        }
        if (clusterLights != null) clusterLights.SetActive(true);      // ignition on: cluster wakes up
        _src.loop = false;
        _src.clip = CrankClip(crankTime);
        _src.volume = 0.8f;
        _src.Play();
        XRHaptics.PulseNear(transform.position, 3f, 0.25f, crankTime);
        yield return new WaitForSeconds(crankTime - 0.05f);

        // it catches: a quick rev, then idle
        _src.clip = IdleClip;
        _src.loop = true;
        _src.Play();
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            float k = t / 0.6f;
            _src.pitch = Mathf.Lerp(1.7f, 1f, k * k);
            _src.volume = Mathf.Lerp(0.9f, idleVolume, k);
            yield return null;
        }
        _src.pitch = 1f;
        _src.volume = idleVolume;
        Started = true;
        _starting = false;
        Debug.Log("[CarEngine] engine running");

        yield return new WaitForSeconds(0.8f);
        ShowWarning();
    }

    // The real recording: crank -> catch at catchTime -> idle. Everything is timed to it.
    private IEnumerator RecordedStart()
    {
        if (clusterLights != null) clusterLights.SetActive(true);     // ignition on: the cluster wakes up
        _src.loop = false; _src.pitch = 1f;
        _src.clip = startClip;
        _src.volume = recordedVolume;
        _src.Play();
        XRHaptics.PulseNear(transform.position, 3f, 0.3f, catchTime);    // the car shakes while cranking
        yield return new WaitForSeconds(catchTime);

        Started = true;                                                  // it caught
        _starting = false;
        XRHaptics.PulseNear(transform.position, 3f, 0.5f, 0.15f);
        Debug.Log("[CarEngine] engine running (recorded)");
        StartCoroutine(WarningLater(warningDelay));

        if (idleLoop == null) yield break;
        float untilFade = startClip.length - catchTime - crossfade;
        if (untilFade > 0f) yield return new WaitForSeconds(untilFade);
        _loopSrc.clip = idleLoop;
        _loopSrc.volume = 0f;
        _loopSrc.Play();
        for (float t = 0f; t < crossfade; t += Time.deltaTime)
        {
            float k = t / crossfade;
            _src.volume = recordedVolume * Mathf.Sqrt(1f - k);
            _loopSrc.volume = recordedVolume * Mathf.Sqrt(k);
            yield return null;
        }
        _src.Stop();
        _loopSrc.volume = recordedVolume;
    }

    private IEnumerator WarningLater(float delay)
    {
        yield return new WaitForSeconds(delay);
        ShowWarning();
    }

    private void ShowWarning()
    {
        if (_tyreOk) return;
        if (warningText != null)
        {
            warningText.text = GameLanguage.Pick(warningEnglish, warningHinglish);
            warningText.gameObject.SetActive(true);
        }
        if (_warningSince < 0f) _warningSince = Time.time;
        if (_blink != null) StopCoroutine(_blink);
        _blink = StartCoroutine(Blink(true));
        if (hud != null)
            hud.ShowToast(GameLanguage.Pick("Dashboard: right rear tyre is punctured!", "Dashboard: peeche ka daaya tyre puncture hai!"), hud.WarnColor);
    }

    private IEnumerator Blink(bool beeps)
    {
        // beep-beep three times, then keep blinking quietly
        for (int i = 0; ; i++)
        {
            if (warningText != null) warningText.enabled = true;
            if (beeps && i < 3) SfxMix.PlayAt(BeepClip, warningText != null ? warningText.transform.position : transform.position, 0.6f, 0f);
            yield return new WaitForSeconds(0.55f);
            if (warningText != null) warningText.enabled = false;
            yield return new WaitForSeconds(0.35f);
        }
    }

    /// <summary>Key turned off (getting out of the car): the engine dies down, but the ignition stays on,
    /// so the dashboard warning keeps blinking (quietly).</summary>
    public void SwitchOff()
    {
        if (!Started && !_starting) return;
        bool warned = _warningSince >= 0f;
        StopAllCoroutines();
        _blink = null;
        _starting = false;
        Started = false;
        if (!warned) ShowWarning();                       // got out before it came up - show it anyway
        else if (!_tyreOk && warningText != null && warningText.gameObject.activeSelf) _blink = StartCoroutine(Blink(false));
        StartCoroutine(DieDown());
        Debug.Log("[CarEngine] engine off");
    }

    private IEnumerator DieDown()
    {
        float v1 = _src.volume, v2 = _loopSrc != null ? _loopSrc.volume : 0f;
        for (float t = 0f; t < 0.9f; t += Time.deltaTime)
        {
            float k = t / 0.9f;
            _src.volume = v1 * (1f - k);
            _src.pitch = 1f - 0.3f * k;
            if (_loopSrc != null) { _loopSrc.volume = v2 * (1f - k); _loopSrc.pitch = 1f - 0.3f * k; }
            yield return null;
        }
        _src.Stop(); _src.pitch = 1f;
        if (_loopSrc != null) { _loopSrc.Stop(); _loopSrc.pitch = 1f; }
    }

    /// <summary>Driving: engine note follows the speed (0..1) and how hard the pedal is pressed (0..1).</summary>
    public void SetDrive(float speed01, float pedal01)
    {
        if (!Started || _loopSrc == null) return;
        float target = 1f + 0.55f * Mathf.Clamp01(speed01) + 0.15f * Mathf.Clamp01(pedal01);
        var src = _loopSrc.isPlaying ? _loopSrc : _src;
        if (src == null || !src.isPlaying) return;
        if (src == _src && src.clip == startClip) return;          // still on the start recording
        src.pitch = Mathf.MoveTowards(src.pitch, target, Time.deltaTime * 0.8f);
    }

    /// <summary>Tyre fixed: the warning goes green for a moment and then off for good.</summary>
    public void ClearWarning()
    {
        if (_tyreOk) return;
        _tyreOk = true;
        if (_blink != null) { StopCoroutine(_blink); _blink = null; }
        StartCoroutine(OkFlash());
        if (hud != null)
            hud.ShowToast(GameLanguage.Pick("Dashboard: tyre warning gone!", "Dashboard: puncture ki warning chali gayi!"), hud.GoodColor, false);
    }

    private IEnumerator OkFlash()
    {
        if (warningText == null) yield break;
        warningText.gameObject.SetActive(true);
        warningText.enabled = true;
        warningText.text = GameLanguage.Pick(okEnglish, okHinglish);
        warningText.color = okColor;
        SfxMix.PlayAt(BeepClip, warningText.transform.position, 0.4f, 0f);
        yield return new WaitForSeconds(4f);
        warningText.gameObject.SetActive(false);
        warningText.color = _warnColor;
    }

    /// <summary>Engine off (e.g. later, when the car has been fixed and driven out).</summary>
    public void StopEngine()
    {
        StopAllCoroutines();
        _src.Stop();
        if (_loopSrc != null) _loopSrc.Stop();
        Started = false;
        _starting = false;
        if (warningText != null) warningText.gameObject.SetActive(false);
        if (clusterLights != null) clusterLights.SetActive(false);
    }

    // ---------------- generated sounds ----------------
    private static AudioClip CrankClip(float seconds)
    {
        if (_crank != null) return _crank;
        const int rate = 22050; int len = (int)(rate * seconds);
        var d = new float[len]; var rnd = new System.Random(9);
        float ph = 0f, lp = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / rate;
            float comp = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 7.5f * t);          // compression strokes
            float f = 95f + 25f * comp;
            ph += 2f * Mathf.PI * f / rate;
            float saw = (ph / Mathf.PI % 2f) - 1f;
            float n = (float)(rnd.NextDouble() * 2 - 1);
            lp = Mathf.Lerp(lp, n, 0.2f);
            float env = Mathf.Min(1f, t * 20f) * Mathf.Min(1f, (seconds - t) * 15f);
            d[i] = (saw * 0.35f + lp * 0.35f) * (0.45f + 0.55f * comp) * env;
        }
        _crank = AudioClip.Create("Crank", len, 1, rate, false);
        _crank.SetData(d, 0);
        return _crank;
    }

    private static AudioClip IdleClip
    {
        get
        {
            if (_idle != null) return _idle;
            const int rate = 22050; int len = rate;                      // exactly 1 s, loops seamlessly
            var d = new float[len]; var rnd = new System.Random(4);
            float fire = 25f;                                             // ~750 rpm, 4 cylinders
            float[] amp = new float[25]; for (int k = 0; k < 25; k++) amp[k] = 0.75f + (float)rnd.NextDouble() * 0.5f;
            float lp = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / rate;
                int cyc = Mathf.Min(24, (int)(t * fire));
                float pulse = Mathf.Exp(-((t * fire) % 1f) * 5f) * amp[cyc];   // each firing thump
                float body = 0.5f * Mathf.Sin(2f * Mathf.PI * 50f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * 75f * t)
                           + 0.25f * Mathf.Sin(2f * Mathf.PI * 100f * t) + 0.12f * Mathf.Sin(2f * Mathf.PI * 150f * t);
                float n = (float)(rnd.NextDouble() * 2 - 1); lp = Mathf.Lerp(lp, n, 0.08f);
                d[i] = (body * (0.35f + 0.65f * pulse) + lp * 0.5f * pulse) * 0.55f;
            }
            _idle = AudioClip.Create("Idle", len, 1, rate, false);
            _idle.SetData(d, 0);
            return _idle;
        }
    }

    private static AudioClip BeepClip
    {
        get
        {
            if (_beep != null) return _beep;
            const int rate = 22050; int len = (int)(rate * 0.18f);
            var d = new float[len];
            for (int i = 0; i < len; i++) { float t = (float)i / rate; d[i] = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 1850f * t)) * 0.25f * Mathf.Min(1f, t * 200f) * Mathf.Min(1f, (0.18f - t) * 60f); }
            _beep = AudioClip.Create("Beep", len, 1, rate, false);
            _beep.SetData(d, 0);
            return _beep;
        }
    }
}
