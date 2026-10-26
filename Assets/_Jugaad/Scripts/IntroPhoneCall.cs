using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Story intro: the phone on the table rings ("Mummy calling..."), buzzes, and the player picks it up
/// and presses TRIGGER to answer. Mummy's intro line plays from the phone with timed subtitles on the
/// HUD (English or Hinglish, from GameLanguage). When the call ends, Finished becomes true and
/// GameFlowManager starts the first task.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class IntroPhoneCall : MonoBehaviour
{
    [System.Serializable]
    public class Line
    {
        [Tooltip("Seconds into the voice clip when this subtitle appears.")]
        public float time;
        [TextArea] public string text;
    }

    [SerializeField] private TaskHUD hud;
    [SerializeField] private string callerName = "Mummy";

    [Header("Audio")]
    [SerializeField] private AudioClip ringtone;
    [SerializeField, Range(0f, 1f)] private float ringVolume = 0.5f;
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private AudioClip hangupSound;
    [SerializeField] private AudioClip voiceEnglish;
    [SerializeField] private AudioClip voiceHinglish;
    [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.9f;

    [Header("Subtitles (timed to the voice clip)")]
    [SerializeField] private Line[] linesEnglish;
    [SerializeField] private Line[] linesHinglish;

    [Header("Phone screen")]
    [SerializeField] private Renderer screen;
    [SerializeField] private TMP_Text callerText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Color screenOff = new Color(0.02f, 0.02f, 0.03f);
    [SerializeField] private Color screenRinging = new Color(0.10f, 0.45f, 0.28f);
    [SerializeField] private Color screenInCall = new Color(0.08f, 0.16f, 0.32f);
    [SerializeField] private Color screenEnded = new Color(0.25f, 0.08f, 0.08f);

    [Header("Buzz")]
    [Tooltip("Visual child that shakes while ringing (not the rigidbody).")]
    [SerializeField] private Transform wiggleTarget;
    [SerializeField] private float wiggleDegrees = 2f;
    [SerializeField] private float hapticAmplitude = 0.3f;
    [SerializeField] private float reminderEvery = 20f;

    public bool Finished { get; private set; }
    public bool IsRinging => _state == State.Ringing;
    /// <summary>A call is queued, ringing or in progress - the level waits before showing a new task.</summary>
    public bool Busy => _state == State.Ringing || _state == State.InCall || Time.time < _expectedUntil;

    /// <summary>Say a call will start within the next few seconds (callers that ring after a delay).</summary>
    public void ExpectCall(float seconds) { _expectedUntil = Mathf.Max(_expectedUntil, Time.time + seconds); }
    private float _expectedUntil = -1f;
    /// <summary>True while TRIGGER belongs to the call (ringing / in call / the frame it was answered),
    /// so other phone features (torch) ignore that press.</summary>
    public bool UsesTrigger => _state == State.Ringing || _state == State.InCall || Time.frameCount == _answeredFrame;
    /// <summary>True once the call is over and its screen is cleared (the hint screen can take over).</summary>
    public bool ScreenFree { get; private set; }

    private enum State { Idle, Ringing, InCall, Ended }
    private State _state = State.Idle;
    private XRGrabInteractable _grab;
    private AudioSource _ring, _voice;
    private Material _screenMat;
    private Quaternion _wiggleRest;
    private float _ringStart, _nextHaptic, _nextReminder;
    private int _answeredFrame = -1;
    private PhoneSummon _pocket;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _pocket = GetComponent<PhoneSummon>();
        _ring = MakeSource(true, 1f);
        _voice = MakeSource(false, 0.5f);
        if (screen) _screenMat = screen.material; // own instance
        if (wiggleTarget) _wiggleRest = wiggleTarget.localRotation;
        SetScreen(screenOff, string.Empty, string.Empty);
    }

    private AudioSource MakeSource(bool loop, float spatial)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.spatialBlend = spatial;
        s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.minDistance = 0.8f;
        s.maxDistance = 12f;
        s.dopplerLevel = 0f;
        return s;
    }

    private void OnEnable()  { _grab.activated.AddListener(OnActivated); }
    private void OnDisable() { _grab.activated.RemoveListener(OnActivated); }

    /// <summary>Called by GameFlowManager after the title banner.</summary>
    public void StartRinging()
    {
        if (_state != State.Idle) return;
        _state = State.Ringing;
        _ringStart = Time.time;
        _nextReminder = Time.time + reminderEvery;
        if (ringtone) { _ring.clip = ringtone; _ring.volume = ringVolume; _ring.Play(); }
        SetScreen(screenRinging, callerName, GameLanguage.Pick("Incoming call...", "Call aa rahi hai..."));
        if (hud) hud.ShowBanner(GameLanguage.Pick("Your phone is ringing!", "Phone baj raha hai!"),
                                GameLanguage.Pick("Press B to take out your phone, then TRIGGER to answer", "B dabake phone nikalo, phir TRIGGER se uthao"));
    }

    /// <summary>A later call on the same phone (new caller lines, optional voice). Rings like the intro.</summary>
    public void StartCall(string caller, Line[] english, Line[] hinglish, AudioClip voiceEn, AudioClip voiceHi)
    {
        if (_state == State.Ringing || _state == State.InCall) return;
        StopAllCoroutines();
        if (!string.IsNullOrEmpty(caller)) callerName = caller;
        linesEnglish = english;
        linesHinglish = hinglish;
        voiceEnglish = voiceEn;
        voiceHinglish = voiceHi;
        Finished = false;
        ScreenFree = false;
        _state = State.Idle;
        StartRinging();
    }

    private void Update()
    {
        if (_state != State.Ringing) return;

        // buzz pattern: 0.45 s on, 0.55 s off
        float cycle = (Time.time - _ringStart) % 1f;
        bool buzzing = cycle < 0.45f;
        if (wiggleTarget)
            wiggleTarget.localRotation = buzzing
                ? _wiggleRest * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 70f) * wiggleDegrees)
                : _wiggleRest;

        if (buzzing && Time.time >= _nextHaptic)
        {
            if (_grab.isSelected)
            {
                _nextHaptic = Time.time + 0.15f;
                XRHaptics.PulseHolders(_grab, hapticAmplitude, 0.12f);
            }
            else if (_pocket != null && _pocket.IsPocketed && _pocket.Hand != null)
            {
                _nextHaptic = Time.time + 0.15f; // buzzing in the pocket: feel it on the right controller
                XRHaptics.Pulse(_pocket.Hand, hapticAmplitude * 0.8f, 0.12f);
            }
        }

        if (Time.time >= _nextReminder && hud)
        {
            _nextReminder = Time.time + reminderEvery;
            hud.ShowToast(GameLanguage.Pick(callerName + " is calling! Press B, then TRIGGER to answer",
                                            callerName + " ka phone hai! B dabao, phir TRIGGER se uthao"), hud.WarnColor, false);
        }
    }

    private void OnActivated(ActivateEventArgs args)
    {
        if (_state == State.Ringing) StartCoroutine(CallRoutine());
    }

    /// <summary>Answer from code (tests / fallback button).</summary>
    public void Answer()
    {
        if (_state == State.Ringing) StartCoroutine(CallRoutine());
    }

    private IEnumerator CallRoutine()
    {
        _state = State.InCall;
        _answeredFrame = Time.frameCount;
        _ring.Stop();
        if (wiggleTarget) wiggleTarget.localRotation = _wiggleRest;
        if (pickupSound) _voice.PlayOneShot(pickupSound, 0.6f);
        SetScreen(screenInCall, callerName, "00:00");
        if (hud) hud.ShowSubtitle(callerName, string.Empty);

        yield return new WaitForSeconds(0.4f);

        bool en = GameLanguage.IsEnglish;
        AudioClip clip = en ? (voiceEnglish ? voiceEnglish : voiceHinglish) : (voiceHinglish ? voiceHinglish : voiceEnglish);
        Line[] lines = en ? linesEnglish : linesHinglish;
        if (lines == null || lines.Length == 0) lines = en ? linesHinglish : linesEnglish;

        float length = clip ? clip.length : (lines != null && lines.Length > 0 ? lines[lines.Length - 1].time + 4f : 3f);
        if (clip) { _voice.clip = clip; _voice.volume = voiceVolume; _voice.Play(); }

        float start = Time.time;
        int next = 0;
        while (Time.time - start < length)
        {
            float t = Time.time - start;
            while (lines != null && next < lines.Length && t >= lines[next].time)
            {
                if (hud) hud.ShowSubtitle(callerName, lines[next].text);
                next++;
            }
            if (statusText) statusText.text = "00:" + Mathf.FloorToInt(t).ToString("00");
            yield return null;
        }

        yield return new WaitForSeconds(0.3f);
        if (hangupSound) _voice.PlayOneShot(hangupSound, 0.6f);
        SetScreen(screenEnded, callerName, GameLanguage.Pick("Call ended", "Call khatam"));
        if (hud) hud.ClearSubtitle();
        _state = State.Ended;
        Finished = true;

        yield return new WaitForSeconds(2.5f);
        SetScreen(screenOff, string.Empty, string.Empty);
        ScreenFree = true;
    }

    private void SetScreen(Color c, string caller, string status)
    {
        if (_screenMat)
        {
            if (_screenMat.HasProperty(BaseColorId)) _screenMat.SetColor(BaseColorId, c);
            else _screenMat.color = c;
        }
        if (callerText) callerText.text = caller;
        if (statusText) statusText.text = status;
    }
}
