using UnityEngine;

/// <summary>
/// Player comfort options chosen in the Main Menu (Settings) and saved on the headset.
/// Defaults: smooth turning + smooth movement (what the game shipped with).
/// </summary>
public static class ComfortSettings
{
    private const string SnapTurnKey = "jugaad_snap_turn";
    private const string TeleportKey = "jugaad_teleport_move";

    /// <summary>true = snap turn (45° steps), false = smooth turn.</summary>
    public static bool SnapTurn
    {
        get => PlayerPrefs.GetInt(SnapTurnKey, 0) == 1;
        set { PlayerPrefs.SetInt(SnapTurnKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// <summary>true = teleport with the left stick, false = smooth movement.</summary>
    public static bool Teleport
    {
        get => PlayerPrefs.GetInt(TeleportKey, 0) == 1;
        set { PlayerPrefs.SetInt(TeleportKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }
}
