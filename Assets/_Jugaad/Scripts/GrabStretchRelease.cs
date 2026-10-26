using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// For grabbables that are held back by a joint (door handles, drawers, cabinet doors):
/// if the hand moves too far from the grabbed point - e.g. pulling a drawer past its end stop,
/// or yanking a locked door - the grab lets go, like a real hand slipping off.
/// Without this, velocity tracking keeps pulling against the joint limit: the part shakes,
/// gets stretched past its limit and can end up stuck.
/// Put it next to the XRGrabInteractable.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class GrabStretchRelease : MonoBehaviour
{
    [Tooltip("Hand-to-grab-point distance (m) at which the grab is released.")]
    [SerializeField] private float maxStretch = 0.18f;
    [Tooltip("Ignore stretch right after grabbing while the part eases into the hand (s).")]
    [SerializeField] private float graceTime = 0.25f;
    [SerializeField] private float slipHaptic = 0.3f;

    private XRGrabInteractable _grab;
    private float _grabTime;

    private void Awake() => _grab = GetComponent<XRGrabInteractable>();
    private void OnEnable() => _grab.selectEntered.AddListener(OnGrab);
    private void OnDisable() => _grab.selectEntered.RemoveListener(OnGrab);

    private void OnGrab(SelectEnterEventArgs args) => _grabTime = Time.time;

    private void LateUpdate()
    {
        if (!_grab.isSelected || Time.time - _grabTime < graceTime) return;

        for (int i = _grab.interactorsSelecting.Count - 1; i >= 0; i--)
        {
            var interactor = _grab.interactorsSelecting[i];
            if (interactor is XRSocketInteractor) continue;

            Vector3 hand = interactor.GetAttachTransform(_grab).position;
            Vector3 held = _grab.GetAttachTransform(interactor).position;
            if ((hand - held).sqrMagnitude < maxStretch * maxStretch) continue;

            if (interactor is XRBaseInputInteractor input) input.SendHapticImpulse(slipHaptic, 0.06f);
            if (_grab.interactionManager != null)
                _grab.interactionManager.SelectExit(interactor, _grab);
        }
    }
}
