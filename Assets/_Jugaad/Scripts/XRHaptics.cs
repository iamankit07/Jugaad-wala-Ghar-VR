using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>Small helpers to send controller vibration from anywhere.</summary>
public static class XRHaptics
{
    /// <summary>Vibrate the controller behind this interactor (hands only; sockets/pokes are ignored).</summary>
    public static void Pulse(IXRInteractor interactor, float amplitude, float duration)
    {
        if (interactor is XRBaseInputInteractor input && input.isActiveAndEnabled)
            input.SendHapticImpulse(Mathf.Clamp01(amplitude), duration);
    }

    /// <summary>Vibrate every hand currently holding this object.</summary>
    public static void PulseHolders(IXRSelectInteractable interactable, float amplitude, float duration)
    {
        if (interactable == null) return;
        foreach (var i in interactable.interactorsSelecting) Pulse(i, amplitude, duration);
    }

    /// <summary>Vibrate every hand within radius of a point (e.g. both hands around a remote).</summary>
    public static void PulseNear(Vector3 point, float radius, float amplitude, float duration)
    {
        foreach (var input in Object.FindObjectsByType<XRBaseInputInteractor>(FindObjectsSortMode.None))
        {
            if (!input.isActiveAndEnabled) continue;
            if (Vector3.Distance(input.transform.position, point) <= radius)
                input.SendHapticImpulse(Mathf.Clamp01(amplitude), duration);
        }
    }
}
