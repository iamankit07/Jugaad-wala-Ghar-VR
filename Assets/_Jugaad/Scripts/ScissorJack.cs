using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Scissor jack. Carried around folded; once it's set down in its place under the car (SnapSpot)
/// its long crank handle comes out. Grab the knob at the end of the handle and wind it round in
/// circles: clockwise (looking down the handle at the jack) raises it, anticlockwise lowers it.
/// The saddle first rises up to the car's sill, then lifts the car (CarLift). Ratchet "khat-khat"
/// as it turns, a creak when it's taking the car's weight. The knob only moves while the jack can
/// move, so a blocked direction feels stuck.
/// Put it on the jack root (local X = screw axis, pointing out from under the car).
/// </summary>
public class ScissorJack : MonoBehaviour
{
    [Header("Parts (built by the setup script)")]
    [SerializeField] private Transform saddle;
    [SerializeField] private Transform armLowerOut, armUpperOut, armLowerIn, armUpperIn;
    [SerializeField] private Transform blockOut, blockIn, screw;
    [Tooltip("Hook end of the handle - sits at the outer screw block.")]
    [SerializeField] private Transform handleRoot;
    [Tooltip("Rotates with the crank (local X = crank axis).")]
    [SerializeField] private Transform crankPivot;
    [SerializeField] private XRSimpleInteractable knob;

    [Header("Geometry")]
    [SerializeField] private float baseY = 0.035f;
    [SerializeField] private float topInset = 0.025f;
    [SerializeField] private float armLength = 0.13f;
    [SerializeField] private float minHeight = 0.11f;

    [Header("Car")]
    [SerializeField] private SnapSpot place;
    [SerializeField] private CarLift lift;
    [Tooltip("Car-local point on the sill that the saddle pushes on.")]
    [SerializeField] private Vector3 sillLocal;
    [Tooltip("Hub lift when fully jacked up.")]
    [SerializeField] private float maxLift = 0.02f;
    [Tooltip("How far below the sill (car on its wheels) the saddle must drop to count as 'lowered away'.")]
    [SerializeField] private float lowerClearance = 0.03f;

    [Header("Feel")]
    [SerializeField] private float heightPerTurn = 0.04f;
    [SerializeField] private float tickEveryDegrees = 30f;
    [SerializeField] private AudioClip[] ratchetClips;
    [SerializeField] private AudioClip creakClip;
    [SerializeField] private AudioClip stopClip;

    public bool Placed { get; private set; }
    /// <summary>Taking the jack away is allowed (only ever when it isn't holding the car up).</summary>
    public bool AllowRemove { get; set; } = true;
    /// <summary>The saddle is pushing the car up.</summary>
    public bool CarriesCar => Placed && lift != null && _h > SillY(lift.BaseLift) + 0.0005f;
    public bool CanRaise { get; set; }
    public bool CanLower { get; set; }
    public string RaiseBlockedHint { get; set; }
    public string LowerBlockedHint { get; set; }
    /// <summary>Shown when it's wound the wrong way at the bottom (nothing to lower).</summary>
    public string WrongWayHint { get; set; } = "Clockwise ghumao - tab gaadi uthegi!";
    /// <summary>Shown the first time the knob is grabbed after placing the jack.</summary>
    public string GrabHint { get; set; }
    public float Height => _h;
    public bool CarFullyUp => lift != null && lift.Lift >= maxLift - 0.0015f;
    /// <summary>Saddle has come down clear of the sill with the car standing on its own wheels.</summary>
    public bool LoweredAway => Placed && lift != null && lift.Lift <= lift.BaseLift + 0.0005f && _h <= SillY(lift.BaseLift) - lowerClearance + 0.001f;
    public event System.Action<string> Hint;

    private float _h;
    private float _crank;          // degrees, positive = raising
    private float _tickAcc, _creakAcc;
    private Transform _hand;
    private float _prevHandAngle;
    private bool _haveAngle;
    private float _nextHint, _stopSaid, _wrongAcc;
    private Vector3 _handleScale;
    private Vector3 _prevTwist;
    private bool _grabHintShown;

    private void Awake()
    {
        _h = minHeight;
        if (handleRoot != null) { _handleScale = handleRoot.localScale; handleRoot.gameObject.SetActive(false); }
        Apply();
        if (place != null) place.CanRemove = () => Placed && AllowRemove && !CarriesCar && _hand == null;
    }

    private void OnEnable()
    {
        if (place != null) { place.OnSnapped += OnPlaced; place.OnRemoved += OnTakenAway; }
        if (knob != null)
        {
            knob.selectEntered.AddListener(OnGrab);
            knob.selectExited.AddListener(OnRelease);
        }
    }

    private void OnDisable()
    {
        if (place != null) { place.OnSnapped -= OnPlaced; place.OnRemoved -= OnTakenAway; }
        if (knob != null)
        {
            knob.selectEntered.RemoveListener(OnGrab);
            knob.selectExited.RemoveListener(OnRelease);
        }
    }

    private void OnPlaced()
    {
        Placed = true;
        _grabHintShown = false;
        if (handleRoot != null) StartCoroutine(ShowHandle());
        StartCoroutine(RiseToSill());
    }

    // the saddle comes up to just under the car by itself - cranking then lifts the car straight away
    private System.Collections.IEnumerator RiseToSill()
    {
        if (lift == null) yield break;
        float from = _h, to = Mathf.Max(minHeight, SillY(lift.BaseLift) - 0.008f);
        if (to <= from) yield break;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.5f)
        {
            if (_hand != null) yield break;              // the player started cranking - leave it to them
            float nh = Mathf.Lerp(from, to, t * t * (3f - 2f * t));
            _crank += (nh - _h) / heightPerTurn * 360f;
            _h = nh;
            Apply();
            yield return null;
        }
    }

    // picked up again: fold the handle away and close it right down
    private void OnTakenAway()
    {
        Placed = false;
        StopAllCoroutines();
        if (knob != null && knob.isSelected && knob.interactionManager != null)
            foreach (var i in new System.Collections.Generic.List<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor>(knob.interactorsSelecting))
                knob.interactionManager.SelectExit(i, (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)knob);
        _hand = null;
        if (lift != null && lift.Lift > lift.BaseLift) lift.SetLift(lift.BaseLift);   // never leave the car hanging in the air
        _h = minHeight;
        _crank = 0f;
        Apply();
        if (handleRoot != null) { handleRoot.localScale = _handleScale; handleRoot.gameObject.SetActive(false); }
    }

    private System.Collections.IEnumerator ShowHandle()
    {
        handleRoot.gameObject.SetActive(true);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
        {
            float e = 1f - (1f - t) * (1f - t);
            handleRoot.localScale = new Vector3(_handleScale.x * Mathf.Max(0.05f, e), _handleScale.y, _handleScale.z);
            yield return null;
        }
        handleRoot.localScale = _handleScale;
        SfxMix.PlayAt(ratchetClips != null && ratchetClips.Length > 0 ? ratchetClips[0] : ProceduralSfx.Tick, handleRoot.position, 0.6f, 0.05f);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        _hand = args.interactorObject.transform;
        _haveAngle = false;
        if (!_grabHintShown && !string.IsNullOrEmpty(GrabHint)) { _grabHintShown = true; Hint?.Invoke(GrabHint); }
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        _hand = null;
    }

    private float SillY(float atLift) => lift != null ? lift.PointAt(sillLocal, atLift).y - transform.position.y : 999f;
    private float MaxHeight => SillY(maxLift);

    private void Update()
    {
        if (!Placed || _hand == null || crankPivot == null) return;
        Vector3 axis = crankPivot.right;                  // from the jack out towards the knob
        // clockwise as seen by someone at the knob looking at the jack = positive about the axis (Unity: + is clockwise seen from the axis tip)
        Vector3 d = Vector3.ProjectOnPlane(_hand.position - crankPivot.position, axis);
        Vector3 tw = Vector3.ProjectOnPlane(_hand.up, axis);
        if (tw.sqrMagnitude < 0.04f) tw = Vector3.ProjectOnPlane(_hand.forward, axis);
        float ang = d.sqrMagnitude > 0.0004f ? Vector3.SignedAngle(crankPivot.parent.up, d, axis) : _prevHandAngle;
        if (!_haveAngle) { _prevHandAngle = ang; _prevTwist = tw; _haveAngle = true; return; }
        float circle = d.sqrMagnitude > 0.0004f ? Mathf.DeltaAngle(_prevHandAngle, ang) : 0f;
        float twist = Vector3.SignedAngle(_prevTwist, tw, axis);
        _prevHandAngle = ang; _prevTwist = tw;
        float delta = Mathf.Clamp(Mathf.Abs(twist) > Mathf.Abs(circle) ? twist : circle, -40f, 40f);
        if (Mathf.Abs(delta) < 0.01f) return;

        if (delta > 0f && !CanRaise) { Blocked(RaiseBlockedHint); return; }
        if (delta < 0f && !CanLower) { Blocked(LowerBlockedHint); return; }
        if (delta < 0f && _h <= minHeight + 0.0005f) { _wrongAcc += -delta; if (_wrongAcc > 60f) { _wrongAcc = 0f; Blocked(WrongWayHint); } return; }
        if (delta > 0f) _wrongAcc = 0f;

        float hMax = MaxHeight;
        float hMin = minHeight;
        float newH = Mathf.Clamp(_h + delta / 360f * heightPerTurn, hMin, hMax);
        float moved = newH - _h;
        if (Mathf.Abs(moved) < 1e-6f)
        {
            if (delta > 0f && _h >= hMax - 1e-4f && Time.time > _stopSaid)
            {
                _stopSaid = Time.time + 2f;
                SfxMix.PlayAt(stopClip != null ? stopClip : ProceduralSfx.Thunk, transform.position, 0.7f, 0.05f);
                XRHaptics.Pulse(KnobHolder(), 0.6f, 0.08f);
            }
            return;
        }
        _h = newH;
        float turned = moved / heightPerTurn * 360f;      // only as much as the jack really moved
        _crank += turned;
        bool carried = Apply();

        _tickAcc += Mathf.Abs(turned);
        if (_tickAcc >= tickEveryDegrees)
        {
            _tickAcc = 0f;
            AudioClip c = ratchetClips != null && ratchetClips.Length > 0 ? ratchetClips[Random.Range(0, ratchetClips.Length)] : ProceduralSfx.Tick;
            SfxMix.PlayAt(c, crankPivot.position, carried ? 0.55f : 0.4f, 0.12f);
            XRHaptics.Pulse(KnobHolder(), carried ? 0.35f : 0.15f, 0.03f);
        }
        if (carried)
        {
            _creakAcc += Mathf.Abs(turned);
            if (_creakAcc >= 140f)
            {
                _creakAcc = 0f;
                if (creakClip != null) SfxMix.PlayAt(creakClip, saddle != null ? saddle.position : transform.position, 0.35f, 0.15f);
            }
        }
    }

    private UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor KnobHolder()
    {
        if (knob == null || !knob.isSelected) return null;
        return knob.interactorsSelecting[0];
    }

    private void Blocked(string hint)
    {
        if (string.IsNullOrEmpty(hint) || Time.time < _nextHint) return;
        _nextHint = Time.time + 5f;
        Hint?.Invoke(hint);
        XRHaptics.Pulse(KnobHolder(), 0.5f, 0.06f);
    }

    /// <summary>Pose the scissor + handle for the current height and push the car. True if the car is on the jack.</summary>
    private bool Apply()
    {
        float yb = baseY, yt = _h - topInset;
        float half = (yt - yb) * 0.5f;
        float ym = yb + half;
        float w = Mathf.Sqrt(Mathf.Max(0.0001f, armLength * armLength - half * half));

        SetArm(armLowerOut, new Vector3(0f, yb, 0f), new Vector3(w, ym, 0f));
        SetArm(armUpperOut, new Vector3(w, ym, 0f), new Vector3(0f, yt, 0f));
        SetArm(armLowerIn, new Vector3(0f, yb, 0f), new Vector3(-w, ym, 0f));
        SetArm(armUpperIn, new Vector3(-w, ym, 0f), new Vector3(0f, yt, 0f));
        if (blockOut != null) blockOut.localPosition = new Vector3(w, ym, 0f);
        if (blockIn != null) blockIn.localPosition = new Vector3(-w, ym, 0f);
        if (screw != null) screw.localPosition = new Vector3(w, ym, 0f);
        if (saddle != null) saddle.localPosition = new Vector3(0f, _h, 0f);
        if (handleRoot != null) handleRoot.localPosition = new Vector3(w + 0.025f, ym, 0f);
        if (crankPivot != null) crankPivot.localRotation = Quaternion.AngleAxis(_crank, Vector3.right);

        if (!Placed || lift == null) return false;
        float s0 = SillY(0f);
        float f = (SillY(0.05f) - s0) / 0.05f;
        float target = f > 0.01f ? (_h - s0) / f : lift.BaseLift;
        float l = Mathf.Max(lift.BaseLift, target);
        if (Mathf.Abs(l - lift.Lift) > 1e-5f) lift.SetLift(l);
        return target > lift.BaseLift;
    }

    private static void SetArm(Transform arm, Vector3 a, Vector3 b)
    {
        if (arm == null) return;
        arm.localPosition = (a + b) * 0.5f;
        arm.localRotation = Quaternion.FromToRotation(Vector3.right, (b - a).normalized);
    }

    // ---- tests / editor ----
    public void TurnFromCode(float degrees)
    {
        if (!Placed) return;
        if (degrees > 0f && !CanRaise) { Blocked(RaiseBlockedHint); return; }
        if (degrees < 0f && !CanLower) { Blocked(LowerBlockedHint); return; }
        float newH = Mathf.Clamp(_h + degrees / 360f * heightPerTurn, minHeight, MaxHeight);
        _crank += (newH - _h) / heightPerTurn * 360f;
        _h = newH;
        Apply();
    }

    public float TurnsLeftToMax => (MaxHeight - _h) / heightPerTurn;
}
