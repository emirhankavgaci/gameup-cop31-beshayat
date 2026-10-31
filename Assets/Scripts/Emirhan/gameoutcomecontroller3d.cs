using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Unity 6 (6000.x) uyumlu, Kazanma ("Korku Değil Ümit") ve Kaybetme (Nehir Kurudu) durumlarını,
/// Su Miktarı Barını, 5x4 Baraj İlerleme Arayüzünü, Köşe Kontrol Rehberini ve Sahne Yeniden Başlatmayı yöneten kontrolcü.
/// </summary>
public class GameOutcomeController3D : MonoBehaviour
{
    [Header("Sistem Referansları (Boş Bırakılırsa Otomatik Bulunur)")]
    [SerializeField] private WaterLevelController waterController;
    [SerializeField] private DamBuilderController3D damBuilder;
    [SerializeField] private PlayerMovement3D playerMovement;

    [Header("Kazanma & Kaybetme Ekranları (UI Panel)")]
    [SerializeField] private GameObject winPanel;  // 5x4 Baraj tamamlandığında açılacak "Ümit / Kazanma" paneli
    [SerializeField] private GameObject losePanel; // Su tamamen bittiğinde açılacak "Kaybetme" paneli

    [Header("Oyun İçi HUD Arayüzü (Opsiyonel)")]
    [SerializeField] private Slider waterBarSlider;
    [SerializeField] private Image waterBarFillImage;
    [SerializeField] private TMP_Text waterPercentText;
    [SerializeField] private TMP_Text damProgressText;
    [SerializeField] private TMP_Text cornerControlsHintText; // Köşedeki sabit mini kontrol rehberi

    [Header("Köşe Kontrol Rehberi Metni")]
    [SerializeField] [TextArea(2, 4)] private string defaultControlsHint =
        "WASD: Hareket  |  Shift: Koş  |  Space: Zıpla  |  Mouse: Bakış  |  Sol Tık (Basılı Tut): Nesne Taşı";

    [Header("Kısayol Ayarları")]
    [SerializeField] private bool allowRKeyToRestartOnEnd = true;

    private bool isGameEnded = false;
    private bool isReloadingScene = false;
    public bool IsGameEnded => isGameEnded;

    private void Awake()
    {
        if (waterController == null)
            waterController = FindAnyObjectByType<WaterLevelController>();

        if (damBuilder == null)
            damBuilder = FindAnyObjectByType<DamBuilderController3D>();

        if (playerMovement == null)
            playerMovement = FindAnyObjectByType<PlayerMovement3D>();

        if (winPanel != null) winPanel.SetActive(false);
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

        // Oyun bittiğinde 'R' tuşuna basarak hızlıca yeniden başlatabilme (Game Jam demoları için pratik)
        if (isGameEnded && allowRKeyToRestartOnEnd && Keyboard.current != null)
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
            waterPercentText.text = $"Su: %{percent}";
        }
    }

    private void HandleDamProgressUpdated(int placedItems, int totalItems, int completedBlocks)
    {
        if (isReloadingScene || this == null) return;

        if (damProgressText != null && damProgressText.gameObject != null)
        {
            damProgressText.text = $"Baraj: {completedBlocks}/5 Blok ({placedItems}/{totalItems})";
        }
    }

    /// <summary>
    /// 5x4 Baraj tamamlandığında ("Korku değil ümit vermek" kazanma durumu) çağrılır.
    /// </summary>
    private void HandleRiverSaved()
    {
        if (isGameEnded || isReloadingScene) return;
        isGameEnded = true;

        if (playerMovement != null)
        {
            playerMovement.SetCanMove(false);
        }

        UnlockCursorForUI();

        if (winPanel != null)
        {
            winPanel.SetActive(true);
        }
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

    private void UnlockCursorForUI()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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