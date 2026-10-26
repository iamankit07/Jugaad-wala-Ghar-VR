using System.Collections;
using UnityEngine;

/// <summary>
/// What happens when the key is turned. No battery: one dead "khat" from the starter relay and
/// nothing else - no lights, no engine. The first time that happens during the "start the car"
/// task, Papa calls a moment later and remembers he took the battery out to charge it.
/// Put it on the car.
/// </summary>
public class CarIgnition : MonoBehaviour
{
    [SerializeField] private IgnitionKey key;
    [SerializeField] private BatteryBay battery;
    [SerializeField] private IntroPhoneCall phone;
    [SerializeField] private AudioClip deadClick;
    [Tooltip("Recorded dead attempts (click + weak whirr) - a different one each turn.")]
    [SerializeField] private AudioClip[] deadAttempts;
    [SerializeField] private float callDelay = 1.4f;

    [Header("Papa's call")]
    [SerializeField] private string caller = "Papa";
    [SerializeField] private IntroPhoneCall.Line[] callEnglish;
    [SerializeField] private IntroPhoneCall.Line[] callHinglish;
    [SerializeField] private AudioClip voiceEnglish;
    [SerializeField] private AudioClip voiceHinglish;

    /// <summary>Set when the "start the car" task begins (before that, turning the key just clicks).</summary>
    public bool Armed { get; set; }
    public bool TriedWithoutBattery { get; private set; }
    public bool CallDone => _callStarted && phone != null && phone.Finished;
    public event System.Action TurnedWithBattery;

    private bool _callStarted, _callQueued;
    private int _nextDead;
    private float _lastDeadLength;

    private void Awake()
    {
        if (phone == null) phone = FindFirstObjectByType<IntroPhoneCall>();
    }

    private void OnEnable() { if (key != null) key.Turned += OnTurned; }
    private void OnDisable() { if (key != null) key.Turned -= OnTurned; }

    private void OnTurned()
    {
        bool hasBattery = battery != null && battery.Installed;
        if (hasBattery)
        {
            TurnedWithBattery?.Invoke();
            return;
        }
        // no battery: the recorded dead attempt (or just the relay click), then silence
        AudioClip dead = deadAttempts != null && deadAttempts.Length > 0 ? deadAttempts[_nextDead++ % deadAttempts.Length] : null;
        if (dead == null) dead = deadClick != null ? deadClick : ProceduralSfx.Tick;
        SfxMix.PlayAt(dead, key.transform.position, 1f, 0f);
        _lastDeadLength = dead.length;
        if (!Armed) return;
        TriedWithoutBattery = true;
        if (!_callQueued) { _callQueued = true; StartCoroutine(CallLater()); }
    }

    private IEnumerator CallLater()
    {
        float wait = Mathf.Max(callDelay, _lastDeadLength + 0.6f);   // let the failed attempt finish first
        if (phone != null) phone.ExpectCall(wait + 1f);
        yield return new WaitForSeconds(wait);
        if (phone != null) phone.StartCall(caller, callEnglish, callHinglish, voiceEnglish, voiceHinglish);
        _callStarted = true;   // only now: before this the phone still reports the LAST call as finished
        Debug.Log("[CarIgnition] no battery - Papa is calling");
    }
}
