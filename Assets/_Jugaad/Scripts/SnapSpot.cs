using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// "Socket" feel for big parts (the jack): while you carry the item near its place, a see-through
/// ghost shows exactly where it goes (glowing brighter once you're close enough). Let go there and
/// it glides in. Only while Armed. If removable, it can be picked up again whenever CanRemove says so.
/// Put it on an empty at the item's final pose (item root lines up with it).
/// </summary>
public class SnapSpot : MonoBehaviour
{
    [SerializeField] private XRGrabInteractable item;
    [Tooltip("Parent of the item once it's in (empty = this).")]
    [SerializeField] private Transform parentWhenSnapped;
    [SerializeField] private float ghostDistance = 1.2f;
    [SerializeField] private float snapDistance = 0.3f;
    [Tooltip("Local offset the item slides in from at the end (e.g. 0.12 outwards along an axle).")]
    [SerializeField] private Vector3 approachLocal = Vector3.zero;
    [SerializeField] private float slideTime = 0.35f;
    [Tooltip("Can it be taken out again (when CanRemove allows)?")]
    [SerializeField] private bool removable = true;
    [SerializeField] private Material ghostMaterial;
    [SerializeField] private Color farColor = new Color(1f, 0.85f, 0.45f, 0.22f);
    [SerializeField] private Color nearColor = new Color(0.45f, 1f, 0.55f, 0.45f);
    [SerializeField] private AudioClip snapSound;
    [SerializeField, Range(0f, 1f)] private float snapVolume = 0.9f;

    public bool Armed { get; set; }
    public bool Snapped { get; private set; }
    public XRGrabInteractable Item => item;
    /// <summary>May the item be taken out right now? (null = never)</summary>
    public System.Func<bool> CanRemove;
    public event System.Action OnSnapped;
    public event System.Action OnRemoved;

    private GameObject _ghost;
    private Material _ghostMat;
    private bool _near, _throwWas, _snapping;
    private readonly List<Renderer> _ghostRenderers = new List<Renderer>();

    private void Awake()
    {
        BuildGhost();
        if (item != null)
            item.selectFilters.Add(new XRSelectFilterDelegate((i, t) =>
                !Snapped || i is XRSocketInteractor || (removable && !_snapping && CanRemove != null && CanRemove())));
    }

    private void OnEnable()
    {
        if (item != null) { item.selectExited.AddListener(OnReleased); item.selectEntered.AddListener(OnGrabbed); }
    }

    private void OnDisable()
    {
        if (item != null) { item.selectExited.RemoveListener(OnReleased); item.selectEntered.RemoveListener(OnGrabbed); }
    }

    private void BuildGhost()
    {
        if (item == null) return;
        _ghost = new GameObject("Ghost_" + item.name);
        _ghost.transform.SetParent(transform, false);
        _ghostMat = ghostMaterial != null ? new Material(ghostMaterial) : null;
        Matrix4x4 toItem = item.transform.worldToLocalMatrix;
        foreach (var mf in item.GetComponentsInChildren<MeshFilter>())
        {
            var src = mf.GetComponent<MeshRenderer>();
            if (src == null || !src.enabled || mf.sharedMesh == null) continue;
            var g = new GameObject(mf.name);
            g.transform.SetParent(_ghost.transform, false);
            Matrix4x4 m = toItem * mf.transform.localToWorldMatrix;            // pose relative to the item root
            g.transform.localPosition = m.GetColumn(3);
            g.transform.localRotation = m.rotation;
            g.transform.localScale = m.lossyScale;
            g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r = g.AddComponent<MeshRenderer>();
            var mats = new Material[mf.sharedMesh.subMeshCount];
            for (int i = 0; i < mats.Length; i++) mats[i] = _ghostMat;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            _ghostRenderers.Add(r);
        }
        _ghost.SetActive(false);
    }

    private bool HeldByHand()
    {
        if (item == null || !item.isSelected) return false;
        foreach (var i in item.interactorsSelecting) if (!(i is XRSocketInteractor)) return true;
        return false;
    }

    private float Distance => item == null ? 999f : Vector3.Distance(item.transform.position, transform.position);

    private void Update()
    {
        if (_ghost == null) return;
        bool held = !Snapped && Armed && HeldByHand();
        bool show = held && Distance < ghostDistance;
        if (_ghost.activeSelf != show) _ghost.SetActive(show);

        bool near = held && Distance < snapDistance;
        if (near != _near)
        {
            _near = near;
            if (near)
            {
                _throwWas = item.throwOnDetach;
                item.throwOnDetach = false;                      // let go here = put it in, not throw it
                XRHaptics.PulseHolders(item, 0.3f, 0.05f);
            }
            else item.throwOnDetach = _throwWas;
        }
        if (show && _ghostMat != null)
        {
            Color c = near ? nearColor : farColor;
            c.a *= 0.75f + 0.25f * Mathf.Sin(Time.time * 6f);
            _ghostMat.SetColor("_BaseColor", c);
            if (_ghostMat.HasProperty("_EmissionColor")) _ghostMat.SetColor("_EmissionColor", new Color(c.r, c.g, c.b) * (near ? 0.6f : 0.25f));
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (!Snapped || _snapping || args.interactorObject is XRSocketInteractor) return;
        // taken back out
        Snapped = false;
        foreach (var r in item.GetComponents<ResetIfFallen>()) r.enabled = true;
        Debug.Log("[SnapSpot] " + item.name + " taken out");
        OnRemoved?.Invoke();
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (Snapped || args.interactorObject is XRSocketInteractor) return;
        if (Armed && Distance <= snapDistance) { StartCoroutine(SnapIn()); return; }
        var rb = item.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = false; rb.useGravity = true; }   // XRI would leave it hanging in the air
    }

    /// <summary>Put it in from code (tests, or a fallback).</summary>
    public void ForceSnap()
    {
        if (Snapped || item == null) return;
        Snapped = true;
        if (item.isSelected && item.interactionManager != null)
            foreach (var i in new List<IXRSelectInteractor>(item.interactorsSelecting))
                item.interactionManager.SelectExit(i, (IXRSelectInteractable)item);
        Snapped = false;
        StartCoroutine(SnapIn());
    }

    private IEnumerator SnapIn()
    {
        Snapped = true;
        _snapping = true;
        if (_ghost != null) _ghost.SetActive(false);
        yield return null;                                       // XRI finishes dropping it first
        var rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            rb.isKinematic = true;
        }
        foreach (var k in item.GetComponents<KinematicUntilGrabbed>()) k.enabled = false;
        foreach (var r in item.GetComponents<ResetIfFallen>()) r.enabled = false;
        item.throwOnDetach = _throwWas;
        if (!removable) item.enabled = false;
        Transform parent = parentWhenSnapped != null ? parentWhenSnapped : transform;
        item.transform.SetParent(parent, true);

        Vector3 p0 = item.transform.position; Quaternion r0 = item.transform.rotation;
        float glide = 0.22f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / glide)
        {
            float e = 1f - (1f - t) * (1f - t);
            item.transform.SetPositionAndRotation(Vector3.Lerp(p0, transform.TransformPoint(approachLocal), e),
                                                  Quaternion.Slerp(r0, transform.rotation, e));
            yield return null;
        }
        if (approachLocal.sqrMagnitude > 1e-6f)
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / slideTime)
            {
                item.transform.SetPositionAndRotation(transform.TransformPoint(Vector3.Lerp(approachLocal, Vector3.zero, t * t)), transform.rotation);
                yield return null;
            }
        }
        item.transform.SetPositionAndRotation(transform.position, transform.rotation);
        _snapping = false;
        SfxMix.PlayAt(snapSound != null ? snapSound : ProceduralSfx.Thunk, transform.position, snapVolume, 0.05f);
        XRHaptics.PulseNear(transform.position, 1.5f, 0.5f, 0.1f);
        Debug.Log("[SnapSpot] " + item.name + " in place");
        OnSnapped?.Invoke();
    }
}
