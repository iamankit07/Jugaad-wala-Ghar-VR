using UnityEngine;

/// <summary>
/// Keeps a world-space HUD floating in front of the player, VR-comfortably:
/// it only re-centres when you turn more than yawThreshold, moves smoothly, faces you,
/// and comes closer (and scales down) if a wall is in the way so it never hides inside geometry.
/// </summary>
public class HudFollow : MonoBehaviour
{
    [SerializeField] private Transform head;          // defaults to Camera.main
    [SerializeField] private float distance = 1.4f;
    [SerializeField] private float heightOffset = -0.15f;
    [Tooltip("Degrees to the side of your view (negative = left) so the panel doesn't block the middle.")]
    [SerializeField] private float yawOffset = -22f;
    [SerializeField] private float yawThreshold = 25f;
    [SerializeField] private float followSpeed = 3f;
    [SerializeField] private float minDistance = 0.45f;
    [SerializeField] private float wallPadding = 0.08f;

    private Vector3 _anchorDir;
    private Vector3 _targetDir;
    private float _currentDist;
    private Vector3 _baseScale;
    private bool _init;
    private readonly RaycastHit[] _hits = new RaycastHit[16];

    private void Awake() { _baseScale = transform.localScale; }

    private void OnEnable() { _init = false; }

    private void LateUpdate()
    {
        if (head == null)
        {
            var cam = Camera.main;
            if (cam == null) return;
            head = cam.transform;
        }

        Vector3 fwd = Flat(head.forward);
        if (fwd.sqrMagnitude < 1e-4f) fwd = Flat(head.up);
        if (fwd.sqrMagnitude < 1e-4f) return;
        fwd.Normalize();

        if (!_init)
        {
            _anchorDir = _targetDir = fwd;
            _currentDist = distance;
            _init = true;
        }

        if (Vector3.Angle(_targetDir, fwd) > yawThreshold) _targetDir = fwd;
        _anchorDir = Vector3.Slerp(_anchorDir, _targetDir, 1f - Mathf.Exp(-followSpeed * Time.deltaTime)).normalized;

        Vector3 placeDir = Quaternion.Euler(0f, yawOffset, 0f) * _anchorDir;
        float wanted = WallClampedDistance(placeDir);
        // come closer fast (never sit inside a wall), go back out slowly
        float k = wanted < _currentDist ? 20f : 3f;
        _currentDist = Mathf.Lerp(_currentDist, wanted, 1f - Mathf.Exp(-k * Time.deltaTime));

        float s = _currentDist / distance;
        transform.position = head.position + placeDir * _currentDist + Vector3.up * heightOffset * s;
        Vector3 look = transform.position - head.position;
        if (look.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(look, Vector3.up);
        transform.localScale = _baseScale * s;
    }

    private float WallClampedDistance(Vector3 dir)
    {
        int n = Physics.RaycastNonAlloc(head.position, dir, _hits, distance + wallPadding,
                                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = distance;
        Transform root = head.root;
        for (int i = 0; i < n; i++)
        {
            var col = _hits[i].collider;
            if (col == null || col.transform.IsChildOf(root)) continue;             // our own rig / hands
            var rb = col.attachedRigidbody;
            if (rb != null && col.bounds.size.magnitude < 0.6f) continue;          // small props (held remote etc.)
            best = Mathf.Min(best, _hits[i].distance - wallPadding);
        }
        return Mathf.Max(minDistance, best);
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
