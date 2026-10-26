using UnityEngine;

/// <summary>
/// The car body rocking up off one corner on a jack. "Lift" is how far the jacked corner's hub is
/// above where it sits on four good tyres: negative = sagging on the flat tyre, positive = jacked up.
/// The body tilts around the opposite (diagonal) wheel, so the two other corners move half as much,
/// and the other wheels stay on the floor (suspension). The jacked corner itself (wheel, nuts,
/// studs) lives on a separate un-mirrored object that is kept glued to the hub.
/// Put it on the car.
/// </summary>
public class CarLift : MonoBehaviour
{
    [Tooltip("The wheel diagonally opposite the jacked corner - the body pivots around it.")]
    [SerializeField] private Transform pivotWheel;
    [Tooltip("Wheels that stay on the floor while the body moves (all except the jacked one).")]
    [SerializeField] private Transform[] plantedWheels;
    [Tooltip("Car-local position of the jacked corner's hub.")]
    [SerializeField] private Vector3 hubLocal = new Vector3(-0.671f, -0.070f, -1.239f);
    [Tooltip("Un-mirrored stand-in for the jacked corner - follows the hub.")]
    [SerializeField] private Transform corner;
    [Tooltip("Lift at the start: negative = the car sags on the flat tyre.")]
    [SerializeField] private float restLift = -0.059f;
    [Tooltip("Un-mirrored things on the ground that keep their place relative to the car when it drives (jack spot).")]
    [SerializeField] private Transform[] followers;

    [Header("Level pose (captured once in the editor)")]
    [SerializeField] private bool captured;
    [SerializeField] private Vector3 levelPos;
    [SerializeField] private Quaternion levelRot = Quaternion.identity;
    [SerializeField] private Vector3[] plantedWorld;

    /// <summary>Current lift of the jacked hub (m).</summary>
    public float Lift => _lift;
    /// <summary>Where the car rests on its own: restLift on the flat tyre, 0 once a good tyre is on.</summary>
    public float BaseLift { get; set; }
    public float RestLift => restLift;

    private float _lift;
    private Vector3 _pivot;
    private float _width, _length, _sideSign, _endSign;
    private Vector3[] _followPos;
    private Quaternion[] _followRot;

    private void Awake()
    {
        if (!captured) CaptureLevel();
        Setup();
        BaseLift = restLift;
        SetLift(restLift);
        int n = followers != null ? followers.Length : 0;
        _followPos = new Vector3[n]; _followRot = new Quaternion[n];
        for (int i = 0; i < n; i++)
        {
            if (followers[i] == null) continue;
            _followPos[i] = Quaternion.Inverse(levelRot) * (followers[i].position - levelPos);
            _followRot[i] = Quaternion.Inverse(levelRot) * followers[i].rotation;
        }
    }

    /// <summary>The car drove: turn it by yawDegrees around pivot (world) and then shift it by move.
    /// Everything that keeps its place relative to the car comes along.</summary>
    public void Drive(Vector3 pivot, float yawDegrees, Vector3 move)
    {
        Quaternion q = Quaternion.AngleAxis(yawDegrees, Vector3.up);
        levelPos = pivot + q * (levelPos - pivot) + move;
        levelRot = Quaternion.Normalize(q * levelRot);           // re-normalise: thousands of small turns add up rounding error
        _pivot = pivot + q * (_pivot - pivot) + move; _pivot.y = 0f;
        if (plantedWorld != null)
            for (int i = 0; i < plantedWorld.Length; i++) plantedWorld[i] = pivot + q * (plantedWorld[i] - pivot) + move;
        SetLift(_lift);
        if (followers != null && _followPos != null)
            for (int i = 0; i < followers.Length && i < _followPos.Length; i++)
                if (followers[i] != null) followers[i].SetPositionAndRotation(levelPos + levelRot * _followPos[i], levelRot * _followRot[i]);
    }

    /// <summary>The car's ground-level frame (where it stands, which way it faces).</summary>
    public Vector3 LevelPosition => levelPos;
    public Quaternion LevelRotation => levelRot;

    [ContextMenu("Capture level pose")]
    public void CaptureLevel()
    {
        levelPos = transform.position;
        levelRot = transform.rotation;
        plantedWorld = new Vector3[plantedWheels != null ? plantedWheels.Length : 0];
        for (int i = 0; i < plantedWorld.Length; i++) if (plantedWheels[i] != null) plantedWorld[i] = plantedWheels[i].position;
        captured = true;
    }

    private void Setup()
    {
        // geometry in the level pose
        Matrix4x4 level = Matrix4x4.TRS(levelPos, levelRot, transform.lossyScale);
        Vector3 hub = level.MultiplyPoint3x4(hubLocal);
        Vector3 piv = pivotWheel != null ? LevelPointOf(pivotWheel) : levelPos;
        _pivot = new Vector3(piv.x, 0f, piv.z);
        _width = Mathf.Max(0.1f, Mathf.Abs(hub.x - piv.x));
        _length = Mathf.Max(0.1f, Mathf.Abs(hub.z - piv.z));
        _sideSign = Mathf.Sign(hub.x - piv.x);
        _endSign = Mathf.Sign(piv.z - hub.z);
    }

    // where a child sits in the level pose (works whatever pose the car is in now)
    private Vector3 LevelPointOf(Transform t)
    {
        Vector3 local = transform.InverseTransformPoint(t.position);
        return Matrix4x4.TRS(levelPos, levelRot, transform.lossyScale).MultiplyPoint3x4(local);
    }

    private Quaternion RotationFor(float lift)
    {
        float a = Mathf.Atan(0.5f * lift / _width) * Mathf.Rad2Deg * _sideSign;   // roll: half the lift across the width
        float b = Mathf.Atan(0.5f * lift / _length) * Mathf.Rad2Deg * _endSign;   // pitch: half along the length
        return Quaternion.AngleAxis(a, levelRot * Vector3.forward) * Quaternion.AngleAxis(b, levelRot * Vector3.right);
    }

    /// <summary>World position a car-local point would have at this lift (no side effects).</summary>
    public Vector3 PointAt(Vector3 carLocal, float lift)
    {
        if (_width <= 0f) Setup();
        Quaternion q = RotationFor(lift);
        Vector3 pos = _pivot + q * (levelPos - _pivot);
        return Matrix4x4.TRS(pos, q * levelRot, transform.lossyScale).MultiplyPoint3x4(carLocal);
    }

    public void SetLift(float lift)
    {
        if (_width <= 0f) Setup();
        _lift = lift;
        Quaternion q = RotationFor(lift);
        transform.SetPositionAndRotation(_pivot + q * (levelPos - _pivot), q * levelRot);
        if (plantedWheels != null)
            for (int i = 0; i < plantedWheels.Length && i < plantedWorld.Length; i++)
                if (plantedWheels[i] != null) plantedWheels[i].position = plantedWorld[i];
        SyncCorner();
    }

    public void SyncCorner()
    {
        if (corner != null) corner.SetPositionAndRotation(transform.TransformPoint(hubLocal), transform.rotation);
    }

    /// <summary>Editor helper: show the start (sagging) pose in the scene view.</summary>
    [ContextMenu("Preview rest pose")]
    public void PreviewRest()
    {
        if (!captured) CaptureLevel();
        Setup();
        SetLift(restLift);
    }
}
