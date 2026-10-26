using UnityEngine;

/// <summary>
/// Says when one task of a LevelFlowManager level is done. Make one small subclass per kind of
/// task (torch on, item in socket, ...) and drag it into the task's "condition" slot.
/// </summary>
public abstract class TaskCondition : MonoBehaviour
{
    /// <summary>True once the task is done. Polled every frame while the task is active.</summary>
    public abstract bool IsMet { get; }

    /// <summary>Called when this task becomes the active one (reset counters, arm sockets...).</summary>
    public virtual void OnTaskStarted() { }
}
