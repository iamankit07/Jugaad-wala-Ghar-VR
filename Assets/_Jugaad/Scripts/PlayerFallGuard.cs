using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// Safety net: if the player ever ends up below the floor (fell off the edge of the map, glitched
/// through something), put them back where the level started, with a quick blink-free snap.
/// Put it on the XR Origin (player rig).
/// </summary>
public class PlayerFallGuard : MonoBehaviour
{
    [SerializeField] private float killY = -2f;
    [SerializeField] private TaskHUD hud;

    private Vector3 _startPos;
    private Quaternion _startRot;
    private CharacterController _cc;

    private void Start()
    {
        _startPos = transform.position;
        _startRot = transform.rotation;
        _cc = GetComponent<CharacterController>();
        if (hud == null) hud = FindFirstObjectByType<TaskHUD>();
    }

    private void LateUpdate()
    {
        if (transform.position.y > killY) return;
        if (_cc) _cc.enabled = false;
        transform.SetPositionAndRotation(_startPos, _startRot);
        if (_cc) _cc.enabled = true;
        if (hud) hud.ShowToast(GameLanguage.Pick("Oops - you fell! Back to the start.", "Arre - gir gaye! Wapas shuru ki jagah pe."), hud.WarnColor, false);
        Debug.Log("[PlayerFallGuard] player fell below " + killY + " - reset to start");
    }
}
