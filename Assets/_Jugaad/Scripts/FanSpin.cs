using UnityEngine;

/// <summary>
/// Spins a ceiling-fan blade object around its local Y axis.
/// Speed eases in on start so it doesn't snap to full speed.
/// Kept deliberately slow for VR: very fast blades strobe/flicker on a 72/90 Hz headset.
/// </summary>
public class FanSpin : MonoBehaviour
{
    [Tooltip("Degrees per second at full speed (240 = 40 RPM, calm and VR-friendly).")]
    [SerializeField] private float degreesPerSecond = 240f;

    [Tooltip("Seconds to reach full speed.")]
    [SerializeField] private float spinUpTime = 2f;

    [SerializeField] private bool isOn = true;

    private float _currentSpeed;

    public void SetOn(bool on) => isOn = on;
    public void Toggle() => isOn = !isOn;

    private void Update()
    {
        float target = isOn ? degreesPerSecond : 0f;
        float accel = spinUpTime > 0f ? degreesPerSecond / spinUpTime : float.MaxValue;
        _currentSpeed = Mathf.MoveTowards(_currentSpeed, target, accel * Time.deltaTime);

        if (_currentSpeed > 0.01f)
            transform.Rotate(0f, _currentSpeed * Time.deltaTime, 0f, Space.Self);
    }
}
