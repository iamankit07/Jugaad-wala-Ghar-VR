using UnityEngine;

/// <summary>
/// TV that plays a pre-baked flipbook (sprite-sheet frames) on its screen, driven by the
/// AudioSource clock. No video decoder is involved, so it can never stall, and picture
/// stays locked to the sound. Frames are laid out left-to-right, top-to-bottom per sheet.
/// </summary>
public class TVController : MonoBehaviour
{
    [Header("Screen")]
    [SerializeField] private Renderer screen;
    [SerializeField] private Material screenOn;   // Unlit material; its _BaseMap gets the sheets
    [SerializeField] private Material screenOff;  // black glass

    [Header("Flipbook")]
    [SerializeField] private Texture2D[] sheets;
    [SerializeField] private int columns = 8;
    [SerializeField] private int rows = 14;
    [SerializeField] private int frameCount = 607;
    [SerializeField] private float fps = 12f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip powerOnSound;
    [SerializeField] private bool startOn = false;

    public bool IsOn { get; private set; }

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private Material _runtimeMat;
    private int _shownFrame = -1;
    private float _clock;

    private void Awake()
    {
        if (screenOn != null) _runtimeMat = new Material(screenOn);
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = true;
        }
        SetOn(startOn, false);
    }

    private void OnDestroy()
    {
        if (_runtimeMat != null) Destroy(_runtimeMat);
    }

    public void Toggle() { SetOn(!IsOn, true); }
    public void SetOn(bool on) { SetOn(on, true); }

    private void SetOn(bool on, bool withFeedback)
    {
        IsOn = on;
        _clock = 0f;
        _shownFrame = -1;
        if (screen) screen.sharedMaterial = on && _runtimeMat != null ? _runtimeMat : screenOff;

        if (on)
        {
            ShowFrame(0);
            if (audioSource != null && audioSource.clip != null)
            {
                audioSource.time = 0f;
                audioSource.Play();
            }
            if (withFeedback && powerOnSound) SfxMix.PlayAt(powerOnSound, transform.position, 0.7f);
        }
        else if (audioSource != null)
        {
            audioSource.Stop();
        }
    }

    private void Update()
    {
        if (!IsOn || frameCount <= 0) return;

        float t;
        if (audioSource != null && audioSource.clip != null && audioSource.isPlaying)
            t = audioSource.time;               // audio is the master clock
        else
        {
            _clock += Time.deltaTime;           // fallback: own clock (e.g. no audio)
            t = _clock;
        }

        int frame = Mathf.FloorToInt(t * fps) % frameCount;
        if (frame != _shownFrame) ShowFrame(frame);
    }

    private void ShowFrame(int frame)
    {
        if (_runtimeMat == null || sheets == null || sheets.Length == 0) return;
        int perSheet = columns * rows;
        int sheetIndex = Mathf.Clamp(frame / perSheet, 0, sheets.Length - 1);
        int local = frame % perSheet;
        int col = local % columns;
        int row = local / columns;

        var scale = new Vector2(1f / columns, 1f / rows);
        var offset = new Vector2(col * scale.x, 1f - (row + 1) * scale.y);

        _runtimeMat.SetTexture(BaseMapId, sheets[sheetIndex]);
        _runtimeMat.SetTextureScale(BaseMapId, scale);
        _runtimeMat.SetTextureOffset(BaseMapId, offset);
        _shownFrame = frame;
    }
}
