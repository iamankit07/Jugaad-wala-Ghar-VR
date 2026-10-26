using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One wheel nut on its stud. Turned by the LugWrench: anticlockwise (seen from outside) undoes it -
/// it creeps out along the stud and after a third of a turn pops off and drops to the floor (when
/// PopAllowed). Clockwise screws it back home. When a wheel goes onto the hub the nuts that are off
/// fly back onto their studs, loose. Works any number of times.
/// Put it on the nut (child of the hub corner, local X = outwards along the stud).
/// </summary>
public class LugNut : MonoBehaviour
{
    public enum State { OnStud, Off, Returning }

    public static readonly List<LugNut> All = new List<LugNut>();

    [SerializeField] private float turnAngle = 120f;
    [SerializeField] private float travel = 0.015f;
    [SerializeField] private AudioClip[] dropClips;
    [SerializeField] private AudioClip tightClip;

    public State Current { get; private set; } = State.OnStud;
    /// <summary>0 = screwed home (tight), 1 = at the end of the stud.</summary>
    public float Progress { get; private set; }
    public bool IsTight => Current == State.OnStud && Progress <= 0.0001f;
    public bool IsOff => Current == State.Off;
    /// <summary>The wrench may turn it.</summary>
    public bool CanTurn { get; set; }
    /// <summary>Undoing it past the end lets it fall off (off while fitting a wheel, so a wrong turn doesn't drop it).</summary>
    public bool PopAllowed { get; set; } = true;
    /// <summary>Shown when the wrench goes on but it can't be turned yet.</summary>
    public string LockedHint { get; set; }
    public float TurnAngle => turnAngle;
    public Vector3 OuterFace => transform.position + transform.right * NutLength;
    public Vector3 Axis => transform.right;

    public const float NutLength = 0.02f;

    private Transform _mount;
    private Vector3 _seatPos;
    private Quaternion _seatRot;
    private Rigidbody _rb;
    private Collider _col;

    private void Awake()
    {
        _mount = transform.parent;
        _seatPos = transform.localPosition;
        _seatRot = transform.localRotation;
        _col = GetComponent<Collider>();
        if (_col != null) _col.enabled = false;
    }

    private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    /// <summary>Wrench turned it by this many degrees (positive = anticlockwise from outside = undo).
    /// Returns how much it really turned (0 at a stop).</summary>
    public float Turn(float degrees)
    {
        if (Current != State.OnStud || !CanTurn) return 0f;
        float before = Progress;
        Progress = Mathf.Clamp(Progress + degrees / turnAngle, 0f, 1f);
        float turned = (Progress - before) * turnAngle;
        Pose();
        if (before > 0.0001f && Progress <= 0.0001f) SeatHome();
        if (degrees > 0f && Progress >= 1f && PopAllowed) PopOff();
        return turned;
    }

    private void Pose()
    {
        transform.localPosition = _seatPos + _seatRot * Vector3.right * (Progress * travel);
        transform.localRotation = _seatRot * Quaternion.AngleAxis(Progress * turnAngle, Vector3.right);
    }

    private void PopOff()
    {
        Current = State.Off;
        transform.SetParent(null, true);
        if (_col != null) _col.enabled = true;
        _rb = GetComponent<Rigidbody>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
        _rb.mass = 0.05f;
        _rb.isKinematic = false;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.linearVelocity = Axis * 0.35f + Random.insideUnitSphere * 0.1f;
        _rb.angularVelocity = Random.insideUnitSphere * 6f;
        SfxMix.PlayAt(ProceduralSfx.Tick, transform.position, 0.6f, 0.1f);
        Debug.Log("[LugNut] " + name + " off");
    }

    private void SeatHome()
    {
        SfxMix.PlayAt(tightClip != null ? tightClip : ProceduralSfx.Thunk, transform.position, 0.7f, 0.05f);
        XRHaptics.PulseNear(transform.position, 0.6f, 0.5f, 0.08f);
        Debug.Log("[LugNut] " + name + " tight");
    }

    /// <summary>A wheel went on: fly back onto the stud, loose.</summary>
    public void ReturnLoose(float delay)
    {
        if (Current != State.Off) return;
        StartCoroutine(ReturnRoutine(delay));
    }

    private IEnumerator ReturnRoutine(float delay)
    {
        Current = State.Returning;
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (_rb != null) Destroy(_rb);
        _rb = null;
        if (_col != null) _col.enabled = false;
        yield return null;
        Vector3 p0 = transform.position; Quaternion r0 = transform.rotation;
        transform.SetParent(_mount, true);
        Progress = 1f;
        float time = 0.55f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / time)
        {
            Vector3 end = _mount.TransformPoint(_seatPos + _seatRot * Vector3.right * travel);
            Quaternion endRot = _mount.rotation * _seatRot * Quaternion.AngleAxis(turnAngle, Vector3.right);
            float e = t * t * (3f - 2f * t);
            Vector3 p = Vector3.Lerp(p0, end, e) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.25f;   // a little hop
            transform.SetPositionAndRotation(p, Quaternion.Slerp(r0, endRot, e));
            yield return null;
        }
        Pose();
        Current = State.OnStud;
        SfxMix.PlayAt(dropClips != null && dropClips.Length > 0 ? dropClips[0] : ProceduralSfx.Tick, transform.position, 0.4f, 0.1f);
    }

    private void OnCollisionEnter(Collision c)
    {
        if (Current != State.Off || dropClips == null || dropClips.Length == 0) return;
        if (c.relativeVelocity.magnitude < 0.4f) return;
        SfxMix.PlayAt(dropClips[Random.Range(0, dropClips.Length)], transform.position, Mathf.Clamp01(c.relativeVelocity.magnitude / 3f) * 0.6f, 0.1f);
    }

    // tests
    public void ForceOff() { if (Current == State.OnStud) { Progress = 1f; Pose(); PopOff(); } }
}
