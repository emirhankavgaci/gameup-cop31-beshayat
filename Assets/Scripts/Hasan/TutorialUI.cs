using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// CANOPY: The Last Run - Tam Ekran Tutorial Yazı ve Kuyruk (Queue) Yöneticisi.
/// Bir tutorial mesajı ekranda gösterilirken yeni bir TutorialTrigger tetiklenirse mesajları kuyruğa alır
/// ve üst üste binmeden sırayla gösterir. Oyun akışını durdurmaz (Time.timeScale değişmez).
/// Sahnede hazır Canvas/TMP atanmamışsa çalışma zamanında (runtime) otomatik oluşturur.
/// </summary>
public class TutorialUI : MonoBehaviour
{
    /// <summary>
    /// Kuyrukta bekleyen tek bir tutorial mesajının veri yapısı.
    /// </summary>
    public struct TutorialMessageRequest
    {
        public string Message;
        public float Duration;
        public float FadeInTime;
        public float FadeOutTime;

        public TutorialMessageRequest(string message, float duration, float fadeInTime, float fadeOutTime)
        {
            Message = message;
            Duration = duration;
            FadeInTime = fadeInTime;
            FadeOutTime = fadeOutTime;
        }
    }

    private const int DEFAULT_CANVAS_SORT_ORDER = 900;
    private const float DEFAULT_REFERENCE_WIDTH = 1920f;
    private const float DEFAULT_REFERENCE_HEIGHT = 1080f;
    private const float MIN_FADE_DURATION = 0.01f;

    private static TutorialUI instance;

    [Header("UI Bileşen Referansları (Boşsa Otomatik Oluşturulur)")]
    [Tooltip("Tutorial yazısını ve arka plan panelini saydamlaştırıp görünür yapan CanvasGroup bileşeni.")]
    [SerializeField] private CanvasGroup tutorialCanvasGroup;

    [Tooltip("Yazının arkasındaki çok hafif yarı saydam siyah tam ekran panel Image bileşeni.")]
    [SerializeField] private Image backgroundDimPanel;

    [Tooltip("Tüm ekranı kaplayan, ortalanmış TextMeshPro (TMP_Text) bileşeni.")]
    [SerializeField] private TMP_Text tutorialText;

    [Header("Görünüm ve Tipografi Ayarları")]
    [Tooltip("Arkada beliren çok hafif yarı saydam siyah panelin rengi ve maksimum opaklığı.")]
    [SerializeField] private Color panelBackgroundColor = new Color(0f, 0f, 0f, 0.22f);

    [Tooltip("Ana tutorial metninin temel rengi (rich text olmayan kısımlar için beyaz).")]
    [SerializeField] private Color textBaseColor = Color.white;

    [Tooltip("Okunabilirliği artıran koyu dış çizgi (outline) rengi.")]
    [SerializeField] private Color textOutlineColor = new Color(0f, 0f, 0f, 0.92f);

    [Tooltip("TextMeshPro dış çizgi (outline) kalınlığı (0 - 0.5 arası).")]
    [Range(0f, 0.5f)]
    [SerializeField] private float textOutlineWidth = 0.22f;

    [Tooltip("Otomatik boyutlandırma (Auto Size) için minimum font büyüklüğü.")]
    [SerializeField] private float autoSizeMinFont = 42f;

    [Tooltip("Otomatik boyutlandırma (Auto Size) için maksimum font büyüklüğü.")]
    [SerializeField] private float autoSizeMaxFont = 92f;

    [Tooltip("Metnin ekran kenarlarından bırakacağı güvenli iç boşluk (piksel).")]
    [SerializeField] private Vector2 screenPadding = new Vector2(120f, 90f);

    // Mesaj kuyruğu ve çalışma durumu
    private readonly Queue<TutorialMessageRequest> messageQueue = new Queue<TutorialMessageRequest>();
    private Coroutine displayQueueCoroutine;
    private bool isShowingMessage;

    /// <summary>
    /// Sahnede aktif bir TutorialUI örneği döndürür; yoksa çalışma zamanında otomatik oluşturur.
    /// </summary>
    public static TutorialUI Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<TutorialUI>();
                if (instance == null)
                {
                    GameObject go = new GameObject("TutorialUI_Runtime");
                    instance = go.AddComponent<TutorialUI>();
                }
            }
            return instance;
        }
    }

    /// <summary>
    /// Şu anda ekranda aktif bir tutorial mesajı gösteriliyor mu?
    /// </summary>
    public bool IsShowingMessage => isShowingMessage;

    /// <summary>
    /// Kuyrukta bekleyen mesaj sayısı.
    /// </summary>
    public int QueuedMessageCount => messageQueue.Count;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        EnsureTutorialUiElements();
        SetOverlayAlpha(0f);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>
    /// Yeni bir tutorial mesajını kuyruğa ekler ve kuyruk işleyicisini başlatır.
    /// Oyun durmaz (Time.timeScale değiştirilmez).
    /// </summary>
    public void EnqueueTutorialMessage(string message, float duration, float fadeInTime, float fadeOutTime)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        EnsureTutorialUiElements();

        TutorialMessageRequest request = new TutorialMessageRequest(
            message,
            Mathf.Max(0.1f, duration),
            Mathf.Max(0f, fadeInTime),
            Mathf.Max(0f, fadeOutTime)
        );

        messageQueue.Enqueue(request);

        if (displayQueueCoroutine == null)
        {
            displayQueueCoroutine = StartCoroutine(ProcessMessageQueueCoroutine());
        }
    }

    /// <summary>
    /// Aktif mesajı ve kuyruktaki tüm bekleyen mesajları anında temizler (ör. bitiş sekansı başladığında).
    /// </summary>
    public void ClearAllMessagesImmediate()
    {
        messageQueue.Clear();
        if (displayQueueCoroutine != null)
        {
            StopCoroutine(displayQueueCoroutine);
            displayQueueCoroutine = null;
        }

        isShowingMessage = false;
        SetOverlayAlpha(0f);
    }

    private IEnumerator ProcessMessageQueueCoroutine()
    {
        isShowingMessage = true;

        while (messageQueue.Count > 0)
        {
            TutorialMessageRequest current = messageQueue.Dequeue();

            if (tutorialText != null)
            {
                tutorialText.text = current.Message;
                tutorialText.ForceMeshUpdate();
            }

            // 1. Fade-In (Görünür olma)
            if (current.FadeInTime > MIN_FADE_DURATION)
            {
                float elapsed = 0f;
                while (elapsed < current.FadeInTime)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / current.FadeInTime);
                    SetOverlayAlpha(Mathf.SmoothStep(0f, 1f, t));
                    yield return null;
                }
            }

            SetOverlayAlpha(1f);

            // 2. Ekranda Kalma Süresi (Oyun durmaz, Time.deltaTime ile sayılır)
            float holdElapsed = 0f;
            while (holdElapsed < current.Duration)
            {
                holdElapsed += Time.deltaTime;
                yield return null;
            }

            // 3. Fade-Out (Kaybolma)
            if (current.FadeOutTime > MIN_FADE_DURATION)
            {
                float elapsed = 0f;
                while (elapsed < current.FadeOutTime)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / current.FadeOutTime);
                    SetOverlayAlpha(1f - Mathf.SmoothStep(0f, 1f, t));
                    yield return null;
                }
            }

            SetOverlayAlpha(0f);
        }

        isShowingMessage = false;
        displayQueueCoroutine = null;
    }

    private void SetOverlayAlpha(float normalizedAlpha)
    {
        float clamped = Mathf.Clamp01(normalizedAlpha);

        if (tutorialCanvasGroup != null)
        {
            tutorialCanvasGroup.alpha = clamped;
            tutorialCanvasGroup.interactable = false;
            tutorialCanvasGroup.blocksRaycasts = false;
        }
    }

    private void EnsureTutorialUiElements()
    {
        if (tutorialCanvasGroup != null && tutorialText != null && backgroundDimPanel != null)
        {
            ApplyTypographyAndRaycastSettings();
            return;
        }

        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("TutorialCanvas");
            canvasObj.transform.SetParent(transform, false);

            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = DEFAULT_CANVAS_SORT_ORDER;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(DEFAULT_REFERENCE_WIDTH, DEFAULT_REFERENCE_HEIGHT);
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (tutorialCanvasGroup == null)
        {
            tutorialCanvasGroup = canvas.GetComponent<CanvasGroup>();
            if (tutorialCanvasGroup == null)
            {
                tutorialCanvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (backgroundDimPanel == null)
        {
            GameObject panelObj = new GameObject("TutorialBackgroundPanel");
            panelObj.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            backgroundDimPanel = panelObj.AddComponent<Image>();
        }

        if (tutorialText == null)
        {
            GameObject textObj = new GameObject("TutorialMessageTMP");
            textObj.transform.SetParent(canvas.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = screenPadding;
            textRect.offsetMax = -screenPadding;

            tutorialText = textObj.AddComponent<TextMeshProUGUI>();
        }

        ApplyTypographyAndRaycastSettings();
    }

    private void ApplyTypographyAndRaycastSettings()
    {
        if (tutorialCanvasGroup != null)
        {
            tutorialCanvasGroup.interactable = false;
            tutorialCanvasGroup.blocksRaycasts = false;
        }

        if (backgroundDimPanel != null)
        {
            backgroundDimPanel.color = panelBackgroundColor;
            backgroundDimPanel.raycastTarget = false;
        }

        if (tutorialText != null)
        {
            tutorialText.raycastTarget = false;
            tutorialText.richText = true;
            tutorialText.alignment = TextAlignmentOptions.Center;
            tutorialText.fontStyle = FontStyles.Bold;
            tutorialText.color = textBaseColor;
            tutorialText.enableAutoSizing = true;
            tutorialText.fontSizeMin = autoSizeMinFont;
            tutorialText.fontSizeMax = autoSizeMaxFont;
            tutorialText.textWrappingMode = TextWrappingModes.Normal;
            tutorialText.overflowMode = TextOverflowModes.Overflow;

            // Koyu dış çizgi (Outline) ayarı
            tutorialText.outlineWidth = textOutlineWidth;
            tutorialText.outlineColor = textOutlineColor;
        }
    }
}
