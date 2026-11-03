using System;
using UnityEngine;

/// <summary>
/// Kamera bakış modu: Birinci Şahıs (FirstPerson) veya Üçüncü Şahıs (ThirdPerson).
/// </summary>
public enum ViewMode
{
    FirstPerson = 0,
    ThirdPerson = 1
}

/// <summary>
/// CANOPY: The Last Run - Birinci ve Üçüncü Şahıs Kamera, Sabit "Eye" Kamera Konumlandırma,
/// Ayrı Offset Mimarisi (Bug Fix 3), Mouse Look ve Dinamik FOV Yöneticisi.
///
/// Ayrı Offset Sistemi (Bug Fix 3):
/// - basePitch YALNIZCA mouse Y girdisi ile değişir ve [-89, +89] arasında clamp'lenir.
/// - transform.localEulerAngles ile geri okuma ASLA yapılmaz (0-360 sarma hatasını önler).
/// - swingPitchOffset, swingRollOffset, transferPitchOffset (±15° sınırı), slideRollOffset ve wallRunRollOffset
///   tamamen ayrı float alanlarda tutulur.
/// - Dal bırakıldığında ResetCameraOffsets() çağrılır ve tüm sarkaç/geçiş offsetleri cameraOffsetResetTime
///   (varsayılan 0.25 sn) içinde SmoothDamp ile sıfıra döner.
/// </summary>
public class PlayerLook : MonoBehaviour
{
    private const float MAX_TRANSFER_LOOK_OFFSET_DEG = 15f;

    [Header("Referanslar")]
    [Tooltip("Yatay eksende (Y) döndürülecek oyuncu ana gövdesi.")]
    [SerializeField] private Transform playerBody;

    [Tooltip("Kontrol edilecek Camera bileşeni.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("Maymun modelinin 'Eye' kamera noktasını ve görünürlüğünü yöneten PlayerVisual bileşeni.")]
    [SerializeField] private PlayerVisual playerVisual;

    [Tooltip("Zıplama ve yere iniş sarsıntısı için PlayerMotor bileşeni (boşsa otomatik bulunur).")]
    [SerializeField] private PlayerMotor playerMotor;

    [Tooltip("Üçüncü şahıs modunda soluklaştırılacak CrosshairUI bileşeni (boşsa otomatik bulunur).")]
    [SerializeField] private CrosshairUI crosshairUI;

    [Header("Sabit Eye (Göz) Kamera Bağlantısı")]
    [Tooltip("FirstPerson modunda kamerayı Monkey_B1 'Eye' yüksekliğinde tamamen SABİT tutar (gecikme/lag olmaz).")]
    [SerializeField] private bool attachCameraToEyeMesh = true;

    [Header("Bakış Modu (FirstPerson / ThirdPerson) Ayarları")]
    [Tooltip("Başlangıç ve aktif kamera bakış modu (varsayılan FirstPerson).")]
    [SerializeField] private ViewMode currentViewMode = ViewMode.FirstPerson;

    [Tooltip("V tuşu ile FirstPerson / ThirdPerson geçişine izin ver (kullanıcı talebiyle kapatılmıştır).")]
    [SerializeField] private bool allowViewToggle = false;

    [Tooltip("Bakış modu değiştirme kısayol tuşu (varsayılan V).")]
    [SerializeField] private KeyCode viewToggleKey = KeyCode.V;

    [Tooltip("Üçüncü şahıs modunda kameranın oyuncuya göre yerel ofseti (varsayılan: 0, 1.2, -3.2).")]
    [SerializeField] private Vector3 thirdPersonOffset = new Vector3(0f, 1.2f, -3.2f);

    [Tooltip("FirstPerson ve ThirdPerson arasındaki yumuşak geçiş süresi (varsayılan ~0.25 sn).")]
    [SerializeField] private float viewTransitionDuration = 0.25f;

    [Tooltip("Üçüncü şahıs kamerasının duvar/dal içine girmesini önleyen SphereCast yarıçapı (metre).")]
    [SerializeField] private float thirdPersonCollisionRadius = 0.22f;

    [Tooltip("Üçüncü şahıs kamerasının çarpıştığı yüzeyden bırakacağı güvenlik payı (metre).")]
    [SerializeField] private float thirdPersonWallPadding = 0.12f;

    [Tooltip("Üçüncü şahıs kamera çarpışma kontrolünde dikkate alınacak katmanlar.")]
    [SerializeField] private LayerMask thirdPersonCollisionMask = ~0;

    [Header("Mouse Look Ayarları")]
    [Tooltip("Fare yatay ve dikey dönüş hassasiyeti.")]
    [SerializeField] private float mouseSensitivity = 2.2f;

    [Tooltip("Yukarı/aşağı bakış açısı alt sınırı (derece).")]
    [SerializeField] private float minVerticalAngle = -89f;

    [Tooltip("Yukarı/aşağı bakış açısı üst sınırı (derece).")]
    [SerializeField] private float maxVerticalAngle = 89f;

    [Tooltip("Oyun başladığında fare imlecini ekran ortasına kilitle.")]
    [SerializeField] private bool lockCursorOnStart = true;

    [Header("FOV (Görüş Açısı) Ayarları")]
    [Tooltip("Temel ve varsayılan kamera görüş açısı (90 sabit temel değer).")]
    [SerializeField] private float baseFieldOfView = 90f;

    [Tooltip("Sallanırken, koşarken veya kayarken dinamik FOV artışını açar/kapatır.")]
    [SerializeField] private bool enableDynamicFov = true;

    [Tooltip("Sallanırken veya yüksek hızda eklenecek maksimum FOV artışı (en fazla +6 derece ile 96'ya çıkar).")]
    [Range(0f, 6f)]
    [SerializeField] private float maxFovBoost = 6f;

    [Tooltip("Dinamik FOV artışının başlayacağı alt hız eşiği (m/s).")]
    [SerializeField] private float fovMinSpeedThreshold = 5.5f;

    [Tooltip("Maksimum FOV artışına ulaşılan hız değeri (m/s).")]
    [SerializeField] private float fovMaxSpeedThreshold = 13f;

    [Tooltip("FOV değişimlerinin yumuşatma hızı.")]
    [SerializeField] private float fovLerpSpeed = 10f;

    [Header("Kamera Sis (Fog) ve Maksimum Görüş Mesafesi (50m)")]
    [Tooltip("Açık olduğunda sahnede Linear Fog aktif edilir ve karakter maxViewDistance (50 m) ötesini göremez.")]
    [SerializeField] private bool enableViewDistanceFog = true;

    [Tooltip("Sisin yoğunlaşmaya başlayacağı başlangıç mesafesi (varsayılan 15 m).")]
    [SerializeField] private float fogStartDistance = 15f;

    [Tooltip("Sisin %100 kapandığı ve ötesinin kesinlikle görünmediği maksimum görüş mesafesi (varsayılan 50 m).")]
    [SerializeField] private float maxViewDistance = 50f;

    [Tooltip("Varsayılan sis rengi (ZoneManager yoksa bu renk kullanılır).")]
    [SerializeField] private Color defaultFogColor = new Color(0.55f, 0.68f, 0.62f, 1f);

    [Tooltip("Kameranın Far Clip Plane değerini de maksimum görüş mesafesine (50 m) sınırla (50 m ötesini hiç çizmez).")]
    [SerializeField] private bool limitCameraFarClipToMaxView = true;

    [Tooltip("50 m ötesi kesildiğinde ufukta keskin çizgi oluşmaması için kamera arka planını sis rengiyle eşitle.")]
    [SerializeField] private bool matchCameraBackgroundToFog = true;

    [Header("Ayrı Kamera Offset Sistemi (Sarkaç, Transfer, Wall Run, Slide)")]
    [Tooltip("Dal bırakıldığında tüm swing ve transfer kamera offsetlerinin SmoothDamp ile sıfıra dönme süresi (varsayılan 0.25 sn).")]
    [SerializeField] private float cameraOffsetResetTime = 0.25f;

    [Tooltip("Aktif sarkaç, wall run ve slide sırasında kamera offsetlerinin hedefe yaklaşma hızı.")]
    [SerializeField] private float offsetLerpSpeed = 12f;

    [Tooltip("Slide sırasında kamera yükseklik ofsetinin yumuşak geçiş hızı.")]
    [SerializeField] private float heightOffsetLerpSpeed = 14f;

    [Header("Hafif ve Dengeli Kamera Sarsıntısı (Camera Shake)")]
    [Tooltip("Hareket ederken hafif ve gerçekçi kamera sarsıntısını açar/kapatır.")]
    [SerializeField] private bool enableHeadBob = true;

    [Tooltip("Yürürken adım frekansı.")]
    [SerializeField] private float walkBobFrequency = 11f;

    [Tooltip("Sprint atarken adım frekansı.")]
    [SerializeField] private float sprintBobFrequency = 15f;

    [Tooltip("Hareket ederken çok hafif dikey sarsıntı genliği (metre). Kamera sabitliğini bozmaz.")]
    [SerializeField] private float bobVerticalAmplitude = 0.012f;

    [Tooltip("Hareket ederken çok hafif yatay sarsıntı genliği (metre).")]
    [SerializeField] private float bobHorizontalAmplitude = 0.008f;

    [Tooltip("Hareket ederken kameraya uygulanan hafif açısal sarsıntı (derece).")]
    [SerializeField] private float movementRotationShakeDegrees = 0.35f;

    [Tooltip("Organik Perlin gürültü sarsıntısının frekansı.")]
    [SerializeField] private float perlinShakeFrequency = 8f;

    [Tooltip("Zıplama ve yere iniş anındaki darbe sarsıntısı çarpanı.")]
    [SerializeField] private float impactShakeIntensity = 0.55f;

    [Tooltip("Durduğunda sarsıntının merkeze dönüş yumuşaklığı.")]
    [SerializeField] private float bobResetSpeed = 14f;

    // Olaylar
    public event Action<ViewMode> OnViewModeChanged;

    // BUG FIX 3: Tamamen ayrı tutulan kamera açı ve offset alanları
    private float basePitch; // SADECE mouse Y ile değişir!
    private float swingPitchOffset;
    private float swingRollOffset;
    private float transferPitchOffset;
    private float slideRollOffset;
    private float wallRunRollOffset;

    // Hedef offsetler ve SmoothDamp hızları
    private float targetSwingPitchOffset;
    private float targetSwingRollOffset;
    private float targetTransferPitchOffset;
    private float targetSlideRollOffset;
    private float targetSlideFovBoost;
    private float targetCameraHeightOffset;
    private float currentCameraHeightOffset;

    private float swingPitchSmoothVel;
    private float swingRollSmoothVel;
    private float transferPitchSmoothVel;
    private float slideRollSmoothVel;
    private float wallRunRollSmoothVel;

    // Kamera sarsıntısı ve genel durum değişkenleri
    private float headBobTimer;
    private float perlinSeedTime;
    private float impactTrauma;
    private Vector3 currentShakePosOffset;
    private Vector3 currentShakeRotEuler;
    private Vector3 externalShakePosOffset;
    private Vector3 externalShakeRotEuler;
    private Vector3 initialPivotLocalPos;
    private float thirdPersonBlend;
    private float thirdPersonBlendVelocity;
    private bool allowMouseInput = true;
    private int lastMouseProcessedFrame = -1;

    // Boğulma (Drowning) kamera durumu
    private bool isDrowningCameraActive;
    private float drowningTargetFov = 75f;
    private float drowningFovTransitionSpeed = 5f;
    private float drowningSwayTimer;
    private float currentDrownPitchSway;
    private float currentDrownRollSway;

    // Dış erişim ve Debug HUD özellikleri
    public Camera TargetCamera => targetCamera;
    public ViewMode CurrentViewMode => currentViewMode;
    public bool AllowViewToggle => allowViewToggle;
    public float BasePitch => basePitch;
    public float SwingPitchOffset => swingPitchOffset;
    public float SwingRollOffset => swingRollOffset;
    public float TransferPitchOffset => transferPitchOffset;
    public float SlideRollOffset => slideRollOffset;
    public float WallRunRollOffset => wallRunRollOffset;
    public float TotalPitchOffset => swingPitchOffset + transferPitchOffset + currentDrownPitchSway;
    public float TotalRollOffset => wallRunRollOffset + swingRollOffset + slideRollOffset + currentDrownRollSway;
    public float CurrentPitchOffset => TotalPitchOffset;
    public float CurrentRollOffset => TotalRollOffset;
    public float CurrentCameraHeightOffset => currentCameraHeightOffset;
    public bool AreOffsetsReset =>
        Mathf.Abs(swingPitchOffset) < 0.05f &&
        Mathf.Abs(swingRollOffset) < 0.05f &&
        Mathf.Abs(transferPitchOffset) < 0.05f;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = GetComponentInChildren<Camera>();

        if (playerBody == null && transform.parent != null)
            playerBody = transform.parent;

        if (playerMotor == null && playerBody != null)
            playerMotor = playerBody.GetComponent<PlayerMotor>();

        if (playerVisual == null && playerBody != null)
            playerVisual = playerBody.GetComponentInChildren<PlayerVisual>();

        if (crosshairUI == null)
            crosshairUI = FindObjectOfType<CrosshairUI>();

        initialPivotLocalPos = transform.localPosition;

        if (bobVerticalAmplitude > 0.02f) bobVerticalAmplitude = 0.012f;
        if (bobHorizontalAmplitude > 0.015f) bobHorizontalAmplitude = 0.008f;
        if (movementRotationShakeDegrees > 0.55f) movementRotationShakeDegrees = 0.35f;
        if (impactShakeIntensity > 0.8f) impactShakeIntensity = 0.55f;

        if (targetCamera != null)
        {
            targetCamera.fieldOfView = baseFieldOfView;
            targetCamera.nearClipPlane = 0.03f;
        }

        ApplyCameraViewDistanceFog();

        // Third Person geçişi tamamen kaldırıldı - daima First Person modunda kilitli
        allowViewToggle = false;
        currentViewMode = ViewMode.FirstPerson;
        thirdPersonBlend = 0f;
        ApplyViewModeState(ViewMode.FirstPerson);
    }

    private void OnEnable()
    {
        if (playerMotor == null && playerBody != null)
            playerMotor = playerBody.GetComponent<PlayerMotor>();

        if (playerMotor != null)
        {
            playerMotor.OnJumpCallback -= HandlePlayerJumpShake;
            playerMotor.OnJumpCallback += HandlePlayerJumpShake;

            playerMotor.OnLandCallback -= HandlePlayerLandShake;
            playerMotor.OnLandCallback += HandlePlayerLandShake;
        }
    }

    private void OnDisable()
    {
        if (playerMotor != null)
        {
            playerMotor.OnJumpCallback -= HandlePlayerJumpShake;
            playerMotor.OnLandCallback -= HandlePlayerLandShake;
        }
    }

    private void Start()
    {
        if (lockCursorOnStart)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        // "V" tuşu ile third person moduna geçiş tamamen kaldırıldı

        if (allowMouseInput)
        {
            ProcessMouseLookForCurrentFrame();
        }
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (playerVisual != null)
        {
            playerVisual.SyncVisualForCurrentFrame(dt);
        }

        if (enableViewDistanceFog && !isDrowningCameraActive)
        {
            SyncCameraFarClipAndBackgroundWithFog();
        }

        UpdateCameraViewPosition(dt);
        ApplyCameraRotation();
    }

    /// <summary>
    /// Karakterin maxViewDistance (50 m) ötesini görmemesi için Linear Fog ve Camera Far Clip ayarlarını uygular.
    /// </summary>
    public void ApplyCameraViewDistanceFog()
    {
        if (!enableViewDistanceFog)
            return;

        float safeEnd = Mathf.Max(5f, maxViewDistance);
        float safeStart = Mathf.Clamp(fogStartDistance, 0f, safeEnd - 1f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = safeStart;
        RenderSettings.fogEndDistance = safeEnd;

        if (RenderSettings.fogColor.a <= 0.01f)
        {
            RenderSettings.fogColor = defaultFogColor;
        }

        SyncCameraFarClipAndBackgroundWithFog();
    }

    private void SyncCameraFarClipAndBackgroundWithFog()
    {
        float safeEnd = Mathf.Max(5f, maxViewDistance);
        float safeStart = Mathf.Clamp(fogStartDistance, 0f, safeEnd - 1f);

        if (RenderSettings.fogMode != FogMode.Linear)
        {
            RenderSettings.fogMode = FogMode.Linear;
        }

        RenderSettings.fogStartDistance = safeStart;
        RenderSettings.fogEndDistance = safeEnd;

        if (targetCamera != null)
        {
            if (limitCameraFarClipToMaxView)
            {
                targetCamera.farClipPlane = safeEnd;
            }

            if (matchCameraBackgroundToFog)
            {
                targetCamera.clearFlags = CameraClearFlags.SolidColor;
                targetCamera.backgroundColor = RenderSettings.fogColor;
            }
        }
    }

    private void HandlePlayerJumpShake()
    {
        AddCameraShakeImpulse(0.35f);
    }

    private void HandlePlayerLandShake(float landingVerticalSpeed)
    {
        float fallStrength = Mathf.InverseLerp(2f, 18f, Mathf.Abs(landingVerticalSpeed));
        AddCameraShakeImpulse(Mathf.Clamp(0.25f + fallStrength * 0.55f, 0.25f, 0.85f));
    }

    public void AddCameraShakeImpulse(float normalizedImpulse)
    {
        impactTrauma = Mathf.Clamp01(impactTrauma + normalizedImpulse);
    }

    public void SetExternalShakeOffset(Vector3 positionOffset, Vector3 rotationEulerOffset)
    {
        externalShakePosOffset = positionOffset;
        externalShakeRotEuler = rotationEulerOffset;
    }

    /// <summary>
    /// Checkpoint'e ışınlanma (Respawn) sırasında kameranın basePitch açısını ve tüm geçici ofsetlerini sıfırlar.
    /// </summary>
    public void ResetLookState(Quaternion targetBodyRotation)
    {
        basePitch = 0f;
        swingPitchOffset = 0f;
        swingRollOffset = 0f;
        transferPitchOffset = 0f;
        slideRollOffset = 0f;
        wallRunRollOffset = 0f;

        targetSwingPitchOffset = 0f;
        targetSwingRollOffset = 0f;
        targetTransferPitchOffset = 0f;
        targetSlideRollOffset = 0f;
        targetSlideFovBoost = 0f;
        targetCameraHeightOffset = 0f;
        currentCameraHeightOffset = 0f;

        swingPitchSmoothVel = 0f;
        swingRollSmoothVel = 0f;
        transferPitchSmoothVel = 0f;
        slideRollSmoothVel = 0f;
        wallRunRollSmoothVel = 0f;

        impactTrauma = 0f;
        currentShakePosOffset = Vector3.zero;
        currentShakeRotEuler = Vector3.zero;
        externalShakePosOffset = Vector3.zero;
        externalShakeRotEuler = Vector3.zero;
        allowMouseInput = true;
        isDrowningCameraActive = false;
        drowningSwayTimer = 0f;
        currentDrownPitchSway = 0f;
        currentDrownRollSway = 0f;

        if (targetCamera != null)
        {
            targetCamera.fieldOfView = baseFieldOfView;
        }

        if (playerBody != null)
        {
            Vector3 euler = targetBodyRotation.eulerAngles;
            playerBody.rotation = Quaternion.Euler(0f, euler.y, 0f);
        }

        ApplyCameraRotation();
    }

    public void ToggleViewMode()
    {
        // Third Person modu tamamen kaldırıldı - daima First Person
        SetViewMode(ViewMode.FirstPerson);
    }

    public void SetViewMode(ViewMode newMode)
    {
        currentViewMode = ViewMode.FirstPerson;
        ApplyViewModeState(ViewMode.FirstPerson);
        OnViewModeChanged?.Invoke(ViewMode.FirstPerson);
    }

    private void ApplyViewModeState(ViewMode mode)
    {
        if (playerVisual == null && playerBody != null)
        {
            playerVisual = playerBody.GetComponentInChildren<PlayerVisual>();
        }

        if (playerVisual != null)
        {
            playerVisual.SetViewMode(mode);
        }

        if (crosshairUI != null)
        {
            crosshairUI.SetThirdPersonDimmed(mode == ViewMode.ThirdPerson);
        }
    }

    /// <summary>
    /// Yalnızca Mouse X (playerBody yaw) ve Mouse Y (basePitch) girdisini işler.
    /// Hiçbir harici sistem basePitch'i değiştiremez.
    /// </summary>
    public void ProcessMouseLookForCurrentFrame()
    {
        if (Time.timeScale <= 0f || !allowMouseInput || lastMouseProcessedFrame == Time.frameCount)
            return;

        lastMouseProcessedFrame = Time.frameCount;

        float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        basePitch -= mouseY;
        basePitch = Mathf.Clamp(basePitch, minVerticalAngle, maxVerticalAngle);

        ApplyCameraRotation();

        if (playerBody != null)
        {
            playerBody.Rotate(Vector3.up * mouseX);
        }
    }

    public Vector3 GetInstantCameraForward()
    {
        ProcessMouseLookForCurrentFrame();
        if (targetCamera != null)
            return targetCamera.transform.forward;
        return transform.forward;
    }

    public Vector3 GetInstantHorizontalLookDirection()
    {
        ProcessMouseLookForCurrentFrame();
        if (playerBody != null)
        {
            Vector3 bodyFlat = new Vector3(playerBody.forward.x, 0f, playerBody.forward.z);
            if (bodyFlat.sqrMagnitude > 0.0001f)
                return bodyFlat.normalized;
        }

        Vector3 camFwd = targetCamera != null ? targetCamera.transform.forward : transform.forward;
        Vector3 flatFwd = new Vector3(camFwd.x, 0f, camFwd.z);
        if (flatFwd.sqrMagnitude > 0.0001f)
        {
            return flatFwd.normalized;
        }

        return Vector3.forward;
    }

    /// <summary>
    /// Sarkaç (Hanging) sırasında pitch ve roll offset hedeflerini ayarlar (basePitch'e dokunmaz).
    /// </summary>
    public void SetSwingOffset(float pitchOffsetDeg, float rollOffsetDeg)
    {
        targetSwingPitchOffset = pitchOffsetDeg;
        targetSwingRollOffset = rollOffsetDeg;
    }

    /// <summary>
    /// Geriye dönük uyumluluk için SetSwingOffset çağrısı.
    /// </summary>
    public void SetSwingCameraOffsets(float pitchOffsetDeg, float rollOffsetDeg)
    {
        SetSwingOffset(pitchOffsetDeg, rollOffsetDeg);
    }

    /// <summary>
    /// Dallar arası geçiş (Transferring) sırasında yalnızca transferPitchOffset'i ayarlar (en fazla ±15 derece).
    /// basePitch değerine ASLA yazmaz!
    /// </summary>
    public void SetTransferLookOffset(float pitchOffsetDeg)
    {
        targetTransferPitchOffset = Mathf.Clamp(pitchOffsetDeg, -MAX_TRANSFER_LOOK_OFFSET_DEG, MAX_TRANSFER_LOOK_OFFSET_DEG);
    }

    /// <summary>
    /// Dal bırakıldığında (Space fırlama, Left Ctrl bırakma, Transfer bitişi/iptali) tüm sarkaç ve geçiş
    /// hedef offsetlerini sıfırlar; mevcut offsetler cameraOffsetResetTime (0.25 sn) içinde SmoothDamp ile sıfıra döner.
    /// </summary>
    public void ResetCameraOffsets()
    {
        targetSwingPitchOffset = 0f;
        targetSwingRollOffset = 0f;
        targetTransferPitchOffset = 0f;
    }

    public void SetSlideCameraEffects(float heightOffset, float rollOffsetDeg, float fovBoostDeg)
    {
        targetCameraHeightOffset = heightOffset;
        targetSlideRollOffset = rollOffsetDeg;
        targetSlideFovBoost = fovBoostDeg;
    }

    public void SetCameraHeightOffset(float heightOffset)
    {
        targetCameraHeightOffset = heightOffset;
    }

    public void SetMouseInputEnabled(bool enabledInput)
    {
        allowMouseInput = enabledInput;
    }

    public void SetDrowningCameraState(bool active, float targetFov = 75f, float fovSpeed = 4.5f)
    {
        isDrowningCameraActive = active;
        drowningTargetFov = targetFov;
        drowningFovTransitionSpeed = Mathf.Max(0.5f, fovSpeed);
        allowMouseInput = !active;
        if (!active)
        {
            currentDrownPitchSway = 0f;
            currentDrownRollSway = 0f;
            if (targetCamera != null)
            {
                targetCamera.fieldOfView = baseFieldOfView;
            }
        }
    }

    /// <summary>
    /// Geriye dönük uyumluluk için korunmuştur; artık basePitch'e yazmaz, yalnızca transferPitchOffset kullanır.
    /// </summary>
    public void SmoothLookAtWorldPoint(Vector3 targetWorldPoint, float turnSpeed, float dt)
    {
        if (playerBody == null)
            return;

        Vector3 toTarget = targetWorldPoint - transform.position;
        if (toTarget.sqrMagnitude < 0.01f)
            return;

        Vector3 localDir = playerBody.InverseTransformDirection(toTarget.normalized);
        float desiredRelativePitch = -Mathf.Asin(Mathf.Clamp(localDir.y, -1f, 1f)) * Mathf.Rad2Deg - basePitch;
        SetTransferLookOffset(desiredRelativePitch);
    }

    public void UpdateCameraEffects(float effectiveSpeed, bool isGroundedWalking, bool isSprinting, float wallRunTiltTarget, float dt)
    {
        // 1. Hafif Hareket ve Parkur Kamera Sarsıntısını hesapla
        UpdateMovementAndImpactShake(effectiveSpeed, isGroundedWalking, isSprinting, dt);

        // 2. Su altı boğulma yalpalaması (aktifse)
        if (isDrowningCameraActive)
        {
            drowningSwayTimer += dt;
            currentDrownRollSway = Mathf.Sin(drowningSwayTimer * 2.2f) * 4.5f;
            currentDrownPitchSway = Mathf.Cos(drowningSwayTimer * 1.6f) * 3.0f - 4.0f;
        }
        else
        {
            currentDrownRollSway = 0f;
            currentDrownPitchSway = 0f;
        }

        // 3. Ayrı kamera offsetlerini SmoothDamp ile güncelle
        // Dal bırakıldığında (hedef = 0 iken) cameraOffsetResetTime (varsayılan 0.25 sn) içinde pürüzsüzce sıfıra döner!
        float activeSmoothTime = 1f / Mathf.Max(1f, offsetLerpSpeed);
        float resetSmoothTime = Mathf.Max(0.02f, cameraOffsetResetTime);

        float swingPitchTime = Mathf.Abs(targetSwingPitchOffset) < 0.001f ? resetSmoothTime : activeSmoothTime;
        float swingRollTime = Mathf.Abs(targetSwingRollOffset) < 0.001f ? resetSmoothTime : activeSmoothTime;
        float transferPitchTime = Mathf.Abs(targetTransferPitchOffset) < 0.001f ? resetSmoothTime : activeSmoothTime;

        swingPitchOffset = Mathf.SmoothDamp(swingPitchOffset, targetSwingPitchOffset, ref swingPitchSmoothVel, swingPitchTime, Mathf.Infinity, dt);
        swingRollOffset = Mathf.SmoothDamp(swingRollOffset, targetSwingRollOffset, ref swingRollSmoothVel, swingRollTime, Mathf.Infinity, dt);
        transferPitchOffset = Mathf.SmoothDamp(transferPitchOffset, targetTransferPitchOffset, ref transferPitchSmoothVel, transferPitchTime, Mathf.Infinity, dt);
        slideRollOffset = Mathf.SmoothDamp(slideRollOffset, targetSlideRollOffset, ref slideRollSmoothVel, activeSmoothTime, Mathf.Infinity, dt);
        wallRunRollOffset = Mathf.SmoothDamp(wallRunRollOffset, wallRunTiltTarget, ref wallRunRollSmoothVel, activeSmoothTime, Mathf.Infinity, dt);

        // Çok küçük kalıntıları tam sıfıra sabitle
        if (Mathf.Abs(targetSwingPitchOffset) < 0.001f && Mathf.Abs(swingPitchOffset) < 0.01f) swingPitchOffset = 0f;
        if (Mathf.Abs(targetSwingRollOffset) < 0.001f && Mathf.Abs(swingRollOffset) < 0.01f) swingRollOffset = 0f;
        if (Mathf.Abs(targetTransferPitchOffset) < 0.001f && Mathf.Abs(transferPitchOffset) < 0.01f) transferPitchOffset = 0f;

        // 4. Kamera Yükseklik Ofseti (Slide sırasında yumuşakça alçalma ve kalkma)
        currentCameraHeightOffset = Mathf.Lerp(currentCameraHeightOffset, targetCameraHeightOffset, heightOffsetLerpSpeed * dt);

        // 5. Dinamik FOV (90 temel; boğulma sırasında 90 -> 75'e düşer)
        if (targetCamera != null)
        {
            if (isDrowningCameraActive)
            {
                targetCamera.fieldOfView = Mathf.MoveTowards(
                    targetCamera.fieldOfView,
                    drowningTargetFov,
                    drowningFovTransitionSpeed * dt
                );
                return;
            }

            float targetFov = baseFieldOfView;

            if (enableDynamicFov)
            {
                float speedBoost = 0f;
                if (effectiveSpeed > fovMinSpeedThreshold)
                {
                    float speedRatio = Mathf.InverseLerp(fovMinSpeedThreshold, fovMaxSpeedThreshold, effectiveSpeed);
                    speedBoost = Mathf.Min(maxFovBoost, 6f) * speedRatio;
                }

                float totalBoost = Mathf.Min(Mathf.Max(speedBoost, targetSlideFovBoost), 6f);
                targetFov = baseFieldOfView + totalBoost;
            }

            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFov, fovLerpSpeed * dt);
        }
    }

    /// <summary>
    /// Nihai kamera rotasyonu = basePitch (clamp'li) + toplam offsetler (ikinci güvenlik clamp'i ile).
    /// transform.localEulerAngles ile geri okuma ASLA yapılmaz.
    /// </summary>
    private void ApplyCameraRotation()
    {
        float shakeWeight = 1f - thirdPersonBlend * 0.6f;
        Vector3 totalRotShake = (currentShakeRotEuler * shakeWeight) + externalShakeRotEuler;

        // 1. Clamp: Yalnızca basePitch için
        basePitch = Mathf.Clamp(basePitch, minVerticalAngle, maxVerticalAngle);

        // 2. Clamp: Nihai açı (basePitch + toplam offsetler) için ikinci güvenlik clamp'i
        float combinedPitch = basePitch + TotalPitchOffset + totalRotShake.x;
        float finalPitch = Mathf.Clamp(combinedPitch, minVerticalAngle, maxVerticalAngle);

        float finalYaw = totalRotShake.y;
        float finalRoll = TotalRollOffset + totalRotShake.z;

        transform.localRotation = Quaternion.Euler(finalPitch, finalYaw, finalRoll);
    }

    private void UpdateMovementAndImpactShake(float effectiveSpeed, bool isGroundedWalking, bool isSprinting, float dt)
    {
        if (!enableHeadBob)
        {
            currentShakePosOffset = Vector3.Lerp(currentShakePosOffset, Vector3.zero, bobResetSpeed * dt);
            currentShakeRotEuler = Vector3.Lerp(currentShakeRotEuler, Vector3.zero, bobResetSpeed * dt);
            impactTrauma = 0f;
            return;
        }

        if (impactTrauma > 0f)
        {
            impactTrauma = Mathf.Max(0f, impactTrauma - dt * 3.5f);
        }

        perlinSeedTime += dt * perlinShakeFrequency * (isSprinting ? 1.25f : 1.0f);
        float noiseX = (Mathf.PerlinNoise(perlinSeedTime, 13.7f) - 0.5f) * 2f;
        float noiseY = (Mathf.PerlinNoise(29.3f, perlinSeedTime) - 0.5f) * 2f;
        float noiseZ = (Mathf.PerlinNoise(perlinSeedTime * 0.8f, 71.1f) - 0.5f) * 2f;

        bool isWallRunning = playerMotor != null && playerMotor.CurrentState == PlayerMotor.PlayerState.WallRunning;
        bool isHanging = playerMotor != null && playerMotor.CurrentState == PlayerMotor.PlayerState.Hanging;
        bool isMovingActively = (isGroundedWalking || isWallRunning || isHanging) && effectiveSpeed > 0.8f;

        Vector3 targetPosShake = Vector3.zero;
        Vector3 targetRotShake = Vector3.zero;

        if (isMovingActively)
        {
            float speedFactor = Mathf.Clamp(effectiveSpeed / 8.5f, 0.35f, 1.35f);
            float frequency = isSprinting ? sprintBobFrequency : walkBobFrequency;
            if (isWallRunning) frequency *= 1.1f;

            headBobTimer += dt * frequency;

            float bobY = Mathf.Sin(headBobTimer) * bobVerticalAmplitude * speedFactor;
            float bobX = Mathf.Cos(headBobTimer * 0.5f) * bobHorizontalAmplitude * speedFactor;

            targetPosShake = new Vector3(bobX, bobY, 0f);

            float rotMag = movementRotationShakeDegrees * speedFactor;
            float stepPitch = Mathf.Sin(headBobTimer) * rotMag * 0.55f;
            float stepRoll = Mathf.Cos(headBobTimer * 0.5f) * rotMag * 0.70f;

            targetRotShake = new Vector3(
                stepPitch + noiseY * rotMag * 0.35f,
                noiseX * rotMag * 0.25f,
                stepRoll + noiseZ * rotMag * 0.35f
            );
        }
        else
        {
            headBobTimer = 0f;
        }

        if (impactTrauma > 0.001f)
        {
            float traumaSq = impactTrauma * impactTrauma;
            targetPosShake.y -= traumaSq * 0.06f * impactShakeIntensity;
            targetRotShake.x += traumaSq * 1.8f * impactShakeIntensity;
            targetRotShake.z += noiseZ * traumaSq * 1.2f * impactShakeIntensity;
        }

        float smoothRate = isMovingActively ? 18f : bobResetSpeed;
        currentShakePosOffset = Vector3.Lerp(currentShakePosOffset, targetPosShake, smoothRate * dt);
        currentShakeRotEuler = Vector3.Lerp(currentShakeRotEuler, targetRotShake, smoothRate * dt);
    }

    private void UpdateCameraViewPosition(float dt)
    {
        float targetBlend = (currentViewMode == ViewMode.ThirdPerson) ? 1f : 0f;
        float smoothTime = Mathf.Max(0.05f, viewTransitionDuration * 0.5f);
        thirdPersonBlend = Mathf.SmoothDamp(thirdPersonBlend, targetBlend, ref thirdPersonBlendVelocity, smoothTime, Mathf.Infinity, dt);

        if (playerVisual == null && playerBody != null)
        {
            playerVisual = playerBody.GetComponentInChildren<PlayerVisual>();
        }

        Vector3 fpPivotWorld;
        if (attachCameraToEyeMesh && playerVisual != null)
        {
            Vector3 eyeTargetWorld = playerVisual.GetEyeCameraWorldPosition(out bool foundEye);
            if (foundEye)
            {
                fpPivotWorld = eyeTargetWorld + (playerBody != null ? playerBody.up * currentCameraHeightOffset : Vector3.up * currentCameraHeightOffset);
            }
            else
            {
                Vector3 fpPivotLocal = initialPivotLocalPos + new Vector3(0f, currentCameraHeightOffset, 0f);
                fpPivotWorld = playerBody != null ? playerBody.TransformPoint(fpPivotLocal) : transform.position;
            }
        }
        else
        {
            Vector3 fpPivotLocal = initialPivotLocalPos + new Vector3(0f, currentCameraHeightOffset, 0f);
            fpPivotWorld = playerBody != null ? playerBody.TransformPoint(fpPivotLocal) : transform.position;
        }

        Vector3 shakeLocalOffset = (currentShakePosOffset * (1f - thirdPersonBlend * 0.7f)) + externalShakePosOffset;

        float clampedCombinedPitch = Mathf.Clamp(basePitch + TotalPitchOffset, minVerticalAngle, maxVerticalAngle);
        Quaternion yawAndPitchRot = (playerBody != null)
            ? playerBody.rotation * Quaternion.Euler(clampedCombinedPitch, 0f, 0f)
            : transform.rotation;

        Vector3 tpDesiredOffsetWorld =
            (playerBody != null ? playerBody.up : Vector3.up) * thirdPersonOffset.y +
            (yawAndPitchRot * new Vector3(thirdPersonOffset.x, 0f, thirdPersonOffset.z));

        Vector3 tpDesiredWorld = fpPivotWorld + tpDesiredOffsetWorld;

        if (thirdPersonBlend > 0.001f)
        {
            Vector3 castVector = tpDesiredWorld - fpPivotWorld;
            float castDistance = castVector.magnitude;
            if (castDistance > 0.01f)
            {
                Vector3 castDir = castVector / castDistance;
                if (Physics.SphereCast(
                    fpPivotWorld,
                    thirdPersonCollisionRadius,
                    castDir,
                    out RaycastHit hit,
                    castDistance,
                    thirdPersonCollisionMask,
                    QueryTriggerInteraction.Ignore))
                {
                    if (playerBody == null || !hit.transform.IsChildOf(playerBody))
                    {
                        float safeDist = Mathf.Max(0.15f, hit.distance - thirdPersonWallPadding);
                        tpDesiredWorld = fpPivotWorld + castDir * safeDist;
                    }
                }
            }
        }

        Vector3 finalWorldPos = Vector3.Lerp(fpPivotWorld, tpDesiredWorld, thirdPersonBlend);
        Transform camTransform = targetCamera != null ? targetCamera.transform : transform;

        if (camTransform == transform)
        {
            if (transform.parent != null)
            {
                transform.localPosition = transform.parent.InverseTransformPoint(finalWorldPos) + shakeLocalOffset;
            }
            else
            {
                transform.position = finalWorldPos + transform.TransformVector(shakeLocalOffset);
            }
        }
        else
        {
            if (transform.parent != null)
            {
                transform.localPosition = transform.parent.InverseTransformPoint(fpPivotWorld);
            }
            camTransform.position = finalWorldPos + transform.TransformVector(shakeLocalOffset);
        }
    }
}
