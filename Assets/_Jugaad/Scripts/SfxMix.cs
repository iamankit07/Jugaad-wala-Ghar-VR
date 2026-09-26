using UnityEngine;

/// <summary>
/// One place for every 3D one-shot sound in the game. Replaces AudioSource.PlayClipAtPoint so that
/// (1) a single Master volume controls the whole mix, (2) sounds fall off quickly (room-scale VR),
/// (3) a tiny random pitch change stops repeated sounds from feeling robotic / irritating.
/// </summary>
public static class SfxMix
{
    /// <summary>Global volume for all world sounds (0..1). Kenney clips are mastered loud, so keep this low.</summary>
    public static float Master = 0.6f;

    public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitchJitter = 0.04f)
    {
        if (clip == null) return;
        var go = new GameObject("Sfx_" + clip.name);
        go.transform.position = position;
        var s = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.playOnAwake = false;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.minDistance = 0.7f;
        s.maxDistance = 15f;
        s.dopplerLevel = 0f;
        s.volume = Mathf.Clamp01(volume * Master);
        s.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        s.Play();
        Object.Destroy(go, clip.length / Mathf.Max(0.5f, s.pitch) + 0.1f);
    }
}
