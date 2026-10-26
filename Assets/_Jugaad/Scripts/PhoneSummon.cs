using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

/// <summary>
/// The phone lives in the player's pocket. B (right controller) toggles it:
/// - phone not in a hand (in the pocket, or dropped somewhere) -> it jumps into the right hand,
///   screen facing the player. Whatever the right hand held is let go first (the left hand keeps
///   its item), so the phone always arrives - even with both hands full.
/// - phone in a hand (either hand) -> back into the pocket.
/// In the pocket the phone is invisible and not grabbable, but it follows the player's hip, so a
/// ringtone / notification still sounds like it comes from the pocket.
/// The phone stays in the hand without holding grip; it drops only after the player presses grip
/// again and lets go (a grip that was already held when B was pressed does not drop it).
/// If it is dropped and falls out of the world it goes back to the pocket.
/// Put it on the phone (next to its XRGrabInteractable).
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class PhoneSummon : MonoBehaviour
{
    [Tooltip("Hand that receives the phone. Empty = the right-hand Direct Interactor in the scene.")]
    [SerializeField] private XRDirectInteractor hand;
    [SerializeField] private string summonBinding = "<XRController>{RightHand}/{SecondaryButton}"; // B
    [SerializeField] private string gripBinding = "<XRController>{RightHand}/{GripButton}";
    [Tooltip("Where the phone appears, in the hand's local space (m).")]
    [SerializeField] private Vector3 holdOffset = new Vector3(0f, 0.06f, 0.06f);
    [SerializeField] private float haptic = 0.3f;
    [SerializeField] private AudioClip summonSound;

    [Header("Pocket")]
    [SerializeField] private bool startInPocket = true;
    [Tooltip("Pocket position from the head: x = right, y = up, z = forward (m, yaw only).")]
    [SerializeField] private Vector3 pocketOffset = new Vector3(0.18f, -0.6f, 0f);
    [SerializeField] private AudioClip pocketSound;
    [Tooltip("A dropped phone below this world Y goes back to the pocket.")]
    [SerializeField] private float fallY = -0.5f;

    public bool IsPocketed => _pocketed;
    public XRDirectInteractor Hand { get { if (hand == null) hand = FindRightHand(); return hand; } }

    private XRGrabInteractable _phone;
    private Rigidbody _rb;
    private Collider[] _colliders;
    private InputAction _summon, _grip;
    private bool _forced, _pocketed;
    private bool _restoreThrow;
    private XRInputButtonReader.InputSourceMode _origMode;
    private readonly List<GameObject> _hidden = new List<GameObject>();

    private void Awake()
    {
        _phone = GetComponent<XRGrabInteractable>();
        _rb = GetComponent<Rigidbody>();
        _colliders = GetComponents<Collider>();
        // The pocket replaces the 'reset to the table' safety net.
        var reset = GetComponent<ResetIfFallen>();
        if (reset) reset.enabled = false;

        _summon = new InputAction("PhoneSummon", InputActionType.Button);
        _summon.AddBinding(summonBinding);
        _grip = new InputAction("PhoneSummonGrip", InputActionType.Button);
        _grip.AddBinding(gripBinding);
    }

    private void Start()
    {
        if (startInPocket) Pocket();
    }

    private void OnEnable()
    {
        _summon.performed += OnButton;
        _summon.Enable();
        _grip.Enable();
    }

    private void OnDisable()
    {
        _summon.performed -= OnButton;
        _summon.Disable();
        _grip.Disable();
        ForceHold(false);
    }

    private void OnButton(InputAction.CallbackContext ctx) => Toggle();

    /// <summary>B: in a hand -> pocket, otherwise -> right hand.</summary>
    public void Toggle()
    {
        if (Time.timeScale == 0f) return; // paused
        if (IsInHand()) PutInPocket();
        else Summon();
    }

    public bool IsInHand()
    {
        for (int i = 0; i < _phone.interactorsSelecting.Count; i++)
            if (_phone.interactorsSelecting[i] is XRBaseInputInteractor) return true;
        return false;
    }

    /// <summary>Bring the phone into the right hand.</summary>
    public void Summon()
    {
        var h = Hand;
        if (h == null || !h.isActiveAndEnabled) return;
        var mgr = _phone.interactionManager != null ? _phone.interactionManager : h.interactionManager;
        if (mgr == null) return;
        if (IsInHand()) return;

        // Held by anything else (e.g. a socket): take it out.
        for (int i = _phone.interactorsSelecting.Count - 1; i >= 0; i--)
            mgr.SelectExit(_phone.interactorsSelecting[i], _phone);
        // Free the hand.
        for (int i = h.interactablesSelected.Count - 1; i >= 0; i--)
            mgr.SelectExit(h, h.interactablesSelected[i]);

        Unpocket();

        // Just in front of the hand, screen (local +Z) towards the face.
        Transform attach = h.GetAttachTransform(_phone);
        Vector3 pos = attach.TransformPoint(holdOffset);
        var cam = Camera.main;
        Vector3 toFace = cam != null ? cam.transform.position - pos : -attach.forward;
        if (toFace.sqrMagnitude < 1e-4f) toFace = -attach.forward;
        Quaternion rot = Quaternion.LookRotation(toFace.normalized, cam != null ? cam.transform.up : Vector3.up);
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.position = pos;
        _rb.rotation = rot;
        transform.SetPositionAndRotation(pos, rot);

        ForceHold(true);
        mgr.SelectEnter((IXRSelectInteractor)h, (IXRSelectInteractable)_phone);
        XRHaptics.Pulse(h, haptic, 0.08f);
        if (summonSound) SfxMix.PlayAt(summonSound, pos, 0.6f);
    }

    /// <summary>Take the phone out of whichever hand holds it and put it in the pocket.</summary>
    public void PutInPocket()
    {
        IXRSelectInteractor holder = null;
        for (int i = 0; i < _phone.interactorsSelecting.Count; i++)
            if (_phone.interactorsSelecting[i] is XRBaseInputInteractor) { holder = _phone.interactorsSelecting[i]; break; }

        ForceHold(false);
        // The pocketed phone is kinematic; don't let XRI try to throw it (it would warn).
        if (_phone.throwOnDetach) { _phone.throwOnDetach = false; _restoreThrow = true; }
        var mgr = _phone.interactionManager;
        if (mgr != null)
            for (int i = _phone.interactorsSelecting.Count - 1; i >= 0; i--)
                mgr.SelectExit(_phone.interactorsSelecting[i], _phone);

        Pocket();
        if (holder != null) XRHaptics.Pulse(holder, haptic * 0.6f, 0.05f);
        if (pocketSound) SfxMix.PlayAt(pocketSound, transform.position, 0.5f);
    }

    private void Update()
    {
        if (!_forced) return;
        if (!_phone.interactorsSelecting.Contains(hand)) { ForceHold(false); return; } // dropped / pocketed / other hand took it
        if (_grip.WasPressedThisFrame()) ForceHold(false); // player takes over: letting go of this grip drops it
    }

    private void LateUpdate()
    {
        if (_pocketed) { FollowPocket(); return; }
        if (!_phone.isSelected && transform.position.y < fallY) Pocket(); // fell out of the world
    }

    private void Pocket()
    {
        if (_pocketed) return;
        _pocketed = true;
        _hidden.Clear();
        foreach (Transform c in transform)
            if (c.gameObject.activeSelf && c.GetComponentInChildren<Renderer>(true) != null) // visuals only, not XRI's attach points
            { c.gameObject.SetActive(false); _hidden.Add(c.gameObject); }
        foreach (var c in _colliders) if (c) c.enabled = false;
        if (!_rb.isKinematic)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
        _rb.isKinematic = true;
        FollowPocket();
    }

    private void Unpocket()
    {
        if (!_pocketed) return;
        _pocketed = false;
        foreach (var go in _hidden) if (go) go.SetActive(true);
        _hidden.Clear();
        foreach (var c in _colliders) if (c) c.enabled = true;
        _rb.isKinematic = false;
        if (_restoreThrow) { _phone.throwOnDetach = true; _restoreThrow = false; }
    }

    private void FollowPocket()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(cam.transform.up, Vector3.up);
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        Vector3 p = cam.transform.position + right * pocketOffset.x + Vector3.up * pocketOffset.y + fwd * pocketOffset.z;
        _rb.position = p;
        transform.position = p;
    }

    /// <summary>
    /// While forced, the hand's select (grip) input reads as held, so the phone stays in the hand
    /// even though grip is not pressed. Turning it off gives the hand back its real grip input.
    /// </summary>
    private void ForceHold(bool on)
    {
        if (hand == null) return;
        var input = hand.selectInput;
        if (on)
        {
            if (!_forced) _origMode = input.inputSourceMode;
            input.manualValue = 1f;
            input.manualPerformed = true;
            input.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
            _forced = true;
        }
        else if (_forced)
        {
            input.inputSourceMode = _origMode;
            input.manualPerformed = false;
            input.manualValue = 0f;
            _forced = false;
        }
    }

    private static XRDirectInteractor FindRightHand()
    {
        foreach (var d in FindObjectsByType<XRDirectInteractor>(FindObjectsSortMode.None))
            if (d.handedness == InteractorHandedness.Right) return d;
        return null;
    }
}
