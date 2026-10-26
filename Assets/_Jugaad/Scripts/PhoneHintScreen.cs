using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// The phone's screen after the intro call: current task, and its hint. Until the hint is
/// available it shows a countdown; when it arrives the phone buzzes (in the pocket: the right
/// controller vibrates) and plays a notification tick. GameFlowManager drives it.
/// Put it on the phone, next to IntroPhoneCall / PhoneSummon.
/// </summary>
public class PhoneHintScreen : MonoBehaviour
{
    [Tooltip("Hint screen waits until the call screen is done. Empty = IntroPhoneCall on this object.")]
    [SerializeField] private IntroPhoneCall introCall;
    [SerializeField] private TMP_FontAsset font;
    [Tooltip("Text area on the phone screen (local space). The screen faces local +Z.")]
    [SerializeField] private Vector3 localPos = new Vector3(0f, 0f, 0.0058f);
    [SerializeField] private Vector2 size = new Vector2(0.058f, 0.124f);
    [SerializeField] private float maxFontSize = 0.08f;
    [SerializeField] private float minFontSize = 0.04f;
    [Tooltip("Fraction of the screen (from the top) the text may use - the hand covers the bottom.")]
    [SerializeField, Range(0.4f, 1f)] private float usableHeight = 0.8f;

    [Header("Colours")]
    [SerializeField] private Color headerColor = new Color(0.7f, 0.7f, 0.75f);
    [SerializeField] private Color titleColor = Color.white;
    [SerializeField] private Color hintColor = new Color(1f, 0.85f, 0.35f);
    [SerializeField] private Color waitColor = new Color(0.6f, 0.75f, 1f);
    [SerializeField] private Color doneColor = new Color(0.45f, 1f, 0.45f);

    [Header("New hint notification")]
    [SerializeField] private AudioClip notifySound;
    [SerializeField] private float notifyHaptic = 0.5f;

    private TextMeshPro _text;
    private PhoneSummon _pocket;
    private XRGrabInteractable _grab;
    private string _header = "", _title = "", _hint = "", _footer = "";
    private float _hintAt = -1f;
    private bool _done, _hasContent;
    private int _lastSecs = -1;
    private bool _needsFit;

    private void Awake()
    {
        _pocket = GetComponent<PhoneSummon>();
        _grab = GetComponent<XRGrabInteractable>();
        if (introCall == null) introCall = GetComponent<IntroPhoneCall>();

        var go = new GameObject("HintScreen");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // same as the call texts: readable from +Z
        _text = go.AddComponent<TextMeshPro>();
        if (font) _text.font = font;
        _text.rectTransform.sizeDelta = size;
        _text.enableAutoSizing = false;
        _text.fontSize = maxFontSize;
        _text.alignment = TextAlignmentOptions.Top;
        _text.textWrappingMode = TextWrappingModes.Normal;
        _text.overflowMode = TextOverflowModes.Truncate;
        _text.richText = true;
        _text.color = Color.white;
        _text.text = string.Empty;
        var r = _text.GetComponent<MeshRenderer>();
        if (r) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
        _text.enabled = false;
    }

    // ---------------- API (GameFlowManager) ----------------

    public void SetTask(int number, int total, string title)
    {
        _header = "TASK " + number + "/" + total;
        _title = title;
        _hint = _footer = string.Empty;
        _hintAt = -1f;
        _done = false;
        _hasContent = true;
        Refresh();
    }

    /// <summary>Hint not available yet: show a countdown to this Time.time.</summary>
    public void SetHintPending(float availableAtTime)
    {
        _hint = string.Empty;
        _hintAt = availableAtTime;
        _lastSecs = -1;
        Refresh();
    }

    public void SetHint(string hint, bool notify)
    {
        _hint = hint ?? string.Empty;
        _hintAt = -1f;
        Refresh();
        if (notify) Notify();
    }

    public void ShowDone(string doneText)
    {
        _done = true;
        _title = doneText;
        _hint = string.Empty;
        _hintAt = -1f;
        Refresh();
    }

    public void ShowEnd(int score, string rank)
    {
        _header = GameLanguage.Pick("GAME OVER", "GAME KHATAM");
        _title = rank;
        _hint = string.Empty;
        _hintAt = -1f;
        _done = true;
        _footer = "Score: " + score;
        _hasContent = true;
        Refresh();
    }

    // ---------------- internals ----------------

    private void Update()
    {
        bool show = _hasContent && (introCall == null || introCall.ScreenFree);
        if (_text.enabled != show) _text.enabled = show;
        if (show && _needsFit && _text.gameObject.activeInHierarchy) Fit();
        if (show && _hintAt > 0f)
        {
            int secs = Mathf.Max(0, Mathf.CeilToInt(_hintAt - Time.time));
            if (secs != _lastSecs) { _lastSecs = secs; Refresh(); }
        }
    }

    private void Refresh()
    {
        if (_text == null) return;
        var sb = new System.Text.StringBuilder(256);
        sb.Append("<size=80%>").Append(Col(headerColor)).Append(_header).Append("</color></size>\n");
        sb.Append("<b>").Append(Col(_done ? doneColor : titleColor)).Append(_title).Append("</color></b>\n\n");

        if (!_done)
        {
            if (!string.IsNullOrEmpty(_hint))
            {
                sb.Append(Col(hintColor)).Append("<b>HINT:</b>\n").Append(_hint).Append("</color>");
            }
            else if (_hintAt > 0f)
            {
                int secs = Mathf.Max(0, Mathf.CeilToInt(_hintAt - Time.time));
                string t = (secs / 60) + ":" + (secs % 60).ToString("00");
                sb.Append(Col(waitColor)).Append(GameLanguage.Pick("Hint in " + t, "Hint " + t + " mein aayega")).Append("</color>");
            }
        }
        if (!string.IsNullOrEmpty(_footer)) sb.Append(Col(hintColor)).Append(_footer).Append("</color>");
        _text.text = sb.ToString();
        _needsFit = true;
    }

    private static string Col(Color c) => "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">";

    /// <summary>TMP auto-size picks too small a size on this tiny screen, so fit by hand:
    /// the biggest size whose text still fits the screen.</summary>
    private void Fit()
    {
        _needsFit = false;
        float h = size.y * usableHeight;
        for (float fs = maxFontSize; fs >= minFontSize; fs -= 0.005f)
        {
            _text.fontSize = fs;
            _text.ForceMeshUpdate();
            if (!_text.isTextOverflowing && _text.textBounds.size.y <= h) return;
        }
        _text.fontSize = minFontSize;
    }

    private void Notify()
    {
        SfxMix.PlayAt(notifySound != null ? notifySound : ProceduralSfx.Tick, transform.position, 0.8f);
        if (_grab != null && _grab.isSelected) XRHaptics.PulseHolders(_grab, notifyHaptic, 0.15f);
        else if (_pocket != null && _pocket.IsPocketed && _pocket.Hand != null) XRHaptics.Pulse(_pocket.Hand, notifyHaptic, 0.15f);
    }
}
