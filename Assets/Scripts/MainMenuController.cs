using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// "Main Menu" sahnesinin arayüzünü (1920x1080 Arka Plan, "RE:WILD" Başlığı, Start, Settings, Quit, Ayarlar Penceresi),
/// Inspector üzerinden kolayca değiştirilebilir buton konumlarını ve ses kontrol entegrasyonunu yöneten kontrolcü.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Hedef İlk Sahne")]
    [SerializeField] private string firstGameSceneName = "Zeynep";

    [Header("Oyun Başlığı (Title: RE:WILD) Ayarları")]
    [Tooltip("Ekranda butonların üzerinde yazacak oyun ismi.")]
    [SerializeField] private string titleText = "RE:WILD";

    [Tooltip("Standart TTF/OTF Font dosyası (Örn: Assets/Models/ui/oldtimer/Oldtimer.ttf dosyasını doğrudan buraya sürükleyebilirsiniz).")]
    [SerializeField] private Font titleTtfFont;

    [Tooltip("Başlık için yazı tipi (Font). İstediğiniz herhangi bir TextMeshPro Font Asset'i buraya sürükleyip bırakabilirsiniz.")]
    [SerializeField] private TMP_FontAsset titleFont;

    [Tooltip("Başlık yazı boyutu.")]
    [SerializeField] private float titleFontSize = 96f;

    [Tooltip("Başlık yazı rengi.")]
    [SerializeField] private Color titleColor = new Color(0.96f, 0.97f, 0.98f, 1f);

    [Tooltip("Başlığın ekrandaki konumu (X, Y).")]
    [SerializeField] private Vector2 titlePosition = new Vector2(0f, 250f);

    [Tooltip("Harfler arasındaki boşluk (Sinematik görünüm için).")]
    [SerializeField] private float titleCharacterSpacing = 12f;

    [Header("Buton Konum ve Boyut Ayarları (Buradan İstediğiniz Gibi Değiştirebilirsiniz)")]
    [Tooltip("Tüm buton grubunun ana konumu (X, Y).")]
    [SerializeField] private Vector2 buttonsContainerPosition = new Vector2(0f, -70f);

    [Tooltip("Start (Başla) butonunun konumu.")]
    [SerializeField] private Vector2 startButtonPosition = new Vector2(0f, 105f);

    [Tooltip("Start (Başla) butonunun genişlik ve yükseklik boyutu.")]
    [SerializeField] private Vector2 startButtonSize = new Vector2(390f, 100f);

    [Tooltip("Settings (Ayarlar) butonunun konumu.")]
    [SerializeField] private Vector2 settingsButtonPosition = new Vector2(0f, -10f);

    [Tooltip("Settings (Ayarlar) butonunun genişlik ve yükseklik boyutu.")]
    [SerializeField] private Vector2 settingsButtonSize = new Vector2(393f, 105f);

    [Tooltip("Quit (Çıkış) butonunun konumu.")]
    [SerializeField] private Vector2 quitButtonPosition = new Vector2(0f, -125f);

    [Tooltip("Quit (Çıkış) butonunun genişlik ve yükseklik boyutu.")]
    [SerializeField] private Vector2 quitButtonSize = new Vector2(400f, 113f);

    [Tooltip("Ayarlar (Settings) penceresinin açılacağı konum.")]
    [SerializeField] private Vector2 settingsPanelPosition = new Vector2(0f, 0f);

    [Tooltip("Ayarlar (Settings) penceresinin boyutu.")]
    [SerializeField] private Vector2 settingsPanelSize = new Vector2(660f, 490f);

    [Header("Sprite Varlıkları (Boş bırakılırsa dosyalardan otomatik yüklenir)")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Sprite startSprite;
    [SerializeField] private Sprite settingsSprite;
    [SerializeField] private Sprite quitSprite;
    [SerializeField] private Sprite settingsPanelSprite;
    [SerializeField] private Sprite closeButtonSprite;
    [SerializeField] private Sprite musicIconSprite;
    [SerializeField] private Sprite soundIconSprite;
    [SerializeField] private Sprite sliderLineSprite;
    [SerializeField] private Sprite sliderKnobSprite;

    [Header("Ses Varlıkları")]
    [SerializeField] private AudioClip menuMusicClip;
    [SerializeField] private AudioClip sfxPreviewClip;

    private AudioSource musicAudioSource;
    private AudioSource sfxAudioSource;

    private Canvas menuCanvas;
    private RectTransform titleRect;
    private TextMeshProUGUI titleTmp;
    private RectTransform buttonsContainerRect;
    private RectTransform startButtonRect;
    private RectTransform settingsButtonRect;
    private RectTransform quitButtonRect;
    private RectTransform settingsPanelRect;
    private GameObject settingsPanelObj;
    private Slider musicSlider;
    private Slider sfxSlider;

    private const string PATH_BG = "Assets/Models/Hasan/31231231231321.jpeg";
    private const string PATH_START = "Assets/Models/ui/start.png";
    private const string PATH_SETTINGS = "Assets/Models/ui/settings.png";
    private const string PATH_QUIT = "Assets/Models/ui/quit.png";
    private const string PATH_SETTINGS_PANEL = "Assets/Models/ui/settings_panel.png";
    private const string PATH_CLOSE = "Assets/Models/ui/close_button.png";
    private const string PATH_MUSIC_ICON = "Assets/Models/ui/music_symbol.png";
    private const string PATH_SOUND_ICON = "Assets/Models/ui/sound_symbol.png";
    private const string PATH_LINE = "Assets/Models/ui/line.png";
    private const string PATH_KNOB = "Assets/Models/ui/voice_control_point.png";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureManagerInScene()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene.IndexOf("Main Menu", System.StringComparison.OrdinalIgnoreCase) < 0 &&
            currentScene.IndexOf("MainMenu", System.StringComparison.OrdinalIgnoreCase) < 0)
            return;

        if (FindFirstObjectByType<MainMenuController>() == null)
        {
            GameObject go = new GameObject("MainMenuManager");
            go.AddComponent<MainMenuController>();
        }
    }

    private void Awake()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        GameAudioManager.EnsureInitialized();
        LoadMissingSprites();
        LoadMissingFont();
        LoadAudioClips();
        SetupAudioSources();
        EnsureEventSystem();
        BuildUIHierarchy();
    }

    private void Start()
    {
        if (musicSlider != null)
            musicSlider.value = GameAudioManager.MusicVolume;

        if (sfxSlider != null)
            sfxSlider.value = GameAudioManager.SFXVolume;

        if (musicAudioSource != null && menuMusicClip != null)
        {
            musicAudioSource.clip = menuMusicClip;
            musicAudioSource.loop = true;
            musicAudioSource.volume = GameAudioManager.MusicVolume;
            musicAudioSource.Play();
        }
    }

    private void OnEnable()
    {
        GameAudioManager.OnMusicVolumeChanged += HandleMusicVolumeChanged;
    }

    private void OnDisable()
    {
        GameAudioManager.OnMusicVolumeChanged -= HandleMusicVolumeChanged;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingsPanelObj != null && settingsPanelObj.activeSelf)
            {
                CloseSettings();
            }
        }
    }

    private void OnValidate()
    {
        if (titleTtfFont != null && (titleFont == null || titleFont.name != titleTtfFont.name))
        {
            try
            {
                titleFont = TMP_FontAsset.CreateFontAsset(titleTtfFont);
                if (titleFont != null) titleFont.name = titleTtfFont.name;
            }
            catch {}
        }

        // Inspector'dan konum, boyut veya font değiştirildiğinde canlı güncelle
        ApplyLayoutSettings();
    }

    public void ApplyLayoutSettings()
    {
        if (titleRect != null)
        {
            titleRect.anchoredPosition = titlePosition;
        }

        if (titleTmp != null)
        {
            titleTmp.text = titleText;
            titleTmp.fontSize = titleFontSize;
            titleTmp.color = titleColor;
            titleTmp.characterSpacing = titleCharacterSpacing;
            if (titleFont != null) titleTmp.font = titleFont;
        }

        if (buttonsContainerRect != null)
        {
            buttonsContainerRect.anchoredPosition = buttonsContainerPosition;
        }

        if (startButtonRect != null)
        {
            startButtonRect.anchoredPosition = startButtonPosition;
            startButtonRect.sizeDelta = startButtonSize;
        }

        if (settingsButtonRect != null)
        {
            settingsButtonRect.anchoredPosition = settingsButtonPosition;
            settingsButtonRect.sizeDelta = settingsButtonSize;
        }

        if (quitButtonRect != null)
        {
            quitButtonRect.anchoredPosition = quitButtonPosition;
            quitButtonRect.sizeDelta = quitButtonSize;
        }

        if (settingsPanelRect != null)
        {
            settingsPanelRect.anchoredPosition = settingsPanelPosition;
            settingsPanelRect.sizeDelta = settingsPanelSize;
        }
    }

    private void HandleMusicVolumeChanged(float vol)
    {
        if (musicAudioSource != null)
        {
            musicAudioSource.volume = vol;
        }
    }

    private void SetupAudioSources()
    {
        musicAudioSource = gameObject.AddComponent<AudioSource>();
        musicAudioSource.playOnAwake = false;
        musicAudioSource.spatialBlend = 0f;

        sfxAudioSource = gameObject.AddComponent<AudioSource>();
        sfxAudioSource.playOnAwake = false;
        sfxAudioSource.spatialBlend = 0f;
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();
        }
    }

    private void LoadMissingFont()
    {
        if (titleFont != null) return;

        if (titleTtfFont != null)
        {
            try
            {
                titleFont = TMP_FontAsset.CreateFontAsset(titleTtfFont);
                if (titleFont != null) return;
            }
            catch {}
        }

#if UNITY_EDITOR
        titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Models/ui/oldtimer/Oldtimer SDF.asset");
        if (titleFont != null) return;

        titleTtfFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Models/ui/oldtimer/Oldtimer.ttf");
        if (titleTtfFont != null)
        {
            try
            {
                titleFont = TMP_FontAsset.CreateFontAsset(titleTtfFont);
                if (titleFont != null) return;
            }
            catch {}
        }
#endif

        if (titleFont == null)
        {
            titleFont = TMP_Settings.defaultFontAsset;
        }
    }

    private void LoadMissingSprites()
    {
        if (backgroundSprite == null) backgroundSprite = LoadSprite(PATH_BG);
        if (startSprite == null) startSprite = LoadSprite(PATH_START);
        if (settingsSprite == null) settingsSprite = LoadSprite(PATH_SETTINGS);
        if (quitSprite == null) quitSprite = LoadSprite(PATH_QUIT);
        if (settingsPanelSprite == null) settingsPanelSprite = LoadSprite(PATH_SETTINGS_PANEL);
        if (closeButtonSprite == null) closeButtonSprite = LoadSprite(PATH_CLOSE);
        if (musicIconSprite == null) musicIconSprite = LoadSprite(PATH_MUSIC_ICON);
        if (soundIconSprite == null) soundIconSprite = LoadSprite(PATH_SOUND_ICON);
        if (sliderLineSprite == null) sliderLineSprite = LoadSprite(PATH_LINE);
        if (sliderKnobSprite == null) sliderKnobSprite = LoadSprite(PATH_KNOB);
    }

    private Sprite LoadSprite(string assetPath)
    {
#if UNITY_EDITOR
        Sprite edSprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (edSprite != null) return edSprite;
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
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[MainMenuController] Resim yüklenemedi: {assetPath} -> {ex.Message}");
            }
        }
        return null;
    }

    private void LoadAudioClips()
    {
        if (menuMusicClip == null)
        {
#if UNITY_EDITOR
            menuMusicClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Zeynep/gamemusic.mp3");
            if (menuMusicClip == null)
                menuMusicClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Hasan/gamejamses/musicloop.mp3");
#endif
        }

        if (sfxPreviewClip == null)
        {
#if UNITY_EDITOR
            sfxPreviewClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Emirhan/pickup.mp3");
            if (sfxPreviewClip == null)
                sfxPreviewClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Zeynep/seed.mp3");
#endif
        }
    }

    private void BuildUIHierarchy()
    {
        // 1. Canvas
        GameObject canvasObj = new GameObject("MainMenuCanvas");
        menuCanvas = canvasObj.AddComponent<Canvas>();
        menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        menuCanvas.sortingOrder = 0;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // 2. Fullscreen Background Image
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);

        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        Image bgImage = bgObj.AddComponent<Image>();
        if (backgroundSprite != null)
        {
            bgImage.sprite = backgroundSprite;
            bgImage.type = Image.Type.Simple;
            bgImage.preserveAspect = false;
        }
        else
        {
            bgImage.color = new Color(0.1f, 0.15f, 0.2f, 1f);
        }
        bgImage.raycastTarget = false;

        // 3. Oyun Başlığı ("RE:WILD")
        GameObject titleObj = new GameObject("GameTitle_REWILD");
        titleObj.transform.SetParent(canvasObj.transform, false);

        titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = titlePosition;
        titleRect.sizeDelta = new Vector2(1000f, 160f);

        titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
        if (titleFont != null) titleTmp.font = titleFont;
        titleTmp.text = titleText;
        titleTmp.fontSize = titleFontSize;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = titleColor;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.characterSpacing = titleCharacterSpacing;
        titleTmp.raycastTarget = false;

        // 4. Menü Butonları Konteyneri
        GameObject buttonsContainer = new GameObject("ButtonsContainer");
        buttonsContainer.transform.SetParent(canvasObj.transform, false);

        buttonsContainerRect = buttonsContainer.AddComponent<RectTransform>();
        buttonsContainerRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonsContainerRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonsContainerRect.pivot = new Vector2(0.5f, 0.5f);
        buttonsContainerRect.anchoredPosition = buttonsContainerPosition;
        buttonsContainerRect.sizeDelta = new Vector2(440f, 400f);

        // Start Butonu
        GameObject startBtnObj = CreateMenuButton(
            buttonsContainer.transform,
            "StartButton",
            startSprite,
            startButtonPosition,
            startButtonSize,
            StartGame
        );
        startButtonRect = startBtnObj.GetComponent<RectTransform>();

        // Settings Butonu
        GameObject settingsBtnObj = CreateMenuButton(
            buttonsContainer.transform,
            "SettingsButton",
            settingsSprite,
            settingsButtonPosition,
            settingsButtonSize,
            OpenSettings
        );
        settingsButtonRect = settingsBtnObj.GetComponent<RectTransform>();

        // Quit Butonu
        GameObject quitBtnObj = CreateMenuButton(
            buttonsContainer.transform,
            "QuitButton",
            quitSprite,
            quitButtonPosition,
            quitButtonSize,
            QuitGame
        );
        quitButtonRect = quitBtnObj.GetComponent<RectTransform>();

        // 5. Ayarlar Penceresi (Modal Panel)
        BuildSettingsPanel(canvasObj.transform);

        ApplyLayoutSettings();
    }

    private GameObject CreateMenuButton(
        Transform parent,
        string name,
        Sprite sprite,
        Vector2 anchoredPos,
        Vector2 size,
        UnityEngine.Events.UnityAction onClickAction)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = btnObj.AddComponent<Image>();
        if (sprite != null)
        {
            img.sprite = sprite;
            img.preserveAspect = true;
        }
        else
        {
            img.color = new Color(0.25f, 0.35f, 0.45f, 1f);
        }

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.selectedColor = Color.white;
        btn.colors = colors;

        if (onClickAction != null)
            btn.onClick.AddListener(onClickAction);

        return btnObj;
    }

    private void BuildSettingsPanel(Transform parentCanvas)
    {
        settingsPanelObj = new GameObject("SettingsModal");
        settingsPanelObj.transform.SetParent(parentCanvas, false);

        settingsPanelRect = settingsPanelObj.AddComponent<RectTransform>();
        settingsPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
        settingsPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        settingsPanelRect.pivot = new Vector2(0.5f, 0.5f);
        settingsPanelRect.anchoredPosition = settingsPanelPosition;
        settingsPanelRect.sizeDelta = settingsPanelSize;

        Image panelImg = settingsPanelObj.AddComponent<Image>();
        if (settingsPanelSprite != null)
        {
            panelImg.sprite = settingsPanelSprite;
            panelImg.preserveAspect = true;
        }
        else
        {
            panelImg.color = new Color(0.15f, 0.18f, 0.22f, 0.95f);
        }

        // Kapatma Butonu (Sağ Üst)
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
        closeBtn.onClick.AddListener(CloseSettings);

        // Müzik Satırı (Simge + Slider)
        musicSlider = CreateVolumeRow(
            settingsPanelObj.transform,
            "MusicControlRow",
            musicIconSprite,
            new Vector2(60f, 65f),
            new Vector2(0f, 35f),
            GameAudioManager.MusicVolume,
            OnMusicSliderChanged
        );

        // Ses Efekti Satırı (Simge + Slider)
        sfxSlider = CreateVolumeRow(
            settingsPanelObj.transform,
            "SoundControlRow",
            soundIconSprite,
            new Vector2(70f, 60f),
            new Vector2(0f, -65f),
            GameAudioManager.SFXVolume,
            OnSFXSliderChanged
        );

        // Başlangıçta paneli kapalı tut
        settingsPanelObj.SetActive(false);
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

        // İkon (Solda)
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

        // Slider (Sağda)
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
        bgTrackRect.offsetMin = Vector2.zero;
        bgTrackRect.offsetMax = Vector2.zero;

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

    public void StartGame()
    {
        string target = string.IsNullOrWhiteSpace(firstGameSceneName) ? "Zeynep" : firstGameSceneName.Trim();
        SceneManager.LoadScene(target);
    }

    public void OpenSettings()
    {
        if (settingsPanelObj != null)
        {
            settingsPanelObj.SetActive(true);
            if (musicSlider != null) musicSlider.value = GameAudioManager.MusicVolume;
            if (sfxSlider != null) sfxSlider.value = GameAudioManager.SFXVolume;
        }
    }

    public void CloseSettings()
    {
        if (settingsPanelObj != null)
        {
            settingsPanelObj.SetActive(false);
        }
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#endif
    }

    public void OnMusicSliderChanged(float val)
    {
        GameAudioManager.SetMusicVolume(val);
    }

    public void OnSFXSliderChanged(float val)
    {
        GameAudioManager.SetSFXVolume(val);

        if (sfxAudioSource != null && sfxPreviewClip != null && !sfxAudioSource.isPlaying)
        {
            sfxAudioSource.PlayOneShot(sfxPreviewClip, val);
        }
    }
}
