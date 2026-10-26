using UnityEngine;

/// <summary>Task done when the oil spill is covered (bora laid on it).</summary>
public class OilCoveredCondition : TaskCondition
{
    [SerializeField] private OilSpill oil;

    private void Awake()
    {
        if (oil == null) oil = FindFirstObjectByType<OilSpill>();
    }

    public override bool IsMet => oil != null && oil.Covered;
}
