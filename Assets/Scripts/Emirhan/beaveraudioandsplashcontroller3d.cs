using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Unity 6 (6000.x) uyumlu, Kunduz (Player) için:
/// 1. Her ses için ayrı [SerializeField] ses seviyesi (Volume) ayarı sunar.
/// 2. Su sesinin üst üste "taramalı tüfek" gibi çalmasını Cooldown (varsayılan 2 sn) ile engeller.
/// 3. Zıplama sesini yalnızca yerdeyken Space tuşuna basıldığında çalar (oyun başında veya yokuşta kendi kendine çalmaz).
/// 4. Doğa/Nehir arka plan ambiyansını (Loop) ve beyaz su köpüğü (Foam/Splash) efektini yönetir.
/// Oyuncu (Player) objesine eklenir.
/// </summary>
public class BeaverAudioAndSplashController3D : MonoBehaviour
{
    [Header("Genel Ses Kontrolü")]
    [SerializeField] [Range(0f, 1f)] private float masterVolume = 1.0f;

    [Header("1. Eşya Alma & Baraja Koyma Sesleri (Ayrı Ses Ayarlı)")]
    [SerializeField] private AudioClip pickupSound; // pickup.mp3
    [SerializeField] [Range(0f, 1f)] private float pickupVolume = 0.85f;
    [SerializeField] [Range(0f, 1f)] private float dropVolume = 0.60f; // Yere normal bırakma ses yüksekliği

    [SerializeField] private AudioClip putDownSound; // putdownsound.mp3 (Baraja parça yerleşince)
    [SerializeField] [Range(0f, 1f)] private float putDownVolume = 0.85f;

    [Header("2. Kazanma & Kaybetme Sesleri (Ayrı Ses Ayarlı)")]
    [SerializeField] private AudioClip winSound; // winsound.mp3
    [SerializeField] [Range(0f, 1f)] private float winVolume = 0.90f;

    [SerializeField] private AudioClip applauseSound; // applause.mp3
    [SerializeField] [Range(0f, 1f)] private float applauseVolume = 0.70f;

    [SerializeField] private AudioClip failSound; // failsound.mp3
    [SerializeField] [Range(0f, 1f)] private float failVolume = 0.90f;

    [Header("3. Su Sıçrama (Cooldown'lu) & Zıplama Sesleri")]
    [SerializeField] private AudioClip waterSplashSound; // Suya girince / suda yürürken şıpırtı
    [SerializeField] [Range(0f, 1f)] private float waterSplashVolume = 0.65f;
    [SerializeField] private float splashSoundCooldown = 2.0f; // Taramalı tüfek gibi çalmayı önleyen bekleme süresi (sn)
    [SerializeField] private bool playSplashWhileMovingInWater = true; // Suda yüzerken/yürürken her 2 saniyede bir çalsın mı?

    [SerializeField] private AudioClip jumpSound; // Space ile zıplama sesi
    [SerializeField] [Range(0f, 1f)] private float jumpVolume = 0.75f;
    [SerializeField] private float jumpSoundCooldown = 0.35f;

    [Header("4. Arka Plan Doğa / Nehir Ambiyansı (ncprime nature-sound-2 önerilir)")]
    [SerializeField] private AudioClip natureAmbienceClip; // ncprime-prime-facts7-nature-sound-2-291487.mp3
    [SerializeField] [Range(0f, 1f)] private float natureAmbienceVolume = 0.35f;

    [Header("5. Ayarlanabilir Su Sıçrama ve Beyaz Köpük Efekti")]
    [SerializeField] private bool enableWaterFoamAndSplash = true;
    [SerializeField] [Range(0.2f, 5f)] private float effectOverallScale = 1.0f; // Tüm köpük/sıçrama efektini büyütüp küçültür
    [SerializeField] private float foamRingRadius = 0.45f; // Kunduzun etrafındaki beyaz köpük çemberinin yarıçapı
    [SerializeField] private float foamParticleSize = 0.22f; // Beyaz köpük taneciklerinin boyutu
    [SerializeField] private float splashParticleSize = 0.18f; // Suya ilk girişte sıçrayan damlaların boyutu
    [SerializeField] private int splashBurstCount = 22; // Suya girince fırlayan damla sayısı
    [SerializeField] private float foamEmissionRateIdle = 10f; // Suda dururken saniyede çıkan köpük
    [SerializeField] private float foamEmissionRateMoving = 28f; // Suda yürürken saniyede çıkan köpük
    [SerializeField] private float foamHeightOffset = 0.08f; // Köpüğün su yüzeyinden ne kadar yukarıda görüneceği
    [SerializeField] private Color foamColor = new Color(0.95f, 0.98f, 1f, 0.85f); // Beyaz köpük rengi

    private PlayerMovement3D playerMovement;
    private WaterLevelController waterController;
    private DamBuilderController3D damBuilder;

    private AudioSource sfxSource;
    private AudioSource secondarySfxSource;
    private AudioSource ambienceSource;

    private ParticleSystem foamRingParticles;
    private ParticleSystem splashBurstParticles;
    private Material generatedParticleMaterial;

    private bool isInWater = false;
    private float currentWaterSurfaceY = 0f;
    private float lastSplashSoundTime = -10f;
    private float lastJumpSoundTime = -10f;
    private float lastWaterContactTime = -10f;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement3D>();
        waterController = FindAnyObjectByType<WaterLevelController>();
        damBuilder = FindAnyObjectByType<DamBuilderController3D>();

        // 1. Ana SFX kaynağı
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;

        // 2. İkincil SFX kaynağı (Alkış ve Su Sesi çakışmasın diye)
        secondarySfxSource = gameObject.AddComponent<AudioSource>();
        secondarySfxSource.playOnAwake = false;
        secondarySfxSource.spatialBlend = 0f;

        // 3. Doğa / Nehir Ambiyansı kaynağı (Loop)
        ambienceSource = gameObject.AddComponent<AudioSource>();
        ambienceSource.playOnAwake = false;
        ambienceSource.loop = true;
        ambienceSource.spatialBlend = 0f;

        if (natureAmbienceClip != null)
        {
            ambienceSource.clip = natureAmbienceClip;
            ambienceSource.volume = natureAmbienceVolume * masterVolume;
            ambienceSource.Play();
        }

        if (enableWaterFoamAndSplash)
        {
            CreateProceduralFoamAndSplashSystems();
        }
    }

    private void OnEnable()
    {
        TrashPickup3D.OnTrashPickedUp += HandleTrashPickedUp;
        TrashPickup3D.OnTrashDropped += HandleTrashDropped;

        if (damBuilder != null)
        {
            damBuilder.OnDamProgressUpdated += HandleDamPiecePlaced;
        }

        if (waterController != null)
        {
            waterController.OnRiverSaved += HandleGameWon;
            waterController.OnRiverDriedOut += HandleGameLost;
        }
    }

    private void OnDisable()
    {
        TrashPickup3D.OnTrashPickedUp -= HandleTrashPickedUp;
        TrashPickup3D.OnTrashDropped -= HandleTrashDropped;

        if (damBuilder != null)
        {
            damBuilder.OnDamProgressUpdated -= HandleDamPiecePlaced;
        }

        if (waterController != null)
        {
            waterController.OnRiverSaved -= HandleGameWon;
            waterController.OnRiverDriedOut -= HandleGameLost;
        }
    }

    private void Update()
    {
        // Inspector'dan ambiyans sesi değiştirilirse canlı güncelle
        if (ambienceSource != null && ambienceSource.isPlaying)
        {
            ambienceSource.volume = natureAmbienceVolume * masterVolume;
        }

        // Su alçalırken fizik motorunun anlık OnTriggerExit/Enter yapmasını yumuşat (Debounce)
        if (isInWater && Time.time - lastWaterContactTime > 0.2f)
        {
            isInWater = false;
        }

        // Zıplama sesi kontrolü: Yalnızca karakter yerdeyken Space (veya Gamepad zıplama) tuşuna basıldığında çalar
        if (playerMovement != null && jumpSound != null)
        {
            bool jumpPressed = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                               || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);

            if (jumpPressed && playerMovement.IsGrounded && Time.time - lastJumpSoundTime >= jumpSoundCooldown)
            {
                lastJumpSoundTime = Time.time;
                PlayClip(jumpSound, jumpVolume * masterVolume, Random.Range(0.96f, 1.04f));
            }
        }

        // Suda hareket ederken Cooldown süresinde bir (örn. 2 saniyede bir) su sesi ve hafif sıçrama
        if (isInWater && playSplashWhileMovingInWater && playerMovement != null && playerMovement.IsMoving)
        {
            TryPlaySplashWithCooldown();
        }

        // Su içindeyken beyaz köpük halkasını su yüzeyinde ve kunduzun etrafında tut
        if (enableWaterFoamAndSplash && foamRingParticles != null)
        {
            UpdateFoamEffectInRealtime();
        }
    }

    private void TryPlaySplashWithCooldown()
    {
        if (Time.time - lastSplashSoundTime < splashSoundCooldown)
            return;

        lastSplashSoundTime = Time.time;

        if (waterSplashSound != null)
        {
            PlayClip(waterSplashSound, waterSplashVolume * masterVolume, Random.Range(0.92f, 1.08f));
        }

        if (enableWaterFoamAndSplash && splashBurstParticles != null)
        {
            Vector3 splashPos = new Vector3(transform.position.x, currentWaterSurfaceY + foamHeightOffset, transform.position.z);
            splashBurstParticles.transform.position = splashPos;

            var splashMain = splashBurstParticles.main;
            splashMain.startSize = splashParticleSize * effectOverallScale;
            splashMain.startColor = foamColor;

            var splashShape = splashBurstParticles.shape;
            splashShape.radius = foamRingRadius * effectOverallScale;

            splashBurstParticles.Emit(splashBurstCount);
        }
    }

    private void UpdateFoamEffectInRealtime()
    {
        if (waterController != null)
        {
            currentWaterSurfaceY = waterController.transform.position.y;
        }

        // Efektin pozisyonunu kunduzun X/Z hizasında ve suyun Y yüzeyinde tut
        Vector3 foamPos = new Vector3(transform.position.x, currentWaterSurfaceY + foamHeightOffset, transform.position.z);
        foamRingParticles.transform.position = foamPos;
        if (splashBurstParticles != null)
        {
            splashBurstParticles.transform.position = foamPos;
        }

        var foamMain = foamRingParticles.main;
        foamMain.startSize = foamParticleSize * effectOverallScale;
        foamMain.startColor = foamColor;

        var foamShape = foamRingParticles.shape;
        foamShape.radius = foamRingRadius * effectOverallScale;

        var emission = foamRingParticles.emission;
        if (isInWater)
        {
            bool moving = playerMovement != null && playerMovement.IsMoving;
            emission.rateOverTime = moving ? foamEmissionRateMoving : foamEmissionRateIdle;
        }
        else
        {
            emission.rateOverTime = 0f;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<WaterLevelController>(out WaterLevelController water))
        {
            RegisterWaterContact(water.transform.position.y, true);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.TryGetComponent<WaterLevelController>(out WaterLevelController water))
        {
            RegisterWaterContact(water.transform.position.y, false);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.TryGetComponent<WaterLevelController>(out WaterLevelController water))
        {
            RegisterWaterContact(water.transform.position.y, true);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.collider.TryGetComponent<WaterLevelController>(out WaterLevelController water))
        {
            RegisterWaterContact(water.transform.position.y, false);
        }
    }

    private void RegisterWaterContact(float waterY, bool isInitialEnter)
    {
        bool wasInWater = isInWater;
        isInWater = true;
        lastWaterContactTime = Time.time;
        currentWaterSurfaceY = waterY;

        if (!wasInWater && isInitialEnter)
        {
            TryPlaySplashWithCooldown();
        }
    }

    private void HandleTrashPickedUp(TrashPickup3D item)
    {
        if (pickupSound != null)
        {
            PlayClip(pickupSound, pickupVolume * masterVolume, Random.Range(0.95f, 1.05f));
        }
    }

    private void HandleTrashDropped(TrashPickup3D item)
    {
        if (pickupSound != null)
        {
            PlayClip(pickupSound, dropVolume * masterVolume, 0.75f);
        }
    }

    private void HandleDamPiecePlaced(int placed, int total, int blocks)
    {
        if (putDownSound != null)
        {
            float pitch = 0.95f + (placed / (float)Mathf.Max(1, total)) * 0.25f;
            PlayClip(putDownSound, putDownVolume * masterVolume, pitch);
        }
    }

    private void HandleGameWon()
    {
        if (winSound != null)
        {
            sfxSource.pitch = 1f;
            sfxSource.PlayOneShot(winSound, winVolume * masterVolume);
        }

        if (applauseSound != null)
        {
            secondarySfxSource.clip = applauseSound;
            secondarySfxSource.volume = applauseVolume * masterVolume;
            secondarySfxSource.pitch = 1f;
            secondarySfxSource.Play();
            // applause.mp3 sonundaki (00:06) konuşma sesini kesmek için 5.8 saniye sonra durdur
            Invoke(nameof(StopSecondaryAudio), 5.8f);
        }
    }

    private void StopSecondaryAudio()
    {
        if (secondarySfxSource != null && secondarySfxSource.isPlaying)
        {
            secondarySfxSource.Stop();
        }
    }

    private void HandleGameLost()
    {
        // Nehir kuruduğunda su/doğa ambiyansını sustur ki kuraklık dramatik hissedilsin
        if (ambienceSource != null && ambienceSource.isPlaying)
        {
            ambienceSource.Stop();
        }

        if (failSound != null)
        {
            sfxSource.pitch = 1f;
            sfxSource.PlayOneShot(failSound, failVolume * masterVolume);
        }
    }

    private void PlayClip(AudioClip clip, float vol, float pitch)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.pitch = pitch;
        sfxSource.PlayOneShot(clip, vol);
    }

    /// <summary>
    /// Dışarıdan hiçbir asset veya materyal gerektirmeden yumuşak beyaz köpük dokusu ve ParticleSystem üretir.
    /// </summary>
    private void CreateProceduralFoamAndSplashSystems()
    {
        Texture2D circleTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(15.5f, 15.5f);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / 15.5f;
                float alpha = Mathf.Clamp01(1f - dist * dist);
                circleTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        circleTex.Apply();

        Shader particleShader = Shader.Find("Particles/Standard Unlit");
        if (particleShader == null) particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleShader == null) particleShader = Shader.Find("Sprites/Default");

        generatedParticleMaterial = new Material(particleShader);
        generatedParticleMaterial.mainTexture = circleTex;
        if (generatedParticleMaterial.HasProperty("_BaseMap"))
            generatedParticleMaterial.SetTexture("_BaseMap", circleTex);
        generatedParticleMaterial.renderQueue = 2015;

        // 1. Sürekli Beyaz Köpük Halkası (Foam Ring)
        GameObject foamObj = new GameObject("WaterFoamRing_Effect");
        foamObj.transform.SetParent(transform);
        foamObj.transform.localPosition = Vector3.zero;

        foamRingParticles = foamObj.AddComponent<ParticleSystem>();
        var foamMain = foamRingParticles.main;
        foamMain.loop = true;
        foamMain.simulationSpace = ParticleSystemSimulationSpace.World;
        foamMain.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
        foamMain.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        foamMain.startSize = foamParticleSize * effectOverallScale;
        foamMain.startColor = foamColor;
        foamMain.gravityModifier = 0f;

        var foamEmission = foamRingParticles.emission;
        foamEmission.rateOverTime = 0f;

        var foamShape = foamRingParticles.shape;
        foamShape.shapeType = ParticleSystemShapeType.Circle;
        foamShape.radius = foamRingRadius * effectOverallScale;
        foamShape.rotation = new Vector3(90f, 0f, 0f);

        var foamSizeOverLifetime = foamRingParticles.sizeOverLifetime;
        foamSizeOverLifetime.enabled = true;
        AnimationCurve shrinkCurve = new AnimationCurve(new Keyframe(0f, 0.8f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));
        foamSizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, shrinkCurve);

        var foamRenderer = foamObj.GetComponent<ParticleSystemRenderer>();
        foamRenderer.sharedMaterial = generatedParticleMaterial;

        // 2. Suya İlk Giriş Sıçrama Efekti (Splash Burst)
        GameObject splashObj = new GameObject("WaterSplashBurst_Effect");
        splashObj.transform.SetParent(transform);
        splashObj.transform.localPosition = Vector3.zero;

        splashBurstParticles = splashObj.AddComponent<ParticleSystem>();
        var splashMain = splashBurstParticles.main;
        splashMain.loop = false;
        splashMain.playOnAwake = false;
        splashMain.simulationSpace = ParticleSystemSimulationSpace.World;
        splashMain.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        splashMain.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 3.2f);
        splashMain.startSize = splashParticleSize * effectOverallScale;
        splashMain.startColor = foamColor;
        splashMain.gravityModifier = 1.2f;

        var splashEmission = splashBurstParticles.emission;
        splashEmission.rateOverTime = 0f;

        var splashShape = splashBurstParticles.shape;
        splashShape.shapeType = ParticleSystemShapeType.Hemisphere;
        splashShape.radius = foamRingRadius * effectOverallScale;
        splashShape.rotation = new Vector3(-90f, 0f, 0f);

        var splashRenderer = splashObj.GetComponent<ParticleSystemRenderer>();
        splashRenderer.sharedMaterial = generatedParticleMaterial;
    }
}