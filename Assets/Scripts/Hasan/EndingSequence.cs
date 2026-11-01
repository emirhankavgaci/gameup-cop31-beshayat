using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CANOPY: The Last Run - Oyun Sonu Boğulma ve Sinematik Final Sekansı (EndingSequence).
///
/// Akış:
/// 1. Oyuncu FinishTrigger'a girdiğinde (veya Inspector'da debugSkipToEnding işaretlendiğinde) başlar.
///    Oyuncu kontrolü KİLİTLENMEZ; oyuncu koşmaya devam eder.
/// 2. GameFlowManager.EndingActive = true olur; Rubber Banding ve Checkpoint/Respawn kapatılır.
/// 3. Sel anında oyuncunun arkasına ışınlanır (FloodChaser.TeleportBehindPlayer, varsayılan 10 m) ve
///    0.15 sn kısa beyaz/mavi ekran flaşı ile ani sıçrama gizlenir. Sel hızı 16 m/s (endFloodSpeed) olur.
/// 4. Sel oyuncuya yetişip yakaladığında respawn OLMAZ; oyuncu girdisi kilitlenir ve PlayerMotor
///    Drowning durumuna geçer:
///    - Su akıntı yönünde ve yavaşça yukarı-aşağı sürüklenir (azaltılmış yerçekimi, sönümlü hız)
///    - Kamera su altı tonuna geçer (%50 mavi-kahve overlay, artan sis yoğunluğu, AudioLowPassFilter ile boğuk ses)
///    - Kamera hafif sallanır, FOV yavaşça 90 -> 75'e düşer
///    - Kameranın önünde yukarı çıkan su altı kabarcık (bubble) ParticleSystem çalışır
///    - 3.5 sn (drownDuration) sonra ekran 1.5 sn içinde siyaha kararır (fade to black)
/// 5. Ekran tam karardığında EndingInfoUI açılır ve IUCN 2021 bilgileri satır satır gösterilir.
/// </summary>
public class EndingSequence : MonoBehaviour
{
    /// <summary>
    /// FloodDebugHUD üzerinde gösterilecek final sekansı aşamaları.
    /// </summary>
    public enum EndingState
    {
        Idle = 0,
        FloodTeleported = 1,
        Drowning = 2,
        Fading = 3,
        Info = 4
    }

    private const int OVERLAY_CANVAS_SORT_ORDER = 960;
    private const float DEFAULT_AUDIO_NORMAL_CUTOFF = 22000f;

    [Header("Sistem Referansları (Boşsa Otomatik Bulunur)")]
    [Tooltip("Oyuncu hareket ve Drowning durumu yöneticisi.")]
    [SerializeField] private PlayerMotor playerMotor;

    [Tooltip("Oyuncu kamera bakış ve FOV yöneticisi.")]
    [SerializeField] private PlayerLook playerLook;

    [Tooltip("Arkadan gelen sel yöneticisi.")]
    [SerializeField] private FloodChaser floodChaser;

    [Tooltip("Oyun akışı ve EndingActive bayrağı yöneticisi.")]
    [SerializeField] private GameFlowManager gameFlowManager;

    [Tooltip("Kamera sarsıntı yöneticisi.")]
    [SerializeField] private CameraShake cameraShake;

    [Tooltip("Oyun sonu sinematik bilgi ekranı yöneticisi.")]
    [SerializeField] private EndingInfoUI endingInfoUI;

    [Tooltip("Tutorial mesajlarını temizlemek için TutorialUI referansı (opsiyonel).")]
    [SerializeField] private TutorialUI tutorialUI;

    [Header("1. Aşama: Sel Işınlama ve Yakalama Ayarları")]
    [Tooltip("FinishTrigger'a girildiğinde selin oyuncunun kaç metre gerisine ışınlanacağı (varsayılan 10 m).")]
    [SerializeField] private float endFloodDistanceBehind = 10f;

    [Tooltip("Final sekansında selin oyuncuya yetişeceği hız (varsayılan 16 m/s).")]
    [SerializeField] private float endFloodSpeed = 16f;

    [Tooltip("Sel ışınlandığı anda ani sıçramayı gizleyen kısa beyaz/mavi ekran flaşı süresi (varsayılan 0.15 sn).")]
    [SerializeField] private float teleportFlashDuration = 0.15f;

    [Tooltip("Işınlanma anındaki beyaz/mavi ekran flaşının rengi ve maksimum opaklığı.")]
    [SerializeField] private Color teleportFlashColor = new Color(0.78f, 0.94f, 1.0f, 0.82f);

    [Header("2. Aşama: Boğulma (Drowning) ve Su Altı Atmosferi")]
    [Tooltip("Yakalanma anından ekran kararmaya başlayana kadar geçen boğulma süresi (varsayılan 3.5 sn).")]
    [SerializeField] private float drownDuration = 3.5f;

    [Tooltip("Boğulma sonunda ekranın siyaha kararma (fade to black) süresi (varsayılan 1.5 sn).")]
    [SerializeField] private float fadeToBlackDuration = 1.5f;

    [Tooltip("Su altı mavi-kahve ekran overlay rengi (%50 opaklık).")]
    [SerializeField] private Color underwaterOverlayColor = new Color(0.14f, 0.30f, 0.32f, 0.50f);

    [Tooltip("Boğulma sırasında sahne sisinin (RenderSettings.fogColor) geçeceği bulanık mavi-kahve renk.")]
    [SerializeField] private Color underwaterFogColor = new Color(0.12f, 0.24f, 0.26f, 1f);

    [Tooltip("Boğulma sırasında sahne sis yoğunluğunun (RenderSettings.fogDensity) çıkacağı hedef değer.")]
    [Range(0.01f, 0.25f)]
    [SerializeField] private float underwaterFogDensity = 0.085f;

    [Tooltip("Boğulma sırasında kamera FOV değerinin 90'dan düşeceği hedef açı (varsayılan 75).")]
    [SerializeField] private float drownTargetFov = 75f;

    [Tooltip("Boğulma sırasında kameraya uygulanacak sarsıntı şiddeti (0..1).")]
    [Range(0f, 1f)]
    [SerializeField] private float drownCameraShakeIntensity = 0.42f;

    [Header("Su Altı Ses Boğuklaştırma (AudioLowPassFilter) ve Kabarcık Efektleri")]
    [Tooltip("Su altında sesin boğuklaşması için AudioLowPassFilter hedef kesim frekansı (Hz).")]
    [SerializeField] private float underwaterLowPassCutoff = 650f;

    [Tooltip("Su altında genel ses seviyesinin düşeceği çarpan.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float underwaterVolumeMultiplier = 0.35f;

    [Tooltip("Kameranın önünde yukarı çıkan kabarcık ParticleSystem (boşsa runtime'da otomatik oluşturulur).")]
    [SerializeField] private ParticleSystem bubbleParticleSystem;

    [Header("3. Aşama: Bilgi Ekranı ve Sonraki Sahne")]
    [Tooltip("Tüm metin göründükten sonra otomatik sahne geçişi için beklenecek süre (varsayılan 8 sn).")]
    [SerializeField] private float infoHoldTime = 8.0f;

    [Tooltip("Bilgi ekranından sonra yüklenecek sahnenin adı veya build index numarası.")]
    [SerializeField] private string nextSceneName = "";

    [Header("Debug Test Kontrolü")]
    [Tooltip("Inspector'dan işaretlendiğinde oyun içinde anında final sekansını başlatır (E tuşu yerine bu bool kullanılır).")]
    [SerializeField] private bool debugSkipToEnding;

    // UI Overlay referansları
    private Image teleportFlashImage;
    private Image underwaterOverlayImage;
    private Image blackFadeImage;
    private AudioLowPassFilter runtimeLowPassFilter;
    private float initialAudioVolume = 1f;

    // Durum takibi
    private EndingState currentState = EndingState.Idle;
    private bool playerCaughtInEnding;
    private Coroutine sequenceCoroutine;

    /// <summary>
    /// FloodDebugHUD için mevcut oyun sonu sekans durumu (Idle / FloodTeleported / Drowning / Fading / Info).
    /// </summary>
    public EndingState CurrentState => currentState;

    /// <summary>
    /// Final sekansı şu anda çalışıyor mu?
    /// </summary>
    public bool IsSequenceRunning => currentState != EndingState.Idle;

    private void Awake()
    {
        ResolveReferences();
        EnsureOverlayCanvas();
    }

    private void OnEnable()
    {
        if (floodChaser != null)
        {
            floodChaser.OnPlayerCaught -= HandleFloodCaughtPlayerInEnding;
            floodChaser.OnPlayerCaught += HandleFloodCaughtPlayerInEnding;
        }
    }

    private void OnDisable()
    {
        if (floodChaser != null)
        {
            floodChaser.OnPlayerCaught -= HandleFloodCaughtPlayerInEnding;
        }

        if (AudioListener.volume < initialAudioVolume)
        {
            AudioListener.volume = initialAudioVolume;
        }
    }

    private void Update()
    {
        // Debug amaçlı Inspector'daki debugSkipToEnding bool işaretlendiğinde sekansı başlat
        if (debugSkipToEnding)
        {
            debugSkipToEnding = false;
            if (!IsSequenceRunning)
            {
                StartEndingSequence();
            }
        }
    }

    /// <summary>
    /// FinishTrigger tarafından (veya debugSkipToEnding ile) çağrılır ve final sekansını başlatır.
    /// Oyuncu kontrolü kilitlenmez; oyuncu koşmaya devam eder.
    /// </summary>
    public void StartEndingSequence()
    {
        if (IsSequenceRunning)
            return;

        ResolveReferences();
        EnsureOverlayCanvas();

        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
        }

        sequenceCoroutine = StartCoroutine(EndingSequenceCoroutine());
    }

    private void HandleFloodCaughtPlayerInEnding()
    {
        if (!GameFlowManager.EndingActive)
            return;

        playerCaughtInEnding = true;
    }

    private IEnumerator EndingSequenceCoroutine()
    {
        // 1. Sekans bayrağını aç: Rubber Banding ve Checkpoint/Respawn devre dışı kalır
        GameFlowManager.SetEndingActive(true);
        playerCaughtInEnding = false;
        currentState = EndingState.FloodTeleported;

        // Varsa ekrandaki tutorial mesajlarını temizle
        if (tutorialUI != null)
        {
            tutorialUI.ClearAllMessagesImmediate();
        }

        // 2. Seli oyuncunun hemen arkasına (endFloodDistanceBehind = 10 m) ışınla ve hızını endFloodSpeed (16 m/s) yap
        if (floodChaser != null)
        {
            floodChaser.SetRubberBandingEnabled(false);
            floodChaser.TeleportBehindPlayer(endFloodDistanceBehind);
            floodChaser.SetBaseSpeed(endFloodSpeed, 0f);
        }

        // 3. Işınlanma anında 0.15 sn kısa beyaz/mavi ekran flaşı ile ani sıçramayı gizle (oyuncu koşmaya devam eder)
        StartCoroutine(PlayTeleportFlashCoroutine());

        // 4. Selin oyuncuya yetişip yakalamasını bekle
        while (!playerCaughtInEnding)
        {
            if (floodChaser != null && floodChaser.DistanceToPlayer <= 0.1f)
            {
                floodChaser.TriggerPlayerCaught();
                playerCaughtInEnding = true;
            }
            yield return null;
        }

        // 5. Yakalanma anı -> Boğulma (Drowning) başlar! Respawn OLMAZ, oyuncu girdisi kilitlenir.
        currentState = EndingState.Drowning;
        EnterDrowningPhase();

        float startFogDensity = RenderSettings.fogDensity;
        Color startFogColor = RenderSettings.fogColor;
        RenderSettings.fog = true;

        float drownElapsed = 0f;
        float totalDrownTime = Mathf.Max(0.5f, drownDuration);

        while (drownElapsed < totalDrownTime)
        {
            float dt = Time.deltaTime;
            drownElapsed += dt;
            float fastAtmosphereT = Mathf.Clamp01(drownElapsed / Mathf.Min(0.65f, totalDrownTime * 0.4f));

            // Su altı mavi-kahve overlay (%50)
            float overlayAlpha = Mathf.SmoothStep(0f, underwaterOverlayColor.a, fastAtmosphereT);
            SetImageColorAndAlpha(underwaterOverlayImage, underwaterOverlayColor, overlayAlpha);

            // Sis yoğunluğu ve rengi su altı tonuna geçer
            RenderSettings.fogColor = Color.Lerp(startFogColor, underwaterFogColor, fastAtmosphereT);
            RenderSettings.fogDensity = Mathf.Lerp(startFogDensity, underwaterFogDensity, fastAtmosphereT);

            // Ses boğuklaşır (AudioLowPassFilter) ve kısılır
            if (runtimeLowPassFilter != null)
            {
                runtimeLowPassFilter.cutoffFrequency = Mathf.Lerp(DEFAULT_AUDIO_NORMAL_CUTOFF, underwaterLowPassCutoff, fastAtmosphereT);
            }
            AudioListener.volume = Mathf.Lerp(initialAudioVolume, initialAudioVolume * underwaterVolumeMultiplier, fastAtmosphereT);

            // Kamera hafif sallanır
            if (cameraShake != null)
            {
                cameraShake.SetContinuousShakeIntensity(drownCameraShakeIntensity);
            }

            // PlayerMotor'daki akıntı yönünü ve su yüzey yüksekliğini güncel tut
            if (playerMotor != null && floodChaser != null)
            {
                playerMotor.UpdateDrowningWaterContext(floodChaser.CurrentFlowDirection, floodChaser.CurrentWaterSurfaceY);
            }

            yield return null;
        }

        // 6. 3.5 sn (drownDuration) sonra ekran yavaşça siyaha kararır (fade to black, 1.5 sn)
        currentState = EndingState.Fading;
        float fadeElapsed = 0f;
        float totalFadeTime = Mathf.Max(0.2f, fadeToBlackDuration);

        while (fadeElapsed < totalFadeTime)
        {
            float dt = Time.deltaTime;
            fadeElapsed += dt;
            float fadeT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fadeElapsed / totalFadeTime));

            SetImageColorAndAlpha(blackFadeImage, Color.black, fadeT);
            AudioListener.volume = Mathf.Lerp(initialAudioVolume * underwaterVolumeMultiplier, 0.05f, fadeT);

            if (cameraShake != null)
            {
                cameraShake.SetContinuousShakeIntensity(Mathf.Lerp(drownCameraShakeIntensity, 0f, fadeT));
            }

            yield return null;
        }

        SetImageColorAndAlpha(blackFadeImage, Color.black, 1f);

        // Kabarcıkları ve sarsıntıyı durdur, seli durdur
        if (bubbleParticleSystem != null && bubbleParticleSystem.isPlaying)
        {
            bubbleParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (cameraShake != null)
        {
            cameraShake.ClearOffsets();
        }

        if (floodChaser != null)
        {
            floodChaser.StopFlood();
        }

        // 7. Ekran tam karardığında EndingInfoUI açılır
        currentState = EndingState.Info;
        EnsureEndingInfoUI();
        if (endingInfoUI != null)
        {
            endingInfoUI.InfoHoldTime = infoHoldTime;
            endingInfoUI.ShowEndingInfo(nextSceneName);
        }
    }

    private IEnumerator PlayTeleportFlashCoroutine()
    {
        if (teleportFlashImage == null)
            yield break;

        float total = Mathf.Max(0.04f, teleportFlashDuration);
        float inTime = total * 0.35f;
        float outTime = total - inTime;

        float elapsed = 0f;
        while (elapsed < inTime)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(0f, teleportFlashColor.a, Mathf.Clamp01(elapsed / inTime));
            SetImageColorAndAlpha(teleportFlashImage, teleportFlashColor, a);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < outTime)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(teleportFlashColor.a, 0f, Mathf.Clamp01(elapsed / outTime));
            SetImageColorAndAlpha(teleportFlashImage, teleportFlashColor, a);
            yield return null;
        }

        SetImageColorAndAlpha(teleportFlashImage, teleportFlashColor, 0f);
    }

    private void EnterDrowningPhase()
    {
        Vector3 flowDir = Vector3.forward;
        float waterSurfaceY = playerMotor != null ? playerMotor.transform.position.y + 1.5f : 2f;

        if (floodChaser != null)
        {
            flowDir = floodChaser.CurrentFlowDirection;
            waterSurfaceY = floodChaser.CurrentWaterSurfaceY;
            floodChaser.SetBaseSpeed(Mathf.Max(3.5f, endFloodSpeed * 0.25f), 0.4f);
        }

        if (playerMotor != null)
        {
            playerMotor.EnterDrowningState(flowDir, waterSurfaceY);
        }

        if (playerLook != null)
        {
            float fovTransitionSpeed = Mathf.Abs(90f - drownTargetFov) / Mathf.Max(1f, drownDuration * 0.85f);
            playerLook.SetDrowningCameraState(true, drownTargetFov, fovTransitionSpeed);
        }

        SetupUnderwaterAudioFilter();
        EnsureAndPlayBubbleParticles();
    }

    private void SetupUnderwaterAudioFilter()
    {
        initialAudioVolume = AudioListener.volume;

        Camera cam = playerLook != null && playerLook.TargetCamera != null
            ? playerLook.TargetCamera
            : Camera.main;

        if (cam == null)
            return;

        AudioListener listener = cam.GetComponent<AudioListener>();
        GameObject filterHost = listener != null ? listener.gameObject : cam.gameObject;

        runtimeLowPassFilter = filterHost.GetComponent<AudioLowPassFilter>();
        if (runtimeLowPassFilter == null)
        {
            runtimeLowPassFilter = filterHost.AddComponent<AudioLowPassFilter>();
        }

        runtimeLowPassFilter.enabled = true;
        runtimeLowPassFilter.cutoffFrequency = DEFAULT_AUDIO_NORMAL_CUTOFF;
        runtimeLowPassFilter.lowpassResonanceQ = 1.15f;
    }

    private void EnsureAndPlayBubbleParticles()
    {
        if (bubbleParticleSystem == null)
        {
            Camera cam = playerLook != null && playerLook.TargetCamera != null
                ? playerLook.TargetCamera
                : Camera.main;

            Transform parentTransform = cam != null ? cam.transform : transform;
            bubbleParticleSystem = CreateRuntimeBubbleParticleSystem(parentTransform);
        }

        if (bubbleParticleSystem != null)
        {
            bubbleParticleSystem.gameObject.SetActive(true);
            bubbleParticleSystem.Clear();
            bubbleParticleSystem.Play();
        }
    }

    private ParticleSystem CreateRuntimeBubbleParticleSystem(Transform cameraTransform)
    {
        GameObject psObj = new GameObject("UnderwaterBubblesPS_Runtime");
        psObj.transform.SetParent(cameraTransform, false);
        psObj.transform.localPosition = new Vector3(0f, -0.25f, 0.65f);
        psObj.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        ParticleSystem ps = psObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 4.0f;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.055f);
        main.startColor = new Color(0.82f, 0.96f, 1.0f, 0.65f);
        main.gravityModifier = -0.25f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 28f;
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0.0f, 18)
        });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.75f, 0.35f, 0.35f);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.7f),
            new Keyframe(0.7f, 1.15f),
            new Keyframe(1f, 0f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.18f;
        noise.frequency = 1.5f;
        noise.scrollSpeed = 0.8f;

        ParticleSystemRenderer psRenderer = psObj.GetComponent<ParticleSystemRenderer>();
        if (psRenderer != null)
        {
            Shader particleShader = Shader.Find("Particles/Standard Unlit");
            if (particleShader == null)
            {
                particleShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            }
            if (particleShader == null)
            {
                particleShader = Shader.Find("Sprites/Default");
            }

            if (particleShader != null)
            {
                Material bubbleMat = new Material(particleShader);
                bubbleMat.color = new Color(0.85f, 0.96f, 1f, 0.65f);
                psRenderer.material = bubbleMat;
            }
        }

        return ps;
    }

    private void ResolveReferences()
    {
        if (playerMotor == null)
            playerMotor = FindObjectOfType<PlayerMotor>();

        if (playerLook == null)
        {
            if (playerMotor != null)
                playerLook = playerMotor.GetComponentInChildren<PlayerLook>();
            if (playerLook == null)
                playerLook = FindObjectOfType<PlayerLook>();
        }

        if (floodChaser == null)
            floodChaser = FindObjectOfType<FloodChaser>();

        if (gameFlowManager == null)
            gameFlowManager = FindObjectOfType<GameFlowManager>();

        if (cameraShake == null)
            cameraShake = FindObjectOfType<CameraShake>();

        if (endingInfoUI == null)
            endingInfoUI = FindObjectOfType<EndingInfoUI>();

        if (tutorialUI == null)
            tutorialUI = FindObjectOfType<TutorialUI>();
    }

    private void EnsureEndingInfoUI()
    {
        if (endingInfoUI == null)
        {
            endingInfoUI = FindObjectOfType<EndingInfoUI>();
            if (endingInfoUI == null)
            {
                GameObject infoObj = new GameObject("EndingInfoUI_Runtime");
                endingInfoUI = infoObj.AddComponent<EndingInfoUI>();
            }
        }
    }

    private void EnsureOverlayCanvas()
    {
        if (teleportFlashImage != null && underwaterOverlayImage != null && blackFadeImage != null)
            return;

        GameObject canvasObj = new GameObject("EndingSequenceOverlayCanvas");
        canvasObj.transform.SetParent(transform, false);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = OVERLAY_CANVAS_SORT_ORDER;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        underwaterOverlayImage = CreateOverlayImage(canvasObj.transform, "UnderwaterDrownOverlay", underwaterOverlayColor);
        teleportFlashImage = CreateOverlayImage(canvasObj.transform, "TeleportFlashOverlay", teleportFlashColor);
        blackFadeImage = CreateOverlayImage(canvasObj.transform, "EndingBlackFadeOverlay", Color.black);
    }

    private Image CreateOverlayImage(Transform parent, string name, Color baseColor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
        img.raycastTarget = false;
        return img;
    }

    private void SetImageColorAndAlpha(Image img, Color color, float alpha)
    {
        if (img == null)
            return;

        img.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
    }
}
