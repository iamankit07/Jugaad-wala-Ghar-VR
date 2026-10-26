using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// The car's steering wheel. Grab the rim (GRIP, one or both hands) and turn it like a real one:
/// it follows your hands round its column. Angle is + when turned clockwise as the driver sees it
/// (= steer right). Let go while the car is rolling and it slowly straightens itself.
/// Put it on the steering wheel (pivot at its centre); axisEnd = a point on the column towards the driver.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class SteeringWheel : MonoBehaviour
{
    [SerializeField] private Transform axisEnd;
    [SerializeField] private float maxAngle = 450f;
    [SerializeField] private AudioClip gripClip;

    /// <summary>Degrees, + = clockwise from the driver's seat (turn right).</summary>
    public float Angle => _angle;
    public bool Held => _grab != null && _grab.isSelected;

    private XRSimpleInteractable _grab;
    private Quaternion _baseLocal;
    private Vector3 _axisLocal;
    private float _visualSign = 1f;
    private float _angle;
    private readonly Dictionary<IXRSelectInteractor, Vector3> _prev = new Dictionary<IXRSelectInteractor, Vector3>();

    /// <summary>World direction from the wheel towards the driver (the column axis).</summary>
    private Vector3 ToDriver => axisEnd != null ? (axisEnd.position - transform.position).normalized : -transform.forward;

    private void Awake()
    {
        _grab = GetComponent<XRSimpleInteractable>();
        _grab.selectMode = InteractableSelectMode.Multiple;
        _baseLocal = transform.localRotation;
        _axisLocal = axisEnd != null && transform.parent != null
            ? (axisEnd.localPosition - transform.localPosition).normalized : Vector3.forward;
        // The car is mirrored, so find out which way a local turn looks in the world.
        var mf = GetComponent<MeshFilter>();
        Vector3 probe = mf != null && mf.sharedMesh != null && mf.sharedMesh.vertexCount > 0 ? mf.sharedMesh.bounds.max : Vector3.one * 0.1f;
        Vector3 c = transform.position;
        Vector3 p0 = transform.TransformPoint(probe) - c;
        transform.localRotation = Quaternion.AngleAxis(10f, _axisLocal) * _baseLocal;
        Vector3 p1 = transform.TransformPoint(probe) - c;
        transform.localRotation = _baseLocal;
        float seen = Vector3.SignedAngle(Vector3.ProjectOnPlane(p0, ToDriver), Vector3.ProjectOnPlane(p1, ToDriver), ToDriver);
        _visualSign = seen >= 0f ? 1f : -1f;
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
        _prev[args.interactorObject] = HandDir(args.interactorObject.transform.position);
        SfxMix.PlayAt(gripClip != null ? gripClip : ProceduralSfx.Tick, transform.position, 0.3f, 0.1f);
        XRHaptics.Pulse(args.interactorObject, 0.25f, 0.04f);
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        _prev.Remove(args.interactorObject);
    }

    private Vector3 HandDir(Vector3 hand) => Vector3.ProjectOnPlane(hand - transform.position, ToDriver);

    private void Update()
    {
        if (_grab.isSelected && _prev.Count > 0)
        {
            float sum = 0f; int n = 0;
            var keys = new List<IXRSelectInteractor>(_prev.Keys);
            foreach (var i in keys)
            {
                Vector3 now = HandDir(i.transform.position);
                Vector3 was = _prev[i];
                if (now.sqrMagnitude > 0.0004f && was.sqrMagnitude > 0.0004f)
                {
                    sum += Mathf.Clamp(Vector3.SignedAngle(was, now, ToDriver), -45f, 45f);   // + about the axis = clockwise seen from its tip (the driver)
                    n++;
                }
                _prev[i] = now;
            }
            if (n > 0) _angle = Mathf.Clamp(_angle + sum / n, -maxAngle, maxAngle);
        }
        Apply();
    }

    /// <summary>Straighten up by this many degrees (the car rolling with nobody holding the wheel).</summary>
    public void Recentre(float degrees)
    {
        if (Held) return;
        _angle = Mathf.MoveTowards(_angle, 0f, degrees);
    }

    private void Apply()
    {
        transform.localRotation = Quaternion.AngleAxis(_angle * _visualSign, _axisLocal) * _baseLocal;
    }

    // tests
    public void SetAngleFromCode(float a) { _angle = Mathf.Clamp(a, -maxAngle, maxAngle); Apply(); }
}
