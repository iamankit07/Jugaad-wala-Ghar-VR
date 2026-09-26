using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Runs the whole story as 7 tasks in a FIXED order. A task's mechanic is only unlocked
/// when that task is active, so the order can never break. Each finished task scores
/// points based on how fast it was done (clock starts once the task text is typed).
/// Tutorial tasks show their hint right away; later ones only after hintDelay seconds
/// (and taking a hint costs hintPenalty of that task's points).
///
/// Task order (Condition() depends on it):
///  0 TV on | 1 living-room door unlocked | 2 living-room door wedged open |
///  3 bedroom door unlocked | 4 bedroom light on | 5 broken fan switch pressed | 6 fan on (remote)
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

    public const int TaskCount = 7;

    [SerializeField] private TaskDef[] tasks = new TaskDef[TaskCount];

    [Header("Scoring")]
    [SerializeField] private float pointsLostPerSecond = 5f;
    [SerializeField] private int minPoints = 200;
    [SerializeField, Range(0f, 1f)] private float hintPenalty = 0.2f;
    [SerializeField] private int masterScore = 5000;
    [SerializeField] private int goodScore = 3500;

    [Header("Timing")]
    [SerializeField] private float introDelay = 2f;
    [SerializeField] private float introDuration = 4.5f;
    [SerializeField] private float pauseBetweenTasks = 2.5f;

    [Header("References")]
    [SerializeField] private TaskHUD hud;
    [SerializeField] private TVController tv;
    [SerializeField] private LockableDoor livingDoor;
    [SerializeField] private KeyLockSocket livingDoorSocket;
    [SerializeField] private DoorWedge livingDoorWedge;
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
        if (tasks == null || tasks.Length != TaskCount)
            Debug.LogError("[GameFlow] Expected exactly " + TaskCount + " tasks.");
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(introDelay);
        if (hud) hud.ShowBanner("JUGAAD WALA GHAR", GameLanguage.Pick("Every job in this house... done with jugaad!", "Ghar ka har kaam... jugaad se!"));
        yield return new WaitForSeconds(introDuration);
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
            case 2: if (livingDoorWedge) livingDoorWedge.Armed = true; break;
            case 3: if (magnet) magnet.enabled = true; break;
            case 5: _deadPressesAtStart = bedroomPower ? bedroomPower.DeadFanPresses : 0; break;
        }

        var t = tasks[i];
        string hintNow = t.hintImmediately ? t.Hint : null;
        float typeTime = hud ? hud.ShowTask(i + 1, TaskCount, t.Title, hintNow) : 0f;
        if (t.hintImmediately) _hintShown = true;
        _taskStart = Time.time + typeTime; // clock starts once the player could read it
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
            case 2: return livingDoorWedge && livingDoorWedge.IsWedged;
            case 3: return bedroomDoor && !bedroomDoor.IsLocked;
            case 4: return bedroomPower && bedroomPower.LightOn;
            case 5: return bedroomPower && bedroomPower.DeadFanPresses > _deadPressesAtStart;
            case 6: return bedroomPower && bedroomPower.FanOn;
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

        yield return new WaitForSeconds(pauseBetweenTasks);

        if (_current + 1 < TaskCount) Activate(_current + 1);
        else Finish();
    }

    private void Finish()
    {
        _current = TaskCount;
        _finished = true;
        float total = Time.time - _gameStart;
        string rank = _score >= masterScore ? "JUGAAD MASTER!" : _score >= goodScore ? GameLanguage.Pick("PRO JUGAADU!", "PAKKA JUGAADU!") : GameLanguage.Pick("NEWBIE JUGAADU!", "NAYA JUGAADU!");
        Debug.Log("[GameFlow] Finished: " + _score + " in " + total.ToString("F0") + "s -> " + rank);
        if (hud) hud.ShowEnd(_score, total, rank);
    }

    /// <summary>Wire the FAN remote's BrokenRemote.onPressed here (instead of straight to the fan).</summary>
    public void OnFanRemotePressed()
    {
        if (_finished || _current >= 6) { if (bedroomPower) bedroomPower.ToggleFan(); }
        else Remind();
    }

    private void CheckReminders()
    {
        if (Time.time < _nextReminder) return;

        if (_current < 1 && safetyPin && safetyPin.isSelected && livingDoorSocket &&
            Vector3.Distance(safetyPin.transform.position, livingDoorSocket.transform.position) < pinReminderRadius)
            Remind();
        else if (_current < 3 && magnet && _magnetGrab && _magnetGrab.isSelected && bedroomKey &&
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
