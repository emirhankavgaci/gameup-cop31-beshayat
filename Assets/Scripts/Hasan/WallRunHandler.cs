using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// CANOPY: The Last Run - 8 Yönlü Duvar Algılama, WallRunSurface Tek Yüz Doğrulaması,
/// Bakış + Girdi Yönlü Duvardan Duvara Zıplama (Wall Jump), Wall Coyote Time, Wall Jump Buffer,
/// Havada Duvara Yakınken Wall Jump (allowAirWallJump), Aim Assist ve Duvar Zincirleme (Wall Chain) Yöneticisi.
///
/// Not: Wall run sırasında A/D/S ile duvardan düşme tamamen kaldırılmıştır.
/// Wall run yalnızca Space (wall jump), Left Ctrl (bırakma), süre dolması, duvar/geçerli yüz bitmesi
/// veya yere inme ile sona erer.
/// </summary>
public class WallRunHandler : MonoBehaviour
{
    private const int DIRECTION_RAY_COUNT = 8;
    private const float DIRECTION_ANGLE_STEP = 45f;

    [Header("Referanslar")]
    [Tooltip("Merkezi karakter hareket yöneticisi.")]
    [SerializeField] private PlayerMotor motor;

    [Tooltip("Anlık kamera yönünü okumak için PlayerLook bileşeni.")]
    [SerializeField] private PlayerLook playerLook;

    [Header("1b) 8 Yönlü Duvar Algılama ve WallRunSurface Ayarları")]
    [Tooltip("Sadece bu katmandaki (WallRunnable) duvarlarda wall run çalışır.")]
    [SerializeField] private LayerMask wallRunnableLayer;

    [Tooltip("8 yönlü yatay Raycast algılama mesafesi (varsayılan 1.2 m).")]
    [SerializeField] private float wallDetectDistance = 1.2f;

    [Tooltip("Raycast başlangıç noktasının karakter merkezine göre dikey ofseti.")]
    [SerializeField] private float raycastHeightOffset = 0f;

    [Tooltip("Duvar yüzey normali ile Vector3.up arasındaki minimum açı (dik duvar kontrolü).")]
    [SerializeField] private float minWallNormalAngle = 80f;

    [Tooltip("Duvar yüzey normali ile Vector3.up arasındaki maksimum açı (dik duvar kontrolü).")]
    [SerializeField] private float maxWallNormalAngle = 100f;

    [Tooltip("Açık olursa WallRunSurface bileşeni olmayan duvarlarda koşulmasına izin verilmez. Kapalıysa (varsayılan) uyarı verip iki yüzü de kabul eder.")]
    [SerializeField] private bool requireSurfaceComponent = false;

    [Header("1c) Wall Run Başlama, Bırakma ve Zincirleme (Chain) Kuralları")]
    [Tooltip("Wall run başlatmak için gereken minimum yatay hız eşiği (varsayılan 3.5 m/s).")]
    [SerializeField] private float minWallRunSpeed = 3.5f;

    [Tooltip("Wall jump sonrası W tuşuna basılmasa bile wall run başlatılabilen tolerans süresi (varsayılan 0.6 sn).")]
    [SerializeField] private float wallChainGraceTime = 0.6f;

    [Tooltip("Normal zamanda (grace time dışında) wall run başlatmak için W tuşu şartı aransın mı? (Akıcı zincirleme için kapalı önerilir).")]
    [SerializeField] private bool requireForwardKeyOutsideGrace = false;

    [Tooltip("İlk duvardaki maksimum kesintisiz wall run süresi (varsayılan 2.5 sn).")]
    [SerializeField] private float maxWallRunDuration = 2.5f;

    [Tooltip("Her art arda duvar zincirlemesinde (chain) maksimum süreden düşülecek ceza süresi (varsayılan 0.15 sn).")]
    [SerializeField] private float chainTimePenalty = 0.15f;

    [Tooltip("Çoklu zincirlemede wall run süresinin düşebileceği en alt sınır (varsayılan 1.2 sn).")]
    [SerializeField] private float minChainWallRunDuration = 1.2f;

    [Tooltip("Aynı duvara (lastWallCollider) tekrar yapışmayı engelleyen bekleme süresi (varsayılan 0.2 sn).")]
    [SerializeField] private float sameWallRegrabCooldown = 0.2f;

    [Tooltip("Wall run sırasında duvardan bilerek ayrılıp düşmek için kullanılan tek tuş (varsayılan Left Ctrl). A/D/S düşürmez!")]
    [SerializeField] private KeyCode wallDropKey = KeyCode.LeftControl;

    [Header("1d) Wall Run Hareket, Yön Verme ve Fizik")]
    [Tooltip("Wall run sırasında yerçekiminin etki oranı (0.10 = %10 yerçekimi etkisi).")]
    [Range(0f, 1f)]
    [SerializeField] private float wallRunGravityMultiplier = 0.10f;

    [Tooltip("Wall run başladığında dikey hızı yumuşatmak için başlangıç dikey hız çarpanı.")]
    [SerializeField] private float initialVerticalVelocityDamping = 0.25f;

    [Tooltip("Duvar boyunca koşarken yatay hıza eklenecek ivmelenme (m/s²).")]
    [SerializeField] private float wallRunSpeedBoostRate = 3.5f;

    [Tooltip("Wall run sırasında ulaşılabilecek maksimum paralel hız (m/s).")]
    [SerializeField] private float maxWallRunSpeed = 14.5f;

    [Tooltip("Karakteri duvarda tutan ama hız kaybettirmeyen yapışma hızı.")]
    [SerializeField] private float wallStickPushSpeed = 1.8f;

    [Tooltip("A/D tuşlarıyla duvara hafifçe yaklaşma etkisi (duvardan düşürmez).")]
    [SerializeField] private float wallRunSteerStrength = 1.0f;

    [Tooltip("Duvarın karşı tarafına doğru uygulanacak kamera yatış (roll/tilt) açısı (12-15 derece).")]
    [Range(8f, 20f)]
    [SerializeField] private float wallRunCameraTiltAngle = 14f;

    [Header("1a) Bakış + Girdi Yönlü Wall Jump, Coyote, Buffer ve Aim Assist")]
    [Tooltip("Wall jump tetikleme tuşu.")]
    [SerializeField] private KeyCode wallJumpKey = KeyCode.Space;

    [Tooltip("Wall run bittikten sonra (süre dolması, duvar bitişi) Space tuşunun hâlâ wall jump yapabildiği coyote süresi (varsayılan 0.25 sn).")]
    [SerializeField] private float wallJumpCoyoteTime = 0.25f;

    [Tooltip("Duvara yapışmadan hemen önce basılan Space tuşunu hatırlama (buffer) süresi (varsayılan 0.15 sn).")]
    [SerializeField] private float wallJumpBufferTime = 0.15f;

    [Tooltip("Havadayken ve duvara yakınken (wallDetectDistance içinde) wall run başlamamış olsa bile Space ile wall jump yapılabilsin.")]
    [SerializeField] private bool allowAirWallJump = true;

    [Tooltip("Wall jump yatay yön hesabında kamera bakış yönünün ağırlığı (varsayılan 1.0).")]
    [SerializeField] private float wallJumpLookInfluence = 1.0f;

    [Tooltip("A/W/D hareket girdisi varsa, hareket girdisi yönünün kamera bakış yönüne karıştırılma oranı (varsayılan 0.5).")]
    [Range(0f, 1f)]
    [SerializeField] private float wallJumpInputInfluence = 0.5f;

    [Tooltip("Wall jump yatay yön hesabında duvar normalinin (duvardan uzağa) ağırlığı (varsayılan 0.5).")]
    [SerializeField] private float wallJumpAwayInfluence = 0.5f;

    [Tooltip("Oyuncu duvara doğru baksa bile duvardan uzağa uygulanacak minimum normal ağırlığı (varsayılan 0.35).")]
    [SerializeField] private float wallJumpMinAwayFromWall = 0.35f;

    [Tooltip("Wall jump sırasında verilecek minimum yatay fırlama hızı (varsayılan 13 m/s).")]
    [SerializeField] private float wallJumpHorizontalSpeed = 13f;

    [Tooltip("Wall jump sırasında verilecek dikey (yukarı) fırlama hızı (varsayılan 7 m/s).")]
    [SerializeField] private float wallJumpUpSpeed = 7f;

    [Tooltip("Bakış yönünde 20 derece koni ve 12m menzilde karşı duvar varsa yönü %30 o duvara yumuşatır.")]
    [SerializeField] private bool wallJumpAimAssist = true;

    [Tooltip("Aim assist tarama konisi yarı açısı (varsayılan 20 derece).")]
    [SerializeField] private float aimAssistConeAngle = 20f;

    [Tooltip("Aim assist maksimum duvar arama menzili (varsayılan 12 m).")]
    [SerializeField] private float aimAssistMaxDistance = 12f;

    [Tooltip("Aim assist hedef duvara yönelme yumuşatma oranı (0.30 = %30).")]
    [Range(0f, 1f)]
    [SerializeField] private float aimAssistStrength = 0.30f;

    [Header("Olaylar (Ses ve Efekt Bağlantısı İçin)")]
    [Tooltip("Wall run başladığında tetiklenen UnityEvent.")]
    [SerializeField] private UnityEvent onWallRunStart;

    [Tooltip("Wall run sona erdiğinde tetiklenen UnityEvent.")]
    [SerializeField] private UnityEvent onWallRunEnd;

    [Tooltip("Wall jump yapıldığında tetiklenen UnityEvent.")]
    [SerializeField] private UnityEvent onWallJump;

    public event Action OnWallRunStartCallback;
    public event Action OnWallRunEndCallback;
    public event Action<Vector3> OnWallJumpCallback;

    // Dahili çalışma durumu
    private readonly Collider[] aimAssistColliders = new Collider[16];
    private readonly HashSet<Collider> warnedMissingSurfaceColliders = new HashSet<Collider>();

    private bool hasDetectedWall;
    private bool isWallRight;
    private bool isWallLeft;
    private RaycastHit closestWallHit;
    private Vector3 activeWallNormal;
    private Collider activeWallCollider;
    private Collider lastWallCollider;

    // Wall Coyote ve Wall Jump Buffer değişkenleri
    private float wallJumpCoyoteTimer;
    private Vector3 coyoteWallNormal;
    private Collider coyoteWallCollider;
    private float wallJumpBufferTimer;

    private float wallRunTimer;
    private float sameWallCooldownTimer;
    private float postBranchLockTimer;
    private float chainGraceTimer;
    private float currentWallSpeed;
    private int chainCount;
    private Vector3 lastWallJumpDirection = Vector3.forward;

    // HUD tanı değişkenleri
    private bool wallJumpAvailable;
    private string lastWallJumpBlockReason = "none";

    // Debug HUD ve dış erişim özellikleri
    public bool IsWallRight => isWallRight;
    public bool IsWallLeft => isWallLeft;
    public Vector3 ActiveWallNormal => activeWallNormal;
    public float WallRunTimer => wallRunTimer;
    public int ChainCount => chainCount;
    public string ActiveWallName => (motor != null && motor.CurrentState == PlayerMotor.PlayerState.WallRunning && activeWallCollider != null)
        ? activeWallCollider.name
        : "YOK";
    public string LastWallColliderName => lastWallCollider != null ? lastWallCollider.name : "YOK";
    public Vector3 LastWallJumpDirection => lastWallJumpDirection;
    public bool WallJumpAvailable => wallJumpAvailable;
    public float WallCoyoteRemaining => Mathf.Max(0f, wallJumpCoyoteTimer);
    public float WallJumpBufferRemaining => Mathf.Max(0f, wallJumpBufferTimer);
    public string LastWallJumpBlockReason => lastWallJumpBlockReason;

    public float CurrentCameraTiltTarget
    {
        get
        {
            if (motor == null || motor.CurrentState != PlayerMotor.PlayerState.WallRunning)
                return 0f;

            // Duvar sağdaysa kamera sola (+Z roll), duvar soldaysa kamera sağa (-Z roll) yatar
            return isWallRight ? wallRunCameraTiltAngle : -wallRunCameraTiltAngle;
        }
    }

    private void Awake()
    {
        if (motor == null)
            motor = GetComponent<PlayerMotor>();

        if (playerLook == null)
            playerLook = GetComponentInChildren<PlayerLook>();

        if (wallRunnableLayer.value == 0)
        {
            int layerIndex = LayerMask.NameToLayer("WallRunnable");
            if (layerIndex != -1)
            {
                wallRunnableLayer = 1 << layerIndex;
            }
        }
    }

    /// <summary>
    /// PlayerMotor.Update başında çağrılır; zamanlayıcıları ve HUD için wallJumpAvailable durumunu günceller.
    /// </summary>
    public void TickWallTimers(float dt, bool spacePressedThisFrame)
    {
        if (sameWallCooldownTimer > 0f)
        {
            sameWallCooldownTimer -= dt;
        }

        if (postBranchLockTimer > 0f)
        {
            postBranchLockTimer -= dt;
        }

        if (chainGraceTimer > 0f)
        {
            chainGraceTimer -= dt;
        }

        if (wallJumpCoyoteTimer > 0f)
        {
            wallJumpCoyoteTimer -= dt;
            if (wallJumpCoyoteTimer <= 0f)
            {
                coyoteWallCollider = null;
            }
        }

        if (spacePressedThisFrame)
        {
            wallJumpBufferTimer = wallJumpBufferTime;
        }
        else if (wallJumpBufferTimer > 0f)
        {
            wallJumpBufferTimer -= dt;
        }

        UpdateWallJumpDiagnosticState();
    }

    /// <summary>
    /// PlayerMotor tarafından normal zıplama, normal coyote ve normal buffer kontrollerinden ÖNCE çağrılır.
    /// Eğer oyuncu WallRunning durumundaysa, Wall Coyote süresi içindeyse veya allowAirWallJump ile
    /// havada geçerli bir duvara yakınsa Wall Jump yapar ve true döner.
    /// </summary>
    public bool TryExecutePriorityWallJump(bool spacePressedThisFrame)
    {
        if (motor == null)
            return false;

        bool wantsWallJump = spacePressedThisFrame || wallJumpBufferTimer > 0f;
        if (!wantsWallJump)
            return false;

        // 1. Durum: Zaten WallRunning durumundayız -> Space her zaman Wall Jump yapar (asla normal zıplamaya düşmez)
        if (motor.CurrentState == PlayerMotor.PlayerState.WallRunning)
        {
            lastWallJumpBlockReason = "none";
            ExecuteWallJumpFromSurface(activeWallNormal, activeWallCollider);
            return true;
        }

        // Sadece Airborne durumunda coyote veya air wall jump çalışabilir
        if (motor.CurrentState != PlayerMotor.PlayerState.Airborne || motor.IsGrounded)
        {
            if (spacePressedThisFrame)
            {
                lastWallJumpBlockReason = "state";
            }
            return false;
        }

        if (postBranchLockTimer > 0f)
        {
            if (spacePressedThisFrame)
            {
                lastWallJumpBlockReason = "cooldown";
            }
            return false;
        }

        // 2. Durum: Wall Coyote Time (wall run yeni bitti, 0.25 sn içinde Space'e basıldı)
        if (wallJumpCoyoteTimer > 0f && coyoteWallCollider != null)
        {
            lastWallJumpBlockReason = "none";
            ExecuteWallJumpFromSurface(coyoteWallNormal, coyoteWallCollider);
            return true;
        }

        // 3. Durum: Havadayken duvara yakın (wallDetectDistance içinde), wall run başlamamış olsa bile Air Wall Jump
        if (allowAirWallJump && spacePressedThisFrame)
        {
            if (DetectClosestValidWall(ignoreLastWallRule: false, out RaycastHit airWallHit, out string failReason))
            {
                lastWallJumpBlockReason = "none";
                currentWallSpeed = Mathf.Max(motor.HorizontalSpeed, motor.WalkSpeed);
                ExecuteWallJumpFromSurface(airWallHit.normal, airWallHit.collider);
                return true;
            }

            lastWallJumpBlockReason = failReason;
        }

        return false;
    }

    private void UpdateWallJumpDiagnosticState()
    {
        if (motor == null)
        {
            wallJumpAvailable = false;
            lastWallJumpBlockReason = "state";
            return;
        }

        if (motor.CurrentState == PlayerMotor.PlayerState.WallRunning)
        {
            wallJumpAvailable = true;
            lastWallJumpBlockReason = "none";
            return;
        }

        if (motor.CurrentState != PlayerMotor.PlayerState.Airborne || motor.IsGrounded)
        {
            wallJumpAvailable = false;
            lastWallJumpBlockReason = "state";
            return;
        }

        if (postBranchLockTimer > 0f)
        {
            wallJumpAvailable = false;
            lastWallJumpBlockReason = "cooldown";
            return;
        }

        if (wallJumpCoyoteTimer > 0f && coyoteWallCollider != null)
        {
            wallJumpAvailable = true;
            lastWallJumpBlockReason = "none";
            return;
        }

        if (allowAirWallJump)
        {
            bool found = DetectClosestValidWall(ignoreLastWallRule: false, out _, out string failReason);
            wallJumpAvailable = found;
            lastWallJumpBlockReason = found ? "none" : failReason;
            return;
        }

        wallJumpAvailable = false;
        lastWallJumpBlockReason = "no wall";
    }

    /// <summary>
    /// Çarpan RaycastHit'in dik duvar açısına ve WallRunSurface tek yüz kuralına uyup uymadığını kontrol eder.
    /// </summary>
    private bool IsValidWallSurfaceHit(RaycastHit hit, out string failReason)
    {
        failReason = "none";
        if (hit.collider == null)
        {
            failReason = "no wall";
            return false;
        }

        float angleWithUp = Vector3.Angle(hit.normal, Vector3.up);
        bool isVerticalWall = (angleWithUp >= minWallNormalAngle && angleWithUp <= maxWallNormalAngle);
        if (!isVerticalWall)
        {
            failReason = "no wall";
            return false;
        }

        WallRunSurface surface = hit.collider.GetComponent<WallRunSurface>();
        if (surface == null)
        {
            surface = hit.collider.GetComponentInParent<WallRunSurface>();
        }

        if (surface == null)
        {
            if (!warnedMissingSurfaceColliders.Contains(hit.collider))
            {
                warnedMissingSurfaceColliders.Add(hit.collider);
                Debug.LogWarning(
                    $"[WallRunHandler] '{hit.collider.name}' objesi WallRunnable katmanında ancak üzerinde WallRunSurface bileşeni yok! " +
                    (requireSurfaceComponent ? "WallRun reddedildi." : "Eski davranışa (iki yüz geçerli) düşüldü."),
                    hit.collider
                );
            }

            if (requireSurfaceComponent)
            {
                failReason = "no wall";
                return false;
            }

            return true;
        }

        if (!surface.IsHitNormalValid(hit.normal))
        {
            failReason = "backface";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 8 yönlü yatay Raycast atar. Yalnızca geçerli yüze (WallRunSurface) çarpan hit'ler aday olur ve en yakını seçilir.
    /// </summary>
    private bool DetectClosestValidWall(bool ignoreLastWallRule, out RaycastHit bestHit, out string blockReason)
    {
        bestHit = default;
        blockReason = "no wall";
        hasDetectedWall = false;
        isWallRight = false;
        isWallLeft = false;

        Vector3 origin = transform.position + Vector3.up * raycastHeightOffset;
        Vector3 baseForward = GetCameraHorizontalForward();

        float minDistance = float.MaxValue;
        bool foundAny = false;
        bool rejectedBySameCollider = false;
        bool rejectedByCooldown = false;
        bool rejectedByBackface = false;

        for (int i = 0; i < DIRECTION_RAY_COUNT; i++)
        {
            float angle = i * DIRECTION_ANGLE_STEP;
            Vector3 rayDir = Quaternion.Euler(0f, angle, 0f) * baseForward;

            if (Physics.Raycast(origin, rayDir, out RaycastHit hit, wallDetectDistance, wallRunnableLayer, QueryTriggerInteraction.Ignore))
            {
                if (!IsValidWallSurfaceHit(hit, out string surfaceFailReason))
                {
                    if (surfaceFailReason == "backface")
                    {
                        rejectedByBackface = true;
                    }
                    continue;
                }

                // Aynı duvar (lastWallCollider) ve cooldown kontrolü
                if (!ignoreLastWallRule && lastWallCollider != null && hit.collider == lastWallCollider)
                {
                    if (sameWallCooldownTimer > 0f)
                    {
                        rejectedByCooldown = true;
                    }
                    else
                    {
                        rejectedBySameCollider = true;
                    }
                    continue;
                }

                if (hit.distance < minDistance)
                {
                    minDistance = hit.distance;
                    bestHit = hit;
                    foundAny = true;
                }
            }
        }

        if (foundAny)
        {
            hasDetectedWall = true;
            closestWallHit = bestHit;
            blockReason = "none";
            UpdateWallSideRelativeToCamera(bestHit.normal);
            return true;
        }

        if (rejectedByCooldown)
            blockReason = "cooldown";
        else if (rejectedBySameCollider)
            blockReason = "same collider";
        else if (rejectedByBackface)
            blockReason = "backface";
        else
            blockReason = "no wall";

        return false;
    }

    private void UpdateWallSideRelativeToCamera(Vector3 wallNormal)
    {
        Vector3 camForward = GetCameraHorizontalForward();
        Vector3 camRight = Vector3.Cross(Vector3.up, camForward).normalized;
        Vector3 toWallDir = -new Vector3(wallNormal.x, 0f, wallNormal.z).normalized;

        float rightDot = Vector3.Dot(camRight, toWallDir);
        isWallRight = rightDot >= 0f;
        isWallLeft = !isWallRight;
    }

    /// <summary>
    /// Oyuncunun havada olup olmadığını, hız eşiğini ve 8 yönlü geçerli duvar yüzü temasını kontrol eder.
    /// </summary>
    public bool CanStartWallRun()
    {
        if (motor == null || motor.IsGrounded || postBranchLockTimer > 0f)
            return false;

        if (motor.CurrentState == PlayerMotor.PlayerState.Hanging ||
            motor.CurrentState == PlayerMotor.PlayerState.Transferring ||
            motor.CurrentState == PlayerMotor.PlayerState.Sliding ||
            motor.CurrentState == PlayerMotor.PlayerState.Drowning)
        {
            return false;
        }

        if (motor.HorizontalSpeed < minWallRunSpeed)
            return false;

        if (requireForwardKeyOutsideGrace && chainGraceTimer <= 0f && motor.VerticalInput <= 0.05f)
            return false;

        return DetectClosestValidWall(ignoreLastWallRule: false, out _, out _);
    }

    /// <summary>
    /// Wall Run durumunu başlatır; eğer duvara değmeden önceki 0.15 sn içinde Space'e basılmışsa
    /// (wallJumpBufferTimer > 0) otomatik olarak anında Wall Jump tetikler.
    /// </summary>
    public void StartWallRun()
    {
        activeWallNormal = closestWallHit.normal;
        activeWallCollider = closestWallHit.collider;

        if (lastWallCollider != null && activeWallCollider != lastWallCollider)
        {
            lastWallCollider = null;
        }

        wallJumpCoyoteTimer = 0f;
        coyoteWallCollider = null;

        float calculatedDuration = maxWallRunDuration - (chainCount * chainTimePenalty);
        wallRunTimer = Mathf.Max(minChainWallRunDuration, calculatedDuration);
        chainCount++;

        currentWallSpeed = Mathf.Max(motor.HorizontalSpeed, motor.WalkSpeed);

        Vector3 wallForward = CalculateWallForwardDirection(activeWallNormal);
        float dampedVerticalY = motor.Velocity.y > 0f
            ? motor.Velocity.y * initialVerticalVelocityDamping
            : 0f;

        UpdateWallSideRelativeToCamera(activeWallNormal);
        lastWallJumpDirection = ComputeWallJumpVelocityVector(activeWallNormal, activeWallCollider, out _).normalized;

        motor.SetVelocity(wallForward * currentWallSpeed + Vector3.up * dampedVerticalY);
        motor.SetState(PlayerMotor.PlayerState.WallRunning);

        onWallRunStart?.Invoke();
        OnWallRunStartCallback?.Invoke();

        // Wall Jump Buffer: Duvara yapışmadan hemen önce (0.15 sn içinde) Space'e basıldıysa otomatik Wall Jump yap!
        if (wallJumpBufferTimer > 0f || motor.JumpPressedThisFrame)
        {
            lastWallJumpBlockReason = "none";
            ExecuteWallJumpFromSurface(activeWallNormal, activeWallCollider);
        }
    }

    /// <summary>
    /// Her karede Wall Run hareketini işler.
    /// A/D/S ile duvardan düşme tamamen kaldırılmıştır! Sadece Space (Wall Jump), Left Ctrl (Bırakma),
    /// süre dolması, duvarın/geçerli yüzün bitmesi veya yere inme ile biter.
    /// </summary>
    public void UpdateWallRun(float dt)
    {
        // 1. Space basıldıysa veya buffer varsa -> Wall Jump
        if (motor.JumpPressedThisFrame || wallJumpBufferTimer > 0f || motor.HasBufferedJump)
        {
            lastWallJumpBlockReason = "none";
            ExecuteWallJumpFromSurface(activeWallNormal, activeWallCollider);
            return;
        }

        // 2. Left Ctrl basıldıysa -> İsteğe bağlı bırakma
        if (Input.GetKeyDown(wallDropKey))
        {
            StopWallRunAndFall(enableCoyote: false);
            return;
        }

        // 3. Aktif duvarı ve geçerli yüzü (WallRunSurface) kontrol et
        // Köşeden arka yüze dolanılırsa DetectClosestValidWall false döner ve wall run otomatik biter (coyote başlar)
        bool stillOnWall = DetectClosestValidWall(ignoreLastWallRule: true, out RaycastHit currentHit, out _);
        wallRunTimer -= dt;

        if (!stillOnWall || motor.IsGrounded || wallRunTimer <= 0f)
        {
            StopWallRunAndFall(enableCoyote: !motor.IsGrounded);
            return;
        }

        activeWallNormal = currentHit.normal;
        activeWallCollider = currentHit.collider;
        UpdateWallSideRelativeToCamera(activeWallNormal);

        // 4. Oyuncunun mouse bakışı duvar boyunca koşu yönünü belirler
        Vector3 wallForward = CalculateWallForwardDirection(activeWallNormal);
        currentWallSpeed = Mathf.MoveTowards(currentWallSpeed, maxWallRunSpeed, wallRunSpeedBoostRate * dt);

        // Azaltılmış yerçekimi (%10)
        float reducedGravity = motor.Gravity * wallRunGravityMultiplier;
        float newVerticalVelocity = motor.Velocity.y + reducedGravity * dt;

        // Duvarda tutan yapışma kuvveti (A/D basılsa bile asla duvardan düşürmez!)
        Vector3 horizontalNormal = new Vector3(activeWallNormal.x, 0f, activeWallNormal.z).normalized;
        float netStickPush = Mathf.Max(0.8f, wallStickPushSpeed);
        Vector3 stickVelocity = -horizontalNormal * netStickPush;

        Vector3 finalWallVelocity = (wallForward * currentWallSpeed) + (Vector3.up * newVerticalVelocity);

        lastWallJumpDirection = ComputeWallJumpVelocityVector(activeWallNormal, activeWallCollider, out _).normalized;

        motor.SetVelocity(finalWallVelocity);
        if (motor.Controller.enabled)
        {
            motor.Controller.Move((finalWallVelocity + stickVelocity) * dt);
        }
    }

    /// <summary>
    /// Belirtilen duvar normali ve collider üzerinden Wall Jump gerçekleştirir;
    /// tüm zıplama buffer ve coyote sayaçlarını sıfırlar ki aynı Space basışı ikinci kez zıplatmasın.
    /// </summary>
    private void ExecuteWallJumpFromSurface(Vector3 wallNormal, Collider wallCol)
    {
        bool wasWallRunning = (motor != null && motor.CurrentState == PlayerMotor.PlayerState.WallRunning);

        Vector3 jumpVelocity = ComputeWallJumpVelocityVector(wallNormal, wallCol, out Vector3 jumpDirNormalized);
        lastWallJumpDirection = jumpDirNormalized;

        lastWallCollider = wallCol;
        sameWallCooldownTimer = sameWallRegrabCooldown;
        chainGraceTimer = wallChainGraceTime;

        // Tüm zıplama buffer ve coyote sayaçlarını sıfırla (çift zıplamayı önler)
        wallJumpBufferTimer = 0f;
        wallJumpCoyoteTimer = 0f;
        coyoteWallCollider = null;
        motor.ConsumeAllJumpInputs();

        motor.SetState(PlayerMotor.PlayerState.Airborne);
        motor.SetVelocity(jumpVelocity);

        onWallJump?.Invoke();
        OnWallJumpCallback?.Invoke(jumpDirNormalized);

        if (wasWallRunning)
        {
            onWallRunEnd?.Invoke();
            OnWallRunEndCallback?.Invoke();
        }
    }

    /// <summary>
    /// Space basıldığı karedeki kamera yönü + A/W/D hareket girdisi yönü (wallJumpInputInfluence = 0.5)
    /// + duvar normali (wallJumpAwayInfluence) + Aim Assist ile Wall Jump hız vektörünü hesaplar.
    /// </summary>
    private Vector3 ComputeWallJumpVelocityVector(Vector3 referenceWallNormal, Collider referenceWallCollider, out Vector3 normalizedJumpDirection)
    {
        Vector3 lookFlat = GetCameraHorizontalForward();
        Vector3 wallNormalFlat = new Vector3(referenceWallNormal.x, 0f, referenceWallNormal.z);
        if (wallNormalFlat.sqrMagnitude < 0.001f)
            wallNormalFlat = -transform.right;
        wallNormalFlat.Normalize();

        // A/W/D hareket girdisi varsa kamera bakış yönüne wallJumpInputInfluence (0.5) oranında karıştır
        Vector3 desiredHorizontalIntent = lookFlat;
        if (motor != null && motor.MoveInputDirection.sqrMagnitude > 0.01f && wallJumpInputInfluence > 0.001f)
        {
            Vector3 inputFlat = new Vector3(motor.MoveInputDirection.x, 0f, motor.MoveInputDirection.z);
            if (inputFlat.sqrMagnitude > 0.01f)
            {
                inputFlat.Normalize();
                // Duvardan dışarı/ileri yönlü girdiyi kamera yönüyle harmanla
                desiredHorizontalIntent = Vector3.Slerp(lookFlat, inputFlat, wallJumpInputInfluence);
                if (desiredHorizontalIntent.sqrMagnitude > 0.001f)
                {
                    desiredHorizontalIntent.Normalize();
                }
                else
                {
                    desiredHorizontalIntent = lookFlat;
                }
            }
        }

        float effectiveAwayWeight = wallJumpAwayInfluence;

        // Eğer hedeflenen yön duvara doğruysa (dot < 0), duvara giren bileşeni çıkar ve minimum uzağa itme uygula
        float dotWithNormal = Vector3.Dot(desiredHorizontalIntent, wallNormalFlat);
        if (dotWithNormal < 0f)
        {
            desiredHorizontalIntent = desiredHorizontalIntent - wallNormalFlat * dotWithNormal;
            if (desiredHorizontalIntent.sqrMagnitude > 0.001f)
            {
                desiredHorizontalIntent.Normalize();
            }
            else
            {
                desiredHorizontalIntent = wallNormalFlat;
            }

            effectiveAwayWeight = Mathf.Max(effectiveAwayWeight, wallJumpMinAwayFromWall);
        }

        Vector3 combinedHorizontalDir = (desiredHorizontalIntent * wallJumpLookInfluence) + (wallNormalFlat * effectiveAwayWeight);
        if (combinedHorizontalDir.sqrMagnitude < 0.001f)
        {
            combinedHorizontalDir = wallNormalFlat;
        }
        combinedHorizontalDir.Normalize();

        if (wallJumpAimAssist)
        {
            combinedHorizontalDir = ApplyWallJumpAimAssist(combinedHorizontalDir, desiredHorizontalIntent, referenceWallCollider);
        }

        float referenceSpeed = Mathf.Max(currentWallSpeed, motor != null ? motor.HorizontalSpeed : 0f);
        float launchHorizontalSpeed = Mathf.Max(referenceSpeed, wallJumpHorizontalSpeed);
        Vector3 finalVelocity = (combinedHorizontalDir * launchHorizontalSpeed) + (Vector3.up * wallJumpUpSpeed);

        normalizedJumpDirection = finalVelocity.sqrMagnitude > 0.001f ? finalVelocity.normalized : Vector3.up;
        return finalVelocity;
    }

    private Vector3 ApplyWallJumpAimAssist(Vector3 baseHorizontalDir, Vector3 lookHorizontalDir, Collider ignoredWallCollider)
    {
        Vector3 origin = transform.position + Vector3.up * raycastHeightOffset;

        float bestAngle = aimAssistConeAngle;
        Vector3 bestTargetDir = baseHorizontalDir;
        bool foundTargetWall = false;

        int fanRayCount = 5;
        for (int i = 0; i < fanRayCount; i++)
        {
            float t = (i / (float)(fanRayCount - 1)) * 2f - 1f;
            float yawOffset = t * aimAssistConeAngle;
            Vector3 testDir = Quaternion.Euler(0f, yawOffset, 0f) * lookHorizontalDir;

            if (Physics.Raycast(origin, testDir, out RaycastHit hit, aimAssistMaxDistance, wallRunnableLayer, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == ignoredWallCollider)
                    continue;

                if (!IsValidWallSurfaceHit(hit, out _))
                    continue;

                float angleToLook = Vector3.Angle(lookHorizontalDir, testDir);
                if (angleToLook <= bestAngle)
                {
                    bestAngle = angleToLook;
                    bestTargetDir = testDir.normalized;
                    foundTargetWall = true;
                }
            }
        }

        if (!foundTargetWall)
        {
            int count = Physics.OverlapSphereNonAlloc(origin, aimAssistMaxDistance, aimAssistColliders, wallRunnableLayer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider col = aimAssistColliders[i];
                if (col == null || col == ignoredWallCollider)
                    continue;

                Vector3 closestPt = col.ClosestPoint(origin);
                Vector3 toWallFlat = new Vector3(closestPt.x - origin.x, 0f, closestPt.z - origin.z);
                float dist = toWallFlat.magnitude;
                if (dist < 0.5f || dist > aimAssistMaxDistance)
                    continue;

                Vector3 dirToWall = toWallFlat / dist;
                float angle = Vector3.Angle(lookHorizontalDir, dirToWall);
                if (angle <= bestAngle)
                {
                    bestAngle = angle;
                    bestTargetDir = dirToWall;
                    foundTargetWall = true;
                }
            }
        }

        if (foundTargetWall)
        {
            return Vector3.Slerp(baseHorizontalDir, bestTargetDir, aimAssistStrength).normalized;
        }

        return baseHorizontalDir;
    }

    private void StopWallRunAndFall(bool enableCoyote)
    {
        if (enableCoyote && activeWallCollider != null)
        {
            wallJumpCoyoteTimer = wallJumpCoyoteTime;
            coyoteWallNormal = activeWallNormal;
            coyoteWallCollider = activeWallCollider;
        }
        else
        {
            wallJumpCoyoteTimer = 0f;
            coyoteWallCollider = null;
        }

        lastWallCollider = activeWallCollider;
        sameWallCooldownTimer = sameWallRegrabCooldown;
        motor.SetState(PlayerMotor.PlayerState.Airborne);

        onWallRunEnd?.Invoke();
        OnWallRunEndCallback?.Invoke();
    }

    /// <summary>
    /// Dal tutunma (Hanging / Transferring) sonrası wall run'ı kısa süre kilitler.
    /// </summary>
    public void TriggerPostBranchCooldown(float customCooldown = -1f)
    {
        postBranchLockTimer = customCooldown > 0f ? customCooldown : sameWallRegrabCooldown;
        isWallLeft = false;
        isWallRight = false;
        lastWallCollider = null;
        wallJumpCoyoteTimer = 0f;
        coyoteWallCollider = null;
    }

    /// <summary>
    /// Karakter yere değdiğinde son duvar kilidini, coyote süresini ve zincir sayacını sıfırlar.
    /// </summary>
    public void ResetLastWallOnGrounded()
    {
        lastWallCollider = null;
        coyoteWallCollider = null;
        wallJumpCoyoteTimer = 0f;
        chainCount = 0;
        chainGraceTimer = 0f;
    }

    /// <summary>
    /// Checkpoint'e ışınlanma (Respawn) sırasında aktif duvar koşusu ve zincirleme sayaçlarını sıfırlar.
    /// </summary>
    public void ForceResetWallRunState()
    {
        hasDetectedWall = false;
        isWallLeft = false;
        isWallRight = false;
        activeWallCollider = null;
        lastWallCollider = null;
        coyoteWallCollider = null;
        wallRunTimer = 0f;
        wallJumpCoyoteTimer = 0f;
        wallJumpBufferTimer = 0f;
        sameWallCooldownTimer = 0f;
        postBranchLockTimer = 0f;
        chainGraceTimer = 0f;
        currentWallSpeed = 0f;
        chainCount = 0;
        wallJumpAvailable = false;
        lastWallJumpBlockReason = "none";
    }

    private Vector3 CalculateWallForwardDirection(Vector3 wallNormal)
    {
        Vector3 horizontalNormal = new Vector3(wallNormal.x, 0f, wallNormal.z).normalized;
        Vector3 crossForward = Vector3.Cross(horizontalNormal, Vector3.up).normalized;

        Vector3 lookDir = GetCameraHorizontalForward();
        if (Vector3.Dot(lookDir, crossForward) < 0f)
        {
            crossForward = -crossForward;
        }

        return crossForward;
    }

    private Vector3 GetCameraHorizontalForward()
    {
        if (playerLook != null)
        {
            return playerLook.GetInstantHorizontalLookDirection();
        }

        if (Camera.main != null)
        {
            Vector3 camFwd = Camera.main.transform.forward;
            Vector3 flat = new Vector3(camFwd.x, 0f, camFwd.z);
            if (flat.sqrMagnitude > 0.001f)
                return flat.normalized;
        }

        return transform.forward;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position + Vector3.up * raycastHeightOffset;
        Vector3 baseForward = Application.isPlaying ? GetCameraHorizontalForward() : transform.forward;

        for (int i = 0; i < DIRECTION_RAY_COUNT; i++)
        {
            float angle = i * DIRECTION_ANGLE_STEP;
            Vector3 rayDir = Quaternion.Euler(0f, angle, 0f) * baseForward;
            Gizmos.color = hasDetectedWall ? new Color(0f, 0.9f, 1f, 0.65f) : new Color(1f, 0.3f, 0.3f, 0.55f);
            Gizmos.DrawLine(origin, origin + rayDir * wallDetectDistance);
        }

        if (lastWallJumpDirection.sqrMagnitude > 0.01f)
        {
            Gizmos.color = Color.green;
            Vector3 arrowStart = origin;
            Vector3 arrowEnd = origin + lastWallJumpDirection.normalized * 2.2f;
            Gizmos.DrawLine(arrowStart, arrowEnd);
            Gizmos.DrawSphere(arrowEnd, 0.09f);
        }
    }
}
