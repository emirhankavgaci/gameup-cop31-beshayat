using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// CANOPY: The Last Run - 8 Yönlü Duvar Algılama, Bakış Yönlü Duvardan Duvara Zıplama (Wall Jump),
/// Aim Assist ve Sınırsız Duvar Zincirleme (Wall Chain) Yöneticisi.
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

    [Header("1b) 8 Yönlü Duvar Algılama Ayarları")]
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

    [Header("1c) Wall Run Başlama ve Zincirleme (Chain) Kuralları")]
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

    [Tooltip("Karakteri duvarda tutan ama hız kaybettirmeyen hafif yapışma hızı.")]
    [SerializeField] private float wallStickPushSpeed = 1.8f;

    [Tooltip("A/D tuşlarıyla duvara hafifçe yaklaşma veya uzaklaşma etki gücü (varsayılan 1.5).")]
    [SerializeField] private float wallRunSteerStrength = 1.5f;

    [Tooltip("S tuşuna veya duvardan uzağa A/D tuşuna bu süre basılı tutulursa duvardan isteğe bağlı ayrılır (sn).")]
    [SerializeField] private float manualDropHoldDuration = 0.25f;

    [Tooltip("Duvarın karşı tarafına doğru uygulanacak kamera yatış (roll/tilt) açısı (12-15 derece).")]
    [Range(8f, 20f)]
    [SerializeField] private float wallRunCameraTiltAngle = 14f;

    [Header("1a) Bakış Yönlü Wall Jump ve Aim Assist")]
    [Tooltip("Wall jump tetikleme tuşu.")]
    [SerializeField] private KeyCode wallJumpKey = KeyCode.Space;

    [Tooltip("Wall jump yatay yön hesabında kamera bakış yönünün ağırlığı (varsayılan 1.0).")]
    [SerializeField] private float wallJumpLookInfluence = 1.0f;

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
    private bool hasDetectedWall;
    private bool isWallRight;
    private bool isWallLeft;
    private RaycastHit closestWallHit;
    private Vector3 activeWallNormal;
    private Collider activeWallCollider;
    private Collider lastWallCollider;

    private float wallRunTimer;
    private float sameWallCooldownTimer;
    private float postBranchLockTimer;
    private float chainGraceTimer;
    private float manualDropTimer;
    private float currentWallSpeed;
    private int chainCount;
    private Vector3 lastWallJumpDirection = Vector3.forward;

    // Debug HUD ve dış erişim özellikleri
    public bool IsWallRight => isWallRight;
    public bool IsWallLeft => isWallLeft;
    public float WallRunTimer => wallRunTimer;
    public int ChainCount => chainCount;
    public string ActiveWallName => (motor != null && motor.CurrentState == PlayerMotor.PlayerState.WallRunning && activeWallCollider != null)
        ? activeWallCollider.name
        : "YOK";
    public string LastWallColliderName => lastWallCollider != null ? lastWallCollider.name : "YOK";
    public Vector3 LastWallJumpDirection => lastWallJumpDirection;

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

    private void Update()
    {
        float dt = Time.deltaTime;

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
    }

    /// <summary>
    /// 1b) Oyuncu etrafında yatay düzlemde 45 derece aralıklı 8 yönlü Raycast atar.
    /// WallRunnable katmanında ve 80-100 derece normal açısına sahip en yakın geçerli duvarı seçer.
    /// </summary>
    private bool DetectClosestValidWall(bool ignoreLastWallRule, out RaycastHit bestHit)
    {
        bestHit = default;
        hasDetectedWall = false;
        isWallRight = false;
        isWallLeft = false;

        Vector3 origin = transform.position + Vector3.up * raycastHeightOffset;
        Vector3 baseForward = GetCameraHorizontalForward();

        float minDistance = float.MaxValue;
        bool foundAny = false;

        for (int i = 0; i < DIRECTION_RAY_COUNT; i++)
        {
            float angle = i * DIRECTION_ANGLE_STEP;
            Vector3 rayDir = Quaternion.Euler(0f, angle, 0f) * baseForward;

            if (Physics.Raycast(origin, rayDir, out RaycastHit hit, wallDetectDistance, wallRunnableLayer, QueryTriggerInteraction.Ignore))
            {
                float angleWithUp = Vector3.Angle(hit.normal, Vector3.up);
                bool isVerticalWall = (angleWithUp >= minWallNormalAngle && angleWithUp <= maxWallNormalAngle);
                if (!isVerticalWall)
                    continue;

                // Aynı duvar (lastWallCollider) kontrolü: Farklı duvarlar anında kabul edilir,
                // yalnızca lastWallCollider engellenir.
                if (!ignoreLastWallRule && lastWallCollider != null && hit.collider == lastWallCollider)
                {
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
            UpdateWallSideRelativeToCamera(bestHit.normal);
        }

        return foundAny;
    }

    /// <summary>
    /// Duvarın oyuncunun/kameranın sağında mı yoksa solunda mı olduğunu belirleyerek kamera roll yönünü ayarlar.
    /// </summary>
    private void UpdateWallSideRelativeToCamera(Vector3 wallNormal)
    {
        Vector3 camForward = GetCameraHorizontalForward();
        Vector3 camRight = Vector3.Cross(Vector3.up, camForward).normalized;
        Vector3 toWallDir = - new Vector3(wallNormal.x, 0f, wallNormal.z).normalized;

        float rightDot = Vector3.Dot(camRight, toWallDir);
        isWallRight = rightDot >= 0f;
        isWallLeft = !isWallRight;
    }

    /// <summary>
    /// 1c) Oyuncunun havada olup olmadığını, hız eşiğini (3.5 m/s) ve 8 yönlü duvar temasını kontrol eder.
    /// </summary>
    public bool CanStartWallRun()
    {
        if (motor == null || motor.IsGrounded || postBranchLockTimer > 0f)
            return false;

        if (motor.CurrentState == PlayerMotor.PlayerState.Hanging ||
            motor.CurrentState == PlayerMotor.PlayerState.Transferring ||
            motor.CurrentState == PlayerMotor.PlayerState.Sliding)
        {
            return false;
        }

        if (motor.HorizontalSpeed < minWallRunSpeed)
            return false;

        // Wall jump sonrası ilk 0.6 sn (wallChainGraceTime) içinde veya requireForwardKeyOutsideGrace kapalıysa W şartı aranmaz
        if (requireForwardKeyOutsideGrace && chainGraceTimer <= 0f && motor.VerticalInput <= 0.05f)
            return false;

        return DetectClosestValidWall(ignoreLastWallRule: false, out _);
    }

    /// <summary>
    /// Wall Run durumunu başlatır, zincirleme (chain) sayısını ve süresini hesaplar.
    /// </summary>
    public void StartWallRun()
    {
        activeWallNormal = closestWallHit.normal;
        activeWallCollider = closestWallHit.collider;

        // Yeni bir duvara yapıştığımızda önceki duvar kilidi sıfırlanır (şimdi aktif duvar bu oldu)
        if (lastWallCollider != null && activeWallCollider != lastWallCollider)
        {
            lastWallCollider = null;
        }

        // Zincirleme süresi hesabı: Her art arda duvarda 0.15 sn azalır, minimum 1.2 sn olur
        float calculatedDuration = maxWallRunDuration - (chainCount * chainTimePenalty);
        wallRunTimer = Mathf.Max(minChainWallRunDuration, calculatedDuration);
        chainCount++;

        manualDropTimer = 0f;
        currentWallSpeed = Mathf.Max(motor.HorizontalSpeed, motor.WalkSpeed);

        Vector3 wallForward = CalculateWallForwardDirection(activeWallNormal);
        float dampedVerticalY = motor.Velocity.y > 0f
            ? motor.Velocity.y * initialVerticalVelocityDamping
            : 0f;

        UpdateWallSideRelativeToCamera(activeWallNormal);

        // Anlık wall jump önizleme yönünü güncelle
        lastWallJumpDirection = ComputeWallJumpVelocityVector(out _).normalized;

        motor.SetVelocity(wallForward * currentWallSpeed + Vector3.up * dampedVerticalY);
        motor.SetState(PlayerMotor.PlayerState.WallRunning);

        onWallRunStart?.Invoke();
        OnWallRunStartCallback?.Invoke();
    }

    /// <summary>
    /// 1d) Her karede Wall Run hareketini, mouse bakışına göre koşu yönünü, A/D steer etkisini ve Space ile Wall Jump'ı işler.
    /// </summary>
    public void UpdateWallRun(float dt)
    {
        // Space tuşuna basıldıysa gecikmesiz anlık kamera yönüyle hemen Wall Jump yap!
        if (Input.GetKeyDown(wallJumpKey) || motor.HasBufferedJump)
        {
            ExecuteWallJump();
            return;
        }

        // Aktif duvarı veya köşe dönüldüyse yeni duvarı kontrol et
        bool stillOnWall = DetectClosestValidWall(ignoreLastWallRule: true, out RaycastHit currentHit);
        wallRunTimer -= dt;

        if (!stillOnWall || motor.IsGrounded || wallRunTimer <= 0f)
        {
            StopWallRunAndFall();
            return;
        }

        activeWallNormal = currentHit.normal;
        activeWallCollider = currentHit.collider;
        UpdateWallSideRelativeToCamera(activeWallNormal);

        // Opsiyonel manuel bırakma: S basılı tutulursa veya duvardan uzağa A/D basılı tutulursa
        float awayInput = isWallRight ? -motor.HorizontalInput : motor.HorizontalInput;
        bool wantsToDrop = (motor.VerticalInput < -0.5f) || (awayInput > 0.6f && motor.VerticalInput < 0.05f);
        if (wantsToDrop)
        {
            manualDropTimer += dt;
            if (manualDropTimer >= manualDropHoldDuration)
            {
                StopWallRunAndFall();
                return;
            }
        }
        else
        {
            manualDropTimer = 0f;
        }

        // 1d) Oyuncunun mouse bakışı duvar boyunca koşu yönünü belirler
        Vector3 wallForward = CalculateWallForwardDirection(activeWallNormal);
        currentWallSpeed = Mathf.MoveTowards(currentWallSpeed, maxWallRunSpeed, wallRunSpeedBoostRate * dt);

        // Azaltılmış yerçekimi (%10)
        float reducedGravity = motor.Gravity * wallRunGravityMultiplier;
        float newVerticalVelocity = motor.Velocity.y + reducedGravity * dt;

        // A/D ile duvara hafif yaklaşma / uzaklaşma etkisi (wallRunSteerStrength)
        Vector3 horizontalNormal = new Vector3(activeWallNormal.x, 0f, activeWallNormal.z).normalized;
        float steerOffset = awayInput * wallRunSteerStrength;
        float netStickPush = Mathf.Max(0.2f, wallStickPushSpeed - steerOffset);
        Vector3 stickVelocity = -horizontalNormal * netStickPush;

        Vector3 finalWallVelocity = (wallForward * currentWallSpeed) + (Vector3.up * newVerticalVelocity);

        // Debug ve Gizmos için o anki olası wall jump yönünü güncel tut
        lastWallJumpDirection = ComputeWallJumpVelocityVector(out _).normalized;

        motor.SetVelocity(finalWallVelocity);
        if (motor.Controller.enabled)
        {
            motor.Controller.Move((finalWallVelocity + stickVelocity) * dt);
        }
    }

    /// <summary>
    /// 1a) Space'e basıldığı karedeki anlık kamera yönünü ve duvar normalini (ve açıksa Aim Assist'i) kullanarak
    /// oyuncuyu seçtiği yöne fırlatır.
    /// </summary>
    private void ExecuteWallJump()
    {
        Vector3 jumpVelocity = ComputeWallJumpVelocityVector(out Vector3 jumpDirNormalized);
        lastWallJumpDirection = jumpDirNormalized;

        // Sadece bu duvarın collider'ını kilitle; karşı duvar veya başka duvarlar 0 sn gecikmeyle hemen tutunabilir
        lastWallCollider = activeWallCollider;
        sameWallCooldownTimer = sameWallRegrabCooldown;
        chainGraceTimer = wallChainGraceTime;

        motor.ConsumeJumpBuffer();
        motor.SetState(PlayerMotor.PlayerState.Airborne);
        motor.SetVelocity(jumpVelocity);

        onWallJump?.Invoke();
        OnWallJumpCallback?.Invoke(jumpDirNormalized);
        onWallRunEnd?.Invoke();
        OnWallRunEndCallback?.Invoke();
    }

    /// <summary>
    /// 1a) Bakış yönü + duvar normali + isteğe bağlı Aim Assist ile Wall Jump hız vektörünü hesaplar.
    /// </summary>
    private Vector3 ComputeWallJumpVelocityVector(out Vector3 normalizedJumpDirection)
    {
        Vector3 lookFlat = GetCameraHorizontalForward();
        Vector3 wallNormalFlat = new Vector3(activeWallNormal.x, 0f, activeWallNormal.z);
        if (wallNormalFlat.sqrMagnitude < 0.001f)
            wallNormalFlat = -transform.right;
        wallNormalFlat.Normalize();

        float effectiveAwayWeight = wallJumpAwayInfluence;

        // Eğer bakış yönü duvara doğruysa (lookFlat · wallNormalFlat < 0), duvara giren bileşeni çıkar
        // ve minimum uzağa itme (wallJumpMinAwayFromWall) uygula ki oyuncu duvara geri yapışmasın.
        float dotWithNormal = Vector3.Dot(lookFlat, wallNormalFlat);
        if (dotWithNormal < 0f)
        {
            lookFlat = (lookFlat - wallNormalFlat * dotWithNormal);
            if (lookFlat.sqrMagnitude > 0.001f)
            {
                lookFlat.Normalize();
            }
            else
            {
                lookFlat = wallNormalFlat;
            }

            effectiveAwayWeight = Mathf.Max(effectiveAwayWeight, wallJumpMinAwayFromWall);
        }

        Vector3 combinedHorizontalDir = (lookFlat * wallJumpLookInfluence) + (wallNormalFlat * effectiveAwayWeight);
        if (combinedHorizontalDir.sqrMagnitude < 0.001f)
        {
            combinedHorizontalDir = wallNormalFlat;
        }
        combinedHorizontalDir.Normalize();

        // İsteğe bağlı Aim Assist: Baktığımız yönde 20 derece koni ve 12m menzilde başka bir WallRunnable duvar varsa
        // yatay fırlama yönünü %30 o duvara doğru yumuşat
        if (wallJumpAimAssist)
        {
            combinedHorizontalDir = ApplyWallJumpAimAssist(combinedHorizontalDir, lookFlat);
        }

        // Momentum korunumu: max(mevcut duvar koşusu hızı, wallJumpHorizontalSpeed)
        float launchHorizontalSpeed = Mathf.Max(currentWallSpeed, wallJumpHorizontalSpeed);
        Vector3 finalVelocity = (combinedHorizontalDir * launchHorizontalSpeed) + (Vector3.up * wallJumpUpSpeed);

        normalizedJumpDirection = finalVelocity.sqrMagnitude > 0.001f ? finalVelocity.normalized : Vector3.up;
        return finalVelocity;
    }

    /// <summary>
    /// Oyuncunun baktığı yönde 20 derece koni içinde 12 m menzilde başka bir "WallRunnable" duvar arar
    /// ve bulursa yönü %30 o duvara doğru yaklaştırır.
    /// </summary>
    private Vector3 ApplyWallJumpAimAssist(Vector3 baseHorizontalDir, Vector3 lookHorizontalDir)
    {
        Vector3 origin = transform.position + Vector3.up * raycastHeightOffset;

        // Önce koni içinde 5 yelpaze ışını ile hızlı ve keskin yüzey araması yap
        float bestAngle = aimAssistConeAngle;
        Vector3 bestTargetDir = baseHorizontalDir;
        bool foundTargetWall = false;

        int fanRayCount = 5;
        for (int i = 0; i < fanRayCount; i++)
        {
            float t = (i / (float)(fanRayCount - 1)) * 2f - 1f; // -1 .. +1
            float yawOffset = t * aimAssistConeAngle;
            Vector3 testDir = Quaternion.Euler(0f, yawOffset, 0f) * lookHorizontalDir;

            if (Physics.Raycast(origin, testDir, out RaycastHit hit, aimAssistMaxDistance, wallRunnableLayer, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == activeWallCollider)
                    continue;

                float normalAngle = Vector3.Angle(hit.normal, Vector3.up);
                if (normalAngle < minWallNormalAngle || normalAngle > maxWallNormalAngle)
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

        // Eğer yelpaze ışınları ıskaladıysa OverlapSphere ile yakındaki duvarların en yakın noktasını kontrol et
        if (!foundTargetWall)
        {
            int count = Physics.OverlapSphereNonAlloc(origin, aimAssistMaxDistance, aimAssistColliders, wallRunnableLayer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider col = aimAssistColliders[i];
                if (col == null || col == activeWallCollider)
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

    private void StopWallRunAndFall()
    {
        lastWallCollider = activeWallCollider;
        sameWallCooldownTimer = sameWallRegrabCooldown;
        motor.SetState(PlayerMotor.PlayerState.Airborne);

        onWallRunEnd?.Invoke();
        OnWallRunEndCallback?.Invoke();
    }

    /// <summary>
    /// Dal tutunma (Hanging / Transferring) sonrası wall run'ı 0.2 sn kilitler.
    /// </summary>
    public void TriggerPostBranchCooldown(float customCooldown = -1f)
    {
        postBranchLockTimer = customCooldown > 0f ? customCooldown : sameWallRegrabCooldown;
        isWallLeft = false;
        isWallRight = false;
        lastWallCollider = null;
    }

    /// <summary>
    /// Karakter yere değdiğinde son duvar kilidini ve zincir sayacını sıfırlar.
    /// </summary>
    public void ResetLastWallOnGrounded()
    {
        lastWallCollider = null;
        chainCount = 0;
        chainGraceTimer = 0f;
    }

    /// <summary>
    /// 1d) Duvara paralel eksenin hangi yönünün oyuncunun anlık mouse bakışına daha yakın olduğunu (dot product) bulur.
    /// </summary>
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

    /// <summary>
    /// PlayerLook üzerinden gecikmesiz anlık yatay kamera bakış yönünü okur.
    /// </summary>
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

        // 8 yönlü duvar algılama raycast'lerini çiz
        for (int i = 0; i < DIRECTION_RAY_COUNT; i++)
        {
            float angle = i * DIRECTION_ANGLE_STEP;
            Vector3 rayDir = Quaternion.Euler(0f, angle, 0f) * baseForward;
            Gizmos.color = hasDetectedWall ? new Color(0f, 0.9f, 1f, 0.65f) : new Color(1f, 0.3f, 0.3f, 0.55f);
            Gizmos.DrawLine(origin, origin + rayDir * wallDetectDistance);
        }

        // Oyuncudan çıkan yeşil ok: Wall Jump fırlama yönü
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
