using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Main menu: switches between the Main / Levels / Info / Controls / Settings panels and loads a level.
/// START opens level select: Level 1 is always open, Level 2 opens once Level 1 has been completed
/// (LevelProgress, saved on the headset) - after that any level can be played.
/// Settings: language (English / Hinglish), turning (snap / smooth) and movement (smooth / teleport),
/// all saved on the headset (GameLanguage, ComfortSettings).
/// </summary>
public class MainMenu : MonoBehaviour
{
    [SerializeField] private string gameScene = "SampleScene";
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Level select")]
    [SerializeField] private GameObject levelPanel;
    [SerializeField] private string level2Scene = "Garage";
    [SerializeField] private Image level1Button;
    [SerializeField] private Image level2Button;
    [SerializeField] private TMPro.TMP_Text level1Status;
    [SerializeField] private TMPro.TMP_Text level2Status;
    [SerializeField] private TMPro.TMP_Text levelNote;
    [SerializeField] private LocalizedText loadingText;
    [SerializeField] private Color lockedColor = new Color(0.16f, 0.16f, 0.18f);
    [SerializeField, Range(0f, 1f)] private float clickVolume = 0.6f;
    [SerializeField] private AudioClip clickSound, startSound;

    [Header("Language buttons (Settings)")]
    [SerializeField] private Image englishButton;
    [SerializeField] private Image hinglishButton;

    [Header("Comfort buttons (Settings)")]
    [SerializeField] private Image snapTurnButton;
    [SerializeField] private Image smoothTurnButton;
    [SerializeField] private Image smoothMoveButton;
    [SerializeField] private Image teleportButton;

    [Header("Button colours")]
    [SerializeField] private Color selectedColor = new Color(0.95f, 0.64f, 0.23f);
    [SerializeField] private Color normalColor = new Color(0.22f, 0.24f, 0.30f);

    private AudioSource _audio;
    private bool _loading;

    private void Awake()
    {
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;
        Show(mainPanel);
        RefreshButtons();
    }

    public void ShowMain()     { Click(); Show(mainPanel); }
    public void ShowInfo()     { Click(); Show(infoPanel); }
    public void ShowControls() { Click(); Show(controlsPanel); }
    public void ShowSettings() { Click(); Show(settingsPanel); RefreshButtons(); }

    public void SetEnglish()  { Click(); GameLanguage.Current = Language.English;  RefreshButtons(); RefreshLevels(); }
    public void SetHinglish() { Click(); GameLanguage.Current = Language.Hinglish; RefreshButtons(); RefreshLevels(); }

    public void SetSnapTurn()   { Click(); ComfortSettings.SnapTurn = true;  RefreshButtons(); }
    public void SetSmoothTurn() { Click(); ComfortSettings.SnapTurn = false; RefreshButtons(); }
    public void SetSmoothMove() { Click(); ComfortSettings.Teleport = false; RefreshButtons(); }
    public void SetTeleport()   { Click(); ComfortSettings.Teleport = true;  RefreshButtons(); }

    /// <summary>START: open level select (or straight into level 1 if there's no level panel).</summary>
    public void StartGame()
    {
        if (levelPanel == null) { PlayLevel1(); return; }
        Click();
        Show(levelPanel);
        RefreshLevels();
    }

    public void PlayLevel1()
    {
        Load(gameScene, "Opening the house...", "Ghar khul raha hai...");
    }

    public void PlayLevel2()
    {
        if (!LevelProgress.IsUnlocked(2))
        {
            if (_audio) _audio.PlayOneShot(ProceduralSfx.Error, clickVolume);
            if (levelNote) levelNote.text = GameLanguage.Pick("Level 2 is locked - finish Level 1 first!", "Level 2 band hai - pehle Level 1 poora karo!");
            StopCoroutine("Shake");
            if (level2Button) StartCoroutine("Shake", level2Button.rectTransform);
            return;
        }
        Load(level2Scene, "Opening the garage...", "Garage khul raha hai...");
    }

    private void Load(string scene, string textEn, string textHi)
    {
        if (_loading) return;               // ignore double clicks
        _loading = true;
        if (_audio) _audio.PlayOneShot(startSound ? startSound : ProceduralSfx.Success, clickVolume);
        if (loadingText != null) { loadingText.english = textEn; loadingText.hinglish = textHi; loadingText.Apply(); }
        Show(loadingPanel);
        StartCoroutine(LoadGame(scene));
    }

    private IEnumerator LoadGame(string scene)
    {
        yield return new WaitForSeconds(0.4f); // let the click sound / loading text show
        var op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
        if (op == null)
        {
            Debug.LogError("[MainMenu] Scene '" + scene + "' is not in Build Settings.");
            _loading = false;
            Show(mainPanel);
        }
    }

    private IEnumerator Shake(RectTransform rt)
    {
        Vector2 home = rt.anchoredPosition;
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            rt.anchoredPosition = home + new Vector2(Mathf.Sin(t * 70f) * 14f * (1f - t / 0.35f), 0f);
            yield return null;
        }
        rt.anchoredPosition = home;
    }

    private void RefreshLevels()
    {
        bool l1 = LevelProgress.IsDone(1), l2Open = LevelProgress.IsUnlocked(2), l2 = LevelProgress.IsDone(2);
        if (level1Button) level1Button.color = selectedColor;
        if (level1Status) level1Status.text = l1 ? GameLanguage.Pick("COMPLETED", "POORA HO GAYA") : GameLanguage.Pick("NEW", "NAYA");
        if (level2Button) level2Button.color = l2Open ? selectedColor : lockedColor;
        if (level2Status) level2Status.text = !l2Open ? GameLanguage.Pick("LOCKED - finish Level 1", "LOCKED - pehle Level 1")
                                         : l2 ? GameLanguage.Pick("COMPLETED", "POORA HO GAYA") : GameLanguage.Pick("UNLOCKED!", "KHUL GAYA!");
        if (levelNote) levelNote.text = l2Open ? GameLanguage.Pick("Pick a level", "Level chuno") : GameLanguage.Pick("Finish Level 1 to open Level 2", "Level 2 kholne ke liye Level 1 poora karo");
        TintLabel(level1Button, true);
        TintLabel(level2Button, l2Open);
    }

    private void TintLabel(Image button, bool bright)
    {
        if (button == null) return;
        foreach (var t in button.GetComponentsInChildren<TMPro.TMP_Text>(true))
            t.color = bright ? new Color(0.12f, 0.08f, 0.04f) : new Color(0.55f, 0.55f, 0.6f);
    }

    private void Show(GameObject panel)
    {
        if (mainPanel) mainPanel.SetActive(panel == mainPanel);
        if (infoPanel) infoPanel.SetActive(panel == infoPanel);
        if (controlsPanel) controlsPanel.SetActive(panel == controlsPanel);
        if (loadingPanel) loadingPanel.SetActive(panel == loadingPanel);
        if (settingsPanel) settingsPanel.SetActive(panel == settingsPanel);
        if (levelPanel) levelPanel.SetActive(panel == levelPanel);
    }

    private void RefreshButtons()
    {
        bool en = GameLanguage.IsEnglish;
        Tint(englishButton, en);
        Tint(hinglishButton, !en);

        bool snap = ComfortSettings.SnapTurn;
        Tint(snapTurnButton, snap);
        Tint(smoothTurnButton, !snap);

        bool tp = ComfortSettings.Teleport;
        Tint(smoothMoveButton, !tp);
        Tint(teleportButton, tp);
    }

    private void Tint(Image button, bool selected)
    {
        if (button == null) return;
        button.color = selected ? selectedColor : normalColor;
        var label = button.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (label) label.color = selected ? new Color(0.12f, 0.08f, 0.04f) : new Color(0.94f, 0.94f, 0.96f);
    }

    private void Click()
    {
        if (_audio) _audio.PlayOneShot(clickSound ? clickSound : ProceduralSfx.Tick, clickVolume);
    }
}
