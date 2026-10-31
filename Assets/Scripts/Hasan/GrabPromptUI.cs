using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// CANOPY: The Last Run - Dallar Arası Geçiş ("[E] Dala geç") Hedef İşaretçisi ve UI Yöneticisi.
/// BranchGrabHandler üzerindeki HasTransferTarget ve CurrentTransferTarget bilgilerini dinleyerek
/// hedef dal üzerinde "[E] Dala geç" göstergesini çizer.
/// </summary>
public class GrabPromptUI : MonoBehaviour
{
    [Header("Bileşen ve Kamera Referansları")]
    [Tooltip("Hedef dal bilgisini sağlayan BranchGrabHandler bileşeni (boşsa sahnede otomatik bulunur).")]
    [SerializeField] private BranchGrabHandler branchGrabHandler;

    [Tooltip("Hedef dalın dünya pozisyonunu ekran noktasına çevirmek için kullanılan kamera (boşsa MainCamera bulunur).")]
    [SerializeField] private Camera targetCamera;

    [Header("Canvas UI Referansları (Opsiyonel)")]
    [Tooltip("Açılıp kapatılacak ve hedef dala taşınacak UI RectTransform (Panel veya İkon). Boşsa OnGUI yedeği çalışır.")]
    [SerializeField] private RectTransform markerRectTransform;

    [Tooltip("TextMeshPro metin referansı (opsiyonel).")]
    [SerializeField] private TMP_Text tmpPromptText;

    [Tooltip("Klasik Unity UI Text referansı (opsiyonel).")]
    [SerializeField] private Text legacyPromptText;

    [Header("Görünüm ve Metin Ayarları")]
    [Tooltip("Hedef dal üzerinde gösterilecek geçiş metni.")]
    [SerializeField] private string transferPromptMessage = "[E] Dala geç";

    [Tooltip("Hedef dal işaretçi rengi.")]
    [SerializeField] private Color markerColor = new Color(0.35f, 1f, 0.55f, 1f);

    [Tooltip("Canvas UI atanmamışsa hedef dalın üzerinde otomatik OnGUI işaretçisi ve '[E] Dala geç' yazısı çiz.")]
    [SerializeField] private bool useFallbackOnGuiMarker = true;

    [Tooltip("Yedek OnGUI yazı boyutu.")]
    [SerializeField] private int fallbackFontSize = 18;

    [Tooltip("İşaretçinin hedef dal noktasından dikey ekran ofseti (piksel).")]
    [SerializeField] private float screenVerticalOffset = -24f;

    private bool hasActiveTarget;
    private Vector3 targetWorldPosition;
    private GUIStyle labelStyle;
    private GUIStyle boxStyle;

    public bool HasActiveTarget => hasActiveTarget;
    public Vector3 TargetWorldPosition => targetWorldPosition;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (branchGrabHandler == null)
        {
            branchGrabHandler = FindObjectOfType<BranchGrabHandler>();
        }

        if (tmpPromptText != null)
        {
            tmpPromptText.text = transferPromptMessage;
        }

        if (legacyPromptText != null)
        {
            legacyPromptText.text = transferPromptMessage;
        }

        ClearTransferTarget();
    }

    /// <summary>
    /// Dallar arası geçiş için geçerli bir aday dal bulunduğunda çağrılır.
    /// </summary>
    public void SetTransferTarget(Vector3 worldPos)
    {
        hasActiveTarget = true;
        targetWorldPosition = worldPos;
        SetUiElementsActive(true);
        UpdateCanvasMarkerPosition();
    }

    /// <summary>
    /// Aday dal kalmadığında, W/S basıldığında veya geçiş başladığında hedef işaretçisini gizler.
    /// </summary>
    public void ClearTransferTarget()
    {
        hasActiveTarget = false;
        SetUiElementsActive(false);
    }

    private void LateUpdate()
    {
        // BranchGrabHandler referansı varsa doğrudan onun HasTransferTarget durumuyla senkron kal
        if (branchGrabHandler != null)
        {
            if (branchGrabHandler.HasTransferTarget && branchGrabHandler.CurrentTransferTarget != null)
            {
                hasActiveTarget = true;
                targetWorldPosition = branchGrabHandler.CurrentTransferTargetPoint;
                SetUiElementsActive(true);
            }
            else if (hasActiveTarget)
            {
                ClearTransferTarget();
            }
        }

        if (hasActiveTarget)
        {
            UpdateCanvasMarkerPosition();
        }
    }

    /// <summary>
    /// Eğer sahnede bir Canvas RectTransform atanmışsa, onu hedef dalın ekran koordinatına taşır.
    /// </summary>
    private void UpdateCanvasMarkerPosition()
    {
        if (markerRectTransform == null)
            return;

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
            return;

        Vector3 screenPos = targetCamera.WorldToScreenPoint(targetWorldPosition);
        bool isInFrontOfCamera = screenPos.z > 0f;

        if (markerRectTransform.gameObject.activeSelf != isInFrontOfCamera)
        {
            markerRectTransform.gameObject.SetActive(isInFrontOfCamera);
        }

        if (isInFrontOfCamera)
        {
            screenPos.y -= screenVerticalOffset;
            markerRectTransform.position = screenPos;
        }
    }

    private void SetUiElementsActive(bool visible)
    {
        if (markerRectTransform != null && markerRectTransform.gameObject.activeSelf != visible)
        {
            markerRectTransform.gameObject.SetActive(visible);
        }

        if (tmpPromptText != null && tmpPromptText.gameObject.activeSelf != visible)
        {
            tmpPromptText.gameObject.SetActive(visible);
        }

        if (legacyPromptText != null && legacyPromptText.gameObject.activeSelf != visible)
        {
            legacyPromptText.gameObject.SetActive(visible);
        }
    }

    private void OnGUI()
    {
        if (!hasActiveTarget || !useFallbackOnGuiMarker)
            return;

        // Eğer kullanıcı özel bir Canvas RectTransform veya Text atamışsa yedek OnGUI çizme
        if (markerRectTransform != null || tmpPromptText != null || legacyPromptText != null)
            return;

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
            return;

        Vector3 screenPos = targetCamera.WorldToScreenPoint(targetWorldPosition);
        if (screenPos.z <= 0f)
            return;

        EnsureGuiStyles();

        float guiX = screenPos.x;
        float guiY = Screen.height - screenPos.y + screenVerticalOffset;

        float diamondSize = 14f;
        Rect markerRect = new Rect(guiX - diamondSize * 0.5f, (Screen.height - screenPos.y) - diamondSize * 0.5f, diamondSize, diamondSize);
        GUI.Box(markerRect, GUIContent.none, boxStyle);

        float labelWidth = 180f;
        float labelHeight = 32f;
        Rect labelRect = new Rect(guiX - labelWidth * 0.5f, guiY - labelHeight * 0.5f, labelWidth, labelHeight);
        GUI.Label(labelRect, transferPromptMessage, labelStyle);
    }

    private void EnsureGuiStyles()
    {
        if (labelStyle != null)
            return;

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = fallbackFontSize
        };
        labelStyle.normal.textColor = markerColor;

        boxStyle = new GUIStyle(GUI.skin.box);
    }
}
