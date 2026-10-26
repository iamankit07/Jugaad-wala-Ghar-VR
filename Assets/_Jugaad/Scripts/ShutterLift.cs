using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Rolling shutter whose handle broke off - only a stub is left. Jugaad: grab the pliers, bring
/// their jaw to the stub (they clamp on), and lift your hand: the shutter follows. Past
/// <see cref="springLift"/> the spring takes over and it rolls up by itself to <see cref="stopLift"/>,
/// where it gets stuck (half open). Let go too early, or slide the pliers off sideways, and the
/// shutter slowly drops back down.
/// The slat sheet rolls into the roll box (it shrinks from the bottom), so it never pokes
/// through the ceiling. Every few cm a rattle click + a small haptic tick.
/// </summary>
public class ShutterLift : MonoBehaviour
{
    [Header("Shutter parts")]
    [Tooltip("Moves up with the lift: bottom bar, handle bracket, stub.")]
    [SerializeField] private Transform moving;
    [Tooltip("The slat sheet. Its top stays at topY; it shrinks as it rolls up.")]
    [SerializeField] private Transform curtain;
    [Tooltip("Broken handle stub - the pliers clamp here.")]
    [SerializeField] private Transform stub;
    [SerializeField] private float topY = 2.4f;

    [Header("Pliers")]
    [SerializeField] private XRGrabInteractable pliers;
    [Tooltip("Tip of the pliers' jaws.")]
    [SerializeField] private Transform pliersJaw;
    [SerializeField] private float clampRadius = 0.09f;
    [Tooltip("Pliers slide off if the jaw moves this far sideways from the stub (m).")]
    [SerializeField] private float slipDistance = 0.2f;

    [Header("Motion")]
    [Tooltip("Lift (m) after which the spring rolls it up by itself.")]
    [SerializeField] private float springLift = 1.0f;
    [Tooltip("Where it gets stuck - half open (m).")]
    [SerializeField] private float stopLift = 1.25f;
    [SerializeField] private float springSpeed = 0.9f;
    [SerializeField] private float fallSpeed = 0.45f;
    [SerializeField] private float ribSpacing = 0.06f;

    [Header("Feedback")]
    [SerializeField] private AudioClip clampSound;
    [SerializeField] private AudioClip ribSound;
    [SerializeField] private AudioClip stopSound;
    [SerializeField] private float ribHaptic = 0.15f;

    [Header("Daylight through the opening")]
    [SerializeField] private Light daylight;
    [SerializeField] private float daylightMax = 4f;
    [Tooltip("Added to the ambient sky colour when fully lifted.")]
    [SerializeField] private float ambientBoost = 0.35f;

    /// <summary>Only works while its task is active (call Arm from the task's onStart).</summary>
    public bool Armed { get; set; }
    /// <summary>Rolled up and stuck half open.</summary>
    public bool IsOpen { get; private set; }
    public float Lift => _lift;

    private float _lift, _lastRib, _clampJawY, _clampLift;
    private bool _clamped, _springing, _wentBelowSpring;
    private Renderer _curtainRenderer;
    private MaterialPropertyBlock _mpb;
    private Vector4 _baseST;
    private Color _ambSky, _ambEquator;
    private UnityEngine.Rendering.SphericalHarmonicsL2 _baseProbe;
    private static readonly int BaseStId = Shader.PropertyToID("_BaseMap_ST");

    private void Awake()
    {
        _curtainRenderer = curtain ? curtain.GetComponent<Renderer>() : null;
        _mpb = new MaterialPropertyBlock();
        if (_curtainRenderer && _curtainRenderer.sharedMaterial && _curtainRenderer.sharedMaterial.HasProperty(BaseStId))
            _baseST = _curtainRenderer.sharedMaterial.GetVector(BaseStId);
        else _baseST = new Vector4(1, 1, 0, 0);
        _ambSky = RenderSettings.ambientSkyColor;
        _ambEquator = RenderSettings.ambientEquatorColor;
        _baseProbe = RenderSettings.ambientProbe;
        if (daylight) daylight.enabled = false;
        Apply();
    }

    public void Arm() => Armed = true;

    private void Update()
    {
        if (_springing) return;
        bool held = pliers != null && pliers.isSelected;

        if (!_clamped)
        {
            if (Armed && held && pliersJaw && stub && Vector3.Distance(pliersJaw.position, stub.position) < clampRadius)
            {
                Clamp();
                return;
            }
            if (IsOpen) return;                              // rolled up: stays up until someone pulls it down
            if (_lift >= springLift) { StartCoroutine(SpringUp()); return; }   // let go high enough: the spring rolls it up
            if (_lift > 0f) // nothing holding it: slides back down
            {
                SetLift(Mathf.Max(0f, _lift - fallSpeed * Time.deltaTime));
                if (_lift <= 0f) Play(stopSound, 0.5f);
            }
            return;
        }

        if (!held) { Slip(); return; }
        Vector3 off = pliersJaw.position - stub.position; off.y = 0f;
        if (off.magnitude > slipDistance) { Slip(); return; }

        SetLift(Mathf.Clamp(_clampLift + (pliersJaw.position.y - _clampJawY), 0f, stopLift));
        if (_lift < stopLift - 0.02f) IsOpen = false;        // being pulled down again
        if (_lift < springLift - 0.02f) _wentBelowSpring = true;
        if (_lift >= springLift && _wentBelowSpring) StartCoroutine(SpringUp());
    }

    private void Clamp()
    {
        _clamped = true;
        _clampJawY = pliersJaw.position.y;
        _clampLift = _lift;
        _wentBelowSpring = _lift < springLift;
        Play(clampSound, 0.7f);
        XRHaptics.PulseHolders(pliers, 0.5f, 0.08f);
    }

    private void Slip()
    {
        _clamped = false;
        if (pliers != null && pliers.isSelected) XRHaptics.PulseHolders(pliers, 0.3f, 0.06f);
    }

    private IEnumerator SpringUp()
    {
        _clamped = false;
        _springing = true;
        while (_lift < stopLift)
        {
            SetLift(Mathf.Min(stopLift, _lift + springSpeed * Time.deltaTime));
            yield return null;
        }
        Play(stopSound, 0.9f);
        if (pliers != null && pliers.isSelected) XRHaptics.PulseHolders(pliers, 0.6f, 0.12f);
        _springing = false;
        IsOpen = true;
    }

    private void SetLift(float lift)
    {
        _lift = lift;
        if (Mathf.Abs(_lift - _lastRib) >= ribSpacing)
        {
            _lastRib = _lift;
            Play(ribSound, 0.35f);
            if (_clamped && pliers != null && pliers.isSelected) XRHaptics.PulseHolders(pliers, ribHaptic, 0.02f);
        }
        Apply();
    }

    private void Apply()
    {
        if (moving) moving.localPosition = new Vector3(moving.localPosition.x, _lift, moving.localPosition.z);
        if (curtain)
        {
            float h = Mathf.Max(0.05f, topY - _lift);
            var lp = curtain.localPosition; lp.y = h * 0.5f; curtain.localPosition = lp;
            var ls = curtain.localScale; ls.y = h; curtain.localScale = ls;
            if (_curtainRenderer)
            {
                _curtainRenderer.GetPropertyBlock(_mpb);
                _mpb.SetVector(BaseStId, new Vector4(_baseST.x, _baseST.y * h / topY, _baseST.z, _baseST.w));
                _curtainRenderer.SetPropertyBlock(_mpb);
            }
        }
        float t = stopLift > 0f ? Mathf.Clamp01(_lift / stopLift) : 0f;
        if (daylight)
        {
            daylight.enabled = t > 0.01f;
            daylight.intensity = daylightMax * t;
        }
        float b = ambientBoost * t;
        RenderSettings.ambientSkyColor = _ambSky + new Color(b, b, b, 0f);
        RenderSettings.ambientEquatorColor = _ambEquator + new Color(b * 0.6f, b * 0.6f, b * 0.6f, 0f);
        // Trilight colours alone don't always refresh the ambient probe in a build - set it directly.
        var sh = _baseProbe;
        if (b > 0f) sh.AddAmbientLight(new Color(b, b, b));
        RenderSettings.ambientProbe = sh;
    }

    private void Play(AudioClip clip, float vol)
    {
        if (clip == null) return;
        SfxMix.PlayAt(clip, stub ? stub.position : transform.position, vol);
    }

    private void OnDisable()
    {
        RenderSettings.ambientSkyColor = _ambSky;
        RenderSettings.ambientEquatorColor = _ambEquator;
        RenderSettings.ambientProbe = _baseProbe;
    }
}
