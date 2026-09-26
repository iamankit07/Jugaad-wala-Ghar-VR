using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Turns a room's ceiling lights and fan on/off. Wire the light PokeToggleButton.onToggled to SetLight
/// and the fan button to FanSwitchPressed. The lights controlled here must be REALTIME.
/// The fan wall switch can be 'broken' (story): pressing it only gives a dead click; the remote
/// (ToggleFan / SetFan) still works.
/// </summary>
public class RoomPowerController : MonoBehaviour
{
    [Header("Lights")]
    [SerializeField] private Light[] lights;
    [Tooltip("Lamp fixture meshes - swapped to a glowing / dark material.")]
    [SerializeField] private Renderer[] fixtures;
    [SerializeField] private Material fixtureOn;
    [SerializeField] private Material fixtureOff;

    [Header("Fan")]
    [SerializeField] private FanSpin fan;

    [Header("Wall switches (kept in sync when a remote changes things)")]
    [SerializeField] private PokeToggleButton lightSwitch;
    [SerializeField] private PokeToggleButton fanSwitch;

    [Header("Broken fan switch")]
    [Tooltip("If on, the FAN wall switch does nothing (only the remote works).")]
    [SerializeField] private bool fanSwitchBroken = true;
    [SerializeField] private AudioClip deadClickSound;
    public UnityEvent onFanSwitchDead;

    [Header("Start state")]
    [SerializeField] private bool lightStartsOn = false;
    [SerializeField] private bool fanStartsOn = false;

    public bool LightOn { get; private set; }
    public bool FanOn { get; private set; }
    /// <summary>How many times the broken fan switch has been pressed.</summary>
    public int DeadFanPresses { get; private set; }

    private void Awake()
    {
        SetLight(lightStartsOn);
        SetFan(fanStartsOn);
    }

    public void SetLight(bool on)
    {
        LightOn = on;
        if (lights != null)
            foreach (var l in lights) if (l) l.enabled = on;
        if (fixtures != null)
            foreach (var r in fixtures) if (r) r.sharedMaterial = on ? fixtureOn : fixtureOff;
        if (lightSwitch) lightSwitch.SetState(on);
    }

    public void SetFan(bool on)
    {
        FanOn = on;
        if (fan) fan.SetOn(on);
        if (fanSwitch) fanSwitch.SetState(on);
    }

    /// <summary>Wire the FAN wall switch here (PokeToggleButton.onToggled).</summary>
    public void FanSwitchPressed(bool on)
    {
        if (!fanSwitchBroken) { SetFan(on); return; }

        // Broken: snap the switch back to the real fan state.
        if (fanSwitch) fanSwitch.SetState(FanOn);

        // The button's Start() pushes its initial state (== FanOn) - that is not a press.
        if (on == FanOn) return;

        DeadFanPresses++;
        var clip = deadClickSound != null ? deadClickSound : ProceduralSfx.Thunk;
        var at = fanSwitch ? fanSwitch.transform.position : transform.position;
        SfxMix.PlayAt(clip, at, 0.7f);
        onFanSwitchDead?.Invoke();
    }

    public void ToggleLight() => SetLight(!LightOn);
    public void ToggleFan() => SetFan(!FanOn);
}
