using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// CANOPY: The Last Run - Arkadan Gelen Sel Kovalamacası (Flood Chaser) Yöneticisi.
/// Waypoint yolu (pathPoints) boyunca ilerler, oyuncunun yol üzerindeki izdüşümünü (playerProgress)
/// ve selin konumunu (floodProgress) hesaplar, Rubber Banding uygular, bölge hız geçişlerini yönetir,
/// ses/kamera sarsıntısını sürer ve yakalanma (OnPlayerCaught) durumunu algılar.
/// Final sekansında (EndingSequence) TeleportBehindPlayer(distance) ile oyuncunun arkasına ışınlanır.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class FloodChaser : MonoBehaviour
{
    private const float MIN_SEGMENT_LENGTH = 0.001f;
    private const float DEFAULT_CAMERA_MIN_FAR_CLIP = 600f;

    [Header("Hedef ve Yol (Waypoint) Referansları")]
    [Tooltip("Takip edilen oyuncu Transform'u (boşsa PlayerMotor üzerinden otomatik bulunur).")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("Oyuncunun baş yüksekliği ofseti (su seviyesi altında kalma kontrolü için, metre).")]
    [SerializeField] private float playerHeadHeightOffset = 1.6f;

    [Tooltip("Selin level boyunca takip edeceği sıralı waypoint noktaları.")]
    [SerializeField] private Transform[] pathPoints;

    [Tooltip("Selin dönüşlerde bir sonraki waypoint yönüne yumuşak dönme hızı (derece/sn veya Slerp hızı).")]
    [SerializeField] private float rotationSmoothSpeed = 6f;

    [Header("Sel Boyutları ve Dalga Duvarı Ayarları")]
    [Tooltip("Selin ön yüzündeki dalga duvarının genişliği (varsayılan 30 m).")]
    [SerializeField] private float waveWallWidth = 30f;

    [Tooltip("Selin ön yüzündeki dalga duvarının yüksekliği (varsayılan 12 m).")]
    [SerializeField] private float waveWallHeight = 12f;

    [Tooltip("Selin arkaya doğru uzanan su zemininin uzunluğu (varsayılan 60 m).")]
    [SerializeField] private float waterBodyLength = 60f;

    [Tooltip("Su üst yüzeyinin (su seviyesinin) sel objesi merkezine göre dikey Y ofseti (metre).")]
    [SerializeField] private float waterSurfaceLocalY = 4.5f;

    [Tooltip("Başlangıçta BoxCollider trigger boyutunu dalga duvarı ve su gövdesine göre otomatik ayarla.")]
    [SerializeField] private bool autoConfigureTriggerBounds = true;

    [Tooltip("İsteğe bağlı: Ön dalga duvarı çocuk objesi (boyut/konum referansı için).")]
    [SerializeField] private Transform frontWaveWallVisual;

    [Tooltip("İsteğe bağlı: Arka su zemini çocuk objesi.")]
    [SerializeField] private Transform rearWaterFloorVisual;

    [Header("Hız ve Başlangıç Gecikmesi")]
    [Tooltip("Selin temel ilerleme hızı (m/s). Bölgeye göre ZoneManager tarafından değiştirilir.")]
    [SerializeField] private float baseSpeed = 6f;

    [Tooltip("Oyun başında selin harekete geçmeden önce bekleyeceği hazırlanma süresi (varsayılan 4 sn).")]
    [SerializeField] private float gameStartDelay = 4f;

    [Tooltip("Selin başlangıçta yol üzerindeki ilerleme konumu (metre, negatif değer yolun gerisinden başlatır).")]
    [SerializeField] private float initialFloodProgress = -35f;

    [Header("Rubber Banding (Mesafeye Göre Hız Dengeleme)")]
    [Tooltip("Oyuncu bu mesafeden (varsayılan 45 m) daha uzaktaysa sel hızlanır (catchUpMultiplier).")]
    [SerializeField] private float farDistance = 45f;

    [Tooltip("Oyuncu bu mesafeden (varsayılan 8 m) daha yakındaysa sel yavaşlar (slowDownMultiplier).")]
    [SerializeField] private float nearDistance = 8f;

    [Tooltip("Oyuncu çok açıldığında (> farDistance) uygulanacak hızlanma çarpanı (varsayılan 1.5).")]
    [SerializeField] private float catchUpMultiplier = 1.5f;

    [Tooltip("Oyuncu çok yaklaştığında (< nearDistance) nefes alma payı için yavaşlama çarpanı (varsayılan 0.8).")]
    [SerializeField] private float slowDownMultiplier = 0.8f;

    [Tooltip("Rubber band çarpanının değişim yumuşatma hızı.")]
    [SerializeField] private float multiplierLerpSpeed = 5f;

    [Header("Ses ve Kamera Sarsıntısı Efektleri")]
    [Tooltip("Yakınlığa göre kamera sarsıntısı uygulayan CameraShake bileşeni (boşsa otomatik bulunur).")]
    [SerializeField] private CameraShake cameraShake;

    [Tooltip("Kamera sarsıntısının başlayacağı maksimum sel-oyuncu mesafesi (varsayılan 25 m).")]
    [SerializeField] private float shakeTriggerDistance = 25f;

    [Tooltip("Sel uğultu/dalga sesini çalan AudioSource bileşeni (opsiyonel).")]
    [SerializeField] private AudioSource floodAudioSource;

    [Tooltip("Sesin maksimum yüksekliğe (1.0) ulaştığı mesafe (varsayılan 0 m).")]
    [SerializeField] private float audioNearDistance = 0f;

    [Tooltip("Sesin minimum yüksekliğe (0.1) düştüğü mesafe (varsayılan 60 m).")]
    [SerializeField] private float audioFarDistance = 60f;

    [Tooltip("Mesafe 0 iken sel ses yüksekliği (varsayılan 1.0).")]
    [Range(0f, 1f)]
    [SerializeField] private float maxAudioVolume = 1.0f;

    [Tooltip("Mesafe >= 60 m iken sel ses yüksekliği (varsayılan 0.1).")]
    [Range(0f, 1f)]
    [SerializeField] private float minAudioVolume = 0.1f;

    [Header("Olaylar (Events)")]
    [Tooltip("Oyuncu sele yakalandığında tetiklenen UnityEvent.")]
    [SerializeField] private UnityEvent onPlayerCaughtEvent;

    /// <summary>
    /// Selin ön yüz trigger'ı oyuncuya değdiğinde veya oyuncunun başı su seviyesinin altına girdiğinde tetiklenir.
    /// </summary>
    public event Action OnPlayerCaught;

    // Dahili yol ve ilerleme değişkenleri
    private float[] cumulativePathDistances;
    private float totalPathLength;
    private Vector3 fallbackStartPos;
    private Vector3 fallbackForwardDir = Vector3.forward;

    private float floodProgress;
    private float playerProgress;
    private float distanceToPlayer;
    private int currentFloodSegmentIndex;
    private int currentPlayerSegmentIndex;

    // Hız ve durum değişkenleri
    private float currentSpeed;
    private float currentRubberBandMultiplier = 1f;
    private float speedBlendStartValue;
    private float speedBlendTargetValue;
    private float speedBlendDuration;
    private float speedBlendElapsed;
    private bool isBlendingBaseSpeed;

    private float startDelayTimer;
    private bool isFloodStopped;
    private bool hasTriggeredCatch;
    private bool disableRubberBanding;
    private Vector3 currentFlowDirection = Vector3.forward;
    private BoxCollider floodTriggerCollider;

    // Dış erişim özellikleri (FloodDebugHUD, ZoneManager, EndingSequence ve GameFlowManager için)
    public float CurrentSpeed => currentSpeed;
    public float BaseSpeed => baseSpeed;
    public float CurrentRubberBandMultiplier => currentRubberBandMultiplier;
    public float FloodProgress => floodProgress;
    public float PlayerProgress => playerProgress;
    public float DistanceToPlayer => distanceToPlayer;
    public float FarDistance => farDistance;
    public float NearDistance => nearDistance;
    public float RemainingStartDelay => Mathf.Max(0f, startDelayTimer);
    public bool IsWaitingForStartDelay => startDelayTimer > 0f;
    public bool IsStopped => isFloodStopped;
    public bool IsActive => !isFloodStopped && startDelayTimer <= 0f;
    public float CurrentWaterSurfaceY => transform.position.y + waterSurfaceLocalY;
    public Vector3 CurrentFlowDirection => currentFlowDirection.sqrMagnitude > 0.001f ? currentFlowDirection : transform.forward;

    private void Awake()
    {
        floodTriggerCollider = GetComponent<BoxCollider>();
        if (floodTriggerCollider != null)
        {
            floodTriggerCollider.isTrigger = true;
            if (autoConfigureTriggerBounds)
            {
                ConfigureTriggerColliderBounds();
            }
        }

        if (playerTransform == null)
        {
            PlayerMotor motor = FindObjectOfType<PlayerMotor>();
            if (motor != null)
            {
                playerTransform = motor.transform;
            }
        }

        if (cameraShake == null)
        {
            cameraShake = FindObjectOfType<CameraShake>();
        }

        if (floodAudioSource == null)
        {
            floodAudioSource = GetComponent<AudioSource>();
        }

        fallbackStartPos = transform.position;
        fallbackForwardDir = transform.forward.sqrMagnitude > 0.001f ? transform.forward.normalized : Vector3.forward;

        RebuildPathCache();
        EnsureCameraFarClipSufficient();
    }

    private void Start()
    {
        startDelayTimer = Mathf.Max(0f, gameStartDelay);
        speedBlendTargetValue = baseSpeed;
        floodProgress = initialFloodProgress;

        UpdatePlayerProgressOnPath();
        ApplyFloodTransformFromProgress(instantRotation: true);
        distanceToPlayer = playerProgress - floodProgress;
        UpdateAudioAndCameraShake();
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // 1. Oyuncunun yol üzerindeki ilerleme izdüşümünü her kare güncelle
        UpdatePlayerProgressOnPath();

        // 2. Bölge hız geçişi (SetBaseSpeed blend) varsa işle
        UpdateBaseSpeedBlend(dt);

        // 3. Başlangıç hazırlanma gecikmesi (gameStartDelay) veya durdurulma kontrolü
        if (isFloodStopped)
        {
            currentSpeed = 0f;
            distanceToPlayer = playerProgress - floodProgress;
            UpdateAudioAndCameraShake();
            return;
        }

        if (startDelayTimer > 0f)
        {
            startDelayTimer -= dt;
            currentSpeed = 0f;
            distanceToPlayer = playerProgress - floodProgress;
            UpdateAudioAndCameraShake();
            return;
        }

        // 4. Rubber Banding çarpanını mesafeye göre yumuşakça hesapla (EndingActive iken kapalıdır)
        distanceToPlayer = playerProgress - floodProgress;
        if (disableRubberBanding || GameFlowManager.EndingActive)
        {
            currentRubberBandMultiplier = 1f;
        }
        else
        {
            float targetMultiplier = CalculateRubberBandMultiplier(distanceToPlayer);
            currentRubberBandMultiplier = Mathf.Lerp(currentRubberBandMultiplier, targetMultiplier, multiplierLerpSpeed * dt);
        }

        // 5. Sel hızını ve ilerlemesini güncelle
        currentSpeed = baseSpeed * currentRubberBandMultiplier;
        floodProgress += currentSpeed * dt;

        if (HasValidWaypointPath() && floodProgress > totalPathLength && !GameFlowManager.EndingActive)
        {
            floodProgress = totalPathLength;
        }

        ApplyFloodTransformFromProgress(instantRotation: false);
        distanceToPlayer = playerProgress - floodProgress;

        // 6. Ses ve Kamera sarsıntısını güncelle
        UpdateAudioAndCameraShake();

        // 7. Su seviyesi altında kalma veya dalga duvarının oyuncuyu geçmesi kontrolü
        CheckSubmersionAndOverrunCatch();
    }

    private float CalculateRubberBandMultiplier(float distance)
    {
        if (distance >= farDistance)
        {
            return catchUpMultiplier;
        }

        if (distance <= nearDistance)
        {
            return slowDownMultiplier;
        }

        float normalizedRange = Mathf.InverseLerp(nearDistance, farDistance, distance);
        return Mathf.Lerp(slowDownMultiplier, catchUpMultiplier, normalizedRange);
    }

    /// <summary>
    /// Bölge değişimlerinde veya final sekansında selin temel hızını yumuşak geçişle değiştirir.
    /// </summary>
    public void SetBaseSpeed(float speed, float blendTime)
    {
        float clampedTarget = Mathf.Max(0f, speed);
        if (blendTime <= 0.01f)
        {
            baseSpeed = clampedTarget;
            speedBlendTargetValue = clampedTarget;
            isBlendingBaseSpeed = false;
            return;
        }

        speedBlendStartValue = baseSpeed;
        speedBlendTargetValue = clampedTarget;
        speedBlendDuration = blendTime;
        speedBlendElapsed = 0f;
        isBlendingBaseSpeed = true;
    }

    private void UpdateBaseSpeedBlend(float dt)
    {
        if (!isBlendingBaseSpeed)
            return;

        speedBlendElapsed += dt;
        float t = Mathf.Clamp01(speedBlendElapsed / Mathf.Max(0.01f, speedBlendDuration));
        float smoothT = Mathf.SmoothStep(0f, 1f, t);
        baseSpeed = Mathf.Lerp(speedBlendStartValue, speedBlendTargetValue, smoothT);

        if (t >= 1f)
        {
            baseSpeed = speedBlendTargetValue;
            isBlendingBaseSpeed = false;
        }
    }

    public void RebuildPathCache()
    {
        if (!HasValidWaypointPath())
        {
            cumulativePathDistances = null;
            totalPathLength = 0f;
            return;
        }

        int count = pathPoints.Length;
        cumulativePathDistances = new float[count];
        cumulativePathDistances[0] = 0f;
        float runningLength = 0f;

        for (int i = 1; i < count; i++)
        {
            if (pathPoints[i - 1] != null && pathPoints[i] != null)
            {
                runningLength += Vector3.Distance(pathPoints[i - 1].position, pathPoints[i].position);
            }
            cumulativePathDistances[i] = runningLength;
        }

        totalPathLength = runningLength;
    }

    private bool HasValidWaypointPath()
    {
        if (pathPoints == null || pathPoints.Length < 2)
            return false;

        for (int i = 0; i < pathPoints.Length; i++)
        {
            if (pathPoints[i] == null)
                return false;
        }

        return true;
    }

    public void UpdatePlayerProgressOnPath()
    {
        if (playerTransform == null)
            return;

        Vector3 playerPos = playerTransform.position;

        if (!HasValidWaypointPath())
        {
            playerProgress = Vector3.Dot(playerPos - fallbackStartPos, fallbackForwardDir);
            currentPlayerSegmentIndex = 0;
            return;
        }

        if (cumulativePathDistances == null || cumulativePathDistances.Length != pathPoints.Length)
        {
            RebuildPathCache();
        }

        float closestDistSqr = float.MaxValue;
        float bestProgress = 0f;
        int bestSegIndex = 0;

        for (int i = 0; i < pathPoints.Length - 1; i++)
        {
            Vector3 segStart = pathPoints[i].position;
            Vector3 segEnd = pathPoints[i + 1].position;
            Vector3 segVec = segEnd - segStart;
            float segLen = segVec.magnitude;

            if (segLen < MIN_SEGMENT_LENGTH)
                continue;

            Vector3 segDir = segVec / segLen;
            float projection = Vector3.Dot(playerPos - segStart, segDir);

            float clampedProj = (i == 0)
                ? Mathf.Min(projection, segLen)
                : (i == pathPoints.Length - 2 ? Mathf.Max(0f, projection) : Mathf.Clamp(projection, 0f, segLen));

            Vector3 closestPointOnSeg = segStart + segDir * Mathf.Clamp(projection, 0f, segLen);
            float distSqr = (playerPos - closestPointOnSeg).sqrMagnitude;

            if (distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                bestProgress = cumulativePathDistances[i] + clampedProj;
                bestSegIndex = i;
            }
        }

        playerProgress = bestProgress;
        currentPlayerSegmentIndex = bestSegIndex;
    }

    private void ApplyFloodTransformFromProgress(bool instantRotation)
    {
        EvaluatePathAtProgress(floodProgress, out Vector3 targetPos, out Vector3 targetForward, out currentFloodSegmentIndex);
        transform.position = targetPos;

        if (targetForward.sqrMagnitude > 0.001f)
        {
            currentFlowDirection = targetForward.normalized;
            Quaternion targetRot = Quaternion.LookRotation(currentFlowDirection, Vector3.up);
            if (instantRotation)
            {
                transform.rotation = targetRot;
            }
            else
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSmoothSpeed * Time.deltaTime);
            }
        }
    }

    public void EvaluatePathAtProgress(float progress, out Vector3 worldPoint, out Vector3 forwardDirection, out int segmentIndex)
    {
        if (!HasValidWaypointPath())
        {
            worldPoint = fallbackStartPos + fallbackForwardDir * progress;
            forwardDirection = fallbackForwardDir;
            segmentIndex = 0;
            return;
        }

        if (cumulativePathDistances == null || cumulativePathDistances.Length != pathPoints.Length)
        {
            RebuildPathCache();
        }

        if (progress <= 0f)
        {
            Vector3 p0 = pathPoints[0].position;
            Vector3 p1 = pathPoints[1].position;
            Vector3 dir = (p1 - p0);
            forwardDirection = dir.sqrMagnitude > MIN_SEGMENT_LENGTH ? dir.normalized : fallbackForwardDir;
            worldPoint = p0 + forwardDirection * progress;
            segmentIndex = 0;
            return;
        }

        int lastIdx = pathPoints.Length - 1;
        if (progress >= totalPathLength)
        {
            Vector3 pPrev = pathPoints[lastIdx - 1].position;
            Vector3 pLast = pathPoints[lastIdx].position;
            Vector3 dir = (pLast - pPrev);
            forwardDirection = dir.sqrMagnitude > MIN_SEGMENT_LENGTH ? dir.normalized : fallbackForwardDir;
            float extra = progress - totalPathLength;
            worldPoint = pLast + forwardDirection * extra;
            segmentIndex = lastIdx - 1;
            return;
        }

        for (int i = 0; i < lastIdx; i++)
        {
            float segStartDist = cumulativePathDistances[i];
            float segEndDist = cumulativePathDistances[i + 1];

            if (progress >= segStartDist && progress <= segEndDist)
            {
                float segLen = Mathf.Max(MIN_SEGMENT_LENGTH, segEndDist - segStartDist);
                float t = (progress - segStartDist) / segLen;
                Vector3 pA = pathPoints[i].position;
                Vector3 pB = pathPoints[i + 1].position;
                worldPoint = Vector3.Lerp(pA, pB, t);
                Vector3 dir = (pB - pA);
                forwardDirection = dir.sqrMagnitude > MIN_SEGMENT_LENGTH ? dir.normalized : fallbackForwardDir;
                segmentIndex = i;
                return;
            }
        }

        worldPoint = pathPoints[0].position;
        forwardDirection = fallbackForwardDir;
        segmentIndex = 0;
    }

    private void UpdateAudioAndCameraShake()
    {
        float absDistance = Mathf.Max(0f, distanceToPlayer);

        if (cameraShake != null)
        {
            if (!isFloodStopped && startDelayTimer <= 0f && absDistance < shakeTriggerDistance)
            {
                float proximity = 1f - Mathf.Clamp01(absDistance / Mathf.Max(1f, shakeTriggerDistance));
                cameraShake.SetContinuousShakeIntensity(proximity);
            }
            else if (!GameFlowManager.EndingActive)
            {
                cameraShake.SetContinuousShakeIntensity(0f);
            }
        }

        if (floodAudioSource != null)
        {
            float t = Mathf.InverseLerp(audioNearDistance, Mathf.Max(audioNearDistance + 1f, audioFarDistance), absDistance);
            float targetVolume = Mathf.Lerp(maxAudioVolume, minAudioVolume, t);
            if (isFloodStopped)
            {
                targetVolume = minAudioVolume * 0.5f;
            }

            floodAudioSource.volume = targetVolume;
            if (!floodAudioSource.isPlaying && floodAudioSource.clip != null && gameObject.activeInHierarchy)
            {
                floodAudioSource.loop = true;
                floodAudioSource.Play();
            }
        }
    }

    private void CheckSubmersionAndOverrunCatch()
    {
        if (hasTriggeredCatch || isFloodStopped || playerTransform == null)
            return;

        float playerHeadY = playerTransform.position.y + playerHeadHeightOffset;
        bool isWithinFloodLongitudinalRange = distanceToPlayer <= 1.5f && distanceToPlayer >= -waterBodyLength;

        if (isWithinFloodLongitudinalRange && playerHeadY <= CurrentWaterSurfaceY)
        {
            TriggerPlayerCaught();
            return;
        }

        if (distanceToPlayer <= -0.5f)
        {
            Vector3 localPlayerPos = transform.InverseTransformPoint(playerTransform.position);
            bool withinWidth = Mathf.Abs(localPlayerPos.x) <= (waveWallWidth * 0.65f);
            bool withinHeight = localPlayerPos.y <= (waveWallHeight * 0.75f);

            if (withinWidth && withinHeight || GameFlowManager.EndingActive)
            {
                TriggerPlayerCaught();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        CheckColliderForPlayerCatch(other);
    }

    private void OnTriggerStay(Collider other)
    {
        CheckColliderForPlayerCatch(other);
    }

    private void CheckColliderForPlayerCatch(Collider other)
    {
        if (hasTriggeredCatch || isFloodStopped || other == null)
            return;

        bool isPlayer = (playerTransform != null && (other.transform == playerTransform || other.transform.IsChildOf(playerTransform)))
                        || other.GetComponent<PlayerMotor>() != null
                        || other.CompareTag("Player");

        if (isPlayer)
        {
            TriggerPlayerCaught();
        }
    }

    public void TriggerPlayerCaught()
    {
        if (hasTriggeredCatch || isFloodStopped)
            return;

        hasTriggeredCatch = true;

        if (cameraShake != null)
        {
            cameraShake.TriggerShakePulse(0.9f);
        }

        onPlayerCaughtEvent?.Invoke();
        OnPlayerCaught?.Invoke();
    }

    /// <summary>
    /// Oyun sonu sekansında (EndingSequence) seli anında yol üzerinde oyuncunun belirtilen mesafe (varsayılan 10 m)
    /// gerisine ışınlar ve bekleme süresi olmadan kovalamacayı sürdürür.
    /// </summary>
    public void TeleportBehindPlayer(float distance)
    {
        UpdatePlayerProgressOnPath();
        floodProgress = playerProgress - Mathf.Max(1f, distance);
        currentRubberBandMultiplier = 1f;
        startDelayTimer = 0f;
        isFloodStopped = false;
        hasTriggeredCatch = false;

        ApplyFloodTransformFromProgress(instantRotation: true);
        distanceToPlayer = playerProgress - floodProgress;
        UpdateAudioAndCameraShake();
    }

    /// <summary>
    /// Rubber banding (mesafeye göre hız dengeleme) sistemini açar veya kapatır.
    /// Final sekansı (EndingSequence) sırasında kapalı tutulur.
    /// </summary>
    public void SetRubberBandingEnabled(bool enabledRubberBanding)
    {
        disableRubberBanding = !enabledRubberBanding;
        if (disableRubberBanding)
        {
            currentRubberBandMultiplier = 1f;
        }
    }

    /// <summary>
    /// Respawn sırasında seli oyuncunun belirtilen mesafe (varsayılan 80 m) gerisine yerleştirir.
    /// </summary>
    public void RepositionBehindPlayer(float distanceBehindPlayer, float respawnGraceDelay = 0.5f)
    {
        UpdatePlayerProgressOnPath();
        floodProgress = playerProgress - Mathf.Max(5f, distanceBehindPlayer);
        currentRubberBandMultiplier = 1f;
        currentSpeed = 0f;
        startDelayTimer = Mathf.Max(0f, respawnGraceDelay);
        isFloodStopped = false;
        hasTriggeredCatch = false;

        ApplyFloodTransformFromProgress(instantRotation: true);
        distanceToPlayer = playerProgress - floodProgress;
        UpdateAudioAndCameraShake();
    }

    public void StopFlood()
    {
        isFloodStopped = true;
        currentSpeed = 0f;
        if (cameraShake != null)
        {
            cameraShake.ClearOffsets();
        }
    }

    public void ResumeFlood()
    {
        hasTriggeredCatch = false;
        isFloodStopped = false;
    }

    private void ConfigureTriggerColliderBounds()
    {
        if (floodTriggerCollider == null)
            return;

        floodTriggerCollider.size = new Vector3(waveWallWidth, waveWallHeight, waterBodyLength);
        floodTriggerCollider.center = new Vector3(0f, waveWallHeight * 0.25f, -waterBodyLength * 0.5f);
    }

    private void EnsureCameraFarClipSufficient()
    {
        // Sel mesh'lerinin oyuncu arkasına baktığında frustum culling nedeniyle erken kaybolmaması için
        // dinamik occlusion'ı kapat (Kamera farClipPlane değeri PlayerLook 50m sis sınırı tarafından yönetilir)
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].allowOcclusionWhenDynamic = false;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (pathPoints != null && pathPoints.Length >= 2)
        {
            Gizmos.color = new Color(0.1f, 0.85f, 1f, 0.9f);
            for (int i = 0; i < pathPoints.Length; i++)
            {
                if (pathPoints[i] == null)
                    continue;

                Gizmos.DrawSphere(pathPoints[i].position, 0.65f);

                if (i < pathPoints.Length - 1 && pathPoints[i + 1] != null)
                {
                    Vector3 a = pathPoints[i].position;
                    Vector3 b = pathPoints[i + 1].position;
                    Gizmos.DrawLine(a, b);

                    Vector3 mid = (a + b) * 0.5f;
                    Vector3 dir = (b - a).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
                    Gizmos.DrawLine(mid, mid - dir * 1.5f + right * 0.7f);
                    Gizmos.DrawLine(mid, mid - dir * 1.5f - right * 0.7f);
                }
            }
        }

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.45f);
        Vector3 wallCenter = new Vector3(0f, waveWallHeight * 0.25f, 0f);
        Vector3 wallSize = new Vector3(waveWallWidth, waveWallHeight, 0.6f);
        Gizmos.DrawCube(wallCenter, wallSize);
        Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.95f);
        Gizmos.DrawWireCube(wallCenter, wallSize);

        Gizmos.color = new Color(0.1f, 0.55f, 0.95f, 0.2f);
        Vector3 bodyCenter = new Vector3(0f, waveWallHeight * 0.25f, -waterBodyLength * 0.5f);
        Vector3 bodySize = new Vector3(waveWallWidth, waveWallHeight, waterBodyLength);
        Gizmos.DrawCube(bodyCenter, bodySize);
        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.7f);
        Gizmos.DrawWireCube(bodyCenter, bodySize);

        Gizmos.color = Color.cyan;
        Vector3 waterSurfaceCenter = new Vector3(0f, waterSurfaceLocalY, -waterBodyLength * 0.5f);
        Gizmos.DrawWireCube(waterSurfaceCenter, new Vector3(waveWallWidth, 0.05f, waterBodyLength));

        Gizmos.matrix = oldMatrix;
    }
}
