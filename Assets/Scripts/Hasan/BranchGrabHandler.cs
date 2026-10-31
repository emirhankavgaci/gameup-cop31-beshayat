using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// CANOPY: The Last Run - "Maymun Simülatörü" Dal Tutunma, Dal Boyunca Kayma, Gerçek Sarkaç Sallanması,
/// Savrularak Fırlama (Perfect Release destekli) ve Sallanırken de Çalışan Parabolik Dallar Arası Geçiş Yöneticisi.
/// W ve S yalnızca sarkaç torku uygular (S ile bırakma kaldırılmıştır); dal yalnızca Space veya Left Ctrl ile bırakılır.
/// </summary>
public class BranchGrabHandler : MonoBehaviour
{
    public enum BranchReleaseReason
    {
        None,
        Space,
        LeftCtrl,
        Transfer
    }

    [Header("Temel Referanslar")]
    [Tooltip("Merkezi karakter hareket ve state machine yöneticisi.")]
    [SerializeField] private PlayerMotor motor;

    [Tooltip("Kameranın bakış yönünü almak için kullanılan Transform (boşsa MainCamera bulunur).")]
    [SerializeField] private Transform cameraTransform;

    [Tooltip("Hedef dal üzerinde '[E] Dala geç' işaretçisini gösteren UI bileşeni.")]
    [SerializeField] private GrabPromptUI grabPromptUI;

    [Header("El Noktası ve Otomatik Tutunma (E Gerekmez)")]
    [Tooltip("Oyuncu gövde merkezinden (transform.position) el yakalama noktasına olan dikey mesafe (kafa üstü).")]
    [SerializeField] private float handPointVerticalOffset = 0.85f;

    [Tooltip("El noktası etrafında otomatik dal algılama küresi yarıçapı (varsayılan 0.6 m).")]
    [SerializeField] private float autoGrabSphereRadius = 0.6f;

    [Tooltip("Dalların bulunduğu katman ('Branch' layer'ı).")]
    [SerializeField] private LayerMask branchLayerMask;

    [Tooltip("Dal bırakıldıktan sonra tekrar aynı dala tutunabilmek için geçmesi gereken süre (varsayılan 0.35 sn).")]
    [SerializeField] private float regrabCooldownDuration = 0.35f;

    [Tooltip("Basılı tutulduğunda otomatik tutunmayı iptal eden ve daldayken bırakıp düşüren tek bırakma tuşu.")]
    [SerializeField] private KeyCode grabCancelAndDropKey = KeyCode.LeftControl;

    [Tooltip("Tutunma anında el noktasının dala yumuşakça çekilme (SmoothDamp) süresi (varsayılan 0.1 sn).")]
    [SerializeField] private float handSnapSmoothTime = 0.10f;

    [Tooltip("Tutunma anındaki giriş hızının başlangıç sarkaç açısal hızına dönüşüm çarpanı (varsayılan 0.5).")]
    [SerializeField] private float entrySwingMultiplier = 0.50f;

    [Tooltip("Tutunma anında oluşabilecek maksimum başlangıç açısal hızı üst sınırı (varsayılan 3 rad/s).")]
    [SerializeField] private float maxEntryAngularVelocity = 3.0f;

    [Header("1) Dal Boyunca Kayma (A / D)")]
    [Tooltip("Dal ekseni boyunca maksimum sağa/sola kayma hızı (varsayılan 3.5 m/s).")]
    [SerializeField] private float branchSlideSpeed = 3.5f;

    [Tooltip("Dal üzerinde kaymaya başlarken ivmelenme hızı (m/s²).")]
    [SerializeField] private float slideAcceleration = 18f;

    [Tooltip("A/D bırakıldığında dal üzerinde durma yavaşlaması (m/s²).")]
    [SerializeField] private float slideDeceleration = 24f;

    [Header("2) Sarkaç Sallanması (W / S - Bırakma Yapmaz)")]
    [Tooltip("Sarkaç uzunluğu: El noktasından oyuncu gövde merkezine olan mesafe (varsayılan 1.6 m).")]
    [SerializeField] private float pendulumLength = 1.6f;

    [Tooltip("Sarkaç denklemi için yerçekimi ivmesi büyüklüğü (m/s², pozitif değer).")]
    [SerializeField] private float pendulumGravity = 26f;

    [Tooltip("W ve S tuşlarıyla sarkaca uygulanan açısal tork ivmesi (rad/s²).")]
    [SerializeField] private float swingInputTorque = 9.5f;

    [Tooltip("W-S geçişlerinde sert sıçrama olmaması için tork girişi yumuşatma süresi (varsayılan 0.08 sn).")]
    [SerializeField] private float swingInputSmoothing = 0.08f;

    [Tooltip("Sarkaç hız yönüyle aynı yönde tuşa basıldığında (rezonans) uygulanan pozitif tork çarpanı.")]
    [SerializeField] private float resonanceTorqueMultiplier = 1.30f;

    [Tooltip("W/S girişi YOKKEN pasif sallanmayı hızlıca sakinleştiren güçlü sönümleme (idleDamping).")]
    [SerializeField] private float idleDamping = 1.85f;

    [Tooltip("W/S girişi VARKEN aktif sallanmanın rahatça büyümesini sağlayan zayıf sönümleme (activeDamping).")]
    [SerializeField] private float activeDamping = 0.30f;

    [Tooltip("Maksimum sallanma açısı sınırı (varsayılan 80 derece). Sınıra ulaşınca dal bırakılmaz.")]
    [Range(30f, 88f)]
    [SerializeField] private float maxSwingAngleDegrees = 80f;

    [Tooltip("Maksimum açı sınırına yaklaşırken yumuşak fren uygulanacak son açı bölgesi (varsayılan 10 derece).")]
    [SerializeField] private float limitSoftBrakeZoneDegrees = 10f;

    [Tooltip("Maksimum açıya çarpıldığında açısal hızın hafif geri sekme çarpanı (varsayılan 0.2).")]
    [Range(0f, 0.5f)]
    [SerializeField] private float swingLimitBounce = 0.20f;

    [Tooltip("Sallanırken kameraya eklenecek maksimum dikey bakış (pitch) offset açısı (derece).")]
    [SerializeField] private float maxSwingCameraPitchOffset = 10f;

    [Tooltip("Sallanırken kameraya eklenecek maksimum yatış (roll) offset açısı (derece).")]
    [SerializeField] private float maxSwingCameraRollOffset = 6f;

    [Header("3) Savrularak Fırlama (Space)")]
    [Tooltip("Daldan savrularak fırlama tuşu.")]
    [SerializeField] private KeyCode swingLaunchKey = KeyCode.Space;

    [Tooltip("Fırlama anında kameranın baktığı yatay yönde eklenen ileri impuls (varsayılan 8 m/s).")]
    [SerializeField] private float launchForwardImpulse = 8f;

    [Tooltip("Fırlama anında eklenen yukarı yönlü impuls (varsayılan 5 m/s).")]
    [SerializeField] private float launchUpwardImpulse = 5f;

    [Tooltip("Sarkaç teğetsel hızının (angularVelocity * length) fırlama hızına aktarım çarpanı.")]
    [SerializeField] private float tangentialVelocityLaunchScale = 1.15f;

    [Tooltip("İleri savrulmanın zirvesine yakınken Space'e basıldığında verilen 'Perfect Release' hız çarpanı (1.2 = +%20).")]
    [SerializeField] private float perfectReleaseBonusMultiplier = 1.20f;

    [Tooltip("Perfect Release kazanmak için gereken minimum ileri sarkaç açısı (derece).")]
    [SerializeField] private float perfectReleaseMinAngleDegrees = 32f;

    [Header("4) Dallar Arası Geçiş (E Tuşu - Sallanırken Aktif)")]
    [Tooltip("Dallar arası geçişi başlatan tuş.")]
    [SerializeField] private KeyCode branchTransferKey = KeyCode.E;

    [Tooltip("W veya S bırakıldıktan sonra '[E] Dala geç' göstergesinin geri gelmesi için bekleme süresi (varsayılan 0.15 sn).")]
    [SerializeField] private float transferPromptDelay = 0.15f;

    [Tooltip("Yakındaki aday dalları aramak için el noktası etrafındaki OverlapSphere yarıçapı (varsayılan 6 m).")]
    [SerializeField] private float transferSearchRadius = 6.0f;

    [Tooltip("Aday dalın seçilebilmesi için kamera bakış yönü ile arasındaki minimum Dot Product değeri.")]
    [Range(0.05f, 0.85f)]
    [SerializeField] private float minTransferLookDot = 0.25f;

    [Tooltip("Aday dal skorlamasında kamera bakış yönüne yakınlık ağırlığı (varsayılan 0.6).")]
    [SerializeField] private float transferDirectionWeight = 0.60f;

    [Tooltip("Aday dal skorlamasında mesafe yakınlığı ağırlığı (varsayılan 0.4).")]
    [SerializeField] private float transferDistanceWeight = 0.40f;

    [Tooltip("Kısa mesafeli dal geçişi minimum süresi (varsayılan 0.35 sn).")]
    [SerializeField] private float minTransferDuration = 0.35f;

    [Tooltip("Uzun mesafeli (6m) dal geçişi maksimum süresi (varsayılan 0.50 sn).")]
    [SerializeField] private float maxTransferDuration = 0.50f;

    [Tooltip("Parabolik yay yüksekliğinin geçiş mesafesine oranı (0.25 = mesafenin %25'i).")]
    [SerializeField] private float transferArcHeightRatio = 0.25f;

    [Tooltip("Geçiş sırasında kameranın hedef dala doğru yumuşak dönüş hızı.")]
    [SerializeField] private float transferCameraTurnSpeed = 6.5f;

    [Tooltip("Hedef dala varıldığında oluşan hafif başlangıç sallanma açısal hızı (rad/s).")]
    [SerializeField] private float transferArrivalAngularVelocity = 0.9f;

    [Header("Olaylar (Ses, Animasyon ve UI Bağlantısı İçin)")]
    [Tooltip("Oyuncu bir dala tutunduğunda tetiklenen UnityEvent (Inspector'dan ses/efekt bağlanabilir).")]
    [SerializeField] private UnityEvent<BranchData> onBranchGrabbed;

    public event Action<BranchData> OnBranchGrabbedCallback;
    public event Action<bool, BranchData> OnTransferTargetChanged;

    // Dahili çalışma değişkenleri - Tutunma ve Dal
    private readonly Collider[] overlapBuffer = new Collider[24];
    private BranchData currentBranch;
    private BranchData lastReleasedBranch;
    private float lastReleaseTime = -999f;
    private float regrabCooldownTimer;
    private BranchReleaseReason lastReleaseReason = BranchReleaseReason.None;

    // El noktası ve Dal ekseni kayma değişkenleri
    private float currentBranchT;
    private float currentSlideSpeed;
    private Vector3 currentHandWorldPos;
    private Vector3 handSnapVelocity;
    private float handSnapTimer;

    // Sarkaç (Pendulum) değişkenleri
    private float swingAngle; // Radyan
    private float swingAngularVelocity; // Rad/s
    private Vector3 swingForwardDir = Vector3.forward;
    private float smoothedSwingInput;
    private float swingInputSmoothVelocity;
    private bool lastLaunchWasPerfect;

    // Dallar arası geçiş (Transfer) değişkenleri
    private BranchData currentTransferTarget;
    private Vector3 currentTransferTargetPoint;
    private float candidateTransferScore;
    private float candidateTransferDistance;
    private float swingInputReleaseDelayTimer;
    private bool isTransferBlockedBySwingInput;
    private bool previousHasTransferTarget;

    private BranchData activeTransferTargetBranch;
    private Vector3 transferStartBodyPos;
    private Vector3 transferEndHandPos;
    private Vector3 transferEndBodyPos;
    private float transferTargetT;
    private float transferTotalDuration;
    private float transferElapsed;
    private float transferArcHeight;
    private float currentTransferSpeed;

    // CrosshairUI, GrabPromptUI ve Debug HUD için dış erişim özellikleri
    public BranchData CurrentBranch => currentBranch;
    public float CurrentBranchT => currentBranchT;
    public float SwingAngleRadians => swingAngle;
    public float SwingAngleDegrees => swingAngle * Mathf.Rad2Deg;
    public float SwingAngularVelocity => swingAngularVelocity;
    public float CurrentTangentialSpeed => Mathf.Abs(swingAngularVelocity) * pendulumLength;
    public float CurrentTransferSpeed => currentTransferSpeed;
    public BranchReleaseReason LastReleaseReason => lastReleaseReason;

    /// <summary>Menzilde geçilebilecek geçerli bir dal olup olmadığını döndürür (W/S basılı değilken).</summary>
    public bool HasTransferTarget => currentTransferTarget != null && !isTransferBlockedBySwingInput;

    /// <summary>Şu an seçili olan hedef geçiş dalını döndürür.</summary>
    public BranchData CurrentTransferTarget => currentTransferTarget;

    /// <summary>Eski isimlendirmeyle uyumluluk için CurrentTransferTarget referansı.</summary>
    public BranchData CandidateTransferBranch => currentTransferTarget;

    /// <summary>Hedef dal üzerindeki en yakın geçiş noktasının dünya koordinatını döndürür.</summary>
    public Vector3 CurrentTransferTargetPoint => currentTransferTargetPoint;

    public float CandidateTransferDistance => candidateTransferDistance;
    public float CandidateTransferScore => candidateTransferScore;
    public bool IsTransferBlockedBySwingInput => isTransferBlockedBySwingInput;
    public bool LastLaunchWasPerfect => lastLaunchWasPerfect;

    private void Awake()
    {
        if (motor == null)
            motor = GetComponent<PlayerMotor>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (grabPromptUI == null)
            grabPromptUI = FindObjectOfType<GrabPromptUI>();

        if (branchLayerMask.value == 0)
        {
            int branchLayer = LayerMask.NameToLayer("Branch");
            int grabbableLayer = LayerMask.NameToLayer("Grabbable");
            int mask = 0;
            if (branchLayer != -1) mask |= (1 << branchLayer);
            if (grabbableLayer != -1) mask |= (1 << grabbableLayer);
            branchLayerMask = mask != 0 ? mask : ~0;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        if (regrabCooldownTimer > 0f)
        {
            regrabCooldownTimer -= dt;
        }

        // Havadayken de CrosshairUI'ın hedef dal gördüğünde büyüyüp yeşillenebilmesi için
        // Hanging ve Transferring dışındayken pasif hedef taraması yap
        if (motor != null &&
            motor.CurrentState != PlayerMotor.PlayerState.Hanging &&
            motor.CurrentState != PlayerMotor.PlayerState.Transferring)
        {
            if (motor.CurrentState == PlayerMotor.PlayerState.Airborne ||
                motor.CurrentState == PlayerMotor.PlayerState.WallRunning)
            {
                isTransferBlockedBySwingInput = false;
                ScanForBestTransferBranch(GetBodyHandSearchOrigin(), showUiPrompt: false);
            }
            else
            {
                ClearTransferCandidateState();
            }
        }
    }

    public Vector3 GetBodyHandSearchOrigin()
    {
        return transform.position + Vector3.up * handPointVerticalOffset;
    }

    public bool TryAutoGrabBranch()
    {
        if (motor == null || motor.IsGrounded)
            return false;

        if (motor.CurrentState == PlayerMotor.PlayerState.Sliding)
            return false;

        // Left Ctrl basılıysa oyuncu bilerek tutunmadan geçmek istiyordur
        if (Input.GetKey(grabCancelAndDropKey))
            return false;

        Vector3 handOrigin = GetBodyHandSearchOrigin();
        int hitCount = Physics.OverlapSphereNonAlloc(
            handOrigin,
            autoGrabSphereRadius,
            overlapBuffer,
            branchLayerMask,
            QueryTriggerInteraction.Collide
        );

        BranchData bestBranch = null;
        float closestDistSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null || col.transform == transform)
                continue;

            BranchData branch = GetOrCreateBranchData(col);
            if (branch == null)
                continue;

            bool isRecentlyReleasedSameBranch = (branch == lastReleasedBranch) &&
                                                (Time.time - lastReleaseTime < regrabCooldownDuration);
            if (isRecentlyReleasedSameBranch)
                continue;

            if (regrabCooldownTimer > 0f && branch == lastReleasedBranch)
                continue;

            Vector3 closestPt = branch.GetClosestPointOnBranch(handOrigin);
            float distSqr = (closestPt - handOrigin).sqrMagnitude;

            if (distSqr <= autoGrabSphereRadius * autoGrabSphereRadius && distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                bestBranch = branch;
            }
        }

        if (bestBranch != null)
        {
            EngageBranch(bestBranch, motor.Velocity, isFromTransfer: false);
            return true;
        }

        return false;
    }

    private void EngageBranch(BranchData branch, Vector3 entryVelocity, bool isFromTransfer)
    {
        currentBranch = branch;
        Vector3 initialHandOrigin = GetBodyHandSearchOrigin();

        currentBranchT = currentBranch.GetT(initialHandOrigin);
        Vector3 targetPointOnBranch = currentBranch.GetPointAtT(currentBranchT);

        currentHandWorldPos = isFromTransfer ? targetPointOnBranch : initialHandOrigin;
        handSnapVelocity = Vector3.zero;
        handSnapTimer = 0f;
        currentSlideSpeed = 0f;
        smoothedSwingInput = 0f;
        swingInputSmoothVelocity = 0f;
        swingInputReleaseDelayTimer = 0f;
        isTransferBlockedBySwingInput = false;

        swingForwardDir = ComputeSwingForwardDirection(currentBranch.Axis, entryVelocity);

        if (isFromTransfer)
        {
            swingAngle = 0f;
            swingAngularVelocity = Mathf.Clamp(transferArrivalAngularVelocity, -maxEntryAngularVelocity, maxEntryAngularVelocity);
        }
        else
        {
            swingAngle = 0f;
            float forwardEntrySpeed = Vector3.Dot(entryVelocity, swingForwardDir);
            float rawAngularVel = (forwardEntrySpeed / Mathf.Max(0.2f, pendulumLength)) * entrySwingMultiplier;
            swingAngularVelocity = Mathf.Clamp(rawAngularVel, -maxEntryAngularVelocity, maxEntryAngularVelocity);
        }

        motor.SetVelocity(Vector3.zero);
        motor.ConsumeJumpBuffer();
        motor.SetState(PlayerMotor.PlayerState.Hanging);

        onBranchGrabbed?.Invoke(currentBranch);
        OnBranchGrabbedCallback?.Invoke(currentBranch);
    }

    /// <summary>
    /// Hanging durumundaki 3 alt modeli (1: Kayma, 2: Sarkaç, 3: Fırlama) ve Dallar Arası Geçişi yürütür.
    /// Dal YALNIZCA Left Ctrl (Drop) veya Space (Launch) ile bırakılır; S tuşu asla bırakma yapmaz!
    /// </summary>
    public void UpdateHangingState(float dt)
    {
        if (currentBranch == null)
        {
            DropFromBranch(BranchReleaseReason.LeftCtrl);
            return;
        }

        // 0. Dalı bırakıp düşme: SADECE Left Ctrl tuşu ile! (S tuşu ile bırakma tamamen kaldırılmıştır)
        if (Input.GetKeyDown(grabCancelAndDropKey))
        {
            DropFromBranch(BranchReleaseReason.LeftCtrl);
            return;
        }

        // 1. Savrularak Fırlama: SADECE Space tuşu ile!
        if (Input.GetKeyDown(swingLaunchKey))
        {
            ExecuteSwingLaunch();
            return;
        }

        // 2. ALT MODEL 1: Dal boyunca kayma (A / D) -> Uçlarda (t=0 veya 1) asla bırakma yapmaz
        UpdateBranchSliding(dt);

        // 3. ALT MODEL 2: Sarkaç sallanması (W / S) -> Yumuşatılmış tork ve yumuşak açı sınırı
        UpdatePendulumSwing(dt);

        // 4. El noktasını dal çizgisi üzerindeki hedef noktaya 0.1 sn içinde SmoothDamp ile oturt, sonra kilitle
        Vector3 idealBranchHandPoint = currentBranch.GetPointAtT(currentBranchT);
        handSnapTimer += dt;

        if (handSnapTimer < handSnapSmoothTime * 2.5f)
        {
            currentHandWorldPos = Vector3.SmoothDamp(
                currentHandWorldPos,
                idealBranchHandPoint,
                ref handSnapVelocity,
                handSnapSmoothTime,
                Mathf.Infinity,
                dt
            );
        }
        else
        {
            currentHandWorldPos = idealBranchHandPoint;
        }

        // 5. Gövde pozisyonunu sarkaç denkleminden doğrudan hesapla ve transform.position'a uygula
        Vector3 pendulumOffset = (Mathf.Sin(swingAngle) * swingForwardDir + (-Mathf.Cos(swingAngle)) * Vector3.up) * pendulumLength;
        transform.position = currentHandWorldPos + pendulumOffset;

        // 6. Sallanırken kamera pitch ve hafif roll offsetlerini yumuşakça güncelle
        UpdateSwingCameraFeedback();

        // 7. Dallar Arası Geçiş (E Tuşu): Sallanma sırasında da aktiftir!
        UpdateBranchTransferInHanging(dt);

        if (HasTransferTarget && Input.GetKeyDown(branchTransferKey))
        {
            StartBranchTransfer(currentTransferTarget, currentTransferTargetPoint);
        }
    }

    /// <summary>
    /// Alt Model 1: A/D ile dal ekseninde kayma. t değeri 0..1 arasında clamp'lenir ve uçlarda asla dal bırakılmaz.
    /// </summary>
    private void UpdateBranchSliding(float dt)
    {
        float adInput = motor.HorizontalInput;
        Vector3 branchAxis = currentBranch.Axis;

        Transform refTransform = cameraTransform != null ? cameraTransform : transform;
        float rightDot = Vector3.Dot(branchAxis, refTransform.right);
        float axisSign = rightDot >= 0f ? 1f : -1f;

        float desiredAxisVelocity = adInput * axisSign * branchSlideSpeed;
        float accel = Mathf.Abs(adInput) > 0.01f ? slideAcceleration : slideDeceleration;
        currentSlideSpeed = Mathf.MoveTowards(currentSlideSpeed, desiredAxisVelocity, accel * dt);

        float branchLength = Mathf.Max(0.1f, currentBranch.Length);
        float nextT = currentBranchT + (currentSlideSpeed / branchLength) * dt;

        if (nextT <= 0f)
        {
            nextT = 0f;
            if (currentSlideSpeed < 0f) currentSlideSpeed = 0f;
        }
        else if (nextT >= 1f)
        {
            nextT = 1f;
            if (currentSlideSpeed > 0f) currentSlideSpeed = 0f;
        }

        currentBranchT = nextT;
    }

    /// <summary>
    /// Alt Model 2: Gerçek sarkaç sallanması.
    /// - W ve S aynı anda basılıysa tork sıfır olur (birbirini iptal eder).
    /// - Tork girişi swingInputSmoothing (0.08 sn) ile yumuşatılır.
    /// - Sarkaç hızı ters yöndeyken bile torkun yönü değişmez (oyuncu hep bastığı yöne iter, fizik sonucu söner veya büyür).
    /// - Maksimum açıya (80 derece) yaklaşırken son 10 derecede yumuşak fren uygulanır ve sınırda swingLimitBounce (0.2) ile hafif seker.
    /// </summary>
    private void UpdatePendulumSwing(float dt)
    {
        if (cameraTransform != null && Mathf.Abs(swingAngle) < 0.2f && Mathf.Abs(swingAngularVelocity) < 0.8f)
        {
            Vector3 desiredPerp = GetPerpendicularHorizontalDirection(currentBranch.Axis, cameraTransform.forward);
            if (desiredPerp.sqrMagnitude > 0.01f)
            {
                swingForwardDir = Vector3.Slerp(swingForwardDir, desiredPerp, 6f * dt).normalized;
            }
        }

        // W ve S tuşlarını doğrudan oku: İkisi aynı anda basılıysa birbirini iptal eder (0),
        // W'den S'e geçişte hedef yön anında değişir.
        bool holdingW = Input.GetKey(KeyCode.W);
        bool holdingS = Input.GetKey(KeyCode.S);
        float rawWsInput = 0f;
        if (holdingW && !holdingS)
            rawWsInput = 1f;
        else if (holdingS && !holdingW)
            rawWsInput = -1f;
        else if (!holdingW && !holdingS)
            rawWsInput = motor.VerticalInput;

        // 0.08 sn yumuşatma (swingInputSmoothing) ile W-S geçişlerinde sert sıçramayı önle
        smoothedSwingInput = Mathf.SmoothDamp(
            smoothedSwingInput,
            rawWsInput,
            ref swingInputSmoothVelocity,
            Mathf.Max(0.005f, swingInputSmoothing),
            Mathf.Infinity,
            dt
        );

        bool hasActiveSwingInput = Mathf.Abs(rawWsInput) > 0.05f;
        float inputTorqueAccel = 0f;

        if (Mathf.Abs(smoothedSwingInput) > 0.005f)
        {
            // Oyuncu sarkaç hız yönüyle aynı yöne basıyorsa rezonans çarpanı uygulanır;
            // ters yöne basıyorsa tork yönü DEĞİŞMEZ (smoothedSwingInput işareti korunur), böylece fiziksel olarak karşı kuvvet uygular ve sönümler!
            bool isPumpingInVelocityDir = (smoothedSwingInput * swingAngularVelocity) > 0f;
            float multiplier = isPumpingInVelocityDir ? resonanceTorqueMultiplier : 1.0f;
            inputTorqueAccel = (smoothedSwingInput * swingInputTorque * multiplier) / Mathf.Max(0.2f, pendulumLength);
        }

        float currentDamping = hasActiveSwingInput ? activeDamping : idleDamping;

        float gravityRestoreAccel = -(pendulumGravity / Mathf.Max(0.2f, pendulumLength)) * Mathf.Sin(swingAngle);
        float dampingAccel = -swingAngularVelocity * currentDamping;

        float angularAccel = gravityRestoreAccel + inputTorqueAccel + dampingAccel;
        swingAngularVelocity += angularAccel * dt;

        // Maksimum açı sınırına yaklaşırken (son 10 derece) dışa doğru giden açısal hıza yumuşak fren uygula
        float maxAngleRad = maxSwingAngleDegrees * Mathf.Deg2Rad;
        float brakeZoneRad = Mathf.Max(1f, limitSoftBrakeZoneDegrees) * Mathf.Deg2Rad;
        float absAngle = Mathf.Abs(swingAngle);

        if (absAngle > (maxAngleRad - brakeZoneRad))
        {
            bool movingOutward = (swingAngle * swingAngularVelocity) > 0f;
            if (movingOutward)
            {
                float zoneProgress = Mathf.InverseLerp(maxAngleRad - brakeZoneRad, maxAngleRad, absAngle);
                float softBrakeFactor = Mathf.Lerp(1f, 0.25f, zoneProgress);
                swingAngularVelocity *= Mathf.Pow(softBrakeFactor, dt * 15f);
            }
        }

        swingAngle += swingAngularVelocity * dt;

        // Sınırda açıyı clamp'le ve açısal hızı hafifçe geri sektir (swingLimitBounce = 0.2), asla dalı bırakma!
        if (swingAngle > maxAngleRad)
        {
            swingAngle = maxAngleRad;
            if (swingAngularVelocity > 0f)
            {
                swingAngularVelocity = -swingAngularVelocity * swingLimitBounce;
            }
        }
        else if (swingAngle < -maxAngleRad)
        {
            swingAngle = -maxAngleRad;
            if (swingAngularVelocity < 0f)
            {
                swingAngularVelocity = -swingAngularVelocity * swingLimitBounce;
            }
        }
    }

    private void ExecuteSwingLaunch()
    {
        Vector3 tangentDir = (Mathf.Cos(swingAngle) * swingForwardDir + Mathf.Sin(swingAngle) * Vector3.up).normalized;
        float tangentialSpeed = swingAngularVelocity * pendulumLength * tangentialVelocityLaunchScale;
        Vector3 tangentialVelocity = tangentDir * tangentialSpeed;

        Transform refTransform = cameraTransform != null ? cameraTransform : transform;
        Vector3 camFlatForward = new Vector3(refTransform.forward.x, 0f, refTransform.forward.z);
        if (camFlatForward.sqrMagnitude < 0.001f)
            camFlatForward = transform.forward;
        camFlatForward.Normalize();

        Vector3 baseLaunchVelocity =
            tangentialVelocity +
            (camFlatForward * launchForwardImpulse) +
            (Vector3.up * launchUpwardImpulse);

        float minPerfectRad = perfectReleaseMinAngleDegrees * Mathf.Deg2Rad;
        bool isNearForwardPeak = (swingAngle >= minPerfectRad) && (swingAngularVelocity >= -0.8f);
        lastLaunchWasPerfect = isNearForwardPeak;

        if (isNearForwardPeak)
        {
            baseLaunchVelocity *= perfectReleaseBonusMultiplier;
        }

        ReleaseCurrentBranchAndRestoreController(baseLaunchVelocity, BranchReleaseReason.Space);
    }

    private void DropFromBranch(BranchReleaseReason reason)
    {
        Vector3 tangentDir = (Mathf.Cos(swingAngle) * swingForwardDir + Mathf.Sin(swingAngle) * Vector3.up).normalized;
        Vector3 releaseVelocity = tangentDir * (swingAngularVelocity * pendulumLength * 0.5f);
        lastLaunchWasPerfect = false;

        ReleaseCurrentBranchAndRestoreController(releaseVelocity, reason);
    }

    private void ReleaseCurrentBranchAndRestoreController(Vector3 exitVelocity, BranchReleaseReason reason)
    {
        lastReleaseReason = reason;
        lastReleasedBranch = currentBranch;
        lastReleaseTime = Time.time;
        regrabCooldownTimer = regrabCooldownDuration;

        currentBranch = null;
        swingAngle = 0f;
        swingAngularVelocity = 0f;
        smoothedSwingInput = 0f;
        swingInputSmoothVelocity = 0f;
        currentSlideSpeed = 0f;

        ClearTransferCandidateState();

        motor.ConsumeJumpBuffer();
        motor.SetState(PlayerMotor.PlayerState.Airborne);
        motor.SetVelocity(exitVelocity);
    }

    private void UpdateBranchTransferInHanging(float dt)
    {
        bool isPressingWS = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S);

        if (isPressingWS)
        {
            swingInputReleaseDelayTimer = transferPromptDelay;
            isTransferBlockedBySwingInput = true;
        }
        else if (swingInputReleaseDelayTimer > 0f)
        {
            swingInputReleaseDelayTimer -= dt;
            isTransferBlockedBySwingInput = (swingInputReleaseDelayTimer > 0f);
        }
        else
        {
            isTransferBlockedBySwingInput = false;
        }

        ScanForBestTransferBranch(currentHandWorldPos, showUiPrompt: !isTransferBlockedBySwingInput);
    }

    private void ScanForBestTransferBranch(Vector3 searchOrigin, bool showUiPrompt)
    {
        if (cameraTransform == null)
        {
            ClearTransferCandidateState();
            return;
        }

        int hitCount = Physics.OverlapSphereNonAlloc(
            searchOrigin,
            transferSearchRadius,
            overlapBuffer,
            branchLayerMask,
            QueryTriggerInteraction.Collide
        );

        BranchData bestCandidate = null;
        Vector3 bestPoint = Vector3.zero;
        float bestScore = -1f;
        float bestDist = 0f;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null)
                continue;

            BranchData branch = GetOrCreateBranchData(col);
            if (branch == null || branch == currentBranch)
                continue;

            Vector3 closestPt = branch.GetClosestPointOnBranch(searchOrigin);
            Vector3 toCandidate = closestPt - searchOrigin;
            float distance = toCandidate.magnitude;

            if (distance < 0.25f || distance > transferSearchRadius)
                continue;

            Vector3 dirFromCamera = (closestPt - cameraTransform.position).normalized;
            float lookDot = Vector3.Dot(cameraTransform.forward, dirFromCamera);

            if (lookDot < minTransferLookDot)
                continue;

            float proximityRatio = 1f - Mathf.Clamp01(distance / transferSearchRadius);
            float score = (lookDot * transferDirectionWeight) + (proximityRatio * transferDistanceWeight);

            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = branch;
                bestPoint = closestPt;
                bestDist = distance;
            }
        }

        currentTransferTarget = bestCandidate;
        currentTransferTargetPoint = bestPoint;
        candidateTransferScore = bestCandidate != null ? bestScore : 0f;
        candidateTransferDistance = bestCandidate != null ? bestDist : 0f;

        bool effectiveHasTarget = HasTransferTarget;
        if (effectiveHasTarget != previousHasTransferTarget)
        {
            previousHasTransferTarget = effectiveHasTarget;
            OnTransferTargetChanged?.Invoke(effectiveHasTarget, currentTransferTarget);
        }

        if (grabPromptUI != null)
        {
            if (showUiPrompt && effectiveHasTarget)
            {
                grabPromptUI.SetTransferTarget(bestPoint);
            }
            else
            {
                grabPromptUI.ClearTransferTarget();
            }
        }
    }

    private void ClearTransferCandidateState()
    {
        currentTransferTarget = null;
        candidateTransferScore = 0f;
        candidateTransferDistance = 0f;
        isTransferBlockedBySwingInput = false;

        if (previousHasTransferTarget)
        {
            previousHasTransferTarget = false;
            OnTransferTargetChanged?.Invoke(false, null);
        }

        if (grabPromptUI != null)
        {
            grabPromptUI.ClearTransferTarget();
        }
    }

    private void StartBranchTransfer(BranchData targetBranch, Vector3 targetPointOnBranch)
    {
        lastReleaseReason = BranchReleaseReason.Transfer;
        activeTransferTargetBranch = targetBranch;

        swingAngle = 0f;
        swingAngularVelocity = 0f;
        smoothedSwingInput = 0f;
        swingInputSmoothVelocity = 0f;
        currentSlideSpeed = 0f;

        transferStartBodyPos = transform.position;
        transferTargetT = targetBranch.GetT(targetPointOnBranch);
        transferEndHandPos = targetBranch.GetPointAtT(transferTargetT);
        transferEndBodyPos = transferEndHandPos + (Vector3.down * pendulumLength);

        float distance = Vector3.Distance(transferStartBodyPos, transferEndBodyPos);
        float distanceRatio = Mathf.Clamp01(distance / Mathf.Max(0.1f, transferSearchRadius));

        transferTotalDuration = Mathf.Lerp(minTransferDuration, maxTransferDuration, distanceRatio);
        transferArcHeight = distance * transferArcHeightRatio;
        transferElapsed = 0f;

        ClearTransferCandidateState();

        if (motor.Look != null)
        {
            motor.Look.SetSwingCameraOffsets(0f, 0f);
        }

        motor.SetVelocity(Vector3.zero);
        motor.SetState(PlayerMotor.PlayerState.Transferring);
    }

    public void UpdateTransferringState(float dt)
    {
        if (activeTransferTargetBranch == null)
        {
            DropFromBranch(BranchReleaseReason.Transfer);
            return;
        }

        transferElapsed += dt;
        float t = Mathf.Clamp01(transferElapsed / Mathf.Max(0.05f, transferTotalDuration));
        float smoothT = Mathf.SmoothStep(0f, 1f, t);

        transferEndHandPos = activeTransferTargetBranch.GetPointAtT(transferTargetT);
        transferEndBodyPos = transferEndHandPos + (Vector3.down * pendulumLength);

        Vector3 linearBodyPos = Vector3.Lerp(transferStartBodyPos, transferEndBodyPos, smoothT);
        float parabolaOffset = 4f * transferArcHeight * smoothT * (1f - smoothT);

        Vector3 previousPos = transform.position;
        transform.position = linearBodyPos + Vector3.up * parabolaOffset;
        currentHandWorldPos = transform.position + Vector3.up * pendulumLength;

        currentTransferSpeed = dt > 0.0001f ? (transform.position - previousPos).magnitude / dt : 0f;

        if (motor.Look != null)
        {
            motor.Look.SmoothLookAtWorldPoint(transferEndHandPos, transferCameraTurnSpeed, dt);
        }

        if (t >= 1f)
        {
            Vector3 arrivalDirection = (transferEndBodyPos - transferStartBodyPos).normalized;
            BranchData arrivedBranch = activeTransferTargetBranch;
            activeTransferTargetBranch = null;
            currentTransferSpeed = 0f;

            EngageBranch(arrivedBranch, arrivalDirection * (pendulumLength * transferArrivalAngularVelocity), isFromTransfer: true);
        }
    }

    private void UpdateSwingCameraFeedback()
    {
        if (motor == null || motor.Look == null)
            return;

        float maxAngleRad = Mathf.Max(0.1f, maxSwingAngleDegrees * Mathf.Deg2Rad);
        float normalizedSwing = Mathf.Clamp(swingAngle / maxAngleRad, -1f, 1f);

        float pitchOffset = -normalizedSwing * maxSwingCameraPitchOffset;
        float rollOffset = (currentSlideSpeed / Mathf.Max(0.1f, branchSlideSpeed)) * maxSwingCameraRollOffset;

        motor.Look.SetSwingCameraOffsets(pitchOffset, rollOffset);
    }

    private Vector3 ComputeSwingForwardDirection(Vector3 branchAxis, Vector3 entryVelocity)
    {
        Vector3 refDir = cameraTransform != null ? cameraTransform.forward : transform.forward;
        if (new Vector3(entryVelocity.x, 0f, entryVelocity.z).sqrMagnitude > 1.5f)
        {
            refDir = entryVelocity.normalized;
        }

        return GetPerpendicularHorizontalDirection(branchAxis, refDir);
    }

    private Vector3 GetPerpendicularHorizontalDirection(Vector3 branchAxis, Vector3 referenceForward)
    {
        Vector3 perpHorizontal = Vector3.Cross(branchAxis, Vector3.up);
        if (perpHorizontal.sqrMagnitude < 0.001f)
        {
            perpHorizontal = new Vector3(referenceForward.x, 0f, referenceForward.z);
        }

        perpHorizontal.y = 0f;
        if (perpHorizontal.sqrMagnitude < 0.001f)
        {
            perpHorizontal = transform.forward;
        }

        perpHorizontal.Normalize();

        if (Vector3.Dot(perpHorizontal, referenceForward) < 0f)
        {
            perpHorizontal = -perpHorizontal;
        }

        return perpHorizontal;
    }

    private BranchData GetOrCreateBranchData(Collider col)
    {
        BranchData data = col.GetComponentInParent<BranchData>();
        if (data == null)
        {
            data = col.gameObject.AddComponent<BranchData>();
        }
        return data;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 handOrigin = Application.isPlaying &&
                             motor != null &&
                             (motor.CurrentState == PlayerMotor.PlayerState.Hanging || motor.CurrentState == PlayerMotor.PlayerState.Transferring)
            ? currentHandWorldPos
            : transform.position + Vector3.up * handPointVerticalOffset;

        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.85f);
        Gizmos.DrawWireSphere(handOrigin, autoGrabSphereRadius);

        Gizmos.color = new Color(0.3f, 0.75f, 1f, 0.35f);
        Gizmos.DrawWireSphere(handOrigin, transferSearchRadius);

        if (Application.isPlaying && motor != null && motor.CurrentState == PlayerMotor.PlayerState.Hanging)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(currentHandWorldPos, transform.position);
            Gizmos.DrawSphere(currentHandWorldPos, 0.08f);
        }

        if (Application.isPlaying && currentTransferTarget != null)
        {
            Gizmos.color = isTransferBlockedBySwingInput ? Color.red : Color.cyan;
            Gizmos.DrawLine(handOrigin, currentTransferTargetPoint);
            Gizmos.DrawWireSphere(currentTransferTargetPoint, 0.18f);
        }
    }
}
