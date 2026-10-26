using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple, comfortable teleport: push the stick forward to aim an arc from the hand, let go to
/// teleport there (with a quick blink). Only floor spots at the player's own floor level with
/// room to stand are valid - not walls, tables, beds or inside furniture. Pull the stick back to cancel.
/// </summary>
public class ArcTeleporter : MonoBehaviour
{
    [SerializeField] private InputActionReference stick;     // e.g. XRI Left Locomotion/Move
    [SerializeField] private Transform aimFrom;               // the hand / controller
    [SerializeField] private XROrigin origin;

    [Header("Arc")]
    [SerializeField] private float launchSpeed = 7.5f;
    [SerializeField] private float gravity = 9.81f;
    [SerializeField] private float timeStep = 0.03f;
    [SerializeField] private int maxSteps = 60;
    [SerializeField] private LayerMask mask = Physics.DefaultRaycastLayers;

    [Header("Valid spot")]
    [SerializeField] private float maxFloorStep = 0.25f;     // how far above/below the current floor
    [SerializeField] private float minNormalY = 0.85f;       // flat enough to stand on
    [SerializeField] private float playerRadius = 0.2f;

    [Header("Look")]
    [SerializeField] private Material arcMaterial;
    [SerializeField] private Material blinkMaterial;          // always-on-top material for the fade
    [SerializeField] private Color validColor = new Color(0.25f, 0.85f, 0.75f, 0.9f);
    [SerializeField] private Color invalidColor = new Color(0.95f, 0.35f, 0.3f, 0.7f);
    [SerializeField] private float blinkTime = 0.1f;

    private LineRenderer _line;
    private Renderer _marker;
    private Renderer _blink;
    private CharacterController _cc;
    private bool _aiming, _hasTarget, _busy;
    private ITeleportBlocker _blockedBy;
    private Vector3 _blockedAt;

    /// <summary>Something that forbids teleporting onto part of the floor (oil spill...).</summary>
    public interface ITeleportBlocker
    {
        bool BlocksTeleport(Vector3 point);
        /// <summary>The player let go of the stick while aiming at a blocked spot.</summary>
        void OnTeleportBlocked(Vector3 point);
    }
    public static readonly System.Collections.Generic.List<ITeleportBlocker> Blockers = new System.Collections.Generic.List<ITeleportBlocker>();
    private Vector3 _target;
    private readonly Vector3[] _points = new Vector3[128];
    private readonly RaycastHit[] _hits = new RaycastHit[8];
    private readonly Collider[] _overlap = new Collider[16];

    private void Awake()
    {
        if (origin == null) origin = GetComponentInParent<XROrigin>();
        _cc = origin ? origin.GetComponent<CharacterController>() : null;
        BuildVisuals();
        ShowVisuals(false);
    }

    private void OnDisable()
    {
        _aiming = false;
        ShowVisuals(false);
    }

    private void BuildVisuals()
    {
        var lineGo = new GameObject("TeleportArc");
        lineGo.transform.SetParent(transform, false);
        _line = lineGo.AddComponent<LineRenderer>();
        _line.useWorldSpace = true;
        _line.widthMultiplier = 0.012f;
        _line.numCapVertices = 4;
        _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _line.receiveShadows = false;
        if (arcMaterial) _line.sharedMaterial = arcMaterial;

        var m = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        m.name = "TeleportMarker";
        Destroy(m.GetComponent<Collider>());
        m.transform.SetParent(transform, false);
        m.transform.localScale = new Vector3(0.45f, 0.004f, 0.45f);
        _marker = m.GetComponent<Renderer>();
        _marker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (arcMaterial) _marker.material = new Material(arcMaterial);

        var cam = origin && origin.Camera ? origin.Camera.transform : Camera.main ? Camera.main.transform : null;
        if (cam && blinkMaterial)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "TeleportBlink";
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(cam, false);
            q.transform.localPosition = new Vector3(0f, 0f, 0.12f);
            q.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            q.transform.localScale = Vector3.one;
            _blink = q.GetComponent<Renderer>();
            _blink.material = new Material(blinkMaterial);
            _blink.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            SetBlink(0f);
        }
    }

private void Update()
    {
        if (_busy || stick == null || stick.action == null || aimFrom == null || origin == null) return;
        Vector2 v = stick.action.ReadValue<Vector2>();

        if (!_aiming)
        {
            if (v.y > 0.7f && Mathf.Abs(v.x) < 0.65f) { _aiming = true; ShowVisuals(true); }
            else return;
        }

        if (v.y < -0.5f) { _aiming = false; ShowVisuals(false); return; } // pull back to cancel

        if (v.magnitude < 0.25f) // released: preserve the destination before hiding visuals clears it
        {
            bool canTeleport = _hasTarget;
            var blocked = canTeleport ? null : _blockedBy;
            Vector3 blockedAt = _blockedAt;
            Vector3 destination = _target;
            _aiming = false;
            ShowVisuals(false);
            if (canTeleport) StartCoroutine(TeleportRoutine(destination));
            else if (blocked != null) blocked.OnTeleportBlocked(blockedAt);
            return;
        }

        UpdateArc();
    }

    private void UpdateArc()
    {
        Vector3 p = aimFrom.position;
        Vector3 vel = aimFrom.forward * launchSpeed;
        int count = 0;
        _points[count++] = p;
        _hasTarget = false;
        _blockedBy = null;
        bool hitSomething = false;
        RaycastHit hit = default;

        for (int i = 0; i < maxSteps && count < _points.Length; i++)
        {
            Vector3 next = p + vel * timeStep;
            vel += Vector3.down * gravity * timeStep;
            if (SegmentHit(p, next, out hit))
            {
                _points[count++] = hit.point;
                hitSomething = true;
                break;
            }
            _points[count++] = next;
            p = next;
        }

        _line.positionCount = count;
        _line.SetPositions(_points);

        bool valid = hitSomething && IsValid(hit);
        if (valid) { _hasTarget = true; _target = hit.point; }
        Color c = valid ? validColor : invalidColor;
        _line.startColor = c;
        _line.endColor = c;
        _marker.gameObject.SetActive(hitSomething);
        if (hitSomething)
        {
            _marker.transform.position = hit.point + Vector3.up * 0.01f;
            _marker.transform.rotation = Quaternion.identity;
            _marker.material.color = c;
        }
    }

    private bool SegmentHit(Vector3 a, Vector3 b, out RaycastHit best)
    {
        best = default;
        Vector3 d = b - a;
        float len = d.magnitude;
        if (len < 1e-5f) return false;
        int n = Physics.RaycastNonAlloc(a, d / len, _hits, len, mask, QueryTriggerInteraction.Ignore);
        float bestDist = float.MaxValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            var col = _hits[i].collider;
            if (col == null || IsRig(col)) continue;
            if (_hits[i].distance < bestDist) { bestDist = _hits[i].distance; best = _hits[i]; found = true; }
        }
        return found;
    }

    private bool IsValid(RaycastHit h)
    {
        if (h.normal.y < minNormalY) return false;                               // walls, slopes
        if (h.collider.attachedRigidbody != null) return false;                  // props, doors
        float floorY = origin ? origin.transform.position.y : 0f;
        if (Mathf.Abs(h.point.y - floorY) > maxFloorStep) return false;          // tables, beds

        Vector3 a = h.point + Vector3.up * (playerRadius + 0.05f);
        Vector3 b = h.point + Vector3.up * 1.6f;
        int n = Physics.OverlapCapsuleNonAlloc(a, b, playerRadius, _overlap, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var col = _overlap[i];
            if (col == null || IsRig(col)) continue;
            return false;                                                        // no room to stand
        }
        foreach (var blocker in Blockers)                                        // e.g. oil on the floor
            if (blocker != null && blocker.BlocksTeleport(h.point)) { _blockedBy = blocker; _blockedAt = h.point; return false; }
        return true;
    }

    private bool IsRig(Collider c) => origin != null && c.transform.IsChildOf(origin.transform);

    private IEnumerator TeleportRoutine(Vector3 point)
    {
        _busy = true;
        yield return Fade(0f, 1f);

        var cam = origin.Camera ? origin.Camera.transform : Camera.main.transform;
        Vector3 offset = cam.position - origin.transform.position;
        offset.y = 0f;
        Vector3 dest = new Vector3(point.x - offset.x, origin.transform.position.y, point.z - offset.z);
        if (_cc) _cc.enabled = false;
        origin.transform.position = dest;
        if (_cc) _cc.enabled = true;

        yield return Fade(1f, 0f);
        yield return new WaitForSeconds(0.15f);
        _busy = false;
    }

    /// <summary>Same blink as teleporting, for other scripts (getting into the car...). 1 = black.</summary>
    public IEnumerator FadeScreen(float from, float to, float time)
    {
        if (_blink == null) yield break;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            SetBlink(Mathf.Lerp(from, to, t / time));
            yield return null;
        }
        SetBlink(to);
    }

    private IEnumerator Fade(float from, float to)
    {
        if (_blink == null) yield break;
        float t = 0f;
        while (t < blinkTime)
        {
            t += Time.deltaTime;
            SetBlink(Mathf.Lerp(from, to, t / blinkTime));
            yield return null;
        }
        SetBlink(to);
    }

    private void SetBlink(float a)
    {
        if (_blink == null) return;
        _blink.enabled = a > 0.001f;
        var m = _blink.material;
        if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(0f, 0f, 0f, a));
    }

    private void ShowVisuals(bool on)
    {
        if (_line) _line.enabled = on;
        if (_marker) _marker.gameObject.SetActive(on && _hasTarget);
        if (!on) _hasTarget = false;
    }
}
