using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// The UI rays on the hands only exist when they are needed:
/// - pause / settings menu open  -> both rays on (with a visible line) so the menu can be clicked,
/// - menu closed                 -> rays off, so the triggers never click UI by accident (driving!).
///   (Optional: with a settingsIcon set, pointing a hand at it brings that hand's ray back -
///   not used now, the menu only opens with the left MENU button.)
/// Put it next to PauseMenu. Finds the 'UI Ray' interactors on the rig by itself.
/// </summary>
public class MenuRays : MonoBehaviour
{
    [SerializeField] private PauseMenu menu;
    [SerializeField] private RectTransform settingsIcon;
    [Tooltip("Degrees between where the hand points and the icon for its ray to come on.")]
    [SerializeField] private float aimAngle = 8f;
    [Tooltip("Once on, the ray stays until the hand points further away than this (no flicker).")]
    [SerializeField] private float releaseAngle = 14f;
    [SerializeField] private float maxDistance = 6f;

    private XRRayInteractor[] _rays;
    private bool[] _on;

    private void Awake()
    {
        if (menu == null) menu = GetComponent<PauseMenu>();
        var all = FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var list = new System.Collections.Generic.List<XRRayInteractor>();
        foreach (var r in all) if (r.name == "UI Ray") list.Add(r);
        _rays = list.ToArray();
        _on = new bool[_rays.Length];
        for (int i = 0; i < _rays.Length; i++) _rays[i].gameObject.SetActive(false);
    }

    private void Update()
    {
        bool open = menu != null && menu.IsOpen;
        for (int i = 0; i < _rays.Length; i++)
        {
            var r = _rays[i];
            if (r == null) continue;
            bool want = open || Aiming(r.transform, _on[i]);
            if (want == _on[i]) continue;
            _on[i] = want;
            r.gameObject.SetActive(want);
        }
    }

    private bool Aiming(Transform hand, bool already)
    {
        if (settingsIcon == null || !settingsIcon.gameObject.activeInHierarchy) return false;
        Vector3 to = settingsIcon.position - hand.position;
        if (to.magnitude > maxDistance) return false;
        return Vector3.Angle(hand.forward, to) < (already ? releaseAngle : aimAngle);
    }

    /// <summary>Is this hand's ray on right now (tests).</summary>
    public int RaysOn { get { int n = 0; foreach (var b in _on) if (b) n++; return n; } }
}
