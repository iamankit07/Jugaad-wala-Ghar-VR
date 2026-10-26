using System.Collections;
using UnityEngine;

/// <summary>
/// Last garage task: drive the car out through the shutter and stop it in the parking box outside.
/// The box lights up when the task starts. Once the car stands still inside it, Papa calls one last
/// time; when that call is over the task (and so the level) is done.
/// Put it on the LevelFlow object and drag it into the last step's condition.
/// </summary>
public class DriveOutCondition : TaskCondition
{
    [Header("Car")]
    [SerializeField] private CarLift car;
    [SerializeField] private CarDrive drive;

    [Header("Parking box")]
    [Tooltip("Centre of the box on the ground; its forward = along the box.")]
    [SerializeField] private Transform parkingSpot;
    [Tooltip("How far the car's centre may be from the box centre (x = across, y = along).")]
    [SerializeField] private Vector2 slack = new Vector2(0.9f, 1.3f);
    [SerializeField] private float stopSpeed = 0.08f;
    [SerializeField] private float holdTime = 0.6f;
    [Tooltip("Lines + text of the box; hidden until this task starts.")]
    [SerializeField] private GameObject marker;
    [SerializeField] private Renderer fill;
    [SerializeField] private TMPro.TMP_Text label;
    [SerializeField] private Color fillColor = new Color(1f, 0.8f, 0.1f, 0.18f);
    [SerializeField] private Color fillParkedColor = new Color(0.3f, 1f, 0.4f, 0.3f);

    [Header("Papa's last call")]
    [SerializeField] private IntroPhoneCall phone;
    [SerializeField] private float callDelay = 1.2f;
    [SerializeField] private string caller = "Papa";
    [SerializeField] private IntroPhoneCall.Line[] callEnglish;
    [SerializeField] private IntroPhoneCall.Line[] callHinglish;
    [SerializeField] private AudioClip voiceEnglish;
    [SerializeField] private AudioClip voiceHinglish;

    [SerializeField] private TaskHUD hud;

    public bool Parked { get; private set; }
    public Transform ParkingSpot => parkingSpot;

    private bool _active, _callStarted;
    private float _still;
    private MaterialPropertyBlock _mpb;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        if (car == null) car = FindFirstObjectByType<CarLift>();
        if (drive == null) drive = FindFirstObjectByType<CarDrive>();
        if (phone == null) phone = FindFirstObjectByType<IntroPhoneCall>();
        if (hud == null) hud = FindFirstObjectByType<TaskHUD>();
        if (marker != null) marker.SetActive(false);
        _mpb = new MaterialPropertyBlock();
    }

    public override void OnTaskStarted()
    {
        _active = true;
        Parked = false;
        _callStarted = false;
        _still = 0f;
        if (marker != null) marker.SetActive(true);
        if (label != null) label.text = GameLanguage.Pick("PARK HERE", "YAHAN GAADI LAGAO");
    }

    public override bool IsMet => Parked && (phone == null || (_callStarted && phone.Finished));

    /// <summary>The car's centre is inside the box.</summary>
    public bool CarInBox
    {
        get
        {
            if (car == null || parkingSpot == null) return false;
            Vector3 local = parkingSpot.InverseTransformPoint(car.LevelPosition);
            return Mathf.Abs(local.x) <= slack.x && Mathf.Abs(local.z) <= slack.y;
        }
    }

    private void Update()
    {
        if (!_active) return;
        bool inBox = CarInBox;

        if (!Parked)
        {
            float speed = drive != null ? Mathf.Abs(drive.Speed) : 0f;
            _still = inBox && speed < stopSpeed ? _still + Time.deltaTime : 0f;
            if (_still >= holdTime)
            {
                Parked = true;
                if (hud != null) hud.ShowToast(GameLanguage.Pick("Parked! Your phone's ringing...", "Gaadi lag gayi! Phone baj raha hai..."), hud.GoodColor, false);
                SfxMix.PlayAt(ProceduralSfx.Success, parkingSpot.position, 0.6f);
                StartCoroutine(Call());
            }
        }

        // the box breathes yellow while waiting, turns green once the car is in
        if (fill != null)
        {
            Color c = Parked || inBox ? fillParkedColor : fillColor;
            if (!Parked) c.a *= 0.65f + 0.35f * Mathf.Sin(Time.time * 3f);
            fill.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            fill.SetPropertyBlock(_mpb);
        }
    }

    private IEnumerator Call()
    {
        yield return new WaitForSeconds(callDelay);
        if (phone != null)
        {
            // another call still going (shouldn't happen) - wait for it
            while (phone.IsRinging) yield return null;
            phone.StartCall(caller, callEnglish, callHinglish, voiceEnglish, voiceHinglish);
        }
        _callStarted = true;
        yield return new WaitUntil(() => IsMet);
        if (marker != null) marker.SetActive(false);
        _active = false;
    }

    // tests
    public void ForcePark()
    {
        if (!_active || Parked) return;
        Parked = true;
        StartCoroutine(Call());
    }
}
