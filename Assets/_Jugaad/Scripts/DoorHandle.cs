using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Door lever that you grab and push DOWN. The visible lever rotates around its base
/// (a runtime copy of the mesh, so the physics body/joint is untouched) and Press (0..1)
/// tells the DoorLatch how far it is pressed.
/// Put on each handle (the object with XRGrabInteractable + MeshRenderer, mesh pivot at the base).
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class DoorHandle : MonoBehaviour
{
    [SerializeField] private float maxAngle = 40f;
    [Tooltip("How far (m) the hand must move down for a full press.")]
    [SerializeField] private float fullPressDistance = 0.05f;
    [SerializeField] private float followSpeed = 18f;

    /// <summary>0 = resting, 1 = fully pressed down.</summary>
    public float Press { get; private set; }
    public bool IsPressed => Press >= 0.7f;
    public XRGrabInteractable Grab { get; private set; }

    private Transform _visual;
    private float _sign = 1f;
    private float _angle;
    private IXRSelectInteractor _holder;
    private float _startY;

    private void Awake()
    {
        Grab = GetComponent<XRGrabInteractable>();
        BuildVisual();
    }

    private void OnEnable()
    {
        Grab.selectEntered.AddListener(OnGrab);
        Grab.selectExited.AddListener(OnRelease);
    }

    private void OnDisable()
    {
        Grab.selectEntered.RemoveListener(OnGrab);
        Grab.selectExited.RemoveListener(OnRelease);
    }

    private void BuildVisual()
    {
        var mf = GetComponent<MeshFilter>();
        var mr = GetComponent<MeshRenderer>();
        if (mf == null || mr == null || mf.sharedMesh == null) return;

        var go = new GameObject("HandleVisual");
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterials = mr.sharedMaterials;
        r.shadowCastingMode = mr.shadowCastingMode;
        r.receiveShadows = mr.receiveShadows;
        r.lightProbeUsage = mr.lightProbeUsage;
        mr.enabled = false;
        _visual = go.transform;

        // The lever sticks out along local X (pivot at the base); rotate about local Z (the door normal).
        // Pick the rotation sign that moves the lever tip DOWN.
        var b = mf.sharedMesh.bounds;
        float tipX = Mathf.Abs(b.max.x) >= Mathf.Abs(b.min.x) ? b.max.x : b.min.x;
        var tip = new Vector3(tipX, b.center.y, b.center.z);
        float yPos = transform.TransformPoint(Quaternion.AngleAxis(10f, Vector3.forward) * tip).y;
        float yNeg = transform.TransformPoint(Quaternion.AngleAxis(-10f, Vector3.forward) * tip).y;
        _sign = yPos < yNeg ? 1f : -1f;
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor) return;
        _holder = args.interactorObject;
        _startY = _holder.GetAttachTransform(Grab).position.y;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        if (args.interactorObject == _holder) _holder = null;
    }

    private void Update()
    {
        if (_holder != null && Grab.isSelected)
        {
            float dy = _holder.GetAttachTransform(Grab).position.y - _startY; // negative = hand moved down
            Press = Mathf.Clamp01(-dy / Mathf.Max(0.005f, fullPressDistance));
        }
        else
        {
            Press = 0f;
        }

        if (_visual == null) return;
        float target = Press * maxAngle;
        _angle = Mathf.Lerp(_angle, target, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
        _visual.localRotation = Quaternion.AngleAxis(_sign * _angle, Vector3.forward);
    }
}
