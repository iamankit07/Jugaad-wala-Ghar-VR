using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Hand-held magnet. While held, pulls the nearest MagneticItem within range to its
/// pull point, then keeps it stuck with a FixedJoint. Grabbing the item with the other
/// hand detaches it.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
public class MagnetTool : MonoBehaviour
{
    [SerializeField] private Transform pullPoint;
    [SerializeField] private float pullRadius = 0.3f;
    [SerializeField] private float pullDuration = 0.3f;
    [SerializeField] private bool onlyWhileHeld = true;
    [SerializeField] private float regrabCooldown = 1.5f;
    [SerializeField] private AudioClip snapSound;

    private XRGrabInteractable _grab;
    private Rigidbody _body;
    private Collider[] _myColliders;

    private MagneticItem _pulling;
    private float _pullT;
    private Vector3 _pullStart;

    private MagneticItem _held;
    private FixedJoint _joint;

    public bool IsHolding { get { return _held != null; } }

    private void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _body = GetComponent<Rigidbody>();
        _myColliders = GetComponentsInChildren<Collider>();
        if (pullPoint == null) pullPoint = transform;
    }

    private void FixedUpdate()
    {
        if (_held != null)
        {
            if (_joint == null) Release(); // joint broke / removed
            return;
        }

        if (_pulling != null)
        {
            if (!_pulling.CanBePulledWhilePulling()) { CancelPull(); return; }
            _pullT += Time.fixedDeltaTime / Mathf.Max(0.01f, pullDuration);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_pullT));
            Vector3 centerOffset = _pulling.WorldCenter - _pulling.transform.position;
            Vector3 target = pullPoint.position - centerOffset;
            _pulling.Body.MovePosition(Vector3.Lerp(_pullStart, target, k));
            if (_pullT >= 1f) Attach(_pulling);
            return;
        }

        if (onlyWhileHeld && !_grab.isSelected) return;

        MagneticItem best = null;
        float bestDist = pullRadius;
        foreach (var item in MagneticItem.All)
        {
            if (item == null || !item.CanBePulled) continue;
            float d = Vector3.Distance(item.WorldCenter, pullPoint.position);
            if (d < bestDist) { bestDist = d; best = item; }
        }
        if (best != null) StartPull(best);
    }

    private void StartPull(MagneticItem item)
    {
        _pulling = item;
        _pullT = 0f;
        _pullStart = item.transform.position;
        item.AttachedTo = this;              // reserve it
        item.Body.isKinematic = true;        // glide through the gap, ignore furniture
        SetIgnore(item, true);
    }

    private void CancelPull()
    {
        if (_pulling == null) return;
        _pulling.Body.isKinematic = false;
        _pulling.AttachedTo = null;
        SetIgnore(_pulling, false);
        _pulling = null;
    }

    private void Attach(MagneticItem item)
    {
        _pulling = null;
        item.Body.isKinematic = false;
        item.Body.linearVelocity = Vector3.zero;
        item.Body.angularVelocity = Vector3.zero;
        _joint = item.gameObject.AddComponent<FixedJoint>();
        _joint.connectedBody = _body;
        _joint.enablePreprocessing = false;
        _held = item;
        item.AttachedTo = this;
        item.MarkExtracted();

        if (snapSound) SfxMix.PlayAt(snapSound, pullPoint.position, 0.8f);
        var input = _grab.isSelected ? _grab.firstInteractorSelecting as XRBaseInputInteractor : null;
        if (input != null) input.SendHapticImpulse(0.6f, 0.1f);
    }

    /// <summary>Drop whatever the magnet is holding (called when a hand grabs the item).</summary>
    public void Release()
    {
        if (_pulling != null) { CancelPull(); return; }
        if (_held == null) return;
        if (_joint != null) Destroy(_joint);
        _joint = null;
        var item = _held;
        _held = null;
        item.AttachedTo = null;
        item.CooldownUntil = Time.time + regrabCooldown;
        SetIgnore(item, false);
    }

    private void SetIgnore(MagneticItem item, bool ignore)
    {
        foreach (var a in _myColliders)
            foreach (var b in item.Colliders)
                if (a != null && b != null) Physics.IgnoreCollision(a, b, ignore);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(pullPoint ? pullPoint.position : transform.position, pullRadius);
    }
}
