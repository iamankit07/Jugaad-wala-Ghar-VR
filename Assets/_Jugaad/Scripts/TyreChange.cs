using System.Collections;
using UnityEngine;

/// <summary>
/// Runs the flat-tyre part of the garage level (jack the car up / undo the nuts and pull the wheel /
/// fit a good wheel and let the car down) and keeps all of it working afterwards: the jack can be
/// wound up and down and moved, nuts undone and done up, and any garage wheel put on or taken off,
/// as often as the player likes - within what makes sense (no wheel off while the car stands on it,
/// no lowering onto a hub without a wheel...). Also: Papa's call about the puncture, and the engine
/// going off when you get out of the car.
/// Put it on the car (next to CarLift).
/// </summary>
public class TyreChange : MonoBehaviour
{
    public enum Stage { None, Jack, Wheel, Spare, Done }

    [Header("Parts")]
    [SerializeField] private CarLift lift;
    [SerializeField] private ScissorJack jack;
    [SerializeField] private SnapSpot jackSpot;
    [SerializeField] private LugNut[] nuts;
    [SerializeField] private WheelHub hub;

    [Header("Car")]
    [SerializeField] private CarEngine engine;
    [SerializeField] private CarSeat seat;

    [Header("Papa's call (start of the jack task)")]
    [SerializeField] private IntroPhoneCall phone;
    [SerializeField] private float callDelay = 1.5f;
    [SerializeField] private string caller = "Papa";
    [SerializeField] private IntroPhoneCall.Line[] callEnglish;
    [SerializeField] private IntroPhoneCall.Line[] callHinglish;
    [SerializeField] private AudioClip voiceEnglish;
    [SerializeField] private AudioClip voiceHinglish;

    [SerializeField] private TaskHUD hud;

    public Stage Current { get; private set; }
    public ScissorJack Jack => jack;
    public SnapSpot JackSpot => jackSpot;
    public WheelHub Hub => hub;
    public LugNut[] Nuts => nuts;
    public bool JackDone => jack != null && jack.Placed && jack.CarFullyUp;
    public bool WheelDone => _flatRemoved;
    public bool SpareDone => hub != null && hub.Mounted != null && !hub.Mounted.IsFlat && AllTight && jack != null && jack.LoweredAway;

    private bool _flatRemoved, _toldNutsOff, _toldNutsTight;

    private bool AllTight { get { if (nuts == null) return false; foreach (var n in nuts) if (n == null || !n.IsTight) return false; return true; } }
    private bool AllOff { get { if (nuts == null) return false; foreach (var n in nuts) if (n == null || !n.IsOff) return false; return true; } }

    private void Awake()
    {
        if (hud == null) hud = FindFirstObjectByType<TaskHUD>();
        if (phone == null) phone = FindFirstObjectByType<IntroPhoneCall>();
        if (hub != null)
        {
            hub.RemoveBlocked = WhyWheelStays;
            hub.MountBlocked = WhyNoMount;
        }
    }

    private void OnEnable()
    {
        if (seat != null) seat.Exited += OnGotOut;
        if (jack != null) jack.Hint += Toast;
        if (hub != null) { hub.WheelMounted += OnWheelOn; hub.WheelRemoved += OnWheelOff; }
    }

    private void OnDisable()
    {
        if (seat != null) seat.Exited -= OnGotOut;
        if (jack != null) jack.Hint -= Toast;
        if (hub != null) { hub.WheelMounted -= OnWheelOn; hub.WheelRemoved -= OnWheelOff; }
    }

    // ignition off on the way out (you don't jack up a running car)
    private void OnGotOut()
    {
        if (engine != null && engine.Started) engine.SwitchOff();
    }

    public void Begin(Stage s)
    {
        if (s < Current) return;
        Current = s;
        if (s == Stage.Jack)
        {
            if (jackSpot != null) jackSpot.Armed = true;
            StartCoroutine(PapaCalls());
        }
        Debug.Log("[TyreChange] stage " + s);
    }

    public bool IsDone(Stage s)
    {
        switch (s)
        {
            case Stage.Jack: return JackDone || Current > Stage.Jack;
            case Stage.Wheel: return WheelDone;
            case Stage.Spare: return SpareDone || Current == Stage.Done;
        }
        return false;
    }

    // ---------------- rules, every frame ----------------
    private void Update()
    {
        // the car rests on whatever is on the hub
        if (lift != null && hub != null && hub.Mounted != null)
            lift.BaseLift = hub.Mounted.IsFlat ? lift.RestLift : 0f;

        UpdateNuts();
        UpdateJack();

        if (Current == Stage.Wheel && !_toldNutsOff && AllOff && hub != null && hub.Mounted != null)
        {
            _toldNutsOff = true;
            Toast(GameLanguage.Pick("All nuts off - now pull the wheel out!", "Saare nut khul gaye - ab tyre pakad ke bahar kheencho!"), true);
        }
        if (Current == Stage.Spare && hub != null && hub.Mounted != null && !hub.Mounted.IsFlat && !_toldNutsTight && AllTight)
        {
            _toldNutsTight = true;
            Toast(GameLanguage.Pick("All tight! Now wind the jack the other way to let the car down.", "Sab tight! Ab jack ka handle ulta ghuma ke gaadi neeche karo."), true);
        }
        if (Current == Stage.Spare && SpareDone)
        {
            Current = Stage.Done;
            if (engine != null) engine.ClearWarning();
            Debug.Log("[TyreChange] all done - free play");
        }
    }

    private void UpdateNuts()
    {
        if (nuts == null) return;
        bool turnable = Current >= Stage.Wheel;
        bool pop = Current != Stage.Spare;          // while fitting the new wheel a wrong turn shouldn't drop the nut again
        string locked = Current == Stage.Jack ? GameLanguage.Pick("Jack the car up first!", "Pehle jack se gaadi upar uthao!") : null;
        foreach (var n in nuts)
        {
            if (n == null) continue;
            n.CanTurn = turnable;
            n.PopAllowed = pop;
            n.LockedHint = locked;
        }
    }

    private void UpdateJack()
    {
        if (jack == null) return;
        bool wheelOn = hub != null && hub.Mounted != null;
        switch (Current)
        {
            case Stage.None:
                jack.CanRaise = false; jack.CanLower = false;
                break;
            case Stage.Jack:
                jack.CanRaise = true;
                jack.CanLower = false;                    // turning it the other way = tell them which way is up
                jack.GrabHint = GameLanguage.Pick("Wind the knob round CLOCKWISE (or twist your wrist) to lift the car!", "Knob ko CLOCKWISE gol-gol ghumao (ya kalai ghumao) - gaadi uthegi!");
                jack.LowerBlockedHint = GameLanguage.Pick("Other way - clockwise lifts the car!", "Doosri taraf ghumao - clockwise se gaadi upar uthegi!");
                break;
            case Stage.Wheel:
                jack.CanRaise = true;
                jack.CanLower = !jack.CarriesCar;
                jack.LowerBlockedHint = GameLanguage.Pick("Not yet - change the tyre first!", "Abhi neeche mat karo - pehle tyre badlo!");
                break;
            default:   // fitting the new wheel, and free play afterwards
                jack.CanRaise = true;
                jack.CanLower = !jack.CarriesCar || (wheelOn && AllTight);
                jack.LowerBlockedHint = !wheelOn
                    ? GameLanguage.Pick("Put a wheel on first - the car would drop on the hub!", "Pehle tyre lagao - warna gaadi hub pe gir jaayegi!")
                    : GameLanguage.Pick("Tighten all the nuts first!", "Pehle saare nut tight karo!");
                break;
        }
        jack.RaiseBlockedHint = null;
        if (Current != Stage.Jack) jack.GrabHint = null;
        jack.WrongWayHint = GameLanguage.Pick("Turn it CLOCKWISE to lift the car!", "CLOCKWISE ghumao - tab gaadi uthegi!");
        jack.AllowRemove = wheelOn;                // the jack only comes away when the car stands on a wheel
    }

    /// <summary>The wheel on the hub is the punctured one.</summary>
    public bool FlatOn => hub != null && hub.Mounted != null && hub.Mounted.IsFlat;

    /// <summary>Why the car can't be driven (null = it can).</summary>
    public string DriveBlockReason()
    {
        if (hub != null && hub.Mounted == null) return GameLanguage.Pick("There's no wheel on the back right!", "Peeche daayein taraf tyre hi nahi laga!");
        if (!AllTight) return GameLanguage.Pick("Tighten all the wheel nuts first!", "Pehle tyre ke saare nut tight karo!");
        if (lift != null && lift.Lift > lift.BaseLift + 0.003f) return GameLanguage.Pick("Let the car down off the jack first!", "Pehle jack se gaadi neeche utaaro!");
        return null;
    }

    // ---------------- wheels ----------------
    private string WhyWheelStays()
    {
        if (!AllOff) return GameLanguage.Pick("Undo all the nuts first!", "Pehle saare nut kholo!");
        if (!WheelClear(hub.Mounted)) return GameLanguage.Pick("Jack the car up first - the tyre is standing on the floor!", "Pehle jack se gaadi uthao - tyre zameen pe tika hai!");
        return null;
    }

    private string WhyNoMount(CarWheel w)
    {
        if (w == null) return null;
        if (!WheelClear(w)) return GameLanguage.Pick("Raise the car a bit more first!", "Gaadi thodi aur upar uthao!");
        return null;
    }

    // would this wheel on the hub hang clear of the floor?
    private bool WheelClear(CarWheel w)
    {
        if (w == null || hub == null) return true;
        return hub.transform.position.y - w.Radius > 0.015f;     // a little gap under the tyre
    }

    private void OnWheelOff(CarWheel w)
    {
        if (w != null && w.IsFlat) _flatRemoved = true;
    }

    private void OnWheelOn(CarWheel w)
    {
        _toldNutsTight = false;
        float d = 0.3f;
        foreach (var n in nuts) if (n != null && n.IsOff) { n.ReturnLoose(d); d += 0.18f; }
        if (w.IsFlat && Current == Stage.Spare)
            Toast(GameLanguage.Pick("That's the punctured one! Fit a good wheel.", "Yeh to puncture wala tyre hai! Achha wala lagao."), false);
        else
            Toast(GameLanguage.Pick("Wheel's on! Tighten the nuts - clockwise.", "Tyre lag gaya! Ab nut tight karo - clockwise."), true);
    }

    private IEnumerator PapaCalls()
    {
        if (phone != null) phone.ExpectCall(callDelay + 1f);
        yield return new WaitForSeconds(callDelay);
        if (phone != null) phone.StartCall(caller, callEnglish, callHinglish, voiceEnglish, voiceHinglish);
    }

    private void Toast(string msg) { Toast(msg, false); }

    private void Toast(string msg, bool good)
    {
        if (hud == null || string.IsNullOrEmpty(msg)) return;
        hud.ShowToast(msg, good ? hud.GoodColor : hud.WarnColor, !good);
    }
}
