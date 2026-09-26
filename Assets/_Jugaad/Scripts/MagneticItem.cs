using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Something a MagnetTool can pull and hold (e.g. the bedroom key).
/// Optionally cannot be grabbed by hand until a magnet has pulled it out once
/// (so the player can't just reach under the furniture).
/// </summary>
[RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
public class MagneticItem : MonoBehaviour
{
    public static readonly List<MagneticItem> All = new List<MagneticItem>();

    [Tooltip("If on, hands cannot grab this until a magnet has pulled it out once.")]
    [SerializeField] private bool handGrabOnlyAfterMagnet = true;

    public XRGrabInteractable Grab { get; private set; }
    public Rigidbody Body { get; private set; }
    public bool Extracted { get; private set; }
    public MagnetTool AttachedTo { get; set; }
    public float CooldownUntil { get; set; }

    private InteractionLayerMask _originalLayers;
    private Collider[] _colliders;

    public Collider[] Colliders { get { return _colliders; } }

    private void Awake()
    {
        Grab = GetComponent<XRGrabInteractable>();
        Body = GetComponent<Rigidbody>();
        _colliders = GetComponentsInChildren<Collider>();
        _originalLayers = Grab.interactionLayers;
        if (handGrabOnlyAfterMagnet) Grab.interactionLayers = 0; // nothing can select it yet
        else Extracted = true;
    }

    private void OnEnable()
    {
        All.Add(this);
        Grab.selectEntered.AddListener(OnSelectEntered);
    }

    private void OnDisable()
    {
        All.Remove(this);
        Grab.selectEntered.RemoveListener(OnSelectEntered);
    }

    /// <summary>Called by the magnet the first time it catches this item.</summary>
    public void MarkExtracted()
    {
        if (Extracted) return;
        Extracted = true;
        Grab.interactionLayers = _originalLayers;
    }

    public bool CanBePulled
    {
        get { return AttachedTo == null && !Grab.isSelected && Time.time >= CooldownUntil; }
    }

    public bool CanBePulledWhilePulling() { return !Grab.isSelected; }

    public Vector3 WorldCenter
    {
        get
        {
            if (_colliders == null || _colliders.Length == 0) return transform.position;
            Bounds b = _colliders[0].bounds;
            for (int i = 1; i < _colliders.Length; i++) b.Encapsulate(_colliders[i].bounds);
            return b.center;
        }
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        // A hand (or socket) took it: let go of the magnet.
        if (AttachedTo != null) AttachedTo.Release();
    }
}
