using UnityEngine;

/// <summary>
/// CANOPY: The Last Run - Sel Yakınlığı ve Darbe Kamera Sarsıntısı (Camera Shake) Yöneticisi.
/// PlayerLook bileşeni ile çakışmadan tamamen toplamsal (additive offset) olarak çalışır.
/// Sel oyuncuya yaklaştıkça (varsayılan < 25m) sarsıntı şiddeti artar.
/// </summary>
[DefaultExecutionOrder(100)]
public class CameraShake : MonoBehaviour
{
    private const float NOISE_OFFSET_X = 19.3f;
    private const float NOISE_OFFSET_Y = 47.7f;
    private const float NOISE_OFFSET_Z = 83.1f;

    [Header("Referanslar")]
    [Tooltip("Sarsıntı ofsetinin çakışmadan iletileceği PlayerLook bileşeni (boşsa otomatik bulunur).")]
    [SerializeField] private PlayerLook playerLook;

    [Tooltip("Mesafeye göre sürekli sarsıntı şiddetini hesaplamak için FloodChaser referansı (opsiyonel).")]
    [SerializeField] private FloodChaser floodChaser;

    [Header("Sel Yakınlık Sarsıntısı Ayarları")]
    [Tooltip("Sel bu mesafeden (metre) daha yakına girdiğinde kamera sarsıntısı başlar (varsayılan 25 m).")]
    [SerializeField] private float shakeStartDistance = 25f;

    [Tooltip("Sarsıntının maksimum şiddete ulaştığı en yakın mesafe (metre, varsayılan 2 m).")]
    [SerializeField] private float shakeMaxDistance = 2f;

    [Header("Sarsıntı Genliği ve Frekansı")]
    [Tooltip("Perlin gürültüsü sarsıntı frekansı (Hz).")]
    [SerializeField] private float shakeFrequency = 18f;

    [Tooltip("Maksimum yakınlıkta uygulanacak pozisyonel sarsıntı genliği (metre).")]
    [SerializeField] private Vector3 maxPositionOffset = new Vector3(0.035f, 0.045f, 0f);

    [Tooltip("Maksimum yakınlıkta uygulanacak açısal sarsıntı genliği (derece - Pitch, Yaw, Roll).")]
    [SerializeField] private Vector3 maxRotationEulerOffset = new Vector3(1.1f, 0.6f, 1.4f);

    [Tooltip("Sarsıntı şiddetinin değişim yumuşatma hızı.")]
    [SerializeField] private float intensityLerpSpeed = 8f;

    [Tooltip("Yakalanma veya darbe anındaki anlık sarsıntının sönümlenme hızı (birim/sn).")]
    [SerializeField] private float burstDecayRate = 2.5f;

    // Dahili çalışma değişkenleri
    private float targetContinuousIntensity;
    private float currentContinuousIntensity;
    private float burstIntensity;
    private float noiseTime;

    private Vector3 currentPosOffset;
    private Vector3 currentRotOffset;
    private Vector3 lastAppliedStandalonePosOffset;
    private Quaternion lastAppliedStandaloneRotOffset = Quaternion.identity;

    // Dış erişim özellikleri (Debug HUD için)
    public float CurrentTotalIntensity => Mathf.Clamp01(currentContinuousIntensity + burstIntensity);
    public float ShakeStartDistance => shakeStartDistance;

    private void Awake()
    {
        if (playerLook == null)
        {
            playerLook = GetComponent<PlayerLook>();
            if (playerLook == null)
            {
                playerLook = GetComponentInParent<PlayerLook>();
            }
            if (playerLook == null)
            {
                playerLook = FindObjectOfType<PlayerLook>();
            }
        }

        if (floodChaser == null)
        {
            floodChaser = FindObjectOfType<FloodChaser>();
        }
    }

    private void OnDisable()
    {
        ClearOffsets();
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // Eğer FloodChaser referansı varsa ve dışarıdan manuel set edilmiyorsa mesafeye göre otomatik güncelle
        if (floodChaser != null)
        {
            if (floodChaser.IsActive && !floodChaser.IsStopped)
            {
                float dist = floodChaser.DistanceToPlayer;
                if (dist < shakeStartDistance)
                {
                    float proximity = 1f - Mathf.InverseLerp(shakeMaxDistance, shakeStartDistance, dist);
                    // Yaklaştıkça daha hissedilir olması için hafif eğrisel artış
                    targetContinuousIntensity = Mathf.Clamp01(proximity * proximity * 0.65f + proximity * 0.35f);
                }
                else
                {
                    targetContinuousIntensity = 0f;
                }
            }
            else
            {
                targetContinuousIntensity = 0f;
            }
        }

        currentContinuousIntensity = Mathf.Lerp(currentContinuousIntensity, targetContinuousIntensity, intensityLerpSpeed * dt);

        if (burstIntensity > 0f)
        {
            burstIntensity = Mathf.Max(0f, burstIntensity - burstDecayRate * dt);
        }

        float totalIntensity = CurrentTotalIntensity;
        if (totalIntensity <= 0.001f)
        {
            currentPosOffset = Vector3.Lerp(currentPosOffset, Vector3.zero, intensityLerpSpeed * dt);
            currentRotOffset = Vector3.Lerp(currentRotOffset, Vector3.zero, intensityLerpSpeed * dt);
        }
        else
        {
            noiseTime += dt * shakeFrequency * Mathf.Lerp(0.85f, 1.35f, totalIntensity);

            float nx = (Mathf.PerlinNoise(noiseTime, NOISE_OFFSET_X) - 0.5f) * 2f;
            float ny = (Mathf.PerlinNoise(NOISE_OFFSET_Y, noiseTime) - 0.5f) * 2f;
            float nz = (Mathf.PerlinNoise(noiseTime * 0.9f, NOISE_OFFSET_Z) - 0.5f) * 2f;

            currentPosOffset = new Vector3(
                nx * maxPositionOffset.x * totalIntensity,
                ny * maxPositionOffset.y * totalIntensity,
                nz * maxPositionOffset.z * totalIntensity
            );

            currentRotOffset = new Vector3(
                ny * maxRotationEulerOffset.x * totalIntensity,
                nx * maxRotationEulerOffset.y * totalIntensity,
                nz * maxRotationEulerOffset.z * totalIntensity
            );
        }

        // PlayerLook mevcutsa sarsıntıyı doğrudan PlayerLook'un harici ofset kanalına ilet (sıfır çakışma)
        if (playerLook != null)
        {
            playerLook.SetExternalShakeOffset(currentPosOffset, currentRotOffset);
        }
    }

    private void LateUpdate()
    {
        // Eğer sahnede PlayerLook yoksa (bağımsız bir kamera üzerinde çalışıyorsa) LateUpdate'te ofset uygula
        if (playerLook != null)
            return;

        transform.localPosition -= lastAppliedStandalonePosOffset;
        transform.localRotation = transform.localRotation * Quaternion.Inverse(lastAppliedStandaloneRotOffset);

        lastAppliedStandalonePosOffset = currentPosOffset;
        lastAppliedStandaloneRotOffset = Quaternion.Euler(currentRotOffset);

        transform.localPosition += lastAppliedStandalonePosOffset;
        transform.localRotation = transform.localRotation * lastAppliedStandaloneRotOffset;
    }

    /// <summary>
    /// Sel mesafesine göre sürekli sarsıntı hedef şiddetini (0..1) dışarıdan ayarlar.
    /// </summary>
    public void SetContinuousShakeIntensity(float normalizedIntensity)
    {
        targetContinuousIntensity = Mathf.Clamp01(normalizedIntensity);
    }

    /// <summary>
    /// Yakalanma veya patlama gibi anlarda kısa süreli ekstra darbe sarsıntısı tetikler.
    /// </summary>
    public void TriggerShakePulse(float impulseIntensity = 0.85f)
    {
        burstIntensity = Mathf.Clamp01(burstIntensity + impulseIntensity);
        if (playerLook != null)
        {
            playerLook.AddCameraShakeImpulse(impulseIntensity * 0.6f);
        }
    }

    /// <summary>
    /// Respawn veya oyun sonu anında tüm sarsıntıyı anında sıfırlar.
    /// </summary>
    public void ClearOffsets()
    {
        targetContinuousIntensity = 0f;
        currentContinuousIntensity = 0f;
        burstIntensity = 0f;
        currentPosOffset = Vector3.zero;
        currentRotOffset = Vector3.zero;

        if (playerLook != null)
        {
            playerLook.SetExternalShakeOffset(Vector3.zero, Vector3.zero);
        }
        else
        {
            transform.localPosition -= lastAppliedStandalonePosOffset;
            transform.localRotation = transform.localRotation * Quaternion.Inverse(lastAppliedStandaloneRotOffset);
            lastAppliedStandalonePosOffset = Vector3.zero;
            lastAppliedStandaloneRotOffset = Quaternion.identity;
        }
    }
}

