using System.Collections;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Get in / out of the car with one button (A on the right controller - nothing else uses it).
/// Stand by the driver's door: a "[A] Get in" prompt shows. Press it: the door swings open, a quick
/// blink, and you are in the driver's seat (head at seated eye height, facing forward, whether you
/// play standing or sitting). Locomotion is off while seated. Press A again to get out by the door.
/// Put it on the car.
/// </summary>
public class CarSeat : MonoBehaviour
{
    [Header("Rig")]
    [SerializeField] private XROrigin origin;
    [SerializeField] private ArcTeleporter teleporter;
    [Tooltip("Locomotion etc. switched off while seated.")]
    [SerializeField] private Behaviour[] disableWhileSeated;

    [Header("Car")]
    [Tooltip("Where the driver's eyes go; its forward = looking out of the windscreen.")]
    [SerializeField] private Transform driverEye;
    [Tooltip("Floor point outside the driver's door; its forward = where you face after getting out.")]
    [SerializeField] private Transform exitPoint;
    [Tooltip("Door hinge (rotates around its local Y).")]
    [SerializeField] private Transform doorHinge;
    [SerializeField] private float doorOpenAngle = 62f;

    [Header("Prompt")]
    [SerializeField] private Transform outsidePrompt;
    [SerializeField] private Transform insidePrompt;
    [SerializeField] private TMP_Text promptLabel;
    [SerializeField] private float promptRadius = 1.0f;
    [SerializeField] private string buttonBinding = "<XRController>{RightHand}/{PrimaryButton}"; // A

    [Header("Sound")]
    [SerializeField] private AudioClip doorOpenSound;
    [SerializeField] private AudioClip doorCloseSound;

    public bool Seated { get; private set; }
    /// <summary>Set false to keep the player out (or in) for a while.</summary>
    public bool CanUse { get; set; } = true;
    public event System.Action Entered;
    public event System.Action Exited;

    private InputAction _button;
    private bool _busy;
    private float _floorY;
    private Quaternion _doorClosed;

    private Transform Cam => origin != null && origin.Camera != null ? origin.Camera.transform : Camera.main.transform;

    private void Awake()
    {
        if (origin == null) origin = FindFirstObjectByType<XROrigin>();
        if (teleporter == null) teleporter = FindFirstObjectByType<ArcTeleporter>();
        if (doorHinge != null) _doorClosed = doorHinge.localRotation;
        _button = new InputAction("CarSeat", InputActionType.Button, buttonBinding);
        if (promptLabel != null) promptLabel.gameObject.SetActive(false);
    }

    private void OnEnable() => _button.Enable();
    private void OnDisable() => _button.Disable();

    private void Update()
    {
        if (_busy || origin == null) { ShowPrompt(null, null); return; }
        bool pressed = _button.WasPressedThisFrame();

        if (!Seated)
        {
            Vector3 d = Cam.position - outsidePrompt.position; d.y = 0f;
            bool near = CanUse && d.magnitude < promptRadius;
            ShowPrompt(near ? outsidePrompt : null, GameLanguage.Pick("[A]  Get in the car", "[A]  Gaadi mein baitho"));
            if (near && pressed) StartCoroutine(GetIn());
        }
        else
        {
            ShowPrompt(CanUse ? insidePrompt : null, GameLanguage.Pick("[A]  Get out", "[A]  Bahar niklo"));
            if (CanUse && pressed) StartCoroutine(GetOut());
        }
    }

    private void ShowPrompt(Transform at, string text)
    {
        if (promptLabel == null) return;
        bool on = at != null;
        if (promptLabel.gameObject.activeSelf != on) promptLabel.gameObject.SetActive(on);
        if (!on) return;
        if (promptLabel.text != text) promptLabel.text = text;
        Transform t = promptLabel.transform;
        t.position = at.position;
        Vector3 look = t.position - Cam.position; look.y = 0f;          // upright, facing the player
        if (look.sqrMagnitude > 1e-4f) t.rotation = Quaternion.LookRotation(look);
    }

    // ---------------- in / out ----------------
    public IEnumerator GetIn()
    {
        if (_busy || Seated) yield break;
        _busy = true;
        ShowPrompt(null, null);
        yield return Door(true);
        yield return Fade(0f, 1f);

        _floorY = origin.transform.position.y;
        SetLocomotion(false);
        Face(driverEye.forward);
        origin.MoveCameraToWorldLocation(driverEye.position);       // head exactly at the seat, also in height

        if (doorHinge != null) doorHinge.localRotation = _doorClosed;
        Play(doorCloseSound, 0.9f);
        yield return Fade(1f, 0f);
        Seated = true;
        _busy = false;
        Entered?.Invoke();
        Debug.Log("[CarSeat] in the driver's seat");
    }

    public IEnumerator GetOut()
    {
        if (_busy || !Seated) yield break;
        _busy = true;
        ShowPrompt(null, null);
        if (doorHinge != null) doorHinge.localRotation = _doorClosed * Quaternion.Euler(0f, doorOpenAngle, 0f);
        Play(doorOpenSound, 0.8f);
        yield return Fade(0f, 1f);

        Face(exitPoint.forward);
        Vector3 offset = Cam.position - origin.transform.position; offset.y = 0f;
        origin.transform.position = new Vector3(exitPoint.position.x - offset.x, _floorY, exitPoint.position.z - offset.z);
        SetLocomotion(true);
        Seated = false;

        yield return Fade(1f, 0f);
        yield return Door(false);
        _busy = false;
        Exited?.Invoke();
        Debug.Log("[CarSeat] got out");
    }

    private void Face(Vector3 forward)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-4f) return;
        Vector3 camFwd = Cam.forward; camFwd.y = 0f;
        if (camFwd.sqrMagnitude < 1e-4f) camFwd = origin.transform.forward;
        float angle = Vector3.SignedAngle(camFwd, forward, Vector3.up);
        origin.RotateAroundCameraUsingOriginUp(angle);
    }

    // Remember exactly what was on (continuous move OR teleport, smooth OR snap turn - whatever the
    // player picked in the menu) and put back only that when getting out.
    private readonly System.Collections.Generic.List<Behaviour> _wasOn = new System.Collections.Generic.List<Behaviour>();
    private bool _ccWasOn;

    private void SetLocomotion(bool on)
    {
        var cc = origin.GetComponent<CharacterController>();
        if (!on)
        {
            _wasOn.Clear();
            if (teleporter != null && teleporter.enabled) _wasOn.Add(teleporter);
            if (disableWhileSeated != null) foreach (var b in disableWhileSeated) if (b != null && b.enabled) _wasOn.Add(b);
            foreach (var b in _wasOn) b.enabled = false;
            _ccWasOn = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;                       // it would shove us out of the car
        }
        else
        {
            foreach (var b in _wasOn) if (b != null) b.enabled = true;
            _wasOn.Clear();
            if (cc != null) cc.enabled = _ccWasOn;
        }
    }

    private IEnumerator Door(bool open)
    {
        if (doorHinge == null) yield break;
        Play(open ? doorOpenSound : doorCloseSound, 0.8f);
        Quaternion from = doorHinge.localRotation;
        Quaternion to = open ? _doorClosed * Quaternion.Euler(0f, doorOpenAngle, 0f) : _doorClosed;
        for (float k = 0f; k < 1f; k += Time.deltaTime / 0.3f)
        {
            doorHinge.localRotation = Quaternion.Slerp(from, to, 1f - (1f - k) * (1f - k));
            yield return null;
        }
        doorHinge.localRotation = to;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (teleporter != null) yield return teleporter.FadeScreen(from, to, 0.15f);
        else yield return new WaitForSeconds(0.15f);
    }

    private void Play(AudioClip clip, float vol)
    {
        if (clip != null) SfxMix.PlayAt(clip, doorHinge != null ? doorHinge.position : transform.position, vol, 0.05f);
    }
}
