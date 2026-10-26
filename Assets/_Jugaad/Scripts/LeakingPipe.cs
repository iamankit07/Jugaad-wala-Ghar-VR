using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// A pipe joint dripping water - drop by drop, "tap... tap..." - onto whatever is below it
/// (the car battery). Jugaad fix in two steps:
///  1) let go of the polythene at the joint -> it wraps round it, the drip slows down;
///  2) bring the rope to the joint (a ghost shows where it goes) and let go -> it winds round the
///     polythene, the knot pulls tight and the drip stops.
/// Moving the battery out from under the drip (before it is sealed) earns a bonus.
/// Put it on the leaking joint.
/// </summary>
public class LeakingPipe : MonoBehaviour
{
    public enum State { Leaking, Wrapped, Sealed }

    [Header("Joint")]
    [Tooltip("Where drops form (just under the joint).")]
    [SerializeField] private Transform dripPoint;
    [SerializeField] private float wrapRadius = 0.18f;
    [SerializeField] private float tieRadius = 0.14f;

    [Header("Fix visuals (children, hidden at start)")]
    [SerializeField] private GameObject sleeve;
    [SerializeField] private Mesh sleevePinched;
    [SerializeField] private GameObject sleeveGhost;
    [SerializeField] private GameObject tieBand;
    [SerializeField] private GameObject tieGhost;

    [Header("Items")]
    [SerializeField] private XRGrabInteractable polythene;
    [SerializeField] private XRGrabInteractable rope;

    [Header("Drip")]
    [SerializeField] private Material waterMaterial;
    [SerializeField] private float leakInterval = 0.6f;
    [SerializeField] private float wrappedInterval = 2.2f;
    [SerializeField] private float formTime = 0.3f;
    [SerializeField] private float puddleMax = 0.17f;
    [Tooltip("Thinner water for the wet patch it leaves. Empty = waterMaterial.")]
    [SerializeField] private Material puddleMaterial;

    [Header("Battery bonus")]
    [SerializeField] private Transform battery;
    [SerializeField] private float batteryClearRadius = 0.22f;
    [SerializeField] private int bonusPoints = 250;

    [Header("Refs")]
    [SerializeField] private TaskHUD hud;
    [SerializeField] private LevelFlowManager flow;
    [SerializeField] private AudioClip wrapSound;
    [SerializeField] private AudioClip tieSound;

    public State Current => _state;
    public bool Wrapped => _state != State.Leaking;
    public bool Sealed => _state == State.Sealed;

    private State _state = State.Leaking;
    private bool _armed, _bonusGiven, _polyNear, _busy;
    private bool _polyThrowWas = true, _ropeThrowWas = true, _ropeNear;
    private float _nextDrop, _nextToast, _ropeNearSince = -1f;

    private class Drop { public Transform t; public Vector3 v; public float life; public bool forming, splash; public float hitY; public Vector3 baseScale; }
    private readonly List<Drop> _drops = new List<Drop>();
    private readonly Dictionary<Transform, Transform> _puddles = new Dictionary<Transform, Transform>();
    private Vector3 _polyScale, _ropeScale;
    private Mesh _sleeveMesh;
    private Collider _sleeveCol;
    private static AudioClip[] _plinks;
    private static AudioClip _crinkle;

    private void Awake()
    {
        if (hud == null) hud = FindFirstObjectByType<TaskHUD>();
        if (flow == null) flow = FindFirstObjectByType<LevelFlowManager>();
        foreach (var g in new[] { sleeve, sleeveGhost, tieBand, tieGhost }) if (g != null) g.SetActive(false);
        BuildPool();
        _nextDrop = Time.time + 1f;
        if (polythene != null) _polyScale = polythene.transform.localScale;
        if (rope != null) _ropeScale = rope.transform.localScale;
        if (sleeve != null) { var mf = sleeve.GetComponent<MeshFilter>(); if (mf != null) _sleeveMesh = mf.sharedMesh; }
        // the fix can be undone: grab the knot to untie the rope, grab the sleeve to peel the polythene off
        _sleeveCol = MakeGrabbable(sleeve, OnSleeveGrabbed);
        MakeGrabbable(tieBand, OnTieGrabbed);
    }

    private Collider MakeGrabbable(GameObject go, UnityEngine.Events.UnityAction<SelectEnterEventArgs> onGrab)
    {
        if (go == null) return null;
        var col = go.GetComponent<Collider>();
        if (col == null)
        {
            var box = go.AddComponent<BoxCollider>();
            box.size *= 1.4f;
            col = box;
        }
        col.isTrigger = true;                               // drops fall through it, hands can still grab it
        var si = go.GetComponent<XRSimpleInteractable>();
        if (si == null) si = go.AddComponent<XRSimpleInteractable>();
        si.selectEntered.AddListener(onGrab);
        return col;
    }

    private void OnTieGrabbed(SelectEnterEventArgs args)
    {
        if (_state != State.Sealed || _busy || rope == null) { StartCoroutine(LetGo(args)); return; }
        _busy = true;
        Vector3 at = tieBand.transform.position;
        tieBand.SetActive(false);
        if (sleeve != null && _sleeveMesh != null) { var mf = sleeve.GetComponent<MeshFilter>(); if (mf != null) mf.sharedMesh = _sleeveMesh; }
        if (_sleeveCol != null) _sleeveCol.enabled = true;
        Revive(rope, at, _ropeScale, _ropeThrowWas);
        _state = State.Wrapped;
        _nextDrop = Time.time + wrappedInterval;
        SfxMix.PlayAt(tieSound != null ? tieSound : Crinkle, at, 0.6f, 0.1f);
        Debug.Log("[LeakingPipe] rope untied");
        StartCoroutine(HandOver(args, rope));
    }

    private void OnSleeveGrabbed(SelectEnterEventArgs args)
    {
        if (_state == State.Sealed)
        {
            Toast(GameLanguage.Pick("Untie the rope first!", "Pehle rassi kholo!"), true);
            StartCoroutine(LetGo(args));
            return;
        }
        if (_state != State.Wrapped || _busy || polythene == null) { StartCoroutine(LetGo(args)); return; }
        _busy = true;
        Vector3 at = sleeve.transform.position;
        sleeve.SetActive(false);
        Revive(polythene, at, _polyScale, _polyThrowWas);
        _state = State.Leaking;
        _nextDrop = Time.time + 0.4f;
        SfxMix.PlayAt(wrapSound != null ? wrapSound : Crinkle, at, 0.7f, 0.05f);
        Debug.Log("[LeakingPipe] polythene peeled off");
        StartCoroutine(HandOver(args, polythene));
    }

    private static void Revive(XRGrabInteractable item, Vector3 at, Vector3 scale, bool throwWas)
    {
        item.transform.position = at;
        item.transform.localScale = scale;
        item.gameObject.SetActive(true);
        item.enabled = true;
        item.throwOnDetach = throwWas;
        var rb = item.GetComponent<Rigidbody>();
        if (rb != null) { rb.detectCollisions = true; rb.isKinematic = false; }
        foreach (var c in item.GetComponentsInChildren<Collider>(true)) c.enabled = true;
        var reset = item.GetComponent<ResetIfFallen>(); if (reset != null) reset.enabled = true;
    }

    // the hand that grabbed the knot / sleeve ends up holding the rope / polythene
    private IEnumerator HandOver(SelectEnterEventArgs args, XRGrabInteractable item)
    {
        var hand = args.interactorObject;
        var from = args.interactableObject;
        var mgr = args.manager;
        yield return null;
        if (mgr != null && hand != null)
        {
            if (hand.IsSelecting(from)) mgr.SelectExit(hand, from);
            if (!item.isSelected) mgr.SelectEnter(hand, (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)item);
        }
        _busy = false;
    }

    private IEnumerator LetGo(SelectEnterEventArgs args)
    {
        yield return null;
        if (args.manager != null && args.interactorObject != null && args.interactorObject.IsSelecting(args.interactableObject))
            args.manager.SelectExit(args.interactorObject, args.interactableObject);
    }

    private void OnEnable()
    {
        if (polythene != null) polythene.selectExited.AddListener(OnPolytheneReleased);
        if (rope != null) rope.selectExited.AddListener(OnRopeReleased);
    }

    private void OnDisable()
    {
        if (polythene != null) polythene.selectExited.RemoveListener(OnPolytheneReleased);
        if (rope != null) rope.selectExited.RemoveListener(OnRopeReleased);
    }

    /// <summary>Called when the task starts (bonus only counts from then).</summary>
    public void Arm() { _armed = true; }

    private void Update()
    {
        UpdatePolythene();
        UpdateRope();
        UpdateBonus();
        UpdateDrops();
    }

    // ---------------- step 1: polythene ----------------
    private void UpdatePolythene()
    {
        bool near = _state == State.Leaking && !_busy && polythene != null && polythene.isSelected
                    && Vector3.Distance(polythene.transform.position, dripPoint.position) < wrapRadius;
        if (near != _polyNear)
        {
            _polyNear = near;
            if (sleeveGhost != null) sleeveGhost.SetActive(near);
            if (near)
            {
                _polyThrowWas = polythene.throwOnDetach;
                polythene.throwOnDetach = false;          // let go here = wrap, not throw
                XRHaptics.PulseHolders(polythene, 0.25f, 0.04f);
            }
            else if (polythene != null && _state == State.Leaking) polythene.throwOnDetach = _polyThrowWas;
        }
    }

    private void OnPolytheneReleased(SelectExitEventArgs args)
    {
        if (_polyNear && _state == State.Leaking && !_busy) StartCoroutine(Wrap());
    }

    private IEnumerator Wrap()
    {
        _busy = true;
        _polyNear = false;
        if (sleeveGhost != null) sleeveGhost.SetActive(false);
        yield return null;                                 // let XRI finish its release first
        var rb = polythene.GetComponent<Rigidbody>();
        polythene.enabled = false;
        if (rb != null)
        {
            if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
        foreach (var c in polythene.GetComponentsInChildren<Collider>()) c.enabled = false;
        var reset = polythene.GetComponent<ResetIfFallen>(); if (reset != null) reset.enabled = false;

        SfxMix.PlayAt(wrapSound != null ? wrapSound : Crinkle, dripPoint.position, 0.8f, 0.05f);
        Transform pt = polythene.transform;
        Vector3 p0 = pt.position, s0 = pt.localScale;
        _polyScale = s0;
        Vector3 target = sleeve != null ? sleeve.transform.position : dripPoint.position;
        for (float k = 0f; k < 1f; k += Time.deltaTime / 0.18f)
        {
            pt.position = Vector3.Lerp(p0, target, k);
            pt.localScale = s0 * Mathf.Lerp(1f, 0.3f, k);
            yield return null;
        }
        polythene.gameObject.SetActive(false);

        if (sleeve != null)
        {
            sleeve.SetActive(true);
            Transform st = sleeve.transform;
            Vector3 full = st.localScale;
            Quaternion r1 = st.localRotation;
            for (float k = 0f; k < 1f; k += Time.deltaTime / 0.35f)
            {
                float e = 1f - (1f - k) * (1f - k);
                st.localScale = new Vector3(full.x * Mathf.Lerp(1.6f, 1f, e), full.y * Mathf.Lerp(0.4f, 1f, e), full.z * Mathf.Lerp(1.6f, 1f, e));
                st.localRotation = r1 * Quaternion.Euler(0f, Mathf.Lerp(-140f, 0f, e), 0f);   // wrap round the pipe
                yield return null;
            }
            st.localScale = full;
            st.localRotation = r1;
        }
        _state = State.Wrapped;
        _nextDrop = Time.time + wrappedInterval;
        XRHaptics.PulseNear(dripPoint.position, 1.2f, 0.3f, 0.06f);
        Toast(GameLanguage.Pick("Dripping less... but not stopped. Tie it with something!", "Tapakna kam hua... par band nahi. Ise kis cheez se baandho!"), false);
        Debug.Log("[LeakingPipe] wrapped in polythene");
        _busy = false;
    }

    // ---------------- step 2: rope ----------------
    private void UpdateRope()
    {
        if (rope == null || _state == State.Sealed || _busy || !rope.isActiveAndEnabled) { SetTieGhost(false); _ropeNear = false; return; }
        bool near = rope.isSelected && Vector3.Distance(rope.transform.position, dripPoint.position) < tieRadius;
        if (near != _ropeNear)
        {
            _ropeNear = near;
            if (near)
            {
                _ropeThrowWas = rope.throwOnDetach;
                rope.throwOnDetach = false;               // let go here = tie, not throw
                if (_state == State.Wrapped) XRHaptics.PulseHolders(rope, 0.25f, 0.04f);
            }
            else rope.throwOnDetach = _ropeThrowWas;
        }
        SetTieGhost(near && _state == State.Wrapped);

        if (!near) { _ropeNearSince = -1f; return; }
        if (_ropeNearSince < 0f) _ropeNearSince = Time.time;
        if (_state == State.Leaking && Time.time - _ropeNearSince > 0.8f)
            Toast(GameLanguage.Pick("Rope alone won't stop water - wrap the polythene first!", "Akeli rassi paani nahi rokegi - pehle polythene lapeto!"), true);
    }

    private void SetTieGhost(bool on)
    {
        if (tieGhost != null && tieGhost.activeSelf != on) tieGhost.SetActive(on);
    }

    private void OnRopeReleased(SelectExitEventArgs args)
    {
        if (!_ropeNear || _busy || _state == State.Sealed) return;
        if (_state == State.Leaking)
        {
            Toast(GameLanguage.Pick("Rope alone won't stop water - wrap the polythene first!", "Akeli rassi paani nahi rokegi - pehle polythene lapeto!"), true);
            return;
        }
        StartCoroutine(Tie());
    }

    private IEnumerator Tie()
    {
        _busy = true;
        _ropeNear = false;
        SetTieGhost(false);
        yield return null;                                 // let XRI finish its release first
        var rb = rope.GetComponent<Rigidbody>();
        rope.enabled = false;
        if (rb != null)
        {
            if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
        foreach (var c in rope.GetComponentsInChildren<Collider>()) c.enabled = false;
        var reset = rope.GetComponent<ResetIfFallen>(); if (reset != null) reset.enabled = false;

        // the coil flies onto the joint like into a socket...
        SfxMix.PlayAt(tieSound != null ? tieSound : Crinkle, dripPoint.position, 0.6f, 0.1f);
        Transform rt = rope.transform;
        Vector3 p0 = rt.position, s0 = rt.localScale;
        _ropeScale = s0;
        Vector3 target = tieBand != null ? tieBand.transform.position : dripPoint.position;
        for (float k = 0f; k < 1f; k += Time.deltaTime / 0.15f)
        {
            rt.position = Vector3.Lerp(p0, target, k);
            rt.localScale = s0 * Mathf.Lerp(1f, 0.4f, k);
            yield return null;
        }
        rope.gameObject.SetActive(false);

        // ...and winds round it, pulling tight
        if (tieBand != null)
        {
            tieBand.SetActive(true);
            Transform tt = tieBand.transform;
            Vector3 full = tt.localScale; Quaternion r1 = tt.localRotation;
            for (float k = 0f; k < 1f; k += Time.deltaTime / 0.4f)
            {
                float e = 1f - (1f - k) * (1f - k);
                float squeeze = Mathf.Lerp(1.5f, 1f, e);
                tt.localScale = new Vector3(full.x * squeeze, full.y * Mathf.Lerp(0.3f, 1f, e), full.z * squeeze);
                tt.localRotation = r1 * Quaternion.Euler(0f, Mathf.Lerp(-300f, 0f, e), 0f);
                yield return null;
            }
            tt.localScale = full; tt.localRotation = r1;
        }
        if (sleeve != null && sleevePinched != null)
        {
            var mf = sleeve.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = sleevePinched;
        }
        if (_sleeveCol != null) _sleeveCol.enabled = false;
        SfxMix.PlayAt(ProceduralSfx.Tick, dripPoint.position, 0.8f, 0.05f);   // the knot pulled tight
        XRHaptics.PulseNear(dripPoint.position, 1.2f, 0.6f, 0.1f);
        _state = State.Sealed;
        Debug.Log("[LeakingPipe] tied off with the rope");
        _busy = false;
    }

    // ---------------- battery bonus ----------------
    private void UpdateBonus()
    {
        if (!_armed || _bonusGiven || battery == null || _state == State.Sealed) return;
        Vector3 d = battery.position - dripPoint.position; d.y = 0f;
        if (BatteryCentre().y > dripPoint.position.y) return;          // still in a hand above the pipe - wait
        Vector3 c = BatteryCentre() - dripPoint.position; c.y = 0f;
        if (c.magnitude < batteryClearRadius) return;
        _bonusGiven = true;
        if (flow != null) flow.AddBonus(bonusPoints);
        Toast(GameLanguage.Pick("Battery saved! +" + bonusPoints, "Battery bacha li! +" + bonusPoints), false);
    }

    private Vector3 BatteryCentre()
    {
        var col = battery.GetComponent<Collider>();
        return col != null ? col.bounds.center : battery.position;
    }

    // ---------------- drops ----------------
    private void BuildPool()
    {
        for (int i = 0; i < 10; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Drop";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            var mr = go.GetComponent<MeshRenderer>();
            if (waterMaterial != null) mr.sharedMaterial = waterMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(false);
            _drops.Add(new Drop { t = go.transform });
        }
    }

    private Drop FreeDrop()
    {
        foreach (var d in _drops) if (!d.t.gameObject.activeSelf) return d;
        return null;
    }

    private void UpdateDrops()
    {
        if (_state != State.Sealed && Time.time >= _nextDrop && dripPoint != null)
        {
            _nextDrop = Time.time + (_state == State.Leaking ? leakInterval : wrappedInterval) * Random.Range(0.85f, 1.15f);
            var d = FreeDrop();
            if (d != null)
            {
                d.forming = true; d.splash = false; d.life = 0f; d.v = Vector3.zero;
                d.baseScale = _state == State.Leaking ? new Vector3(0.008f, 0.011f, 0.008f) : new Vector3(0.006f, 0.008f, 0.006f);
                d.t.position = dripPoint.position;
                d.t.localScale = Vector3.zero;
                d.t.gameObject.SetActive(true);
            }
        }

        float dt = Time.deltaTime;
        for (int i = 0; i < _drops.Count; i++)
        {
            var d = _drops[i];
            if (!d.t.gameObject.activeSelf) continue;
            d.life += dt;
            if (d.forming)
            {
                float k = d.life / formTime;
                d.t.localScale = d.baseScale * Mathf.Clamp01(k);
                d.t.position = dripPoint.position + Vector3.down * 0.004f * Mathf.Clamp01(k);
                if (k >= 1f)
                {
                    d.forming = false;
                    RaycastHit hit;
                    if (Physics.Raycast(d.t.position, Vector3.down, out hit, 5f, ~0, QueryTriggerInteraction.Ignore))
                    { d.hitY = hit.point.y; }
                    else d.hitY = 0f;
                }
                continue;
            }
            d.v += Physics.gravity * dt;
            d.t.position += d.v * dt;
            if (d.splash)
            {
                d.t.localScale = d.baseScale * Mathf.Clamp01(1f - d.life / 0.25f);
                if (d.life > 0.25f || d.t.position.y < d.hitY - 0.01f) d.t.gameObject.SetActive(false);
                continue;
            }
            float stretch = Mathf.Clamp(1f + d.v.magnitude * 0.25f, 1f, 1.8f);
            d.t.localScale = new Vector3(d.baseScale.x, d.baseScale.y * stretch, d.baseScale.z);
            if (d.t.position.y <= d.hitY) Land(d);
        }
    }

    private void Land(Drop d)
    {
        Vector3 p = new Vector3(d.t.position.x, d.hitY, d.t.position.z);
        d.t.gameObject.SetActive(false);
        if (_plinks == null) MakePlinks();
        SfxMix.PlayAt(_plinks[Random.Range(0, _plinks.Length)], p, _state == State.Leaking ? 0.55f : 0.35f, 0.08f);

        for (int s = 0; s < 3; s++)
        {
            var sp = FreeDrop();
            if (sp == null) break;
            sp.forming = false; sp.splash = true; sp.life = 0f; sp.hitY = d.hitY;
            sp.baseScale = Vector3.one * 0.0035f;
            float a = Random.Range(0f, Mathf.PI * 2f);
            sp.v = new Vector3(Mathf.Cos(a) * 0.35f, Random.Range(0.45f, 0.75f), Mathf.Sin(a) * 0.35f);
            sp.t.position = p + Vector3.up * 0.003f;
            sp.t.localScale = sp.baseScale;
            sp.t.gameObject.SetActive(true);
        }

        RaycastHit hit;
        if (Physics.Raycast(p + Vector3.up * 0.05f, Vector3.down, out hit, 0.2f, ~0, QueryTriggerInteraction.Ignore))
            GrowPuddle(hit);
    }

    private void GrowPuddle(RaycastHit hit)
    {
        Material pm = puddleMaterial != null ? puddleMaterial : waterMaterial;
        if (pm == null) return;
        // on a prop (the battery) the wet patch must stay on its top, not overhang it
        float cap = hit.collider.attachedRigidbody != null
            ? Mathf.Min(puddleMax, 0.6f * Mathf.Min(hit.collider.bounds.size.x, hit.collider.bounds.size.z))
            : puddleMax;
        Transform host = hit.collider.attachedRigidbody != null ? hit.collider.attachedRigidbody.transform : hit.collider.transform;
        Transform pud;
        if (!_puddles.TryGetValue(host, out pud) || pud == null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "WaterPuddle";
            Destroy(go.GetComponent<Collider>());
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = pm;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pud = go.transform;
            pud.position = hit.point + hit.normal * 0.0015f;
            pud.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            pud.localScale = new Vector3(0.02f, 0.0006f, 0.016f);
            pud.SetParent(host, true);                    // stays on the battery if it is carried away
            _puddles[host] = pud;
        }
        Vector3 ws = pud.lossyScale;
        if (ws.x < cap)
        {
            Vector3 grow = new Vector3(ws.x * 1.06f + 0.002f, ws.y, ws.z * 1.06f + 0.0018f);
            Vector3 ps = pud.parent != null ? pud.parent.lossyScale : Vector3.one;
            pud.localScale = new Vector3(grow.x / ps.x, grow.y / ps.y, grow.z / ps.z);
        }
    }

    private void Toast(string msg, bool warn)
    {
        if (hud == null || Time.time < _nextToast) return;
        _nextToast = Time.time + 6f;
        hud.ShowToast(msg, warn ? hud.WarnColor : hud.GoodColor);
    }

    // ---------------- procedural sounds ----------------
    private static void MakePlinks()
    {
        const int rate = 22050;
        float[] baseF = { 820f, 960f, 1100f };
        _plinks = new AudioClip[baseF.Length];
        for (int c = 0; c < baseF.Length; c++)
        {
            int len = (int)(rate * 0.11f);
            var data = new float[len];
            float phase = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / rate;
                float f = baseF[c] * (1f + 1.1f * (1f - Mathf.Exp(-t * 40f)));   // the rising "plip"
                phase += 2f * Mathf.PI * f / rate;
                float env = Mathf.Min(1f, t * 900f) * Mathf.Exp(-t * 38f);
                data[i] = Mathf.Sin(phase) * env * 0.7f;
            }
            _plinks[c] = AudioClip.Create("Plink" + c, len, 1, rate, false);
            _plinks[c].SetData(data, 0);
        }
    }

    private static AudioClip Crinkle
    {
        get
        {
            if (_crinkle != null) return _crinkle;
            const int rate = 22050; int len = (int)(rate * 0.4f);
            var data = new float[len];
            var rnd = new System.Random(5);
            float amp = 0f, lp = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / rate;
                if (rnd.NextDouble() < 0.004) amp = 0.5f + (float)rnd.NextDouble() * 0.5f;   // crackles
                amp *= 0.9965f;
                float n = (float)(rnd.NextDouble() * 2 - 1);
                lp = Mathf.Lerp(lp, n, 0.55f);
                float env = Mathf.Min(1f, t * 30f) * Mathf.Clamp01((0.4f - t) * 6f);
                data[i] = (n - lp) * amp * env * 0.9f;
            }
            _crinkle = AudioClip.Create("Crinkle", len, 1, rate, false);
            _crinkle.SetData(data, 0);
            return _crinkle;
        }
    }
}
