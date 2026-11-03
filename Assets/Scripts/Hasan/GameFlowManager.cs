using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CANOPY: The Last Run - Yakalanma, Ölüm Sayacı, Süre Takibi, Respawn ve Ekran Kararması / Su Altı Flaşı Yöneticisi.
/// Oyun sonu (EndingSequence) aktif olduğunda EndingActive bayrağı ile respawn ve checkpoint sistemini devre dışı bırakır.
/// Eski "Kurtuldun" OnGUI ekranı kaldırılmıştır.
/// </summary>
public class GameFlowManager : MonoBehaviour
{
    private const float DEFAULT_UNDERWATER_MAX_ALPHA = 0.40f;

    /// <summary>
    /// Final sekansı (EndingSequence) aktifken true olur.
    /// Bu bayrak true iken rubber banding, checkpoint kaydı ve yakalanınca respawn olma devre dışıdır.
    /// </summary>
    public static bool EndingActive { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticEndingFlag()
    {
        EndingActive = false;
    }

    public static void SetEndingActive(bool active)
    {
        EndingActive = active;
    }

    [Header("Temel Sistem Referansları")]
    [Tooltip("Oyuncu hareket ve state yöneticisi.")]
    [SerializeField] private PlayerMotor playerMotor;

    [Tooltip("Arkadan gelen sel yöneticisi.")]
    [SerializeField] private FloodChaser floodChaser;

    [Tooltip("Checkpoint kayıt yöneticisi.")]
    [SerializeField] private CheckpointManager checkpointManager;

    [Tooltip("Kamera sarsıntı yöneticisi.")]
    [SerializeField] private CameraShake cameraShake;

    [Tooltip("Oyun sonu boğulma ve bilgi ekranı sekansı yöneticisi (boşsa otomatik bulunur).")]
    [SerializeField] private EndingSequence endingSequence;

    [Header("Yakalanma ve Respawn Ayarları")]
    [Tooltip("Yakalanınca ekran kararması ve son checkpoint'e ışınlanma arasındaki bekleme süresi (varsayılan 1.2 sn).")]
    [SerializeField] private float respawnDelay = 1.2f;

    [Tooltip("Respawn sonrasında selin oyuncunun ne kadar gerisine yerleşeceği (varsayılan 80 m).")]
    [SerializeField] private float respawnFloodDistance = 80f;

    [Tooltip("Respawn sonrasında selin tekrar harekete geçmeden önce kısa bekleme süresi (sn).")]
    [SerializeField] private float respawnFloodGraceTime = 0.6f;

    [Tooltip("Ekranın siyaha kararma ve açılma geçiş hızı (sn).")]
    [SerializeField] private float fadeOutInDuration = 0.45f;

    [Header("Su Altı Tonu ve Ekran Flaşı (UI Overlay)")]
    [Tooltip("Yakalanma anında beliren mavi su altı tonu rengi (%40 opaklık).")]
    [SerializeField] private Color underwaterOverlayColor = new Color(0.06f, 0.42f, 0.78f, DEFAULT_UNDERWATER_MAX_ALPHA);

    [Tooltip("İsteğe bağlı: Su altı mavi flaş UI Image bileşeni (boşsa otomatik oluşturulur).")]
    [SerializeField] private Image underwaterFlashImage;

    [Tooltip("İsteğe bağlı: Siyah ekran kararma (fade) UI Image bileşeni (boşsa otomatik oluşturulur).")]
    [SerializeField] private Image blackFadeImage;

    [Header("Debug Kontrolleri")]
    [Tooltip("Son checkpoint'e anında dönmek için debug kısayol tuşu (varsayılan R).")]
    [SerializeField] private KeyCode debugRespawnKey = KeyCode.R;

    // Çalışma durumu
    private int deathCount;
    private float elapsedTime;
    private bool isRespawning;
    private bool isLevelCompleted;

    // Dış erişim özellikleri (FloodDebugHUD için)
    public int DeathCount => deathCount;
    public float ElapsedTime => elapsedTime;
    public bool IsRespawning => isRespawning;
    public bool IsLevelCompleted => isLevelCompleted;
    public float RespawnFloodDistance => respawnFloodDistance;

    /// <summary>
    /// Oyuncu sele yakalandığında veya bir engele çarpıp öldüğünde tetiklenir (Ses ve efektler için).
    /// </summary>
    public event System.Action OnPlayerDied;

    private void Awake()
    {
        EndingActive = false;

        if (Mathf.Approximately(respawnFloodDistance, 40f))
        {
            respawnFloodDistance = 80f;
        }

        if (playerMotor == null)
            playerMotor = FindObjectOfType<PlayerMotor>();

        if (floodChaser == null)
            floodChaser = FindObjectOfType<FloodChaser>();

        if (checkpointManager == null)
            checkpointManager = FindObjectOfType<CheckpointManager>();

        if (cameraShake == null)
            cameraShake = FindObjectOfType<CameraShake>();

        if (endingSequence == null)
            endingSequence = FindObjectOfType<EndingSequence>();

        EnsureRuntimeOverlayUI();
    }

    private void OnEnable()
    {
        if (floodChaser != null)
        {
            floodChaser.OnPlayerCaught -= HandlePlayerCaught;
            floodChaser.OnPlayerCaught += HandlePlayerCaught;
        }
    }

    private void OnDisable()
    {
        if (floodChaser != null)
        {
            floodChaser.OnPlayerCaught -= HandlePlayerCaught;
        }
    }

    private void Update()
    {
        if (!isLevelCompleted && !EndingActive)
        {
            elapsedTime += Time.deltaTime;
        }

        // R tuşuyla (debug) son checkpoint'e dönme (EndingActive iken devre dışıdır)
        if (!EndingActive && Input.GetKeyDown(debugRespawnKey) && !isRespawning)
        {
            RespawnImmediatelyAtLastCheckpoint();
        }
    }

    private void HandlePlayerCaught()
    {
        // Final sekansı (EndingActive) sırasında yakalanınca respawn OLMAZ (EndingSequence boğulmayı yönetir)
        if (EndingActive || isRespawning || isLevelCompleted)
            return;

        deathCount++;
        OnPlayerDied?.Invoke();

        if (playerMotor != null)
        {
            playerMotor.SetMovementLocked(true);
        }

        StartCoroutine(RespawnSequenceCoroutine());
    }

    /// <summary>
    /// Kaktüs veya diken gibi bir engele çarpıldığında oyuncuyu son checkpoint'e gönderir.
    /// Final sekansı (EndingActive) sırasında devre dışıdır.
    /// </summary>
    public void TriggerObstacleRespawn(bool instantTeleport = true)
    {
        if (EndingActive || isRespawning || isLevelCompleted)
            return;

        deathCount++;
        OnPlayerDied?.Invoke();

        if (cameraShake != null)
        {
            cameraShake.TriggerShakePulse(0.75f);
        }

        if (playerMotor != null)
        {
            playerMotor.SetMovementLocked(true);
        }

        if (instantTeleport)
        {
            RespawnImmediatelyAtLastCheckpoint();
        }
        else
        {
            StartCoroutine(RespawnSequenceCoroutine());
        }
    }

    private IEnumerator RespawnSequenceCoroutine()
    {
        isRespawning = true;

        if (playerMotor != null)
        {
            playerMotor.SetMovementLocked(true);
            playerMotor.SetVelocity(Vector3.zero);
            playerMotor.SetCharacterControllerEnabled(false);
        }

        float halfDelay = Mathf.Max(0.2f, respawnDelay);
        float elapsed = 0f;

        while (elapsed < halfDelay)
        {
            if (EndingActive)
            {
                SetImageAlpha(underwaterFlashImage, underwaterOverlayColor, 0f);
                SetImageAlpha(blackFadeImage, Color.black, 0f);
                if (playerMotor != null)
                {
                    playerMotor.SetMovementLocked(false);
                }
                isRespawning = false;
                yield break;
            }

            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / halfDelay);

            float underwaterAlpha = Mathf.Sin(Mathf.Clamp01(normalizedTime * 1.4f) * Mathf.PI * 0.5f) * underwaterOverlayColor.a;
            SetImageAlpha(underwaterFlashImage, underwaterOverlayColor, underwaterAlpha);

            float blackAlpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.15f, halfDelay * 0.75f)));
            SetImageAlpha(blackFadeImage, Color.black, blackAlpha);

            yield return null;
        }

        PerformTeleportAndReset();

        yield return null;

        float fadeInElapsed = 0f;
        float fadeInTime = Mathf.Max(0.15f, fadeOutInDuration);
        while (fadeInElapsed < fadeInTime)
        {
            fadeInElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(fadeInElapsed / fadeInTime);
            float remaining = 1f - Mathf.SmoothStep(0f, 1f, t);

            SetImageAlpha(underwaterFlashImage, underwaterOverlayColor, underwaterOverlayColor.a * remaining);
            SetImageAlpha(blackFadeImage, Color.black, remaining);

            yield return null;
        }

        SetImageAlpha(underwaterFlashImage, underwaterOverlayColor, 0f);
        SetImageAlpha(blackFadeImage, Color.black, 0f);

        if (playerMotor != null)
        {
            playerMotor.SetMovementLocked(false);
        }

        isRespawning = false;
    }

    public void RespawnImmediatelyAtLastCheckpoint()
    {
        if (EndingActive)
            return;

        StopAllCoroutines();
        isRespawning = false;
        SetImageAlpha(underwaterFlashImage, underwaterOverlayColor, 0f);
        SetImageAlpha(blackFadeImage, Color.black, 0f);

        PerformTeleportAndReset();
    }

    private void PerformTeleportAndReset()
    {
        Vector3 targetPos = playerMotor != null ? playerMotor.transform.position : Vector3.zero;
        Quaternion targetRot = playerMotor != null ? playerMotor.transform.rotation : Quaternion.identity;

        if (checkpointManager != null && checkpointManager.HasSavedCheckpoint)
        {
            targetPos = checkpointManager.LastCheckpointPosition;
            targetRot = checkpointManager.LastCheckpointRotation;
        }

        if (playerMotor != null)
        {
            playerMotor.TeleportAndResetState(targetPos, targetRot);
        }

        if (cameraShake != null)
        {
            cameraShake.ClearOffsets();
        }

        if (floodChaser != null)
        {
            floodChaser.RepositionBehindPlayer(respawnFloodDistance, respawnFloodGraceTime);
        }
    }

    /// <summary>
    /// FinishTrigger'a ulaşıldığında çağrılır: eski "Kurtuldun" ekranı yerine yeni EndingSequence başlatılır.
    /// </summary>
    public void CompleteLevel()
    {
        if (isLevelCompleted || EndingActive)
            return;

        isLevelCompleted = true;

        if (endingSequence == null)
        {
            endingSequence = FindObjectOfType<EndingSequence>();
            if (endingSequence == null)
            {
                endingSequence = gameObject.AddComponent<EndingSequence>();
            }
        }

        endingSequence.StartEndingSequence();
    }

    private void EnsureRuntimeOverlayUI()
    {
        if (underwaterFlashImage != null && blackFadeImage != null)
        {
            SetImageAlpha(underwaterFlashImage, underwaterOverlayColor, 0f);
            SetImageAlpha(blackFadeImage, Color.black, 0f);
            return;
        }

        GameObject canvasObj = new GameObject("FloodOverlayCanvas_Runtime");
        canvasObj.transform.SetParent(transform, false);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 950;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        if (underwaterFlashImage == null)
        {
            underwaterFlashImage = CreateFullscreenOverlayImage(canvasObj.transform, "UnderwaterBlueOverlay", underwaterOverlayColor);
            SetImageAlpha(underwaterFlashImage, underwaterOverlayColor, 0f);
        }

        if (blackFadeImage == null)
        {
            blackFadeImage = CreateFullscreenOverlayImage(canvasObj.transform, "BlackFadeOverlay", Color.black);
            SetImageAlpha(blackFadeImage, Color.black, 0f);
        }
    }

    private Image CreateFullscreenOverlayImage(Transform parent, string objectName, Color baseColor)
    {
        GameObject imgObj = new GameObject(objectName);
        imgObj.transform.SetParent(parent, false);

        RectTransform rt = imgObj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = imgObj.AddComponent<Image>();
        img.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
        img.raycastTarget = false;
        return img;
    }

    private void SetImageAlpha(Image img, Color baseColor, float alpha)
    {
        if (img == null)
            return;

        img.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Clamp01(alpha));
    }

    public static string FormatElapsedTime(float seconds)
    {
        int mins = Mathf.FloorToInt(seconds / 60f);
        float secs = seconds - (mins * 60f);
        return $"{mins:00}:{secs:00.00}";
    }
}
