using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Unity 6 (6000.x) uyumlu, Kazanma ("Korku Değil Ümit") ve Kaybetme (Nehir Kurudu) durumlarını,
/// Su Miktarı Barını, 5x4 Baraj İlerleme Arayüzünü, Köşe Kontrol Rehberini,
/// Sadece Bu Sahneye Özel 60 FPS (Düşük Sistemde Otomatik 30 FPS Fallback) Ayarını,
/// Kazanma durumunda tam ekran siyah ekran gösterip ("Across the world...") ardından 'Hasan' sahnesine geçişi ve
/// Sahne Yeniden Başlatmayı yöneten kontrolcü.
/// </summary>
public class GameOutcomeController3D : MonoBehaviour
{
    [Header("Sistem Referansları (Boş Bırakılırsa Otomatik Bulunur)")]
    [SerializeField] private WaterLevelController waterController;
    [SerializeField] private DamBuilderController3D damBuilder;
    [SerializeField] private PlayerMovement3D playerMovement;

    [Header("Sonraki Sahne Geçiş Ayarları")]
    [Tooltip("Kazanma ekranından sonra yüklenecek hedef sahne.")]
    [SerializeField] private string nextSceneName = "Hasan";

    [Tooltip("Kazanma ekranı açıldıktan sonra tuşla geçişin aktifleşeceği minimum bekleme süresi (sn).")]
    [SerializeField] private float minReadTimeBeforeSkip = 2.0f;

    [Tooltip("Kullanıcı herhangi bir tuşa basmasa dahi otomatik sahne geçişi için beklenecek maksimum süre (sn).")]
    [SerializeField] private float autoTransitionDelay = 20.0f;

    [Header("Kazanma Siyah Ekran Ayarları")]
    [Tooltip("Opsiyonel: Önceden hazırlanmış kazanma siyah ekran paneli (Boş bırakılırsa dinamik siyah ekran oluşturulur).")]
    [SerializeField] private GameObject winBlackScreenPanel;

    [Tooltip("Opsiyonel: Hikaye metninin yazılacağı TMP bileşeni.")]
    [SerializeField] private TMP_Text winStoryText;

    [Tooltip("Opsiyonel: Sonraki sahneye geçiş butonu.")]
    [SerializeField] private Button winContinueButton;

    [Tooltip("Kazanıldığında siyah ekranda gösterilecek mesaj.")]
    [TextArea(5, 12)]
    [SerializeField] private string winStoryMessage =
        "Across the world, rising temperatures are drying up the rivers that beavers call home, forcing nature's smallest engineers to build dams out of human trash just to survive another day. Plastic only delays the drought, but true restoration brings the water back to life. Today, you helped one beaver hold back the dry season and proved that our rivers are still worth fighting for. When we replace waste with care, we don't just save a stream—we build hope.";

    [Header("Kazanma & Kaybetme Ekranları (Eski / Fallback UI)")]
    [SerializeField] private GameObject winPanel;  // Eski UI paneli referansı
    [SerializeField] private GameObject losePanel; // Su tamamen bittiğinde açılacak "Kaybetme" paneli

    [Header("Oyun İçi HUD Arayüzü (Opsiyonel)")]
    [SerializeField] private Slider waterBarSlider;
    [SerializeField] private Image waterBarFillImage;
    [SerializeField] private TMP_Text waterPercentText;
    [SerializeField] private TMP_Text damProgressText;
    [SerializeField] private TMP_Text cornerControlsHintText; // Köşedeki sabit mini kontrol rehberi

    [Header("Köşe Kontrol Rehberi Metni")]
    [SerializeField] [TextArea(2, 4)] private string defaultControlsHint =
        "WASD: Movement  |  Shift: Run  |  Space: Jump  |  Mouse: Look Around  |  Left Mouse Button (Hold): Carry";

    [Header("Sahneye Özel 60 FPS & Otomatik 30 FPS Fallback (Diğer Sahneleri Etkilemez)")]
    [SerializeField] private bool enableSceneFpsControl = true;
    [SerializeField] private int preferredFps = 60;
    [SerializeField] private int fallbackFps = 30;
    [SerializeField] private float lowFpsThreshold = 45f; // Ortalama FPS bu değerin altına düşerse 30 FPS'e sabitler

    [Header("Kısayol Ayarları")]
    [SerializeField] private bool allowRKeyToRestart = true;

    private bool isGameEnded = false;
    private bool isReloadingScene = false;
    private bool isWinScreenActive = false;
    private float winStartTime = 0f;
    private Canvas dynamicWinCanvas;
    private int previousTargetFrameRate;
    private int previousVSyncCount;
    private float smoothedDeltaTime = 0.016f;
    private float lowFpsTimer = 0f;
    private bool hasFallenBackTo30Fps = false;

    public bool IsGameEnded => isGameEnded;

    private void Awake()
    {
        Time.timeScale = 1f;

        if (enableSceneFpsControl)
        {
            // Diğer ekip arkadaşlarının sahnelerini etkilememek için önceki ayarları kaydet
            previousTargetFrameRate = Application.targetFrameRate;
            previousVSyncCount = QualitySettings.vSyncCount;

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = preferredFps;
        }

        if (waterController == null)
            waterController = FindAnyObjectByType<WaterLevelController>();

        if (damBuilder == null)
            damBuilder = FindAnyObjectByType<DamBuilderController3D>();

        if (playerMovement == null)
            playerMovement = FindAnyObjectByType<PlayerMovement3D>();

        if (winPanel != null) winPanel.SetActive(false);
        if (winBlackScreenPanel != null) winBlackScreenPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);

        if (cornerControlsHintText != null && !string.IsNullOrEmpty(defaultControlsHint))
        {
            cornerControlsHintText.text = defaultControlsHint;
        }
    }

    private void OnEnable()
    {
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();

        // Başka bir ekip arkadaşının sahnesine geçilirse eski FPS/VSync ayarlarını geri yükle
        if (enableSceneFpsControl && !isReloadingScene)
        {
            QualitySettings.vSyncCount = previousVSyncCount;
            Application.targetFrameRate = previousTargetFrameRate;
        }
    }

    private void SubscribeEvents()
    {
        if (waterController != null)
        {
            waterController.OnWaterLevelRatioChanged -= HandleWaterRatioChanged;
            waterController.OnWaterLevelRatioChanged += HandleWaterRatioChanged;

            waterController.OnRiverDriedOut -= HandleRiverDriedOut;
            waterController.OnRiverDriedOut += HandleRiverDriedOut;

            waterController.OnRiverSaved -= HandleRiverSaved;
            waterController.OnRiverSaved += HandleRiverSaved;
        }

        if (damBuilder != null)
        {
            damBuilder.OnDamProgressUpdated -= HandleDamProgressUpdated;
            damBuilder.OnDamProgressUpdated += HandleDamProgressUpdated;
        }
    }

    private void UnsubscribeEvents()
    {
        if (waterController != null)
        {
            waterController.OnWaterLevelRatioChanged -= HandleWaterRatioChanged;
            waterController.OnRiverDriedOut -= HandleRiverDriedOut;
            waterController.OnRiverSaved -= HandleRiverSaved;
        }

        if (damBuilder != null)
        {
            damBuilder.OnDamProgressUpdated -= HandleDamProgressUpdated;
        }
    }

    private void Start()
    {
        if (damBuilder != null)
        {
            HandleDamProgressUpdated(
                damBuilder.CurrentPlacedItems,
                damBuilder.TotalRequiredItems,
                damBuilder.CurrentCompletedBlocks
            );
        }
    }

    private void Update()
    {
        if (isReloadingScene) return;

        // Kazanma siyah ekranı aktifse tuş kontrolleri ve otomatik geçiş kontrolü
        if (isWinScreenActive)
        {
            float elapsedSinceWin = Time.unscaledTime - winStartTime;

            // Minimum okuma süresi geçtikten sonra Boşluk veya Enter ile sahne geçişi yapılabilir
            if (elapsedSinceWin >= minReadTimeBeforeSkip)
            {
                bool keyPressed = false;

                if (Keyboard.current != null)
                {
                    keyPressed = Keyboard.current.spaceKey.wasPressedThisFrame ||
                                 Keyboard.current.enterKey.wasPressedThisFrame ||
                                 Keyboard.current.numpadEnterKey.wasPressedThisFrame;
                }
                else
                {
                    keyPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
                }

                if (keyPressed)
                {
                    LoadNextScene();
                    return;
                }
            }

            // Otomatik süre dolduğunda da sonraki sahneye geçiş yap
            if (autoTransitionDelay > 0f && elapsedSinceWin >= autoTransitionDelay)
            {
                LoadNextScene();
                return;
            }

            return; // Kazanma ekranı açıkken diğer oyun içi kontrolleri yürütme
        }

        // Sistem 60 FPS'i kaldıramıyorsa (2.5 saniye boyunca 45 FPS altında kalırsa) dalgalanmayı önlemek için 30 FPS'e sabitle
        if (enableSceneFpsControl && !hasFallenBackTo30Fps && Time.timeSinceLevelLoad > 2.0f)
        {
            smoothedDeltaTime += (Time.unscaledDeltaTime - smoothedDeltaTime) * 0.1f;
            float currentFps = 1f / Mathf.Max(0.0001f, smoothedDeltaTime);

            if (currentFps < lowFpsThreshold)
            {
                lowFpsTimer += Time.unscaledDeltaTime;
                if (lowFpsTimer >= 2.5f)
                {
                    hasFallenBackTo30Fps = true;
                    Application.targetFrameRate = fallbackFps;
                }
            }
            else
            {
                lowFpsTimer = Mathf.Max(0f, lowFpsTimer - Time.unscaledDeltaTime);
            }
        }

        // 'R' tuşuna basarak her an (veya oyun bittiğinde) sahneyi hızlıca yeniden başlatabilme
        if (allowRKeyToRestart && Keyboard.current != null)
        {
            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                RestartScene();
            }
        }
    }

    private void HandleWaterRatioChanged(float ratio)
    {
        if (isReloadingScene || this == null) return;

        if (waterBarSlider != null && waterBarSlider.gameObject != null)
        {
            waterBarSlider.value = waterBarSlider.maxValue > 1.5f ? ratio * 100f : ratio;
        }

        if (waterBarFillImage != null && waterBarFillImage.gameObject != null)
        {
            waterBarFillImage.fillAmount = ratio;
        }

        if (waterPercentText != null && waterPercentText.gameObject != null)
        {
            int percent = Mathf.RoundToInt(ratio * 100f);
            waterPercentText.text = $"%{percent}";
        }
    }

    private void HandleDamProgressUpdated(int placedItems, int totalItems, int completedBlocks)
    {
        if (isReloadingScene || this == null) return;

        if (damProgressText != null && damProgressText.gameObject != null)
        {
            damProgressText.text = $"{completedBlocks}/5 Blocks ({placedItems}/{totalItems}) Tiles";
        }
    }

    /// <summary>
    /// 5x4 Baraj tamamlandığında çağrılır: Tam ekran siyah ekranı ve mesajı gösterir.
    /// </summary>
    private void HandleRiverSaved()
    {
        if (isGameEnded || isReloadingScene) return;
        isGameEnded = true;
        isWinScreenActive = true;
        winStartTime = Time.unscaledTime;

        if (playerMovement != null)
        {
            playerMovement.SetCanMove(false);
        }

        UnlockCursorForUI();

        // Oyun içi HUD öğelerini gizle
        HideInGameHUD();

        // Tam ekran siyah ekranı göster
        ShowWinBlackScreen();
    }

    /// <summary>
    /// Nehir suyu tamamen tükendiğinde (Kaybetme durumu) çağrılır.
    /// </summary>
    private void HandleRiverDriedOut()
    {
        if (isGameEnded || isReloadingScene) return;
        isGameEnded = true;

        if (playerMovement != null)
        {
            playerMovement.SetCanMove(false);
        }

        UnlockCursorForUI();

        if (losePanel != null)
        {
            losePanel.SetActive(true);
        }
    }

    private void HideInGameHUD()
    {
        if (waterBarSlider != null && waterBarSlider.gameObject != null)
            waterBarSlider.gameObject.SetActive(false);

        if (waterPercentText != null && waterPercentText.gameObject != null)
            waterPercentText.gameObject.SetActive(false);

        if (damProgressText != null && damProgressText.gameObject != null)
            damProgressText.gameObject.SetActive(false);

        if (cornerControlsHintText != null && cornerControlsHintText.gameObject != null)
            cornerControlsHintText.gameObject.SetActive(false);

        if (winPanel != null && winPanel != winBlackScreenPanel)
            winPanel.SetActive(false);

        if (losePanel != null)
            losePanel.SetActive(false);
    }

    private void ShowWinBlackScreen()
    {
        if (winBlackScreenPanel != null)
        {
            winBlackScreenPanel.SetActive(true);

            if (winStoryText != null)
            {
                winStoryText.text = winStoryMessage;
            }

            if (winContinueButton != null)
            {
                winContinueButton.onClick.RemoveListener(LoadNextScene);
                winContinueButton.onClick.AddListener(LoadNextScene);
            }
            return;
        }

        if (dynamicWinCanvas != null)
        {
            dynamicWinCanvas.gameObject.SetActive(true);
            return;
        }

        // Dinamik Tam Ekran Siyah Canvas Oluştur
        GameObject canvasObj = new GameObject("WinBlackScreenCanvas");
        dynamicWinCanvas = canvasObj.AddComponent<Canvas>();
        dynamicWinCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        dynamicWinCanvas.sortingOrder = 9999;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // Tam Ekran Siyah Arka Plan
        GameObject bgObj = new GameObject("BlackBackground");
        bgObj.transform.SetParent(canvasObj.transform, false);

        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = Color.black;
        bgImage.raycastTarget = true;

        TMP_FontAsset fontAsset = TMP_Settings.defaultFontAsset;

        // Hikaye Metni
        GameObject textObj = new GameObject("StoryText");
        textObj.transform.SetParent(canvasObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = new Vector2(0f, 50f);
        textRect.sizeDelta = new Vector2(1360f, 520f);

        TextMeshProUGUI storyTmp = textObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) storyTmp.font = fontAsset;
        storyTmp.text = winStoryMessage;
        storyTmp.fontSize = 28f;
        storyTmp.color = new Color(0.94f, 0.95f, 0.96f, 1f);
        storyTmp.alignment = TextAlignmentOptions.Center;
        storyTmp.textWrappingMode = TextWrappingModes.Normal;
        storyTmp.lineSpacing = 16f;
        storyTmp.raycastTarget = false;

        // "DEVAM ET" Butonu
        GameObject btnObj = new GameObject("ContinueButton");
        btnObj.transform.SetParent(canvasObj.transform, false);

        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = new Vector2(0f, -330f);
        btnRect.sizeDelta = new Vector2(280f, 65f);

        Image btnImage = btnObj.AddComponent<Image>();
        btnImage.color = new Color(0.20f, 0.24f, 0.28f, 1f);

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = new Color(0.20f, 0.24f, 0.28f, 1f);
        colors.highlightedColor = new Color(0.28f, 0.48f, 0.38f, 1f);
        colors.pressedColor = new Color(0.14f, 0.32f, 0.24f, 1f);
        colors.selectedColor = new Color(0.28f, 0.48f, 0.38f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(LoadNextScene);

        // Buton Metni
        GameObject btnTextObj = new GameObject("ButtonText");
        btnTextObj.transform.SetParent(btnObj.transform, false);

        RectTransform btnTextRect = btnTextObj.AddComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;

        TextMeshProUGUI btnTmp = btnTextObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) btnTmp.font = fontAsset;
        btnTmp.text = "DEVAM ET";
        btnTmp.fontSize = 22f;
        btnTmp.fontStyle = FontStyles.Bold;
        btnTmp.color = Color.white;
        btnTmp.alignment = TextAlignmentOptions.Center;
        btnTmp.raycastTarget = false;

        // İpucu Metni
        GameObject hintObj = new GameObject("HintText");
        hintObj.transform.SetParent(canvasObj.transform, false);

        RectTransform hintRect = hintObj.AddComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.5f, 0.5f);
        hintRect.anchorMax = new Vector2(0.5f, 0.5f);
        hintRect.pivot = new Vector2(0.5f, 0.5f);
        hintRect.anchoredPosition = new Vector2(0f, -400f);
        hintRect.sizeDelta = new Vector2(600f, 30f);

        TextMeshProUGUI hintTmp = hintObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) hintTmp.font = fontAsset;
        hintTmp.text = "Devam etmek için butona tıklayın veya Boşluk (Space) tuşuna basın";
        hintTmp.fontSize = 15f;
        hintTmp.fontStyle = FontStyles.Italic;
        hintTmp.color = new Color(0.6f, 0.65f, 0.7f, 0.8f);
        hintTmp.alignment = TextAlignmentOptions.Center;
        hintTmp.raycastTarget = false;
    }

    private void UnlockCursorForUI()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// Kazanma ekranından sonra 'Hasan' adlı sahneye geçişi gerçekleştirir.
    /// UI üzerindeki 'DEVAM ET' butonu veya tuş kısayolu ile tetiklenir.
    /// </summary>
    public void LoadNextScene()
    {
        if (isReloadingScene) return;
        isReloadingScene = true;

        // Sahne yüklenmeden önce tüm event aboneliklerini ve static eventleri temizle
        UnsubscribeEvents();
        TrashPickup3D.ResetStaticEvents();

        // Başka bir ekip arkadaşının sahnesine geçildiğinde eski FPS/VSync ayarlarını geri yükle
        if (enableSceneFpsControl)
        {
            QualitySettings.vSyncCount = previousVSyncCount;
            Application.targetFrameRate = previousTargetFrameRate;
        }

        Time.timeScale = 1f;

        string targetScene = string.IsNullOrEmpty(nextSceneName) ? "Hasan" : nextSceneName;
        SceneManager.LoadScene(targetScene);
    }

    /// <summary>
    /// UI üzerindeki "Tekrar Dene" butonuna bağlanabilir veya 'R' tuşu ile tetiklenir.
    /// </summary>
    public void RestartScene()
    {
        if (isReloadingScene) return;
        isReloadingScene = true;

        // Sahne yüklenmeden önce tüm event aboneliklerini ve static eventleri temizle
        UnsubscribeEvents();
        TrashPickup3D.ResetStaticEvents();

        Time.timeScale = 1f;
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    /// <summary>
    /// UI üzerindeki "Çıkış" butonuna bağlanabilir.
    /// </summary>
    public void QuitGame()
    {
        Application.Quit();
    }
}
