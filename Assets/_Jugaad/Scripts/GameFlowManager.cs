using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Runs the whole story as 6 tasks in a FIXED order. A task's mechanic is only unlocked
/// when that task is active, so the order can never break. Each finished task scores
/// points based on how fast it was done (clock starts once the task text is typed).
/// Tutorial tasks show their hint right away; later ones only after hintDelay seconds
/// (and taking a hint costs hintPenalty of that task's points).
///
/// Task order (Condition() depends on it):
///  0 TV on | 1 living-room door unlocked (it then stays open - no wedge task) |
///  2 bedroom door unlocked | 3 bedroom light on | 4 broken fan switch pressed | 5 fan on (remote)
/// </summary>
public class GameFlowManager : MonoBehaviour
{
    [System.Serializable]
    public class TaskDef
    {
        public string title;
        [TextArea] public string hint;
        [Tooltip("Shown (green) when the task is done.")]
        public string doneText = "Ho gaya!";
        [Tooltip("Tutorial task: show the hint immediately (no penalty).")]
        public bool hintImmediately;
        public float hintDelay = 60f;
        public int basePoints = 1000;

        [Header("English")]
        public string titleEn;
        [TextArea] public string hintEn;
        public string doneTextEn;

        public string Title => GameLanguage.Pick(titleEn, title);
        public string Hint => GameLanguage.Pick(hintEn, hint);
        public string Done => GameLanguage.Pick(doneTextEn, doneText);
    }

    public const int TaskCount = 6;

    [SerializeField] private TaskDef[] tasks = new TaskDef[TaskCount];

    [Header("Scoring")]
    [SerializeField] private float pointsLostPerSecond = 5f;
    [SerializeField] private int minPoints = 200;
    [SerializeField, Range(0f, 1f)] private float hintPenalty = 0.2f;
    [SerializeField] private int masterScore = 4300;
    [SerializeField] private int goodScore = 3000;

    [Header("Timing")]
    [SerializeField] private float introDelay = 2f;
    [SerializeField] private float introDuration = 4.5f;
    [SerializeField] private float pauseBetweenTasks = 2.5f;

    [Header("References")]
    [SerializeField] private TaskHUD hud;
    [Tooltip("Optional story intro: the phone call that must be answered before task 1.")]
    [SerializeField] private IntroPhoneCall introCall;
    [Tooltip("Phone screen that shows the task hint. Empty = PhoneHintScreen on the intro phone.")]
    [SerializeField] private PhoneHintScreen phoneScreen;
    [SerializeField] private TVController tv;
    [SerializeField] private LockableDoor livingDoor;
    [SerializeField] private KeyLockSocket livingDoorSocket;
    [SerializeField] private DoorWedge livingDoorWedge;

    [Header("Ending")]
    [Tooltip("Doorbell + Mummy's lines before the score card (optional).")]
    [SerializeField] private MummyArrival arrival;
    [SerializeField] private string endHeader = "LEVEL 1 PURA!";
    [SerializeField] private string endHeaderEn = "LEVEL 1 COMPLETE!";
    [SerializeField] private LockableDoor bedroomDoor;
    [SerializeField] private MagnetTool magnet;
    [SerializeField] private RoomPowerController bedroomPower;

    [Header("'Not yet' reminders")]
    [SerializeField] private XRGrabInteractable safetyPin;
    [SerializeField] private Transform bedroomKey;
    [SerializeField] private float pinReminderRadius = 0.25f;
    [SerializeField] private float magnetReminderRadius = 0.6f;
    [SerializeField] private float reminderCooldown = 4f;

    /// <summary>-1 before the first task, TaskCount after the last.</summary>
    public int CurrentTask => _current;
    public int Score => _score;
    public bool Finished => _finished;
    /// <summary>True once the score card is up (after Mummy's arrival).</summary>
    public bool EndShown { get; private set; }
    public float TotalTime { get; private set; }
    public string Rank { get; private set; }
    public int MasterScore => masterScore;
    public int GoodScore => goodScore;
    /// <summary>1-3 stars, same thresholds as the rank.</summary>
    public int Stars => _score >= masterScore ? 3 : _score >= goodScore ? 2 : 1;

    private int _current = -1;
    private bool _accepting;
    private float _taskStart;
    private bool _hintShown, _hintUsed;
    private int _score;
    private bool _finished;
    private float _gameStart;
    private int _deadPressesAtStart;
    private float _nextReminder;
    private XRGrabInteractable _magnetGrab;

    private void Awake()
    {
        // Lock everything that could be done out of order.
        if (livingDoorSocket) livingDoorSocket.socketActive = false;
        if (livingDoorWedge) livingDoorWedge.Armed = false;
        if (magnet)
        {
            magnet.enabled = false;
            _magnetGrab = magnet.GetComponent<XRGrabInteractable>();
        }
        if (phoneScreen == null && introCall != null) phoneScreen = introCall.GetComponent<PhoneHintScreen>();
        if (tasks == null || tasks.Length != TaskCount)
            Debug.LogError("[GameFlow] Expected exactly " + TaskCount + " tasks.");
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(introDelay);
        if (hud) hud.ShowBanner("JUGAAD WALA GHAR", GameLanguage.Pick("Every job in this house... done with jugaad!", "Ghar ka har kaam... jugaad se!"));
        yield return new WaitForSeconds(introDuration);
        if (introCall != null)
        {
            introCall.StartRinging();
            yield return new WaitUntil(() => introCall.Finished);
            yield return new WaitForSeconds(1f);
        }
        _gameStart = Time.time;
        Activate(0);
    }

    private void Activate(int i)
    {
        _current = i;
        _hintShown = false;
        _hintUsed = false;

        switch (i) // unlock this task's mechanic
        {
            case 1: if (livingDoorSocket) livingDoorSocket.socketActive = true; break;
            case 2: if (magnet) magnet.enabled = true; break;
            case 4: _deadPressesAtStart = bedroomPower ? bedroomPower.DeadFanPresses : 0; break;
        }

        var t = tasks[i];
        string hintNow = t.hintImmediately ? t.Hint : null;
        float typeTime = hud ? hud.ShowTask(i + 1, TaskCount, t.Title, hintNow) : 0f;
        if (t.hintImmediately) _hintShown = true;
        _taskStart = Time.time + typeTime; // clock starts once the player could read it
        if (phoneScreen)
        {
            phoneScreen.SetTask(i + 1, TaskCount, t.Title);
            if (t.hintImmediately) phoneScreen.SetHint(t.Hint, false);
            else if (!string.IsNullOrEmpty(t.Hint)) phoneScreen.SetHintPending(_taskStart + t.hintDelay);
        }
        _accepting = true;
        Debug.Log("[GameFlow] Task " + (i + 1) + ": " + t.Title);
    }

    private void Update()
    {
        if (!_accepting || _current < 0 || _current >= TaskCount) return;

        var t = tasks[_current];
        if (!_hintShown && !string.IsNullOrEmpty(t.Hint) && Time.time - _taskStart >= t.hintDelay)
        {
            _hintShown = true;
            _hintUsed = true;
            if (hud) hud.ShowHint(t.Hint);
            if (phoneScreen) phoneScreen.SetHint(t.Hint, true); // the hint itself lives on the phone
        }

        CheckReminders();

        if (Condition(_current)) StartCoroutine(CompleteRoutine());
    }

    private bool Condition(int i)
    {
        switch (i)
        {
            case 0: return tv && tv.IsOn;
            case 1: return livingDoor && !livingDoor.IsLocked;
            case 2: return bedroomDoor && !bedroomDoor.IsLocked;
            case 3: return bedroomPower && bedroomPower.LightOn;
            case 4: return bedroomPower && bedroomPower.DeadFanPresses > _deadPressesAtStart;
            case 5: return bedroomPower && bedroomPower.FanOn;
        }
        return false;
    }

    private IEnumerator CompleteRoutine()
    {
        _accepting = false;
        var t = tasks[_current];
        float secs = Mathf.Max(0f, Time.time - _taskStart);
        float pts = Mathf.Max(minPoints, t.basePoints - secs * pointsLostPerSecond);
        if (_hintUsed) pts *= 1f - hintPenalty;
        int p = Mathf.RoundToInt(pts);
        _score += p;
        Debug.Log("[GameFlow] Task " + (_current + 1) + " done in " + secs.ToString("F1") + "s  +" + p + (_hintUsed ? " (hint)" : "") + "  total " + _score);
        if (hud) hud.ShowCompleted(t.Done, p, _score, _hintUsed);
        if (phoneScreen) phoneScreen.ShowDone(t.Done);

        yield return new WaitForSeconds(pauseBetweenTasks);

        if (_current + 1 < TaskCount) Activate(_current + 1);
        else Finish();
    }

    private void Finish()
    {
        _current = TaskCount;
        _finished = true;
        bool firstClear = LevelProgress.MarkDone(1);          // opens Level 2 in the main menu
        float total = Time.time - _gameStart;
        string rank = _score >= masterScore ? "JUGAAD MASTER!" : _score >= goodScore ? GameLanguage.Pick("PRO JUGAADU!", "PAKKA JUGAADU!") : GameLanguage.Pick("NEWBIE JUGAADU!", "NAYA JUGAADU!");
        TotalTime = total;
        Rank = rank;
        Debug.Log("[GameFlow] Finished: " + _score + " in " + total.ToString("F0") + "s -> " + rank);
        StartCoroutine(EndRoutine(firstClear, total, rank));
    }

    private IEnumerator EndRoutine(bool firstClear, float total, string rank)
    {
        if (arrival)
        {
            arrival.Play();
            yield return new WaitUntil(() => arrival.Finished);
        }
        if (hud) hud.ShowEnd(_score, total, rank, GameLanguage.Pick(endHeaderEn, endHeader));
        if (phoneScreen) phoneScreen.ShowEnd(_score, rank);
        if (firstClear && hud) hud.ShowToast(GameLanguage.Pick("LEVEL 2 UNLOCKED!", "LEVEL 2 KHUL GAYA!"), hud.GoodColor, false);
        EndShown = true;
    }

    /// <summary>Wire the FAN remote's BrokenRemote.onPressed here (instead of straight to the fan).</summary>
    public void OnFanRemotePressed()
    {
        if (_finished || _current >= 5) { if (bedroomPower) bedroomPower.ToggleFan(); }
        else Remind();
    }

    private void CheckReminders()
    {
        if (Time.time < _nextReminder) return;

        if (_current < 1 && safetyPin && safetyPin.isSelected && livingDoorSocket &&
            Vector3.Distance(safetyPin.transform.position, livingDoorSocket.transform.position) < pinReminderRadius)
            Remind();
        else if (_current < 2 && magnet && _magnetGrab && _magnetGrab.isSelected && bedroomKey &&
            Vector3.Distance(magnet.transform.position, bedroomKey.position) < magnetReminderRadius)
            Remind();
    }

    private void Remind()
    {
        if (!_accepting || Time.time < _nextReminder || _current < 0 || _current >= TaskCount) return;
        _nextReminder = Time.time + reminderCooldown;
        if (hud) hud.ShowToast(GameLanguage.Pick("First do this: ", "Pehle ye karo: ") + tasks[_current].Title, hud.WarnColor);
    }
}
