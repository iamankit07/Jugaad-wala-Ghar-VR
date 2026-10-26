using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

/// <summary>
/// Meta store requirement (VRC): when the app loses input focus - the player opens the
/// Quest system menu, takes the headset off, a system dialog appears - the game must
/// pause, go silent and stop drawing the player's hands/controllers. Everything is put
/// back exactly as it was when focus returns (so an open pause menu stays open).
/// Created automatically once at startup; lives across scene loads.
/// </summary>
public class AppFocusPause : MonoBehaviour
{
    public static AppFocusPause Instance { get; private set; }
    public static bool LostFocus { get; private set; }

    private float _savedTimeScale = 1f;
    private bool _savedAudioPause;
    private readonly List<Renderer> _hidden = new List<Renderer>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (Instance != null) return;
        var go = new GameObject("AppFocusPause");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<AppFocusPause>();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (Application.isEditor) return; // clicking outside the Game view must not freeze play mode
        SetFocus(hasFocus);
    }

    private void OnApplicationPause(bool paused)
    {
        if (Application.isEditor) return;
        if (paused) SetFocus(false);
    }

    /// <summary>Also callable from tests in the editor.</summary>
    public void SetFocus(bool hasFocus)
    {
        if (!hasFocus && !LostFocus)
        {
            LostFocus = true;
            _savedTimeScale = Time.timeScale;
            _savedAudioPause = AudioListener.pause;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            HideHands();
        }
        else if (hasFocus && LostFocus)
        {
            LostFocus = false;
            ShowHands();
            AudioListener.pause = _savedAudioPause;
            Time.timeScale = _savedTimeScale;
        }
    }

    private void HideHands()
    {
        _hidden.Clear();
        // Controller / hand models and their ray lines live under the tracked hand objects.
        foreach (var tpd in FindObjectsByType<UnityEngine.InputSystem.XR.TrackedPoseDriver>(FindObjectsSortMode.None))
        {
            if (tpd.GetComponent<Camera>() != null) continue;
            foreach (var r in tpd.GetComponentsInChildren<Renderer>(false)) Hide(r);
        }
        // Teleport arcs / ray lines that may sit elsewhere in the rig.
        foreach (var lv in FindObjectsByType<XRInteractorLineVisual>(FindObjectsSortMode.None))
        {
            var lr = lv.GetComponent<LineRenderer>();
            if (lr) Hide(lr);
            if (lv.reticle) foreach (var r in lv.reticle.GetComponentsInChildren<Renderer>(false)) Hide(r);
        }
    }

    private void Hide(Renderer r)
    {
        if (r == null || !r.enabled || _hidden.Contains(r)) return;
        r.enabled = false;
        _hidden.Add(r);
    }

    private void ShowHands()
    {
        foreach (var r in _hidden) if (r != null) r.enabled = true;
        _hidden.Clear();
    }
}
