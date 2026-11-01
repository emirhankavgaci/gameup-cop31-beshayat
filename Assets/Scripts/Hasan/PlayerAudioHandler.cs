using System.Collections;
using UnityEngine;

/// <summary>
/// CANOPY: The Last Run - Oyuncu ve Genel Ses Sistemi (PlayerAudioHandler).
/// Bu bileşen oyundaki tüm ses efektlerini ve müzik döngüsünü koordine eder:
/// 1. musicloop.mp3: Arka planda sürekli çalan müzik döngüsü.
/// 2. Runmonkey.mp3: Karakter yerde koşarken çalan ayak/koşma sesi.
/// 3. Jumpmonkey.mp3: Karakter zıpladığında (veya duvardan zıpladığında) çalan ses.
/// 4. rastgelemonkeysound.mp3: Her 10 - 15 saniyede bir rastgele çalan maymun sesi.
/// 5. whenmonkeydie.mp3: Maymun kaktüse çarptığında veya sele kapılıp öldüğünde çalan ses.
/// 6. drowingmonkey.mp3: Final sekansında su altında boğulurken çalan ses.
/// </summary>
public class PlayerAudioHandler : MonoBehaviour
{
    public static PlayerAudioHandler Instance { get; private set; }

    [Header("Bileşen Referansları (Boşsa Otomatik Bulunur)")]
    [Tooltip("Oyuncu hareket ve durum yöneticisi.")]
    [SerializeField] private PlayerMotor playerMotor;

    [Tooltip("Duvar koşusu ve duvardan zıplama yöneticisi.")]
    [SerializeField] private WallRunHandler wallRunHandler;

    [Tooltip("Oyun akışı ve ölüm yöneticisi.")]
    [SerializeField] private GameFlowManager gameFlowManager;

    [Header("AudioSource Kaynakları (Boşsa Çalışma Zamanında Otomatik Üretilir)")]
    [Tooltip("Müzik çalan 2D AudioSource.")]
    [SerializeField] private AudioSource musicSource;

    [Tooltip("Koşma/ayak sesi çalan 2D AudioSource.")]
    [SerializeField] private AudioSource runSource;

    [Tooltip("Tek seferlik ses efektlerini (Zıplama, Ölüm, Boğulma) çalan AudioSource.")]
    [SerializeField] private AudioSource sfxSource;

    [Tooltip("Rastgele maymun çığlığını çalan AudioSource.")]
    [SerializeField] private AudioSource voiceSource;

    [Header("1. Arka Plan Müziği (musicloop.mp3)")]
    [Tooltip("Arka plan müzik klibi.")]
    [SerializeField] private AudioClip musicLoopClip;

    [Range(0f, 1f)]
    [Tooltip("Müzik ses seviyesi (varsayılan 0.45).")]
    [SerializeField] private float musicVolume = 0.45f;

    [Tooltip("Oyun başlar başlamaz müziği otomatik başlat.")]
    [SerializeField] private bool autoPlayMusic = true;

    [Header("2. Koşma Sesi (Runmonkey.mp3)")]
    [Tooltip("Karakter yerde koşarken döngüsel çalan ses.")]
    [SerializeField] private AudioClip runClip;

    [Range(0f, 1f)]
    [Tooltip("Koşma maksimum ses seviyesi (varsayılan 0.7).")]
    [SerializeField] private float runVolume = 0.7f;

    [Tooltip("Sesin açılma ve kısılma yumuşatma hızı.")]
    [SerializeField] private float runFadeSpeed = 10f;

    [Tooltip("Koşma sesinin tetikleneceği minimum yatay hız eşiği (m/s).")]
    [SerializeField] private float minRunSpeedThreshold = 1.2f;

    [Header("3. Zıplama Sesi (Jumpmonkey.mp3)")]
    [Tooltip("Zıplama ses efekti.")]
    [SerializeField] private AudioClip jumpClip;

    [Range(0f, 1f)]
    [Tooltip("Zıplama ses seviyesi (varsayılan 0.85).")]
    [SerializeField] private float jumpVolume = 0.85f;

    [Header("4. Rastgele Maymun Sesi (rastgelemonkeysound.mp3)")]
    [Tooltip("10 - 15 saniyede bir rastgele çalacak maymun ses efekti.")]
    [SerializeField] private AudioClip randomMonkeyClip;

    [Range(0f, 1f)]
    [Tooltip("Rastgele maymun ses seviyesi (varsayılan 0.8).")]
    [SerializeField] private float randomMonkeyVolume = 0.8f;

    [Tooltip("Rastgele ses için minimum bekleme süresi (sn).")]
    [SerializeField] private float minRandomInterval = 10f;

    [Tooltip("Rastgele ses için maksimum bekleme süresi (sn).")]
    [SerializeField] private float maxRandomInterval = 15f;

    [Header("5. Boğulma ve Bitiş Ölüm Sesi (whenmonkeydie.mp3)")]
    [Tooltip("Yalnızca FinishTrigger tetiklendiğinde ve maymun boğulurken çalan ölüm sesi (her ölümde/respawn'da çalmaz).")]
    [SerializeField] private AudioClip deathClip;

    [Range(0f, 1f)]
    [Tooltip("Ölüm sesi seviyesi (varsayılan 0.95).")]
    [SerializeField] private float deathVolume = 0.95f;

    [Header("6. Boğulma Sesi (drowingmonkey.mp3)")]
    [Tooltip("Final boğulma anında çalan maymun sesi.")]
    [SerializeField] private AudioClip drowningClip;

    [Range(0f, 1f)]
    [Tooltip("Boğulma sesi seviyesi (varsayılan 0.9).")]
    [SerializeField] private float drowningVolume = 0.9f;

    // Dahili zamanlayıcılar ve durumlar
    private float randomMonkeyTimer;
    private float currentRunVolume;
    private float lastJumpSoundTime = -1f;
    private bool isDrowningActive;
    private bool hasPlayedEndingDeathSound;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

#if UNITY_EDITOR
        LoadEditorClipsIfNull();
#endif

        FindRequiredComponents();
        EnsureAudioSources();
        ResetRandomMonkeyTimer();
    }

    private void Start()
    {
        SubscribeEvents();

        if (autoPlayMusic && musicSource != null && musicLoopClip != null)
        {
            musicSource.clip = musicLoopClip;
            musicSource.loop = true;
            musicSource.volume = musicVolume;
            musicSource.Play();
        }
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        UpdateRunFootsteps(dt);
        UpdateRandomMonkeySound(dt);
    }

    private void FindRequiredComponents()
    {
        if (playerMotor == null)
            playerMotor = GetComponent<PlayerMotor>() ?? FindObjectOfType<PlayerMotor>();

        if (wallRunHandler == null)
            wallRunHandler = GetComponent<WallRunHandler>() ?? FindObjectOfType<WallRunHandler>();

        if (gameFlowManager == null)
            gameFlowManager = FindObjectOfType<GameFlowManager>();
    }

    private void EnsureAudioSources()
    {
        // 2D ses kaynaklarını oluştur veya hazırla
        if (musicSource == null)
        {
            GameObject musicObj = new GameObject("Music_AudioSource");
            musicObj.transform.SetParent(transform, false);
            musicSource = musicObj.AddComponent<AudioSource>();
        }
        ConfigureSource(musicSource, true, 0f);

        if (runSource == null)
        {
            GameObject runObj = new GameObject("Run_AudioSource");
            runObj.transform.SetParent(transform, false);
            runSource = runObj.AddComponent<AudioSource>();
        }
        ConfigureSource(runSource, true, 0f);
        if (runClip != null)
        {
            runSource.clip = runClip;
        }

        if (sfxSource == null)
        {
            GameObject sfxObj = new GameObject("SFX_AudioSource");
            sfxObj.transform.SetParent(transform, false);
            sfxSource = sfxObj.AddComponent<AudioSource>();
        }
        ConfigureSource(sfxSource, false, 0f);

        if (voiceSource == null)
        {
            GameObject voiceObj = new GameObject("Voice_AudioSource");
            voiceObj.transform.SetParent(transform, false);
            voiceSource = voiceObj.AddComponent<AudioSource>();
        }
        ConfigureSource(voiceSource, false, 0f);
    }

    private void ConfigureSource(AudioSource src, bool loop, float spatialBlend)
    {
        if (src == null) return;
        src.playOnAwake = false;
        src.loop = loop;
        src.spatialBlend = spatialBlend; // 2D net ses
        src.bypassReverbZones = false;
    }

    private void SubscribeEvents()
    {
        if (playerMotor != null)
        {
            playerMotor.OnJumpCallback -= HandlePlayerJump;
            playerMotor.OnJumpCallback += HandlePlayerJump;
        }

        if (wallRunHandler != null)
        {
            wallRunHandler.OnWallJumpCallback -= HandleWallJump;
            wallRunHandler.OnWallJumpCallback += HandleWallJump;
        }
    }

    private void UnsubscribeEvents()
    {
        if (playerMotor != null)
        {
            playerMotor.OnJumpCallback -= HandlePlayerJump;
        }

        if (wallRunHandler != null)
        {
            wallRunHandler.OnWallJumpCallback -= HandleWallJump;
        }
    }

    /// <summary>
    /// Koşma/ayak sesinin oyuncunun hareket hızına ve zemin durumuna göre dinamik yönetimi.
    /// </summary>
    private void UpdateRunFootsteps(float dt)
    {
        if (runSource == null || runClip == null)
            return;

        if (isDrowningActive)
        {
            runSource.volume = 0f;
            return;
        }

        bool isGroundedAndMoving = false;
        float speedRatio = 0f;

        if (playerMotor != null)
        {
            bool isGrounded = playerMotor.IsGrounded && playerMotor.CurrentState == PlayerMotor.PlayerState.Grounded;
            float horizontalSpeed = playerMotor.HorizontalSpeed;

            if (isGrounded && horizontalSpeed >= minRunSpeedThreshold)
            {
                isGroundedAndMoving = true;
                speedRatio = Mathf.Clamp01(horizontalSpeed / Mathf.Max(1f, playerMotor.SprintSpeed));
            }
        }

        float targetVolume = isGroundedAndMoving ? (runVolume * Mathf.Lerp(0.6f, 1f, speedRatio)) : 0f;
        currentRunVolume = Mathf.MoveTowards(currentRunVolume, targetVolume, dt * runFadeSpeed);
        runSource.volume = currentRunVolume;

        if (currentRunVolume > 0.01f)
        {
            if (!runSource.isPlaying)
            {
                runSource.Play();
            }
            // Hıza göre adım temposunu hafifçe hızlandır
            runSource.pitch = Mathf.Lerp(0.92f, 1.12f, speedRatio);
        }
        else if (runSource.isPlaying)
        {
            runSource.Pause();
        }
    }

    /// <summary>
    /// Rastgele maymun sesini 10 - 15 saniyede bir tetikler.
    /// </summary>
    private void UpdateRandomMonkeySound(float dt)
    {
        if (isDrowningActive || GameFlowManager.EndingActive)
            return;

        randomMonkeyTimer -= dt;
        if (randomMonkeyTimer <= 0f)
        {
            PlayRandomMonkeySound();
            ResetRandomMonkeyTimer();
        }
    }

    private void ResetRandomMonkeyTimer()
    {
        float minVal = Mathf.Min(minRandomInterval, maxRandomInterval);
        float maxVal = Mathf.Max(minRandomInterval, maxRandomInterval);
        randomMonkeyTimer = Random.Range(minVal, maxVal);
    }

    private void HandlePlayerJump()
    {
        PlayJumpSound();
    }

    private void HandleWallJump(Vector3 direction)
    {
        PlayJumpSound();
    }

    /// <summary>
    /// Zıplama sesini çalar (çift tetiklenmeyi önleme korumasıyla).
    /// </summary>
    public void PlayJumpSound()
    {
        if (isDrowningActive) return;
        if (Time.time - lastJumpSoundTime < 0.09f) return;
        lastJumpSoundTime = Time.time;

        if (sfxSource != null && jumpClip != null)
        {
            sfxSource.pitch = Random.Range(0.95f, 1.05f);
            sfxSource.PlayOneShot(jumpClip, jumpVolume);
        }
    }

    /// <summary>
    /// Rastgele maymun ses efektini çalar (10 - 15 sn kuralı).
    /// </summary>
    public void PlayRandomMonkeySound()
    {
        if (isDrowningActive) return;
        if (randomMonkeyClip == null) return;

        if (voiceSource != null)
        {
            voiceSource.pitch = Random.Range(0.93f, 1.07f);
            voiceSource.PlayOneShot(randomMonkeyClip, randomMonkeyVolume);
        }
        else if (sfxSource != null)
        {
            sfxSource.pitch = Random.Range(0.93f, 1.07f);
            sfxSource.PlayOneShot(randomMonkeyClip, randomMonkeyVolume);
        }
    }

    /// <summary>
    /// Maymunun boğulma anındaki ölüm sesini (whenmonkeydie.mp3) çalar.
    /// YALNIZCA FinishTrigger tetiklendiğinde veya final boğulma sekansı başladığında çalışır.
    /// Normal checkpoint respawn veya kaktüs çarpmalarında kesinlikle ÇALMAZ.
    /// </summary>
    public void PlayWhenMonkeyDieSound()
    {
        if (hasPlayedEndingDeathSound) return;
        hasPlayedEndingDeathSound = true;

        if (voiceSource != null && deathClip != null)
        {
            voiceSource.pitch = 1f;
            voiceSource.PlayOneShot(deathClip, deathVolume);
        }
        else if (sfxSource != null && deathClip != null)
        {
            sfxSource.pitch = 1f;
            sfxSource.PlayOneShot(deathClip, deathVolume);
        }
    }

    /// <summary>
    /// Eski metod çağrıları için: Normal ölümlerde artık ses çalınmaz.
    /// </summary>
    public void PlayDeathSound()
    {
        // Normal ölümlerde ses çalınması kapatıldı. Sadece FinishTrigger ile PlayWhenMonkeyDieSound() tetiklenir.
    }

    /// <summary>
    /// Oyun sonu sel altında boğulma sesini çalar (drowingmonkey.mp3).
    /// </summary>
    public void PlayDrowningSound()
    {
        isDrowningActive = true;

        if (runSource != null)
        {
            runSource.Stop();
        }

        // Müzik sesini boğulma anında kısıp atmosferi derinleştir
        if (musicSource != null)
        {
            StartCoroutine(FadeOutMusicCoroutine(1.5f));
        }

        if (voiceSource != null && drowningClip != null)
        {
            voiceSource.pitch = 1f;
            voiceSource.PlayOneShot(drowningClip, drowningVolume);
        }
        else if (sfxSource != null && drowningClip != null)
        {
            sfxSource.pitch = 1f;
            sfxSource.PlayOneShot(drowningClip, drowningVolume);
        }
    }

    private IEnumerator FadeOutMusicCoroutine(float duration)
    {
        if (musicSource == null) yield break;
        float startVol = musicSource.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVol, startVol * 0.2f, elapsed / duration);
            yield return null;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        LoadEditorClipsIfNull();
    }

    private void LoadEditorClipsIfNull()
    {
        const string basePath = "Assets/Audio/Hasan/gamejamses/";

        if (musicLoopClip == null)
            musicLoopClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(basePath + "musicloop.mp3");

        if (runClip == null)
            runClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(basePath + "Runmonkey.mp3");

        if (jumpClip == null)
            jumpClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(basePath + "Jumpmonkey.mp3");

        if (randomMonkeyClip == null)
            randomMonkeyClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(basePath + "rastgelemonkeysound.mp3");

        if (deathClip == null)
            deathClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(basePath + "whenmonkeydie.mp3");

        if (drowningClip == null)
            drowningClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(basePath + "drowingmonkey.mp3");
    }
#endif
}
