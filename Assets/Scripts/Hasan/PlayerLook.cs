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
/// Hafif Gerçekçi Kamera Sarsıntısı (Camera Shake), Mouse Look ve Dinamik FOV Yöneticisi.
/// Dünya uzayında (World Space) Lerp gecikmesi KULLANMAZ; böylece yürürken/koşarken kamera asla geride kalmaz
/// ve maymunun içine girmez.
/// </summary>
public class PlayerLook : MonoBehaviour
{
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

    [Tooltip("V tuşu ile FirstPerson / ThirdPerson geçişine izin ver (debug amaçlı kapatılabilir).")]
    [SerializeField] private bool allowViewToggle = true;

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

    [Header("Sarkaç, Wall Run ve Slide Kamera Offset Ayarları")]
    [Tooltip("Kamera yatış (roll/tilt) ve sarkaç pitch offset açılarının yumuşak geçiş hızı.")]
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

    // Dahili değişkenler
    private float pitchAngle;
    private float currentRollOffset;
    private float currentPitchOffset;
    private float targetSwingPitchOffset;
    private float targetSwingRollOffset;
    private float targetSlideRollOffset;
    private float targetSlideFovBoost;
    private float targetCameraHeightOffset;
    private float currentCameraHeightOffset;
    private float headBobTimer;
    private float perlinSeedTime;
    private float impactTrauma;
    private Vector3 currentShakePosOffset;
    private Vector3 currentShakeRotEuler;
    private Vector3 initialPivotLocalPos;
    private float thirdPersonBlend;
    private float thirdPersonBlendVelocity;
    private bool allowMouseInput = true;
    private int lastMouseProcessedFrame = -1;

    // Dış erişim özellikleri
    public Camera TargetCamera => targetCamera;
    public ViewMode CurrentViewMode => currentViewMode;
    public bool AllowViewToggle => allowViewToggle;
    public float CurrentRollOffset => currentRollOffset;
    public float CurrentPitchOffset => currentPitchOffset;
    public float CurrentCameraHeightOffset => currentCameraHeightOffset;

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

        // Sahnedeki eski yüksek shake değerleri serialize edilmişse kameranın sabit kalması için makul sınırlara çek
        if (bobVerticalAmplitude > 0.02f) bobVerticalAmplitude = 0.012f;
        if (bobHorizontalAmplitude > 0.015f) bobHorizontalAmplitude = 0.008f;
        if (movementRotationShakeDegrees > 0.55f) movementRotationShakeDegrees = 0.35f;
        if (impactShakeIntensity > 0.8f) impactShakeIntensity = 0.55f;

        if (targetCamera != null)
        {
            targetCamera.fieldOfView = baseFieldOfView;
            // Yakın kırpma düzlemini küçült ki gövdeye yakınken kesilme olmasın
            targetCamera.nearClipPlane = 0.03f;
        }

        thirdPersonBlend = (currentViewMode == ViewMode.ThirdPerson) ? 1f : 0f;
        ApplyViewModeState(currentViewMode);
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
        if (allowViewToggle && Input.GetKeyDown(viewToggleKey))
        {
            ToggleViewMode();
        }

        if (allowMouseInput)
        {
            ProcessMouseLookForCurrentFrame();
        }
    }

    private void LateUpdate()
    {
        // Animator kemikleri güncelledikten sonra önce PlayerVisual'ı senkronize et,
        // ardından kamerayı gecikmesiz (lag olmadan) yerleştir.
        float dt = Time.deltaTime;
        if (playerVisual != null)
        {
            playerVisual.SyncVisualForCurrentFrame(dt);
        }

        UpdateCameraViewPosition(dt);
        ApplyCameraRotation();
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

    /// <summary>
    /// Zıplama, yere iniş veya dala tutunma anında kameraya hafif darbe sarsıntısı ekler.
    /// </summary>
    public void AddCameraShakeImpulse(float normalizedImpulse)
    {
        impactTrauma = Mathf.Clamp01(impactTrauma + normalizedImpulse);
    }

    public void ToggleViewMode()
    {
        ViewMode nextMode = (currentViewMode == ViewMode.FirstPerson)
            ? ViewMode.ThirdPerson
            : ViewMode.FirstPerson;

        SetViewMode(nextMode);
    }

    public void SetViewMode(ViewMode newMode)
    {
        if (currentViewMode == newMode)
            return;

        currentViewMode = newMode;
        ApplyViewModeState(currentViewMode);
        OnViewModeChanged?.Invoke(currentViewMode);
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

    public void ProcessMouseLookForCurrentFrame()
    {
        if (!allowMouseInput || lastMouseProcessedFrame == Time.frameCount)
            return;

        lastMouseProcessedFrame = Time.frameCount;

        float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        pitchAngle -= mouseY;
        pitchAngle = Mathf.Clamp(pitchAngle, minVerticalAngle, maxVerticalAngle);

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
        Vector3 camFwd = GetInstantCameraForward();
        Vector3 flatFwd = new Vector3(camFwd.x, 0f, camFwd.z);
        if (flatFwd.sqrMagnitude > 0.0001f)
        {
            return flatFwd.normalized;
        }

        if (playerBody != null)
        {
            Vector3 bodyFlat = new Vector3(playerBody.forward.x, 0f, playerBody.forward.z);
            if (bodyFlat.sqrMagnitude > 0.0001f)
                return bodyFlat.normalized;
        }

        return Vector3.forward;
    }

    public void SetSwingCameraOffsets(float pitchOffsetDeg, float rollOffsetDeg)
    {
        targetSwingPitchOffset = pitchOffsetDeg;
        targetSwingRollOffset = rollOffsetDeg;
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

    public void SmoothLookAtWorldPoint(Vector3 targetWorldPoint, float turnSpeed, float dt)
    {
        if (playerBody == null)
            return;

        Vector3 toTarget = targetWorldPoint - transform.position;
        if (toTarget.sqrMagnitude < 0.01f)
            return;

        Vector3 flatDir = new Vector3(toTarget.x, 0f, toTarget.z);
        if (flatDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetBodyRot = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
            playerBody.rotation = Quaternion.Slerp(playerBody.rotation, targetBodyRot, turnSpeed * dt);
        }

        Vector3 localDir = playerBody.InverseTransformDirection(toTarget.normalized);
        float desiredPitch = -Mathf.Asin(Mathf.Clamp(localDir.y, -1f, 1f)) * Mathf.Rad2Deg;
        desiredPitch = Mathf.Clamp(desiredPitch, minVerticalAngle, maxVerticalAngle);
        pitchAngle = Mathf.Lerp(pitchAngle, desiredPitch, turnSpeed * dt);

        ApplyCameraRotation();
    }

    public void UpdateCameraEffects(float effectiveSpeed, bool isGroundedWalking, bool isSprinting, float wallRunTiltTarget, float dt)
    {
        // 1. Hafif Hareket ve Parkur Kamera Sarsıntısını hesapla
        UpdateMovementAndImpactShake(effectiveSpeed, isGroundedWalking, isSprinting, dt);

        // 2. Roll ve Pitch Offset yumuşak geçişi (Wall Run + Sarkaç + Slide)
        float combinedRollTarget = wallRunTiltTarget + targetSwingRollOffset + targetSlideRollOffset;
        currentRollOffset = Mathf.Lerp(currentRollOffset, combinedRollTarget, offsetLerpSpeed * dt);
        currentPitchOffset = Mathf.Lerp(currentPitchOffset, targetSwingPitchOffset, offsetLerpSpeed * dt);

        // 3. Kamera Yükseklik Ofseti (Slide sırasında yumuşakça alçalma ve kalkma)
        currentCameraHeightOffset = Mathf.Lerp(currentCameraHeightOffset, targetCameraHeightOffset, heightOffsetLerpSpeed * dt);

        // 4. Dinamik FOV (90 temel; hız/sallanma ile en fazla +6, slide ile +4 derece)
        if (targetCamera != null)
        {
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

    private void ApplyCameraRotation()
    {
        float shakeWeight = 1f - thirdPersonBlend * 0.6f;
        float finalPitch = Mathf.Clamp(
            pitchAngle + currentPitchOffset + (currentShakeRotEuler.x * shakeWeight),
            minVerticalAngle,
            maxVerticalAngle
        );
        float finalYaw = currentShakeRotEuler.y * shakeWeight;
        float finalRoll = currentRollOffset + (currentShakeRotEuler.z * shakeWeight);

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

            targetPosShake = new Vector3(
                bobX,
                bobY,
                0f // İleri-geri (Z) pozisyon sarsıntısı yapılmaz ki kamera asla geriye düşmesin
            );

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

        // FirstPerson kamera konumunu Player yerel uzayında (Local Space) TAM SABİT olarak al (Dünya uzayında Lerp gecikmesi YOK!)
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

        Vector3 shakeLocalOffset = currentShakePosOffset * (1f - thirdPersonBlend * 0.7f);

        // ThirdPerson hedef noktası: oyuncunun arkasında ve yukarıda (thirdPersonOffset: varsayılan 0, 1.2, -3.2)
        Quaternion yawAndPitchRot = (playerBody != null)
            ? playerBody.rotation * Quaternion.Euler(pitchAngle + currentPitchOffset, 0f, 0f)
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
