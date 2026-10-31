using System;
using UnityEngine;

/// <summary>
/// Unity 2022 / 2023 / Unity 6 uyumlu 3D nehir su seviyesi ve kuraklık simülasyonu kontrolcüsü.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class WaterLevelController : MonoBehaviour
{
    [Header("Su Seviyesi Sınırları")]
    [SerializeField] private bool useScenePositionAsMaxY = true; // Sahnede koyduğun Y yüksekliğini başlangıç (Max) kabul eder
    [SerializeField] private float maxWaterY = 1.0f; // Baraj tamken veya başlangıçtaki sağlıklı su yüksekliği
    [SerializeField] private float minWaterY = -0.5f; // Kuraklıkta nehrin kuruduğu en dip yükseklik
    [SerializeField] private float droughtDrainRate = 0.05f; // Saniyede suyun kuruma/alçalma hızı
    [SerializeField] private float fillSpeed = 1.5f; // Baraja çöp/kütük eklendiğinde suyun yükselme hızı

    [Header("Su Akış Efekti (Texture Offset)")]
    [SerializeField] private Vector2 flowSpeed = new Vector2(0f, 0.5f); // Suyun doku kayma (akış) hızı

    [Header("Oyuncu Su İçi Hız Çarpanı")]
    [SerializeField] private float waterSpeedMultiplier = 0.6f; // Oyuncu suya girdiğinde uygulanacak hız çarpanı

    private float targetWaterY;
    private float currentWaterY;
    private bool isDryingActive = true;
    private bool hasRiverDriedOutTriggered = false;
    private BoxCollider boxCollider;
    private Renderer rend;
    private Vector2 currentTextureOffset;

    /// <summary>
    /// UI güncellemeleri için su seviyesi oranını (0.0f - 1.0f arası) döndürür.
    /// </summary>
    public event Action<float> OnWaterLevelRatioChanged;

    /// <summary>
    /// Su seviyesi minWaterY değerine düştüğünde bir kez tetiklenir.
    /// </summary>
    public event Action OnRiverDriedOut;

    /// <summary>
    /// Mevcut su seviyesi oranını (0.0f - 1.0f) dışarıdan okumak için yardımcı property.
    /// </summary>
    public float CurrentWaterRatio => Mathf.InverseLerp(minWaterY, maxWaterY, currentWaterY);

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;

        rend = GetComponent<Renderer>();
        if (rend != null && rend.material != null)
        {
            currentTextureOffset = rend.material.mainTextureOffset;
        }
    }

    private void Start()
    {
        // Nehrin sahnede koyduğun Y pozisyonundan başlamasını sağla ve kuruma mesafesini koru
        if (useScenePositionAsMaxY)
        {
            float depthRange = Mathf.Max(0.1f, Mathf.Abs(maxWaterY - minWaterY));
            maxWaterY = transform.position.y;
            minWaterY = maxWaterY - depthRange;
        }

        targetWaterY = maxWaterY;
        currentWaterY = maxWaterY;
        hasRiverDriedOutTriggered = false;

        transform.position = new Vector3(transform.position.x, currentWaterY, transform.position.z);

        float initialRatio = Mathf.InverseLerp(minWaterY, maxWaterY, currentWaterY);
        OnWaterLevelRatioChanged?.Invoke(initialRatio);
    }

    private void Update()
    {
        // 1. Su dokusunu (texture) akış hızına göre kaydır
        if (rend != null && rend.material != null)
        {
            currentTextureOffset += flowSpeed * Time.deltaTime;
            rend.material.mainTextureOffset = currentTextureOffset;
        }

        // 2. Kuruma aktifse hedef su seviyesini zamanla azalt
        if (isDryingActive)
        {
            targetWaterY -= droughtDrainRate * Time.deltaTime;
        }

        // 3. Hedef yüksekliği min ve max sınırlar arasında tut
        targetWaterY = Mathf.Clamp(targetWaterY, minWaterY, maxWaterY);

        // 4. Mevcut su yüksekliğini hedef yüksekliğe doğru yumuşakça taşı
        currentWaterY = Mathf.MoveTowards(currentWaterY, targetWaterY, fillSpeed * Time.deltaTime);

        // 5. Nehrin Y pozisyonunu güncelle
        transform.position = new Vector3(transform.position.x, currentWaterY, transform.position.z);

        // 6. Oran hesabı ve UI Event bildirimi
        float ratio = Mathf.InverseLerp(minWaterY, maxWaterY, currentWaterY);
        OnWaterLevelRatioChanged?.Invoke(ratio);

        // 7. Nehir kuruma kontrolü (minWaterY + 0.01f altına indiğinde yalnızca bir kez tetiklenir)
        if (currentWaterY <= minWaterY + 0.01f)
        {
            if (!hasRiverDriedOutTriggered)
            {
                hasRiverDriedOutTriggered = true;
                OnRiverDriedOut?.Invoke();
            }
        }
        else
        {
            // Su tekrar yükselirse kuruma bayrağını sıfırla
            hasRiverDriedOutTriggered = false;
        }
    }

    /// <summary>
    /// Baraja yeni bir çöp, dal veya kütük eklendiğinde hedef su seviyesini artırır.
    /// </summary>
    /// <param name="amount">Eklenecek su yüksekliği miktarı</param>
    public void AddWaterByDebris(float amount)
    {
        targetWaterY = Mathf.Clamp(targetWaterY + amount, minWaterY, maxWaterY);
    }

    /// <summary>
    /// Suyun kuruma/alçalma durumunu başlatır veya durdurur.
    /// </summary>
    /// <param name="active">Kuruma aktif mi?</param>
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
}