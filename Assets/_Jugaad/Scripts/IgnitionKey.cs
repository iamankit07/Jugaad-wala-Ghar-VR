using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// The key in the ignition. Grip it and twist your wrist (either way) - the key turns with you and
/// past ~45 degrees it "turns over" (Turned fires). Let go and it springs back.
/// TRIGGER while holding it also turns it, for players who don't think of twisting.
/// Put it on the grab collider next to the key; keyVisual is what rotates (around its local Z).
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class IgnitionKey : MonoBehaviour
{
    [SerializeField] private Transform keyVisual;
    [SerializeField] private float maxAngle = 75f;
    [SerializeField] private float fireAngle = 45f;
    [SerializeField] private AudioClip turnClick;

    /// <summary>Fired once per turn (when the key reaches fireAngle).</summary>
    public event System.Action Turned;

    private XRSimpleInteractable _grab;
    private Transform _hand;
    private IXRSelectInteractor _holder;
    private Vector3 _startUp;
    private float _angle;
    private bool _fired, _forced;
    private Quaternion _rest;

    private void Awake()
    {
        _grab = GetComponent<XRSimpleInteractable>();
        if (keyVisual != null) _rest = keyVisual.localRotation;
    }

    private void OnEnable()
    {
        _grab.selectEntered.AddListener(OnGrab);
        _grab.selectExited.AddListener(OnRelease);
        _grab.activated.AddListener(OnTrigger);
    }

    private void OnDisable()
    {
        _grab.selectEntered.RemoveListener(OnGrab);
        _grab.selectExited.RemoveListener(OnRelease);
        _grab.activated.RemoveListener(OnTrigger);
    }

    private Vector3 Axis => keyVisual != null ? keyVisual.forward : transform.forward;

    private void OnGrab(SelectEnterEventArgs args)
    {
        _holder = args.interactorObject;
        _hand = args.interactorObject.transform;
        _startUp = Vector3.ProjectOnPlane(_hand.up, Axis);
        if (_startUp.sqrMagnitude < 1e-4f) _startUp = Vector3.ProjectOnPlane(_hand.right, Axis);
        _fired = false;
        _forced = false;
        XRHaptics.Pulse(_holder, 0.2f, 0.03f);
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        _hand = null;
        _holder = null;
        _forced = false;
    }

    private void OnTrigger(ActivateEventArgs args)
    {
        _forced = true;            // trigger = "just turn it"
    }

    private void Update()
    {
        float target = 0f;
        if (_hand != null)
        {
            if (_forced) target = maxAngle;
            else
            {
                Vector3 cur = Vector3.ProjectOnPlane(_hand.up, Axis);
                if (cur.sqrMagnitude < 1e-4f) cur = Vector3.ProjectOnPlane(_hand.right, Axis);
                target = Mathf.Min(maxAngle, Mathf.Abs(Vector3.SignedAngle(_startUp, cur, Axis)));
            }
        }
        // follows the wrist while held, springs back when let go
        _angle = Mathf.MoveTowards(_angle, target, (_hand != null ? 600f : 400f) * Time.deltaTime);
        if (keyVisual != null) keyVisual.localRotation = _rest * Quaternion.Euler(0f, 0f, _angle);

        if (!_fired && _angle >= fireAngle)
        {
            _fired = true;
            SfxMix.PlayAt(turnClick != null ? turnClick : ProceduralSfx.Tick, transform.position, 0.7f, 0.05f);
            if (_holder != null) XRHaptics.Pulse(_holder, 0.5f, 0.06f);
            Turned?.Invoke();
        }
        if (_fired && _hand == null && _angle < 5f) _fired = false;   // ready for the next turn
    }

    /// <summary>For tests: as if the player turned it.</summary>
    public void TurnFromCode() => Turned?.Invoke();
}
