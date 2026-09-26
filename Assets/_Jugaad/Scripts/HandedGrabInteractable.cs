using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// XRGrabInteractable with a separate attach point for the left and the right hand,
/// so a held object (remote, tool...) sits correctly in either palm instead of being mirrored wrong.
/// Falls back to the normal Attach Transform for anything that isn't a hand (sockets etc).
/// </summary>
public class HandedGrabInteractable : XRGrabInteractable
{
    [Header("Per-hand attach")]
    [SerializeField] private Transform leftHandAttach;
    [SerializeField] private Transform rightHandAttach;

    public override Transform GetAttachTransform(IXRInteractor interactor)
    {
        if (interactor != null && !(interactor is XRSocketInteractor))
        {
            if (interactor.handedness == InteractorHandedness.Left && leftHandAttach != null) return leftHandAttach;
            if (interactor.handedness == InteractorHandedness.Right && rightHandAttach != null) return rightHandAttach;
        }
        return base.GetAttachTransform(interactor);
    }
}
