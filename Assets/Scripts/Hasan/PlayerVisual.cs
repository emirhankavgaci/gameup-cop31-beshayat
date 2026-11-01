using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// CANOPY: The Last Run - Maymun Görsel Modeli (Monkey_B1) ve Sabit "Eye" Kamera Yöneticisi.
/// FirstPerson modunda:
/// 1) Kamerayı 'Eye' yüksekliğinde tamamen SABİT tutar (dünya uzayında gecikme/lag olmaz).
/// 2) Yürüme/koşma animasyonlarında maymunun boyun/gövde kemiğini (macaque_Neck_bone / macaque_Head_bone)
///    LateUpdate içinde kameranın hemen arkasına ve altına kilitler; böylece yürürken kamera asla geride kalmaz.
/// 3) FirstPerson modunda kafa kemiğini (macaque_Head_bone) küçülterek ve 'Eye' mesh'ini ShadowsOnly yaparak
///    maymunun kafasının/ağzının içinin görünmesini %100 engeller; kollar, eller, göğüs ve bacaklar ise tam görünür kalır.
/// </summary>
public class PlayerVisual : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Merkezi karakter hareket ve durum yöneticisi (boşsa üst objeden otomatik bulunur).")]
    [SerializeField] private PlayerMotor motor;

    [Tooltip("Duvar koşusu bilgilerini sağlayan bileşen (boşsa otomatik bulunur).")]
    [SerializeField] private WallRunHandler wallRunHandler;

    [Tooltip("Dal tutunma ve sarkaç açısını sağlayan bileşen (boşsa otomatik bulunur).")]
    [SerializeField] private BranchGrabHandler branchGrabHandler;

    [Tooltip("Kayma (Slide) durumunu sağlayan bileşen (boşsa otomatik bulunur).")]
    [SerializeField] private SlideHandler slideHandler;

    [Tooltip("Görsel modelin bağlı olduğu 'Monkey_B1' çocuk Transform referansı (boşsa otomatik bulunur).")]
    [SerializeField] private Transform monkeyVisualRoot;

    [Header("Sabit Eye (Göz) Kamera ve FPS Gövde Hizalama")]
    [Tooltip("Model içindeki 'Eye' mesh bileşeni (boşsa otomatik olarak isminde 'Eye' geçen mesh bulunur).")]
    [SerializeField] private Renderer eyeMeshRenderer;

    [Tooltip("Kameranın başlangıç 'Eye' noktasına göre ileri/yukarı ince ayar ofseti (metre).")]
    [SerializeField] private Vector3 eyeCameraOffset = new Vector3(0f, 0.04f, 0.10f);

    [Tooltip("KAPALI (0) olduğunda kamera yüksekliği ve konumu tamamen SABİT kalır, animasyonla sallanmaz.")]
    [Range(0f, 1f)]
    [SerializeField] private float eyeAnimationFollowWeight = 0f;

    [Tooltip("FirstPerson modunda yürürken/koşarken maymun gövdesini sabit kameranın hemen altında ve arkasında tutar (kameranın geride kalmasını ve maymunun içine girmesini önler).")]
    [SerializeField] private bool lockBodyUnderCameraInFirstPerson = true;

    [Tooltip("FirstPerson modunda kafa kemiğini (macaque_Head_bone) gizleyerek kafa/ağız içinin görünmesini kesin olarak engeller (kollar, gövde ve bacaklar görünür kalır).")]
    [SerializeField] private bool hideHeadBoneInFirstPerson = true;

    [Tooltip("FirstPerson modunda maymunun kafa/boyun noktasının kameranın ne kadar gerisinde duracağı (metre).")]
    [SerializeField] private float firstPersonNeckBehindCameraOffset = 0.18f;

    [Tooltip("FirstPerson modunda maymunun kafa/boyun noktasının kameranın en az ne kadar altında duracağı (metre).")]
    [SerializeField] private float firstPersonNeckBelowCameraMinOffset = 0.10f;

    [Header("Model Pozisyon, Rotasyon ve Ölçek Offsetleri")]
    [Tooltip("Ayakları CharacterController'ın en alt noktasına otomatik oturt.")]
    [SerializeField] private bool autoAlignFeetToControllerBottom = true;

    [Tooltip("Modelin yerel pozisyon ofseti (ayak hizalamasının üzerine eklenir).")]
    [SerializeField] private Vector3 visualOffset = Vector3.zero;

    [Tooltip("Modelin yerel rotasyon ofseti (derece). FBX ters bakıyorsa Y=180 yapılabilir.")]
    [SerializeField] private Vector3 visualRotationOffset = Vector3.zero;

    [Tooltip("Modelin yerel ölçek (scale) çarpanı.")]
    [SerializeField] private Vector3 visualScale = Vector3.one;

    [Header("State Bazlı Görsel Rotasyon ve Slide Ayarları")]
    [Tooltip("Görsel rotasyon geçişlerinin yumuşatma (Slerp) hızı.")]
    [SerializeField] private float rotationSmoothSpeed = 14f;

    [Tooltip("Görsel pozisyon ofseti geçişlerinin yumuşatma (Lerp) hızı.")]
    [SerializeField] private float positionSmoothSpeed = 16f;

    [Tooltip("Wall Run sırasında modelin duvardan dışarı doğru hafif yatış (roll) açısı (derece).")]
    [SerializeField] private float wallRunVisualTiltDegrees = 14f;

    [Tooltip("Hanging sırasında sarkaç açısının görsel modele uygulanma çarpanı (1.0 = birebir sarkaç açısı).")]
    [Range(0f, 1.5f)]
    [SerializeField] private float swingVisualAngleMultiplier = 0.85f;

    [Tooltip("Dala tutunurken (Hanging/Transferring) sadece görsel maymun modelinin dala tam oturması için eklenecek yukarı yönlü dikey ofset (metre). Fiziksel tutunmayı etkilemez.")]
    [SerializeField] private float hangingExtraVerticalOffset = 1.45f;

    [Tooltip("Slide sırasında CharacterController küçüldüğünde modele eklenecek ekstra dikey ofset (metre).")]
    [SerializeField] private float slideExtraVerticalOffset = -0.08f;

    [Tooltip("Slide sırasında modelin yatık/alçak pozda görünmesi için eğim açısı (derece).")]
    [SerializeField] private float slideVisualPitchDegrees = -20f;

    [Header("Birinci Şahıs (FirstPerson) Görünürlük Ayarları")]
    [Tooltip("KAPALI (false) olduğunda FirstPerson modunda maymun gövdesi (Body) GÖRÜNÜR kalır, sadece Eye mesh'i ve kafa içi gizlenir.")]
    [SerializeField] private bool hideBodyInFirstPerson = false;

    [Tooltip("Başlangıçta modelin altındaki tüm Collider bileşenlerini otomatik kapat (fizik sadece CharacterController'da kalsın).")]
    [SerializeField] private bool disableModelCollidersOnAwake = true;

    [Tooltip("Player ana objesinde eski kapsül/placeholder MeshRenderer varsa otomatik kapat.")]
    [SerializeField] private bool disableRootPlaceholderMeshOnAwake = true;

    // Dahili çalışma değişkenleri
    private CharacterController controller;
    private readonly List<Renderer> bodyRenderers = new List<Renderer>();
    private readonly List<Renderer> eyeRenderers = new List<Renderer>();

    // Kemik referansları (Monkey_B1 iskeleti)
    private Transform headBone;
    private Transform neckBone;
    private Transform leftEyeBone;
    private Transform rightEyeBone;
    private Vector3 originalHeadBoneScale = Vector3.one;
    private bool hasCachedHeadBoneScale;

    private Vector3 defaultEyeLocalPosInPlayer = new Vector3(0f, 0.55f, 0.12f);
    private bool hasCalibratedDefaultEyePos;
    private Quaternion currentDynamicRotation = Quaternion.identity;
    private float currentSlideVerticalOffset;
    private float currentHangingVerticalOffset;
    private float currentSlidePitchOffset;
    private ViewMode currentAppliedViewMode = ViewMode.FirstPerson;
    private int lastSyncedFrame = -1;

    // Dış erişim özellikleri (PlayerLook, Debug HUD ve Editor araçları için)
    public Transform VisualRoot => monkeyVisualRoot;
    public Renderer EyeMeshRenderer => eyeMeshRenderer;
    public bool HasEyeMesh => eyeMeshRenderer != null || eyeRenderers.Count > 0 || leftEyeBone != null || headBone != null;
    public Vector3 VisualOffset
    {
        get => visualOffset;
        set => visualOffset = value;
    }
    public Vector3 VisualRotationOffset
    {
        get => visualRotationOffset;
        set => visualRotationOffset = value;
    }
    public Vector3 VisualScale
    {
        get => visualScale;
        set => visualScale = value;
    }
    public bool IsVisibleInFirstPerson => !hideBodyInFirstPerson;
    public int TotalRendererCount => bodyRenderers.Count + eyeRenderers.Count;
    public ViewMode CurrentAppliedViewMode => currentAppliedViewMode;

    private void Awake()
    {
        // Kameranın animasyonla sallanıp geride kalmaması için takip ağırlığını sıfırla (tam sabit kamera)
        eyeAnimationFollowWeight = 0f;
        hideBodyInFirstPerson = false;
        if (hangingExtraVerticalOffset < 1.0f)
        {
            hangingExtraVerticalOffset = 1.45f;
        }

        ResolveReferences();

        if (disableRootPlaceholderMeshOnAwake && motor != null)
        {
            DisableRootPlaceholderMesh();
        }

        if (disableModelCollidersOnAwake)
        {
            DisableAllModelColliders();
        }

        CacheModelRenderersAndBones();
        ApplyScaleAndImmediateTransform();
        CalibrateDefaultEyeLocalPosition();
        SetViewMode(ViewMode.FirstPerson);
    }

    private void Update()
    {
        // Animator kemikleri güncellemeden önce kafa kemiği ölçeğini orijinal haline döndür
        RestoreHeadBoneScaleIfNeeded();
    }

    private void LateUpdate()
    {
        SyncVisualForCurrentFrame(Time.deltaTime);
    }

    /// <summary>
    /// Animator çalıştıktan sonra (LateUpdate aşamasında) modeli hizalar,
    /// FirstPerson modunda gövdeyi sabit kameranın altına kilitler ve kafa içi görünmesini engeller.
    /// PlayerLook.LateUpdate tarafından da çağrılarak çalışma sırası garanti altına alınır.
    /// </summary>
    public void SyncVisualForCurrentFrame(float dt)
    {
        if (lastSyncedFrame == Time.frameCount)
            return;

        lastSyncedFrame = Time.frameCount;

        if (monkeyVisualRoot == null || (motor != null && monkeyVisualRoot == motor.transform))
            return;

        // 1. Kemik pozisyonlarını doğru okumak için önce kafa kemiği ölçeğini(1,1,1) yap
        RestoreHeadBoneScaleIfNeeded();

        // 2. Ölçek ve rotasyonu güncelle
        UpdateVisualScale();
        UpdateVisualRotation(dt);

        // 3. Pozisyonu güncelle ve FirstPerson modunda boyun/gövdeyi sabit kameranın altına kilitle
        UpdateVisualPositionAndLockToCamera(dt);

        // 4. FirstPerson modunda kafa kemiğini küçült ki maymunun kafasının/ağzının içi kameraya asla girmesin
        ApplyFirstPersonHeadBoneCulling();
    }

    /// <summary>
    /// 'Monkey_B1' / 'maymun' çocuk objesini ve içindeki 'Eye' mesh'ini otomatik tespit eder.
    /// </summary>
    public void ResolveReferences()
    {
        if (motor == null)
            motor = GetComponentInParent<PlayerMotor>();

        if (motor != null)
        {
            controller = motor.GetComponent<CharacterController>();
            if (wallRunHandler == null)
                wallRunHandler = motor.GetComponent<WallRunHandler>();
            if (branchGrabHandler == null)
                branchGrabHandler = motor.GetComponent<BranchGrabHandler>();
            if (slideHandler == null)
                slideHandler = motor.GetComponent<SlideHandler>();
        }
        else
        {
            controller = GetComponentInParent<CharacterController>();
        }

        if (monkeyVisualRoot == null || (motor != null && monkeyVisualRoot == motor.transform))
        {
            if (motor != null && transform != motor.transform)
            {
                monkeyVisualRoot = transform;
            }
            else if (motor != null)
            {
                monkeyVisualRoot = FindMonkeyChildUnderPlayer(motor.transform);
            }
        }
    }

    private Transform FindMonkeyChildUnderPlayer(Transform playerRoot)
    {
        for (int i = 0; i < playerRoot.childCount; i++)
        {
            Transform child = playerRoot.GetChild(i);
            if (!child.gameObject.activeInHierarchy)
                continue;

            string lower = child.name.ToLowerInvariant();
            if (lower.Contains("monkey_b1") || lower.Contains("monkey") || lower.Contains("maymun"))
            {
                return child;
            }
        }

        SkinnedMeshRenderer smr = playerRoot.GetComponentInChildren<SkinnedMeshRenderer>(false);
        if (smr != null && smr.transform != playerRoot)
        {
            Transform current = smr.transform;
            while (current.parent != null && current.parent != playerRoot)
            {
                current = current.parent;
            }
            return current;
        }

        return null;
    }

    private void DisableRootPlaceholderMesh()
    {
        if (motor == null)
            return;

        MeshRenderer rootRenderer = motor.GetComponent<MeshRenderer>();
        if (rootRenderer != null && rootRenderer.transform == motor.transform)
        {
            rootRenderer.enabled = false;
        }
    }

    public void DisableAllModelColliders()
    {
        if (monkeyVisualRoot == null)
            return;

        Collider[] modelColliders = monkeyVisualRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < modelColliders.Length; i++)
        {
            if (controller != null && modelColliders[i] == controller)
                continue;

            modelColliders[i].enabled = false;
        }
    }

    /// <summary>
    /// Modelin altındaki 'Body' / 'Eye' renderer'larını ve kafa/boyun/göz kemiklerini önbelleğe alır.
    /// </summary>
    public void CacheModelRenderers()
    {
        CacheModelRenderersAndBones();
    }

    private void CacheModelRenderersAndBones()
    {
        bodyRenderers.Clear();
        eyeRenderers.Clear();

        if (monkeyVisualRoot == null)
            return;

        Renderer[] renderers = monkeyVisualRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;

            if (motor != null && r.transform == motor.transform)
                continue;

            if (r is SkinnedMeshRenderer smr)
            {
                smr.updateWhenOffscreen = true;
            }

            string lowerName = r.gameObject.name.ToLowerInvariant();
            bool isEye = lowerName.Contains("eye") ||
                         (r.sharedMaterial != null && r.sharedMaterial.name.ToLowerInvariant().Contains("eye"));

            if (isEye)
            {
                eyeRenderers.Add(r);
                if (eyeMeshRenderer == null)
                {
                    eyeMeshRenderer = r;
                }
            }
            else
            {
                bodyRenderers.Add(r);
            }
        }

        // Monkey_B1 içindeki kafa, boyun ve göz kemiklerini bul
        Transform[] allTransforms = monkeyVisualRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];
            if (t == null)
                continue;

            string lower = t.name.ToLowerInvariant();
            if (lower.Contains("nub") || lower.Contains("eyelid") || lower.Contains("hlp"))
                continue;

            if (headBone == null && (lower == "macaque_head_bone" || lower.Contains("head_bone") || lower == "head"))
            {
                headBone = t;
                originalHeadBoneScale = headBone.localScale;
                hasCachedHeadBoneScale = true;
            }
            else if (neckBone == null && (lower == "macaque_neck_bone" || lower.Contains("neck_bone") || lower == "neck"))
            {
                neckBone = t;
            }
            else if (leftEyeBone == null && (lower == "macaque_l_eye_bone" || lower.Contains("l_eye")))
            {
                leftEyeBone = t;
            }
            else if (rightEyeBone == null && (lower == "macaque_r_eye_bone" || lower.Contains("r_eye")))
            {
                rightEyeBone = t;
            }
        }
    }

    private void RestoreHeadBoneScaleIfNeeded()
    {
        if (headBone != null && hasCachedHeadBoneScale)
        {
            headBone.localScale = originalHeadBoneScale;
        }
    }

    private void ApplyFirstPersonHeadBoneCulling()
    {
        if (headBone == null || !hasCachedHeadBoneScale)
            return;

        if (currentAppliedViewMode == ViewMode.FirstPerson && hideHeadBoneInFirstPerson)
        {
            // Kafa kemiğini çok küçük bir noktaya indirerek FirstPerson'da kafa/ağız/burun içinin kameraya girmesini önle
            headBone.localScale = new Vector3(0.0001f, 0.0001f, 0.0001f);
        }
        else
        {
            headBone.localScale = originalHeadBoneScale;
        }
    }

    private void CalibrateDefaultEyeLocalPosition()
    {
        Transform playerRoot = motor != null ? motor.transform : transform;
        RestoreHeadBoneScaleIfNeeded();

        Vector3 eyeCenterWorld = Vector3.zero;
        bool foundAny = false;

        if (leftEyeBone != null && rightEyeBone != null)
        {
            eyeCenterWorld = (leftEyeBone.position + rightEyeBone.position) * 0.5f;
            foundAny = true;
        }
        else if (eyeMeshRenderer != null)
        {
            eyeCenterWorld = eyeMeshRenderer.bounds.center;
            foundAny = true;
        }
        else if (headBone != null)
        {
            eyeCenterWorld = headBone.position;
            foundAny = true;
        }

        if (foundAny)
        {
            Vector3 localInPlayer = playerRoot.InverseTransformPoint(eyeCenterWorld);
            if (localInPlayer.sqrMagnitude > 0.001f)
            {
                // Yatayda tam merkezde (X=0) sabit bir göz noktası kaydet
                localInPlayer.x = 0f;
                defaultEyeLocalPosInPlayer = localInPlayer;
                hasCalibratedDefaultEyePos = true;
            }
        }
    }

    /// <summary>
    /// Sabit First-Person kamerasının Player yerel uzayındaki (local space) konumunu döndürür.
    /// </summary>
    public Vector3 GetSteadyEyeCameraLocalPosition()
    {
        if (!hasCalibratedDefaultEyePos)
        {
            CalibrateDefaultEyeLocalPosition();
        }

        return new Vector3(
            defaultEyeLocalPosInPlayer.x + eyeCameraOffset.x,
            defaultEyeLocalPosInPlayer.y + currentSlideVerticalOffset + currentHangingVerticalOffset + eyeCameraOffset.y,
            defaultEyeLocalPosInPlayer.z + eyeCameraOffset.z
        );
    }

    /// <summary>
    /// Kameranın yerleştirileceği 'Eye' (Göz) noktasının dünya uzayındaki hedef pozisyonunu döndürür.
    /// Varsayılan olarak Player üzerinde tamamen SABİTTİR (gecikme/lag yapmaz, yürürken geride kalmaz).
    /// </summary>
    public Vector3 GetEyeCameraWorldPosition(out bool foundEyeMesh)
    {
        Transform playerRoot = motor != null ? motor.transform : transform;

        if (!hasCalibratedDefaultEyePos)
        {
            CalibrateDefaultEyeLocalPosition();
        }

        foundEyeMesh = HasEyeMesh || hasCalibratedDefaultEyePos;
        Vector3 steadyLocal = GetSteadyEyeCameraLocalPosition();
        return playerRoot.TransformPoint(steadyLocal);
    }

    /// <summary>
    /// FirstPerson modunda 'Eye' mesh'ini gizler (ShadowsOnly) ve kafa kemiğini küçültür;
    /// 'Body' mesh'ini ise görünür (ShadowCastingMode.On) tutar. ThirdPerson modunda tamamı görünür olur.
    /// </summary>
    public void SetViewMode(ViewMode mode)
    {
        currentAppliedViewMode = mode;

        if (bodyRenderers.Count == 0 && eyeRenderers.Count == 0)
        {
            CacheModelRenderersAndBones();
        }

        ShadowCastingMode bodyShadowMode = (mode == ViewMode.FirstPerson && hideBodyInFirstPerson)
            ? ShadowCastingMode.ShadowsOnly
            : ShadowCastingMode.On;

        ShadowCastingMode eyeShadowMode = (mode == ViewMode.FirstPerson)
            ? ShadowCastingMode.ShadowsOnly
            : ShadowCastingMode.On;

        for (int i = 0; i < bodyRenderers.Count; i++)
        {
            if (bodyRenderers[i] != null)
            {
                bodyRenderers[i].enabled = true;
                bodyRenderers[i].shadowCastingMode = bodyShadowMode;
            }
        }

        for (int i = 0; i < eyeRenderers.Count; i++)
        {
            if (eyeRenderers[i] != null)
            {
                eyeRenderers[i].enabled = true;
                eyeRenderers[i].shadowCastingMode = eyeShadowMode;
            }
        }

        ApplyFirstPersonHeadBoneCulling();
    }

    private void ApplyScaleAndImmediateTransform()
    {
        if (monkeyVisualRoot == null || (motor != null && monkeyVisualRoot == motor.transform))
            return;

        monkeyVisualRoot.localScale = visualScale;
        monkeyVisualRoot.localPosition = CalculateTargetLocalPosition(0f);
        monkeyVisualRoot.localRotation = Quaternion.Euler(visualRotationOffset);
    }

    private void UpdateVisualScale()
    {
        if (monkeyVisualRoot.localScale != visualScale)
        {
            monkeyVisualRoot.localScale = visualScale;
        }
    }

    private void UpdateVisualPositionAndLockToCamera(float dt)
    {
        bool isSliding = (motor != null && motor.CurrentState == PlayerMotor.PlayerState.Sliding) ||
                         (slideHandler != null && slideHandler.IsSliding);

        bool isHangingOrTransfer = motor != null &&
                                   (motor.CurrentState == PlayerMotor.PlayerState.Hanging ||
                                    motor.CurrentState == PlayerMotor.PlayerState.Transferring);

        float targetSlideOffset = isSliding ? slideExtraVerticalOffset : 0f;
        currentSlideVerticalOffset = Mathf.Lerp(currentSlideVerticalOffset, targetSlideOffset, positionSmoothSpeed * dt);

        float targetHangOffset = isHangingOrTransfer ? hangingExtraVerticalOffset : 0f;
        currentHangingVerticalOffset = Mathf.Lerp(currentHangingVerticalOffset, targetHangOffset, positionSmoothSpeed * dt);

        Vector3 baseLocalPos = CalculateTargetLocalPosition(currentSlideVerticalOffset + currentHangingVerticalOffset);
        monkeyVisualRoot.localPosition = baseLocalPos;

        // FirstPerson modunda yürürken/koşarken animasyon maymunun boynunu/kafasını ileri uzattığında
        // modeli otomatik olarak sabit kameranın hemen altına ve arkasına hizala:
        if (currentAppliedViewMode == ViewMode.FirstPerson && lockBodyUnderCameraInFirstPerson)
        {
            Transform playerRoot = motor != null ? motor.transform : transform;
            Vector3 steadyCamLocal = GetSteadyEyeCameraLocalPosition();

            bool hasAnchor = false;
            Vector3 currentAnchorLocal = Vector3.zero;

            if (headBone != null && neckBone != null)
            {
                Vector3 headLocal = playerRoot.InverseTransformPoint(headBone.position);
                Vector3 neckLocal = playerRoot.InverseTransformPoint(neckBone.position);
                currentAnchorLocal = new Vector3(
                    (headLocal.x + neckLocal.x) * 0.5f,
                    Mathf.Max(headLocal.y, neckLocal.y),
                    Mathf.Max(headLocal.z, neckLocal.z)
                );
                hasAnchor = true;
            }
            else if (headBone != null)
            {
                currentAnchorLocal = playerRoot.InverseTransformPoint(headBone.position);
                hasAnchor = true;
            }
            else if (neckBone != null)
            {
                currentAnchorLocal = playerRoot.InverseTransformPoint(neckBone.position);
                hasAnchor = true;
            }
            else if (eyeMeshRenderer != null)
            {
                currentAnchorLocal = playerRoot.InverseTransformPoint(eyeMeshRenderer.bounds.center);
                hasAnchor = true;
            }

            if (hasAnchor)
            {
                float desiredNeckX = steadyCamLocal.x;
                float desiredNeckZ = steadyCamLocal.z - firstPersonNeckBehindCameraOffset;

                float shiftX = desiredNeckX - currentAnchorLocal.x;
                float shiftZ = desiredNeckZ - currentAnchorLocal.z;
                float shiftY = 0f;

                float maxAllowedNeckY = steadyCamLocal.y - firstPersonNeckBelowCameraMinOffset;
                if (currentAnchorLocal.y > maxAllowedNeckY)
                {
                    shiftY = maxAllowedNeckY - currentAnchorLocal.y;
                }

                Vector3 shiftInPlayer = new Vector3(shiftX, shiftY, shiftZ);
                if (monkeyVisualRoot.parent != null && monkeyVisualRoot.parent != playerRoot)
                {
                    Vector3 shiftWorld = playerRoot.TransformVector(shiftInPlayer);
                    monkeyVisualRoot.localPosition = baseLocalPos + monkeyVisualRoot.parent.InverseTransformVector(shiftWorld);
                }
                else
                {
                    monkeyVisualRoot.localPosition = baseLocalPos + shiftInPlayer;
                }
            }
        }
    }

    private Vector3 CalculateTargetLocalPosition(float extraSlideY)
    {
        float footBaseY = 0f;
        if (autoAlignFeetToControllerBottom && controller != null)
        {
            footBaseY = controller.center.y - (controller.height * 0.5f);
        }

        return new Vector3(
            visualOffset.x,
            footBaseY + visualOffset.y + extraSlideY,
            visualOffset.z
        );
    }

    private void UpdateVisualRotation(float dt)
    {
        Transform parentTransform = monkeyVisualRoot.parent != null ? monkeyVisualRoot.parent : transform;
        Quaternion targetLocalRotation = Quaternion.identity;

        PlayerMotor.PlayerState state = motor != null ? motor.CurrentState : PlayerMotor.PlayerState.Grounded;

        float targetSlidePitch = (state == PlayerMotor.PlayerState.Sliding) ? slideVisualPitchDegrees : 0f;
        currentSlidePitchOffset = Mathf.Lerp(currentSlidePitchOffset, targetSlidePitch, rotationSmoothSpeed * dt);

        if (state == PlayerMotor.PlayerState.WallRunning && wallRunHandler != null)
        {
            Vector3 wallNormal = wallRunHandler.ActiveWallNormal;
            if (wallNormal.sqrMagnitude > 0.001f)
            {
                Vector3 wallForward = Vector3.Cross(wallNormal, Vector3.up);
                if (Vector3.Dot(wallForward, parentTransform.forward) < 0f)
                {
                    wallForward = -wallForward;
                }

                if (wallForward.sqrMagnitude > 0.001f)
                {
                    Quaternion worldWallRot = Quaternion.LookRotation(wallForward.normalized, Vector3.up);
                    Quaternion localWallRot = Quaternion.Inverse(parentTransform.rotation) * worldWallRot;

                    float rollSign = wallRunHandler.IsWallRight ? 1f : -1f;
                    Quaternion tiltRot = Quaternion.Euler(0f, 0f, rollSign * wallRunVisualTiltDegrees);
                    targetLocalRotation = localWallRot * tiltRot;
                }
            }
        }
        else if (state == PlayerMotor.PlayerState.Hanging && branchGrabHandler != null)
        {
            float swingDeg = branchGrabHandler.SwingAngleDegrees * swingVisualAngleMultiplier;
            Vector3 swingFwdWorld = branchGrabHandler.SwingForwardDirection;

            if (swingFwdWorld.sqrMagnitude > 0.001f)
            {
                Vector3 localSwingFwd = parentTransform.InverseTransformDirection(swingFwdWorld.normalized);
                Vector3 localSwingAxis = Vector3.Cross(Vector3.up, localSwingFwd).normalized;
                if (localSwingAxis.sqrMagnitude > 0.001f)
                {
                    targetLocalRotation = Quaternion.AngleAxis(-swingDeg, localSwingAxis);
                }
            }
            else
            {
                targetLocalRotation = Quaternion.Euler(-swingDeg, 0f, 0f);
            }
        }
        else if (state == PlayerMotor.PlayerState.Sliding)
        {
            targetLocalRotation = Quaternion.Euler(currentSlidePitchOffset, 0f, 0f);
        }

        currentDynamicRotation = Quaternion.Slerp(currentDynamicRotation, targetLocalRotation, rotationSmoothSpeed * dt);
        monkeyVisualRoot.localRotation = currentDynamicRotation * Quaternion.Euler(visualRotationOffset);
    }
}
