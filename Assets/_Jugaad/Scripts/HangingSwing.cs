using UnityEngine;

/// <summary>
/// Something hanging off a pivot (a key ring on the car key): it keeps hanging down under gravity
/// and swings a little when its parent moves or turns, then settles.
/// Put it on the pivot; children hang along its local -Y.
/// </summary>
public class HangingSwing : MonoBehaviour
{
    [Tooltip("Stiffness of the swing back to hanging straight down.")]
    [SerializeField] private float stiffness = 60f;
    [Tooltip("How quickly the swing dies out.")]
    [SerializeField] private float damping = 5f;
    [Tooltip("Parent-local direction the hanging thing should keep facing (its forward), e.g. along the key.")]
    [SerializeField] private Vector3 faceAlongParent = Vector3.forward;
    [Tooltip("How much of the parent's turning is passed on as a nudge (friction at the hole).")]
    [SerializeField] private float kick = 4f;

    private Quaternion _rot;
    private Vector3 _angVel;          // rad/s, world
    private bool _init;
    private Quaternion _parentPrev;

    private void OnEnable() { _init = false; }

    private Quaternion Target()
    {
        Vector3 along = transform.parent != null ? transform.parent.TransformDirection(faceAlongParent) : Vector3.forward;
        along = Vector3.ProjectOnPlane(along, Vector3.up);
        if (along.sqrMagnitude < 1e-6f) along = Vector3.forward;
        return Quaternion.LookRotation(along.normalized, Vector3.up);   // local -Y = straight down
    }

    private void LateUpdate()
    {
        Quaternion target = Target();
        Quaternion parentRot = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
        if (!_init) { _rot = target; _angVel = Vector3.zero; _parentPrev = parentRot; _init = true; }
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f) { transform.rotation = _rot; return; }

        // the parent turning (key turned) drags the ring round a bit, then gravity swings it back
        (parentRot * Quaternion.Inverse(_parentPrev)).ToAngleAxis(out float pa, out Vector3 pAxis);
        _parentPrev = parentRot;
        if (pa > 180f) pa -= 360f;
        if (!float.IsNaN(pAxis.x) && Mathf.Abs(pa) > 0.01f) _angVel += pAxis.normalized * (pa * Mathf.Deg2Rad * kick);   // nudge in proportion to how far it turned

        // spring towards hanging straight down
        Quaternion diff = target * Quaternion.Inverse(_rot);
        diff.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        Vector3 err = float.IsNaN(axis.x) || axis.sqrMagnitude < 1e-8f ? Vector3.zero : axis.normalized * (angle * Mathf.Deg2Rad);
        _angVel += (err * stiffness - _angVel * damping) * dt;
        float a = _angVel.magnitude * dt;
        if (a > 1e-6f) _rot = Quaternion.AngleAxis(a * Mathf.Rad2Deg, _angVel.normalized) * _rot;
        transform.rotation = _rot;
    }
}
