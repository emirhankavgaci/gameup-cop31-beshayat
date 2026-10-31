using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Unity 6 (6000.x) uyumlu 3D nehir su seviyesi, 1 dakikalık kuraklık sayacı,
/// su barı UI entegrasyonu ve baraj su katkısı kontrolcüsü.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class WaterLevelController : MonoBehaviour
{
    [Header("Su Miktarı ve 60 Saniye Tükeniş Ayarları")]
    [SerializeField] private float maxWaterAmount = 100f; // Toplam su kapasitesi (Çöp +5, Dal +10 ekler)
    [SerializeField] private float baseDrainDurationSeconds = 60f; // Hiç müdahale edilmezse suyun tamamen bitme süresi (1 dk = 60 sn)
    [SerializeField] private float droughtEscalationPerSecond = 0.015f; // Zaman geçtikçe kuraklığın şiddetlenme hızı
    [SerializeField] private float minDrainRateFloor = 0.35f; // Baraj bitmeden su azalma hızının düşebileceği en alt sınır
    [SerializeField] private float waterFillLerpSpeed = 25f; // Su eklendiğinde barın ve yüksekliğin yükselme hızı

    [Header("Fiziksel Yükseklik ve Baraj Sınırı")]
    [SerializeField] private bool useScenePositionAsMaxY = true; // Sahnede koyduğun Y yüksekliğini başlangıç (Max) kabul eder
    [SerializeField] private float maxWaterY = 1.0f; // Sağlıklı su yüksekliği
    [SerializeField] private float minWaterY = -0.5f; // Nehrin kuruduğu dip yükseklik
    [SerializeField] private Transform damCeilingReference; // Atanırsa su yüksekliği asla bu noktanın üstüne çıkmaz
    [SerializeField] private float damMaxWaterOffset = -0.15f; // Baraj üst sınırından ne kadar aşağıda kalacağı

    [Header("Karakterin Suyun Üstünde Kalması")]
    [SerializeField] private bool solidWaterSurface = true; // True ise karakter ve eşyalar suyun içinden düşmez, üstünde durur
    [SerializeField] private float waterSpeedMultiplier = 0.75f; // Oyuncu suyun üzerindeyken hız çarpanı

    [Header("Su Akış Efekti (Texture Offset)")]
    [SerializeField] private Vector2 flowSpeed = new Vector2(0f, 0.5f);

    [Header("Ekran Yanı Su Miktarı Barı (Opsiyonel Doğrudan Bağlantı)")]
    [SerializeField] private Slider waterBarSlider; // 0 - 1 arası veya 0 - 100 arası UI Slider
    [SerializeField] private Image waterBarFillImage; // Filled Image kullanıyorsan doğrudan buraya sürükleyebilirsin

    private float currentWaterAmount;
    private float targetWaterAmount;
    private float baseDrainRate; // 100 / 60 = 1.666f su/sn
    private float totalDrainReduction = 0f;
    private float elapsedDroughtTime = 0f;

    private float currentWaterY;
    private bool isDryingActive = true;
    private bool isRiverSaved = false;
    private bool hasRiverDriedOutTriggered = false;

    private BoxCollider boxCollider;
    private Renderer rend;
    private Vector2 currentTextureOffset;

    /// <summary>
    /// UI güncellemeleri için su seviyesi oranını (0.0f - 1.0f arası) döndürür.
    /// </summary>
    public event Action<float> OnWaterLevelRatioChanged;

    /// <summary>
    /// Su seviyesi sıfırlandığında (kuruduğunda) bir kez tetiklenir.
    /// </summary>
    public event Action OnRiverDriedOut;

    /// <summary>
    /// 5x4 Baraj tamamlanıp nehir kurtarıldığında ("Korku değil ümit vermek" kazanma durumu) tetiklenir.
    /// </summary>
    public event Action OnRiverSaved;

    public float CurrentWaterRatio => Mathf.Clamp01(currentWaterAmount / maxWaterAmount);
    public float CurrentWaterAmount => currentWaterAmount;
    public bool IsRiverSaved => isRiverSaved;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
        // Karakterin suyun altına düşmemesi ve suyun üstünde yürümesi için katı yüzey seçeneği
        boxCollider.isTrigger = !solidWaterSurface;

        rend = GetComponent<Renderer>();
        if (rend != null && rend.material != null)
        {
            currentTextureOffset = rend.material.mainTextureOffset;
        }
    }

    private void Start()
    {
        if (useScenePositionAsMaxY)
        {
            float depthRange = Mathf.Max(0.1f, Mathf.Abs(maxWaterY - minWaterY));
            maxWaterY = transform.position.y;
            minWaterY = maxWaterY - depthRange;
        }

        // Eğer baraj tavan referansı verildiyse, suyun barajın üstüne taşmasını engelle
        if (damCeilingReference != null)
        {
            maxWaterY = Mathf.Min(maxWaterY, damCeilingReference.position.y + damMaxWaterOffset);
        }

        baseDrainRate = maxWaterAmount / Mathf.Max(1f, baseDrainDurationSeconds);
        currentWaterAmount = maxWaterAmount;
        targetWaterAmount = maxWaterAmount;
        currentWaterY = maxWaterY;
        hasRiverDriedOutTriggered = false;

        transform.position = new Vector3(transform.position.x, currentWaterY, transform.position.z);
        UpdateWaterUI(1f);
        OnWaterLevelRatioChanged?.Invoke(1f);
    }

    private void OnDestroy()
    {
        // Sahne yeniden yüklendiğinde silinmiş UI Image referanslarına çağrı gitmesini engelle
        OnWaterLevelRatioChanged = null;
        OnRiverDriedOut = null;
        OnRiverSaved = null;
    }

    private void Update()
    {
        // 1. Su dokusunu (texture) akış hızına göre kaydır
        if (rend != null && rend.material != null)
        {
            currentTextureOffset += flowSpeed * Time.deltaTime;
            rend.material.mainTextureOffset = currentTextureOffset;
        }

        // 2. Kuruma aktifse (ve nehir henüz kurtarılmadıysa) su miktarını azalt
        if (isDryingActive && !isRiverSaved)
        {
            elapsedDroughtTime += Time.deltaTime;

            // Zaman geçtikçe kuraklık baskısı artar; böylece baraj tamamlanmazsa su kaçınılmaz olarak tükenir
            float escalation = elapsedDroughtTime * droughtEscalationPerSecond;
            float effectiveDrainRate = Mathf.Max(minDrainRateFloor, baseDrainRate - totalDrainReduction) + escalation;

            targetWaterAmount -= effectiveDrainRate * Time.deltaTime;
        }

        // 3. Su miktarını 0 ile maxWaterAmount (100) arasında sınırla
        targetWaterAmount = Mathf.Clamp(targetWaterAmount, 0f, maxWaterAmount);
        currentWaterAmount = Mathf.MoveTowards(currentWaterAmount, targetWaterAmount, waterFillLerpSpeed * Time.deltaTime);

        // 4. Oran hesabı (0.0f - 1.0f) ve fiziksel Y yüksekliği karşılığı
        float ratio = Mathf.Clamp01(currentWaterAmount / maxWaterAmount);

        float effectiveMaxY = maxWaterY;
        if (damCeilingReference != null)
        {
            effectiveMaxY = Mathf.Min(maxWaterY, damCeilingReference.position.y + damMaxWaterOffset);
        }

        currentWaterY = Mathf.Lerp(minWaterY, effectiveMaxY, ratio);
        transform.position = new Vector3(transform.position.x, currentWaterY, transform.position.z);

        // 5. UI Barı ve Event güncellemeleri
        UpdateWaterUI(ratio);
        OnWaterLevelRatioChanged?.Invoke(ratio);

        // 6. Nehir kuruma kontrolü
        if (currentWaterAmount <= 0.01f)
        {
            if (!hasRiverDriedOutTriggered)
            {
                hasRiverDriedOutTriggered = true;
                OnRiverDriedOut?.Invoke();
            }
        }
        else
        {
            hasRiverDriedOutTriggered = false;
        }
    }

    private void UpdateWaterUI(float ratio)
    {
        if (waterBarSlider != null && waterBarSlider.gameObject != null)
        {
            waterBarSlider.value = waterBarSlider.maxValue > 1.5f ? ratio * maxWaterAmount : ratio;
        }

        if (waterBarFillImage != null && waterBarFillImage.gameObject != null)
        {
            waterBarFillImage.fillAmount = ratio;
        }
    }

    /// <summary>
    /// Baraja yeni bir çöp (+5) veya dal (+10) eklendiğinde hem su miktarını artırır hem de azalma hızını düşürür.
    /// </summary>
    public void AddWaterByDebris(float waterBonusAmount, float drainReductionAmount = 0f)
    {
        targetWaterAmount = Mathf.Clamp(targetWaterAmount + waterBonusAmount, 0f, maxWaterAmount);
        totalDrainReduction += Mathf.Max(0f, drainReductionAmount);
    }

    /// <summary>
    /// 5x4 Baraj tamamen inşa edildiğinde ("Korku değil ümit vermek" kazanma durumu) nehri kurtarır ve doldurur.
    /// </summary>
    public void CompleteDamAndSaveRiver()
    {
        isRiverSaved = true;
        isDryingActive = false;
        targetWaterAmount = maxWaterAmount;
        OnRiverSaved?.Invoke();
    }

    /// <summary>
    /// Suyun kuruma/alçalma durumunu başlatır veya durdurur.
    /// </summary>
    public void SetDryingState(bool active)
    {
        isDryingActive = active;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerMovement3D>(out PlayerMovement3D player))
        {
            player.ApplySpeedModifier(waterSpeedMultiplier);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerMovement3D>(out PlayerMovement3D player))
        {
            player.ResetSpeed();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.TryGetComponent<PlayerMovement3D>(out PlayerMovement3D player))
        {
            player.ApplySpeedModifier(waterSpeedMultiplier);
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.collider.TryGetComponent<PlayerMovement3D>(out PlayerMovement3D player))
        {
            player.ResetSpeed();
        }
    }
}