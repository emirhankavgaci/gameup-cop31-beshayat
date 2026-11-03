using System;
using UnityEngine;

/// <summary>
/// CANOPY: The Last Run - Ana Karakter Hareket ve State Machine Yöneticisi.
/// Durumlar: Grounded, Airborne, WallRunning, Hanging, Transferring, Sliding, Drowning.
///
/// Space Girişi ve Wall Jump Önceliği (Bug Fix 1):
/// - Space girişi Update'in EN BAŞINDA tek bir yerde Input.GetKeyDown(jumpKey) ile okunur ve
///   jumpPressedThisFrame olarak saklanır.
/// - Wall jump kontrolü (WallRunning, Wall Coyote Time ve allowAirWallJump) normal zıplama,
///   normal coyote ve normal buffer kontrollerinden ÖNCE yapılır.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    public enum PlayerState
    {
        Grounded = 0,
        Airborne = 1,
        WallRunning = 2,
        Hanging = 3,
        Transferring = 4,
        Sliding = 5,
        Drowning = 6
    }

    [Header("Bileşen Referansları")]
    [Tooltip("Karakterin bakış, ViewMode, FOV ve kamera roll/pitch/height offset işlemlerini yöneten bileşen.")]
    [SerializeField] private PlayerLook playerLook;

    [Tooltip("Duvar koşusu (Wall Run) algılama ve fizik işlemlerini yöneten bileşen.")]
    [SerializeField] private WallRunHandler wallRunHandler;

    [Tooltip("Otomatik dal tutunma, sarkaç sallanması ve dallar arası geçiş yöneticisi.")]
    [SerializeField] private BranchGrabHandler branchGrabHandler;

    [Tooltip("C tuşu ile kayma (Slide) ve tavan engel kontrolü yöneticisi.")]
    [SerializeField] private SlideHandler slideHandler;

    [Tooltip("Maymun 3B modelinin görsel hizalama ve gölge modunu yöneten bileşen (opsiyonel).")]
    [SerializeField] private PlayerVisual playerVisual;

    [Header("Temel Hareket Hızları")]
    [Tooltip("Normal yürüme hızı (m/s).")]
    [SerializeField] private float walkSpeed = 7f;

    [Tooltip("Shift ile depar (sprint) hızı (m/s).")]
    [SerializeField] private float sprintSpeed = 12f;

    [Tooltip("Zeminde hedef hıza ulaşma ivmesi. Yüksek değer = keskin başlangıç.")]
    [SerializeField] private float groundAcceleration = 65f;

    [Tooltip("Zeminde tuş bırakıldığında durma ivmesi. Yüksek değer = buz gibi kaymayı önler.")]
    [SerializeField] private float groundDeceleration = 55f;

    [Header("Hava Kontrolü ve Momentum")]
    [Tooltip("Havadaki yön değiştirme ivmesi (zeminden düşük, ancak yüksek kontrol sağlar).")]
    [SerializeField] private float airAcceleration = 28f;

    [Tooltip("Havada hareket tuşları bırakıldığında yatay hızın azalma direnci (wall jump ve sarkaç momentumu için düşük tutun).")]
    [SerializeField] private float airDrag = 1.8f;

    [Tooltip("Havada korunabilecek maksimum yatay momentum hızı sınırı (wall jump, sarkaç fırlaması ve slide-jump dahil).")]
    [SerializeField] private float maxAirHorizontalSpeed = 24f;

    [Header("Zıplama ve Yerçekimi")]
    [Tooltip("Tam basışta ulaşılan zıplama yüksekliği (metre).")]
    [SerializeField] private float jumpHeight = 2.2f;

    [Tooltip("Temel yerçekimi ivmesi (m/s²). Negatif olmalıdır.")]
    [SerializeField] private float gravity = -30f;

    [Tooltip("Düşüş sırasında yerçekimi çarpanı (çevik zıplama hissi için).")]
    [SerializeField] private float fastFallMultiplier = 1.65f;

    [Tooltip("Zıplama tuşu erken bırakıldığında dikey hızın kesilme çarpanı (Variable Jump Height).")]
    [SerializeField] private float earlyJumpReleaseMultiplier = 0.45f;

    [Tooltip("Maksimum düşüş hızı sınırı (Terminal Velocity, pozitif değer olarak girilir).")]
    [SerializeField] private float maxFallSpeed = 35f;

    [Tooltip("Zeminden ayrıldıktan sonra hâlâ zıplayabilme süresi (saniye).")]
    [SerializeField] private float coyoteTimeDuration = 0.12f;

    [Tooltip("Yere değmeden hemen önce basılan zıplama tuşunu hatırlama süresi (saniye).")]
    [SerializeField] private float jumpBufferDuration = 0.12f;

    [Header("Zemin ve Eğim Kontrolü")]
    [Tooltip("Zemin olarak kabul edilecek katmanlar (Ground vb.).")]
    [SerializeField] private LayerMask groundMask = ~0;

    [Tooltip("Zemin kontrol küresinin (SphereCast) karakter tabanına göre dikey ofseti.")]
    [SerializeField] private float groundCheckOffset = 0.1f;

    [Tooltip("Zemin kontrol küresinin yarıçap çarpanı (CharacterController radius ile çarpılır).")]
    [SerializeField] private float groundCheckRadiusScale = 0.9f;

    [Tooltip("Zemin küresi algılama mesafesi.")]
    [SerializeField] private float groundCheckDistance = 0.22f;

    [Tooltip("Eğimli yüzeylerde aşağı inerken zıplamayı/sekme yapmayı önleyen yapıştırma kuvveti.")]
    [SerializeField] private float groundedStickForce = -5f;

    [Header("Kontrol Tuşları (Eski Input Manager)")]
    [Tooltip("Sprint (Depar) tuşu.")]
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;

    [Tooltip("Zıplama tuşu.")]
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;

    [Header("Boğulma (Drowning) Fiziği Ayarları")]
    [Tooltip("Boğulma sırasında oyuncunun su akıntı yönünde (selin ilerleme yönü) sürüklenme hızı (m/s).")]
    [SerializeField] private float drownDriftSpeed = 3.2f;

    [Tooltip("Boğulma sırasında mevcut hızın su altı akıntı hızına sönümlenme (damping) ivmesi.")]
    [SerializeField] private float drownVelocityDamping = 5.0f;

    [Tooltip("Boğulma sırasında azaltılmış su altı yerçekimi ivmesi (m/s²).")]
    [SerializeField] private float drownReducedGravity = -2.2f;

    [Tooltip("Boğulma sırasında oyuncunun yavaşça yukarı-aşağı salınım (bobbing) dikey hız genliği (m/s).")]
    [SerializeField] private float drownBobVerticalSpeed = 0.65f;

    [Tooltip("Boğulma sırasında yukarı-aşağı sürüklenme salınım frekansı (rad/sn).")]
    [SerializeField] private float drownBobFrequency = 2.1f;

    [Tooltip("Su yüzeyinin ne kadar altında dengelenerek sürükleneceği (metre).")]
    [SerializeField] private float drownSubmergeDepthOffset = 0.85f;

    // C# Event'leri
    public event Action OnJumpCallback;
    public event Action<float> OnLandCallback;
    public event Action<PlayerState, PlayerState> OnStateChangedCallback;

    // Dahili çalışma değişkenleri
    private CharacterController controller;
    private PlayerState currentState = PlayerState.Grounded;
    private Vector3 velocity;
    private Vector3 groundNormal = Vector3.up;
    private bool isGrounded;
    private bool wasGroundedLastFrame;
    private bool isSprinting;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private bool isJumpCutApplied;
    private float lastAirborneVerticalVelocity;

    // Girdi önbelleği (Space tek noktadan okunur)
    private bool jumpPressedThisFrame;
    private float horizontalInput;
    private float verticalInput;
    private Vector3 moveInputDirection;

    // Boğulma (Drowning) durumu değişkenleri
    private Vector3 drowningFlowDirection = Vector3.forward;
    private float drowningWaterSurfaceY;
    private float drowningElapsedTimer;

    // Dış erişim özellikleri
    public CharacterController Controller => controller;
    public PlayerLook Look => playerLook;
    public WallRunHandler WallRun => wallRunHandler;
    public BranchGrabHandler BranchGrab => branchGrabHandler;
    public SlideHandler Slide => slideHandler;
    public PlayerVisual Visual => playerVisual;
    public PlayerState CurrentState => currentState;
    public Vector3 Velocity => velocity;
    public Vector3 HorizontalVelocity => new Vector3(velocity.x, 0f, velocity.z);
    public float HorizontalSpeed => new Vector3(velocity.x, 0f, velocity.z).magnitude;
    public float VerticalSpeed => velocity.y;
    public bool IsGrounded => isGrounded;
    public bool IsSprinting => isSprinting;
    public Vector3 GroundNormal => groundNormal;
    public float WalkSpeed => walkSpeed;
    public float SprintSpeed => sprintSpeed;
    public float Gravity => gravity;
    public float HorizontalInput => horizontalInput;
    public float VerticalInput => verticalInput;
    public Vector3 MoveInputDirection => moveInputDirection;
    public bool JumpPressedThisFrame => jumpPressedThisFrame;
    public bool HasBufferedJump => jumpBufferTimer > 0f;

    private bool isMovementLocked = false;
    public bool IsMovementLocked => isMovementLocked;

    /// <summary>
    /// Kaktüse çarpma veya ölüm durumunda oyuncunun hareketini tamamen kilitler/açar.
    /// Kilitliyken tüm girdi, hız ve parkur mekanizmaları sıfırlanır ve dondurulur.
    /// </summary>
    public void SetMovementLocked(bool locked)
    {
        isMovementLocked = locked;
        if (locked)
        {
            velocity = Vector3.zero;
            horizontalInput = 0f;
            verticalInput = 0f;
            moveInputDirection = Vector3.zero;
            jumpPressedThisFrame = false;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            isSprinting = false;

            if (slideHandler != null)
            {
                slideHandler.ForceCancelSlide();
            }

            if (branchGrabHandler != null)
            {
                branchGrabHandler.ForceResetBranchState();
            }

            if (wallRunHandler != null)
            {
                wallRunHandler.ForceResetWallRunState();
            }
        }
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (playerLook == null)
            playerLook = GetComponentInChildren<PlayerLook>();
        if (wallRunHandler == null)
            wallRunHandler = GetComponent<WallRunHandler>();
        if (branchGrabHandler == null)
            branchGrabHandler = GetComponent<BranchGrabHandler>();
        if (slideHandler == null)
            slideHandler = GetComponent<SlideHandler>();
        if (playerVisual == null)
            playerVisual = GetComponentInChildren<PlayerVisual>();

        if (GetComponent<PlayerAudioHandler>() == null)
            gameObject.AddComponent<PlayerAudioHandler>();
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // 0. Önce bu karedeki fare dönüşünü anında işle ki kamera ve gövde yönü sıfır gecikmeli olsun
        if (playerLook != null)
        {
            playerLook.ProcessMouseLookForCurrentFrame();
        }

        // Kaktüse çarptığında veya ölüm respawn sürecindeyken oyuncunun hareketini tamamen dondur
        if (isMovementLocked)
        {
            velocity = Vector3.zero;
            horizontalInput = 0f;
            verticalInput = 0f;
            moveInputDirection = Vector3.zero;
            jumpPressedThisFrame = false;
            jumpBufferTimer = 0f;
            isSprinting = false;

            if (playerLook != null)
            {
                playerLook.UpdateCameraEffects(0f, true, false, 0f, dt);
            }
            return;
        }

        // 1. Oyuncu girdilerini (ve Space basışını tek bir yerde) EN BAŞTA oku
        ReadPlayerInput(dt);

        // 2. Zemin algılamasını güncelle
        CheckGround();

        // 3. WallRun zamanlayıcılarını (wall coyote, wall jump buffer, cooldown) güncelle
        if (wallRunHandler != null && currentState != PlayerState.Drowning)
        {
            wallRunHandler.TickWallTimers(dt, jumpPressedThisFrame);
        }

        // 4. Slide zamanlayıcılarını güncelle
        if (slideHandler != null && currentState != PlayerState.Drowning)
        {
            slideHandler.TickTimersAndInput(dt);
        }

        // 5. Havadayken veya Wall Run sırasındayken Otomatik Dal Tutunma
        if (branchGrabHandler != null && currentState != PlayerState.Drowning)
        {
            bool canAutoDetectBranch = (currentState == PlayerState.Airborne || currentState == PlayerState.WallRunning);
            if (canAutoDetectBranch && branchGrabHandler.TryAutoGrabBranch())
            {
                if (wallRunHandler != null)
                {
                    wallRunHandler.TriggerPostBranchCooldown();
                }
            }
        }

        // 6. Durum makinesini (State Machine) çalıştır
        UpdateStateMachine(dt);

        // 7. Kamera efektlerini (FOV, Roll/Pitch/Height Offset, ViewMode ve Headbob) güncelle
        if (playerLook != null)
        {
            float targetWallTilt = (currentState == PlayerState.WallRunning && wallRunHandler != null)
                ? wallRunHandler.CurrentCameraTiltTarget
                : 0f;

            float effectiveSpeedForFov = HorizontalSpeed;
            if (currentState == PlayerState.Hanging && branchGrabHandler != null)
            {
                effectiveSpeedForFov = branchGrabHandler.CurrentTangentialSpeed;
            }
            else if (currentState == PlayerState.Transferring && branchGrabHandler != null)
            {
                effectiveSpeedForFov = branchGrabHandler.CurrentTransferSpeed;
            }
            else if (currentState == PlayerState.Sliding && slideHandler != null)
            {
                effectiveSpeedForFov = slideHandler.CurrentSlideSpeed;
            }

            playerLook.UpdateCameraEffects(
                effectiveSpeedForFov,
                isGrounded && currentState == PlayerState.Grounded,
                isSprinting,
                targetWallTilt,
                dt
            );
        }

        if (!isGrounded)
        {
            lastAirborneVerticalVelocity = velocity.y;
        }

        wasGroundedLastFrame = isGrounded;
    }

    private void ReadPlayerInput(float dt)
    {
        if (currentState == PlayerState.Drowning)
        {
            jumpPressedThisFrame = false;
            horizontalInput = 0f;
            verticalInput = 0f;
            moveInputDirection = Vector3.zero;
            isSprinting = false;
            jumpBufferTimer = 0f;
            return;
        }

        // Space girişi Update'in EN BAŞINDA tek bir yerde okunur ve jumpPressedThisFrame olarak saklanır
        jumpPressedThisFrame = Input.GetKeyDown(jumpKey);

        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        Vector3 rawDirection = transform.right * horizontalInput + transform.forward * verticalInput;
        moveInputDirection = rawDirection.sqrMagnitude > 1f ? rawDirection.normalized : rawDirection;

        bool forwardIntent = verticalInput > 0.1f;
        isSprinting = Input.GetKey(sprintKey) && forwardIntent;

        // Normal Jump Buffering (WallRunning durumunda normal jump buffer'a yazılmaz, WallRunHandler kendi yönetir)
        if (currentState == PlayerState.Grounded ||
            currentState == PlayerState.Airborne ||
            currentState == PlayerState.Sliding)
        {
            if (jumpPressedThisFrame)
            {
                jumpBufferTimer = jumpBufferDuration;
            }
            else if (jumpBufferTimer > 0f)
            {
                jumpBufferTimer -= dt;
            }
        }
        else
        {
            jumpBufferTimer = 0f;
        }

        // Variable Jump Height (yalnızca normal zıplamada; Wall Jump veya Swing Launch sonrasında hızı kesmez)
        if (Input.GetKeyUp(jumpKey) && velocity.y > 0f && !isJumpCutApplied && currentState == PlayerState.Airborne)
        {
            velocity.y *= earlyJumpReleaseMultiplier;
            isJumpCutApplied = true;
        }
    }

    private void CheckGround()
    {
        if (currentState == PlayerState.Hanging ||
            currentState == PlayerState.Transferring ||
            currentState == PlayerState.Drowning ||
            !controller.enabled)
        {
            isGrounded = false;
            groundNormal = Vector3.up;
            return;
        }

        float sphereRadius = controller.radius * groundCheckRadiusScale;
        Vector3 bottomCenter = transform.position + controller.center - Vector3.up * (controller.height * 0.5f - controller.radius - groundCheckOffset);

        bool hitGround = Physics.SphereCast(
            bottomCenter,
            sphereRadius,
            Vector3.down,
            out RaycastHit hitInfo,
            groundCheckDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        if (hitGround)
        {
            float slopeAngle = Vector3.Angle(hitInfo.normal, Vector3.up);
            if (slopeAngle <= controller.slopeLimit && velocity.y <= 1.5f)
            {
                isGrounded = true;
                groundNormal = hitInfo.normal;

                if (!wasGroundedLastFrame)
                {
                    if (wallRunHandler != null)
                    {
                        wallRunHandler.ResetLastWallOnGrounded();
                    }

                    OnLandCallback?.Invoke(lastAirborneVerticalVelocity);
                }

                return;
            }
        }

        isGrounded = false;
        groundNormal = Vector3.up;
    }

    private void UpdateStateMachine(float dt)
    {
        switch (currentState)
        {
            case PlayerState.Grounded:
                UpdateGroundedState(dt);
                break;

            case PlayerState.Airborne:
                UpdateAirborneState(dt);
                break;

            case PlayerState.WallRunning:
                UpdateWallRunningState(dt);
                break;

            case PlayerState.Hanging:
                if (branchGrabHandler != null)
                {
                    branchGrabHandler.UpdateHangingState(dt);
                }
                break;

            case PlayerState.Transferring:
                if (branchGrabHandler != null)
                {
                    branchGrabHandler.UpdateTransferringState(dt);
                }
                break;

            case PlayerState.Sliding:
                if (slideHandler != null)
                {
                    slideHandler.UpdateSlidingState(dt);
                }
                else
                {
                    SetState(PlayerState.Grounded);
                }
                break;

            case PlayerState.Drowning:
                UpdateDrowningState(dt);
                break;
        }
    }

    private void UpdateGroundedState(float dt)
    {
        coyoteTimer = coyoteTimeDuration;
        isJumpCutApplied = false;

        if (!isGrounded)
        {
            SetState(PlayerState.Airborne);
            return;
        }

        if (slideHandler != null && slideHandler.TryStartSlide())
        {
            return;
        }

        float targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
        Vector3 slopeAlignedInput = Vector3.ProjectOnPlane(moveInputDirection, groundNormal).normalized;
        Vector3 targetHorizontalVel = slopeAlignedInput * (moveInputDirection.magnitude * targetSpeed);

        Vector3 currentHorizontalVel = new Vector3(velocity.x, 0f, velocity.z);
        float accelRate = moveInputDirection.sqrMagnitude > 0.001f ? groundAcceleration : groundDeceleration;

        Vector3 newHorizontalVel = Vector3.MoveTowards(currentHorizontalVel, targetHorizontalVel, accelRate * dt);
        velocity.x = newHorizontalVel.x;
        velocity.z = newHorizontalVel.z;

        if (velocity.y < 0f)
        {
            velocity.y = groundedStickForce;
        }

        if (jumpPressedThisFrame || jumpBufferTimer > 0f)
        {
            ExecuteJump();
            return;
        }

        if (controller.enabled)
        {
            controller.Move(velocity * dt);
        }
    }

    private void UpdateAirborneState(float dt)
    {
        if (isGrounded)
        {
            SetState(PlayerState.Grounded);
            if (slideHandler != null && slideHandler.TryStartSlide())
            {
                return;
            }
            return;
        }

        if (coyoteTimer > 0f)
        {
            coyoteTimer -= dt;
        }

        // BUG FIX 1: Wall Jump kontrolü (Wall Coyote Time + allowAirWallJump)
        // normal zıplama, normal coyote ve normal buffer kontrollerinden ÖNCE yapılır!
        if (wallRunHandler != null && wallRunHandler.TryExecutePriorityWallJump(jumpPressedThisFrame))
        {
            return;
        }

        // Normal Coyote Jump kontrolü
        if ((jumpPressedThisFrame || jumpBufferTimer > 0f) && coyoteTimer > 0f)
        {
            ExecuteJump();
            return;
        }

        // 8 yönlü Wall Run başlangıç kontrolü (zincirleme ve wallJumpBufferTime dahil)
        if (wallRunHandler != null && wallRunHandler.CanStartWallRun())
        {
            wallRunHandler.StartWallRun();
            return;
        }

        Vector3 currentHorizontalVel = new Vector3(velocity.x, 0f, velocity.z);
        float currentHorizMag = currentHorizontalVel.magnitude;

        if (moveInputDirection.sqrMagnitude > 0.001f)
        {
            float baseTargetSpeed = isSprinting ? sprintSpeed : walkSpeed;
            float preservedTargetSpeed = Mathf.Max(baseTargetSpeed, currentHorizMag);
            preservedTargetSpeed = Mathf.Min(preservedTargetSpeed, maxAirHorizontalSpeed);

            Vector3 targetVel = moveInputDirection * preservedTargetSpeed;
            currentHorizontalVel = Vector3.MoveTowards(currentHorizontalVel, targetVel, airAcceleration * dt);
        }
        else
        {
            currentHorizontalVel = Vector3.MoveTowards(currentHorizontalVel, Vector3.zero, airDrag * dt);
        }

        velocity.x = currentHorizontalVel.x;
        velocity.z = currentHorizontalVel.z;

        float gravityMultiplier = velocity.y < 0f ? fastFallMultiplier : 1f;
        velocity.y += gravity * gravityMultiplier * dt;
        velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);

        if (controller.enabled)
        {
            CollisionFlags flags = controller.Move(velocity * dt);

            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f)
            {
                velocity.y = 0f;
            }
        }
    }

    private void UpdateWallRunningState(float dt)
    {
        if (wallRunHandler == null)
        {
            SetState(PlayerState.Airborne);
            return;
        }

        // WallRunning durumunda Space asla normal zıplamaya düşmez; yalnızca WallRunHandler içinde işlenir
        wallRunHandler.UpdateWallRun(dt);
    }

    public void EnterDrowningState(Vector3 flowDirection, float waterSurfaceY)
    {
        if (branchGrabHandler != null)
        {
            branchGrabHandler.ForceResetBranchState();
        }

        if (wallRunHandler != null)
        {
            wallRunHandler.ForceResetWallRunState();
        }

        if (slideHandler != null)
        {
            slideHandler.ForceResetSlideState();
        }

        UpdateDrowningWaterContext(flowDirection, waterSurfaceY);
        drowningElapsedTimer = 0f;

        ConsumeAllJumpInputs();
        horizontalInput = 0f;
        verticalInput = 0f;
        moveInputDirection = Vector3.zero;
        isSprinting = false;

        velocity *= 0.35f;

        SetState(PlayerState.Drowning);
    }

    public void UpdateDrowningWaterContext(Vector3 flowDirection, float waterSurfaceY)
    {
        Vector3 flatFlow = new Vector3(flowDirection.x, 0f, flowDirection.z);
        if (flatFlow.sqrMagnitude > 0.001f)
        {
            drowningFlowDirection = flatFlow.normalized;
        }
        drowningWaterSurfaceY = waterSurfaceY;
    }

    private void UpdateDrowningState(float dt)
    {
        drowningElapsedTimer += dt;

        Vector3 targetHorizontalDrift = drowningFlowDirection * drownDriftSpeed;
        Vector3 currentHoriz = new Vector3(velocity.x, 0f, velocity.z);
        Vector3 dampedHoriz = Vector3.MoveTowards(currentHoriz, targetHorizontalDrift, drownVelocityDamping * dt);

        velocity.x = dampedHoriz.x;
        velocity.z = dampedHoriz.z;

        float bobWave = Mathf.Sin(drowningElapsedTimer * drownBobFrequency) * drownBobVerticalSpeed;
        float targetSubmergedY = drowningWaterSurfaceY - drownSubmergeDepthOffset;
        float depthError = targetSubmergedY - transform.position.y;
        float buoyancyPull = Mathf.Clamp(depthError * 1.8f, -1.5f, 2.2f);

        float targetVerticalVel = bobWave + buoyancyPull + (drownReducedGravity * 0.25f);
        velocity.y = Mathf.MoveTowards(velocity.y, targetVerticalVel, drownVelocityDamping * dt);

        if (controller != null && controller.enabled)
        {
            controller.Move(velocity * dt);
        }
        else
        {
            transform.position += velocity * dt;
        }
    }

    private void ExecuteJump()
    {
        float jumpVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
        velocity.y = jumpVelocity;

        ConsumeAllJumpInputs();
        isGrounded = false;
        isJumpCutApplied = false;

        SetState(PlayerState.Airborne);
        OnJumpCallback?.Invoke();

        if (controller.enabled)
        {
            controller.Move(velocity * Time.deltaTime);
        }
    }

    public void ExecuteJumpWithCustomHorizontalVelocity(Vector3 customHorizontalVel)
    {
        float jumpVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
        velocity = new Vector3(customHorizontalVel.x, jumpVelocity, customHorizontalVel.z);

        ConsumeAllJumpInputs();
        isGrounded = false;
        isJumpCutApplied = false;

        SetState(PlayerState.Airborne);
        OnJumpCallback?.Invoke();

        if (controller.enabled)
        {
            controller.Move(velocity * Time.deltaTime);
        }
    }

    public void SetState(PlayerState newState)
    {
        if (currentState == newState)
            return;

        PlayerState previousState = currentState;
        currentState = newState;

        if (previousState == PlayerState.Sliding && newState != PlayerState.Sliding && slideHandler != null)
        {
            slideHandler.ForceCancelSlide();
        }

        bool requiresManualTransform = (newState == PlayerState.Hanging || newState == PlayerState.Transferring);
        SetCharacterControllerEnabled(!requiresManualTransform);

        bool wasOnBranch = (previousState == PlayerState.Hanging || previousState == PlayerState.Transferring);
        if (wasOnBranch && !requiresManualTransform)
        {
            if (playerLook != null)
            {
                playerLook.ResetCameraOffsets();
                if (newState != PlayerState.Drowning)
                {
                    playerLook.SetMouseInputEnabled(true);
                }
            }

            if (wallRunHandler != null)
            {
                wallRunHandler.TriggerPostBranchCooldown();
            }
        }

        OnStateChangedCallback?.Invoke(previousState, newState);
    }

    public void SetCharacterControllerEnabled(bool shouldEnable)
    {
        if (controller == null)
            return;

        if (shouldEnable && !controller.enabled)
        {
            Physics.SyncTransforms();
            controller.enabled = true;
        }
        else if (!shouldEnable && controller.enabled)
        {
            controller.enabled = false;
            Physics.SyncTransforms();
        }
    }

    public void SetVelocity(Vector3 newVelocity)
    {
        velocity = newVelocity;
        if (newVelocity.y > 0f)
        {
            isGrounded = false;
            coyoteTimer = 0f;
            isJumpCutApplied = true;
        }
    }

    public void AddVelocity(Vector3 impulse)
    {
        SetVelocity(velocity + impulse);
    }

    public void ConsumeJumpBuffer()
    {
        jumpPressedThisFrame = false;
        jumpBufferTimer = 0f;
    }

    /// <summary>
    /// Wall jump, normal jump veya dal fırlaması gerçekleştiğinde aynı Space basışının
    /// ikinci bir zıplama tetiklememesi için tüm zıplama/coyote/buffer bayraklarını sıfırlar.
    /// </summary>
    public void ConsumeAllJumpInputs()
    {
        jumpPressedThisFrame = false;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider == null)
            return;

        CactusHazard hazard = hit.collider.GetComponentInParent<CactusHazard>();
        if (hazard != null)
        {
            hazard.HandlePlayerHit(this);
        }
    }

    public void TeleportAndResetState(Vector3 targetPosition, Quaternion targetRotation)
    {
        SetMovementLocked(false);
        SetCharacterControllerEnabled(false);

        if (branchGrabHandler != null)
        {
            branchGrabHandler.ForceResetBranchState();
        }

        if (wallRunHandler != null)
        {
            wallRunHandler.ForceResetWallRunState();
        }

        if (slideHandler != null)
        {
            slideHandler.ForceResetSlideState();
        }

        velocity = Vector3.zero;
        moveInputDirection = Vector3.zero;
        horizontalInput = 0f;
        verticalInput = 0f;
        ConsumeAllJumpInputs();
        isJumpCutApplied = false;
        isSprinting = false;
        lastAirborneVerticalVelocity = 0f;

        Vector3 euler = targetRotation.eulerAngles;
        Quaternion flatBodyRot = Quaternion.Euler(0f, euler.y, 0f);
        transform.SetPositionAndRotation(targetPosition, flatBodyRot);

        if (playerLook != null)
        {
            playerLook.ResetLookState(flatBodyRot);
        }

        PlayerState prev = currentState;
        currentState = PlayerState.Grounded;
        isGrounded = true;
        wasGroundedLastFrame = true;

        SetCharacterControllerEnabled(true);

        if (prev != currentState)
        {
            OnStateChangedCallback?.Invoke(prev, currentState);
        }
    }

    public void ResetMotorVelocityAndState()
    {
        SetMovementLocked(false);
        TeleportAndResetState(transform.position, transform.rotation);
    }

    private void OnDrawGizmosSelected()
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc == null) return;

        float sphereRadius = cc.radius * groundCheckRadiusScale;
        Vector3 bottomCenter = transform.position + cc.center - Vector3.up * (cc.height * 0.5f - cc.radius - groundCheckOffset);
        Vector3 castEnd = bottomCenter + Vector3.down * groundCheckDistance;

        Gizmos.color = isGrounded ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(bottomCenter, sphereRadius);
        Gizmos.DrawWireSphere(castEnd, sphereRadius);
        Gizmos.DrawLine(bottomCenter, castEnd);
    }
}
