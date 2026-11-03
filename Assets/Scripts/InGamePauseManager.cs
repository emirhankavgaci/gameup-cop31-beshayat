using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Oyun icinde (Emirhan, Hasan, Ozlem, Zeynep) ESC tusuna basildiginda oyunu duraklatan (pause),
/// imleci serbest birakan ve tas arkaplanli Settings (Muzik ve Ses Efekti) menusunu acan merkezi yonetici.
/// </summary>
public class InGamePauseManager : MonoBehaviour
{
    public static InGamePauseManager Instance { get; private set; }

    [Header("Sprite Varliklari")]
    [SerializeField] private Sprite settingsPanelSprite;
    [SerializeField] private Sprite closeButtonSprite;
    [SerializeField] private Sprite musicIconSprite;
    [SerializeField] private Sprite soundIconSprite;
    [SerializeField] private Sprite sliderLineSprite;
    [SerializeField] private Sprite sliderKnobSprite;

    private Canvas pauseCanvas;
    private GameObject pauseRootObj;
    private GameObject settingsPanelObj;
    private Slider musicSlider;
    private Slider sfxSlider;

    private bool isPaused = false;
    public bool IsPaused => isPaused;

    private const string PATH_SETTINGS_PANEL = "Assets/Models/ui/settings_panel.png";
    private const string PATH_CLOSE = "Assets/Models/ui/close_button.png";
    private const string PATH_MUSIC_ICON = "Assets/Models/ui/music_symbol.png";
    private const string PATH_SOUND_ICON = "Assets/Models/ui/sound_symbol.png";
    private const string PATH_LINE = "Assets/Models/ui/line.png";
    private const string PATH_KNOB = "Assets/Models/ui/voice_control_point.png";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        EnsureInstance();
    }

    public static void EnsureInstance()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("[InGamePauseManager]");
            Instance = go.AddComponent<InGamePauseManager>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        GameAudioManager.EnsureInitialized();
        LoadSprites();
        BuildPauseUI();

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Yeni sahne yuklendiginde duraklatmayi kaldir
        if (isPaused)
        {
            ResumeGame(false);
        }
        else
        {
            if (pauseRootObj != null)
                pauseRootObj.SetActive(false);
        }

        EnsureEventSystem();
    }

    private bool IsGameplayScene()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        // Main Menu'de in-game pause menusu devre disidir (Main Menu'nun kendi ayarlari vardir)
        if (currentScene.IndexOf("Main Menu", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            currentScene.IndexOf("MainMenu", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        return true;
    }

    private void Update()
    {
        if (!IsGameplayScene())
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                ResumeGame(true);
            }
            else
            {
                PauseGame();
            }
        }
    }

    public void PauseGame()
    {
        if (!IsGameplayScene() || isPaused)
            return;

        isPaused = true;
        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnsureEventSystem();

        if (musicSlider != null)
            musicSlider.value = GameAudioManager.MusicVolume;

        if (sfxSlider != null)
            sfxSlider.value = GameAudioManager.SFXVolume;

        if (pauseRootObj != null)
            pauseRootObj.SetActive(true);
    }

    public void ResumeGame(bool restoreCursor = true)
    {
        isPaused = false;
        Time.timeScale = 1f;

        if (pauseRootObj != null)
            pauseRootObj.SetActive(false);

        if (restoreCursor)
        {
            string currentScene = SceneManager.GetActiveScene().name;
            // Birinci sahis veya serbest bakis sahnelerinde (Hasan ve Emirhan) imleci tekrar kilitle
            if (currentScene.IndexOf("Hasan", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                currentScene.IndexOf("Emirhan", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() == null && FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject esObj = new GameObject("InGameEventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();
            DontDestroyOnLoad(esObj);
        }
    }

    private void LoadSprites()
    {
        if (settingsPanelSprite == null) settingsPanelSprite = LoadSprite("ui/settings_panel", PATH_SETTINGS_PANEL);
        if (closeButtonSprite == null) closeButtonSprite = LoadSprite("ui/close_button", PATH_CLOSE);
        if (musicIconSprite == null) musicIconSprite = LoadSprite("ui/music_symbol", PATH_MUSIC_ICON);
        if (soundIconSprite == null) soundIconSprite = LoadSprite("ui/sound_symbol", PATH_SOUND_ICON);
        if (sliderLineSprite == null) sliderLineSprite = LoadSprite("ui/line", PATH_LINE);
        if (sliderKnobSprite == null) sliderKnobSprite = LoadSprite("ui/voice_control_point", PATH_KNOB);
    }

    private Sprite LoadSprite(string resourcePath, string assetPath)
    {
        Sprite s = Resources.Load<Sprite>(resourcePath);
        if (s != null) return s;

#if UNITY_EDITOR
        s = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (s != null) return s;
#endif

        if (File.Exists(assetPath))
        {
            try
            {
                byte[] data = File.ReadAllBytes(assetPath);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(data))
                {
                    tex.filterMode = FilterMode.Bilinear;
                    return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }
            catch {}
        }

        return null;
    }

    private void BuildPauseUI()
    {
        GameObject canvasObj = new GameObject("InGamePauseCanvas");
        canvasObj.transform.SetParent(transform, false);

        pauseCanvas = canvasObj.AddComponent<Canvas>();
        pauseCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        pauseCanvas.sortingOrder = 32767;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        pauseRootObj = new GameObject("PauseRoot");
        pauseRootObj.transform.SetParent(canvasObj.transform, false);

        RectTransform rootRect = pauseRootObj.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;

        // Karartma Perdesi (Dim Backdrop)
        GameObject backdropObj = new GameObject("DimBackdrop");
        backdropObj.transform.SetParent(pauseRootObj.transform, false);

        RectTransform backdropRect = backdropObj.AddComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.sizeDelta = Vector2.zero;

        Image backdropImg = backdropObj.AddComponent<Image>();
        backdropImg.color = new Color(0f, 0f, 0f, 0.45f);

        // Tas Settings Paneli
        settingsPanelObj = new GameObject("SettingsPanel");
        settingsPanelObj.transform.SetParent(pauseRootObj.transform, false);

        RectTransform panelRect = settingsPanelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(660f, 490f);

        Image panelImg = settingsPanelObj.AddComponent<Image>();
        if (settingsPanelSprite != null)
        {
            panelImg.sprite = settingsPanelSprite;
            panelImg.preserveAspect = true;
        }
        else
        {
            panelImg.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);
        }

        // Kapatma Butonu ("X" - Sag Ust)
        GameObject closeBtnObj = new GameObject("CloseButton");
        closeBtnObj.transform.SetParent(settingsPanelObj.transform, false);

        RectTransform closeRect = closeBtnObj.AddComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(0.5f, 0.5f);
        closeRect.anchoredPosition = new Vector2(-55f, -55f);
        closeRect.sizeDelta = new Vector2(60f, 60f);

        Image closeImg = closeBtnObj.AddComponent<Image>();
        if (closeButtonSprite != null)
        {
            closeImg.sprite = closeButtonSprite;
            closeImg.preserveAspect = true;
        }
        else
        {
            closeImg.color = Color.red;
        }

        Button closeBtn = closeBtnObj.AddComponent<Button>();
        closeBtn.onClick.AddListener(() => ResumeGame(true));

        // Muzik Satiri
        musicSlider = CreateVolumeRow(
            settingsPanelObj.transform,
            "MusicRow",
            musicIconSprite,
            new Vector2(60f, 65f),
            new Vector2(0f, 35f),
            GameAudioManager.MusicVolume,
            (val) => GameAudioManager.SetMusicVolume(val)
        );

        // Ses Efekti Satiri
        sfxSlider = CreateVolumeRow(
            settingsPanelObj.transform,
            "SFXRow",
            soundIconSprite,
            new Vector2(70f, 60f),
            new Vector2(0f, -65f),
            GameAudioManager.SFXVolume,
            (val) => GameAudioManager.SetSFXVolume(val)
        );

        pauseRootObj.SetActive(false);
    }

    private Slider CreateVolumeRow(
        Transform parent,
        string rowName,
        Sprite iconSprite,
        Vector2 iconSize,
        Vector2 rowAnchoredPos,
        float initialValue,
        UnityEngine.Events.UnityAction<float> onValueChanged)
    {
        GameObject rowObj = new GameObject(rowName);
        rowObj.transform.SetParent(parent, false);

        RectTransform rowRect = rowObj.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.anchoredPosition = rowAnchoredPos;
        rowRect.sizeDelta = new Vector2(460f, 70f);

        // Ikon (Solda)
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(rowObj.transform, false);

        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(40f, 0f);
        iconRect.sizeDelta = iconSize;

        Image iconImg = iconObj.AddComponent<Image>();
        if (iconSprite != null)
        {
            iconImg.sprite = iconSprite;
            iconImg.preserveAspect = true;
        }

        // Slider (Sagda)
        GameObject sliderObj = new GameObject("Slider");
        sliderObj.transform.SetParent(rowObj.transform, false);

        RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0.5f);
        sliderRect.anchorMax = new Vector2(1f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.anchoredPosition = new Vector2(45f, 0f);
        sliderRect.sizeDelta = new Vector2(-110f, 26f);

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = initialValue;

        // Background Track (line.png)
        GameObject bgTrackObj = new GameObject("BackgroundTrack");
        bgTrackObj.transform.SetParent(sliderObj.transform, false);

        RectTransform bgTrackRect = bgTrackObj.AddComponent<RectTransform>();
        bgTrackRect.anchorMin = Vector2.zero;
        bgTrackRect.anchorMax = Vector2.one;
        bgTrackRect.sizeDelta = Vector2.zero;

        Image bgTrackImg = bgTrackObj.AddComponent<Image>();
        if (sliderLineSprite != null)
        {
            bgTrackImg.sprite = sliderLineSprite;
            bgTrackImg.type = Image.Type.Sliced;
        }
        else
        {
            bgTrackImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        }

        // Handle Slide Area
        GameObject handleAreaObj = new GameObject("Handle Slide Area");
        handleAreaObj.transform.SetParent(sliderObj.transform, false);

        RectTransform handleAreaRect = handleAreaObj.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(12f, 0f);
        handleAreaRect.offsetMax = new Vector2(-12f, 0f);

        // Handle (voice_control_point.png)
        GameObject handleObj = new GameObject("Handle");
        handleObj.transform.SetParent(handleAreaObj.transform, false);

        RectTransform handleRect = handleObj.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(36f, 36f);

        Image handleImg = handleObj.AddComponent<Image>();
        if (sliderKnobSprite != null)
        {
            handleImg.sprite = sliderKnobSprite;
            handleImg.preserveAspect = true;
        }
        else
        {
            handleImg.color = Color.white;
        }

        slider.targetGraphic = handleImg;
        slider.handleRect = handleRect;

        if (onValueChanged != null)
            slider.onValueChanged.AddListener(onValueChanged);

        return slider;
    }
}

