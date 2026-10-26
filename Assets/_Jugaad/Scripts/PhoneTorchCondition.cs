using UnityEngine;

/// <summary>Task done when the phone torch is on (and, by default, out of the pocket).</summary>
public class PhoneTorchCondition : TaskCondition
{
    [SerializeField] private PhoneTorch torch;
    [Tooltip("Torch must actually light the room (phone out of the pocket).")]
    [SerializeField] private bool requireOutOfPocket = true;

    private void Awake()
    {
        if (torch == null) torch = FindFirstObjectByType<PhoneTorch>();
    }

    public override bool IsMet => torch != null && torch.IsOn && (!requireOutOfPocket || !torch.InPocket);
}
