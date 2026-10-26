using UnityEngine;

/// <summary>Task done once the key was turned without a battery and Papa's call about it is over.</summary>
public class CarStartTriedCondition : TaskCondition
{
    [SerializeField] private CarIgnition ignition;

    private void Awake()
    {
        if (ignition == null) ignition = FindFirstObjectByType<CarIgnition>();
    }

    public override bool IsMet => ignition != null && ignition.CallDone;

    public override void OnTaskStarted()
    {
        if (ignition != null) ignition.Armed = true;
    }
}
