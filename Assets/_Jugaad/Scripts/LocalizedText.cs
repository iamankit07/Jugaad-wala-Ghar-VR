using TMPro;
using UnityEngine;

/// <summary>
/// Put on any TextMeshPro text: shows the English or Hinglish version depending on
/// GameLanguage, and updates live when the language is changed in Settings.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [TextArea(2, 8)] public string english;
    [TextArea(2, 8)] public string hinglish;

    private TMP_Text _text;

    private void OnEnable()
    {
        GameLanguage.Changed += Apply;
        Apply();
    }

    private void OnDisable()
    {
        GameLanguage.Changed -= Apply;
    }

    public void Apply()
    {
        if (_text == null) _text = GetComponent<TMP_Text>();
        string s = GameLanguage.Pick(english, hinglish);
        if (_text != null && !string.IsNullOrEmpty(s)) _text.text = s;
    }
}
