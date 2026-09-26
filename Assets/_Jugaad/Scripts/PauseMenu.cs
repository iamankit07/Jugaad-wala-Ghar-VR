using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// In-game settings / pause menu: RESUME, RESTART, QUIT.
/// Opened by the settings icon on the task panel, or the left controller's Menu button
/// (Esc in the editor). While open the game time is paused. Always restores Time.timeScale.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject panel;              // the pause canvas (world space)
    [SerializeField] private GameObject[] hideWhileOpen;     // e.g. the settings icon
    [SerializeField] private Transform head;                 // defaults to Camera.main
    [SerializeField] private float distance = 0.9f;
    [SerializeField] private float heightOffset = -0.1f;
    [SerializeField] private float minDistance = 0.45f;
    [SerializeField] private bool pauseTime = true;
    [SerializeField, Range(0f, 1f)] private float clickVolume = 0.6f;

    public bool IsOpen { get; private set; }

    private InputAction _menuButton;
    private AudioSource _audio;
    private readonly RaycastHit[] _hits = new RaycastHit[16];

    private void Awake()
    {
        if (panel) panel.SetActive(false);
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;
        _audio.ignoreListenerPause = true;

        _menuButton = new InputAction("PauseMenuToggle", InputActionType.Button);
        _menuButton.AddBinding("<XRController>{LeftHand}/{MenuButton}");
        _menuButton.AddBinding("<Keyboard>/escape");
        _menuButton.performed += OnMenuButton;
    }

    private void OnEnable() { _menuButton.Enable(); }
    private void OnDisable() { _menuButton.Disable(); }

    private void OnDestroy()
    {
        _menuButton.performed -= OnMenuButton;
        _menuButton.Dispose();
        if (IsOpen) Time.timeScale = 1f; // never leave the game frozen
    }

    private void OnMenuButton(InputAction.CallbackContext ctx) { Toggle(); }

    public void Toggle() { if (IsOpen) Resume(); else Open(); }

    public void Open()
    {
        if (IsOpen || panel == null) return;
        IsOpen = true;
        PlaceInFront();
        panel.SetActive(true);
        SetHidden(true);
        if (pauseTime) Time.timeScale = 0f;
        Click();
    }

    public void Resume()
    {
        if (!IsOpen) return;
        IsOpen = false;
        panel.SetActive(false);
        SetHidden(false);
        Time.timeScale = 1f;
        Click();
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        Click();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void Quit()
    {
        Time.timeScale = 1f;
        Click();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetHidden(bool hidden)
    {
        if (hideWhileOpen == null) return;
        foreach (var go in hideWhileOpen) if (go) go.SetActive(!hidden);
    }

    private void PlaceInFront()
    {
        var h = head;
        if (h == null && Camera.main != null) h = Camera.main.transform;
        if (h == null) return;

        Vector3 fwd = h.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = h.up; fwd.y = 0f;
        fwd.Normalize();

        // come closer if a wall is in the way, so the panel never ends up inside geometry
        float d = distance;
        int n = Physics.RaycastNonAlloc(h.position, fwd, _hits, distance + 0.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var col = _hits[i].collider;
            if (col == null || col.transform.IsChildOf(h.root)) continue;
            if (col.attachedRigidbody != null && col.bounds.size.magnitude < 0.6f) continue; // small props
            d = Mathf.Min(d, _hits[i].distance - 0.1f);
        }
        d = Mathf.Max(minDistance, d);

        Vector3 pos = h.position + fwd * d + Vector3.up * heightOffset;
        panel.transform.position = pos;
        panel.transform.rotation = Quaternion.LookRotation(pos - h.position, Vector3.up);
    }

    private void Click()
    {
        if (_audio) _audio.PlayOneShot(ProceduralSfx.Tick, clickVolume);
    }
}
