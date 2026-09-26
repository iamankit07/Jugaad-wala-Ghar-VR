using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// The jugaad remote: its battery cover is broken, so it only works once a rubber band
/// is wrapped around it (the band sits in a KeyLockSocket on the remote).
/// Hold it and press TRIGGER to toggle the TV.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class BrokenRemote : MonoBehaviour
{
    [SerializeField] private KeyLockSocket bandSocket;
    [SerializeField] private TVController tv;
    [Tooltip("Anything else to toggle when the fixed remote's trigger is pressed (e.g. RoomPowerController.ToggleFan).")]
    public UnityEvent onPressed;

    [Header("Broken / fixed look")]
    [SerializeField] private Transform batteryCover;
    [SerializeField] private Vector3 coverClosedLocalEuler;
    [SerializeField] private Vector3 coverOpenLocalEuler = new Vector3(-40f, 0f, 0f);
    [SerializeField] private Transform[] batteries;
    [Tooltip("How far (local) each battery pops out while the cover is open.")]
    [SerializeField] private Vector3 batteryPoppedOffset = new Vector3(0f, -0.006f, 0f);
    [SerializeField] private float batteryPoppedTilt = 12f;

    [Header("Feedback")]
    [SerializeField] private AudioClip clickSound;
    [SerializeField] private AudioClip deadClickSound;

    public bool IsFixed => bandSocket != null && bandSocket.HasKey;

    private XRGrabInteractable _grab;
    private Vector3[] _batteryHome;
    private Quaternion[] _batteryHomeRot;
    private bool _lastFixed = true;   // force first refresh

    private void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        if (batteries != null)
        {
            _batteryHome = new Vector3[batteries.Length];
            _batteryHomeRot = new Quaternion[batteries.Length];
            for (int i = 0; i < batteries.Length; i++)
            {
                _batteryHome[i] = batteries[i].localPosition;
                _batteryHomeRot[i] = batteries[i].localRotation;
            }
        }
        RefreshLook(true);
    }

    private void OnEnable() => _grab.activated.AddListener(OnTrigger);
    private void OnDisable() => _grab.activated.RemoveListener(OnTrigger);

    private void Update() => RefreshLook(false);

    private void RefreshLook(bool force)
    {
        bool fixedNow = IsFixed;
        if (!force && fixedNow == _lastFixed) return;
        _lastFixed = fixedNow;

        if (batteryCover)
            batteryCover.localRotation = Quaternion.Euler(fixedNow ? coverClosedLocalEuler : coverOpenLocalEuler);

        if (batteries != null && _batteryHome != null)
            for (int i = 0; i < batteries.Length; i++)
            {
                if (!batteries[i]) continue;
                float sign = (i % 2 == 0) ? 1f : -1f;
                batteries[i].localPosition = fixedNow ? _batteryHome[i] : _batteryHome[i] + batteryPoppedOffset * (1f + 0.5f * i);
                batteries[i].localRotation = fixedNow ? _batteryHomeRot[i] : _batteryHomeRot[i] * Quaternion.Euler(batteryPoppedTilt * sign, 0f, 0f);
            }
    }

    private void OnTrigger(ActivateEventArgs args)
    {
        if (!IsFixed)
        {
            // batteries rattle, nothing happens
            if (deadClickSound) SfxMix.PlayAt(deadClickSound, transform.position, 0.5f);
            Haptic(args, 0.15f, 0.05f);
            return;
        }

        if (clickSound) SfxMix.PlayAt(clickSound, transform.position, 0.5f);
        Haptic(args, 0.4f, 0.08f);
        if (tv) tv.Toggle();
        onPressed?.Invoke();
    }

    private static void Haptic(ActivateEventArgs args, float amp, float dur)
    {
        if (args.interactorObject is XRBaseInputInteractor input)
            input.SendHapticImpulse(amp, dur);
    }
}
