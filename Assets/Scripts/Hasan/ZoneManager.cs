using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

/// <summary>
/// Bir zorluk bölgesinin (Easy, Medium, Hard) sel hızı ve atmosfer (sis/ışık) ayarlarını tutan veri sınıfı.
/// </summary>
[Serializable]
public class ZoneSettings
{
    [Tooltip("Bölgenin görünen adı (örneğin Easy, Medium, Hard).")]
    public string zoneName = "Easy";

    [Tooltip("Bu bölgedeki sel temel hızı (m/s). Varsayılanlar: Easy=6, Medium=8.5, Hard=10.5.")]
    public float floodBaseSpeed = 6f;

    [Tooltip("Bu bölgedeki sis rengi (RenderSettings.fogColor).")]
    public Color fogColor = new Color(0.55f, 0.68f, 0.62f, 1f);

    [Tooltip("Bu bölgedeki ortam ışığı rengi (RenderSettings.ambientLight).")]
    public Color ambientColor = new Color(0.45f, 0.52f, 0.48f, 1f);

    [Tooltip("Bu bölgedeki üstel sis yoğunluğu (RenderSettings.fogDensity).")]
    [Range(0f, 0.1f)]
    public float fogDensity = 0.012f;

    [Tooltip("Bu bölgeye girildiğinde sel hızının ve atmosferin geçiş süresi (saniye).")]
    public float blendTime = 2.5f;
}

/// <summary>
/// CANOPY: The Last Run - Bölge (Zone) ve Atmosfer Yöneticisi.
/// Easy, Medium ve Hard bölgeleri arasında geçiş yapar; sel hızını (FloodChaser.SetBaseSpeed)
/// ve sahne sis/ışık ayarlarını (RenderSettings) yumuşakça günceller.
/// </summary>
public class ZoneManager : MonoBehaviour
{
    private const float DEFAULT_EASY_SPEED = 6.0f;
    private const float DEFAULT_MEDIUM_SPEED = 8.5f;
    private const float DEFAULT_HARD_SPEED = 10.5f;

    [Header("Referanslar")]
    [Tooltip("Bölgeye göre temel hızı değiştirilecek FloodChaser bileşeni.")]
    [SerializeField] private FloodChaser floodChaser;

    [Header("Bölge Tanımları (Easy / Medium / Hard)")]
    [Tooltip("Oyun başlangıcında aktif olacak bölge indeksi (varsayılan 0 = Easy).")]
    [SerializeField] private int initialZoneIndex = 0;

    [Tooltip("Sahnede sis kapalıysa başlangıçta otomatik olarak aktif et.")]
    [SerializeField] private bool enableFogOnStart = true;

    [Tooltip("Bölgelerin hız ve atmosfer ayarları listesi.")]
    [SerializeField] private ZoneSettings[] zones = new ZoneSettings[]
    {
        new ZoneSettings
        {
            zoneName = "Easy",
            floodBaseSpeed = DEFAULT_EASY_SPEED,
            fogColor = new Color(0.55f, 0.68f, 0.62f, 1f),
            ambientColor = new Color(0.48f, 0.55f, 0.50f, 1f),
            fogDensity = 0.010f,
            blendTime = 2.0f
        },
        new ZoneSettings
        {
            zoneName = "Medium",
            floodBaseSpeed = DEFAULT_MEDIUM_SPEED,
            fogColor = new Color(0.42f, 0.50f, 0.52f, 1f),
            ambientColor = new Color(0.36f, 0.42f, 0.45f, 1f),
            fogDensity = 0.018f,
            blendTime = 2.5f
        },
        new ZoneSettings
        {
            zoneName = "Hard",
            floodBaseSpeed = DEFAULT_HARD_SPEED,
            fogColor = new Color(0.28f, 0.32f, 0.36f, 1f),
            ambientColor = new Color(0.25f, 0.28f, 0.33f, 1f),
            fogDensity = 0.026f,
            blendTime = 3.0f
        }
    };

    [Header("Olaylar (Ses ve UI Bağlantısı İçin)")]
    [Tooltip("Bölge değiştiğinde tetiklenen UnityEvent (bölge adını parametre olarak geçirir).")]
    [SerializeField] private UnityEvent<string> onZoneChangedUnityEvent;

    /// <summary>
    /// Yeni bir bölgeye girildiğinde tetiklenir: (zoneIndex, zoneSettings).
    /// </summary>
    public event Action<int, ZoneSettings> OnZoneChanged;

    // Dahili çalışma durumu
    private int currentZoneIndex = -1;
    private bool isBlendingAtmosphere;
    private float blendElapsed;
    private float activeBlendDuration;

    private Color startFogColor;
    private Color targetFogColor;
    private Color startAmbientColor;
    private Color targetAmbientColor;
    private float startFogDensity;
    private float targetFogDensity;

    // Dış erişim özellikleri (FloodDebugHUD için)
    public int CurrentZoneIndex => currentZoneIndex;
    public ZoneSettings CurrentZone => (zones != null && currentZoneIndex >= 0 && currentZoneIndex < zones.Length)
        ? zones[currentZoneIndex]
        : null;
    public string CurrentZoneName => CurrentZone != null ? CurrentZone.zoneName : "YOK";

    private void Awake()
    {
        if (floodChaser == null)
        {
            floodChaser = FindObjectOfType<FloodChaser>();
        }

        if (enableFogOnStart)
        {
            RenderSettings.fog = true;
        }
    }

    private void Start()
    {
        if (zones != null && zones.Length > 0)
        {
            int validIndex = Mathf.Clamp(initialZoneIndex, 0, zones.Length - 1);
            ApplyZoneImmediate(validIndex);
        }
    }

    private void Update()
    {
        if (!isBlendingAtmosphere)
            return;

        blendElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(blendElapsed / Mathf.Max(0.01f, activeBlendDuration));
        float smoothT = Mathf.SmoothStep(0f, 1f, t);

        RenderSettings.fogColor = Color.Lerp(startFogColor, targetFogColor, smoothT);
        RenderSettings.ambientLight = Color.Lerp(startAmbientColor, targetAmbientColor, smoothT);
        RenderSettings.fogDensity = Mathf.Lerp(startFogDensity, targetFogDensity, smoothT);

        if (t >= 1f)
        {
            isBlendingAtmosphere = false;
        }
    }

    /// <summary>
    /// Oyuncu bir ZoneTrigger'a girdiğinde çağrılır; sel hızını ve atmosfer ayarlarını yumuşak geçişle değiştirir.
    /// </summary>
    public void EnterZone(int index)
    {
        if (zones == null || index < 0 || index >= zones.Length)
            return;

        if (currentZoneIndex == index)
            return;

        currentZoneIndex = index;
        ZoneSettings targetZone = zones[index];

        if (floodChaser != null)
        {
            floodChaser.SetBaseSpeed(targetZone.floodBaseSpeed, targetZone.blendTime);
        }

        if (targetZone.blendTime <= 0.01f)
        {
            RenderSettings.fogColor = targetZone.fogColor;
            RenderSettings.ambientLight = targetZone.ambientColor;
            RenderSettings.fogDensity = targetZone.fogDensity;
            isBlendingAtmosphere = false;
        }
        else
        {
            startFogColor = RenderSettings.fogColor;
            targetFogColor = targetZone.fogColor;

            startAmbientColor = RenderSettings.ambientLight;
            targetAmbientColor = targetZone.ambientColor;

            startFogDensity = RenderSettings.fogDensity;
            targetFogDensity = targetZone.fogDensity;

            activeBlendDuration = targetZone.blendTime;
            blendElapsed = 0f;
            isBlendingAtmosphere = true;
        }

        OnZoneChanged?.Invoke(currentZoneIndex, targetZone);
        onZoneChangedUnityEvent?.Invoke(targetZone.zoneName);
    }

    private void ApplyZoneImmediate(int index)
    {
        currentZoneIndex = index;
        ZoneSettings zone = zones[index];

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.fogColor = zone.fogColor;
        RenderSettings.ambientLight = zone.ambientColor;
        RenderSettings.fogDensity = zone.fogDensity;
        isBlendingAtmosphere = false;

        if (floodChaser != null)
        {
            floodChaser.SetBaseSpeed(zone.floodBaseSpeed, 0f);
        }

        OnZoneChanged?.Invoke(currentZoneIndex, zone);
        onZoneChangedUnityEvent?.Invoke(zone.zoneName);
    }
}

