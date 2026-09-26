using UnityEngine;

/// <summary>
/// Marks an object as a "key" that can open a KeyLockSocket with the same keyId.
/// Put this on the same GameObject as the XRGrabInteractable (e.g. the safety pin).
/// </summary>
public class KeyItem : MonoBehaviour
{
    [Tooltip("Must match KeyLockSocket.requiredKeyId")]
    public string keyId = "safety_pin";
}
