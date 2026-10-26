using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// End-of-level results card, floating in front of the player: 'LEVEL n COMPLETE!', 1-3 stars that
/// pop in one by one, the score counting up, time taken, best score (saved on the headset) and a tip
/// for the next star. Stars: 3 at masterScore+, 2 at goodScore+, else 1 (same as the rank).
/// Built at runtime, so the same component works in every level. EndCelebration calls Show().
/// </summary>
public class LevelResults : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Sprite starSprite;
    [SerializeField] private Sprite roundSprite;
    [SerializeField] private AudioClip starSound;            // optional, else a procedural chime
    [SerializeField] private float distance = 1.15f;
    [SerializeField] private float heightOffset = 0.02f;
    [SerializeField] private float minDistance = 0.6f;

    static readonly Color Panel = new Color(0.09f, 0.10f, 0.14f, 0.96f);
    static readonly Color Band = new Color(0.95f, 0.64f, 0.23f, 0.14f);
    static readonly Color Orange = new Color(0.95f, 0.64f, 0.23f);
    static readonly Color Gold = new Color(1f, 0.80f, 0.22f);
    static readonly Color EmptyStar = new Color(0.22f, 0.24f, 0.31f);
    static readonly Color Box = new Color(0.14f, 0.15f, 0.21f);
    static readonly Color Grey = new Color(0.62f, 0.64f, 0.71f);
    static readonly Color White = new Color(0.95f, 0.95f, 0.97f);

    public bool Shown { get; private set; }
    public int StarsShown { get; private set; }

    private RectTransform _root;
    private CanvasGroup _group;
    private RectTransform[] _fill = new RectTransform[3];
    private TextMeshProUGUI _rank, _score, _time, _best, _bestLabel, _tip;
    private AudioSource _audio;
    private readonly RaycastHit[] _hits = new RaycastHit[16];

    public void Show(int level, int score, float seconds, int stars, string rank, int goodScore, int masterScore, Transform head)
    {
        if (Shown) return;
        Shown = true;
        stars = Mathf.Clamp(stars, 1, 3);
        bool newBest = LevelProgress.SubmitScore(level, score, stars);
        int best = LevelProgress.GetBest(level);

        Build(level);
        Place(head);
        _time.text = Mathf.FloorToInt(seconds / 60f) + ":" + Mathf.FloorToInt(seconds % 60f).ToString("00");
        _best.text = best.ToString();
        _bestLabel.text = newBest ? GameLanguage.Pick("NEW BEST!", "NAYA BEST!") : "BEST";
        _bestLabel.color = newBest ? Orange : Grey;
        _rank.text = rank;
        _tip.text = Tip(stars, goodScore, masterScore);
        StartCoroutine(Animate(score, stars, head));
    }

    private static string Tip(int stars, int good, int master)
    {
        if (stars >= 3) return GameLanguage.Pick("Perfect jugaad!", "Ekdum perfect jugaad!");
        int need = stars == 2 ? master : good;
        return GameLanguage.Pick("Score " + need + "+ for " + (stars + 1) + " stars - be quicker and use fewer hints",
                                 (stars + 1) + " star ke liye " + need + "+ score chahiye - jaldi karo, hint kam lo");
    }

    // ---------------- build ----------------

    private void Build(int level)
    {
        var go = new GameObject("LevelResultsCard", typeof(RectTransform));
        go.layer = 5;
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        c.sortingOrder = 40;
        go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
        _group = go.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.interactable = false;
        _group.blocksRaycasts = false;
        _root = (RectTransform)go.transform;
        _root.sizeDelta = new Vector2(820f, 600f);
        _root.localScale = Vector3.one * 0.001f;

        var bg = Img(_root, "Background", roundSprite, Panel, Vector2.zero, new Vector2(820f, 600f));
        bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = 0.25f;
        var band = Img(_root, "Band", roundSprite, Band, new Vector2(0f, 245f), new Vector2(780f, 92f));
        band.type = Image.Type.Sliced; band.pixelsPerUnitMultiplier = 0.35f;
        Txt(_root, "Header", GameLanguage.Pick("LEVEL " + level + " COMPLETE!", "LEVEL " + level + " PURA!"), 58f, Orange, new Vector2(0f, 245f), new Vector2(760f, 80f), FontStyles.Bold);

        Star(0, new Vector2(-180f, 92f), 128f, 14f);
        Star(1, new Vector2(0f, 112f), 168f, 0f);
        Star(2, new Vector2(180f, 92f), 128f, -14f);

        _rank = Txt(_root, "Rank", string.Empty, 44f, White, new Vector2(0f, -22f), new Vector2(760f, 60f), FontStyles.Bold);
        _rank.alpha = 0f;

        TextMeshProUGUI unused;
        _score = Stat("SCORE", -250f, out unused);
        _time = Stat("TIME", 0f, out unused);
        _best = Stat("BEST", 250f, out _bestLabel);
        _score.text = "0";

        _tip = Txt(_root, "Tip", string.Empty, 24f, Grey, new Vector2(0f, -252f), new Vector2(760f, 60f), FontStyles.Normal);
        _tip.alpha = 0f;

        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 5;

        _audio = go.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;
    }

    private void Star(int i, Vector2 pos, float size, float rot)
    {
        var back = Img(_root, "Star" + i, starSprite, EmptyStar, pos, new Vector2(size, size));
        back.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rot);
        var fill = Img(back.rectTransform, "Fill", starSprite, Gold, Vector2.zero, new Vector2(size, size));
        fill.rectTransform.localScale = Vector3.zero;
        _fill[i] = fill.rectTransform;
    }

    private TextMeshProUGUI Stat(string label, float x, out TextMeshProUGUI labelText)
    {
        var box = Img(_root, label + "Box", roundSprite, Box, new Vector2(x, -140f), new Vector2(228f, 112f));
        box.type = Image.Type.Sliced; box.pixelsPerUnitMultiplier = 0.35f;
        labelText = Txt(box.rectTransform, "Label", label, 22f, Grey, new Vector2(0f, 30f), new Vector2(220f, 30f), FontStyles.Bold);
        return Txt(box.rectTransform, "Value", string.Empty, 46f, White, new Vector2(0f, -14f), new Vector2(220f, 56f), FontStyles.Bold);
    }

    private Image Img(RectTransform parent, string name, Sprite s, Color c, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = s;
        img.color = c;
        img.raycastTarget = false;
        img.rectTransform.anchoredPosition = pos;
        img.rectTransform.sizeDelta = size;
        return img;
    }

    private TextMeshProUGUI Txt(RectTransform parent, string name, string text, float fs, Color c, Vector2 pos, Vector2 size, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font) t.font = font;
        t.text = text;
        t.fontSize = fs;
        t.color = c;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        t.rectTransform.anchoredPosition = pos;
        t.rectTransform.sizeDelta = size;
        return t;
    }

    private void Place(Transform head)
    {
        if (head == null) return;
        Vector3 fwd = head.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        fwd.Normalize();
        float d = distance;
        int n = Physics.RaycastNonAlloc(head.position, fwd, _hits, distance + 0.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var col = _hits[i].collider;
            if (col == null || col.transform.IsChildOf(head.root)) continue;
            if (col.attachedRigidbody != null && col.bounds.size.magnitude < 0.6f) continue;
            d = Mathf.Min(d, _hits[i].distance - 0.1f);
        }
        d = Mathf.Max(minDistance, d);
        Vector3 pos = head.position + fwd * d + Vector3.up * heightOffset;
        _root.position = pos;
        _root.rotation = Quaternion.LookRotation(pos - head.position, Vector3.up);
    }

    // ---------------- animation (unscaled, so it also runs if the game is paused) ----------------

    private IEnumerator Animate(int score, int stars, Transform head)
    {
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.35f);
            _group.alpha = k;
            _root.localScale = Vector3.one * 0.001f * Mathf.Lerp(0.88f, 1f, k);
            yield return null;
        }
        _group.alpha = 1f;
        _root.localScale = Vector3.one * 0.001f;

        // score counts up
        float dur = 1.1f, nextTick = 0f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / dur, 3f);
            _score.text = Mathf.RoundToInt(score * k).ToString();
            if (t >= nextTick) { nextTick = t + 0.07f; _audio.pitch = 1.4f + k * 0.6f; _audio.PlayOneShot(ProceduralSfx.Tick, 0.25f); }
            yield return null;
        }
        _score.text = score.ToString();
        yield return new WaitForSecondsRealtime(0.15f);

        // stars, one by one
        for (int i = 0; i < stars; i++)
        {
            _audio.pitch = 1f + i * 0.18f;
            _audio.PlayOneShot(starSound ? starSound : ProceduralSfx.Success, 0.8f);
            if (head) XRHaptics.PulseNear(head.position, 1.5f, 0.35f + i * 0.15f, 0.1f);
            var fill = _fill[i];
            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.3f;
                float s = k < 0.6f ? Mathf.Lerp(0f, 1.3f, k / 0.6f) : Mathf.Lerp(1.3f, 1f, (k - 0.6f) / 0.4f);
                fill.localScale = Vector3.one * s;
                yield return null;
            }
            fill.localScale = Vector3.one;
            StarsShown = i + 1;
            yield return new WaitForSecondsRealtime(0.22f);
        }
        _audio.pitch = 1f;

        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            _rank.alpha = t / 0.3f;
            _tip.alpha = t / 0.3f;
            yield return null;
        }
        _rank.alpha = 1f;
        _tip.alpha = 1f;
    }

    /// <summary>Fade the card away (a button was pressed - next level / replay / menu).</summary>
    public void Hide()
    {
        if (_root == null || !_root.gameObject.activeSelf) return;
        StopAllCoroutines();
        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        float a0 = _group.alpha;
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            _group.alpha = Mathf.Lerp(a0, 0f, t / 0.25f);
            yield return null;
        }
        _root.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_root) Destroy(_root.gameObject);
    }
}
