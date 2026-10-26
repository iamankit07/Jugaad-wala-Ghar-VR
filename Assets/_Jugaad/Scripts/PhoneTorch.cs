using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Phone flashlight: TRIGGER (activate) while holding the phone toggles a spotlight on the back
/// of the phone, shining where the back points (away from the player).
/// - Only works where the level turns it on (torchAvailable), so level 1 is unchanged.
/// - Ignores TRIGGER while it belongs to the phone call (ringing / in call).
/// - In the pocket the light is off (the pocket covers it); it comes back on when taken out.
/// Put it on the phone, next to IntroPhoneCall / PhoneSummon.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class PhoneTorch : MonoBehaviour
{
    [Tooltip("Off in levels that don't use the torch (TRIGGER then does nothing).")]
    [SerializeField] private bool torchAvailable = false;
    [Tooltip("Lens position on the back of the phone (local space; the screen faces +Z).")]
    [SerializeField] private Vector3 lensLocalPos = new Vector3(0.022f, 0.058f, -0.006f);
    [SerializeField] private Color color = new Color(1f, 0.97f, 0.9f);
    [SerializeField] private float intensity = 9f;
    [SerializeField] private float range = 12f;
    [SerializeField] private float spotAngle = 70f;
    [SerializeField] private float innerSpotAngle = 38f;
    [Tooltip("Small glowing dot on the back of the phone while the torch is on (optional).")]
    [SerializeField] private Material lensMaterial;
    [SerializeField] private AudioClip clickSound;
    [SerializeField] private float haptic = 0.25f;

    public bool Available
    {
        get => torchAvailable;
        set { torchAvailable = value; if (!value && IsOn) Set(false); }
    }
    public bool IsOn { get; private set; }
    public bool InPocket => _pocket != null && _pocket.IsPocketed;
    /// <summary>Raised whenever the torch is switched (true = on).</summary>
    public event System.Action<bool> Toggled;

    private XRGrabInteractable _grab;
    private IntroPhoneCall _call;
    private PhoneSummon _pocket;
    private Light _light;
    private Renderer _lens;

    private void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _call = GetComponent<IntroPhoneCall>();
        _pocket = GetComponent<PhoneSummon>();

        var lt = new GameObject("TorchLight");
        lt.transform.SetParent(transform, false);
        lt.transform.localPosition = lensLocalPos;
        lt.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // shine out of the back (-Z)
        _light = lt.AddComponent<Light>();
        _light.type = LightType.Spot;
        _light.color = color;
        _light.intensity = intensity;
        _light.range = range;
        _light.spotAngle = spotAngle;
        _light.innerSpotAngle = innerSpotAngle;
        _light.shadows = LightShadows.None; // cheap on Quest
        _light.enabled = false;

        if (lensMaterial != null)
        {
            var lens = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lens.name = "TorchLens";
            Destroy(lens.GetComponent<Collider>());
            lens.transform.SetParent(transform, false);
            lens.transform.localPosition = lensLocalPos + new Vector3(0f, 0f, -0.0008f);
            lens.transform.localScale = Vector3.one * 0.009f;
            _lens = lens.GetComponent<Renderer>();
            _lens.sharedMaterial = lensMaterial;
            _lens.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lens.enabled = false;
        }
    }

    private void OnEnable() => _grab.activated.AddListener(OnActivated);
    private void OnDisable() => _grab.activated.RemoveListener(OnActivated);

    private void OnActivated(ActivateEventArgs args)
    {
        if (!torchAvailable) return;
        if (_call != null && _call.UsesTrigger) return; // TRIGGER answers the call, not the torch
        Set(!IsOn);
    }

    /// <summary>Switch the torch from code (tests, cut-scenes).</summary>
    public void Set(bool on)
    {
        if (IsOn == on) return;
        IsOn = on;
        SfxMix.PlayAt(clickSound != null ? clickSound : ProceduralSfx.Tick, transform.position, 0.6f);
        if (_grab.isSelected) XRHaptics.PulseHolders(_grab, haptic, 0.04f);
        Apply();
        Toggled?.Invoke(on);
    }

    private void LateUpdate() => Apply();

    private void Apply()
    {
        bool lit = IsOn && !InPocket;
        if (_light.enabled != lit) _light.enabled = lit;
        if (_lens != null && _lens.enabled != IsOn) _lens.enabled = IsOn;
    }
}
