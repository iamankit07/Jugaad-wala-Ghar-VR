using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Jugaad reach-extender: bring the S-hook to the top end of the broom handle and it clips on -
/// the lower loop round the stick, the straight middle taped flat along the handle, the upper loop
/// sticking out past the end as a crook. From then on the broom carries it.
/// Put it on the broom.
/// </summary>
public class BroomHook : MonoBehaviour
{
    [SerializeField] private XRGrabInteractable broom;
    [SerializeField] private XRGrabInteractable hook;
    [Tooltip("Where the hook sits on the broom (its pose = the hook's pose when attached).")]
    [SerializeField] private Transform tipSocket;
    [Tooltip("Inside the hook's upper loop - what catches things.")]
    [SerializeField] private Transform hookCatchPoint;
    [Tooltip("Tape wrapped round handle + hook, shown once attached.")]
    [SerializeField] private GameObject tapeWraps;
    [SerializeField] private float snapRadius = 0.09f;
    [SerializeField] private float snapTime = 0.15f;
    [SerializeField] private AudioClip attachSound;

    public bool Attached { get; private set; }
    public Transform Tip => tipSocket;
    public Transform CatchPoint => hookCatchPoint;
    public XRGrabInteractable Broom => broom;

    private bool _snapping;

    private void Awake()
    {
        if (broom == null) broom = GetComponent<XRGrabInteractable>();
        if (tapeWraps != null) tapeWraps.SetActive(false);
    }

    private void Update()
    {
        if (Attached || _snapping || hook == null || tipSocket == null) return;
        if (Vector3.Distance(hook.transform.position, tipSocket.position) < snapRadius)
            StartCoroutine(Snap());
    }

    private IEnumerator Snap()
    {
        _snapping = true;
        var holders = new List<IXRSelectInteractor>(hook.interactorsSelecting);
        hook.throwOnDetach = false;                         // it goes onto the broom, not flying
        if (hook.interactionManager != null)
            for (int i = holders.Count - 1; i >= 0; i--) hook.interactionManager.SelectExit(holders[i], hook);
        hook.enabled = false;
        var rb = hook.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.detectCollisions = false;
        }
        foreach (var c in hook.GetComponentsInChildren<Collider>()) c.enabled = false;
        var reset = hook.GetComponent<ResetIfFallen>();
        if (reset != null) reset.enabled = false;

        Transform t = hook.transform;
        t.SetParent(tipSocket, true);
        Vector3 p0 = t.localPosition;
        Quaternion r0 = t.localRotation;
        for (float k = 0f; k < 1f; k += Time.deltaTime / Mathf.Max(0.01f, snapTime))
        {
            float e = Mathf.SmoothStep(0f, 1f, k);
            t.localPosition = Vector3.Lerp(p0, Vector3.zero, e);
            t.localRotation = Quaternion.Slerp(r0, Quaternion.identity, e);
            yield return null;
        }
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;

        if (tapeWraps != null) tapeWraps.SetActive(true);
        Attached = true;
        _snapping = false;
        SfxMix.PlayAt(attachSound != null ? attachSound : ProceduralSfx.Tick, tipSocket.position, 0.7f);
        foreach (var h in holders) XRHaptics.Pulse(h, 0.5f, 0.08f);
        if (broom != null) XRHaptics.PulseHolders(broom, 0.5f, 0.08f);
        Debug.Log("[BroomHook] S-hook taped onto the broom");
    }
}
