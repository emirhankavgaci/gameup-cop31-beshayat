using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CANOPY: The Last Run - Dinamik Merkez Nokta Crosshair Yöneticisi.
/// Normalde ekran merkezinde 6x6 piksel (%80 opaklıkta) beyaz ve hafif siyah dış çizgili bir nokta gösterir.
/// Oyuncu havadayken veya dalda asılıyken menzilde geçilebilecek bir hedef dal (HasTransferTarget) varsa
/// yumuşakça 1.5x büyür ve açık yeşil renge dönüşür.
/// </summary>
public class CrosshairUI : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Hedef dal bilgisini (HasTransferTarget) sağlayan BranchGrabHandler bileşeni.")]
    [SerializeField] private BranchGrabHandler branchGrabHandler;

    [Tooltip("Durum kontrolü için PlayerMotor bileşeni (boşsa otomatik bulunur).")]
    [SerializeField] private PlayerMotor playerMotor;

    [Tooltip("Canvas altındaki merkez nokta UI Image bileşeni.")]
    [SerializeField] private Image crosshairImage;

    [Header("Boyut ve Geçiş Ayarları")]
    [Tooltip("Varsayılan nokta boyutu (piksel, örn. 6x6).")]
    [SerializeField] private float baseSize = 6f;

    [Tooltip("Hedef dal varken noktanın büyüme çarpanı (varsayılan 1.5x -> 9x9 piksel).")]
    [SerializeField] private float targetScaleMultiplier = 1.5f;

    [Tooltip("Boyut ve renk değişimlerinin yumuşak geçiş (Lerp) hızı.")]
    [SerializeField] private float transitionLerpSpeed = 14f;

    [Header("Renk ve Opaklık Ayarları")]
    [Tooltip("Normal durumdaki nokta rengi (varsayılan beyaz).")]
    [SerializeField] private Color normalColor = Color.white;

    [Tooltip("Normal durumdaki nokta opaklığı (varsayılan %80 = 0.80).")]
    [Range(0.1f, 1f)]
    [SerializeField] private float normalOpacity = 0.80f;

    [Tooltip("Hedef dal menzildeyken noktanın alacağı renk (varsayılan açık yeşil).")]
    [SerializeField] private Color targetBranchColor = new Color(0.40f, 1f, 0.55f, 1f);

    [Tooltip("Hedef dal menzildeyken nokta opaklığı.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float targetBranchOpacity = 0.95f;

    [Header("Okunurluk İçin Dış Çizgi (Outline / Gölge)")]
    [Tooltip("UI Image üzerinde otomatik hafif siyah dış çizgi (Outline) oluştur.")]
    [SerializeField] private bool ensureBlackOutline = true;

    [Tooltip("Dış çizgi veya gölge rengi.")]
    [SerializeField] private Color outlineColor = new Color(0f, 0f, 0f, 0.75f);

    [Tooltip("Dış çizgi kalınlığı (piksel).")]
    [SerializeField] private Vector2 outlineDistance = new Vector2(1f, -1f);

    [Header("Yedek Gösterim (Canvas Yoksa)")]
    [Tooltip("Eğer sahnede henüz Canvas Image atanmamışsa ekran ortasında yedek OnGUI noktası çizer.")]
    [SerializeField] private bool useFallbackOnGuiIfNoImage = true;

    private RectTransform imageRectTransform;
    private bool isVisible = true;
    private bool isTargetHighlighted;
    private float currentSize;
    private Color currentColor;
    private Texture2D fallbackWhiteTex;

    public bool IsVisible => isVisible;

    private void Awake()
    {
        if (branchGrabHandler == null)
            branchGrabHandler = FindObjectOfType<BranchGrabHandler>();

        if (playerMotor == null)
            playerMotor = FindObjectOfType<PlayerMotor>();

        if (crosshairImage == null)
            crosshairImage = GetComponent<Image>();

        currentSize = baseSize;
        currentColor = new Color(normalColor.r, normalColor.g, normalColor.b, normalOpacity);

        if (crosshairImage != null)
        {
            imageRectTransform = crosshairImage.rectTransform;
            crosshairImage.raycastTarget = false;

            // Ekran merkezine sabitle ve başlangıç boyutunu uygula
            imageRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            imageRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            imageRectTransform.pivot = new Vector2(0.5f, 0.5f);
            imageRectTransform.anchoredPosition = Vector2.zero;
            imageRectTransform.sizeDelta = new Vector2(currentSize, currentSize);
            crosshairImage.color = currentColor;

            if (ensureBlackOutline)
            {
                Outline outline = crosshairImage.GetComponent<Outline>();
                if (outline == null)
                {
                    outline = crosshairImage.gameObject.AddComponent<Outline>();
                }
                outline.effectColor = outlineColor;
                outline.effectDistance = outlineDistance;
            }
        }
    }

    private void OnEnable()
    {
        if (branchGrabHandler != null)
        {
            branchGrabHandler.OnTransferTargetChanged += HandleTransferTargetChanged;
        }
    }

    private void OnDisable()
    {
        if (branchGrabHandler != null)
        {
            branchGrabHandler.OnTransferTargetChanged -= HandleTransferTargetChanged;
        }
    }

    private void HandleTransferTargetChanged(bool hasTarget, BranchData targetBranch)
    {
        isTargetHighlighted = hasTarget;
    }

    private void Update()
    {
        if (!isVisible)
            return;

        // BranchGrabHandler property'sinden de her karede güncel durumu doğrula
        if (branchGrabHandler != null)
        {
            bool isAirOrHanging = true;
            if (playerMotor != null)
            {
                PlayerMotor.PlayerState state = playerMotor.CurrentState;
                isAirOrHanging = (state == PlayerMotor.PlayerState.Airborne ||
                                  state == PlayerMotor.PlayerState.Hanging ||
                                  state == PlayerMotor.PlayerState.WallRunning);
            }

            isTargetHighlighted = isAirOrHanging && branchGrabHandler.HasTransferTarget;
        }

        float desiredSize = isTargetHighlighted ? (baseSize * targetScaleMultiplier) : baseSize;
        Color baseTargetCol = isTargetHighlighted ? targetBranchColor : normalColor;
        float desiredAlpha = isTargetHighlighted ? targetBranchOpacity : normalOpacity;
        Color desiredColor = new Color(baseTargetCol.r, baseTargetCol.g, baseTargetCol.b, desiredAlpha);

        float dt = Time.deltaTime;
        currentSize = Mathf.Lerp(currentSize, desiredSize, transitionLerpSpeed * dt);
        currentColor = Color.Lerp(currentColor, desiredColor, transitionLerpSpeed * dt);

        if (crosshairImage != null && imageRectTransform != null)
        {
            imageRectTransform.sizeDelta = new Vector2(currentSize, currentSize);
            crosshairImage.color = currentColor;
        }
    }

    /// <summary>
    /// Pause menüsü veya ölüm ekranı gibi durumlarda crosshair'i gizlemek/göstermek için kullanılır.
    /// </summary>
    public void SetVisible(bool visible)
    {
        isVisible = visible;
        if (crosshairImage != null && crosshairImage.gameObject.activeSelf != visible)
        {
            crosshairImage.gameObject.SetActive(visible);
        }
    }

    private void OnGUI()
    {
        if (!isVisible || crosshairImage != null || !useFallbackOnGuiIfNoImage)
            return;

        if (fallbackWhiteTex == null)
        {
            fallbackWhiteTex = Texture2D.whiteTexture;
        }

        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;

        // Hafif siyah dış çerçeve (okunurluk için)
        float outlineSize = currentSize + 2f;
        GUI.color = outlineColor;
        GUI.DrawTexture(new Rect(centerX - outlineSize * 0.5f, centerY - outlineSize * 0.5f, outlineSize, outlineSize), fallbackWhiteTex);

        // Merkez nokta
        GUI.color = currentColor;
        GUI.DrawTexture(new Rect(centerX - currentSize * 0.5f, centerY - currentSize * 0.5f, currentSize, currentSize), fallbackWhiteTex);
        GUI.color = Color.white;
    }
}

