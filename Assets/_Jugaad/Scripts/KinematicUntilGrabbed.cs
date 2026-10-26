using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// For props that start leaning against a wall (a broom): they stay put until the player picks
/// them up, and behave like normal physics objects after they are let go.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class KinematicUntilGrabbed : MonoBehaviour
{
    private XRGrabInteractable _grab;
    private Rigidbody _rb;

    private void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _rb = GetComponent<Rigidbody>();
        if (_rb != null) _rb.isKinematic = true;
    }

    private void OnEnable() => _grab.selectExited.AddListener(OnReleased);
    private void OnDisable() => _grab.selectExited.RemoveListener(OnReleased);

    // XRI restores the 'was kinematic' state when it lets go - undo that once, then we're done.
    private void OnReleased(SelectExitEventArgs args)
    {
        if (_rb != null) _rb.isKinematic = false;
        enabled = false;
    }
}
