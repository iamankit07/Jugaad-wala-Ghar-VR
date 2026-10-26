using UnityEngine;

/// <summary>
/// Which levels the player has finished, saved on the headset. Level 1 is always open; every next
/// level opens once the one before it has been completed. Once finished, levels stay open for good.
/// </summary>
public static class LevelProgress
{
    public const int LevelCount = 2;
    private const string Prefix = "jugaad_level_done_";
    private const string BestPrefix = "jugaad_level_best_";
    private const string StarsPrefix = "jugaad_level_stars_";

    public static int GetBest(int level) => PlayerPrefs.GetInt(BestPrefix + level, 0);
    public static int GetBestStars(int level) => PlayerPrefs.GetInt(StarsPrefix + level, 0);

    /// <summary>Saves score / stars if better than before. Returns true for a new best score.</summary>
    public static bool SubmitScore(int level, int score, int stars)
    {
        bool best = score > GetBest(level);
        if (best) PlayerPrefs.SetInt(BestPrefix + level, score);
        if (stars > GetBestStars(level)) PlayerPrefs.SetInt(StarsPrefix + level, stars);
        PlayerPrefs.Save();
        return best;
    }

    public static bool IsDone(int level) => PlayerPrefs.GetInt(Prefix + level, 0) == 1;

    public static bool IsUnlocked(int level) => level <= 1 || IsDone(level - 1);

    /// <summary>Returns true if this was the first time (so a "next level unlocked" message can show).</summary>
    public static bool MarkDone(int level)
    {
        bool first = !IsDone(level);
        PlayerPrefs.SetInt(Prefix + level, 1);
        PlayerPrefs.Save();
        Debug.Log("[LevelProgress] level " + level + " completed" + (first ? " (first time)" : ""));
        return first;
    }

    public static void ResetAll()
    {
        for (int i = 1; i <= LevelCount; i++) { PlayerPrefs.DeleteKey(Prefix + i); PlayerPrefs.DeleteKey(BestPrefix + i); PlayerPrefs.DeleteKey(StarsPrefix + i); }
        PlayerPrefs.Save();
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Jugaad/Progress/Unlock all levels")]
    private static void UnlockAll() { for (int i = 1; i <= LevelCount; i++) MarkDone(i); }

    [UnityEditor.MenuItem("Jugaad/Progress/Reset (fresh install)")]
    private static void ResetMenu() { ResetAll(); Debug.Log("[LevelProgress] reset - only level 1 is open"); }
#endif
}
