using System.Collections;
using UnityEngine;

/// <summary>
/// Tool bag out of reach on a high shelf - a real physics object. Push or drag it with the broom
/// (XRI Kinematic movement, so the broom shoves it properly) until it slides off the edge and
/// tumbles down. When it hits something low (floor, bora, pallet) it bursts open: the closed look
/// becomes the open one, settled upright and turned towards the player, and what was inside is
/// released into the room.
/// Put it on the bag (needs a Rigidbody + collider).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ToolBagDrop : MonoBehaviour
{
    [SerializeField] private GameObject closedLook;
    [SerializeField] private GameObject openLook;
    [Tooltip("What's inside (children of the bag, posed round the open bag's mouth). Hidden until it lands, then released into the room.")]
    [SerializeField] private GameObject[] spillItems;
    [SerializeField] private Transform spillParent;
    [Tooltip("Bag-local direction that should end up facing the player when it opens (where the rubber band hangs).")]
    [SerializeField] private Vector3 frontLocal = Vector3.left;
    [Header("Landing")]
    [Tooltip("It only bursts open when it hits something below this height.")]
    [SerializeField] private float burstBelowY = 1.2f;
    [SerializeField] private float minImpactSpeed = 1.2f;
    [SerializeField] private float settleTime = 0.15f;
    [Header("Feedback")]
    [SerializeField] private AudioClip slideSound;
    [SerializeField] private AudioClip thudSound;

    public bool Fallen { get; private set; }
    public bool Landed { get; private set; }

    private Rigidbody _rb;
    private Collider _col;
    private float _startY, _bottomOffset, _restTimer, _nextScrape;
    private Vector3 _lastPos;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _col = GetComponent<Collider>();
        if (openLook != null) openLook.SetActive(false);
        if (spillItems != null) foreach (var s in spillItems) if (s != null) s.SetActive(false);
        _startY = transform.position.y;
        _bottomOffset = transform.position.y - BoundsOf(gameObject).min.y;
        _lastPos = transform.position;
    }

    private void FixedUpdate()
    {
        if (Landed) return;
        Vector3 p = transform.position;
        // scraping along the shelf: a soft rustle now and then
        if (!Fallen && (p - _lastPos).sqrMagnitude > 0.0004f && Time.time >= _nextScrape)
        {
            _nextScrape = Time.time + 0.45f;
            SfxMix.PlayAt(slideSound != null ? slideSound : ProceduralSfx.Tick, p, 0.35f, 0.15f);
        }
        _lastPos = p;

        if (!Fallen && p.y < _startY - 0.25f)
        {
            Fallen = true;
            Debug.Log("[ToolBagDrop] bag went over the edge");
        }

        // fallback: it came to rest somewhere low without a hard hit (slid down slowly)
        if (Fallen && p.y < burstBelowY + _bottomOffset && _rb.linearVelocity.sqrMagnitude < 0.01f)
        {
            _restTimer += Time.fixedDeltaTime;
            if (_restTimer > 0.4f) StartCoroutine(Burst());
        }
        else _restTimer = 0f;
    }

    private void OnCollisionEnter(Collision c)
    {
        if (Landed || !Fallen || c.contactCount == 0) return;
        if (c.GetContact(0).point.y > burstBelowY) return;
        if (c.relativeVelocity.magnitude < minImpactSpeed) return;
        StartCoroutine(Burst());
    }

    private IEnumerator Burst()
    {
        if (Landed) yield break;
        Landed = true;
        _rb.isKinematic = true;

        // ground under it
        Vector3 c = _col != null ? _col.bounds.center : transform.position;
        float groundY = 0f;
        bool was = _col != null && _col.enabled;
        if (_col != null) _col.enabled = false;
        RaycastHit hit;
        if (Physics.Raycast(c + Vector3.up * 0.2f, Vector3.down, out hit, 3f, ~0, QueryTriggerInteraction.Ignore)) groundY = hit.point.y;
        if (_col != null) _col.enabled = was;

        // upright, front turned to the player
        Vector3 toPlayer = Vector3.left;
        var cam = Camera.main;
        if (cam != null) { toPlayer = cam.transform.position - c; toPlayer.y = 0f; }
        Vector3 f = frontLocal; f.y = 0f;
        Quaternion target = Quaternion.LookRotation(toPlayer.sqrMagnitude > 1e-4f ? toPlayer.normalized : Vector3.left)
                            * Quaternion.Inverse(Quaternion.LookRotation(f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.left));
        Vector3 endPos = new Vector3(c.x, groundY + _bottomOffset, c.z);
        endPos = PushOutOfStuff(endPos, target);

        if (closedLook != null) closedLook.SetActive(false);
        if (openLook != null) openLook.SetActive(true);
        SfxMix.PlayAt(thudSound != null ? thudSound : ProceduralSfx.Thunk, endPos, 0.9f, 0.05f);
        XRHaptics.PulseNear(endPos, 1.5f, 0.4f, 0.1f);

        Vector3 p0 = transform.position; Quaternion r0 = transform.rotation;
        for (float k = 0f; k < 1f; k += Time.deltaTime / Mathf.Max(0.01f, settleTime))
        {
            float e = 1f - (1f - k) * (1f - k);
            transform.SetPositionAndRotation(Vector3.Lerp(p0, endPos, e), Quaternion.Slerp(r0, target, e));
            yield return null;
        }
        transform.SetPositionAndRotation(endPos, target);

        if (spillItems != null)
            foreach (var s in spillItems)
            {
                if (s == null) continue;
                s.transform.SetParent(spillParent, true);   // rode down with the bag, now its own thing
                s.SetActive(true);
            }
        Debug.Log("[ToolBagDrop] bag landed and opened");

        // down on the floor it's just a bag: pick it up, carry it, drop it
        if (GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>() == null)
        {
            var g = gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            g.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Instantaneous;
            g.useDynamicAttach = true;
            gameObject.AddComponent<KinematicUntilGrabbed>();
            if (GetComponent<ResetIfFallen>() == null) gameObject.AddComponent<ResetIfFallen>();
        }
    }

    // Standing it up upright can put it inside a wall/barrel it landed against - slide it out sideways.
    private Vector3 PushOutOfStuff(Vector3 pos, Quaternion rot)
    {
        var box = _col as BoxCollider;
        if (box == null) return pos;
        Vector3 half = Vector3.Scale(box.size, transform.lossyScale) * 0.5f;
        for (int it = 0; it < 4; it++)
        {
            bool moved = false;
            Vector3 centre = pos + rot * Vector3.Scale(box.center, transform.lossyScale);
            foreach (var other in Physics.OverlapBox(centre, half * 0.98f, rot, ~0, QueryTriggerInteraction.Ignore))
            {
                if (other == _col || other.transform.IsChildOf(transform)) continue;
                if (other.attachedRigidbody != null && !other.attachedRigidbody.isKinematic) continue; // small props get shoved by physics anyway
                Vector3 dir; float dist;
                if (Physics.ComputePenetration(_col, pos, rot, other, other.transform.position, other.transform.rotation, out dir, out dist))
                {
                    dir.y = 0f;
                    if (dir.sqrMagnitude < 1e-6f) continue;
                    pos += dir.normalized * (dist + 0.005f);
                    moved = true;
                }
            }
            if (!moved) break;
        }
        return pos;
    }

    private static Bounds BoundsOf(GameObject go)
    {
        var b = new Bounds(go.transform.position, Vector3.zero);
        bool init = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!init) { b = r.bounds; init = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }
}
