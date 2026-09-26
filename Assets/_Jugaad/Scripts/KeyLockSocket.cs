using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Socket that only accepts a KeyItem with the matching keyId.
/// When the key goes in: unlocks the linked LockableDoor (optional) and fires onKeyInserted.
/// When it is taken out: fires onKeyRemoved. Works on static objects (doors) and on grabbable ones (remote).
/// Haptics: a light buzz when a correct key (still in hand) reaches the socket, and a firm
/// "click" in every nearby hand when it goes in.
/// </summary>
public class KeyLockSocket : XRSocketInteractor
{
    [Header("Key Lock")]
    [SerializeField] private string requiredKeyId = "safety_pin";
    [SerializeField] private LockableDoor door;

    [Tooltip("Colliders the key should NOT physically bump into while it sits in the socket.")]
    [SerializeField] private Collider[] ignoreCollisionWith;

    [Header("Haptics")]
    [SerializeField] private float hoverHaptic = 0.2f;
    [SerializeField] private float insertHaptic = 0.7f;
    [SerializeField] private float insertHapticDuration = 0.12f;
    [SerializeField] private float insertHapticRadius = 0.45f;

    [SerializeField] private AudioClip insertSound;          // leave empty on door sockets (the door plays its unlock sound)
    [SerializeField, Range(0f, 1f)] private float insertVolume = 0.6f;

    [Header("Events")]
    public UnityEvent onKeyInserted;
    public UnityEvent onKeyRemoved;

    public bool HasKey => hasSelection;

    public override bool CanHover(IXRHoverInteractable interactable)
    {
        return base.CanHover(interactable) && IsCorrectKey(interactable);
    }

    public override bool CanSelect(IXRSelectInteractable interactable)
    {
        return base.CanSelect(interactable) && IsCorrectKey(interactable);
    }

    private bool IsCorrectKey(IXRInteractable interactable)
    {
        if (interactable == null) return false;
        var key = interactable.transform.GetComponentInParent<KeyItem>();
        return key != null && key.keyId == requiredKeyId;
    }

    protected override void OnHoverEntered(HoverEnterEventArgs args)
    {
        base.OnHoverEntered(args);
        // "it fits here" - buzz the hand that is still holding the key
        if (args.interactableObject is IXRSelectInteractable held)
            XRHaptics.PulseHolders(held, hoverHaptic, 0.04f);
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        SetIgnoreCollisions(args.interactableObject.transform, true);
        if (door != null) door.Unlock();
        var at = attachTransform != null ? attachTransform.position : transform.position;
        XRHaptics.PulseNear(at, insertHapticRadius, insertHaptic, insertHapticDuration);
        if (insertSound && Time.timeSinceLevelLoad > 0.5f) SfxMix.PlayAt(insertSound, at, insertVolume);
        onKeyInserted?.Invoke();
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);
        SetIgnoreCollisions(args.interactableObject.transform, false);
        onKeyRemoved?.Invoke();
    }

    private void SetIgnoreCollisions(Transform key, bool ignore)
    {
        if (ignoreCollisionWith == null) return;
        foreach (var keyCol in key.GetComponentsInChildren<Collider>())
            foreach (var other in ignoreCollisionWith)
                if (other != null && keyCol != null)
                    Physics.IgnoreCollision(keyCol, other, ignore);
    }
}
