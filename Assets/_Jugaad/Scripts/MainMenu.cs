using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Main menu: switches between the Main / Info / Controls / Settings panels and loads the game scene.
/// Settings lets the player pick English or Hinglish (saved via GameLanguage).
/// </summary>
public class MainMenu : MonoBehaviour
{
    [SerializeField] private string gameScene = "SampleScene";
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField, Range(0f, 1f)] private float clickVolume = 0.6f;
    [SerializeField] private AudioClip clickSound, startSound;

    [Header("Language buttons (Settings)")]
    [SerializeField] private Image englishButton;
    [SerializeField] private Image hinglishButton;
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
        RefreshLanguageButtons();
    }

    public void ShowMain()     { Click(); Show(mainPanel); }
    public void ShowInfo()     { Click(); Show(infoPanel); }
    public void ShowControls() { Click(); Show(controlsPanel); }
    public void ShowSettings() { Click(); Show(settingsPanel); RefreshLanguageButtons(); }

    public void SetEnglish()  { Click(); GameLanguage.Current = Language.English;  RefreshLanguageButtons(); }
    public void SetHinglish() { Click(); GameLanguage.Current = Language.Hinglish; RefreshLanguageButtons(); }

    public void StartGame()
    {
        if (_loading) return;               // ignore double clicks
        _loading = true;
        if (_audio) _audio.PlayOneShot(startSound ? startSound : ProceduralSfx.Success, clickVolume);
        Show(loadingPanel);
        StartCoroutine(LoadGame());
    }

    private IEnumerator LoadGame()
    {
        yield return new WaitForSeconds(0.4f); // let the click sound / loading text show
        var op = SceneManager.LoadSceneAsync(gameScene, LoadSceneMode.Single);
        if (op == null)
        {
            Debug.LogError("[MainMenu] Scene '" + gameScene + "' is not in Build Settings.");
            _loading = false;
            Show(mainPanel);
        }
    }

    private void Show(GameObject panel)
    {
        if (mainPanel) mainPanel.SetActive(panel == mainPanel);
        if (infoPanel) infoPanel.SetActive(panel == infoPanel);
        if (controlsPanel) controlsPanel.SetActive(panel == controlsPanel);
        if (loadingPanel) loadingPanel.SetActive(panel == loadingPanel);
        if (settingsPanel) settingsPanel.SetActive(panel == settingsPanel);
    }

    private void RefreshLanguageButtons()
    {
        bool en = GameLanguage.IsEnglish;
        Tint(englishButton, en);
        Tint(hinglishButton, !en);
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
