using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CANOPY: The Last Run - Maymun Animasyon Sürücüsü (MonkeyAnimationDriver).
/// 'maymun' / 'MonkeyVisual' üzerindeki Animator bileşenini otomatik bulur,
/// yürürken ve diğer tüm parkur durumlarında (Jump, Fall, WallRun, Slide, Hang, Swing, Transfer)
/// yürüme animasyonunun kesintisiz oynamasını sağlar.
/// </summary>
public class MonkeyAnimationDriver : MonoBehaviour
{
    [Header("Bileşen Referansları")]
    [Tooltip("Maymun modelinin ('maymun' çocuk objesinin) üzerindeki Animator bileşeni (boşsa otomatik bulunur).")]
    [SerializeField] private Animator targetAnimator;

    [Tooltip("Merkezi karakter hareket ve durum yöneticisi (boşsa üst objeden otomatik bulunur).")]
    [SerializeField] private PlayerMotor motor;

    [Tooltip("Duvar koşusu bilgilerini sağlayan bileşen (boşsa otomatik bulunur).")]
    [SerializeField] private WallRunHandler wallRunHandler;

    [Tooltip("Dal tutunma ve sarkaç bilgilerini sağlayan bileşen (boşsa otomatik bulunur).")]
    [SerializeField] private BranchGrabHandler branchGrabHandler;

    [Tooltip("Slide bilgilerini sağlayan bileşen (boşsa otomatik bulunur).")]
    [SerializeField] private SlideHandler slideHandler;

    [Header("İsteğe Bağlı Klip Ataması (Boşsa Controller Klipleri Kullanılır)")]
    [Tooltip("Yürüme ve diğer tüm parkur state'lerinde oynatılacak klip (boşsa MonkeyController içindeki klip kullanılır).")]
    [SerializeField] private AnimationClip walkAnimationClip;

    [Tooltip("Dururken (Idle) oynatılacak klip (boşsa MonkeyController içindeki klip veya yürüme klibi kullanılır).")]
    [SerializeField] private AnimationClip idleAnimationClip;

    [Tooltip("Eğer oyunda sadece yürüme animasyonu varsa, maymun olduğu yerde dururken (Speed=0) yürüme animasyonunu durdur (sabit duruş pozu).")]
    [SerializeField] private bool pauseWalkClipWhenStandingStill = false;

    [Header("Koşu ve Animasyon Hızı (Ayak Kaymasını Önleme)")]
    [Tooltip("Yürüme/koşu animasyon klibinin doğal (1.0x hızdaki) referans yer hızı (m/s).")]
    [SerializeField] private float referenceRunSpeed = 7.0f;

    [Tooltip("Yerde yürürken uygulanacak minimum animasyon hızı çarpanı.")]
    [SerializeField] private float minRunAnimSpeedMultiplier = 0.85f;

    [Tooltip("Yerde sprint atarken uygulanacak maksimum animasyon hızı çarpanı.")]
    [SerializeField] private float maxRunAnimSpeedMultiplier = 1.65f;

    [Tooltip("Yatay hız parametresinin (Speed) Animator'a yazılırken yumuşatma süresi (saniye).")]
    [SerializeField] private float speedParamSmoothTime = 0.04f;

    [Header("Sarkaç (Hanging) ve Hava Eşikleri")]
    [Tooltip("Hanging sırasında sarkaç açısal hızının animasyon hızına etki çarpanı.")]
    [SerializeField] private float swingAnimSpeedFactor = 0.25f;

    [Tooltip("Yere inildiğinde LandTrigger tetiklenmesi için gereken minimum düşüş dikey hızı (m/s, negatif).")]
    [SerializeField] private float minFallSpeedForLandTrigger = -3.5f;

    [Tooltip("Root Motion özelliğini başlangıçta zorla kapat (CharacterController ile çakışmayı önler).")]
    [SerializeField] private bool forceDisableRootMotion = true;

    [Header("Debug ve Builder Bilgisi")]
    [Tooltip("MonkeyAnimatorBuilder tarafından kaydedilen klip eşleşme özeti.")]
    [SerializeField] private string unmatchedClipsReport = "Yürüme animasyonu tüm state'lere bağlandı";

    // Animator Parametre İsimleri (Sabitler)
    public const string ParamSpeed = "Speed";
    public const string ParamIsGrounded = "IsGrounded";
    public const string ParamVerticalVelocity = "VerticalVelocity";
    public const string ParamState = "State";
    public const string ParamWallSide = "WallSide";
    public const string ParamSwingVelocity = "SwingVelocity";
    public const string ParamAnimSpeedMultiplier = "AnimSpeedMultiplier";
    public const string ParamJumpTrigger = "JumpTrigger";
    public const string ParamLandTrigger = "LandTrigger";

    private static readonly string[] KnownStateNames = new string[]
    {
        "Idle",
        "Run",
        "JumpUp",
        "Fall",
        "Land",
        "Slide",
        "WallRunLeft",
        "WallRunRight",
        "Hang",
        "Swing",
        "Transfer"
    };

    private readonly HashSet<int> validParamHashes = new HashSet<int>();
    private readonly Dictionary<int, string> stateHashToName = new Dictionary<int, string>();

    private int hashSpeed;
    private int hashIsGrounded;
    private int hashVerticalVelocity;
    private int hashState;
    private int hashWallSide;
    private int hashSwingVelocity;
    private int hashAnimSpeedMultiplier;
    private int hashJumpTrigger;
    private int hashLandTrigger;

    private float smoothedSpeed;
    private float speedSmoothVelocity;
    private float currentAnimSpeedMultiplier = 1f;
    private string currentAnimatorStateName = "Idle";
    private RuntimeAnimatorController cachedController;

    // Debug HUD ve Editor erişim özellikleri
    public Animator TargetAnimator
    {
        get => targetAnimator;
        set => targetAnimator = value;
    }
    public float CurrentAnimSpeedMultiplier => currentAnimSpeedMultiplier;
    public string CurrentAnimatorStateName => currentAnimatorStateName;
    public string UnmatchedClipsReport
    {
        get => unmatchedClipsReport;
        set => unmatchedClipsReport = value;
    }

    private void Awake()
    {
        ResolveReferences();
        InitializeHashes();
        ApplyOptionalClipOverrides();
        RebuildParameterCache();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void Update()
    {
        if (motor == null)
            return;

        float dt = Time.deltaTime;

        if (targetAnimator == null)
        {
            ResolveReferences();
        }

        if (targetAnimator != null && targetAnimator.runtimeAnimatorController != cachedController)
        {
            RebuildParameterCache();
        }

        UpdateAnimatorParameters(dt);
        UpdateCurrentStateName();
    }

    /// <summary>
    /// 'maymun' / 'MonkeyVisual' üzerindeki gerçek Animator bileşenini bulur.
    /// Eğer yanlışlıkla ana Player objesine de bir Animator eklenmişse, onun Controller'ını çocuk modele aktarır
    /// ve ana Player üzerindeki fazlalık Animator'ı devre dışı bırakır.
    /// </summary>
    public void ResolveReferences()
    {
        if (motor == null)
            motor = GetComponentInParent<PlayerMotor>();

        if (motor != null)
        {
            if (wallRunHandler == null)
                wallRunHandler = motor.GetComponent<WallRunHandler>();
            if (branchGrabHandler == null)
                branchGrabHandler = motor.GetComponent<BranchGrabHandler>();
            if (slideHandler == null)
                slideHandler = motor.GetComponent<SlideHandler>();
        }

        Transform rootTransform = motor != null ? motor.transform : transform;
        Animator rootAnimator = rootTransform.GetComponent<Animator>();

        // Eğer targetAnimator atanmamışsa veya yanlışlıkla ana Player objesindeki Animator seçilmişse,
        // SkinnedMeshRenderer / Animal_3146_Rig barındıran 'maymun' çocuk objesindeki Animator'ı bul!
        if (targetAnimator == null || targetAnimator.transform == rootTransform)
        {
            Animator childModelAnimator = FindModelChildAnimator(rootTransform);
            if (childModelAnimator != null)
            {
                targetAnimator = childModelAnimator;
            }
            else if (targetAnimator == null)
            {
                targetAnimator = rootAnimator;
            }
        }

        // Eğer ana Player üzerinde yanlışlıkla eklenmiş bir Animator varsa ve onun Controller'ı doluysa çocuk modele kopyala
        if (rootAnimator != null && targetAnimator != null && rootAnimator != targetAnimator)
        {
            if (targetAnimator.runtimeAnimatorController == null && rootAnimator.runtimeAnimatorController != null)
            {
                targetAnimator.runtimeAnimatorController = rootAnimator.runtimeAnimatorController;
            }
            rootAnimator.enabled = false;
        }

        if (targetAnimator != null)
        {
            targetAnimator.enabled = true;
            // FPS kamerası modele çok yakın olduğunda animasyonun durmasını (culling) engeller
            targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (forceDisableRootMotion)
            {
                targetAnimator.applyRootMotion = false;
            }
        }
    }

    private Animator FindModelChildAnimator(Transform playerRoot)
    {
        // 1. Önce SkinnedMeshRenderer'ın (örn. Animal_3146) üst objesindeki ('maymun') Animator'a bak
        SkinnedMeshRenderer smr = playerRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (smr != null)
        {
            Animator smrParentAnim = smr.GetComponentInParent<Animator>();
            if (smrParentAnim != null && smrParentAnim.transform != playerRoot)
            {
                return smrParentAnim;
            }
        }

        // 2. Player altındaki tüm çocuk Animator'ları tara
        Animator[] allAnimators = playerRoot.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < allAnimators.Length; i++)
        {
            if (allAnimators[i] != null && allAnimators[i].transform != playerRoot)
            {
                return allAnimators[i];
            }
        }

        return null;
    }

    private void ApplyOptionalClipOverrides()
    {
        if (targetAnimator == null || targetAnimator.runtimeAnimatorController == null)
            return;

        if (walkAnimationClip == null && idleAnimationClip == null)
            return;

        AnimatorOverrideController overrideCtrl = new AnimatorOverrideController(targetAnimator.runtimeAnimatorController);
        List<KeyValuePair<AnimationClip, AnimationClip>> overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        overrideCtrl.GetOverrides(overrides);

        for (int i = 0; i < overrides.Count; i++)
        {
            AnimationClip original = overrides[i].Key;
            if (original == null)
                continue;

            AnimationClip replacement = walkAnimationClip != null ? walkAnimationClip : original;
            if (idleAnimationClip != null && original.name.ToLowerInvariant().Contains("idle"))
            {
                replacement = idleAnimationClip;
            }
            overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, replacement);
        }

        overrideCtrl.ApplyOverrides(overrides);
        targetAnimator.runtimeAnimatorController = overrideCtrl;
    }

    private void InitializeHashes()
    {
        hashSpeed = Animator.StringToHash(ParamSpeed);
        hashIsGrounded = Animator.StringToHash(ParamIsGrounded);
        hashVerticalVelocity = Animator.StringToHash(ParamVerticalVelocity);
        hashState = Animator.StringToHash(ParamState);
        hashWallSide = Animator.StringToHash(ParamWallSide);
        hashSwingVelocity = Animator.StringToHash(ParamSwingVelocity);
        hashAnimSpeedMultiplier = Animator.StringToHash(ParamAnimSpeedMultiplier);
        hashJumpTrigger = Animator.StringToHash(ParamJumpTrigger);
        hashLandTrigger = Animator.StringToHash(ParamLandTrigger);

        stateHashToName.Clear();
        for (int i = 0; i < KnownStateNames.Length; i++)
        {
            string sName = KnownStateNames[i];
            stateHashToName[Animator.StringToHash(sName)] = sName;
            stateHashToName[Animator.StringToHash("Base Layer." + sName)] = sName;
        }
    }

    public void RebuildParameterCache()
    {
        validParamHashes.Clear();

        if (targetAnimator == null || targetAnimator.runtimeAnimatorController == null)
        {
            cachedController = null;
            return;
        }

        cachedController = targetAnimator.runtimeAnimatorController;
        targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (forceDisableRootMotion)
        {
            targetAnimator.applyRootMotion = false;
        }

        AnimatorControllerParameter[] parameters = targetAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            validParamHashes.Add(parameters[i].nameHash);
        }
    }

    private void SubscribeEvents()
    {
        if (motor != null)
        {
            motor.OnJumpCallback -= HandleJump;
            motor.OnJumpCallback += HandleJump;

            motor.OnLandCallback -= HandleLand;
            motor.OnLandCallback += HandleLand;

            motor.OnStateChangedCallback -= HandleStateChanged;
            motor.OnStateChangedCallback += HandleStateChanged;
        }

        if (wallRunHandler != null)
        {
            wallRunHandler.OnWallJumpCallback -= HandleWallJump;
            wallRunHandler.OnWallJumpCallback += HandleWallJump;

            wallRunHandler.OnWallRunStartCallback -= HandleWallRunStart;
            wallRunHandler.OnWallRunStartCallback += HandleWallRunStart;

            wallRunHandler.OnWallRunEndCallback -= HandleWallRunEnd;
            wallRunHandler.OnWallRunEndCallback += HandleWallRunEnd;
        }

        if (slideHandler != null)
        {
            slideHandler.OnSlideStartCallback -= HandleSlideStart;
            slideHandler.OnSlideStartCallback += HandleSlideStart;

            slideHandler.OnSlideEndCallback -= HandleSlideEnd;
            slideHandler.OnSlideEndCallback += HandleSlideEnd;
        }

        if (branchGrabHandler != null)
        {
            branchGrabHandler.OnBranchGrabbedCallback -= HandleBranchGrabbed;
            branchGrabHandler.OnBranchGrabbedCallback += HandleBranchGrabbed;
        }
    }

    private void UnsubscribeEvents()
    {
        if (motor != null)
        {
            motor.OnJumpCallback -= HandleJump;
            motor.OnLandCallback -= HandleLand;
            motor.OnStateChangedCallback -= HandleStateChanged;
        }

        if (wallRunHandler != null)
        {
            wallRunHandler.OnWallJumpCallback -= HandleWallJump;
            wallRunHandler.OnWallRunStartCallback -= HandleWallRunStart;
            wallRunHandler.OnWallRunEndCallback -= HandleWallRunEnd;
        }

        if (slideHandler != null)
        {
            slideHandler.OnSlideStartCallback -= HandleSlideStart;
            slideHandler.OnSlideEndCallback -= HandleSlideEnd;
        }

        if (branchGrabHandler != null)
        {
            branchGrabHandler.OnBranchGrabbedCallback -= HandleBranchGrabbed;
        }
    }

    private void HandleJump()
    {
        SafeResetTrigger(hashLandTrigger);
        SafeSetTrigger(hashJumpTrigger);
    }

    private void HandleWallJump(Vector3 jumpVelocity)
    {
        SafeResetTrigger(hashLandTrigger);
        SafeSetTrigger(hashJumpTrigger);
    }

    private void HandleLand(float landingVerticalSpeed)
    {
        if (landingVerticalSpeed <= minFallSpeedForLandTrigger)
        {
            SafeResetTrigger(hashJumpTrigger);
            SafeSetTrigger(hashLandTrigger);
        }
    }

    private void HandleStateChanged(PlayerMotor.PlayerState previousState, PlayerMotor.PlayerState newState)
    {
        if (previousState == PlayerMotor.PlayerState.Hanging &&
            newState == PlayerMotor.PlayerState.Airborne &&
            motor != null && motor.VerticalSpeed > 1.5f)
        {
            SafeSetTrigger(hashJumpTrigger);
        }
    }

    private void HandleWallRunStart()
    {
        SafeResetTrigger(hashJumpTrigger);
    }

    private void HandleWallRunEnd() { }

    private void HandleSlideStart()
    {
        SafeResetTrigger(hashJumpTrigger);
    }

    private void HandleSlideEnd() { }

    private void HandleBranchGrabbed(BranchData branch)
    {
        SafeResetTrigger(hashJumpTrigger);
        SafeResetTrigger(hashLandTrigger);

        if (motor != null && motor.Look != null)
        {
            motor.Look.AddCameraShakeImpulse(0.40f);
        }
    }

    private void UpdateAnimatorParameters(float dt)
    {
        float rawHorizSpeed = motor.HorizontalSpeed;
        smoothedSpeed = Mathf.SmoothDamp(smoothedSpeed, rawHorizSpeed, ref speedSmoothVelocity, speedParamSmoothTime, Mathf.Infinity, dt);

        PlayerMotor.PlayerState state = motor.CurrentState;
        bool grounded = motor.IsGrounded;
        float verticalVel = motor.VerticalSpeed;

        float wallSide = 0f;
        if (wallRunHandler != null && state == PlayerMotor.PlayerState.WallRunning)
        {
            wallSide = wallRunHandler.IsWallRight ? 1f : -1f;
        }

        float swingAngVel = 0f;
        if (branchGrabHandler != null && (state == PlayerMotor.PlayerState.Hanging || state == PlayerMotor.PlayerState.Transferring))
        {
            swingAngVel = Mathf.Abs(branchGrabHandler.SwingAngularVelocity);
        }

        // Animasyon hızı: Yürürken ve diğer tüm parkur hareketlerinde yürüme animasyonu akıcı oynar
        if (state == PlayerMotor.PlayerState.Grounded)
        {
            if (rawHorizSpeed > 0.20f)
            {
                float safeRefSpeed = Mathf.Max(1f, referenceRunSpeed);
                currentAnimSpeedMultiplier = Mathf.Clamp(
                    rawHorizSpeed / safeRefSpeed,
                    minRunAnimSpeedMultiplier,
                    maxRunAnimSpeedMultiplier
                );
            }
            else
            {
                currentAnimSpeedMultiplier = pauseWalkClipWhenStandingStill ? 0f : 1f;
            }
        }
        else if (state == PlayerMotor.PlayerState.WallRunning)
        {
            float safeRefSpeed = Mathf.Max(1f, referenceRunSpeed);
            currentAnimSpeedMultiplier = Mathf.Clamp(
                rawHorizSpeed / safeRefSpeed,
                1.0f,
                maxRunAnimSpeedMultiplier
            );
        }
        else if (state == PlayerMotor.PlayerState.Hanging)
        {
            currentAnimSpeedMultiplier = Mathf.Clamp(0.9f + swingAngVel * swingAnimSpeedFactor, 0.85f, 1.6f);
        }
        else
        {
            // Airborne (JumpUp / Fall), Sliding ve Transferring durumlarında yürüme animasyonu aktif hızda oynar
            currentAnimSpeedMultiplier = 1.15f;
        }

        if (targetAnimator == null || targetAnimator.runtimeAnimatorController == null)
            return;

        SafeSetFloat(hashSpeed, smoothedSpeed);
        SafeSetBool(hashIsGrounded, grounded);
        SafeSetFloat(hashVerticalVelocity, verticalVel);
        SafeSetInt(hashState, (int)state);
        SafeSetFloat(hashWallSide, wallSide);
        SafeSetFloat(hashSwingVelocity, swingAngVel);
        SafeSetFloat(hashAnimSpeedMultiplier, currentAnimSpeedMultiplier);
    }

    private void UpdateCurrentStateName()
    {
        if (targetAnimator == null || targetAnimator.runtimeAnimatorController == null || !targetAnimator.isActiveAndEnabled)
        {
            currentAnimatorStateName = $"{GetFallbackLogicalStateName()} (Controller Atanmamış!)";
            return;
        }

        AnimatorStateInfo stateInfo = targetAnimator.GetCurrentAnimatorStateInfo(0);
        string baseStateName = GetFallbackLogicalStateName();
        if (stateHashToName.TryGetValue(stateInfo.shortNameHash, out string matchedName))
        {
            baseStateName = matchedName;
        }
        else if (stateHashToName.TryGetValue(stateInfo.fullPathHash, out string matchedFullName))
        {
            baseStateName = matchedFullName;
        }

        AnimatorClipInfo[] clipInfos = targetAnimator.GetCurrentAnimatorClipInfo(0);
        if (clipInfos != null && clipInfos.Length > 0 && clipInfos[0].clip != null)
        {
            string shortClipName = clipInfos[0].clip.name;
            int lastPipe = shortClipName.LastIndexOf('|');
            if (lastPipe >= 0 && lastPipe < shortClipName.Length - 1)
            {
                shortClipName = shortClipName.Substring(lastPipe + 1);
            }
            currentAnimatorStateName = $"{baseStateName} [{shortClipName}]";
            return;
        }

        currentAnimatorStateName = baseStateName;
    }

    private string GetFallbackLogicalStateName()
    {
        if (motor == null)
            return "Idle";

        switch (motor.CurrentState)
        {
            case PlayerMotor.PlayerState.Grounded:
                return motor.HorizontalSpeed > 0.25f ? "Run" : "Idle";
            case PlayerMotor.PlayerState.Airborne:
                return motor.VerticalSpeed > 0.5f ? "JumpUp" : "Fall";
            case PlayerMotor.PlayerState.WallRunning:
                return (wallRunHandler != null && wallRunHandler.IsWallRight) ? "WallRunRight" : "WallRunLeft";
            case PlayerMotor.PlayerState.Hanging:
                return (branchGrabHandler != null && Mathf.Abs(branchGrabHandler.SwingAngularVelocity) > 0.45f) ? "Swing" : "Hang";
            case PlayerMotor.PlayerState.Transferring:
                return "Transfer";
            case PlayerMotor.PlayerState.Sliding:
                return "Slide";
            default:
                return "Idle";
        }
    }

    private void SafeSetFloat(int paramHash, float value)
    {
        if (validParamHashes.Contains(paramHash))
        {
            targetAnimator.SetFloat(paramHash, value);
        }
    }

    private void SafeSetBool(int paramHash, bool value)
    {
        if (validParamHashes.Contains(paramHash))
        {
            targetAnimator.SetBool(paramHash, value);
        }
    }

    private void SafeSetInt(int paramHash, int value)
    {
        if (validParamHashes.Contains(paramHash))
        {
            targetAnimator.SetInteger(paramHash, value);
        }
    }

    private void SafeSetTrigger(int paramHash)
    {
        if (validParamHashes.Contains(paramHash))
        {
            targetAnimator.SetTrigger(paramHash);
        }
    }

    private void SafeResetTrigger(int paramHash)
    {
        if (validParamHashes.Contains(paramHash))
        {
            targetAnimator.ResetTrigger(paramHash);
        }
    }
}
