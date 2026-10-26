using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Floating task panel: header (TASK n/N) + score, the task title (typed like a typewriter),
/// an optional hint line, a toast line for +points / reminders, and a fixed footer that says
/// how to open the settings menu (left MENU button). Builds its own
/// children at runtime; put HudFollow on the same object to keep it in view.
/// </summary>
public class TaskHUD : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Material panelMaterial;
    [SerializeField] private Vector2 panelSize = new Vector2(0.82f, 0.34f);
    [SerializeField] private float charsPerSecond = 32f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.35f;
    [Tooltip("Multiplies every HUD font size (1 = original).")]
    [SerializeField] private float fontScale = 1.15f;
    [Tooltip("Optional text material (e.g. an always-on-top overlay). Empty = the font's default.")]
    [SerializeField] private Material textMaterial;
    [SerializeField] private AudioClip typeSound, successSound, reminderSound;

    [Header("Colours")]
    [SerializeField] private Color titleColor = Color.white;
    [SerializeField] private Color doneColor = new Color(0.45f, 1f, 0.45f);
    [SerializeField] private Color hintColor = new Color(1f, 0.85f, 0.35f);
    [SerializeField] private Color headerColor = new Color(0.7f, 0.7f, 0.75f);
    [SerializeField] private Color scoreColor = new Color(1f, 0.8f, 0.2f);
    [SerializeField] private Color goodToast = new Color(0.45f, 1f, 0.45f);
    [SerializeField] private Color warnToast = new Color(1f, 0.55f, 0.3f);

    [Header("Footer (always shown)")]
    [Tooltip("Extra strip at the bottom of the panel for the 'how to open settings' note.")]
    [SerializeField] private float footerHeight = 0.045f;
    [SerializeField] private Color footerColor = new Color(0.62f, 0.64f, 0.70f);
    [SerializeField] private string footerEn = "Settings: press the <color=#F2A33A><b>MENU</b></color> button on the left controller";
    [SerializeField] private string footerHi = "Settings: left controller ka <color=#F2A33A><b>MENU</b></color> button dabao";

    private TextMeshPro _header, _score, _title, _hint, _toast, _footer;
    // the task on screen, so it can come back after a phone call's subtitles
    private bool _hasTask;
    private string _taskHeader, _taskTitle, _taskSecond;
    private AudioSource _audio;
    private Coroutine _titleCo, _hintCo, _toastCo;
    private bool _built;
    private float _titleSize;

    [Tooltip("Hints are shown on the phone; the panel only says 'check your phone (B)'.")]
    [SerializeField] private bool hintsOnPhone = true;

    public Color WarnColor => warnToast;
    public Color GoodColor => goodToast;

    // The hint itself is on the phone (PhoneHintScreen); the panel only points to it.
    private string PhoneHintWait => GameLanguage.Pick("Need a hint? Check your phone (press B)", "Hint chahiye? Phone check karo (B dabao)");
    private string PhoneHintReady => GameLanguage.Pick("Hint is on your phone - press B", "Hint phone pe aa gaya - B dabao");

    private void Awake() { Build(); }

    private void Build()
    {
        if (_built) return;
        _built = true;

        var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        panel.name = "Panel";
        Destroy(panel.GetComponent<Collider>());
        panel.transform.SetParent(transform, false);
        // the panel grows downwards by footerHeight; everything above stays where it was
        panel.transform.localPosition = new Vector3(0f, -footerHeight * 0.5f, 0.01f);
        panel.transform.localScale = new Vector3(panelSize.x, panelSize.y + footerHeight, 1f);
        var pr = panel.GetComponent<MeshRenderer>();
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;
        if (panelMaterial) pr.sharedMaterial = panelMaterial;

        float w = panelSize.x - 0.07f;
        float top = panelSize.y * 0.5f;
        _header = MakeText("Header", new Vector3(0f, top - 0.035f, 0f), new Vector2(w, 0.05f), 0.16f, TextAlignmentOptions.Left, headerColor);
        _score  = MakeText("Score",  new Vector3(0f, top - 0.035f, 0f), new Vector2(w, 0.05f), 0.16f, TextAlignmentOptions.Right, scoreColor);
        _title  = MakeText("Title",  new Vector3(0f, 0.035f, 0f), new Vector2(w, 0.11f), 0.30f, TextAlignmentOptions.Center, titleColor);
        _hint   = MakeText("Hint",   new Vector3(0f, -0.07f, 0f), new Vector2(w, 0.10f), 0.20f, TextAlignmentOptions.Center, hintColor);
        _toast  = MakeText("Toast",  new Vector3(0f, -top + 0.03f, 0f), new Vector2(w, 0.05f), 0.19f, TextAlignmentOptions.Center, goodToast);
        _footer = MakeText("Footer", new Vector3(0f, -top - footerHeight * 0.5f + 0.006f, 0f), new Vector2(w, footerHeight), 0.18f, TextAlignmentOptions.Center, footerColor);
        _footer.text = GameLanguage.Pick(footerEn, footerHi);
        _title.fontStyle = FontStyles.Bold;
        _titleSize = _title.fontSize;

        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;
        _audio.volume = sfxVolume;
    }

    private TextMeshPro MakeText(string n, Vector3 pos, Vector2 size, float fontSize, TextAlignmentOptions align, Color c)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var t = go.AddComponent<TextMeshPro>();
        if (font) t.font = font;
        if (textMaterial) t.fontSharedMaterial = textMaterial;
        t.rectTransform.sizeDelta = size;
        t.rectTransform.localPosition = pos;
        t.fontSize = fontSize * fontScale;
        t.alignment = align;
        t.color = c;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        t.richText = true;
        t.text = string.Empty;
        var r = t.GetComponent<MeshRenderer>();
        if (r) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
        return t;
    }

    // ---------------- public API ----------------

    /// <summary>Shows a new task. Returns roughly how long the title takes to type.</summary>
    public float ShowTask(int number, int total, string title, string hintOrNull)
    {
        Build();
        StopTyping();
        RestoreTitle();
        _header.text = "TASK " + number + "/" + total;
        _title.color = titleColor;
        _hint.color = hintColor;
        _hint.text = string.Empty;
        _hasTask = true;
        _taskHeader = _header.text;
        _taskTitle = title;
        _taskSecond = hintsOnPhone ? (hintOrNull != null ? PhoneHintReady : PhoneHintWait) : (hintOrNull != null ? "HINT: " + hintOrNull : string.Empty);
        if (hintsOnPhone) _titleCo = StartCoroutine(TypeThenHint(title, null, hintOrNull != null ? PhoneHintReady : PhoneHintWait));
        else _titleCo = StartCoroutine(TypeThenHint(title, hintOrNull));
        return TypeTime(title);
    }

    public void ShowHint(string hint)
    {
        Build();
        if (_hintCo != null) StopCoroutine(_hintCo);
        _taskSecond = hintsOnPhone ? PhoneHintReady : "HINT: " + hint;
        _hintCo = StartCoroutine(Type(_hint, _taskSecond));
    }

    public void ShowCompleted(string doneText, int points, int totalScore, bool hintUsed)
    {
        Build();
        _hasTask = false;
        StopTyping();
        RestoreTitle();
        _title.color = doneColor;
        _title.text = doneText;
        _title.maxVisibleCharacters = 99999;
        _hint.text = string.Empty;
        SetScore(totalScore);
        ShowToast("+" + points + " points" + (hintUsed ? GameLanguage.Pick("  (hint used)", "  (hint liya)") : ""), goodToast, false);
        Play(successSound ? successSound : ProceduralSfx.Success, 0.8f);
    }

    public void ShowBanner(string big, string small)
    {
        Build();
        StopTyping();
        RestoreTitle();
        _header.text = string.Empty;
        _title.color = titleColor;
        _hint.text = string.Empty;
        _titleCo = StartCoroutine(TypeThenHint(big, null, small));
    }

    public void ShowEnd(int score, float seconds, string rank, string header = null)
    {
        Build();
        _hasTask = false;
        StopTyping();
        RestoreTitle();
        int m = Mathf.FloorToInt(seconds / 60f), s = Mathf.FloorToInt(seconds % 60f);
        _header.text = !string.IsNullOrEmpty(header) ? header : GameLanguage.Pick("GAME OVER", "GAME KHATAM");
        SetScore(score);
        _title.color = scoreColor;
        _hint.text = string.Empty;
        _titleCo = StartCoroutine(TypeThenHint(rank, null, "Total score: " + score + "   |   Time: " + m + ":" + s.ToString("00")));
        Play(successSound ? successSound : ProceduralSfx.Success, 0.8f);
    }

    public void ShowToast(string msg, Color color, bool errorSound = true)
    {
        Build();
        if (_toastCo != null) StopCoroutine(_toastCo);
        _toastCo = StartCoroutine(ToastRoutine(msg, color));
        if (errorSound) Play(reminderSound ? reminderSound : ProceduralSfx.Error, 0.7f);
    }

    /// <summary>Shows a spoken line (phone call) instantly, no typing sound.</summary>
    public void ShowSubtitle(string speaker, string line)
    {
        Build();
        StopTyping();
        RestoreTitle();
        _header.text = speaker;
        _title.fontSize = _titleSize * 0.72f;
        _title.fontStyle = FontStyles.Normal;
        _title.color = Color.white;
        _title.text = line;
        _title.maxVisibleCharacters = 99999;
        _hint.text = string.Empty;
    }

    /// <summary>Call over: the task that was on screen comes back (or the panel clears if there was none).</summary>
    public void ClearSubtitle()
    {
        Build();
        StopTyping();
        RestoreTitle();
        _hint.color = hintColor;
        if (_hasTask)
        {
            _header.text = _taskHeader;
            _title.color = titleColor;
            _title.text = _taskTitle;
            _title.maxVisibleCharacters = 99999;
            _hint.text = _taskSecond;
            _hint.maxVisibleCharacters = 99999;
            return;
        }
        _header.text = string.Empty;
        _title.text = string.Empty;
        _hint.text = string.Empty;
    }

    private void RestoreTitle()
    {
        if (_title == null || _titleSize <= 0f) return;
        _title.fontSize = _titleSize;
        _title.fontStyle = FontStyles.Bold;
    }

    /// <summary>Hide / show the floating task panel (not the settings icon), e.g. while the results card is up.</summary>
    public void SetPanelVisible(bool visible)
    {
        Build();
        var p = transform.Find("Panel");
        if (p) p.gameObject.SetActive(visible);
        foreach (var t in new TMP_Text[] { _header, _score, _title, _hint, _toast, _footer }) if (t) t.gameObject.SetActive(visible);
    }

    public void SetScore(int score) { Build(); _score.text = "Score: " + score; }

    public float TypeTime(string s) => string.IsNullOrEmpty(s) ? 0f : s.Length / Mathf.Max(1f, charsPerSecond);

    // ---------------- internals ----------------

    private void StopTyping()
    {
        if (_titleCo != null) StopCoroutine(_titleCo);
        if (_hintCo != null) StopCoroutine(_hintCo);
        _titleCo = _hintCo = null;
    }

    private IEnumerator TypeThenHint(string title, string hint, string plainSecondLine = null)
    {
        yield return Type(_title, title);
        if (!string.IsNullOrEmpty(hint)) yield return Type(_hint, "HINT: " + hint);
        else if (!string.IsNullOrEmpty(plainSecondLine)) yield return Type(_hint, plainSecondLine);
    }

    private IEnumerator Type(TextMeshPro t, string s)
    {
        t.text = s;
        t.maxVisibleCharacters = 0;
        t.ForceMeshUpdate();
        int total = t.textInfo.characterCount;
        float shown = 0f;
        int last = 0;
        while (last < total)
        {
            shown += charsPerSecond * Time.deltaTime;
            int n = Mathf.Min(total, Mathf.FloorToInt(shown));
            if (n != last)
            {
                if (n / 2 != last / 2) Play(typeSound ? typeSound : ProceduralSfx.Tick, 0.3f, Random.Range(0.9f, 1.15f));
                last = n;
                t.maxVisibleCharacters = n;
            }
            yield return null;
        }
        t.maxVisibleCharacters = 99999;
    }

    private IEnumerator ToastRoutine(string msg, Color c)
    {
        _toast.text = msg;
        _toast.maxVisibleCharacters = 99999;
        float t = 0f;
        while (t < 3.5f)
        {
            t += Time.deltaTime;
            float a = t < 2.5f ? 1f : Mathf.Clamp01(1f - (t - 2.5f));
            _toast.color = new Color(c.r, c.g, c.b, a);
            yield return null;
        }
        _toast.text = string.Empty;
    }

    private void Play(AudioClip clip, float vol, float pitch = 1f)
    {
        if (_audio == null || clip == null) return;
        _audio.pitch = pitch;
        _audio.PlayOneShot(clip, vol);
    }
}
