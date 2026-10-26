using System.Collections;
using UnityEngine;

/// <summary>
/// A light switch wired to a fused bulb: pressing it makes the bulb sputter for a moment and die,
/// and tells the player the bulb is fused (so they look for another jugaad). Call Repair() later
/// if a task fixes the bulb; after that the switch works normally.
/// Hook a PokeToggleButton's onToggled (or a PokeActionButton's onPressed) to OnSwitch / Press.
/// </summary>
public class BulbFuse : MonoBehaviour
{
    [SerializeField] private Light bulbLight;
    [Tooltip("Lamp renderers + glowing materials to show while the bulb flickers / works (optional).")]
    [SerializeField] private Renderer[] lampRenderers;
    [SerializeField] private Material[] glowMaterials;
    [SerializeField] private AudioClip fizzSound;
    [SerializeField] private TaskHUD hud;
    [SerializeField] private float messageCooldown = 6f;

    public bool Repaired { get; private set; }
    public int DeadPresses { get; private set; }

    private Material[][] _offMaterials;
    private float _nextMessage;
    private Coroutine _flicker;
    private bool? _switchState;

    private void Awake()
    {
        if (hud == null) hud = FindFirstObjectByType<TaskHUD>();
        if (lampRenderers != null)
        {
            _offMaterials = new Material[lampRenderers.Length][];
            for (int i = 0; i < lampRenderers.Length; i++)
                if (lampRenderers[i] != null) _offMaterials[i] = lampRenderers[i].sharedMaterials;
        }
        if (bulbLight) bulbLight.enabled = false;
    }

    /// <summary>For PokeToggleButton.onToggled. Its Start() fires once with the initial state to sync;
    /// that is not a press, so the first call is only remembered.</summary>
    public void OnSwitch(bool on)
    {
        bool first = !_switchState.HasValue;
        _switchState = on;
        if (!first) Press();
    }

    public void Press()
    {
        if (Repaired)
        {
            SetLit(bulbLight == null || !bulbLight.enabled);
            return;
        }
        DeadPresses++;
        if (_flicker != null) StopCoroutine(_flicker);
        _flicker = StartCoroutine(Flicker());
        if (hud != null && Time.time >= _nextMessage)
        {
            _nextMessage = Time.time + messageCooldown;
            hud.ShowToast(GameLanguage.Pick("The bulb is fused! Find another way to get light.",
                                            "Bulb fuse hai! Roshni ka koi aur jugaad karo."), hud.WarnColor);
        }
    }

    /// <summary>Bulb fixed (a later task): the switch then turns it on/off normally.</summary>
    public void Repair()
    {
        Repaired = true;
        SetLit(true);
    }

    private IEnumerator Flicker()
    {
        Vector3 at = bulbLight ? bulbLight.transform.position : transform.position;
        SfxMix.PlayAt(fizzSound != null ? fizzSound : ProceduralSfx.Thunk, at, 0.5f);
        float[] pattern = { 0.05f, 0.09f, 0.03f, 0.14f, 0.02f };
        for (int i = 0; i < pattern.Length; i++)
        {
            SetLit(i % 2 == 0);
            yield return new WaitForSeconds(pattern[i]);
        }
        SetLit(false);
        _flicker = null;
    }

    private void SetLit(bool lit)
    {
        if (bulbLight) bulbLight.enabled = lit;
        if (lampRenderers == null || glowMaterials == null || glowMaterials.Length == 0) return;
        for (int i = 0; i < lampRenderers.Length; i++)
        {
            var r = lampRenderers[i];
            if (r == null || _offMaterials[i] == null) continue;
            if (!lit) { r.sharedMaterials = _offMaterials[i]; continue; }
            var mats = (Material[])_offMaterials[i].Clone();
            for (int m = 0; m < mats.Length && m < glowMaterials.Length; m++) if (glowMaterials[m] != null) mats[m] = glowMaterials[m];
            r.sharedMaterials = mats;
        }
    }
}
