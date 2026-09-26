using UnityEngine;

/// <summary>
/// Safety net for grabbable props: if the object ends up below the floor
/// (fell through, thrown out of the room), it is put back where it started.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ResetIfFallen : MonoBehaviour
{
    [Tooltip("If the object goes below this world Y, it is reset.")]
    [SerializeField] private float minY = -0.5f;

    [Tooltip("Optional: reset if the object goes this far from its start point (0 = off).")]
    [SerializeField] private float maxDistanceFromStart = 12f;

    private Rigidbody _rb;
    private Vector3 _startPos;
    private Quaternion _startRot;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _startPos = transform.position;
        _startRot = transform.rotation;
    }

    private void FixedUpdate()
    {
        bool tooLow = transform.position.y < minY;
        bool tooFar = maxDistanceFromStart > 0f &&
                      (transform.position - _startPos).sqrMagnitude > maxDistanceFromStart * maxDistanceFromStart;

        if (tooLow || tooFar)
            ResetToStart();
    }

    public void ResetToStart()
    {
        if (!_rb.isKinematic)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
        _rb.position = _startPos;
        _rb.rotation = _startRot;
        transform.SetPositionAndRotation(_startPos, _startRot);
    }
}
