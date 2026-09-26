using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// A wall switch you press with your finger (XR Poke) - each press flips it ON/OFF.
/// Needs an XRSimpleInteractable (+ XRPokeFilter) on the same GameObject.
/// Also works with a grab/select press, which is handy for the XR Device Simulator.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class PokeToggleButton : MonoBehaviour
{
    [SerializeField] private bool startOn = false;

    [Header("Visuals")]
    [Tooltip("The part that physically moves in when pressed.")]
    [SerializeField] private Transform cap;
    [SerializeField] private float pressDepth = 0.004f;
    [Tooltip("Small LED that shows the state.")]
    [SerializeField] private Renderer indicator;
    [SerializeField] private Material indicatorOn;
    [SerializeField] private Material indicatorOff;

    [Header("Feedback")]
    [SerializeField] private AudioClip clickSound;
    [SerializeField, Range(0f, 1f)] private float clickVolume = 0.6f;
    [Tooltip("Ignore extra presses within this time (finger jitter).")]
    [SerializeField] private float cooldown = 0.35f;

    [Header("Output")]
    public UnityEvent<bool> onToggled;

    public bool IsOn { get; private set; }

    private XRSimpleInteractable _interactable;
    private Vector3 _capRestPos;
    private float _lastPressTime = -10f;

    private void Awake()
    {
        _interactable = GetComponent<XRSimpleInteractable>();
        if (cap != null) _capRestPos = cap.localPosition;
        IsOn = startOn;
        RefreshIndicator();
    }

    private void Start()
    {
        // push the initial state out once, so whatever is wired starts in sync
        onToggled?.Invoke(IsOn);
    }

    private void OnEnable()
    {
        _interactable.selectEntered.AddListener(OnPressed);
        _interactable.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        _interactable.selectEntered.RemoveListener(OnPressed);
        _interactable.selectExited.RemoveListener(OnReleased);
    }

    private void OnPressed(SelectEnterEventArgs args)
    {
        if (cap != null) cap.localPosition = _capRestPos - Vector3.forward * pressDepth;

        if (Time.time - _lastPressTime < cooldown) return;
        _lastPressTime = Time.time;

        IsOn = !IsOn;
        RefreshIndicator();
        if (clickSound) SfxMix.PlayAt(clickSound, transform.position, clickVolume);
        onToggled?.Invoke(IsOn);
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (cap != null) cap.localPosition = _capRestPos;
    }

    /// <summary>Sync the switch to an outside change (e.g. a remote) without firing onToggled.</summary>
    public void SetState(bool on)
    {
        IsOn = on;
        RefreshIndicator();
    }

    private void RefreshIndicator()
    {
        if (indicator == null) return;
        var m = IsOn ? indicatorOn : indicatorOff;
        if (m != null) indicator.sharedMaterial = m;
    }
}
