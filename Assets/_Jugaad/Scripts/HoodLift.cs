using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Bonnet: grab its front edge and lift - it swings on its hinge with your hand. Let go above
/// stayOpenAngle and it goes up all the way and the support rod props it; let go lower and it drops
/// shut with a thud. Grab it again to close it.
/// Put it on the bonnet's grab collider; hinge rotates around its local X (negative = open).
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class HoodLift : MonoBehaviour
{
    [SerializeField] private Transform hinge;
    [SerializeField] private float openAngle = 62f;
    [SerializeField] private float stayOpenAngle = 35f;
    [Tooltip("Thin rod shown between rodBase and rodTop while it's propped open.")]
    [SerializeField] private Transform rod;
    [SerializeField] private Transform rodBase;
    [SerializeField] private Transform rodTop;
    [SerializeField] private float rodThickness = 0.012f;
    [SerializeField] private AudioClip thudSound;
    [SerializeField] private AudioClip creakSound;

    public float Angle => _angle;
    public bool IsOpen => _angle > stayOpenAngle && _hand == null && !_moving;

    private XRSimpleInteractable _grab;
    private Transform _hand;
    private Quaternion _closed;
    private float _angle;
    private bool _moving;

    private void Awake()
    {
        _grab = GetComponent<XRSimpleInteractable>();
        if (hinge != null) _closed = hinge.localRotation;
        if (rod != null) rod.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        _grab.selectEntered.AddListener(OnGrab);
        _grab.selectExited.AddListener(OnRelease);
    }

    private void OnDisable()
    {
        _grab.selectEntered.RemoveListener(OnGrab);
        _grab.selectExited.RemoveListener(OnRelease);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        StopAllCoroutines();
        _moving = false;
        _hand = args.interactorObject.transform;
        if (rod != null) rod.gameObject.SetActive(false);
        if (_angle < 5f) SfxMix.PlayAt(creakSound != null ? creakSound : ProceduralSfx.Tick, transform.position, 0.4f, 0.1f);
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        _hand = null;
        StartCoroutine(Settle(_angle > stayOpenAngle ? openAngle : 0f));
    }

    private void Update()
    {
        if (_hand == null || hinge == null || hinge.parent == null) return;
        // the hand's height/forwardness around the hinge, in the car's side view (Y-Z)
        Vector3 local = hinge.parent.InverseTransformPoint(_hand.position) - hinge.localPosition;
        float a = Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg;
        SetAngle(Mathf.Clamp(a, 0f, openAngle));
    }

    private IEnumerator Settle(float target)
    {
        _moving = true;
        float from = _angle;
        float time = target > from ? 0.35f : 0.25f;
        for (float k = 0f; k < 1f; k += Time.deltaTime / time)
        {
            float e = target > from ? 1f - (1f - k) * (1f - k) : k * k;     // eases up, falls faster
            SetAngle(Mathf.Lerp(from, target, e));
            yield return null;
        }
        SetAngle(target);
        _moving = false;
        if (target <= 0f)
        {
            SfxMix.PlayAt(thudSound != null ? thudSound : ProceduralSfx.Thunk, transform.position, 0.9f, 0.05f);
            XRHaptics.PulseNear(transform.position, 1.2f, 0.5f, 0.08f);
        }
        else if (rod != null && rodBase != null && rodTop != null)
        {
            rod.gameObject.SetActive(true);
            Vector3 a = rodBase.position, b = rodTop.position;
            rod.position = (a + b) * 0.5f;
            rod.rotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            rod.localScale = new Vector3(rodThickness, (b - a).magnitude * 0.5f, rodThickness);   // unit cylinder is 2 tall; keep the rod outside the mirrored car
            SfxMix.PlayAt(ProceduralSfx.Tick, rodTop.position, 0.5f, 0.05f);
        }
    }

    private void SetAngle(float a)
    {
        _angle = a;
        if (hinge != null) hinge.localRotation = _closed * Quaternion.Euler(-a, 0f, 0f);
    }
}
