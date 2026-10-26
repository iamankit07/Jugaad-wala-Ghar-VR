using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Driving. Sitting in the car with the engine running:
/// - LEFT TRIGGER = accelerator (analog),
/// - RIGHT TRIGGER = brake; keep holding it once the car has stopped and it goes into reverse
///   (then right trigger drives backwards and left trigger brakes; hold left at a standstill = forward again),
/// - steer by grabbing the steering wheel (GRIP) and turning it.
/// Slow, garage speeds (VR comfort). The car stops dead at walls/shelves/the closed shutter and at
/// the edge of the ground, the player rides along, the wheels roll and the front ones steer, and the
/// engine note rises with speed. Won't drive with the jack under it, the bonnet up, or a wheel not
/// bolted on. Can't get out while it's moving.
/// Put it on the car (with CarLift).
/// </summary>
public class CarDrive : MonoBehaviour
{
    [Header("Car")]
    [SerializeField] private CarLift lift;
    [SerializeField] private CarEngine engine;
    [SerializeField] private CarSeat seat;
    [SerializeField] private SteeringWheel steering;
    [SerializeField] private ScissorJack jack;
    [SerializeField] private HoodLift hood;
    [SerializeField] private TyreChange tyre;
    [Tooltip("The un-mirrored right-rear corner (its wheel is on the hub).")]
    [SerializeField] private Transform corner;

    [Header("Wheels (visual)")]
    [SerializeField] private Transform frontLeft;
    [SerializeField] private Transform frontRight;
    [SerializeField] private Transform rearLeft;
    [Tooltip("Spin with the right-rear wheel (children of the corner): nuts, studs, the hub mount.")]
    [SerializeField] private Transform[] rearRightSpin;
    [SerializeField] private float wheelRadius = 0.308f;
    [Tooltip("Car-local Z of the front / rear axles.")]
    [SerializeField] private float frontAxleZ = 1.375f;
    [SerializeField] private float rearAxleZ = -1.239f;

    [Header("Driving")]
    [SerializeField] private float maxForward = 3.0f;
    [SerializeField] private float maxReverse = 1.4f;
    [SerializeField] private float maxWithFlat = 1.0f;
    [SerializeField] private float acceleration = 1.6f;
    [SerializeField] private float brakeDecel = 4.5f;
    [SerializeField] private float coastDecel = 0.7f;
    [SerializeField] private float reverseHold = 0.35f;
    [SerializeField] private float maxSteer = 32f;
    [SerializeField] private float steerRatio = 14f;

    [Header("Body (car-local, for bumping into things)")]
    [SerializeField] private Vector3 bodyCentre = new Vector3(0f, 0.39f, 0f);
    [SerializeField] private Vector3 bodyHalfSize = new Vector3(0.82f, 0.62f, 2.2f);
    [SerializeField] private float heavyMass = 5f;

    [Header("Input")]
    [SerializeField] private string gasBinding = "<XRController>{LeftHand}/trigger";
    [SerializeField] private string brakeBinding = "<XRController>{RightHand}/trigger";

    [Header("Feedback")]
    [SerializeField] private AudioClip bumpClip;
    [SerializeField] private TaskHUD hud;

    /// <summary>m/s, + forwards, - backwards.</summary>
    public float Speed => _speed;
    public bool Reversing => _gear < 0;

    private InputAction _gas, _brake;
    private float _speed, _hold, _spin, _nextToast, _steerVis;
    private int _gear = 1;
    private Transform _origin;
    private Quaternion _flBase, _frBase, _rlBase;
    private Quaternion[] _rrBase;
    private float _mirror = 1f;
    private bool _flatToldThisDrive;
    private readonly HashSet<Collider> _already = new HashSet<Collider>();
    private readonly Collider[] _hits = new Collider[64];

    private void Awake()
    {
        if (hud == null) hud = FindFirstObjectByType<TaskHUD>();
        var xr = FindFirstObjectByType<XROrigin>();
        if (xr != null) _origin = xr.transform;
        _mirror = Mathf.Sign(transform.lossyScale.x);
        if (frontLeft) _flBase = frontLeft.localRotation;
        if (frontRight) _frBase = frontRight.localRotation;
        if (rearLeft) _rlBase = rearLeft.localRotation;
        _rrBase = new Quaternion[rearRightSpin != null ? rearRightSpin.Length : 0];
        for (int i = 0; i < _rrBase.Length; i++) if (rearRightSpin[i]) _rrBase[i] = rearRightSpin[i].localRotation;
        _gas = new InputAction("CarGas", InputActionType.Value, gasBinding);
        _brake = new InputAction("CarBrake", InputActionType.Value, brakeBinding);
#if UNITY_EDITOR
        _gas.AddBinding("<Keyboard>/upArrow");                    // desk testing without the headset
        _brake.AddBinding("<Keyboard>/downArrow");
#endif
    }

    private void OnEnable() { _gas.Enable(); _brake.Enable(); }
    private void OnDisable() { _gas.Disable(); _brake.Disable(); }

    /// <summary>Why the car won't drive right now (null = it will).</summary>
    public string BlockReason()
    {
        if (seat == null || !seat.Seated) return null;
        if (engine == null || !engine.Started) return GameLanguage.Pick("Start the engine first - turn the key.", "Pehle gaadi start karo - chaabi ghumao.");
        if (jack != null && jack.Placed) return GameLanguage.Pick("Take the jack out from under the car first!", "Pehle gaadi ke neeche se jack hatao!");
        if (hood != null && hood.Angle > 5f) return GameLanguage.Pick("Close the bonnet first - you can't see!", "Pehle bonnet band karo - aage kuch dikhega nahi!");
        if (tyre != null) { string why = tyre.DriveBlockReason(); if (why != null) return why; }
        return null;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        bool seated = seat != null && seat.Seated;
        float gas = seated ? _gas.ReadValue<float>() : 0f;
        float brk = seated ? _brake.ReadValue<float>() : 0f;
        string why = BlockReason();
        bool canDrive = seated && why == null;

        if (seated && why != null && gas > 0.3f && Time.time > _nextToast)   // (right trigger also turns the key - don't nag about that)
        {
            _nextToast = Time.time + 4f;
            if (hud != null) hud.ShowToast(why, hud.WarnColor);
        }
        if (!canDrive) { gas = 0f; brk = 1f; }

        // ---- pedals ----
        float push = 0f, stop = coastDecel;
        if (_gear > 0)
        {
            push = gas * acceleration;
            stop = brk * brakeDecel + coastDecel * (1f - gas);
            if (canDrive && _speed <= 0.02f && brk > 0.2f && gas < 0.1f) { _hold += dt; if (_hold > reverseHold) { _gear = -1; _hold = 0f; Toast(GameLanguage.Pick("Reverse", "Reverse gear"), true); } }
            else _hold = 0f;
        }
        else
        {
            push = -brk * acceleration * 0.7f;
            stop = gas * brakeDecel + coastDecel * (1f - brk);
            if (canDrive && _speed >= -0.02f && gas > 0.2f && brk < 0.1f) { _hold += dt; if (_hold > reverseHold) { _gear = 1; _hold = 0f; } }
            else _hold = 0f;
        }
        float top = tyre != null && tyre.FlatOn ? maxWithFlat : maxForward;
        _speed += push * dt;
        _speed = Mathf.MoveTowards(_speed, 0f, stop * dt);
        _speed = _gear > 0 ? Mathf.Clamp(_speed, 0f, top) : Mathf.Clamp(_speed, -Mathf.Min(maxReverse, top), 0f);

        if (canDrive && tyre != null && tyre.FlatOn && Mathf.Abs(_speed) > 0.3f && !_flatToldThisDrive)
        {
            _flatToldThisDrive = true;
            Toast(GameLanguage.Pick("Driving on a flat tyre - go easy!", "Puncture tyre pe chala rahe ho - dheere!"), false);
        }

        // ---- steering ----
        float steer = steering != null ? Mathf.Clamp(steering.Angle / steerRatio, -maxSteer, maxSteer) : 0f;
        if (steering != null && Mathf.Abs(_speed) > 0.05f) steering.Recentre(Mathf.Abs(_speed) * 40f * dt);

        // ---- move ----
        if (Mathf.Abs(_speed) > 0.0005f && lift != null) Move(_speed * dt, steer);

        // ---- the rest ----
        if (seat != null) seat.CanUse = Mathf.Abs(_speed) < 0.05f;
        _steerVis = Mathf.MoveTowards(_steerVis, steer, 90f * dt);
        Wheels();
        if (engine != null) engine.SetDrive(Mathf.Abs(_speed) / maxForward, _gear > 0 ? gas : brk);
    }

    private void Move(float distance, float steerDeg)
    {
        Quaternion heading = lift.LevelRotation;
        Vector3 fwd = heading * Vector3.forward;
        Vector3 rearAxle = lift.LevelPosition + fwd * rearAxleZ;
        float wheelBase = Mathf.Max(0.5f, frontAxleZ - rearAxleZ);
        float yaw = distance * Mathf.Tan(steerDeg * Mathf.Deg2Rad) / wheelBase * Mathf.Rad2Deg;
        Quaternion q = Quaternion.AngleAxis(yaw, Vector3.up);
        Vector3 move = (q * fwd) * distance;

        // where the body would end up
        Vector3 newLevel = rearAxle + q * (lift.LevelPosition - rearAxle) + move;
        Quaternion newRot = q * heading;
        if (Blocked(newLevel, newRot, heading))
        {
            if (Mathf.Abs(_speed) > 0.4f)
            {
                SfxMix.PlayAt(bumpClip != null ? bumpClip : ProceduralSfx.Thunk, transform.position + (q * fwd) * Mathf.Sign(_speed) * 2f, 0.9f, 0.05f);
                XRHaptics.PulseNear(transform.position, 3f, 0.8f, 0.15f);
            }
            _speed = 0f;
            return;
        }

        lift.Drive(rearAxle, yaw, move);
        if (_origin != null && seat != null && seat.Seated)
        {
            _origin.SetPositionAndRotation(rearAxle + q * (_origin.position - rearAxle) + move, q * _origin.rotation);
        }
        _spin += distance / wheelRadius * Mathf.Rad2Deg;
    }

    // would the body at this pose run into something solid, or leave the ground?
    private bool Blocked(Vector3 levelPos, Quaternion rot, Quaternion nowRot)
    {
        Vector3 c = levelPos + rot * bodyCentre;
        Vector3 cNow = lift.LevelPosition + nowRot * bodyCentre;
        _already.Clear();
        int nNow = Physics.OverlapBoxNonAlloc(cNow, bodyHalfSize, _hits, nowRot, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < nNow; i++) _already.Add(_hits[i]);       // stuck in it already: don't trap the car
        int n = Physics.OverlapBoxNonAlloc(c, bodyHalfSize, _hits, rot, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var col = _hits[i];
            if (_already.Contains(col) || Ignore(col)) continue;
            return true;
        }
        // all four corners must be over ground
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 p = levelPos + rot * new Vector3(sx * bodyHalfSize.x, 0f, sz * bodyHalfSize.z);
                p.y = 0.4f;
                if (!Physics.Raycast(p, Vector3.down, 0.8f, ~0, QueryTriggerInteraction.Ignore)) return true;
            }
        return false;
    }

    private bool Ignore(Collider col)
    {
        Transform t = col.transform;
        if (t.IsChildOf(transform)) return true;
        if (corner != null && t.IsChildOf(corner)) return true;
        if (_origin != null && t.IsChildOf(_origin)) return true;
        if (jack != null && t.IsChildOf(jack.transform)) return true;
        var rb = col.attachedRigidbody;
        if (rb != null)
        {
            if (rb.mass < heavyMass) return true;                       // small props: drive through them
            var g = rb.GetComponent<XRGrabInteractable>();
            if (g != null && g.isSelected) return true;
        }
        return false;
    }

    private void Wheels()
    {
        Quaternion spin = Quaternion.AngleAxis(_spin, Vector3.right);
        Quaternion steerQ = Quaternion.AngleAxis(_steerVis * _mirror, Vector3.up);   // the car is mirrored: a local yaw shows the other way
        if (frontLeft) frontLeft.localRotation = steerQ * _flBase * spin;
        if (frontRight) frontRight.localRotation = steerQ * _frBase * spin;
        if (rearLeft) rearLeft.localRotation = _rlBase * spin;
        for (int i = 0; i < _rrBase.Length; i++)
            if (rearRightSpin[i]) rearRightSpin[i].localRotation = Quaternion.AngleAxis(_spin, Vector3.right) * _rrBase[i];
    }

    private void Toast(string msg, bool good)
    {
        if (hud != null) hud.ShowToast(msg, good ? hud.GoodColor : hud.WarnColor, false);
    }

    // tests
    public void DriveFromCode(float distance, float steerDeg) { Move(distance, steerDeg); Wheels(); }
}
