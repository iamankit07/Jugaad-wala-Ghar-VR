using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Locks a physics (HingeJoint) door. While locked the hinge limits are clamped
/// to a tiny range, so pulling the handle only rattles the door. Unlock() restores
/// the original hinge limits so the existing grab-the-handle setup opens it.
/// Put this on the door leaf (the object with the HingeJoint).
/// </summary>
[RequireComponent(typeof(HingeJoint))]
public class LockableDoor : MonoBehaviour
{
    [SerializeField] private bool startLocked = true;

    [Tooltip("How far (degrees) the door can wiggle while locked. 0 = no movement.")]
    [SerializeField] private float lockedRattleAngle = 1.5f;

    [Header("Feedback (optional)")]
    [SerializeField] private AudioClip unlockSound;
    [SerializeField] private AudioClip lockedRattleSound;

    [Header("Events")]
    public UnityEvent onUnlocked;

    public bool IsLocked { get; private set; }
    /// <summary>The hinge limits the door has when unlocked.</summary>
    public JointLimits OpenLimits => _openLimits;
    /// <summary>Hinge angle of the shut door.</summary>
    public float ClosedAngle => _closedAngle;

    private HingeJoint _hinge;
    private Rigidbody _rb;
    private JointLimits _openLimits;
    private float _closedAngle;
    private float _nextRattleSoundTime;
    private XRGrabInteractable[] _handles;
    private Vector3 _closedLocalPos;
    private Quaternion _closedLocalRot;
    private bool _frozen;

    private void Awake()
    {
        _hinge = GetComponent<HingeJoint>();
        _rb = GetComponent<Rigidbody>();
        _openLimits = _hinge.limits;

        // "Closed" is whichever limit is nearest 0 (door starts closed).
        _closedAngle = Mathf.Abs(_openLimits.min) < Mathf.Abs(_openLimits.max) ? _openLimits.min : _openLimits.max;

        _handles = GetComponentsInChildren<XRGrabInteractable>(true);
        _closedLocalPos = transform.localPosition;
        _closedLocalRot = transform.localRotation;

        SetLocked(startLocked);
    }

    public void Unlock()
    {
        if (!IsLocked) return;
        SetLocked(false);
        if (unlockSound) SfxMix.PlayAt(unlockSound, transform.position);
        onUnlocked?.Invoke();
        Debug.Log($"[LockableDoor] {name} unlocked");
    }

    public void Lock() => SetLocked(true);

    private void SetLocked(bool locked)
    {
        IsLocked = locked;

        if (locked)
        {
            // Allow a small wiggle towards the "open" side only, so it feels locked, not glued.
            float openDir = Mathf.Sign((_openLimits.min + _openLimits.max) * 0.5f - _closedAngle);
            float a = _closedAngle;
            float b = _closedAngle + openDir * lockedRattleAngle;
            var l = _openLimits;
            l.min = Mathf.Min(a, b);
            l.max = Mathf.Max(a, b);
            _hinge.limits = l;
        }
        else
        {
            _hinge.limits = _openLimits;
        }

        _hinge.useLimits = true;
        if (!locked) SetFrozen(false);
        if (_rb) _rb.WakeUp();
    }

    /// <summary>
    /// While locked and nobody holds a handle, the leaf is frozen at its closed pose, so bumping it
    /// (a held book, a remote, the player) cannot shake it or push it a little open. Grabbing a
    /// handle unfreezes it so pulling still gives the 'it's locked' rattle.
    /// </summary>
    private void FixedUpdate()
    {
        if (!IsLocked) return;
        bool held = false;
        foreach (var h in _handles) if (h != null && h.isSelected) { held = true; break; }
        SetFrozen(!held);
    }

    private void SetFrozen(bool frozen)
    {
        if (_rb == null || frozen == _frozen) return;
        _frozen = frozen;
        if (frozen)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
            transform.localPosition = _closedLocalPos;
            transform.localRotation = _closedLocalRot;
            _rb.position = transform.position;
            _rb.rotation = transform.rotation;
        }
        else
        {
            _rb.isKinematic = false;
            _rb.WakeUp();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Cheap "it's locked" feedback when the door hits its locked limit / gets yanked.
        if (!IsLocked || lockedRattleSound == null) return;
        if (Time.time < _nextRattleSoundTime) return;
        _nextRattleSoundTime = Time.time + 0.5f;
        SfxMix.PlayAt(lockedRattleSound, transform.position, 0.6f);
    }
}
