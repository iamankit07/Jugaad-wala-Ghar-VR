using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// The battery's place in the engine bay: an XRSocketInteractor that only takes the car battery,
/// only once the battery task is on (Armed) and the bonnet is open. While you hold the battery near
/// it, a ghost battery shows where it goes; let go and it clicks in. Once in, it stays in.
/// The bay is shallow, so with the bonnet coming down the battery is hidden (it would poke through).
/// Put it on the socket object.
/// </summary>
[RequireComponent(typeof(XRSocketInteractor))]
public class BatteryBay : MonoBehaviour, IXRSelectFilter, IXRHoverFilter
{
    [SerializeField] private XRGrabInteractable battery;
    [SerializeField] private HoodLift hood;
    [Tooltip("Shown with the battery once it's in (cables etc).")]
    [SerializeField] private GameObject installedExtras;
    [SerializeField] private float hideBelowHoodAngle = 28f;
    [Tooltip("Bonnet open this much (degrees) is enough to get the battery in or out.")]
    [SerializeField] private float openEnoughAngle = 10f;
    [Tooltip("Let go of the battery within this distance of its place and it goes in.")]
    [SerializeField] private float snapDistance = 0.28f;
    [SerializeField] private AudioClip clickIn;

    public bool Armed { get; set; }
    public bool Installed { get; private set; }

    private XRSocketInteractor _socket;
    private Renderer[] _batteryRenderers;
    private CarEngine _engine;

    public bool canProcess => isActiveAndEnabled;

    private void Awake()
    {
        _socket = GetComponent<XRSocketInteractor>();
        _socket.selectFilters.Add(this);
        _socket.hoverFilters.Add(this);
        if (installedExtras != null) installedExtras.SetActive(false);
        if (battery != null) _batteryRenderers = battery.GetComponentsInChildren<Renderer>();
        _engine = GetComponentInParent<CarEngine>();
        // once it's in, a hand can only take it back out with the bonnet up and the engine off
        if (battery != null)
            battery.selectFilters.Add(new XRSelectFilterDelegate((i, t) => !Installed || i == (IXRSelectInteractor)_socket || CanTakeOut()));
    }

    private bool CanTakeOut() => (hood == null || hood.Angle > openEnoughAngle) && (_engine == null || !_engine.Started);

    private void OnEnable()
    {
        _socket.selectEntered.AddListener(OnSocketed);
        _socket.selectExited.AddListener(OnUnsocketed);
        if (battery != null) battery.selectExited.AddListener(OnBatteryReleased);
    }

    private void OnDisable()
    {
        _socket.selectEntered.RemoveListener(OnSocketed);
        _socket.selectExited.RemoveListener(OnUnsocketed);
        if (battery != null) battery.selectExited.RemoveListener(OnBatteryReleased);
    }

    // Letting go of the battery flips it from kinematic to dynamic, which breaks the socket's trigger
    // contact for a frame - long enough for it to drop straight through. So when it's let go close
    // to its place, hand it to the socket ourselves.
    private void OnBatteryReleased(SelectExitEventArgs args)
    {
        if (Installed || args.interactorObject == (IXRSelectInteractor)_socket || !Accepts(battery)) return;
        Vector3 c = battery.GetComponentInChildren<Collider>().bounds.center;
        if (Vector3.Distance(c, transform.position + transform.up * 0.09f) > snapDistance) return;
        StartCoroutine(SnapNextFrame());
    }

    private System.Collections.IEnumerator SnapNextFrame()
    {
        yield return null;
        if (Installed || _socket.hasSelection || battery.isSelected) yield break;
        var rb = battery.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        _socket.interactionManager.SelectEnter((IXRSelectInteractor)_socket, (IXRSelectInteractable)battery);
    }

    // only the battery, only when it's time, only with the bonnet up
    private bool Accepts(IXRInteractable t)
    {
        if (battery == null || t == null || t.transform != battery.transform) return false;
        if (Installed) return true;
        return Armed && (hood == null || hood.Angle > openEnoughAngle);
    }

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable) => Accepts(interactable);
    public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable) => Accepts(interactable);

    private void OnSocketed(SelectEnterEventArgs args)
    {
        if (Installed) return;
        Installed = true;
        // stays put: only this socket may hold it from now on
        var reset = battery.GetComponent<ResetIfFallen>(); if (reset != null) reset.enabled = false;
        if (installedExtras != null) installedExtras.SetActive(true);
        SfxMix.PlayAt(clickIn != null ? clickIn : ProceduralSfx.Thunk, transform.position, 0.9f, 0.05f);
        XRHaptics.PulseNear(transform.position, 1.2f, 0.5f, 0.08f);
        Debug.Log("[BatteryBay] battery in");
    }

    // taken back out by hand
    private void OnUnsocketed(SelectExitEventArgs args)
    {
        if (!Installed) return;
        Installed = false;
        if (_batteryRenderers != null) foreach (var r in _batteryRenderers) if (r != null) r.enabled = true;
        if (installedExtras != null) installedExtras.SetActive(false);
        var reset = battery.GetComponent<ResetIfFallen>(); if (reset != null) reset.enabled = true;
        Debug.Log("[BatteryBay] battery out");
    }

    private void LateUpdate()
    {
        if (!Installed || hood == null || _batteryRenderers == null) return;
        bool show = hood.Angle > hideBelowHoodAngle;
        if (_batteryRenderers.Length > 0 && _batteryRenderers[0].enabled != show)
        {
            foreach (var r in _batteryRenderers) if (r != null) r.enabled = show;
            if (installedExtras != null) installedExtras.SetActive(show);
        }
    }
}
