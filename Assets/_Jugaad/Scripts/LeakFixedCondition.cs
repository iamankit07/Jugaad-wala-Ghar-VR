using UnityEngine;

/// <summary>Task done when the leaking joint is wrapped and tied (no more dripping).</summary>
public class LeakFixedCondition : TaskCondition
{
    [SerializeField] private LeakingPipe pipe;

    private void Awake()
    {
        if (pipe == null) pipe = FindFirstObjectByType<LeakingPipe>();
    }

    public override bool IsMet => pipe != null && pipe.Sealed;

    public override void OnTaskStarted()
    {
        if (pipe != null) pipe.Arm();
    }
}
