using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// CANOPY: The Last Run - Kayma (Slide - C Tuşu) ve Alçak Tavan Kontrolü Yöneticisi.
/// (Sprint >= 0.3 sn VEYA Yatay Hız >= 6 m/s) sağlandığında C tuşuyla (veya 0.25 sn hava tamponuyla)
/// kayma başlatır. C tuşu bırakılsa bile minSlideDuration (0.35 sn) boyunca kaymaya devam eder.
/// </summary>
public class SlideHandler : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Merkezi karakter hareket ve state machine yöneticisi.")]
    [SerializeField] private PlayerMotor motor;

    [Tooltip("Kamera yükseklik ofseti, FOV ve roll efektlerini yöneten bileşen.")]
    [SerializeField] private PlayerLook playerLook;

    [Header("Başlatma Koşulları ve Zamanlayıcılar")]
    [Tooltip("Kayma (Slide) başlatma tuşu.")]
    [SerializeField] private KeyCode slideKey = KeyCode.C;

    [Tooltip("Hız şartı sağlanmadığında alternatif olarak gereken minimum sprint süresi (varsayılan 0.3 sn).")]
    [SerializeField] private float minSprintTimeForSlide = 0.3f;

    [Tooltip("Sprint şartı aranmadan slide başlatabilmek için gereken yatay hız eşiği (varsayılan 6 m/s).")]
    [SerializeField] private float minSlideSpeed = 6.0f;

    [Tooltip("Havada C'ye basıldığında yere inince slide'ın otomatik başlaması için tampon süre (varsayılan 0.25 sn).")]
    [SerializeField] private float slideBufferTime = 0.25f;

    [Tooltip("Slide BİTTİKTEN sonra tekrar slide yapabilmek için bekleme süresi (varsayılan 1.0 sn).")]
    [SerializeField] private float slideCooldown = 1.0f;

    [Tooltip("C tuşu erken bırakılsa bile slide'ın zorunlu devam edeceği minimum süre (varsayılan 0.35 sn).")]
    [SerializeField] private float minSlideDuration = 0.35f;

    [Tooltip("C basılı tutulduğunda maksimum kesintisiz slide süresi (varsayılan 1.4 sn).")]
    [SerializeField] private float maxSlideDuration = 1.4f;

    [Header("Slide Hız, Boost, Sürtünme ve Eğim Fiziği")]
    [Tooltip("Düşük hızda (6 - 9 m/s arası) başlayan slide için güçlü hız patlaması çarpanı (varsayılan 1.4x).")]
    [SerializeField] private float lowSpeedBoostMultiplier = 1.40f;

    [Tooltip("Yüksek hızda (9 m/s ve üstü) başlayan slide için standart hız patlaması çarpanı (varsayılan 1.25x).")]
    [SerializeField] private float slideBoostMultiplier = 1.25f;

    [Tooltip("Düşük hız boost çarpanından normal boost çarpanına geçiş hız eşiği (varsayılan 9 m/s).")]
    [SerializeField] private float highSpeedBoostThreshold = 9.0f;

    [Tooltip("Slide sırasında ulaşılabilecek maksimum hız sınırı (m/s).")]
    [SerializeField] private float maxSlideSpeedCap = 19f;

    [Tooltip("Düz zeminde kayarken hızın saniyedeki azalma miktarı (sürtünme, m/s²).")]
    [SerializeField] private float slideFriction = 6.5f;

    [Tooltip("Yokuş aşağı kayarken eğim dikliğine göre eklenecek ivmelenme gücü (m/s²).")]
    [SerializeField] private float slopeAcceleration = 18f;

    [Tooltip("Yokuş yukarı kayarken uygulanacak ekstra yavaşlama çarpanı.")]
    [SerializeField] private float uphillExtraFrictionMultiplier = 2.2f;

    [Tooltip("Slide sırasında A/D tuşlarıyla yönü hafifçe saptırma gücü (düşük tutulmalıdır).")]
    [SerializeField] private float slideSteerStrength = 1.6f;

    [Tooltip("Hız bu değerin altına düştüğünde slide otomatik sona erer (varsayılan 3.5 m/s).")]
    [SerializeField] private float minSlideEndSpeed = 3.5f;

    [Tooltip("Üstte engel varken (tavan altında sıkışmışken) alçak yürüme hızı (m/s).")]
    [SerializeField] private float crouchedBlockedMoveSpeed = 4.5f;

    [Header("Yükseklik ve Kamera Değişimi")]
    [Tooltip("Normal ayakta duruş CharacterController yüksekliği (varsayılan 2.0 m).")]
    [SerializeField] private float standingHeight = 2.0f;

    [Tooltip("Slide sırasındaki alçaltılmış CharacterController yüksekliği (varsayılan 1.0 m).")]
    [SerializeField] private float slidingHeight = 1.0f;

    [Tooltip("Başlangıçta CharacterController üzerinde tanımlı yüksekliği standingHeight olarak otomatik al.")]
    [SerializeField] private bool useInitialControllerHeightIfDifferent = true;

    [Tooltip("CharacterController yüksekliğinin küçülme/büyüme yumuşatma hızı.")]
    [SerializeField] private float heightTransitionSpeed = 14f;

    [Tooltip("Slide sırasında kameranın dikey alçalma ofseti (metre, negatif değer).")]
    [SerializeField] private float slideCameraHeightOffset = -0.55f;

    [Tooltip("Slide sırasında kameraya uygulanacak hafif yatış (roll) açısı (derece).")]
    [SerializeField] private float slideCameraRollAngle = 2.5f;

    [Tooltip("Slide sırasında kameraya eklenecek FOV artışı (varsayılan +4 derece).")]
    [Range(0f, 6f)]
    [SerializeField] private float slideFovBoost = 4f;

    [Header("Tavan / Engel Kontrolü")]
    [Tooltip("Ayağa kalkarken baş üstü engel kontrolü yapılacak katmanlar.")]
    [SerializeField] private LayerMask ceilingObstructionMask = ~0;

    [Header("Olaylar (Ses ve Efekt Bağlantısı İçin)")]
    [Tooltip("Slide başladığında tetiklenen UnityEvent.")]
    [SerializeField] private UnityEvent onSlideStart;

    [Tooltip("Slide sona erdiğinde tetiklenen UnityEvent.")]
    [SerializeField] private UnityEvent onSlideEnd;

    public event Action OnSlideStartCallback;
    public event Action OnSlideEndCallback;

    // Dahili çalışma değişkenleri
    private float sprintDurationTimer;
    private float slideBufferTimer;
    private float slideCooldownTimer;
    private float activeSlideTimer;

    private Vector3 slideDirection;
    private float currentSlideSpeed;
    private bool isCeilingBlocked;
    private string currentBlockReason = "none";

    private float baseStandingHeight;
    private Vector3 baseStandingCenter;
    private float baseCapsuleBottomY;
    private float currentControllerHeight;

    // Debug HUD ve dış erişim özellikleri
    public float SprintDurationTimer => sprintDurationTimer;
    public float MinSprintTimeRequired => minSprintTimeForSlide;
    public float SlideCooldownRemaining => Mathf.Max(0f, slideCooldownTimer);
    public float RemainingSlideTime => motor != null && motor.CurrentState == PlayerMotor.PlayerState.Sliding
        ? Mathf.Max(0f, maxSlideDuration - activeSlideTimer)
        : 0f;
    public bool IsSliding => motor != null && motor.CurrentState == PlayerMotor.PlayerState.Sliding;
    public bool IsCeilingBlocked => isCeilingBlocked;
    public float CurrentSlideSpeed => currentSlideSpeed;
    public string CurrentSlideBlockReason => currentBlockReason;

    private void Awake()
    {
        if (motor == null)
            motor = GetComponent<PlayerMotor>();

        if (playerLook == null)
            playerLook = GetComponentInChildren<PlayerLook>();

        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            baseStandingHeight = (useInitialControllerHeightIfDifferent && cc.height > 0.5f)
                ? cc.height
                : standingHeight;
            baseStandingCenter = cc.center;
            baseCapsuleBottomY = baseStandingCenter.y - (baseStandingHeight * 0.5f);
            currentControllerHeight = baseStandingHeight;
        }
        else
        {
            baseStandingHeight = standingHeight;
            currentControllerHeight = standingHeight;
        }
    }

    /// <summary>
    /// PlayerMotor.Update tarafından her karede çağrılır; sprint süresini, C tuşu buffer'ını (0.25 sn),
    /// cooldown'u, HUD engel nedenini ve CharacterController yükseklik geri dönüşünü günceller.
    /// </summary>
    public void TickTimersAndInput(float dt)
    {
        if (motor == null)
            return;

        // 1. Cooldown sayacı (yalnızca slide bittikten sonra işler)
        if (motor.CurrentState != PlayerMotor.PlayerState.Sliding && slideCooldownTimer > 0f)
        {
            slideCooldownTimer -= dt;
        }

        // 2. Kesintisiz Sprint Süresi Takibi
        bool isActivelySprintingOnGround = (motor.CurrentState == PlayerMotor.PlayerState.Grounded) &&
                                           motor.IsSprinting &&
                                           motor.HorizontalSpeed >= (motor.WalkSpeed * 0.8f);

        bool isMaintainingSprintInAir = (motor.CurrentState == PlayerMotor.PlayerState.Airborne) &&
                                        motor.IsSprinting &&
                                        motor.HorizontalSpeed >= minSlideSpeed;

        if (isActivelySprintingOnGround)
        {
            sprintDurationTimer += dt;
        }
        else if (!isMaintainingSprintInAir && motor.CurrentState != PlayerMotor.PlayerState.Sliding)
        {
            sprintDurationTimer = 0f;
        }

        // 3. C tuşu Slide Buffer (0.25 sn)
        if (Input.GetKeyDown(slideKey))
        {
            slideBufferTimer = slideBufferTime;
        }
        else if (slideBufferTimer > 0f)
        {
            slideBufferTimer -= dt;
        }

        // 4. HUD için anlık Slide başlatma engel nedenini güncelle
        EvaluateSlideAvailability(out currentBlockReason);

        // 5. Slide durumunda değilsek CharacterController yüksekliğini yumuşakça normal boya geri getir
        if (motor.CurrentState != PlayerMotor.PlayerState.Sliding)
        {
            UpdateControllerDimensions(baseStandingHeight, dt);
        }
    }

    /// <summary>
    /// Slide başlatma koşullarını değerlendirir ve sağlanmıyorsa HUD için nedenini ("cooldown / speed / state / not grounded") döndürür.
    /// Kural: (sprint süresi >= minSprintTimeForSlide) VEYA (yatay hız >= minSlideSpeed) yeterlidir.
    /// </summary>
    private bool EvaluateSlideAvailability(out string blockReason)
    {
        if (motor == null)
        {
            blockReason = "state";
            return false;
        }

        if (motor.CurrentState == PlayerMotor.PlayerState.Sliding)
        {
            blockReason = "active";
            return false;
        }

        if (motor.CurrentState == PlayerMotor.PlayerState.WallRunning ||
            motor.CurrentState == PlayerMotor.PlayerState.Hanging ||
            motor.CurrentState == PlayerMotor.PlayerState.Transferring)
        {
            blockReason = "state";
            return false;
        }

        if (!motor.IsGrounded || motor.CurrentState != PlayerMotor.PlayerState.Grounded)
        {
            blockReason = "not grounded";
            return false;
        }

        if (slideCooldownTimer > 0f)
        {
            blockReason = "cooldown";
            return false;
        }

        bool hasEnoughSpeed = motor.HorizontalSpeed >= minSlideSpeed;
        bool hasEnoughSprintTime = (sprintDurationTimer >= minSprintTimeForSlide) && (motor.HorizontalSpeed > 1.5f);

        if (!hasEnoughSpeed && !hasEnoughSprintTime)
        {
            blockReason = "speed";
            return false;
        }

        blockReason = "ready";
        return true;
    }

    /// <summary>
    /// C tuşuna basılmışsa (veya 0.25 sn buffer aktifse) ve koşullar uygunsa Slide durumunu başlatır.
    /// </summary>
    public bool TryStartSlide()
    {
        if (slideBufferTimer <= 0f)
            return false;

        if (!EvaluateSlideAvailability(out currentBlockReason))
            return false;

        StartSlide();
        return true;
    }

    /// <summary>
    /// Slide durumunu başlatır; düşük hızda (6-9 m/s) 1.4x, yüksek hızda (>=9 m/s) 1.25x hız patlaması uygular.
    /// </summary>
    private void StartSlide()
    {
        slideBufferTimer = 0f;
        activeSlideTimer = 0f;
        isCeilingBlocked = false;
        currentBlockReason = "active";

        Vector3 horizVel = motor.HorizontalVelocity;
        slideDirection = horizVel.sqrMagnitude > 0.01f ? horizVel.normalized : transform.forward;

        float entryHorizSpeed = Mathf.Max(motor.HorizontalSpeed, minSlideSpeed);
        float chosenBoost = (entryHorizSpeed < highSpeedBoostThreshold)
            ? lowSpeedBoostMultiplier
            : slideBoostMultiplier;

        currentSlideSpeed = Mathf.Min(entryHorizSpeed * chosenBoost, maxSlideSpeedCap);

        Vector3 initialSlideVelocity = slideDirection * currentSlideSpeed + Vector3.up * motor.Velocity.y;
        motor.SetVelocity(initialSlideVelocity);
        motor.SetState(PlayerMotor.PlayerState.Sliding);

        if (playerLook != null)
        {
            playerLook.SetSlideCameraEffects(slideCameraHeightOffset, slideCameraRollAngle, slideFovBoost);
        }

        onSlideStart?.Invoke();
        OnSlideStartCallback?.Invoke();
    }

    /// <summary>
    /// Sliding durumundayken her karede eğim fiziğini, minSlideDuration (0.35 sn) garantisini ve bitiş koşullarını işletir.
    /// </summary>
    public void UpdateSlidingState(float dt)
    {
        activeSlideTimer += dt;

        // 1. Kapsül yüksekliğini ayak tabanı sabit kalacak şekilde yumuşakça küçült
        float targetRatio = slidingHeight / Mathf.Max(0.5f, standingHeight);
        float targetCapsuleHeight = baseStandingHeight * targetRatio;
        UpdateControllerDimensions(targetCapsuleHeight, dt);

        // 2. Zemin bittiyse hız korunarak Airborne'a geç
        if (!motor.IsGrounded)
        {
            ExitSlideToState(PlayerMotor.PlayerState.Airborne, preserveHorizontalSpeed: true);
            return;
        }

        // 3. Slide sırasında Space basıldıysa: Slide-Jump!
        if (motor.HasBufferedJump)
        {
            isCeilingBlocked = CheckCeilingObstacle();
            if (!isCeilingBlocked)
            {
                ExecuteSlideJump();
                return;
            }
        }

        // 4. Bitiş koşulları:
        // C tuşu bırakılmış olsa bile minSlideDuration (0.35 sn) dolana kadar devam eder!
        bool minDurationReached = activeSlideTimer >= minSlideDuration;
        bool releasedSlideKeyAfterMin = minDurationReached && !Input.GetKey(slideKey);
        bool maxDurationExpired = activeSlideTimer >= maxSlideDuration;
        bool tooSlow = minDurationReached && (currentSlideSpeed < minSlideEndSpeed);

        if (releasedSlideKeyAfterMin || maxDurationExpired || tooSlow)
        {
            isCeilingBlocked = CheckCeilingObstacle();
            if (!isCeilingBlocked)
            {
                ExitSlideToState(PlayerMotor.PlayerState.Grounded, preserveHorizontalSpeed: true);
                return;
            }
        }
        else
        {
            isCeilingBlocked = false;
        }

        // 5. Hafif A/D ile kayma yönünü saptırma
        float steerInput = motor.HorizontalInput;
        if (Mathf.Abs(steerInput) > 0.01f)
        {
            slideDirection = Vector3.RotateTowards(
                slideDirection,
                transform.right * Mathf.Sign(steerInput),
                slideSteerStrength * dt,
                0f
            );
            slideDirection.y = 0f;
            if (slideDirection.sqrMagnitude > 0.001f)
            {
                slideDirection.Normalize();
            }
        }

        // 6. Eğim (Slope) ve Sürtünme (Friction) Hesabı
        Vector3 groundNormal = motor.GroundNormal;
        Vector3 slopeAlignedDir = Vector3.ProjectOnPlane(slideDirection, groundNormal).normalized;

        float slopeVerticalComponent = -slopeAlignedDir.y;
        float netAcceleration = -slideFriction;

        if (slopeVerticalComponent > 0.02f)
        {
            netAcceleration += slopeVerticalComponent * slopeAcceleration;
        }
        else if (slopeVerticalComponent < -0.02f)
        {
            netAcceleration -= Mathf.Abs(slopeVerticalComponent) * slideFriction * uphillExtraFrictionMultiplier;
        }

        currentSlideSpeed += netAcceleration * dt;

        float minFloorSpeed = isCeilingBlocked ? crouchedBlockedMoveSpeed : 0f;
        currentSlideSpeed = Mathf.Clamp(currentSlideSpeed, minFloorSpeed, maxSlideSpeedCap);

        Vector3 moveVelocity = slopeAlignedDir * currentSlideSpeed;
        if (moveVelocity.y > 0f && slopeVerticalComponent >= 0f)
        {
            moveVelocity.y = -2f;
        }
        else if (moveVelocity.y <= 0f)
        {
            moveVelocity.y -= 4f;
        }

        motor.SetVelocity(new Vector3(moveVelocity.x, -4f, moveVelocity.z));
        if (motor.Controller.enabled)
        {
            motor.Controller.Move(moveVelocity * dt);
        }
    }

    private void ExecuteSlideJump()
    {
        Vector3 jumpHorizVel = slideDirection * currentSlideSpeed;
        // Cooldown slide bitiş anından itibaren başlar (1.0 sn)
        slideCooldownTimer = slideCooldown;
        sprintDurationTimer = 0f;
        isCeilingBlocked = false;

        if (playerLook != null)
        {
            playerLook.SetSlideCameraEffects(0f, 0f, 0f);
        }

        onSlideEnd?.Invoke();
        OnSlideEndCallback?.Invoke();

        motor.ExecuteJumpWithCustomHorizontalVelocity(jumpHorizVel);
    }

    private void ExitSlideToState(PlayerMotor.PlayerState nextState, bool preserveHorizontalSpeed)
    {
        // Cooldown slide bitiş anından itibaren başlar (1.0 sn)
        slideCooldownTimer = slideCooldown;
        sprintDurationTimer = 0f;
        isCeilingBlocked = false;

        if (playerLook != null)
        {
            playerLook.SetSlideCameraEffects(0f, 0f, 0f);
        }

        if (preserveHorizontalSpeed)
        {
            Vector3 exitHoriz = slideDirection * currentSlideSpeed;
            motor.SetVelocity(new Vector3(exitHoriz.x, motor.Velocity.y, exitHoriz.z));
        }

        onSlideEnd?.Invoke();
        OnSlideEndCallback?.Invoke();

        motor.SetState(nextState);
    }

    public void ForceCancelSlide()
    {
        slideCooldownTimer = slideCooldown;
        isCeilingBlocked = false;
        if (playerLook != null)
        {
            playerLook.SetSlideCameraEffects(0f, 0f, 0f);
        }
    }

    private bool CheckCeilingObstacle()
    {
        if (motor == null || motor.Controller == null)
            return false;

        CharacterController cc = motor.Controller;
        float checkRadius = cc.radius * 0.85f;

        Vector3 footWorldPos = transform.position + new Vector3(baseStandingCenter.x, baseCapsuleBottomY, baseStandingCenter.z);
        Vector3 castOrigin = footWorldPos + Vector3.up * (checkRadius + 0.1f);
        float castDistance = Mathf.Max(0.1f, baseStandingHeight - (checkRadius * 2f) - 0.1f);

        if (Physics.SphereCast(
                castOrigin,
                checkRadius,
                Vector3.up,
                out RaycastHit hit,
                castDistance,
                ceilingObstructionMask,
                QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform != transform)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// CharacterController yüksekliğini küçültürken/büyütürken ayak tabanını (baseCapsuleBottomY) sabit tutar;
    /// böylece düşük hızda bile yere gömülme veya havada kalma yaşanmaz.
    /// </summary>
    private void UpdateControllerDimensions(float targetHeight, float dt)
    {
        if (motor == null || motor.Controller == null || !motor.Controller.enabled)
            return;

        CharacterController cc = motor.Controller;
        if (Mathf.Abs(currentControllerHeight - targetHeight) < 0.005f)
        {
            currentControllerHeight = targetHeight;
        }
        else
        {
            currentControllerHeight = Mathf.Lerp(currentControllerHeight, targetHeight, heightTransitionSpeed * dt);
        }

        cc.height = currentControllerHeight;
        float newCenterY = baseCapsuleBottomY + (currentControllerHeight * 0.5f);
        cc.center = new Vector3(baseStandingCenter.x, newCenterY, baseStandingCenter.z);
    }
}
