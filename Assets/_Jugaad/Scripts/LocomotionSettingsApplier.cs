using UnityEngine;

/// <summary>
/// Put on the player rig in the game scene: switches turning (snap / smooth) and movement
/// (smooth / teleport) to what the player picked in the Main Menu settings.
/// </summary>
public class LocomotionSettingsApplier : MonoBehaviour
{
    [SerializeField] private Behaviour continuousMove;
    [SerializeField] private ArcTeleporter teleporter;
    [SerializeField] private GameObject smoothTurn;
    [SerializeField] private GameObject snapTurn;

    private void Awake() { Apply(); }

    public void Apply()
    {
        bool tp = ComfortSettings.Teleport;
        if (continuousMove) continuousMove.enabled = !tp;
        if (teleporter) teleporter.enabled = tp;

        bool snap = ComfortSettings.SnapTurn;
        if (smoothTurn) smoothTurn.SetActive(!snap);
        if (snapTurn) snapTurn.SetActive(snap);
    }
}
