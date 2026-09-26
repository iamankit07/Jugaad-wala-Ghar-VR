using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Game-end party: when GameFlowManager finishes -> confetti burst, applause, lights flicker once,
/// a haptic buzz in both hands, and a finger-press "MAIN MENU" button appears in front of the player.
/// </summary>
public class EndCelebration : MonoBehaviour
{
    [SerializeField] private GameFlowManager flow;
    [SerializeField] private ParticleSystem confetti;
    [SerializeField] private GameObject menuButton;          // PokeActionButton root, starts hidden
    [SerializeField] private string menuScene = "MainMenu";
    [SerializeField] private AudioClip applauseClip;          // optional: real recording
    [SerializeField, Range(0f, 1f)] private float applauseVolume = 0.9f;
    [SerializeField] private AudioClip menuAppearSound;

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
        if (flow == null) flow = FindFirstObjectByType<GameFlowManager>();
    }

    private void Start()
    {
        if (applauseClip == null) { var warm = ProceduralSfx.Applause; } // build it now, not at the big moment
    }

    private void Update()
    {
        if (_done || flow == null || !flow.Finished) return;
        _done = true;
        StartCoroutine(Celebrate());
    }

    private IEnumerator Celebrate()
    {
        var head = Camera.main != null ? Camera.main.transform : transform;
        Vector3 fwd = Flat(head.forward);

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
        ShowMenuButton(head);
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

    private void ShowMenuButton(Transform head)
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

        var pos = head.position + fwd * d + Vector3.up * buttonHeight;
        menuButton.transform.position = pos;
        var toPlayer = Flat(head.position - pos);
        if (toPlayer.sqrMagnitude < 1e-4f) toPlayer = -fwd;
        menuButton.transform.rotation = Quaternion.LookRotation(toPlayer, Vector3.up); // face (+Z) towards the player
        menuButton.SetActive(true);
        SfxMix.PlayAt(menuAppearSound ? menuAppearSound : ProceduralSfx.Success, pos, 0.7f);
    }

    /// <summary>Wire the MAIN MENU button here.</summary>
    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuScene);
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward;
    }
}
