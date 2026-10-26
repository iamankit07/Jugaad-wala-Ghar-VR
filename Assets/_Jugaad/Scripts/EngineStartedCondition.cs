using UnityEngine;

/// <summary>Task done once the engine runs and the player has had a moment to see the dashboard warning.</summary>
public class EngineStartedCondition : TaskCondition
{
    [SerializeField] private CarEngine engine;
    [SerializeField] private float readTime = 3f;

    private void Awake()
    {
        if (engine == null) engine = FindFirstObjectByType<CarEngine>();
    }

    public override bool IsMet => engine != null && engine.Started && engine.WarningShownFor >= readTime;
}
