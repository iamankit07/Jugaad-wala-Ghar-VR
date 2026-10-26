using UnityEngine;

/// <summary>Task done when the battery sits in its place in the engine bay.</summary>
public class BatteryInstalledCondition : TaskCondition
{
    [SerializeField] private BatteryBay bay;

    private void Awake()
    {
        if (bay == null) bay = FindFirstObjectByType<BatteryBay>();
    }

    public override bool IsMet => bay != null && bay.Installed;

    public override void OnTaskStarted()
    {
        if (bay != null) bay.Armed = true;
    }
}
