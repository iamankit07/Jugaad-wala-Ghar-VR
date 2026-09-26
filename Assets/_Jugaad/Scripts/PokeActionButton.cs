using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// One-shot 3D button you press with your finger (XR Poke). Fires onPressed (no toggle state,
/// nothing fires on Start). Needs XRSimpleInteractable + XRPokeFilter on the same object.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class PokeActionButton : MonoBehaviour
{
    [SerializeField] private Transform cap;
    [SerializeField] private float pressDepth = 0.008f;
    [SerializeField] private float cooldown = 0.6f;
    [SerializeField] private AudioClip clickSound;
    [SerializeField] private float haptic = 0.5f;

    public UnityEvent onPressed;

    private XRSimpleInteractable _interactable;
    private Vector3 _capRest;
    private float _last = -10f;

    private void Awake()
    {
        _interactable = GetComponent<XRSimpleInteractable>();
        if (cap) _capRest = cap.localPosition;
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
        if (cap) cap.localPosition = _capRest - Vector3.forward * pressDepth;
        if (Time.unscaledTime - _last < cooldown) return;
        _last = Time.unscaledTime;

        var clip = clickSound != null ? clickSound : ProceduralSfx.Tick;
        SfxMix.PlayAt(clip, transform.position, 0.7f);
        if (args != null) XRHaptics.Pulse(args.interactorObject, haptic, 0.06f);
        onPressed?.Invoke();
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (cap) cap.localPosition = _capRest;
    }

    /// <summary>For testing / other scripts.</summary>
    public void Press() { OnPressed(null); }
}
