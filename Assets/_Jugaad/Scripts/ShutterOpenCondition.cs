using UnityEngine;

/// <summary>Task done when the shutter is rolled up (stuck half open). Arms the shutter when the task starts.</summary>
public class ShutterOpenCondition : TaskCondition
{
    [SerializeField] private ShutterLift shutter;

    private void Awake()
    {
        if (shutter == null) shutter = FindFirstObjectByType<ShutterLift>();
    }

    public override void OnTaskStarted()
    {
        if (shutter != null) shutter.Arm();
    }

    public override bool IsMet => shutter != null && shutter.IsOpen;
}
