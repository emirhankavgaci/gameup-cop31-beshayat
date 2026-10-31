using UnityEngine;

/// <summary>
/// CANOPY: The Last Run - Tüm Parkur Sistemlerini (Wall Run Zinciri, Sarkaç ve Son Bırakma Sebebi,
/// Slide Engel Nedeni ve Hız Vektörleri) Ekranda Gösteren OnGUI Debug Paneli.
/// </summary>
public class PlayerDebugHUD : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("İzlenecek merkezi PlayerMotor bileşeni.")]
    [SerializeField] private PlayerMotor motor;

    [Tooltip("İzlenecek WallRunHandler bileşeni (opsiyonel).")]
    [SerializeField] private WallRunHandler wallRunHandler;

    [Tooltip("İzlenecek BranchGrabHandler bileşeni (opsiyonel).")]
    [SerializeField] private BranchGrabHandler branchGrabHandler;

    [Tooltip("İzlenecek SlideHandler bileşeni (opsiyonel).")]
    [SerializeField] private SlideHandler slideHandler;

    [Header("Görünüm ve Kontrol Ayarları")]
    [Tooltip("Debug HUD panelini ekranda göster/gizle.")]
    [SerializeField] private bool showDebugHud = true;

    [Tooltip("Oyun içinde Debug HUD panelini açıp kapatmak için kısayol tuşu.")]
    [SerializeField] private KeyCode toggleHudKey = KeyCode.F3;

    [Tooltip("Panelin sol üst köşeden X uzaklığı (piksel).")]
    [SerializeField] private float panelMarginX = 16f;

    [Tooltip("Panelin sol üst köşeden Y uzaklığı (piksel).")]
    [SerializeField] private float panelMarginY = 16f;

    [Tooltip("Panel genişliği (piksel).")]
    [SerializeField] private float panelWidth = 420f;

    [Tooltip("Panel yüksekliği (piksel).")]
    [SerializeField] private float panelHeight = 420f;

    [Tooltip("Yazı puntosu büyüklüğü.")]
    [SerializeField] private int fontSize = 13;

    private GUIStyle boxStyle;
    private GUIStyle labelStyle;
    private GUIStyle headerStyle;

    private void Awake()
    {
        if (motor == null)
            motor = GetComponent<PlayerMotor>();
        if (wallRunHandler == null)
            wallRunHandler = GetComponent<WallRunHandler>();
        if (branchGrabHandler == null)
            branchGrabHandler = GetComponent<BranchGrabHandler>();
        if (slideHandler == null)
            slideHandler = GetComponent<SlideHandler>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleHudKey))
        {
            showDebugHud = !showDebugHud;
        }
    }

    private void OnGUI()
    {
        if (!showDebugHud || motor == null)
            return;

        EnsureStylesInitialized();

        Rect panelRect = new Rect(panelMarginX, panelMarginY, panelWidth, panelHeight);
        GUI.Box(panelRect, GUIContent.none, boxStyle);

        GUILayout.BeginArea(new Rect(panelMarginX + 12f, panelMarginY + 10f, panelWidth - 24f, panelHeight - 20f));

        GUILayout.Label("CANOPY: MONKEY PARKOUR HUD [F3]", headerStyle);
        GUILayout.Space(3f);

        GUILayout.Label($"Durum (State): <b>{motor.CurrentState}</b> (CC: {(motor.Controller.enabled ? "AÇIK" : "KAPALI")})", labelStyle);
        GUILayout.Label($"Yatay / Dikey Hız: <b>{motor.HorizontalSpeed:F2} m/s</b> | <b>{motor.Velocity.y:F2} m/s</b>", labelStyle);

        float slopeAngle = Vector3.Angle(motor.GroundNormal, Vector3.up);
        string groundedText = motor.IsGrounded ? $"EVET ({slopeAngle:F1}°)" : "HAYIR";
        GUILayout.Label($"Zeminde mi: <b>{groundedText}</b>", labelStyle);

        // 1. Slide ve Sprint Bilgileri
        if (slideHandler != null)
        {
            GUILayout.Space(3f);
            bool sprintReady = slideHandler.SprintDurationTimer >= slideHandler.MinSprintTimeRequired;
            string sprintColor = sprintReady ? "#55FF88" : "#FFFFFF";
            GUILayout.Label(
                $"Sprint Süresi: <color={sprintColor}><b>{slideHandler.SprintDurationTimer:F2}s</b></color> / {slideHandler.MinSprintTimeRequired:F1}s",
                labelStyle
            );

            string slideStatus;
            if (slideHandler.IsSliding)
            {
                slideStatus = slideHandler.IsCeilingBlocked
                    ? "<color=#FFAA44><b>AKTİF (Tavan Engeli!)</b></color>"
                    : $"<color=#55FF88><b>AKTİF ({slideHandler.RemainingSlideTime:F2}s kaldı)</b></color>";
            }
            else if (slideHandler.CurrentSlideBlockReason == "ready")
            {
                slideStatus = "<color=#55FF88><b>HAZIR [C]</b></color>";
            }
            else
            {
                slideStatus = $"<color=#FF6666><b>slide blocked: {slideHandler.CurrentSlideBlockReason}</b></color>";
            }

            GUILayout.Label(
                $"Slide [C]: {slideStatus} | Cooldown: <b>{slideHandler.SlideCooldownRemaining:F2}s</b>",
                labelStyle
            );
        }

        // 2. Dal ve Sarkaç Bilgileri
        if (branchGrabHandler != null)
        {
            GUILayout.Space(3f);
            string activeBranchName = branchGrabHandler.CurrentBranch != null
                ? branchGrabHandler.CurrentBranch.name
                : "YOK";

            GUILayout.Label($"Tutulan Dal: <b>{activeBranchName}</b> | branchT: <b>{branchGrabHandler.CurrentBranchT:F2}</b>", labelStyle);
            GUILayout.Label(
                $"swingAngle: <b>{branchGrabHandler.SwingAngleDegrees:F1}°</b> | swingAngVel: <b>{branchGrabHandler.SwingAngularVelocity:F2} rad/s</b>",
                labelStyle
            );
            GUILayout.Label($"lastReleaseReason: <b>{branchGrabHandler.LastReleaseReason}</b>", labelStyle);

            bool transferAvailable = branchGrabHandler.HasTransferTarget;
            string blockedNote = branchGrabHandler.IsTransferBlockedBySwingInput
                ? " <color=#FF6666><b>(blocked by swing input)</b></color>"
                : string.Empty;

            GUILayout.Label($"transferAvailable: <b>{transferAvailable}</b>{blockedNote}", labelStyle);

            string targetBranchInfo = branchGrabHandler.CurrentTransferTarget != null
                ? $"{branchGrabHandler.CurrentTransferTarget.name} ({branchGrabHandler.CandidateTransferDistance:F1}m | Skor: {branchGrabHandler.CandidateTransferScore:F2})"
                : "YOK";

            GUILayout.Label($"Hedef Dal [E]: <b>{targetBranchInfo}</b>", labelStyle);
        }

        // 3. Wall Run ve Duvardan Duvara Zincirleme Bilgileri
        if (wallRunHandler != null)
        {
            GUILayout.Space(3f);
            string sideInfo = motor.CurrentState == PlayerMotor.PlayerState.WallRunning
                ? (wallRunHandler.IsWallRight ? " (SAĞ)" : " (SOL)")
                : string.Empty;

            GUILayout.Label(
                $"Aktif Duvar: <b>{wallRunHandler.ActiveWallName}{sideInfo}</b> | Kalan Süre: <b>{wallRunHandler.WallRunTimer:F2}s</b>",
                labelStyle
            );
            GUILayout.Label(
                $"lastWallCollider: <b>{wallRunHandler.LastWallColliderName}</b> | Zincir (Chain): <b>{wallRunHandler.ChainCount}</b>",
                labelStyle
            );

            Vector3 wjDir = wallRunHandler.LastWallJumpDirection;
            GUILayout.Label(
                $"WallJump Yönü: <b>({wjDir.x:F2}, {wjDir.y:F2}, {wjDir.z:F2})</b>",
                labelStyle
            );
        }

        GUILayout.EndArea();
    }

    private void EnsureStylesInitialized()
    {
        if (labelStyle != null)
            return;

        boxStyle = new GUIStyle(GUI.skin.box);

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize + 1,
            fontStyle = FontStyle.Bold
        };
        headerStyle.normal.textColor = new Color(0.4f, 1f, 0.6f, 1f);

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            richText = true
        };
        labelStyle.normal.textColor = Color.white;
    }
}
