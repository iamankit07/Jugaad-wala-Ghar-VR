using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CONTROLS page of the pause menu: every button the game uses, on two tabs
/// (BASICS / CAR &amp; TOOLS), in English or Hinglish (GameLanguage). Rows are built at runtime.
/// </summary>
public class ControlsInfo : MonoBehaviour
{
    [SerializeField] private RectTransform rows;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Button basicsTab;
    [SerializeField] private Button carTab;
    [SerializeField] private float rowTop = 215f;
    [SerializeField] private float rowStep = 62f;

    static readonly Color On = new Color(0.95f, 0.64f, 0.23f);
    static readonly Color OnHi = new Color(1f, 0.76f, 0.40f);
    static readonly Color Off = new Color(0.20f, 0.22f, 0.29f);
    static readonly Color OffHi = new Color(0.30f, 0.33f, 0.42f);
    static readonly Color KeyColor = new Color(0.95f, 0.64f, 0.23f);
    static readonly Color TextColor = new Color(0.92f, 0.93f, 0.96f);

    // key (EN), key (HI), what it does (EN), what it does (HI)
    static readonly string[][] Basics =
    {
        new[] { "Left stick", "Left stick",
                "Walk around. Teleport mode: push forward to aim, let go to jump there.",
                "Chalo-phiro. Teleport mode: stick aage karke nishana lagao, chhodo toh wahan pahunch jaoge." },
        new[] { "Right stick", "Right stick", "Turn left / right.", "Left / right ghoomo." },
        new[] { "Grip (side button)", "Grip (side wala button)",
                "Grab and hold things. Let go to drop them.", "Cheezein pakdo. Chhodo toh gir jaayengi." },
        new[] { "Grip, both hands", "Dono haath se Grip",
                "Hold a rubber band with both hands and pull to stretch it.", "Rubber band dono haathon se pakad ke kheencho." },
        new[] { "Trigger", "Trigger",
                "Use what you're holding: TV remote, phone call, phone torch.", "Haath ki cheez use karo: TV remote, phone call, phone torch." },
        new[] { "Index finger", "Ungli",
                "Touch switches and 3D buttons to press them.", "Switch aur buttons ko ungli se chhoo ke dabao." },
        new[] { "B (right)", "B (right)",
                "Take the phone out of your pocket / put it back. Hints are on its screen.", "Phone jeb se nikalo / wapas rakho. Hint phone ki screen pe hai." },
        new[] { "Menu (left)", "Menu (left)",
                "Open / close this menu. Point at a button and press trigger.", "Yeh menu kholo / band karo. Button pe point karke trigger dabao." },
    };

    static readonly string[][] Car =
    {
        new[] { "A (right)", "A (right)",
                "Get in / out of the car. Stand by the driver's door.", "Gaadi mein baitho / utro. Driver ke darwaze ke paas khade raho." },
        new[] { "Car key", "Gaadi ki chaabi",
                "Grab the key and twist your wrist (or press trigger) to start the engine.", "Chaabi pakad ke kalai ghumao (ya trigger dabao) - engine start." },
        new[] { "Left trigger", "Left trigger",
                "Accelerator - once the engine is running.", "Accelerator - engine chalu hone ke baad." },
        new[] { "Right trigger", "Right trigger",
                "Brake. Keep holding after the car stops to reverse.", "Brake. Gaadi rukne ke baad dabaye rakho toh reverse." },
        new[] { "Steering wheel", "Steering",
                "Grab the rim (grip) and turn it to steer.", "Steering pakad ke (grip) ghumao." },
        new[] { "Spanner", "Spanner",
                "Put it on a nut. Anticlockwise opens it, clockwise tightens it.", "Nut pe lagao. Ulta ghumao toh khulega, seedha ghumao toh tight." },
        new[] { "Jack handle", "Jack ka handle",
                "Grab the knob and wind it in circles - clockwise lifts the car.", "Knob pakad ke gol ghumao - seedha ghumane se gaadi upar uthegi." },
    };

    private int _page;

    private void OnEnable()
    {
        GameLanguage.Changed += Rebuild;
        Rebuild();
    }

    private void OnDisable() { GameLanguage.Changed -= Rebuild; }

    public void ShowBasics() { _page = 0; Rebuild(); }
    public void ShowCar()    { _page = 1; Rebuild(); }

    /// <summary>Open on the first tab (called when the page is opened).</summary>
    public void ResetPage() { _page = 0; Rebuild(); }

    public int RowCount => rows ? rows.childCount / 2 : 0;

    private void Rebuild()
    {
        if (rows == null) return;
        for (int i = rows.childCount - 1; i >= 0; i--) Destroy(rows.GetChild(i).gameObject);
        rows.DetachChildren();

        bool en = GameLanguage.IsEnglish;
        var data = _page == 0 ? Basics : Car;
        for (int i = 0; i < data.Length; i++)
        {
            float y = rowTop - i * rowStep;
            Text(en ? data[i][0] : data[i][1], new Vector2(-215f, y), new Vector2(200f, rowStep - 2f), 26f, KeyColor, FontStyles.Bold);
            Text(en ? data[i][2] : data[i][3], new Vector2(110f, y), new Vector2(430f, rowStep - 2f), 24f, TextColor, FontStyles.Normal);
        }
        Tint(basicsTab, _page == 0);
        Tint(carTab, _page == 1);
    }

    private void Text(string s, Vector2 pos, Vector2 size, float fs, Color c, FontStyles style)
    {
        var go = new GameObject("Row", typeof(RectTransform));
        go.transform.SetParent(rows, false);
        go.layer = rows.gameObject.layer;
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font) t.font = font;
        t.text = s;
        t.fontSize = fs;
        t.color = c;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        t.rectTransform.anchoredPosition = pos;
        t.rectTransform.sizeDelta = size;
    }

    private static void Tint(Button b, bool on)
    {
        if (b == null) return;
        var cb = b.colors;
        cb.normalColor = on ? On : Off;
        cb.selectedColor = cb.normalColor;
        cb.highlightedColor = on ? OnHi : OffHi;
        b.colors = cb;
        var label = b.GetComponentInChildren<TMP_Text>(true);
        if (label) label.color = on ? new Color(0.12f, 0.08f, 0.04f) : new Color(0.94f, 0.94f, 0.96f);
    }
}
