using UnityEngine;

/// <summary>
/// CANOPY: The Last Run - Birinci Şahıs Kamera, Mouse Look, Dinamik FOV,
/// Wall Run / Sarkaç / Slide Roll & Pitch Offset, Kamera Yükseklik Ofseti (Slide) ve Head Bob Yöneticisi.
/// Wall Jump ve Wall Run sırasında gecikmesiz (anlık) kamera yönü okuması sağlar.
/// </summary>
public class PlayerLook : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Yatay eksende (Y) döndürülecek oyuncu ana gövdesi.")]
    [SerializeField] private Transform playerBody;

    [Tooltip("Kontrol edilecek birinci şahıs Camera bileşeni.")]
    [SerializeField] private Camera targetCamera;

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

    [Header("Hafif Kamera Sarsıntısı (Head Bob)")]
    [Tooltip("Yürürken ve koşarken hafif kamera salınımını açar/kapatır.")]
    [SerializeField] private bool enableHeadBob = true;

    [Tooltip("Yürürken head bob frekansı.")]
    [SerializeField] private float walkBobFrequency = 11f;

    [Tooltip("Sprint atarken head bob frekansı.")]
    [SerializeField] private float sprintBobFrequency = 15f;

    [Tooltip("Dikey head bob genliği (çok hafif kalması için küçük değer).")]
    [SerializeField] private float bobVerticalAmplitude = 0.035f;

    [Tooltip("Yatay head bob genliği (çok hafif kalması için küçük değer).")]
    [SerializeField] private float bobHorizontalAmplitude = 0.02f;

    [Tooltip("Durduğunda kameranın merkeze dönüş yumuşaklığı.")]
    [SerializeField] private float bobResetSpeed = 10f;

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
    private Vector3 initialPivotLocalPos;
    private Vector3 initialCameraLocalPos;
    private bool allowMouseInput = true;
    private int lastMouseProcessedFrame = -1;

    public Camera TargetCamera => targetCamera;
    public float CurrentRollOffset => currentRollOffset;
    public float CurrentPitchOffset => currentPitchOffset;
    public float CurrentCameraHeightOffset => currentCameraHeightOffset;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = GetComponentInChildren<Camera>();

        if (playerBody == null && transform.parent != null)
            playerBody = transform.parent;

        initialPivotLocalPos = transform.localPosition;

        if (targetCamera != null)
        {
            targetCamera.fieldOfView = baseFieldOfView;
            initialCameraLocalPos = targetCamera.transform.localPosition;
        }
        else
        {
            initialCameraLocalPos = transform.localPosition;
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
        if (allowMouseInput)
        {
            ProcessMouseLookForCurrentFrame();
        }
    }

    /// <summary>
    /// Bu karedeki fare hareketini henüz işlenmemişse anında işler; böylece PlayerMotor veya WallRunHandler
    /// Update sırası ne olursa olsun Space basıldığı karedeki en güncel bakış yönünü sıfır gecikmeyle okur.
    /// </summary>
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

    /// <summary>
    /// O karedeki fare girişini anında uygulayarak kameranın gecikmesiz 3B ileri vektörünü döndürür.
    /// </summary>
    public Vector3 GetInstantCameraForward()
    {
        ProcessMouseLookForCurrentFrame();
        if (targetCamera != null)
            return targetCamera.transform.forward;
        return transform.forward;
    }

    /// <summary>
    /// O karedeki fare girişini anında uygulayarak kameranın yatay düzlemdeki (Y=0) birim bakış yönünü döndürür.
    /// </summary>
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

    /// <summary>
    /// Sarkaç sallanması sırasında kameraya eklenecek geçici pitch ve roll offset hedeflerini belirler.
    /// </summary>
    public void SetSwingCameraOffsets(float pitchOffsetDeg, float rollOffsetDeg)
    {
        targetSwingPitchOffset = pitchOffsetDeg;
        targetSwingRollOffset = rollOffsetDeg;
    }

    /// <summary>
    /// Slide sırasında kameranın dikey yükseklik ofsetini (alçalma miktarı), roll açısını ve FOV artışını belirler.
    /// </summary>
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
        // 1. Roll ve Pitch Offset yumuşak geçişi (Wall Run + Sarkaç + Slide)
        float combinedRollTarget = wallRunTiltTarget + targetSwingRollOffset + targetSlideRollOffset;
        currentRollOffset = Mathf.Lerp(currentRollOffset, combinedRollTarget, offsetLerpSpeed * dt);
        currentPitchOffset = Mathf.Lerp(currentPitchOffset, targetSwingPitchOffset, offsetLerpSpeed * dt);

        ApplyCameraRotation();

        // 2. Kamera Yükseklik Ofseti (Slide sırasında yumuşakça alçalma ve kalkma)
        currentCameraHeightOffset = Mathf.Lerp(currentCameraHeightOffset, targetCameraHeightOffset, heightOffsetLerpSpeed * dt);
        transform.localPosition = initialPivotLocalPos + new Vector3(0f, currentCameraHeightOffset, 0f);

        // 3. Dinamik FOV (90 temel; hız/sallanma ile en fazla +6, slide ile +4 derece)
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

        // 4. Opsiyonel ve çok hafif Head Bob
        UpdateHeadBob(effectiveSpeed, isGroundedWalking, isSprinting, dt);
    }

    private void ApplyCameraRotation()
    {
        float finalPitch = Mathf.Clamp(pitchAngle + currentPitchOffset, minVerticalAngle, maxVerticalAngle);
        transform.localRotation = Quaternion.Euler(finalPitch, 0f, currentRollOffset);
    }

    private void UpdateHeadBob(float horizontalSpeed, bool isGroundedWalking, bool isSprinting, float dt)
    {
        Transform camTransform = targetCamera != null ? targetCamera.transform : transform;

        bool sameTransform = (camTransform == transform);
        Vector3 baseLocalPos = sameTransform
            ? (initialPivotLocalPos + new Vector3(0f, currentCameraHeightOffset, 0f))
            : initialCameraLocalPos;

        if (!enableHeadBob || !isGroundedWalking || horizontalSpeed < 1.5f)
        {
            headBobTimer = 0f;
            camTransform.localPosition = Vector3.Lerp(camTransform.localPosition, baseLocalPos, bobResetSpeed * dt);
            return;
        }

        float frequency = isSprinting ? sprintBobFrequency : walkBobFrequency;
        headBobTimer += dt * frequency;

        float bobOffsetY = Mathf.Sin(headBobTimer) * bobVerticalAmplitude;
        float bobOffsetX = Mathf.Cos(headBobTimer * 0.5f) * bobHorizontalAmplitude;

        Vector3 targetPos = baseLocalPos + new Vector3(bobOffsetX, bobOffsetY, 0f);
        camTransform.localPosition = Vector3.Lerp(camTransform.localPosition, targetPos, bobResetSpeed * dt);
    }
}
