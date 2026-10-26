using UnityEngine;

/// <summary>Task done when the tool bag has been pulled down off the high shelf.</summary>
public class BagDownCondition : TaskCondition
{
    [SerializeField] private ToolBagDrop bag;

    private void Awake()
    {
        if (bag == null) bag = FindFirstObjectByType<ToolBagDrop>();
    }

    public override bool IsMet => bag != null && bag.Landed;
}
