using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Self-closing door that can be 'wedged' open.
/// - Self close: while nobody holds a handle, a gentle, capped torque swings the door shut
///   (scripted on purpose: the PhysX hinge spring was unstable and could slam through the limit).
/// - Wedge: once Armed, if the door is open more than minOpenAngle and touches a loose grabbable
///   object (book, pot...) that nobody is holding, the door freezes and stays open for good.
/// Put it on the door leaf (HingeJoint + Rigidbody). Turn the HingeJoint's own spring OFF.
/// </summary>
[RequireComponent(typeof(HingeJoint), typeof(Rigidbody))]
public class DoorWedge : MonoBehaviour
{
    [Header("Wedge")]
    [SerializeField] private float minOpenAngle = 30f;
    [SerializeField] private AudioClip wedgeSound;
    public UnityEvent onWedged;

    [Header("Self closing")]
    [SerializeField] private bool selfClose = true;
    [Tooltip("Torque per radian of opening (N·m/rad).")]
    [SerializeField] private float closeStrength = 2.5f;
    [Tooltip("Torque per rad/s of swing speed - eases the door in instead of slamming.")]
    [SerializeField] private float closeDamping = 2.5f;
    [SerializeField] private float maxCloseTorque = 3f;
    [SerializeField] private float closedDeadZone = 1.5f;

    /// <summary>Only detects wedging while armed (the game flow arms it when the task starts).</summary>
    public bool Armed { get; set; }
    public bool IsWedged { get; private set; }
    public Collider WedgedBy { get; private set; }

    private HingeJoint _hinge;
    private Rigidbody _rb;
    private XRGrabInteractable[] _handles;

    private void Awake()
    {
        _hinge = GetComponent<HingeJoint>();
        _rb = GetComponent<Rigidbody>();
        _handles = GetComponentsInChildren<XRGrabInteractable>();
        _hinge.useSpring = false; // scripted closing instead
    }

    private void FixedUpdate()
    {
        if (!selfClose || IsWedged || _rb.isKinematic) return;
        if (AnyHandleHeld()) return;

        // NOTE: HingeJoint.velocity is unreliable here, so read the swing speed from the body itself.
        Vector3 axis = transform.TransformDirection(_hinge.axis).normalized;
        float angle = _hinge.angle;                                  // degrees
        float velRad = Vector3.Dot(_rb.angularVelocity, axis);       // rad/s around the hinge
        if (Mathf.Abs(angle) < closedDeadZone && Mathf.Abs(velRad) < 0.1f) return;

        float torque = -closeStrength * angle * Mathf.Deg2Rad - closeDamping * velRad;
        torque = Mathf.Clamp(torque, -maxCloseTorque, maxCloseTorque);
        _rb.AddTorque(axis * torque, ForceMode.Force);
    }

    private bool AnyHandleHeld()
    {
        if (_handles == null) return false;
        foreach (var h in _handles) if (h != null && h.isSelected) return true;
        return false;
    }

    private void OnCollisionEnter(Collision c) { Check(c); }
    private void OnCollisionStay(Collision c) { Check(c); }

    private void Check(Collision c)
    {
        if (!Armed || IsWedged) return;
        if (Mathf.Abs(_hinge.angle) < minOpenAngle) return;

        var other = c.rigidbody;                          // the other body's rigidbody
        if (other == null || other.isKinematic) return;   // walls, floor, frame, hands
        if (other.transform.IsChildOf(transform)) return; // own handles / lock parts
        var joint = other.GetComponent<Joint>();
        if (joint != null && joint.connectedBody == _rb) return; // own handle even after it was un-parented by a grab

        var grab = other.GetComponentInParent<XRGrabInteractable>();
        if (grab == null || grab.isSelected) return;      // must be a loose prop, not held / socketed

        Wedge(c.collider);
    }

    /// <summary>Freeze the door open (also callable for testing).</summary>
    public void Wedge(Collider by)
    {
        if (IsWedged) return;
        IsWedged = true;
        WedgedBy = by;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.isKinematic = true;                           // stays open from now on
        var clip = wedgeSound != null ? wedgeSound : ProceduralSfx.Thunk;
        SfxMix.PlayAt(clip, transform.position, 1f);
        Debug.Log("[DoorWedge] " + name + " wedged open by " + (by ? by.name : "?") + " at " + _hinge.angle.ToString("F0") + " deg");
        onWedged?.Invoke();
    }
}
