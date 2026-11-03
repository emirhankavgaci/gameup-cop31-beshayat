using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class GameOutcomeController3D : MonoBehaviour
{
    [Header("System References")]
    [SerializeField] private WaterLevelController waterController;
    [SerializeField] private DamBuilderController3D damBuilder;
    [SerializeField] private PlayerMovement3D playerMovement;

    [Header("Next Scene Transition Settings")]
    [Tooltip("Next scene name to load after the win screen.")]
    [SerializeField] private string nextSceneName = "Hasan";

    [Tooltip("Minimum wait time (in seconds) before allowing skip after the win screen is displayed.")]
    [SerializeField] private float minReadTimeBeforeSkip = 2.0f;

    [Tooltip("Maximum time (in seconds) to wait for automatic scene transition, regardless of user input.")]
    [SerializeField] private float autoTransitionDelay = 20.0f;

    [Header("Win Screen Settings")]
    [Tooltip("Optional: Pre-prepared win black screen panel (if left empty, a dynamic black screen will be created).")]
    [SerializeField] private GameObject winBlackScreenPanel;

    [Tooltip("Optional: TMP component where the story text will be displayed.")]
    [SerializeField] private TMP_Text winStoryText;

    [Tooltip("Optional: Button to transition to the next scene.")]
    [SerializeField] private Button winContinueButton;

    [Tooltip("Message to display on the win screen.")]
    [TextArea(5, 12)]
    [SerializeField] private string winStoryMessage =
        "Across the world, rising temperatures are drying up the rivers that beavers call home, forcing nature's smallest engineers to build dams out of human trash just to survive another day. Plastic only delays the drought, but true restoration brings the water back to life. Today, you helped one beaver hold back the dry season and proved that our rivers are still worth fighting for. When we replace waste with care, we don't just save a stream—we build hope.";

    [Header("Win & Lose Screens")]
    [SerializeField] private GameObject winPanel; 
    [SerializeField] private GameObject losePanel;

    [Header("In-Game HUD Interface (Optional)")]
    [SerializeField] private Slider waterBarSlider;
    [SerializeField] private Image waterBarFillImage;
    [SerializeField] private TMP_Text waterPercentText;
    [SerializeField] private TMP_Text damProgressText;
    [SerializeField] private TMP_Text cornerControlsHintText; 

    [Header("Corner Controls Hint Text")]
    [SerializeField] [TextArea(2, 4)] private string defaultControlsHint =
        "WASD: Movement  |  Shift: Run  |  Space: Jump  |  Mouse: Look Around  |  Left Mouse Button (Hold): Carry";

    [Header("Scene-Specific 60 FPS & Automatic 30 FPS Fallback (Does Not Affect Other Scenes)")]
    [SerializeField] private bool enableSceneFpsControl = true;
    [SerializeField] private int preferredFps = 60;
    [SerializeField] private int fallbackFps = 30;
    [SerializeField] private float lowFpsThreshold = 45f; // If FPS drops below this threshold for 2.5 seconds, fallback to 30 FPS to prevent stuttering.

    [Header("Shortcut Settings")]
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
            // To prevent stuttering when switching to another teammate's scene, store the previous FPS/VSync settings and restore them when leaving this scene.
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

        //  restore the previous FPS/VSync settings when leaving this scene to avoid affecting other scenes.
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

        // If the win screen is active, handle user input for skipping or transitioning to the next scene.
        if (isWinScreenActive)
        {
            float elapsedSinceWin = Time.unscaledTime - winStartTime;

            // Minimum read time has passed, so the player can skip to the next scene with Space or Enter
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

            // When the auto-transition delay has passed, automatically load the next scene
            if (autoTransitionDelay > 0f && elapsedSinceWin >= autoTransitionDelay)
            {
                LoadNextScene();
                return;
            }

            return; // Do not process further updates while the win screen is active
        }

        // If the system can't maintain the preferred FPS, fallback to 30 FPS after a short delay to prevent stuttering.
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

        // 'R' -> restart shortcut for convenience during testing or gameplay
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


    // This method is called when the river is successfully saved (win condition).

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

        HideInGameHUD();

        ShowWinBlackScreen();
    }


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

        // Create a dynamic win black screen canvas if no pre-prepared panel is assigned.
        GameObject canvasObj = new GameObject("WinBlackScreenCanvas");
        dynamicWinCanvas = canvasObj.AddComponent<Canvas>();
        dynamicWinCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        dynamicWinCanvas.sortingOrder = 9999;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // Fullscreen black background
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

        // Story Text
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

        // "Continue" Button
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

        // Button Text
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

        // Hint Text
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
        hintTmp.text = "Continue by clicking the button or pressing the [SPACE] key";
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

    //loads the next screen by name 
    public void LoadNextScene()
    {
        if (isReloadingScene) return;
        isReloadingScene = true;

        UnsubscribeEvents(); // remove all event subscriptions to avoid memory leaks or unintended behavior
        TrashPickup3D.ResetStaticEvents();

        // restore the previous FPS/VSync settings when leaving this scene to avoid affecting other scenes.
        if (enableSceneFpsControl)
        {
            QualitySettings.vSyncCount = previousVSyncCount;
            Application.targetFrameRate = previousTargetFrameRate;
        }

        Time.timeScale = 1f;

        string targetScene = string.IsNullOrEmpty(nextSceneName) ? "Hasan" : nextSceneName;
        SceneManager.LoadScene(targetScene);
    }


    public void RestartScene() // restart using "R" key
    {
        if (isReloadingScene) return;
        isReloadingScene = true;

        UnsubscribeEvents();
        TrashPickup3D.ResetStaticEvents();

        Time.timeScale = 1f;
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
