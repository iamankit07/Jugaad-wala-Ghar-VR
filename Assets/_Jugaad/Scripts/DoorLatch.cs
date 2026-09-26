using UnityEngine;

/// <summary>
/// Real-door latch for an UNLOCKED door: while the door is shut it stays latched until a
/// DoorHandle is pressed down; when it swings back shut it latches again ("click").
/// Also gives haptics to the hand on the handle: a knock when the latch releases, a light
/// rumble while the door swings, and a thud when it shuts.
/// While the LockableDoor is locked it does nothing (the lock owns the hinge limits).
/// Put on the door leaf (HingeJoint + Rigidbody), with DoorHandle on its handles.
/// </summary>
[RequireComponent(typeof(HingeJoint), typeof(Rigidbody))]
public class DoorLatch : MonoBehaviour
{
    [SerializeField] private float closedTolerance = 1.5f;
    [SerializeField] private float latchWiggle = 1f;
    [Tooltip("After the lever is pressed, the latch stays open this long (s) so you can let go of the press while pulling.")]
    [SerializeField] private float graceTime = 0.6f;
    [SerializeField] private AudioClip latchSound;
    [SerializeField] private AudioClip unlatchSound;

    [Header("Haptics")]
    [SerializeField] private float unlatchHaptic = 0.55f;
    [SerializeField] private float latchHaptic = 0.4f;
    [SerializeField] private float swingHapticScale = 0.12f;   // amplitude per rad/s of swing
    [SerializeField] private float maxSwingHaptic = 0.25f;
    [SerializeField] private float swingHapticInterval = 0.08f;

    public bool IsLatched { get; private set; }

    private HingeJoint _hinge;
    private Rigidbody _rb;
    private LockableDoor _lock;
    private DoorHandle[] _handles;
    private JointLimits _fallbackOpen;
    private bool _init;
    private float _nextSwingHaptic;
    private float _lastPressTime = -10f;

    private void Awake()
    {
        _hinge = GetComponent<HingeJoint>();
        _rb = GetComponent<Rigidbody>();
        _lock = GetComponent<LockableDoor>();
        _handles = GetComponentsInChildren<DoorHandle>();
        _fallbackOpen = _hinge.limits;
    }

    private JointLimits OpenLimits => _lock != null ? _lock.OpenLimits : _fallbackOpen;
    private float ClosedAngle => _lock != null ? _lock.ClosedAngle : 0f;

    private void FixedUpdate()
    {
        if (_lock != null && _lock.IsLocked) { _init = false; return; } // lock owns the limits
        if (_rb.isKinematic) return;                                     // wedged open / frozen

        bool atClosed = Mathf.Abs(_hinge.angle - ClosedAngle) <= closedTolerance;
        if (AnyPressed()) _lastPressTime = Time.time;
        bool recentlyPressed = Time.time - _lastPressTime <= graceTime;
        bool wantLatched = atClosed && !recentlyPressed;
        if (!_init || wantLatched != IsLatched)
        {
            SetLatched(wantLatched, _init);
            _init = true;
        }
    }

    private void Update()
    {
        if (IsLatched || _rb.isKinematic || Time.time < _nextSwingHaptic) return;
        Vector3 axis = transform.TransformDirection(_hinge.axis).normalized;
        float w = Mathf.Abs(Vector3.Dot(_rb.angularVelocity, axis)); // rad/s
        if (w < 0.05f) return;
        _nextSwingHaptic = Time.time + swingHapticInterval;
        float amp = Mathf.Min(maxSwingHaptic, w * swingHapticScale);
        foreach (var h in _handles) if (h != null) XRHaptics.PulseHolders(h.Grab, amp, swingHapticInterval * 0.8f);
    }

    private bool AnyPressed()
    {
        foreach (var h in _handles) if (h != null && h.IsPressed) return true;
        return false;
    }

    private void SetLatched(bool latched, bool feedback)
    {
        IsLatched = latched;
        var open = OpenLimits;
        if (latched)
        {
            float closed = ClosedAngle;
            float openDir = Mathf.Sign((open.min + open.max) * 0.5f - closed);
            float a = closed, b = closed + openDir * latchWiggle;
            var l = open;
            l.min = Mathf.Min(a, b);
            l.max = Mathf.Max(a, b);
            _hinge.limits = l;
        }
        else
        {
            _hinge.limits = open;
        }
        _hinge.useLimits = true;
        _rb.WakeUp();

        if (!feedback) return;
        var clip = latched ? (latchSound != null ? latchSound : ProceduralSfx.Thunk)
                           : (unlatchSound != null ? unlatchSound : ProceduralSfx.Tick);
        SfxMix.PlayAt(clip, transform.position, latched ? 0.75f : 0.6f);
        foreach (var h in _handles)
            if (h != null) XRHaptics.PulseHolders(h.Grab, latched ? latchHaptic : unlatchHaptic, 0.08f);
    }
}
