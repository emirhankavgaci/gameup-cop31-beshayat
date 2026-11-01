using UnityEngine;

/// <summary>
/// CANOPY: The Last Run - Sel Materyali UV Kaydırma (Texture Offset Scroller) ve Otomatik Çamurlu Su Dokusu Üretici.
/// Render Pipeline'dan bağımsızdır (Built-in Standard '_MainTex' ve URP Lit/Unlit '_BaseMap'
/// özelliklerini otomatik algılar, özel shader veya internetten hazır materyal gerektirmez).
/// Eğer objede doku (texture) atanmamışsa çalışma zamanında otomatik olarak köpüklü/çamurlu bir su dokusu üretir.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class WaterScroller : MonoBehaviour
{
    private const string URP_BASE_MAP_PROP = "_BaseMap";
    private const string BUILTIN_MAIN_TEX_PROP = "_MainTex";
    private const string BUMP_MAP_PROP = "_BumpMap";
    private const string DETAIL_ALBEDO_PROP = "_DetailAlbedoMap";
    private const string URP_BASE_COLOR_PROP = "_BaseColor";
    private const string BUILTIN_COLOR_PROP = "_Color";

    [Header("Ana Dokuyu (Albedo / BaseMap) Kaydırma Hızları")]
    [Tooltip("Ana su dokusunun X eksenindeki (yatay/yanal) kayma hızı.")]
    [SerializeField] private float scrollSpeedX = 0.08f;

    [Tooltip("Ana su dokusunun Y eksenindeki (akış yönü) kayma hızı.")]
    [SerializeField] private float scrollSpeedY = -0.55f;

    [Tooltip("Dokunun yüzeyde kaç kez tekrarlanacağı (Tiling).")]
    [SerializeField] private Vector2 textureTiling = new Vector2(6f, 4f);

    [Header("İkincil Doku / Normal Map Kaydırma Hızları")]
    [Tooltip("Normal map veya detay dokusunu farklı hızda kaydırarak dalga karmaşası oluşturur.")]
    [SerializeField] private bool scrollSecondaryMaps = true;

    [Tooltip("İkincil dokunun X eksenindeki kayma hızı.")]
    [SerializeField] private float secondaryScrollSpeedX = -0.06f;

    [Tooltip("İkincil dokunun Y eksenindeki kayma hızı.")]
    [SerializeField] private float secondaryScrollSpeedY = -0.28f;

    [Header("Sel Hızına Göre Dinamik Akış Çarpanı")]
    [Tooltip("Selin gerçek ilerleme hızına göre UV kaydırma hızını dinamik olarak ölçekle.")]
    [SerializeField] private bool scaleWithFloodSpeed = true;

    [Tooltip("Dinamik ölçekleme için referans alınan standart sel hızı (m/s).")]
    [SerializeField] private float referenceFloodSpeed = 8.5f;

    [Tooltip("İsteğe bağlı: Hızını okuyacağı FloodChaser referansı (boşsa ebeveynde arar).")]
    [SerializeField] private FloodChaser floodChaser;

    [Header("Otomatik Çamurlu Sel Görünümü (İnternetten Materyal Gerekmez)")]
    [Tooltip("Açık olduğunda materyal rengini otomatik olarak çamurlu sel rengine ayarlar.")]
    [SerializeField] private bool applyMuddyFloodTintOnStart = true;

    [Tooltip("Materyalde doku (Texture) yoksa otomatik olarak dalgalı/köpüklü çamurlu su dokusu üretir.")]
    [SerializeField] private bool generateProceduralWaterTextureIfMissing = true;

    [Tooltip("Hafif şeffaf mavi-kahve çamurlu sel ana rengi.")]
    [SerializeField] private Color muddyFloodColor = new Color(0.28f, 0.36f, 0.38f, 0.88f);

    [Tooltip("Dalga sırtlarındaki açık çamur/köpük rengi.")]
    [SerializeField] private Color waveFoamHighlightColor = new Color(0.58f, 0.64f, 0.62f, 0.92f);

    private Renderer targetRenderer;
    private Material runtimeMaterial;
    private Texture2D generatedTexture;
    private Vector2 primaryOffset;
    private Vector2 secondaryOffset;

    private bool hasUrpBaseMap;
    private bool hasBuiltinMainTex;
    private bool hasBumpMap;
    private bool hasDetailMap;

    private void Awake()
    {
        targetRenderer = GetComponent<Renderer>();
        if (floodChaser == null)
        {
            floodChaser = GetComponentInParent<FloodChaser>();
        }

        if (targetRenderer != null)
        {
            runtimeMaterial = targetRenderer.material;
            CacheShaderProperties();

            if (generateProceduralWaterTextureIfMissing)
            {
                EnsureWaterTextureAssigned();
            }

            if (applyMuddyFloodTintOnStart && runtimeMaterial != null)
            {
                // Prosedürel doku kendi renklerini barındırdığı için beyazla çarpıyoruz, doku yoksa doğrudan çamur rengini basıyoruz
                Color finalTint = generatedTexture != null ? Color.white : muddyFloodColor;
                ApplyTintColor(finalTint);
            }

            ApplyTextureTiling();
        }
    }

    private void CacheShaderProperties()
    {
        if (runtimeMaterial == null)
            return;

        hasUrpBaseMap = runtimeMaterial.HasProperty(URP_BASE_MAP_PROP);
        hasBuiltinMainTex = runtimeMaterial.HasProperty(BUILTIN_MAIN_TEX_PROP);
        hasBumpMap = runtimeMaterial.HasProperty(BUMP_MAP_PROP);
        hasDetailMap = runtimeMaterial.HasProperty(DETAIL_ALBEDO_PROP);

        if (hasUrpBaseMap)
        {
            primaryOffset = runtimeMaterial.GetTextureOffset(URP_BASE_MAP_PROP);
        }
        else if (hasBuiltinMainTex)
        {
            primaryOffset = runtimeMaterial.GetTextureOffset(BUILTIN_MAIN_TEX_PROP);
        }
    }

    /// <summary>
    /// Eğer kullanıcının materyalinde herhangi bir su dokusu yoksa (düz beyaz küpse),
    /// Perlin gürültüsü ile kesintisiz (seamless) dalgalı ve köpüklü çamurlu sel dokusu üretir.
    /// </summary>
    private void EnsureWaterTextureAssigned()
    {
        if (runtimeMaterial == null)
            return;

        Texture existingTex = null;
        if (hasUrpBaseMap)
        {
            existingTex = runtimeMaterial.GetTexture(URP_BASE_MAP_PROP);
        }
        if (existingTex == null && hasBuiltinMainTex)
        {
            existingTex = runtimeMaterial.GetTexture(BUILTIN_MAIN_TEX_PROP);
        }

        if (existingTex != null)
            return;

        const int texSize = 128;
        generatedTexture = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            name = "ProceduralMuddyFloodTex"
        };

        Color darkMud = new Color(muddyFloodColor.r * 0.65f, muddyFloodColor.g * 0.65f, muddyFloodColor.b * 0.70f, 1f);

        for (int y = 0; y < texSize; y++)
        {
            float v = y / (float)texSize;
            for (int x = 0; x < texSize; x++)
            {
                float u = x / (float)texSize;

                // Seamless (kesintisiz tekrar eden) dalga ve köpük deseni
                float wave1 = Mathf.Sin((u * 2f + Mathf.Sin(v * Mathf.PI * 2f) * 0.25f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                float wave2 = Mathf.Cos((v * 3f + Mathf.Cos(u * Mathf.PI * 2f) * 0.35f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                float noise = Mathf.PerlinNoise(u * 6f + 1.7f, v * 6f + 3.1f);

                float combined = (wave1 * 0.35f) + (wave2 * 0.40f) + (noise * 0.25f);
                Color pixelColor = Color.Lerp(darkMud, muddyFloodColor, combined);

                // Köpük çizgileri
                if (combined > 0.68f)
                {
                    float foamT = Mathf.InverseLerp(0.68f, 0.92f, combined);
                    pixelColor = Color.Lerp(pixelColor, waveFoamHighlightColor, foamT);
                }

                generatedTexture.SetPixel(x, y, pixelColor);
            }
        }

        generatedTexture.Apply();

        if (hasUrpBaseMap)
        {
            runtimeMaterial.SetTexture(URP_BASE_MAP_PROP, generatedTexture);
        }

        if (hasBuiltinMainTex)
        {
            runtimeMaterial.SetTexture(BUILTIN_MAIN_TEX_PROP, generatedTexture);
        }
    }

    private void ApplyTextureTiling()
    {
        if (runtimeMaterial == null)
            return;

        if (hasUrpBaseMap)
        {
            runtimeMaterial.SetTextureScale(URP_BASE_MAP_PROP, textureTiling);
        }

        if (hasBuiltinMainTex)
        {
            runtimeMaterial.SetTextureScale(BUILTIN_MAIN_TEX_PROP, textureTiling);
        }
    }

    private void Update()
    {
        if (runtimeMaterial == null)
            return;

        float dt = Time.deltaTime;
        float speedFactor = 1f;

        if (scaleWithFloodSpeed && floodChaser != null && referenceFloodSpeed > 0.1f)
        {
            speedFactor = Mathf.Clamp(floodChaser.CurrentSpeed / referenceFloodSpeed, 0.5f, 2.5f);
        }

        primaryOffset.x = Mathf.Repeat(primaryOffset.x + scrollSpeedX * speedFactor * dt, 1f);
        primaryOffset.y = Mathf.Repeat(primaryOffset.y + scrollSpeedY * speedFactor * dt, 1f);

        if (hasUrpBaseMap)
        {
            runtimeMaterial.SetTextureOffset(URP_BASE_MAP_PROP, primaryOffset);
        }

        if (hasBuiltinMainTex)
        {
            runtimeMaterial.SetTextureOffset(BUILTIN_MAIN_TEX_PROP, primaryOffset);
        }

        if (scrollSecondaryMaps)
        {
            secondaryOffset.x = Mathf.Repeat(secondaryOffset.x + secondaryScrollSpeedX * speedFactor * dt, 1f);
            secondaryOffset.y = Mathf.Repeat(secondaryOffset.y + secondaryScrollSpeedY * speedFactor * dt, 1f);

            if (hasBumpMap)
            {
                runtimeMaterial.SetTextureOffset(BUMP_MAP_PROP, secondaryOffset);
            }

            if (hasDetailMap)
            {
                runtimeMaterial.SetTextureOffset(DETAIL_ALBEDO_PROP, secondaryOffset);
            }
        }
    }

    public void ApplyTintColor(Color color)
    {
        if (runtimeMaterial == null)
            return;

        if (runtimeMaterial.HasProperty(URP_BASE_COLOR_PROP))
        {
            runtimeMaterial.SetColor(URP_BASE_COLOR_PROP, color);
        }

        if (runtimeMaterial.HasProperty(BUILTIN_COLOR_PROP))
        {
            runtimeMaterial.SetColor(BUILTIN_COLOR_PROP, color);
        }
    }

    private void OnDestroy()
    {
        if (generatedTexture != null)
        {
            Destroy(generatedTexture);
        }

        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }
}
