using UnityEngine;

/// <summary>One part of the tyre change (jack up / wheel off / spare on and car down) - see TyreChange.</summary>
public class TyreStageCondition : TaskCondition
{
    [SerializeField] private TyreChange tyre;
    [SerializeField] private TyreChange.Stage stage = TyreChange.Stage.Jack;

    private void Awake()
    {
        if (tyre == null) tyre = FindFirstObjectByType<TyreChange>();
    }

    public override void OnTaskStarted()
    {
        if (tyre != null) tyre.Begin(stage);
    }

    public override bool IsMet => tyre != null && tyre.IsDone(stage);
}
