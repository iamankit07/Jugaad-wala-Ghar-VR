using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Game-end party: when GameFlowManager finishes -> confetti burst, applause, lights flicker once,
/// a haptic buzz in both hands, and finger-press buttons appear in front of the player:
/// [PLAY AGAIN] [NEXT LEVEL] [MAIN MENU] (the first two only if wired). Next level goes through a
/// fade with a line like 'The next day...' (SceneTransition).
/// </summary>
public class EndCelebration : MonoBehaviour
{
    [SerializeField] private GameFlowManager flow;
    [Tooltip("Levels built on LevelFlowManager (garage...).")]
    [SerializeField] private LevelFlowManager levelFlow;
    [SerializeField] private ParticleSystem confetti;
    [SerializeField] private GameObject menuButton;          // PokeActionButton root, starts hidden
    [SerializeField] private string menuScene = "MainMenu";
    [SerializeField] private AudioClip applauseClip;          // optional: real recording
    [SerializeField, Range(0f, 1f)] private float applauseVolume = 0.9f;
    [SerializeField] private AudioClip menuAppearSound;

    [Tooltip("Results card (score + stars). Optional.")]
    [SerializeField] private LevelResults results;

    [Header("Next level / replay (optional)")]
    [SerializeField] private GameObject nextButton;           // PokeActionButton root, starts hidden
    [SerializeField] private GameObject replayButton;
    [SerializeField] private string nextScene = "Garage";
    [SerializeField] private string nextLabelEn = "PLAY LEVEL 2", nextLabelHi = "LEVEL 2 KHELO";
    [SerializeField] private string replayLabelEn = "PLAY AGAIN", replayLabelHi = "DOBARA KHELO";
    [SerializeField] private string nextLineEn = "The next day...", nextLineHi = "Agle din...";
    [SerializeField] private float sideAngle = 25f;
    [SerializeField] private Material fadeMaterial;
    [SerializeField] private TMPro.TMP_FontAsset lineFont;
    [SerializeField] private Material lineMaterial;

    [Header("Placement")]
    [SerializeField] private float confettiDistance = 0.9f;
    [SerializeField] private float buttonDistance = 0.42f;
    [SerializeField] private float buttonHeight = -0.32f;     // below the eyes, at chest height
    [SerializeField] private float buttonDelay = 2.0f;

    [Header("Light flicker")]
    [SerializeField] private float[] flickerPattern = { 0.08f, 0.07f, 0.14f, 0.1f }; // off, on, off, on ...

    private bool _done;
    private readonly RaycastHit[] _hits = new RaycastHit[16];

    private void Awake()
    {
        if (menuButton) menuButton.SetActive(false);
        if (nextButton) nextButton.SetActive(false);
        if (replayButton) replayButton.SetActive(false);
        if (flow == null) flow = FindFirstObjectByType<GameFlowManager>();
        if (levelFlow == null) levelFlow = FindFirstObjectByType<LevelFlowManager>();
    }

    private void Start()
    {
        if (applauseClip == null) { var warm = ProceduralSfx.Applause; } // build it now, not at the big moment
    }

    private void Update()
    {
        bool finished = (flow != null && flow.EndShown) || (levelFlow != null && levelFlow.Finished);
        if (_done || !finished) return;
        _done = true;
        StartCoroutine(Celebrate());
    }

    private IEnumerator Celebrate()
    {
        var head = Camera.main != null ? Camera.main.transform : transform;
        Vector3 fwd = Flat(head.forward);
        ShowResults(head);

        if (confetti)
        {
            confetti.transform.position = head.position + fwd * confettiDistance + Vector3.down * 0.4f;
            confetti.transform.rotation = Quaternion.LookRotation(Vector3.up, -fwd); // shoot upwards
            confetti.Play(true);
        }

        var clip = applauseClip != null ? applauseClip : ProceduralSfx.Applause;
        SfxMix.PlayAt(clip, head.position + fwd * 0.5f, applauseVolume);
        XRHaptics.PulseNear(head.position, 1.5f, 0.6f, 0.25f);

        yield return Flicker();
        yield return new WaitForSeconds(buttonDelay);
        ShowButtons(head);
    }

    private void ShowResults(Transform head)
    {
        if (results == null) return;
        if (flow != null && flow.EndShown)
            results.Show(1, flow.Score, flow.TotalTime, flow.Stars, flow.Rank, flow.GoodScore, flow.MasterScore, head);
        else if (levelFlow != null && levelFlow.Finished)
            results.Show(levelFlow.LevelNumber, levelFlow.Score, levelFlow.TotalTime, levelFlow.Stars, levelFlow.Rank, levelFlow.GoodScore, levelFlow.MasterScore, head);
        else return;
        var hud = FindFirstObjectByType<TaskHUD>();
        if (hud) hud.SetPanelVisible(false);   // the card shows the same numbers, nicer
    }

    private IEnumerator Flicker()
    {
        var lights = new List<Light>();
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.enabled && l.type != LightType.Directional) lights.Add(l);
        if (lights.Count == 0) yield break;

        bool on = true;
        foreach (float step in flickerPattern)
        {
            on = !on;
            foreach (var l in lights) if (l) l.enabled = on;
            yield return new WaitForSeconds(step);
        }
        foreach (var l in lights) if (l) l.enabled = true; // always end with the lights ON
    }

    private void ShowButtons(Transform head)
    {
        if (menuButton == null) return;
        Vector3 fwd = Flat(head.forward);

        // come closer if something is in the way so the button is never inside a wall
        float d = buttonDistance;
        int n = Physics.RaycastNonAlloc(head.position, fwd, _hits, buttonDistance + 0.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var col = _hits[i].collider;
            if (col == null || col.transform.IsChildOf(head.root) || col.transform.IsChildOf(menuButton.transform)) continue;
            if (col.attachedRigidbody != null && col.bounds.size.magnitude < 0.6f) continue;
            d = Mathf.Min(d, _hits[i].distance - 0.08f);
        }
        d = Mathf.Max(0.25f, d);

        // centre: next level if there is one, else main menu; the others sit on an arc either side
        bool hasNext = nextButton != null && !string.IsNullOrEmpty(nextScene);
        if (hasNext)
        {
            Place(replayButton, head, fwd, d, -sideAngle, GameLanguage.Pick(replayLabelEn, replayLabelHi), false);
            Place(nextButton, head, fwd, d, 0f, GameLanguage.Pick(nextLabelEn, nextLabelHi), true);
            Place(menuButton, head, fwd, d, sideAngle, null, false);
        }
        else Place(menuButton, head, fwd, d, 0f, null, true);

        var centre = head.position + fwd * d + Vector3.up * buttonHeight;
        SfxMix.PlayAt(menuAppearSound ? menuAppearSound : ProceduralSfx.Success, centre, 0.7f);
    }

    private void Place(GameObject button, Transform head, Vector3 fwd, float d, float angle, string label, bool showHint)
    {
        if (button == null) return;
        Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * fwd;
        var pos = head.position + dir * d + Vector3.up * buttonHeight;
        button.transform.position = pos;
        var toPlayer = Flat(head.position - pos);
        if (toPlayer.sqrMagnitude < 1e-4f) toPlayer = -dir;
        button.transform.rotation = Quaternion.LookRotation(toPlayer, Vector3.up); // face (+Z) towards the player
        if (label != null)
        {
            var l = button.transform.Find("Label");
            var t = l ? l.GetComponent<TMPro.TMP_Text>() : null;
            if (t) t.text = label;
        }
        var hint = button.transform.Find("Hint");
        if (hint) hint.gameObject.SetActive(showHint);
        button.SetActive(true);
    }

    /// <summary>Wire the NEXT LEVEL button here.</summary>
    public void GoToNextLevel()
    {
        if (SceneTransition.Busy) return;
        HideButtons();
        if (fadeMaterial) SceneTransition.Begin(nextScene, GameLanguage.Pick(nextLineEn, nextLineHi), fadeMaterial, lineFont, lineMaterial);
        else { Time.timeScale = 1f; SceneManager.LoadScene(nextScene); }
    }

    /// <summary>Wire the PLAY AGAIN button here.</summary>
    public void Replay()
    {
        if (SceneTransition.Busy) return;
        HideButtons();
        string scene = SceneManager.GetActiveScene().name;
        if (fadeMaterial) SceneTransition.Begin(scene, null, fadeMaterial, null, null);
        else { Time.timeScale = 1f; SceneManager.LoadScene(scene); }
    }

    private void HideButtons()
    {
        if (results) results.Hide();   // never leave the score card over the 'next day' fade
        if (nextButton) nextButton.SetActive(false);
        if (replayButton) replayButton.SetActive(false);
        if (menuButton) menuButton.SetActive(false);
    }

    /// <summary>Wire the MAIN MENU button here.</summary>
    public void GoToMainMenu()
    {
        HideButtons();
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuScene);
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward;
    }
}
