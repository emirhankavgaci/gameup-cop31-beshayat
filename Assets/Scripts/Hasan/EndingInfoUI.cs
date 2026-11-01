using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// CANOPY: The Last Run - Oyun Sonu Sinematik Bilgi Ekranı (EndingInfoUI).
/// Siyah arka plan üzerinde "Black-headed Squirrel Monkey" IUCN 2021 değerlendirme bilgilerini
/// satır satır (her satır arası 0.8 sn) fade-in ile gösterir.
/// Tüm metin belirdikten sonra infoHoldTime (8 sn) bekleyip veya en az 3 sn geçtikten sonra
/// herhangi bir tuşa basıldığında SceneManager.LoadSceneAsync ile bir sonraki sahneye fade geçişi yapar.
/// </summary>
public class EndingInfoUI : MonoBehaviour
{
    private const int DEFAULT_INFO_CANVAS_SORT_ORDER = 990;
    private const float DEFAULT_REF_WIDTH = 1920f;
    private const float DEFAULT_REF_HEIGHT = 1080f;

    [Header("Sahne Geçiş Ayarları")]
    [Tooltip("Bilgi ekranından sonra yüklenecek sahnenin adı veya build index numarası (ör: 'Scene2' veya '1').")]
    [SerializeField] private string nextSceneName = "";

    [Tooltip("Tüm metin göründükten sonra otomatik sahne geçişi için beklenecek süre (varsayılan 8 sn).")]
    [SerializeField] private float infoHoldTime = 8.0f;

    [Tooltip("Yanlışlıkla atlanmasını önlemek için tuşla geçme özelliğinin aktif olacağı minimum süre (varsayılan 3 sn).")]
    [SerializeField] private float minSkipWaitTime = 3.0f;

    [Tooltip("Sonraki sahneye geçerken metinlerin siyaha kararma (fade-out) süresi (sn).")]
    [SerializeField] private float exitFadeOutDuration = 1.0f;

    [Header("Satır Satır Belirme (Fade-In) Zamanlaması")]
    [Tooltip("Her satırın belirmeye başlaması arasındaki bekleme süresi (varsayılan 0.8 sn).")]
    [SerializeField] private float lineInterval = 0.8f;

    [Tooltip("Tek bir satırın 0 opaklıktan 1 opaklığa ulaşma (fade-in) süresi (sn).")]
    [SerializeField] private float lineFadeDuration = 0.55f;

    [Header("Sinematik Metin İçeriği (IUCN 2021)")]
    [Tooltip("Büyük sinematik başlık metni (değiştirmeyin).")]
    [SerializeField] private string headingText = "Black-headed Squirrel Monkey";

    [Tooltip("Alt başlık ve maddeler (her eleman ayrı bir satır olarak sırayla fade-in olur).")]
    [TextArea(2, 4)]
    [SerializeField] private string[] infoLines = new string[]
    {
        "IUCN'nin 2021 değerlendirmesine göre:",
        "- Tehlike kategorisi: Endangered (EN).",
        "- Yalnızca Brezilya Amazonları'nda çok sınırlı bir bölgede yaşıyor.",
        "- Mevsimsel olarak su altında kalan ormanlara bağımlı.",
        "- İklim değişikliği, yıllık yağış döngüsünü ciddi biçimde değiştirebilecek en önemli tehdit olarak belirtiliyor.",
        "- Değerlendirmede gelecek 30 yılda popülasyonun en az %50 azalabileceği öngörülüyor."
    };

    [Header("Tipografi ve Görünüm Ayarları")]
    [Tooltip("Başlık (Black-headed Squirrel Monkey) font büyüklüğü.")]
    [SerializeField] private float headingFontSize = 54f;

    [Tooltip("Alt başlık (IUCN'nin 2021 değerlendirmesine göre:) font büyüklüğü.")]
    [SerializeField] private float subheadingFontSize = 32f;

    [Tooltip("Madde satırlarının font büyüklüğü.")]
    [SerializeField] private float bulletFontSize = 27f;

    [Tooltip("Başlık yazı rengi.")]
    [SerializeField] private Color headingColor = new Color(1f, 0.96f, 0.88f, 1f);

    [Tooltip("Alt başlık ve madde satırlarının yazı rengi.")]
    [SerializeField] private Color bodyTextColor = new Color(0.90f, 0.92f, 0.94f, 1f);

    [Tooltip("Başlık ile alt başlık arasındaki dikey boşluk (piksel).")]
    [SerializeField] private float spacingAfterHeading = 28f;

    [Tooltip("Maddeler arasındaki dikey satır boşluğu (piksel).")]
    [SerializeField] private float lineVerticalSpacing = 16f;

    [Tooltip("Metin bloğunun maksimum genişliği (piksel).")]
    [SerializeField] private float contentMaxWidth = 1280f;

    [Header("Opsiyonel UI Referansları (Boşsa Otomatik Oluşturulur)")]
    [Tooltip("Bilgi ekranı ana CanvasGroup bileşeni.")]
    [SerializeField] private CanvasGroup rootCanvasGroup;

    [Tooltip("Tam ekran siyah arka plan Image bileşeni.")]
    [SerializeField] private Image blackBackgroundImage;

    [Tooltip("Satırların dizileceği dikey konteyner RectTransform.")]
    [SerializeField] private RectTransform linesContainer;

    private readonly List<CanvasGroup> spawnedLineGroups = new List<CanvasGroup>();
    private Coroutine activeSequenceCoroutine;
    private bool isShowingInfo;
    private bool isTransitioningScene;

    public bool IsShowingInfo => isShowingInfo;
    public float InfoHoldTime
    {
        get => infoHoldTime;
        set => infoHoldTime = Mathf.Max(0.5f, value);
    }
    public string NextSceneName
    {
        get => nextSceneName;
        set => nextSceneName = value;
    }

    private void Awake()
    {
        EnsureUiHierarchy();
        HideImmediate();
    }

    /// <summary>
    /// Siyah bilgi ekranını açar ve satır satır fade-in sekansını başlatır.
    /// </summary>
    public void ShowEndingInfo(string overrideNextSceneName = null)
    {
        if (!string.IsNullOrWhiteSpace(overrideNextSceneName))
        {
            nextSceneName = overrideNextSceneName;
        }

        EnsureUiHierarchy();
        RebuildLineElements();

        if (activeSequenceCoroutine != null)
        {
            StopCoroutine(activeSequenceCoroutine);
        }

        gameObject.SetActive(true);
        activeSequenceCoroutine = StartCoroutine(RunEndingInfoSequenceCoroutine());
    }

    public void HideImmediate()
    {
        isShowingInfo = false;
        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = 0f;
            rootCanvasGroup.blocksRaycasts = false;
            rootCanvasGroup.interactable = false;
        }
    }

    private IEnumerator RunEndingInfoSequenceCoroutine()
    {
        isShowingInfo = true;
        isTransitioningScene = false;

        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = 1f;
            rootCanvasGroup.blocksRaycasts = true;
        }

        for (int i = 0; i < spawnedLineGroups.Count; i++)
        {
            if (spawnedLineGroups[i] != null)
            {
                spawnedLineGroups[i].alpha = 0f;
            }
        }

        float totalElapsedSinceInfoStart = 0f;
        bool skipRequested = false;

        // 1. Satırları sırayla (her satır arası lineInterval = 0.8 sn) fade-in ile göster
        for (int i = 0; i < spawnedLineGroups.Count; i++)
        {
            CanvasGroup lineGroup = spawnedLineGroups[i];
            if (lineGroup != null)
            {
                StartCoroutine(FadeCanvasGroupCoroutine(lineGroup, 0f, 1f, lineFadeDuration));
            }

            float waitTimer = 0f;
            float interval = Mathf.Max(0.1f, lineInterval);
            while (waitTimer < interval)
            {
                float dt = Time.deltaTime;
                waitTimer += dt;
                totalElapsedSinceInfoStart += dt;

                if (totalElapsedSinceInfoStart >= minSkipWaitTime && Input.anyKeyDown)
                {
                    skipRequested = true;
                    break;
                }

                yield return null;
            }

            if (skipRequested)
            {
                for (int j = 0; j < spawnedLineGroups.Count; j++)
                {
                    if (spawnedLineGroups[j] != null)
                    {
                        spawnedLineGroups[j].alpha = 1f;
                    }
                }
                skipRequested = false;
                yield return null;
                break;
            }
        }

        // 2. Tüm metin göründükten sonra infoHoldTime (varsayılan 8 sn) bekle;
        // en az minSkipWaitTime (3 sn) geçtiyse herhangi bir tuşa basıldığında hemen sahne geçişine başla
        float holdElapsed = 0f;
        float targetHold = Mathf.Max(0.5f, infoHoldTime);

        while (holdElapsed < targetHold)
        {
            float dt = Time.deltaTime;
            holdElapsed += dt;
            totalElapsedSinceInfoStart += dt;

            if (totalElapsedSinceInfoStart >= minSkipWaitTime && Input.anyKeyDown)
            {
                break;
            }

            yield return null;
        }

        // 3. Sonraki sahneye yumuşak fade-out ile geç
        yield return StartCoroutine(FadeOutTextAndLoadNextSceneCoroutine());
    }

    private IEnumerator FadeCanvasGroupCoroutine(CanvasGroup cg, float fromAlpha, float toAlpha, float duration)
    {
        if (cg == null)
            yield break;

        if (duration <= 0.01f)
        {
            cg.alpha = toAlpha;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            cg.alpha = Mathf.Lerp(fromAlpha, toAlpha, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        cg.alpha = toAlpha;
    }

    private IEnumerator FadeOutTextAndLoadNextSceneCoroutine()
    {
        if (isTransitioningScene)
            yield break;

        isTransitioningScene = true;

        float fadeTime = Mathf.Max(0.2f, exitFadeOutDuration);
        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeTime));
            for (int i = 0; i < spawnedLineGroups.Count; i++)
            {
                if (spawnedLineGroups[i] != null)
                {
                    spawnedLineGroups[i].alpha = alpha;
                }
            }
            yield return null;
        }

        for (int i = 0; i < spawnedLineGroups.Count; i++)
        {
            if (spawnedLineGroups[i] != null)
            {
                spawnedLineGroups[i].alpha = 0f;
            }
        }

        GameFlowManager.SetEndingActive(false);

        AsyncOperation loadOp = BeginLoadTargetSceneAsync();
        if (loadOp != null)
        {
            while (!loadOp.isDone)
            {
                yield return null;
            }
        }
    }

    private AsyncOperation BeginLoadTargetSceneAsync()
    {
        string trimmed = nextSceneName != null ? nextSceneName.Trim() : string.Empty;

        if (!string.IsNullOrEmpty(trimmed) && int.TryParse(trimmed, out int parsedBuildIndex))
        {
            if (parsedBuildIndex >= 0 && parsedBuildIndex < SceneManager.sceneCountInBuildSettings)
            {
                return SceneManager.LoadSceneAsync(parsedBuildIndex);
            }
        }

        if (!string.IsNullOrEmpty(trimmed))
        {
            if (Application.CanStreamedLevelBeLoaded(trimmed))
            {
                return SceneManager.LoadSceneAsync(trimmed);
            }
            Debug.LogWarning($"[EndingInfoUI] '{trimmed}' adlı sahne Build Settings'de bulunamadı. Sonraki build index deneniyor.");
        }

        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        int nextIndex = currentIndex + 1;
        if (nextIndex >= 0 && nextIndex < SceneManager.sceneCountInBuildSettings)
        {
            return SceneManager.LoadSceneAsync(nextIndex);
        }

        return SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().name);
    }

    private void EnsureUiHierarchy()
    {
        if (rootCanvasGroup != null && blackBackgroundImage != null && linesContainer != null)
            return;

        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("EndingInfoCanvas");
            canvasObj.transform.SetParent(transform, false);

            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = DEFAULT_INFO_CANVAS_SORT_ORDER;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(DEFAULT_REF_WIDTH, DEFAULT_REF_HEIGHT);
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (rootCanvasGroup == null)
        {
            rootCanvasGroup = canvas.GetComponent<CanvasGroup>();
            if (rootCanvasGroup == null)
            {
                rootCanvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (blackBackgroundImage == null)
        {
            GameObject bgObj = new GameObject("BlackBackground");
            bgObj.transform.SetParent(canvas.transform, false);

            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            blackBackgroundImage = bgObj.AddComponent<Image>();
            blackBackgroundImage.color = Color.black;
            blackBackgroundImage.raycastTarget = false;
        }

        if (linesContainer == null)
        {
            GameObject containerObj = new GameObject("InfoLinesContainer");
            containerObj.transform.SetParent(canvas.transform, false);

            linesContainer = containerObj.AddComponent<RectTransform>();
            linesContainer.anchorMin = new Vector2(0.5f, 0.5f);
            linesContainer.anchorMax = new Vector2(0.5f, 0.5f);
            linesContainer.pivot = new Vector2(0.5f, 0.5f);
            linesContainer.sizeDelta = new Vector2(contentMaxWidth, 760f);
            linesContainer.anchoredPosition = Vector2.zero;

            VerticalLayoutGroup vlg = containerObj.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleLeft;
            vlg.spacing = lineVerticalSpacing;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
        }
    }

    private void RebuildLineElements()
    {
        if (linesContainer == null)
            return;

        for (int i = linesContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(linesContainer.GetChild(i).gameObject);
        }

        spawnedLineGroups.Clear();

        CanvasGroup headingGroup = CreateLineElement(
            "Heading_Line",
            headingText,
            headingFontSize,
            FontStyles.Bold,
            headingColor,
            spacingAfterHeading
        );
        spawnedLineGroups.Add(headingGroup);

        if (infoLines != null)
        {
            for (int i = 0; i < infoLines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(infoLines[i]))
                    continue;

                bool isSubheading = (i == 0 && !infoLines[i].TrimStart().StartsWith("-"));
                float fontSize = isSubheading ? subheadingFontSize : bulletFontSize;
                FontStyles style = isSubheading ? FontStyles.Bold : FontStyles.Normal;
                float extraBottomMargin = isSubheading ? 8f : 0f;

                CanvasGroup itemGroup = CreateLineElement(
                    $"Info_Line_{i}",
                    infoLines[i],
                    fontSize,
                    style,
                    bodyTextColor,
                    extraBottomMargin
                );
                spawnedLineGroups.Add(itemGroup);
            }
        }
    }

    private CanvasGroup CreateLineElement(
        string objectName,
        string textContent,
        float fontSize,
        FontStyles fontStyle,
        Color textColor,
        float bottomMargin)
    {
        GameObject lineObj = new GameObject(objectName);
        lineObj.transform.SetParent(linesContainer, false);

        RectTransform rt = lineObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(contentMaxWidth, fontSize * 1.5f);

        CanvasGroup cg = lineObj.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        TextMeshProUGUI tmp = lineObj.AddComponent<TextMeshProUGUI>();
        tmp.text = textContent;
        tmp.fontSize = fontSize;
        tmp.fontStyle = fontStyle;
        tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.margin = new Vector4(0f, 0f, 0f, bottomMargin);

        return cg;
    }
}
