using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Safety net for a player who is completely stuck: if nothing moves forward for a long time
/// (stuckDelay, 3 min by default) a big bobbing arrow appears over the object they need next
/// ("the jack is HERE"), plus a small pointer at the edge of the view when that object is behind
/// them. It follows the task's own little steps (pick up the jack -> where it goes -> the knob...).
/// Any progress hides it and restarts the clock; once help was needed in a task, the next step of
/// that same task gets its arrow sooner (followUpDelay).
/// Put it anywhere in the level (e.g. on LevelFlow) and fill the references.
/// </summary>
public class StuckHintArrow : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds without progress before the arrow shows.")]
    [SerializeField] private float stuckDelay = 180f;
    [Tooltip("Once the arrow was needed in a task, the next step of that task shows it after this long.")]
    [SerializeField] private float followUpDelay = 30f;

    [Header("Look")]
    [SerializeField] private Material arrowMaterial;
    [SerializeField] private float arrowLength = 0.26f;
    [SerializeField] private float gapAbove = 0.06f;
    [SerializeField] private float pointerDistance = 0.7f;
    [SerializeField] private float pointerOffset = 0.2f;
    [Tooltip("Show the edge pointer when the target is more than this many degrees from where you look.")]
    [SerializeField] private float pointerAngle = 35f;

    [Header("Level")]
    [SerializeField] private LevelFlowManager flow;
    [SerializeField] private TaskHUD hud;
    [SerializeField] private CarSeat seat;

    [Header("Task objects")]
    [SerializeField] private PhoneTorch phone;
    [SerializeField] private XRBaseInteractable bora;
    [SerializeField] private Transform oilZone;
    [SerializeField] private XRBaseInteractable pliers;
    [SerializeField] private Transform shutterStub;
    [SerializeField] private XRBaseInteractable broom;
    [SerializeField] private Transform toolBag;
    [SerializeField] private LeakingPipe pipe;
    [SerializeField] private XRBaseInteractable polythene;
    [SerializeField] private XRBaseInteractable rope;
    [SerializeField] private Transform leakJoint;
    [SerializeField] private Transform driverDoor;
    [SerializeField] private Transform ignitionKey;
    [SerializeField] private HoodLift hood;
    [SerializeField] private float hoodOpenAngle = 10f;
    [SerializeField] private XRBaseInteractable battery;
    [SerializeField] private Transform batterySocket;
    [SerializeField] private TyreChange tyre;
    [SerializeField] private Transform jackKnob;
    [SerializeField] private LugWrench wrench;
    [SerializeField] private CarEngine engine;

    public Transform Target { get; private set; }
    public bool Showing { get; private set; }

    private Transform _arrow, _pointer;
    private int _task = -2;
    private string _lastKey;
    private float _since;
    private bool _helpedThisTask;
    private Vector3 _pointerPos;

    private void Awake()
    {
        if (flow == null) flow = FindFirstObjectByType<LevelFlowManager>();
        if (hud == null) hud = FindFirstObjectByType<TaskHUD>();
        if (seat == null) seat = FindFirstObjectByType<CarSeat>();
        if (tyre == null) tyre = FindFirstObjectByType<TyreChange>();
        if (engine == null) engine = FindFirstObjectByType<CarEngine>();
        _arrow = BuildArrow("HintArrow", arrowLength);
        _pointer = BuildArrow("HintPointer", arrowLength * 0.35f);
        _arrow.gameObject.SetActive(false);
        _pointer.gameObject.SetActive(false);
    }

    // ---------------- what does the player need right now? ----------------
    private Transform FindTarget()
    {
        TaskCondition c = flow != null ? flow.CurrentCondition : null;
        if (c == null) return null;

        if (c is PhoneTorchCondition)
            return phone != null && !phone.InPocket ? phone.transform : null;   // in the pocket: nothing to point at (B button)
        if (c is OilCoveredCondition)
            return Held(bora) ? oilZone : T(bora);
        if (c is ShutterOpenCondition)
            return Held(pliers) ? shutterStub : T(pliers);
        if (c is BagDownCondition)
            return Held(broom) ? toolBag : T(broom);
        if (c is LeakFixedCondition)
        {
            if (pipe == null) return null;
            XRBaseInteractable need = pipe.Current == LeakingPipe.State.Leaking ? polythene : rope;
            if (need == null || !need.gameObject.activeInHierarchy) return toolBag;   // still in the bag
            return Held(need) ? leakJoint : need.transform;
        }
        if (c is CarStartTriedCondition || c is EngineStartedCondition)
            return seat != null && seat.Seated ? ignitionKey : driverDoor;
        if (c is BatteryInstalledCondition)
        {
            if (!Held(battery)) return T(battery);                                // first: find the battery
            if (hood != null && !hood.IsOpen && hood.Angle < hoodOpenAngle) return hood.transform;   // then: open the bonnet
            return batterySocket;                                                // then: where it goes
        }
        if (c is TyreStageCondition && tyre != null) return TyreTarget();
        if (c is DriveOutCondition)
        {
            var d = (DriveOutCondition)c;
            if (d.Parked) return null;                                               // waiting for Papa's call
            if (tyre != null && tyre.Jack != null && tyre.Jack.Placed) return tyre.Jack.transform;   // jack still under the car
            if (seat != null && !seat.Seated) return driverDoor;
            if (engine != null && !engine.Started) return ignitionKey;
            return d.ParkingSpot;
        }
        return null;
    }

    private Transform TyreTarget()
    {
        ScissorJack jack = tyre.Jack;
        WheelHub hub = tyre.Hub;
        var jackGrab = jack != null ? jack.GetComponent<XRBaseInteractable>() : null;
        switch (tyre.Current)
        {
            case TyreChange.Stage.Jack:
                if (jack == null) return null;
                if (jack.Placed) return jackKnob;
                return Held(jackGrab) && tyre.JackSpot != null ? tyre.JackSpot.transform : jack.transform;

            case TyreChange.Stage.Wheel:
            {
                LugNut on = NutWhere(n => n.Current == LugNut.State.OnStud);
                if (on != null) return SpannerOr(on);
                return hub != null && hub.Mounted != null ? hub.Mounted.transform : null;
            }

            default:   // fitting the good wheel and letting the car down
            {
                if (hub == null) return null;
                if (hub.Mounted == null)
                {
                    foreach (var w in CarWheel.All)
                        if (w != null && !w.IsFlat && Held(w.Grab)) return hub.transform;
                    return NearestGoodWheel();
                }
                if (hub.Mounted.IsFlat) return hub.Mounted.transform;
                LugNut loose = NutWhere(n => n.Current == LugNut.State.OnStud && !n.IsTight);
                if (loose != null) return SpannerOr(loose);
                return jackKnob;
            }
        }
    }

    // the spanner first, then the nut it should go on
    private Transform SpannerOr(LugNut nut)
    {
        if (wrench == null || !wrench.gameObject.activeInHierarchy) return nut.transform;
        if (wrench.Engaged != null) return wrench.Engaged.transform;
        if (!wrench.IsHeld) return wrench.transform;
        return nut.transform;
    }

    private LugNut NutWhere(System.Func<LugNut, bool> ok)
    {
        LugNut best = null; float bd = float.MaxValue;
        Vector3 from = wrench != null ? wrench.transform.position : Head != null ? Head.position : Vector3.zero;
        foreach (var n in LugNut.All)
        {
            if (n == null || !ok(n)) continue;
            float d = (n.transform.position - from).sqrMagnitude;
            if (d < bd) { bd = d; best = n; }
        }
        return best;
    }

    private Transform NearestGoodWheel()
    {
        Transform best = null; float bd = float.MaxValue;
        Vector3 from = Head != null ? Head.position : Vector3.zero;
        foreach (var w in CarWheel.All)
        {
            if (w == null || w.IsFlat || !w.gameObject.activeInHierarchy) continue;
            float d = (w.transform.position - from).sqrMagnitude;
            if (d < bd) { bd = d; best = w.transform; }
        }
        return best;
    }

    private static Transform T(Component c) => c != null && c.gameObject.activeInHierarchy ? c.transform : null;

    private static bool Held(XRBaseInteractable g)
    {
        if (g == null || !g.isSelected) return false;
        foreach (var i in g.interactorsSelecting) if (!(i is XRSocketInteractor)) return true;
        return false;
    }

    private Transform Head => Camera.main != null ? Camera.main.transform : null;

    // What counts as progress: the KIND of thing needed changing, or nuts getting done - not which
    // wheel/nut happens to be nearest (that changes just by walking around).
    private string ProgressKey(Transform target)
    {
        if (target == null) return "";
        string kind;
        if (target.GetComponent<LugNut>() != null) kind = "nut";
        else if (target.GetComponent<CarWheel>() != null && (tyre == null || tyre.Hub == null || tyre.Hub.Mounted == null || tyre.Hub.Mounted.transform != target)) kind = "wheel";
        else kind = target.GetInstanceID().ToString();
        int done = 0;
        foreach (var n in LugNut.All) if (n != null && (n.IsTight || n.IsOff)) done++;
        return kind + "|" + done;
    }

    // ---------------- clock ----------------
    private void Update()
    {
        int task = flow != null ? flow.CurrentTask : -1;
        if (task != _task)
        {
            _task = task;
            _helpedThisTask = false;
            _since = 0f;
        }
        Transform target = FindTarget();
        string key = ProgressKey(target);
        bool progressed = key != _lastKey;
        if (progressed)
        {
            _lastKey = key;
            _since = 0f;                       // something changed = progress
        }
        else _since += Time.deltaTime;         // paused game = paused clock

        // once up, it stays up until the player gets somewhere
        bool show = target != null && ((Showing && !progressed) || _since >= (_helpedThisTask ? followUpDelay : stuckDelay));
        if (show && !_helpedThisTask)
        {
            _helpedThisTask = true;
            if (hud != null) hud.ShowToast(GameLanguage.Pick("Stuck? Follow the arrow!", "Atak gaye? Teer ki taraf dekho!"), hud.WarnColor);
        }
        if (show && !Showing) SfxMix.PlayAt(ProceduralSfx.Tick, target.position, 0.8f, 0.02f);
        Showing = show;
        Target = show ? target : null;
        if (_arrow.gameObject.activeSelf != show) _arrow.gameObject.SetActive(show);
        if (!show && _pointer.gameObject.activeSelf) _pointer.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!Showing || Target == null) return;

        // big arrow standing over the object, pointing down at it
        Vector3 top = TopOf(Target);
        float bob = Mathf.Sin(Time.time * 4f) * 0.04f;
        Vector3 tip = top + Vector3.up * (gapAbove + bob);
        Quaternion down = Quaternion.LookRotation(Vector3.down, Quaternion.Euler(0f, Time.time * 90f, 0f) * Vector3.forward);
        _arrow.SetPositionAndRotation(tip - down * Vector3.forward * arrowLength, down);

        // small pointer in front of the eyes when the object is out of view
        Transform head = Head;
        if (head == null) { _pointer.gameObject.SetActive(false); return; }
        Vector3 to = top - head.position;
        bool outOfView = Vector3.Angle(head.forward, to) > pointerAngle;
        if (_pointer.gameObject.activeSelf != outOfView) { _pointer.gameObject.SetActive(outOfView); _pointerPos = PointerSpot(head, to); }
        if (!outOfView) return;
        _pointerPos = Vector3.Lerp(_pointerPos, PointerSpot(head, to), 1f - Mathf.Exp(-8f * Time.deltaTime));   // soft follow - no hard head-lock
        Vector3 aim = top - _pointerPos;
        if (aim.sqrMagnitude > 1e-6f)
            _pointer.SetPositionAndRotation(_pointerPos - aim.normalized * arrowLength * 0.35f, Quaternion.LookRotation(aim, head.up));
    }

    private Vector3 PointerSpot(Transform head, Vector3 to)
    {
        Vector3 side = Vector3.ProjectOnPlane(to, head.forward);
        if (side.sqrMagnitude < 1e-4f) side = head.right;                      // straight behind: go via the side
        return head.position + head.forward * pointerDistance + side.normalized * pointerOffset;
    }

    private static Vector3 TopOf(Transform t)
    {
        bool any = false; Bounds b = new Bounds(t.position, Vector3.zero);
        foreach (var r in t.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (!any || b.size.magnitude > 2.5f) return t.position;               // nothing to measure / something huge
        return new Vector3(b.center.x, b.max.y, b.center.z);
    }

    // ---------------- the arrow model (tip at local +Z) ----------------
    private Transform BuildArrow(string name, float length)
    {
        var root = new GameObject(name).transform;
        float headLen = length * 0.42f, shaftLen = length - headLen;
        float shaftR = length * 0.09f, headR = length * 0.22f;

        var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(shaft.GetComponent<Collider>());
        shaft.name = "Shaft";
        shaft.transform.SetParent(root, false);
        shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        shaft.transform.localScale = new Vector3(shaftR * 2f, shaftLen * 0.5f, shaftR * 2f);
        shaft.transform.localPosition = new Vector3(0f, 0f, shaftLen * 0.5f);

        var head = new GameObject("Head");
        head.transform.SetParent(root, false);
        head.transform.localPosition = new Vector3(0f, 0f, shaftLen);
        head.AddComponent<MeshFilter>().sharedMesh = Cone(headR, headLen, 20);
        var hr = head.AddComponent<MeshRenderer>();

        var sr = shaft.GetComponent<MeshRenderer>();
        foreach (var r in new Renderer[] { sr, hr })
        {
            if (arrowMaterial != null) r.sharedMaterial = arrowMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        return root;
    }

    private static Mesh Cone(float radius, float height, int n)
    {
        var v = new Vector3[n * 2 + 1 + n];
        var tris = new int[n * 6];
        // sides: each segment has its own base pair so normals stay crisp enough; tip shared per segment
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            v[i] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);   // ring (sides)
            v[n + i] = new Vector3(0f, 0f, height);                                // tip copies
            v[2 * n + 1 + i] = v[i];                                               // ring (base cap)
        }
        v[2 * n] = Vector3.zero;                                                   // base centre
        int k = 0;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            tris[k++] = i; tris[k++] = n + i; tris[k++] = j;                      // side
            tris[k++] = 2 * n; tris[k++] = 2 * n + 1 + j; tris[k++] = 2 * n + 1 + i; // cap
        }
        var m = new Mesh { name = "HintCone", vertices = v, triangles = tris };
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // tests
    public void ForceStuck() { _since = stuckDelay + 1f; }
}
