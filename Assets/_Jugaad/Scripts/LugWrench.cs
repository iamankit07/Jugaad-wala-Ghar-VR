using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// L-shaped wheel spanner. Bring its socket end to a wheel nut and it clicks onto the nut; from then
/// on it only swings around the nut's axis, following your hand like a real lever. Anticlockwise
/// (seen from outside) undoes a nut, clockwise tightens it. Let go and it stays hanging on the nut -
/// grab it again to carry on (ratchet-style). It only comes off a nut that is finished - fully undone
/// or fully tight; half-way, pulling your hand away just lets go of it (it stays on the nut).
/// Put it on the wrench root: local +Z = from the socket back along the short leg, local +Y = along
/// the long lever; socketPoint = inside bottom of the socket (sits on the nut's outer face).
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class LugWrench : MonoBehaviour
{
    [Tooltip("Point inside the socket that sits on the nut's outer face.")]
    [SerializeField] private Transform socketPoint;
    [Tooltip("Where the hand holds the lever.")]
    [SerializeField] private Transform gripPoint;
    [SerializeField] private float engageDistance = 0.06f;
    [Tooltip("After coming off a nut the spanner won't hop straight back onto it until the socket is this far away.")]
    [SerializeField] private float releaseDistance = 0.08f;
    [Tooltip("Hand at least this far from the nut's axis: moving it round the nut turns the spanner.")]
    [SerializeField] private float leverRadius = 0.08f;
    [SerializeField] private float pullOffDistance = 0.35f;
    [SerializeField] private float tickEveryDegrees = 20f;
    [SerializeField] private AudioClip engageClip;
    [SerializeField] private AudioClip[] turnClips;
    [SerializeField] private TaskHUD hud;

    [TextArea] [SerializeField] private string wrongWayLoosenEn = "This nut is tight - turn ANTICLOCKWISE to undo it.";
    [TextArea] [SerializeField] private string wrongWayLoosenHi = "Yeh nut tight hai - kholne ke liye ANTICLOCKWISE (ghadi ki ulti disha) ghumao.";
    [TextArea] [SerializeField] private string wrongWayTightenEn = "Other way - CLOCKWISE to tighten!";
    [TextArea] [SerializeField] private string wrongWayTightenHi = "Doosri taraf - tight karne ke liye CLOCKWISE (ghadi ki disha) ghumao!";
    [TextArea] [SerializeField] private string finishFirstEn = "Finish this nut first - undo it all the way or tighten it all the way.";
    [TextArea] [SerializeField] private string finishFirstHi = "Pehle yeh nut poora kholo ya poora tight karo.";

    public LugNut Engaged => _nut;
    public bool IsHeld => _grab != null && _grab.isSelected && Hand != null;

    private XRGrabInteractable _grab;
    private Rigidbody _rb;
    private LugNut _nut;
    private float _phi;                // lever angle around the nut axis
    private float _prevHand;
    private bool _haveHand;
    private float _wrong, _tickAcc, _nextToast, _nextLockedToast;
    private bool _regrabbing;
    private Vector3 _ref;
    private Vector3 _prevTwist;
    private float _prevR, _lastWrongTime;
    private LugNut _lastNut;          // the nut we just came off - not re-engaged until the socket has moved away

    private Transform Hand
    {
        get
        {
            if (_grab == null || !_grab.isSelected) return null;
            foreach (var i in _grab.interactorsSelecting) if (!(i is XRSocketInteractor)) return i.transform;
            return null;
        }
    }

    private void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _rb = GetComponent<Rigidbody>();
        if (hud == null) hud = FindFirstObjectByType<TaskHUD>();
    }

    private void OnEnable()
    {
        _grab.selectEntered.AddListener(OnGrab);
        _grab.selectExited.AddListener(OnRelease);
        Application.onBeforeRender += OnBeforeRender;
    }

    private void OnDisable()
    {
        _grab.selectEntered.RemoveListener(OnGrab);
        _grab.selectExited.RemoveListener(OnRelease);
        Application.onBeforeRender -= OnBeforeRender;
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        if (_regrabbing) return;
        _haveHand = false;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        if (_regrabbing) return;
        _haveHand = false;
        if (_nut != null && _rb != null) _rb.isKinematic = true;   // stays hanging on the nut
    }

    private bool OnNut => _nut != null && _nut.Current == LugNut.State.OnStud;

    private void Update()
    {
        if (_nut == null) TryEngage();
        else Drive();
    }

    private void TryEngage()
    {
        if (!IsHeld || socketPoint == null) return;
        LugNut best = null; float bestD = engageDistance;
        foreach (var n in LugNut.All)
        {
            if (n == null || n.Current != LugNut.State.OnStud) continue;
            float d = Vector3.Distance(socketPoint.position, n.OuterFace);
            if (n == _lastNut)
            {
                if (d > releaseDistance) _lastNut = null;
                continue;
            }
            if (d < bestD) { bestD = d; best = n; }
        }
        if (best == null) return;
        if (!best.CanTurn)
        {
            if (!string.IsNullOrEmpty(best.LockedHint) && Time.time > _nextLockedToast)
            {
                _nextLockedToast = Time.time + 6f;
                Toast(best.LockedHint);
            }
            return;
        }
        Engage(best);
    }

    private void Engage(LugNut n)
    {
        _nut = n;
        _grab.trackPosition = false;
        _grab.trackRotation = false;
        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, n.Axis);
        _ref = up.sqrMagnitude > 1e-4f ? up.normalized : Vector3.ProjectOnPlane(Vector3.forward, n.Axis).normalized;
        Vector3 lever = Vector3.ProjectOnPlane(transform.up, n.Axis);
        _phi = lever.sqrMagnitude > 1e-4f ? Vector3.SignedAngle(_ref, lever, n.Axis) : 0f;
        _haveHand = false;
        _wrong = 0f;
        PoseOnNut();
        SfxMix.PlayAt(engageClip != null ? engageClip : ProceduralSfx.Tick, n.OuterFace, 0.7f, 0.05f);
        XRHaptics.PulseHolders(_grab, 0.4f, 0.05f);
    }

    private void Disengage()
    {
        _lastNut = _nut;
        _nut = null;
        _grab.trackPosition = true;
        _grab.trackRotation = true;
        // re-grab so it stays where it is in the hand instead of jumping to the old grab pose
        var hand = FirstHandInteractor();
        if (hand != null && _grab.interactionManager != null)
        {
            _regrabbing = true;
            _grab.interactionManager.SelectExit(hand, (IXRSelectInteractable)_grab);
            _grab.interactionManager.SelectEnter(hand, (IXRSelectInteractable)_grab);
            _regrabbing = false;
        }
        else if (_rb != null) { _rb.isKinematic = false; }
    }

    private IXRSelectInteractor FirstHandInteractor()
    {
        if (_grab == null || !_grab.isSelected) return null;
        foreach (var i in _grab.interactorsSelecting) if (!(i is XRSocketInteractor)) return i;
        return null;
    }

    private void Drive()
    {
        if (!OnNut) { Disengage(); return; }      // it came off
        Transform hand = Hand;
        if (hand == null) { PoseOnNut(); return; }

        Vector3 axis = _nut.Axis;
        Vector3 c = _nut.OuterFace;
        Vector3 grip = gripPoint != null ? gripPoint.position : transform.position;
        if (Vector3.Distance(hand.position, grip) > pullOffDistance)
        {
            if (HalfWay) { LetGoOnNut(); return; }   // half-done nut: the spanner stays on it, the hand just lets go
            Disengage(); return;                     // nut finished (or untouched): pulled right off
        }

        // Two ways to turn it, whichever the player does more of this frame:
        //  - hand out on the lever, moving round the nut (the hand's angle round the axis)
        //  - holding it anywhere and twisting the wrist (the hand's own roll round the axis)
        // Hand position close to the axis is useless for an angle (tiny moves = huge random swings),
        // so the circle only counts when the hand is out on the lever.
        Vector3 d = Vector3.ProjectOnPlane(hand.position - c, axis);
        float r = d.magnitude;
        Vector3 tw = Vector3.ProjectOnPlane(hand.up, axis);
        if (tw.sqrMagnitude < 0.04f) tw = Vector3.ProjectOnPlane(hand.forward, axis);
        float ang = r > 1e-4f ? Vector3.SignedAngle(_ref, d, axis) : _prevHand;
        if (!_haveHand)
        {
            _prevHand = ang; _prevTwist = tw; _prevR = r; _haveHand = true;
            if (r > leverRadius * 1.5f) _phi = ang;          // grabbed out on the lever: it swings to the hand
            PoseOnNut();
            return;
        }
        float circle = r > leverRadius && _prevR > leverRadius ? Mathf.DeltaAngle(_prevHand, ang) : 0f;
        float twist = Vector3.SignedAngle(_prevTwist, tw, axis);
        _prevHand = ang; _prevTwist = tw; _prevR = r;
        float delta = Mathf.Clamp(Mathf.Abs(twist) > Mathf.Abs(circle) ? twist : circle, -30f, 30f);

        if (Mathf.Abs(delta) > 0.05f) ApplyTurn(-delta, c);   // + about the outward axis is clockwise seen from outside = tighten; the nut counts undoing as +
        if (OnNut) PoseOnNut();
    }

    /// <summary>The nut is part-way: not fully tight and not fully undone. The spanner stays on it.</summary>
    private bool HalfWay => _nut != null && _nut.Progress > 0.0001f && _nut.Progress < 0.9999f;

    private void LetGoOnNut()
    {
        var hand = FirstHandInteractor();
        if (hand != null && _grab.interactionManager != null) _grab.interactionManager.SelectExit(hand, (IXRSelectInteractable)_grab);
        if (_rb != null) _rb.isKinematic = true;
        _haveHand = false;
        PoseOnNut();
        if (Time.time > _nextToast)
        {
            _nextToast = Time.time + 5f;
            Toast(GameLanguage.Pick(finishFirstEn, finishFirstHi));
        }
    }

    private void ApplyTurn(float delta, Vector3 at)
    {
        LugNut nut = _nut;
        float before = nut.Progress;
        float turned = nut.Turn(delta);
        _phi -= turned;                                      // the lever turns the opposite way round the axis
        bool justTightened = before > 0.0001f && nut.IsTight;

        if (Mathf.Abs(turned) < Mathf.Abs(delta) * 0.5f && nut.Current == LugNut.State.OnStud)
        {
            // at a stop - pushing the wrong way?
            bool atHome = nut.Progress <= 0.0001f && delta < 0f;
            bool atEnd = nut.Progress >= 0.9999f && delta > 0f && !nut.PopAllowed;
            if (atHome || atEnd)
            {
                if (Time.time - _lastWrongTime > 0.6f) _wrong = 0f;   // only a steady push the wrong way counts, not hand jitter
                _lastWrongTime = Time.time;
                _wrong += Mathf.Abs(delta);
                if (_wrong > 35f && Time.time > _nextToast)
                {
                    _nextToast = Time.time + 5f;
                    _wrong = 0f;
                    Toast(atHome ? GameLanguage.Pick(wrongWayLoosenEn, wrongWayLoosenHi) : GameLanguage.Pick(wrongWayTightenEn, wrongWayTightenHi));
                    XRHaptics.PulseHolders(_grab, 0.6f, 0.1f);
                }
            }
        }
        else if (Mathf.Abs(turned) > 0f)
        {
            _wrong = 0f;
            _tickAcc += Mathf.Abs(turned);
            if (_tickAcc >= tickEveryDegrees)
            {
                _tickAcc = 0f;
                AudioClip clip = turnClips != null && turnClips.Length > 0 ? turnClips[Random.Range(0, turnClips.Length)] : ProceduralSfx.Tick;
                SfxMix.PlayAt(clip, at, 0.35f, 0.15f);
                XRHaptics.PulseHolders(_grab, 0.25f, 0.03f);
            }
        }
        if (justTightened || nut.Current != LugNut.State.OnStud) Disengage();   // done with this nut
    }

    private void OnBeforeRender()
    {
        if (OnNut) PoseOnNut();
    }

    private void PoseOnNut()
    {
        if (_nut == null || socketPoint == null) return;
        Vector3 axis = _nut.Axis;
        Vector3 lever = Quaternion.AngleAxis(_phi, axis) * _ref;
        Quaternion rot = Quaternion.LookRotation(axis, lever);           // +Z out along the stud, +Y along the lever
        Vector3 offset = rot * Quaternion.Inverse(transform.rotation) * (transform.position - socketPoint.position);
        transform.SetPositionAndRotation(_nut.OuterFace + offset, rot);
        if (_rb != null && _rb.isKinematic) { _rb.position = transform.position; _rb.rotation = rot; }
    }

    private void Toast(string msg)
    {
        if (hud != null) hud.ShowToast(msg, hud.WarnColor);
        Debug.Log("[LugWrench] " + msg);
    }

    // tests
    public void EngageFromCode(LugNut n) { if (n != null && n.CanTurn && n.Current == LugNut.State.OnStud) Engage(n); }
    public void TurnFromCode(float degrees)
    {
        if (_nut == null) return;
        ApplyTurn(degrees, _nut.OuterFace);
        if (OnNut) PoseOnNut();
    }
}
