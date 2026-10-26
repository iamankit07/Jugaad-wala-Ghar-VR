using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Data-driven version of GameFlowManager for new levels (garage...). Same feel: title banner,
/// optional intro phone call, then tasks in a fixed order with timed hints (on the phone),
/// points per task and an end screen. Each task is set up in the Inspector:
/// - condition: a TaskCondition component that says when the task is done
/// - onStart:   unlock this task's mechanic (arm a socket, enable a tool...)
/// - onComplete: anything that should happen when it is done
/// Level 1 keeps its own GameFlowManager.
/// </summary>
public class LevelFlowManager : MonoBehaviour
{
    [System.Serializable]
    public class Step
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

        [Header("Logic")]
        [Tooltip("Says when this task is done.")]
        public TaskCondition condition;
        [Tooltip("Unlock this task's mechanic here.")]
        public UnityEvent onStart;
        public UnityEvent onComplete;

        public string Title => GameLanguage.Pick(titleEn, title);
        public string Hint => GameLanguage.Pick(hintEn, hint);
        public string Done => GameLanguage.Pick(doneTextEn, doneText);
    }

    [Header("Level")]
    [Tooltip("Level number for the main menu's unlocks (LevelProgress).")]
    [SerializeField] private int levelNumber = 2;
    [SerializeField] private string bannerTitle = "JUGAAD WALA GARAGE";
    [SerializeField] private string bannerSubtitle = "Garage ka har kaam... jugaad se!";
    [SerializeField] private string bannerSubtitleEn = "Every job in this garage... done with jugaad!";
    [Tooltip("Heading of the end screen.")]
    [SerializeField] private string endHeader = "LEVEL 2 PURA!";
    [SerializeField] private string endHeaderEn = "LEVEL 2 COMPLETE!";
    [Tooltip("Shown as 'TASK n/N'. 0 = number of steps. Set it higher while the level is still being built;" +
             " the flow then stops after the last finished step instead of showing the end screen.")]
    [SerializeField] private int plannedTaskCount = 0;
    [SerializeField] private List<Step> steps = new List<Step>();

    [Header("Scoring")]
    [SerializeField] private float pointsLostPerSecond = 5f;
    [SerializeField] private int minPoints = 200;
    [SerializeField, Range(0f, 1f)] private float hintPenalty = 0.2f;
    [SerializeField] private int masterScore = 4500;
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

    /// <summary>-1 before the first task, TaskCount after the last.</summary>
    public int CurrentTask => _current;
    public int TaskCount => Mathf.Max(steps.Count, plannedTaskCount);
    public int Score => _score;
    public bool Finished => _finished;
    public int LevelNumber => levelNumber;
    public float TotalTime { get; private set; }
    public string Rank { get; private set; }
    public int MasterScore => masterScore;
    public int GoodScore => goodScore;
    /// <summary>1-3 stars, same thresholds as the rank.</summary>
    public int Stars => _score >= masterScore ? 3 : _score >= goodScore ? 2 : 1;
    /// <summary>The active task's condition (null between tasks / before the first / after the last).</summary>
    public TaskCondition CurrentCondition => _accepting && _current >= 0 && _current < steps.Count ? steps[_current].condition : null;

    /// <summary>Extra points for doing something clever (shown in the score straight away).</summary>
    public void AddBonus(int points)
    {
        _score += points;
        if (hud) hud.SetScore(_score);
        Debug.Log("[LevelFlow] bonus +" + points + "  total " + _score);
    }

    private int _current = -1;
    private bool _accepting;
    private float _taskStart, _gameStart;
    private bool _hintShown, _hintUsed;
    private bool _wasBusy;
    private int _score;
    private bool _finished;

    private void Awake()
    {
        if (hud == null) hud = FindFirstObjectByType<TaskHUD>();
        if (introCall == null) introCall = FindFirstObjectByType<IntroPhoneCall>();
        if (phoneScreen == null && introCall != null) phoneScreen = introCall.GetComponent<PhoneHintScreen>();
        for (int i = 0; i < steps.Count; i++)
            if (steps[i].condition == null) Debug.LogWarning("[LevelFlow] Task " + (i + 1) + " has no condition - it can never finish.");
    }

    private IEnumerator Start()
    {
        if (steps.Count == 0) yield break;
        yield return new WaitForSeconds(introDelay);
        if (hud) hud.ShowBanner(bannerTitle, GameLanguage.Pick(bannerSubtitleEn, bannerSubtitle));
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

    private void Activate(int i) { StartCoroutine(ActivateRoutine(i)); }

    /// <summary>A phone call is queued / ringing / going on.</summary>
    private bool CallBusy => introCall != null && introCall.Busy;

    private IEnumerator ActivateRoutine(int i)
    {
        _current = i;
        _hintShown = false;
        _hintUsed = false;
        var t = steps[i];
        t.onStart?.Invoke();
        if (t.condition != null) t.condition.OnTaskStarted();

        // a call that this task starts (e.g. Papa about the flat tyre): let it play out first, then
        // show the task - its clock and hint timer don't run while the player is listening
        yield return null;
        if (CallBusy)
        {
            while (CallBusy) yield return null;
            yield return new WaitForSeconds(0.6f);
        }

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
        Debug.Log("[LevelFlow] Task " + (i + 1) + ": " + t.Title);
    }

    private void Update()
    {
        if (!_accepting || _current < 0 || _current >= steps.Count) return;
        var t = steps[_current];

        // a phone call during a task: its clock (points) and hint timer stand still while the player listens
        bool busy = CallBusy;
        if (busy) _taskStart += Time.deltaTime;
        else if (_wasBusy && !_hintShown && phoneScreen && !string.IsNullOrEmpty(t.Hint))
            phoneScreen.SetHintPending(_taskStart + t.hintDelay);   // the phone's hint countdown catches up
        _wasBusy = busy;
        if (!_hintShown && !string.IsNullOrEmpty(t.Hint) && Time.time - _taskStart >= t.hintDelay)
        {
            _hintShown = true;
            _hintUsed = true;
            if (hud) hud.ShowHint(t.Hint);
            if (phoneScreen) phoneScreen.SetHint(t.Hint, true);
        }
        if (t.condition != null && t.condition.IsMet) StartCoroutine(CompleteRoutine());
    }

    private IEnumerator CompleteRoutine()
    {
        _accepting = false;
        var t = steps[_current];
        float secs = Mathf.Max(0f, Time.time - _taskStart);
        float pts = Mathf.Max(minPoints, t.basePoints - secs * pointsLostPerSecond);
        if (_hintUsed) pts *= 1f - hintPenalty;
        int p = Mathf.RoundToInt(pts);
        _score += p;
        Debug.Log("[LevelFlow] Task " + (_current + 1) + " done in " + secs.ToString("F1") + "s  +" + p + (_hintUsed ? " (hint)" : "") + "  total " + _score);
        t.onComplete?.Invoke();
        if (hud) hud.ShowCompleted(t.Done, p, _score, _hintUsed);
        if (phoneScreen) phoneScreen.ShowDone(t.Done);

        yield return new WaitForSeconds(pauseBetweenTasks);
        while (CallBusy) yield return null;   // a call started by the finished task plays out first

        if (_current + 1 < steps.Count) Activate(_current + 1);
        else if (steps.Count >= TaskCount) Finish();
        else
        {
            _current = steps.Count; // level still being built: stop here
            Debug.Log("[LevelFlow] No more tasks set up yet (" + steps.Count + "/" + TaskCount + ").");
            if (hud) hud.ShowBanner(GameLanguage.Pick("TO BE CONTINUED", "AAGE JAARI HAI"), GameLanguage.Pick("More tasks coming soon", "Aage ke tasks jaldi aa rahe hain"));
        }
    }

    private void Finish()
    {
        _current = TaskCount;
        _finished = true;
        LevelProgress.MarkDone(levelNumber);
        float total = Time.time - _gameStart;
        string rank = _score >= masterScore ? "JUGAAD MASTER!" : _score >= goodScore ? GameLanguage.Pick("PRO JUGAADU!", "PAKKA JUGAADU!") : GameLanguage.Pick("NEWBIE JUGAADU!", "NAYA JUGAADU!");
        TotalTime = total;
        Rank = rank;
        Debug.Log("[LevelFlow] Finished: " + _score + " in " + total.ToString("F0") + "s -> " + rank);
        if (hud) hud.ShowEnd(_score, total, rank, GameLanguage.Pick(endHeaderEn, endHeader));
        if (phoneScreen) phoneScreen.ShowEnd(_score, rank);
    }
}
