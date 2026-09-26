using UnityEngine;

/// <summary>
/// Soft collision sound for grabbable props (books, key, magnet, remote ...).
/// Put it on the object that has the Rigidbody. Quiet taps are skipped; harder hits are louder.
/// Silent for the first second so objects settling at scene start don't make noise.
/// </summary>
public class ImpactSound : MonoBehaviour
{
    [SerializeField] private AudioClip[] clips;
    [SerializeField, Range(0f, 1f)] private float volume = 0.5f;
    [SerializeField] private float minSpeed = 0.7f;   // m/s, below this: no sound
    [SerializeField] private float fullSpeed = 3f;    // m/s, at/above this: full volume
    [SerializeField] private float cooldown = 0.15f;
    [SerializeField] private float startDelay = 1f;

    private float _next;
    private int _last = -1;

    private void OnCollisionEnter(Collision c)
    {
        if (clips == null || clips.Length == 0) return;
        if (Time.timeSinceLevelLoad < startDelay || Time.time < _next) return;
        float v = c.relativeVelocity.magnitude;
        if (v < minSpeed) return;
        _next = Time.time + cooldown;

        int i = Random.Range(0, clips.Length);
        if (clips.Length > 1 && i == _last) i = (i + 1) % clips.Length;
        _last = i;

        float t = Mathf.InverseLerp(minSpeed, fullSpeed, v);
        Vector3 at = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
        SfxMix.PlayAt(clips[i], at, volume * Mathf.Lerp(0.25f, 1f, t), 0.07f);
    }
}
