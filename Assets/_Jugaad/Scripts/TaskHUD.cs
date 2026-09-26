using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Floating task panel: header (TASK n/7) + score, the task title (typed like a typewriter),
/// an optional hint line, and a toast line for +points / reminders. Builds its own
/// children at runtime; put HudFollow on the same object to keep it in view.
/// </summary>
public class TaskHUD : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Material panelMaterial;
    [SerializeField] private Vector2 panelSize = new Vector2(0.82f, 0.34f);
    [SerializeField] private float charsPerSecond = 32f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.35f;
    [SerializeField] private AudioClip typeSound, successSound, reminderSound;

    [Header("Colours")]
    [SerializeField] private Color titleColor = Color.white;
    [SerializeField] private Color doneColor = new Color(0.45f, 1f, 0.45f);
    [SerializeField] private Color hintColor = new Color(1f, 0.85f, 0.35f);
    [SerializeField] private Color headerColor = new Color(0.7f, 0.7f, 0.75f);
    [SerializeField] private Color scoreColor = new Color(1f, 0.8f, 0.2f);
    [SerializeField] private Color goodToast = new Color(0.45f, 1f, 0.45f);
    [SerializeField] private Color warnToast = new Color(1f, 0.55f, 0.3f);

    private TextMeshPro _header, _score, _title, _hint, _toast;
    private AudioSource _audio;
    private Coroutine _titleCo, _hintCo, _toastCo;
    private bool _built;

    public Color WarnColor => warnToast;
    public Color GoodColor => goodToast;

    private void Awake() { Build(); }

    private void Build()
    {
        if (_built) return;
        _built = true;

        var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        panel.name = "Panel";
        Destroy(panel.GetComponent<Collider>());
        panel.transform.SetParent(transform, false);
        panel.transform.localPosition = new Vector3(0f, 0f, 0.01f);
        panel.transform.localScale = new Vector3(panelSize.x, panelSize.y, 1f);
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
        _title.fontStyle = FontStyles.Bold;

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
        t.rectTransform.sizeDelta = size;
        t.rectTransform.localPosition = pos;
        t.fontSize = fontSize;
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
        _header.text = "TASK " + number + "/" + total;
        _title.color = titleColor;
        _hint.text = string.Empty;
        _titleCo = StartCoroutine(TypeThenHint(title, hintOrNull));
        return TypeTime(title);
    }

    public void ShowHint(string hint)
    {
        Build();
        if (_hintCo != null) StopCoroutine(_hintCo);
        _hintCo = StartCoroutine(Type(_hint, "HINT: " + hint));
    }

    public void ShowCompleted(string doneText, int points, int totalScore, bool hintUsed)
    {
        Build();
        StopTyping();
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
        _header.text = string.Empty;
        _title.color = titleColor;
        _hint.text = string.Empty;
        _titleCo = StartCoroutine(TypeThenHint(big, null, small));
    }

    public void ShowEnd(int score, float seconds, string rank)
    {
        Build();
        StopTyping();
        int m = Mathf.FloorToInt(seconds / 60f), s = Mathf.FloorToInt(seconds % 60f);
        _header.text = GameLanguage.Pick("GAME OVER", "GAME KHATAM");
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
