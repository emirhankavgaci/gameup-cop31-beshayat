using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CANOPY: The Last Run - Yakalanma, Ölüm Sayacı, Süre Takibi, Respawn, Ekran Kararması / Su Altı Flaşı
/// ve "Kurtuldun" Oyun Sonu Ekranı Yöneticisi.
/// Sahnede UI Image / Canvas atanmamışsa çalışma zamanında (runtime) otomatik oluşturur.
/// </summary>
public class GameFlowManager : MonoBehaviour
{
    private const float DEFAULT_UNDERWATER_MAX_ALPHA = 0.40f;

    [Header("Temel Sistem Referansları")]
    [Tooltip("Oyuncu hareket ve state yöneticisi.")]
    [SerializeField] private PlayerMotor playerMotor;

    [Tooltip("Arkadan gelen sel yöneticisi.")]
    [SerializeField] private FloodChaser floodChaser;

    [Tooltip("Checkpoint kayıt yöneticisi.")]
    [SerializeField] private CheckpointManager checkpointManager;

    [Tooltip("Kamera sarsıntı yöneticisi.")]
    [SerializeField] private CameraShake cameraShake;

    [Header("Yakalanma ve Respawn Ayarları")]
    [Tooltip("Yakalanınca ekran kararması ve son checkpoint'e ışınlanma arasındaki bekleme süresi (varsayılan 1.2 sn).")]
    [SerializeField] private float respawnDelay = 1.2f;

    [Tooltip("Respawn sonrasında selin oyuncunun ne kadar gerisine yerleşeceği (varsayılan 60 m).")]
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

    // Bitiş ekranı için OnGUI stilleri
    private GUIStyle victoryBoxStyle;
    private GUIStyle victoryTitleStyle;
    private GUIStyle victoryStatStyle;
    private GUIStyle victoryButtonStyle;

    // Dış erişim özellikleri (FloodDebugHUD için)
    public int DeathCount => deathCount;
    public float ElapsedTime => elapsedTime;
    public bool IsRespawning => isRespawning;
    public bool IsLevelCompleted => isLevelCompleted;
    public float RespawnFloodDistance => respawnFloodDistance;

    private void Awake()
    {
        // Sahnede eski 40m değeri kayıtlı kaldıysa otomatik 60m'ye güncelle
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
        if (!isLevelCompleted)
        {
            elapsedTime += Time.deltaTime;
        }

        // R tuşuyla (debug) son checkpoint'e dönme
        if (Input.GetKeyDown(debugRespawnKey) && !isRespawning)
        {
            if (isLevelCompleted)
            {
                RestartFromLastCheckpoint(resetStats: false);
            }
            else
            {
                RespawnImmediatelyAtLastCheckpoint();
            }
        }
    }

    private void HandlePlayerCaught()
    {
        if (isRespawning || isLevelCompleted)
            return;

        deathCount++;
        StartCoroutine(RespawnSequenceCoroutine());
    }

    /// <summary>
    /// Kaktüs veya diken gibi bir engele çarpıldığında oyuncuyu son checkpoint'e gönderir.
    /// </summary>
    public void TriggerObstacleRespawn(bool instantTeleport = true)
    {
        if (isRespawning || isLevelCompleted)
            return;

        deathCount++;

        if (cameraShake != null)
        {
            cameraShake.TriggerShakePulse(0.75f);
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

        // Oyuncu yakalandığında kontrolü dondur
        if (playerMotor != null)
        {
            playerMotor.SetVelocity(Vector3.zero);
            playerMotor.SetCharacterControllerEnabled(false);
        }

        // 1. Kısa mavi su altı flaşı (%40 opaklık) + ekranın siyaha kararması (1.2 sn toplam süre)
        float halfDelay = Mathf.Max(0.2f, respawnDelay);
        float elapsed = 0f;

        while (elapsed < halfDelay)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / halfDelay);

            // İlk anda hızlıca %40 mavi su altı tonu belirir
            float underwaterAlpha = Mathf.Sin(Mathf.Clamp01(normalizedTime * 1.4f) * Mathf.PI * 0.5f) * underwaterOverlayColor.a;
            SetImageAlpha(underwaterFlashImage, underwaterOverlayColor, underwaterAlpha);

            // Ekran siyaha kararır
            float blackAlpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.15f, halfDelay * 0.75f)));
            SetImageAlpha(blackFadeImage, Color.black, blackAlpha);

            yield return null;
        }

        // 2. 1.2 sn sonunda oyuncuyu son checkpoint'e ışınla, hızı ve state'i sıfırla, seli geri çek
        PerformTeleportAndReset();

        // Kısa bir kare fizik senkronizasyonu bekle
        yield return null;

        // 3. Ekranı yumuşakça geri aç
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

        isRespawning = false;
    }

    /// <summary>
    /// R tuşuna basıldığında beklemeden son checkpoint'e döndürür ve seli respawnFloodDistance kadar geriye çeker.
    /// </summary>
    public void RespawnImmediatelyAtLastCheckpoint()
    {
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
    /// FinishTrigger'a ulaşıldığında çağrılır: seli durdurur ve "Kurtuldun" ekranını açar.
    /// </summary>
    public void CompleteLevel()
    {
        if (isLevelCompleted)
            return;

        isLevelCompleted = true;

        if (floodChaser != null)
        {
            floodChaser.StopFlood();
        }

        if (cameraShake != null)
        {
            cameraShake.ClearOffsets();
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestartFromLastCheckpoint(bool resetStats)
    {
        isLevelCompleted = false;
        if (resetStats)
        {
            deathCount = 0;
            elapsedTime = 0f;
        }

        FinishTrigger[] finishTriggers = FindObjectsOfType<FinishTrigger>();
        for (int i = 0; i < finishTriggers.Length; i++)
        {
            finishTriggers[i].ResetFinishTrigger();
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        RespawnImmediatelyAtLastCheckpoint();
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

    private void OnGUI()
    {
        if (!isLevelCompleted)
            return;

        EnsureVictoryGuiStyles();

        float boxW = 460f;
        float boxH = 260f;
        float boxX = (Screen.width - boxW) * 0.5f;
        float boxY = (Screen.height - boxH) * 0.5f;

        GUI.Box(new Rect(boxX, boxY, boxW, boxH), GUIContent.none, victoryBoxStyle);

        GUILayout.BeginArea(new Rect(boxX + 24f, boxY + 22f, boxW - 48f, boxH - 44f));

        GUILayout.Label("KURTULDUN!", victoryTitleStyle);
        GUILayout.Space(14f);

        GUILayout.Label($"Toplam Süre: <b>{FormatElapsedTime(elapsedTime)}</b>", victoryStatStyle);
        GUILayout.Space(6f);
        GUILayout.Label($"Ölüm Sayısı: <b>{deathCount}</b>", victoryStatStyle);
        GUILayout.Space(22f);

        if (GUILayout.Button("Son Checkpoint'ten Devam Et [R]", victoryButtonStyle, GUILayout.Height(36f)))
        {
            RestartFromLastCheckpoint(resetStats: false);
        }

        GUILayout.EndArea();
    }

    private void EnsureVictoryGuiStyles()
    {
        if (victoryBoxStyle != null)
            return;

        victoryBoxStyle = new GUIStyle(GUI.skin.box);

        victoryTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        victoryTitleStyle.normal.textColor = new Color(0.35f, 1f, 0.55f, 1f);

        victoryStatStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            richText = true,
            alignment = TextAnchor.MiddleCenter
        };
        victoryStatStyle.normal.textColor = Color.white;

        victoryButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold
        };
    }
}

