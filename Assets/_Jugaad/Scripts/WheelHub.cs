using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// The jacked corner's hub. Any CarWheel can go on and come off, as often as you like:
/// - On the hub: grab it and pull - the first stretch it only slides straight out along the axle
///   (it's on the studs), then it comes free: heavy, lagging behind the hand. Let go before that
///   and it slides back on. Only allowed when RemoveBlocked() says nothing (nuts off, car up).
/// - Hub empty: carry a wheel close and a ghost shows where it goes; let go there and it slides on.
///   Only when MountBlocked() says nothing (car high enough).
/// Put it on the mount point (wheel root lines up with it; local -X = outwards).
/// </summary>
public class WheelHub : MonoBehaviour
{
    [SerializeField] private CarWheel startWheel;
    [Tooltip("Outward axle direction, in this object's space.")]
    [SerializeField] private Vector3 outwardLocal = Vector3.left;
    [SerializeField] private float slideOut = 0.10f;
    [SerializeField] private float snapDistance = 0.35f;
    [SerializeField] private float ghostDistance = 1.6f;
    [SerializeField] private float heavySmoothing = 5f;
    [SerializeField] private float wheelMass = 12f;
    [SerializeField] private Transform freeParent;
    [SerializeField] private Material ghostMaterial;
    [SerializeField] private Color farColor = new Color(1f, 0.85f, 0.45f, 0.22f);
    [SerializeField] private Color nearColor = new Color(0.45f, 1f, 0.55f, 0.45f);
    [SerializeField] private AudioClip mountClip;
    [SerializeField] private AudioClip freeClip;
    [SerializeField] private TaskHUD hud;

    public CarWheel Mounted { get; private set; }
    /// <summary>Why the mounted wheel can't come off right now (null = it can).</summary>
    public System.Func<string> RemoveBlocked;
    /// <summary>Why a wheel can't go on right now (null = it can).</summary>
    public System.Func<CarWheel, string> MountBlocked;
    public event System.Action<CarWheel> WheelRemoved;
    public event System.Action<CarWheel> WheelMounted;

    private readonly Dictionary<CarWheel, bool> _hooked = new Dictionary<CarWheel, bool>();
    // A wheel on the hub rides on a moving parent (car jacked up / driving). Rigidbody interpolation on
    // such a kinematic child keeps writing its OLD physics pose back every frame, so it slowly slides off
    // the hub (the nuts have no rigidbody and stay). So: no interpolation while mounted, restored after.
    private readonly Dictionary<CarWheel, RigidbodyInterpolation> _interp = new Dictionary<CarWheel, RigidbodyInterpolation>();
    private CarWheel _pulling;
    private Transform _hand;
    private Vector3 _handStart;
    private float _s, _s0;
    private bool _regrabbing, _busy;
    private GameObject _ghost;
    private MeshFilter _ghostMf;
    private MeshRenderer _ghostMr;
    private Material _ghostMat;
    private float _nextToast;

    private Vector3 Outward => transform.TransformDirection(outwardLocal).normalized;

    private void Awake()
    {
        if (hud == null) hud = FindFirstObjectByType<TaskHUD>();
        _ghost = new GameObject("WheelGhost");
        _ghost.transform.SetParent(transform, false);
        _ghostMf = _ghost.AddComponent<MeshFilter>();
        _ghostMr = _ghost.AddComponent<MeshRenderer>();
        _ghostMat = ghostMaterial != null ? new Material(ghostMaterial) : null;
        _ghostMr.sharedMaterial = _ghostMat;
        _ghostMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _ghost.SetActive(false);
    }

    private void Start()
    {
        foreach (var w in CarWheel.All) Hook(w);
        if (startWheel != null) { Hook(startWheel); Mounted = startWheel; SeatAt(startWheel, 0f); }
    }

    private void Hook(CarWheel w)
    {
        if (w == null || _hooked.ContainsKey(w)) return;
        _hooked[w] = true;
        if (w.Body != null) _interp[w] = w.Body.interpolation;
        var g = w.Grab;
        g.retainTransformParent = false;                     // we decide where it lives
        g.useDynamicAttach = true;
        g.throwOnDetach = false;
        g.smoothPosition = true; g.smoothPositionAmount = heavySmoothing;
        g.smoothRotation = true; g.smoothRotationAmount = heavySmoothing;
        if (w.Body != null) w.Body.mass = wheelMass;
        g.selectFilters.Add(new XRSelectFilterDelegate((i, t) => i is XRSocketInteractor || w != Mounted || (!_busy && RemoveBlockedNow() == null)));
        g.selectEntered.AddListener(a => OnGrab(w, a));
        g.selectExited.AddListener(a => OnRelease(w, a));
        g.hoverEntered.AddListener(a => OnHover(w, a));
    }

    private string RemoveBlockedNow() => RemoveBlocked != null ? RemoveBlocked() : null;
    private string MountBlockedNow(CarWheel w) => MountBlocked != null ? MountBlocked(w) : null;

    private void OnHover(CarWheel w, HoverEnterEventArgs args) { }

    // Only say why the wheel won't come off when the player really grips it - not every time a hand
    // passes near it (e.g. reaching for the spanner on a nut).
    private void UpdateBlockedGrabHint()
    {
        if (Mounted == null || _busy || _pulling != null) return;
        var g = Mounted.Grab;
        if (g == null || !g.isHovered || g.isSelected) return;
        foreach (var h in g.interactorsHovering)
        {
            var sel = h as IXRSelectInteractor;
            if (sel == null || h is XRSocketInteractor || sel.hasSelection || !sel.isSelectActive) continue;
            string why = RemoveBlockedNow();
            if (why != null) Toast(why, 6f);
            return;
        }
    }

    private void OnGrab(CarWheel w, SelectEnterEventArgs args)
    {
        if (_regrabbing || args.interactorObject is XRSocketInteractor) return;
        if (w == Mounted)
        {
            _pulling = w;
            _hand = args.interactorObject.transform;
            _handStart = _hand.position;
            _s0 = _s;
            w.Grab.trackPosition = false;
            w.Grab.trackRotation = false;
        }
        else
        {
            w.Grab.trackPosition = true;
            w.Grab.trackRotation = true;
        }
    }

    private void OnRelease(CarWheel w, SelectExitEventArgs args)
    {
        if (_regrabbing) return;
        if (w == _pulling)
        {
            // let go before it came off the studs: it slides back on
            _pulling = null; _hand = null;
            StartCoroutine(SlideHome(w));
            return;
        }
        if (w == Mounted) return;
        if (!_busy && Mounted == null && Vector3.Distance(w.transform.position, transform.position) < snapDistance && MountBlockedNow(w) == null)
        {
            StartCoroutine(MountRoutine(w));
            return;
        }
        if (w.Body != null) { w.Body.isKinematic = false; w.Body.useGravity = true; }   // a heavy wheel drops
        var reset = w.GetComponent<ResetIfFallen>(); if (reset != null) reset.enabled = true;
    }

    private void Update()
    {
        UpdatePull();
        UpdateGhost();
        UpdateBlockedGrabHint();
    }

    // keep the mounted wheel glued to the hub, whatever moved the car this frame
    private void LateUpdate()
    {
        var w = Mounted;
        if (w == null || _busy || _pulling == w) return;
        Transform wt = w.transform;
        if (wt.parent != transform || wt.localPosition.sqrMagnitude > 1e-8f || Quaternion.Angle(wt.localRotation, Quaternion.identity) > 0.01f)
            SeatAt(w, 0f);
    }

    private void NoInterp(CarWheel w)
    {
        if (w.Body != null && w.Body.interpolation != RigidbodyInterpolation.None) w.Body.interpolation = RigidbodyInterpolation.None;
    }

    private void RestoreInterp(CarWheel w)
    {
        RigidbodyInterpolation i;
        if (w.Body != null && _interp.TryGetValue(w, out i)) w.Body.interpolation = i;
    }

    private void UpdatePull()
    {
        if (_pulling == null || _hand == null) return;
        float s = Mathf.Clamp(_s0 + Vector3.Dot(_hand.position - _handStart, Outward), 0f, slideOut);
        if (Mathf.Abs(s - _s) > 0.003f) XRHaptics.PulseHolders(_pulling.Grab, 0.15f, 0.02f);
        _s = s;
        SeatAt(_pulling, _s);
        if (_s >= slideOut - 0.0005f) ComeOff(_pulling);
    }

    private void SeatAt(CarWheel w, float s)
    {
        if (w.transform.parent != transform) w.transform.SetParent(transform, true);
        w.transform.SetPositionAndRotation(transform.position + Outward * s, transform.rotation);
        if (w.Body != null)
        {
            NoInterp(w);
            w.Body.isKinematic = true;
            w.Body.position = w.transform.position; w.Body.rotation = w.transform.rotation;
        }
    }

    private void ComeOff(CarWheel w)
    {
        _pulling = null; _hand = null; _s = 0f;
        Mounted = null;
        w.transform.SetParent(freeParent, true);
        RestoreInterp(w);
        w.Grab.trackPosition = true;
        w.Grab.trackRotation = true;
        IXRSelectInteractor hand = null;
        foreach (var i in w.Grab.interactorsSelecting) if (!(i is XRSocketInteractor)) { hand = i; break; }
        if (hand != null && w.Grab.interactionManager != null)
        {
            _regrabbing = true;                                  // re-grab where it is now, so it doesn't jump
            w.Grab.interactionManager.SelectExit(hand, (IXRSelectInteractable)w.Grab);
            w.Grab.interactionManager.SelectEnter(hand, (IXRSelectInteractable)w.Grab);
            _regrabbing = false;
        }
        else if (w.Body != null) { w.Body.isKinematic = false; w.Body.useGravity = true; }
        SfxMix.PlayAt(freeClip != null ? freeClip : ProceduralSfx.Thunk, w.transform.position, 0.8f, 0.05f);
        XRHaptics.PulseHolders(w.Grab, 0.7f, 0.12f);
        Debug.Log("[WheelHub] " + w.name + " off the hub");
        WheelRemoved?.Invoke(w);
    }

    private IEnumerator SlideHome(CarWheel w)
    {
        _busy = true;
        yield return null;                                       // XRI finishes the drop first
        float from = _s;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.2f)
        {
            _s = Mathf.Lerp(from, 0f, t);
            SeatAt(w, _s);
            yield return null;
        }
        _s = 0f;
        SeatAt(w, 0f);
        w.Grab.trackPosition = true; w.Grab.trackRotation = true;
        _busy = false;
    }

    private IEnumerator MountRoutine(CarWheel w)
    {
        _busy = true;
        Mounted = w;
        _ghost.SetActive(false);
        yield return null;
        foreach (var k in w.GetComponents<KinematicUntilGrabbed>()) k.enabled = false;
        var reset = w.GetComponent<ResetIfFallen>(); if (reset != null) reset.enabled = false;
        if (w.Body != null)
        {
            if (!w.Body.isKinematic) { w.Body.linearVelocity = Vector3.zero; w.Body.angularVelocity = Vector3.zero; }
            w.Body.isKinematic = true;
            NoInterp(w);
        }
        w.transform.SetParent(transform, true);
        Vector3 p0 = w.transform.position; Quaternion r0 = w.transform.rotation;
        Vector3 approach = Outward * 0.12f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.22f)
        {
            float e = 1f - (1f - t) * (1f - t);
            w.transform.SetPositionAndRotation(Vector3.Lerp(p0, transform.position + approach, e), Quaternion.Slerp(r0, transform.rotation, e));
            yield return null;
        }
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
        {
            w.transform.SetPositionAndRotation(transform.position + approach * (1f - t * t), transform.rotation);
            yield return null;
        }
        _s = 0f;
        SeatAt(w, 0f);
        SfxMix.PlayAt(mountClip != null ? mountClip : ProceduralSfx.Thunk, transform.position, 0.9f, 0.05f);
        XRHaptics.PulseNear(transform.position, 1.5f, 0.5f, 0.1f);
        Debug.Log("[WheelHub] " + w.name + " on the hub");
        _busy = false;
        WheelMounted?.Invoke(w);
    }

    private void UpdateGhost()
    {
        CarWheel held = null;
        if (Mounted == null && !_busy)
            foreach (var w in CarWheel.All)
                if (w != null && w.Grab.isSelected && Vector3.Distance(w.transform.position, transform.position) < ghostDistance) { held = w; break; }
        string why = held != null ? MountBlockedNow(held) : null;
        bool show = held != null && why == null;
        if (held != null && why != null && Vector3.Distance(held.transform.position, transform.position) < snapDistance) Toast(why, 5f);
        if (_ghost.activeSelf != show) _ghost.SetActive(show);
        if (!show) return;
        var mf = held.GetComponent<MeshFilter>();
        if (mf != null && _ghostMf.sharedMesh != mf.sharedMesh) _ghostMf.sharedMesh = mf.sharedMesh;
        bool near = Vector3.Distance(held.transform.position, transform.position) < snapDistance;
        if (_ghostMat != null)
        {
            Color c = near ? nearColor : farColor;
            c.a *= 0.75f + 0.25f * Mathf.Sin(Time.time * 6f);
            _ghostMat.SetColor("_BaseColor", c);
            if (_ghostMat.HasProperty("_EmissionColor")) _ghostMat.SetColor("_EmissionColor", new Color(c.r, c.g, c.b) * (near ? 0.6f : 0.25f));
        }
    }

    private void Toast(string msg, float cooldown)
    {
        if (hud == null || string.IsNullOrEmpty(msg) || Time.time < _nextToast) return;
        _nextToast = Time.time + cooldown;
        hud.ShowToast(msg, hud.WarnColor, false);
    }

    // ---- tests ----
    public void PullOffFromCode()
    {
        if (Mounted == null || RemoveBlockedNow() != null) return;
        var w = Mounted;
        ComeOff(w);
        if (!w.Grab.isSelected && w.Body != null) { w.Body.isKinematic = false; w.Body.useGravity = true; }
    }

    public void MountFromCode(CarWheel w)
    {
        if (w == null || Mounted != null || _busy || MountBlockedNow(w) != null) return;
        _busy = true;
        if (w.Grab.isSelected && w.Grab.interactionManager != null)
            foreach (var i in new List<IXRSelectInteractor>(w.Grab.interactorsSelecting)) w.Grab.interactionManager.SelectExit(i, (IXRSelectInteractable)w.Grab);
        StartCoroutine(MountRoutine(w));
    }
}
