using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Makes a cabinet door or drawer move smoothly:
/// - the moving part (and its children, e.g. the handle) stops rubbing against the cabinet's own
///   static panels (the joint limits already stop it at closed / fully open),
/// - damping so it stays where the player leaves it instead of swinging or sliding on its own,
/// - a speed cap so a hard yank cannot tunnel through a limit,
/// - hinge doors only: a "magnetic catch" - when shut and nobody holds the handle, the door stays
///   shut, so things leaning on it from inside cannot push it open.
/// Items placed inside (books, pins) still collide normally with the part.
/// </summary>
public class CabinetPartPhysics : MonoBehaviour
{
    [Tooltip("Cabinet root whose static colliders this part should ignore. Empty = parent.")]
    [SerializeField] private Transform cabinetRoot;
    [SerializeField] private float linearDamping = 4f;
    [SerializeField] private float angularDamping = 3f;
    [SerializeField] private float maxAngularSpeed = 6f;
    [SerializeField] private int solverIterations = 15;

    [Header("Door catch (hinge doors only)")]
    [SerializeField] private bool magneticCatch = true;
    [Tooltip("Within this many degrees of closed (and not held) the door clicks shut and stays shut.")]
    [SerializeField] private float catchAngle = 8f;

    private HingeJoint _hinge;
    private Rigidbody _rb;
    private JointLimits _openLimits;
    private XRBaseInteractable[] _handles;
    private bool _latched;
    private bool _init;

    private void Start()
    {
        if (cabinetRoot == null) cabinetRoot = transform.parent;
        _rb = GetComponent<Rigidbody>();
        _hinge = GetComponent<HingeJoint>();
        if (_hinge != null) _openLimits = _hinge.limits;
        // grab references now: a grabbed handle is un-parented while held
        _handles = GetComponentsInChildren<XRBaseInteractable>(true);

        if (cabinetRoot != null)
        {
            var mine = GetComponentsInChildren<Collider>(true);
            int pairs = 0;
            foreach (var other in cabinetRoot.GetComponentsInChildren<Collider>(true))
            {
                if (other == null || other.isTrigger || other.attachedRigidbody != null) continue; // static panels only
                foreach (var c in mine)
                {
                    if (c == null || c.isTrigger) continue;
                    Physics.IgnoreCollision(c, other, true);
                    pairs++;
                }
            }
            Debug.Log("[CabinetPartPhysics] " + name + ": ignoring " + pairs + " cabinet collision pairs");
        }

        foreach (var rb in GetComponentsInChildren<Rigidbody>(true))
        {
            rb.linearDamping = linearDamping;
            rb.angularDamping = angularDamping;
            rb.maxAngularVelocity = maxAngularSpeed;
            rb.solverIterations = solverIterations;
            rb.solverVelocityIterations = Mathf.Max(1, solverIterations / 2);
        }
    }

    private void FixedUpdate()
    {
        if (_hinge == null || !magneticCatch) return;

        bool held = false;
        foreach (var h in _handles) if (h != null && h.isSelected) { held = true; break; }

        bool nearClosed = _hinge.angle - _openLimits.min < catchAngle;
        bool wantLatched = nearClosed && !held;
        if (_init && wantLatched == _latched) return;
        _init = true;
        _latched = wantLatched;

        var l = _openLimits;
        if (_latched) l.max = _openLimits.min + 0.5f;
        _hinge.limits = l;
        _hinge.useLimits = true;
        if (_rb) _rb.WakeUp();
    }
}
