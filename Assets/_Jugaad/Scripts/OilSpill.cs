using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

/// <summary>
/// Slippery oil on the floor. Until something covers it:
/// - teleporting onto it is refused (red arc) and tells the player "you'll slip!",
/// - walking onto it (smooth movement or real steps) slides the player back off it.
/// Jugaad: drop the jute sack (bora) on it - it unfolds flat over the oil and the floor is safe.
/// Put it on an unscaled object at the centre of the spill (only its yaw matters).
/// </summary>
public class OilSpill : MonoBehaviour, ArcTeleporter.ITeleportBlocker
{
    [Header("Zone (local X/Z, centred on this object)")]
    [SerializeField] private Vector2 size = new Vector2(1.0f, 1.1f);
    [Tooltip("Extra border (m) where teleport is refused - the player's body is not a point.")]
    [SerializeField] private float teleportMargin = 0.2f;
    [Tooltip("How far (m) inside the edge the head must be before the player slips.")]
    [SerializeField] private float stepInset = 0.05f;

    [Header("Bora (jute sack)")]
    [SerializeField] private XRGrabInteractable bora;
    [SerializeField] private GameObject boraFolded;
    [SerializeField] private GameObject boraUnfolded;
    [Tooltip("The sack counts as 'on the oil' within this distance (m) of the zone.")]
    [SerializeField] private float placeMargin = 0.35f;
    [SerializeField] private float placeHeight = 0.6f;
    [SerializeField] private float placeTime = 0.4f;
    [SerializeField] private AudioClip placeSound;

    [Header("Slip")]
    [SerializeField] private float slipBack = 0.35f;
    [SerializeField] private float slipTime = 0.3f;
    [SerializeField] private AudioClip slipSound;
    [SerializeField] private TaskHUD hud;
    [SerializeField] private float messageCooldown = 3f;

    public bool Covered { get; private set; }
    public int Slips { get; private set; }

    private XROrigin _origin;
    private CharacterController _cc;
    private Transform _head;
    private ContinuousMoveProvider _move;
    private Rigidbody _boraRb;
    private Vector3 _lastSafe, _unfoldedScale = Vector3.one;
    private bool _haveSafe, _sliding, _placing;
    private float _nextMsg;
    // to rebuild the sack as a grabbable thing when it's picked up off the floor again
    private GameObject _boraGo;
    private float _boraMass = 1f, _boraDrag, _boraAngDrag = 0.05f;
    private XRBaseInteractable.MovementType _boraMove = XRBaseInteractable.MovementType.Instantaneous;
    private bool _boraDynAttach = true;
    private readonly System.Collections.Generic.List<Collider> _boraCols = new System.Collections.Generic.List<Collider>();

    private void Awake()
    {
        if (hud == null) hud = FindFirstObjectByType<TaskHUD>();
        _origin = FindFirstObjectByType<XROrigin>();
        if (_origin != null)
        {
            _cc = _origin.GetComponent<CharacterController>();
            _head = _origin.Camera != null ? _origin.Camera.transform : null;
            _move = _origin.GetComponentInChildren<ContinuousMoveProvider>(true);
        }
        if (bora != null)
        {
            _boraRb = bora.GetComponent<Rigidbody>();
            _boraGo = bora.gameObject;
            _boraMove = bora.movementType;
            _boraDynAttach = bora.useDynamicAttach;
            if (_boraRb != null) { _boraMass = _boraRb.mass; _boraDrag = _boraRb.linearDamping; _boraAngDrag = _boraRb.angularDamping; }
        }
        if (boraUnfolded != null) { _unfoldedScale = boraUnfolded.transform.localScale; boraUnfolded.SetActive(false); }
    }

    private void OnEnable()
    {
        ArcTeleporter.Blockers.Add(this);
        if (bora != null) bora.selectExited.AddListener(OnBoraReleased);
    }

    private void OnDisable()
    {
        ArcTeleporter.Blockers.Remove(this);
        if (bora != null) bora.selectExited.RemoveListener(OnBoraReleased);
    }

    // ---------------- zone ----------------

    private bool Inside(Vector3 worldPoint, float margin)
    {
        Vector3 l = transform.InverseTransformPoint(worldPoint);
        return Mathf.Abs(l.x) <= size.x * 0.5f + margin && Mathf.Abs(l.z) <= size.y * 0.5f + margin;
    }

    public bool BlocksTeleport(Vector3 point) => !Covered && Inside(point, teleportMargin);

    public void OnTeleportBlocked(Vector3 point) => Warn();

    // ---------------- every frame ----------------

    private void Update()
    {
        if (!Covered && !_placing && bora != null && !bora.isSelected && _boraRb != null
            && _boraRb.linearVelocity.sqrMagnitude < 0.04f && BoraOverOil())
            StartCoroutine(Place()); // dropped / thrown onto it and came to rest

        if (Covered || _sliding || _head == null) return;
        Vector3 feet = new Vector3(_head.position.x, _origin.transform.position.y, _head.position.z);
        if (Inside(feet, -stepInset)) StartCoroutine(Slip(feet));
        else if (!Inside(feet, 0.05f)) { _lastSafe = feet; _haveSafe = true; }
    }

    // ---------------- bora ----------------

    private bool BoraOverOil()
    {
        Vector3 p = bora.transform.position;
        return Inside(p, placeMargin) && p.y - transform.position.y < placeHeight;
    }

    private void OnBoraReleased(SelectExitEventArgs args)
    {
        if (!Covered && !_placing && BoraOverOil()) StartCoroutine(Place());
    }

    private IEnumerator Place()
    {
        _placing = true;
        bora.enabled = false;                                   // can't be picked up again
        if (_boraRb != null)
        {
            if (!_boraRb.isKinematic) { _boraRb.linearVelocity = Vector3.zero; _boraRb.angularVelocity = Vector3.zero; }
            _boraRb.isKinematic = true;
        }
        _boraCols.Clear();
        foreach (var c in bora.GetComponentsInChildren<Collider>()) { _boraCols.Add(c); c.enabled = false; } // walk / teleport over it

        Transform t = bora.transform;
        Vector3 fromPos = t.position, toPos = transform.position + Vector3.up * 0.004f;
        Quaternion fromRot = t.rotation, toRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        bool swapped = false;
        for (float k = 0f; k < 1f; k += Time.deltaTime / Mathf.Max(0.01f, placeTime))
        {
            float e = Mathf.SmoothStep(0f, 1f, k);
            t.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, e), Quaternion.Slerp(fromRot, toRot, e));
            if (!swapped && k > 0.45f) { swapped = true; SwapToUnfolded(); }
            if (swapped && boraUnfolded != null)
                boraUnfolded.transform.localScale = Vector3.Scale(_unfoldedScale, new Vector3(Mathf.Lerp(0.55f, 1f, e), 1f, Mathf.Lerp(0.5f, 1f, e)));
            yield return null;
        }
        t.SetPositionAndRotation(toPos, toRot);
        if (!swapped) SwapToUnfolded();
        if (boraUnfolded != null) boraUnfolded.transform.localScale = _unfoldedScale;

        Covered = true;
        _placing = false;
        SfxMix.PlayAt(placeSound != null ? placeSound : ProceduralSfx.Thunk, toPos, 0.7f);
        XRHaptics.PulseNear(toPos, 1.2f, 0.4f, 0.1f);
        Debug.Log("[OilSpill] covered with the bora");

        // From now on the laid-out sack is part of the floor: no rigidbody (so teleport accepts it)
        // and a thin collider so things dropped on it rest on top instead of sinking under it.
        var reset = _boraRb != null ? _boraRb.GetComponent<ResetIfFallen>() : null;
        if (reset != null) Destroy(reset);
        Destroy(bora);
        bora = null;
        yield return null;
        if (_boraRb != null) Destroy(_boraRb);
        yield return null;
        if (boraUnfolded != null)
        {
            var floor = boraUnfolded.GetComponent<BoxCollider>();
            if (floor == null) floor = boraUnfolded.AddComponent<BoxCollider>();
            floor.enabled = true;
            // ...but it can still be picked back up
            var pick = boraUnfolded.GetComponent<XRSimpleInteractable>();
            if (pick == null) pick = boraUnfolded.AddComponent<XRSimpleInteractable>();
            pick.enabled = true;
            pick.selectEntered.RemoveListener(OnPickUp);
            pick.selectEntered.AddListener(OnPickUp);
        }
    }

    // the laid-out sack picked up again: it folds up in the hand and the oil is bare again
    private void OnPickUp(SelectEnterEventArgs args)
    {
        if (!Covered || _placing || _boraGo == null) return;
        StartCoroutine(PickUp(args));
    }

    private IEnumerator PickUp(SelectEnterEventArgs args)
    {
        _placing = true;
        var hand = args.interactorObject;
        var mgr = args.manager;
        yield return null;
        if (mgr != null && hand != null && hand.IsSelecting(args.interactableObject)) mgr.SelectExit(hand, args.interactableObject);
        var pick = boraUnfolded != null ? boraUnfolded.GetComponent<XRSimpleInteractable>() : null;
        if (pick != null) pick.enabled = false;
        if (boraUnfolded != null) boraUnfolded.SetActive(false);
        if (boraFolded != null) boraFolded.SetActive(true);
        foreach (var c in _boraCols) if (c != null) c.enabled = true;
        Covered = false;

        _boraGo.transform.position = hand != null ? hand.transform.position : _boraGo.transform.position + Vector3.up * 0.3f;
        _boraRb = _boraGo.AddComponent<Rigidbody>();
        _boraRb.mass = _boraMass; _boraRb.linearDamping = _boraDrag; _boraRb.angularDamping = _boraAngDrag;
        _boraRb.interpolation = RigidbodyInterpolation.Interpolate;
        bora = _boraGo.AddComponent<XRGrabInteractable>();
        bora.movementType = _boraMove;
        bora.useDynamicAttach = _boraDynAttach;
        bora.selectExited.AddListener(OnBoraReleased);
        _boraGo.AddComponent<ResetIfFallen>();
        yield return null;
        if (mgr != null && hand != null) mgr.SelectEnter(hand, (IXRSelectInteractable)bora);
        _placing = false;
        SfxMix.PlayAt(placeSound != null ? placeSound : ProceduralSfx.Thunk, _boraGo.transform.position, 0.4f);
        Debug.Log("[OilSpill] bora picked up again - oil is bare");
    }

    private void SwapToUnfolded()
    {
        if (boraFolded != null) boraFolded.SetActive(false);
        if (boraUnfolded != null) boraUnfolded.SetActive(true);
    }

    // ---------------- slipping ----------------

    private IEnumerator Slip(Vector3 feet)
    {
        _sliding = true;
        Slips++;
        Vector3 back = _haveSafe ? _lastSafe - feet : feet - transform.position;
        back.y = 0f;
        Vector3 dir = back.sqrMagnitude > 1e-4f ? back.normalized : -transform.forward;
        Vector3 total = back + dir * slipBack;   // back to where they came from, plus a little extra

        bool moveWasOn = _move != null && _move.enabled;
        if (moveWasOn) _move.enabled = false;    // the stick can't fight the slide
        SfxMix.PlayAt(slipSound != null ? slipSound : ProceduralSfx.Error, feet, 0.7f);
        XRHaptics.PulseNear(_head.position, 2f, 0.6f, 0.25f);
        Warn();

        Vector3 moved = Vector3.zero;
        for (float k = 0f; k < 1f; k += Time.deltaTime / Mathf.Max(0.01f, slipTime))
        {
            Vector3 want = total * Mathf.Sin(Mathf.Min(1f, k) * Mathf.PI * 0.5f);
            Vector3 step = want - moved;
            moved = want;
            if (_cc != null && _cc.enabled) _cc.Move(step); else _origin.transform.position += step;
            yield return null;
        }
        Vector3 rest = total - moved;
        if (_cc != null && _cc.enabled) _cc.Move(rest); else _origin.transform.position += rest;

        if (moveWasOn) _move.enabled = true;
        _sliding = false;
    }

    private void Warn()
    {
        if (hud == null || Time.time < _nextMsg) return;
        _nextMsg = Time.time + messageCooldown;
        hud.ShowToast(GameLanguage.Pick("You'll slip on the oil! Cover it with something.",
                                        "Phisal jaoge! Oil pe kuch bichhao."), hud.WarnColor);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f), Vector3.one);
        Gizmos.DrawWireCube(Vector3.up * 0.02f, new Vector3(size.x, 0.04f, size.y));
    }
}
