using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// A wheel that can go on the car's jacked-up hub (WheelHub) - the punctured one or any of the
/// spare wheels lying in the garage. Its pivot must be the wheel centre, outer face along local -X.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class CarWheel : MonoBehaviour
{
    public static readonly List<CarWheel> All = new List<CarWheel>();

    [SerializeField] private bool isFlat;
    [Tooltip("Centre to the bottom of the tyre (m).")]
    [SerializeField] private float radius = 0.308f;

    public bool IsFlat => isFlat;
    public float Radius => radius;
    public XRGrabInteractable Grab { get; private set; }
    public Rigidbody Body { get; private set; }

    private void Awake()
    {
        Grab = GetComponent<XRGrabInteractable>();
        Body = GetComponent<Rigidbody>();
        if (!All.Contains(this)) All.Add(this);
    }

    private void OnDestroy() { All.Remove(this); }
}
