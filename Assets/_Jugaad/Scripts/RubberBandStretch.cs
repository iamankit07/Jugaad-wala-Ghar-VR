using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// A rubber band you can hold with one hand like any prop, or grab with BOTH hands and stretch:
/// each hand holds one end, the loop stretches (and thins) between them. Pull too far and it
/// slips out of one hand with a snap. Letting go while it is stretched raises StretchReleased,
/// which is how it gets put on something (see LeakingPipe).
/// The loop mesh is built here, so the band always matches the hands exactly.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class RubberBandStretch : MonoBehaviour
{
    [SerializeField] private MeshFilter loop;
    [Header("Shape (metres)")]
    [SerializeField] private float restHalfLength = 0.012f;
    [SerializeField] private float restEndRadius = 0.015f;
    [SerializeField] private float stretchedEndRadius = 0.004f;
    [SerializeField] private float thickness = 0.0021f;
    [Header("Stretch")]
    [Tooltip("Hand distance counted as 'stretched'.")]
    [SerializeField] private float stretchedSpan = 0.10f;
    [Tooltip("Beyond this the band slips out of one hand.")]
    [SerializeField] private float maxSpan = 0.42f;
    [SerializeField] private AudioClip snapClip;

    public bool TwoHanded => _two;
    public float Span => _span;
    public bool IsStretched => _two && _span >= stretchedSpan;
    public Vector3 Center => transform.position;
    public XRGrabInteractable Grab => _grab;

    /// <summary>Fired when a stretched band is let go (by either hand). Arg = band centre then.</summary>
    public event System.Action<Vector3> StretchReleased;

    private XRGrabInteractable _grab;
    private Mesh _mesh;
    private bool _two, _wasStretched;
    private float _span, _builtSpan = -1f;
    private Vector3 _lastCenter;
    private TaskHUD _hud;
    private float _nextSlipToast;
    private readonly List<IXRSelectInteractor> _tmp = new List<IXRSelectInteractor>();

    private void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _grab.selectMode = InteractableSelectMode.Multiple;
        _hud = FindFirstObjectByType<TaskHUD>();
        if (loop == null)
        {
            var go = new GameObject("Loop");
            go.transform.SetParent(transform, false);
            loop = go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
        }
        _mesh = new Mesh { name = "RubberBandLoop" };
        _mesh.MarkDynamic();
        loop.sharedMesh = _mesh;
        Build(0f);
    }

    private void OnEnable() => Application.onBeforeRender += FollowHands;
    private void OnDisable() => Application.onBeforeRender -= FollowHands;

    private void LateUpdate()
    {
        int n = _grab.isSelected ? _grab.interactorsSelecting.Count : 0;
        if (n >= 2)
        {
            if (!_two)
            {
                _two = true;
                _grab.trackPosition = false;     // we place it between the hands ourselves
                _grab.trackRotation = false;
            }
            FollowHands();
            if (_span > maxSpan) Slip();
        }
        else if (_two)
        {
            _two = false;
            _grab.trackPosition = true;
            _grab.trackRotation = true;
            bool fire = _wasStretched;
            _wasStretched = false;
            Build(0f);
            if (fire) StretchReleased?.Invoke(_lastCenter);
        }
    }

    private void FollowHands()
    {
        if (!_two || _grab.interactorsSelecting.Count < 2) return;
        Vector3 a = _grab.interactorsSelecting[0].GetAttachTransform(_grab).position;
        Vector3 b = _grab.interactorsSelecting[1].GetAttachTransform(_grab).position;
        Vector3 d = b - a;
        _span = d.magnitude;
        transform.position = (a + b) * 0.5f;
        if (_span > 1e-4f)
        {
            Vector3 dir = d / _span;
            Vector3 up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            // loop's long axis is local X
            transform.rotation = Quaternion.LookRotation(dir, up) * Quaternion.Euler(0f, -90f, 0f);
        }
        _lastCenter = transform.position;
        if (_span >= stretchedSpan) _wasStretched = true;
        if (Mathf.Abs(_span - _builtSpan) > 0.002f) Build(_span);
    }

    private void Slip()
    {
        _tmp.Clear();
        _tmp.AddRange(_grab.interactorsSelecting);
        var slipped = _tmp[_tmp.Count - 1];
        _wasStretched = false;                          // slipping off is not "putting it on"
        if (_grab.interactionManager != null) _grab.interactionManager.SelectExit(slipped, _grab);
        SfxMix.PlayAt(snapClip != null ? snapClip : SnapClip, transform.position, 0.8f, 0.1f);
        XRHaptics.Pulse(slipped, 0.8f, 0.08f);
        if (_hud != null && Time.time >= _nextSlipToast)
        {
            _nextSlipToast = Time.time + 5f;
            _hud.ShowToast(GameLanguage.Pick("Easy! Don't pull it that far.", "Arre! Itna mat kheencho."), _hud.WarnColor);
        }
    }

    /// <summary>Lets go of every hand without firing StretchReleased.</summary>
    public void ForceReleaseAll()
    {
        _wasStretched = false;
        _tmp.Clear();
        _tmp.AddRange(_grab.interactorsSelecting);
        if (_grab.interactionManager != null)
            for (int i = _tmp.Count - 1; i >= 0; i--) _grab.interactionManager.SelectExit(_tmp[i], _grab);
    }

    // ---------------- mesh: a stadium-shaped loop in local XY, long axis X ----------------
    private void Build(float span)
    {
        _builtSpan = span;
        float t = span <= 0f ? 0f : Mathf.Clamp01((span - 2f * (restHalfLength + restEndRadius)) / 0.12f);
        float r = Mathf.Lerp(restEndRadius, stretchedEndRadius, t);
        float half = span <= 0f ? restHalfLength : Mathf.Max(0f, span * 0.5f - r);
        float th = thickness * Mathf.Lerp(1f, 0.8f, t);     // thins as it stretches

        const int arc = 12, sides = 5;
        var path = new List<Vector3>();
        for (int i = 0; i <= arc; i++) { float a = -90f + 180f * i / arc; path.Add(new Vector3(half + r * Mathf.Cos(a * Mathf.Deg2Rad), r * Mathf.Sin(a * Mathf.Deg2Rad), 0f)); }
        for (int i = 0; i <= arc; i++) { float a = 90f + 180f * i / arc; path.Add(new Vector3(-half + r * Mathf.Cos(a * Mathf.Deg2Rad), r * Mathf.Sin(a * Mathf.Deg2Rad), 0f)); }
        int n = path.Count;
        var verts = new Vector3[n * sides];
        var norms = new Vector3[n * sides];
        var tris = new int[n * sides * 6];
        for (int i = 0; i < n; i++)
        {
            Vector3 tan = (path[(i + 1) % n] - path[(i - 1 + n) % n]).normalized;
            Vector3 side = Vector3.Cross(tan, Vector3.forward).normalized;
            for (int j = 0; j < sides; j++)
            {
                float ang = 2f * Mathf.PI * j / sides;
                Vector3 dir = Mathf.Cos(ang) * side + Mathf.Sin(ang) * Vector3.forward;
                verts[i * sides + j] = path[i] + dir * th;
                norms[i * sides + j] = dir;
            }
        }
        int k = 0;
        for (int i = 0; i < n; i++)
            for (int j = 0; j < sides; j++)
            {
                int i2 = (i + 1) % n, j2 = (j + 1) % sides;
                int a0 = i * sides + j, a1 = i * sides + j2, b0 = i2 * sides + j, b1 = i2 * sides + j2;
                tris[k++] = a0; tris[k++] = b0; tris[k++] = a1;   // outward-facing
                tris[k++] = a1; tris[k++] = b0; tris[k++] = b1;
            }
        _mesh.Clear();
        _mesh.vertices = verts;
        _mesh.normals = norms;
        _mesh.triangles = tris;
        _mesh.RecalculateBounds();
    }

    private static AudioClip _snap;
    public static AudioClip SnapClip
    {
        get
        {
            if (_snap != null) return _snap;
            const int rate = 22050; int len = (int)(rate * 0.07f);
            var data = new float[len];
            var rnd = new System.Random(3);
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / rate;
                float noise = (float)(rnd.NextDouble() * 2 - 1) * Mathf.Exp(-t * 90f);
                float body = Mathf.Sin(2f * Mathf.PI * 210f * t) * Mathf.Exp(-t * 55f);
                data[i] = (noise * 0.6f + body * 0.7f) * 0.8f;
            }
            _snap = AudioClip.Create("BandSnap", len, 1, rate, false);
            _snap.SetData(data, 0);
            return _snap;
        }
    }
}
