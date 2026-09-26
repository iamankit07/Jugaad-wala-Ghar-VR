using UnityEngine;

/// <summary>
/// Makes a physics (HingeJoint) door swing cleanly: the leaf and its handles stop colliding
/// with the STATIC room geometry around the hinge (floor, walls, frame). The hinge limits
/// already stop the door, and rubbing against the floor/frame caused heavy friction and
/// could push the door through its closed limit. Dynamic props (books, pots) still collide,
/// so the door can still be wedged, and the player's CharacterController is still blocked.
/// </summary>
[RequireComponent(typeof(HingeJoint))]
public class DoorPhysicsHelper : MonoBehaviour
{
    [Tooltip("Static colliders within this radius of the hinge line are ignored by the door.")]
    [SerializeField] private float ignoreRadius = 1.05f;
    [SerializeField] private float doorHeight = 2.3f;
    [Tooltip("Speed cap (rad/s) for the leaf and handles - stops a hard yank from tunnelling through the limit.")]
    [SerializeField] private float maxAngularSpeed = 6f;
    [SerializeField] private int solverIterations = 20;

    private void Start()
    {
        var hinge = GetComponent<HingeJoint>();
        Vector3 anchor = transform.TransformPoint(hinge.anchor);
        Vector3 bottom = new Vector3(anchor.x, anchor.y - doorHeight, anchor.z);
        Vector3 top = new Vector3(anchor.x, anchor.y + 0.1f, anchor.z);

        var mine = GetComponentsInChildren<Collider>();
        var near = Physics.OverlapCapsule(bottom, top, ignoreRadius, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        int pairs = 0;
        foreach (var other in near)
        {
            if (other == null || other.attachedRigidbody != null) continue; // only static geometry
            if (other.transform.IsChildOf(transform)) continue;
            foreach (var c in mine)
            {
                if (c == null || c.isTrigger) continue;
                Physics.IgnoreCollision(c, other, true);
                pairs++;
            }
        }

        foreach (var rb in GetComponentsInChildren<Rigidbody>())
        {
            rb.maxAngularVelocity = maxAngularSpeed;
            rb.solverIterations = solverIterations;
            rb.solverVelocityIterations = solverIterations / 2;
        }

        var limits = hinge.limits;
        limits.bounciness = 0f;
        hinge.limits = limits;
        hinge.useLimits = true;
        Debug.Log("[DoorPhysicsHelper] " + transform.parent.name + ": ignoring " + pairs + " static collision pairs");
    }
}
