using System;
using UnityEngine;

public enum Language { English = 0, Hinglish = 1 }

/// <summary>
/// Chosen game language, saved on the headset (PlayerPrefs) so it carries from the
/// main menu into the game and across app restarts. Default: English.
/// Names (e.g. "JUGAAD WALA GHAR") are never translated.
/// </summary>
public static class GameLanguage
{
    private const string PrefKey = "jugaad_language";
    private static bool _loaded;
    private static Language _current;

    /// <summary>Fired whenever the language changes (LocalizedText listens to this).</summary>
    public static event Action Changed;

    public static Language Current
    {
        get
        {
            if (!_loaded)
            {
                _current = (Language)PlayerPrefs.GetInt(PrefKey, (int)Language.English);
                _loaded = true;
            }
            return _current;
        }
        set
        {
            if (Current == value) return;
            _current = value;
            PlayerPrefs.SetInt(PrefKey, (int)value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    public static bool IsEnglish => Current == Language.English;

    /// <summary>Returns the text for the current language (falls back to the other one if empty).</summary>
    public static string Pick(string english, string hinglish)
    {
        if (IsEnglish) return string.IsNullOrEmpty(english) ? hinglish : english;
        return string.IsNullOrEmpty(hinglish) ? english : hinglish;
    }
}
