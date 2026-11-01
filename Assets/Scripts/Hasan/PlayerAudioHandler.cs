using System.Collections;
using UnityEngine;

/// <summary>
/// CANOPY: The Last Run - Oyuncu ve Genel Ses Sistemi (PlayerAudioHandler).
/// Bu bileÅŸen oyundaki tÃ¼m ses efektlerini ve mÃ¼zik dÃ¶ngÃ¼sÃ¼nÃ¼ koordine eder:
/// 1. musicloop.mp3: Arka planda sÃ¼rekli Ã§alan mÃ¼zik dÃ¶ngÃ¼sÃ¼.
/// 2. Runmonkey.mp3: Karakter yerde koÅŸarken Ã§alan ayak/koÅŸma sesi.
/// 3. Jumpmonkey.mp3: Karakter zÄ±pladÄ±ÄŸÄ±nda (veya duvardan zÄ±pladÄ±ÄŸÄ±nda) Ã§alan ses.
/// 4. rastgelemonkeysound.mp3: Her 10 - 15 saniyede bir rastgele Ã§alan maymun sesi.
/// 5. whenmonkeydie.mp3: Maymun kaktÃ¼se Ã§arptÄ±ÄŸÄ±nda veya sele kapÄ±lÄ±p Ã¶ldÃ¼ÄŸÃ¼nde Ã§alan ses.
/// 6. drowingmonkey.mp3: Final sekansÄ±nda su altÄ±nda boÄŸulurken Ã§alan ses.
/// </summary>
public class PlayerAudioHandler : MonoBehaviour
{
    public static PlayerAudioHandler Instance { get; private set; }

    [Header("BileÅŸen ReferanslarÄ± (BoÅŸsa Otomatik Bulunur)")]
    [Tooltip("Oyuncu hareket ve durum yÃ¶neticisi.")]
    [SerializeField] private PlayerMotor playerMotor;

    [Tooltip("Duvar koÅŸusu ve duvardan zÄ±plama yÃ¶neticisi.")]
    [SerializeField] private WallRunHandler wallRunHandler;

    [Tooltip("Oyun akÄ±ÅŸÄ± ve Ã¶lÃ¼m yÃ¶neticisi.")]
    [SerializeField] private GameFlowManager gameFlowManager;

    [Header("AudioSource KaynaklarÄ± (BoÅŸsa Ã‡alÄ±ÅŸma ZamanÄ±nda Otomatik Ãœretilir)")]
    [Tooltip("MÃ¼zik Ã§alan 2D AudioSource.")]
    [SerializeField] private AudioSource musicSource;

    [Tooltip("KoÅŸma/ayak sesi Ã§alan 2D AudioSource.")]
    [SerializeField] private AudioSource runSource;

    [Tooltip("Tek seferlik ses efektlerini (ZÄ±plama, Ã–lÃ¼m, BoÄŸulma) Ã§alan AudioSource.")]
    [SerializeField] private AudioSource sfxSource;

    [Tooltip("Rastgele maymun Ã§Ä±ÄŸlÄ±ÄŸÄ±nÄ± Ã§alan AudioSource.")]
    [SerializeField] private AudioSource voiceSource;

    [Header("1. Arka Plan MÃ¼ziÄŸi (musicloop.mp3)")]
    [Tooltip("Arka plan mÃ¼zik klibi.")]
    [SerializeField] private AudioClip musicLoopClip;

    [Range(0f, 1f)]
    [Tooltip("MÃ¼zik ses seviyesi (varsayÄ±lan 0.45).")]
    [SerializeField] private float musicVolume = 0.45f;

    [Tooltip("Oyun baÅŸlar baÅŸlamaz mÃ¼ziÄŸi otomatik baÅŸlat.")]
    [SerializeField] private bool autoPlayMusic = true;

    [Header("2. KoÅŸma Sesi (Runmonkey.mp3)")]
    [Tooltip("Karakter yerde koÅŸarken dÃ¶ngÃ¼sel Ã§alan ses.")]
    [SerializeField] private AudioClip runClip;

    [Range(0f, 1f)]
    [Tooltip("KoÅŸma maksimum ses seviyesi (varsayÄ±lan 0.7).")]
    [SerializeField] private float runVolume = 0.7f;

    [Tooltip("Sesin aÃ§Ä±lma ve kÄ±sÄ±lma yumuÅŸatma hÄ±zÄ±.")]
    [SerializeField] private float runFadeSpeed = 10f;

    [Tooltip("KoÅŸma sesinin tetikleneceÄŸi minimum yatay hÄ±z eÅŸiÄŸi (m/s).")]
    [SerializeField] private float minRunSpeedThreshold = 1.2f;

    [Header("3. ZÄ±plama Sesi (Jumpmonkey.mp3)")]
    [Tooltip("ZÄ±plama ses efekti.")]
    [SerializeField] private AudioClip jumpClip;

    [Range(0f, 1f)]
    [Tooltip("ZÄ±plama ses seviyesi (varsayÄ±lan 0.85).")]
    [SerializeField] private float jumpVolume = 0.85f;

    [Header("4. Rastgele Maymun Sesi (rastgelemonkeysound.mp3)")]
    [Tooltip("10 - 15 saniyede bir rastgele Ã§alacak maymun ses efekti.")]
    [SerializeField] private AudioClip randomMonkeyClip;

    [Range(0f, 1f)]
    [Tooltip("Rastgele maymun ses seviyesi (varsayÄ±lan 0.8).")]
    [SerializeField] private float randomMonkeyVolume = 0.8f;

    [Tooltip("Rastgele ses iÃ§in minimum bekleme sÃ¼resi (sn).")]
    [SerializeField] private float minRandomInterval = 10f;

    [Tooltip("Rastgele ses iÃ§in maksimum bekleme sÃ¼resi (sn).")]
    [SerializeField] private float maxRandomInterval = 15f;

    [Header("5. BoÄŸulma ve BitiÅŸ Ã–lÃ¼m Sesi (whenmonkeydie.mp3)")]
    [Tooltip("YalnÄ±zca FinishTrigger tetiklendiÄŸinde ve maymun boÄŸulurken Ã§alan Ã¶lÃ¼m sesi (her Ã¶lÃ¼mde/respawn'da Ã§almaz).")]
    [SerializeField] private AudioClip deathClip;

    [Range(0f, 1f)]
    [Tooltip("Ã–lÃ¼m sesi seviyesi (varsayÄ±lan 0.95).")]
    [SerializeField] private float deathVolume = 0.95f;

    [Header("6. BoÄŸulma Sesi (drowingmonkey.mp3)")]
    [Tooltip("Final boÄŸulma anÄ±nda Ã§alan maymun sesi.")]
    [SerializeField] private AudioClip drowningClip;

    [Range(0f, 1f)]
    [Tooltip("BoÄŸulma sesi seviyesi (varsayÄ±lan 0.9).")]
    [SerializeField] private float drowningVolume = 0.9f;

    // Dahili zamanlayÄ±cÄ±lar ve durumlar
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
            musicSource.volume = musicVolume * GameAudioManager.MusicVolume; GameAudioManager.OnMusicVolumeChanged += (v) => { if (musicSource != null) musicSource.volume = musicVolume * v; };
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
        // 2D ses kaynaklarÄ±nÄ± oluÅŸtur veya hazÄ±rla
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
    /// KoÅŸma/ayak sesinin oyuncunun hareket hÄ±zÄ±na ve zemin durumuna gÃ¶re dinamik yÃ¶netimi.
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
        runSource.volume = currentRunVolume * GameAudioManager.SFXVolume;

        if (currentRunVolume > 0.01f)
        {
            if (!runSource.isPlaying)
            {
                runSource.Play();
            }
            // HÄ±za gÃ¶re adÄ±m temposunu hafifÃ§e hÄ±zlandÄ±r
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
    /// ZÄ±plama sesini Ã§alar (Ã§ift tetiklenmeyi Ã¶nleme korumasÄ±yla).
    /// </summary>
    public void PlayJumpSound()
    {
        if (isDrowningActive) return;
        if (Time.time - lastJumpSoundTime < 0.09f) return;
        lastJumpSoundTime = Time.time;

        if (sfxSource != null && jumpClip != null)
        {
            sfxSource.pitch = Random.Range(0.95f, 1.05f);
            sfxSource.PlayOneShot(jumpClip, jumpVolume * GameAudioManager.SFXVolume);
        }
    }

    /// <summary>
    /// Rastgele maymun ses efektini Ã§alar (10 - 15 sn kuralÄ±).
    /// </summary>
    public void PlayRandomMonkeySound()
    {
        if (isDrowningActive) return;
        if (randomMonkeyClip == null) return;

        if (voiceSource != null)
        {
            voiceSource.pitch = Random.Range(0.93f, 1.07f);
            voiceSource.PlayOneShot(randomMonkeyClip, randomMonkeyVolume * GameAudioManager.SFXVolume);
        }
        else if (sfxSource != null)
        {
            sfxSource.pitch = Random.Range(0.93f, 1.07f);
            sfxSource.PlayOneShot(randomMonkeyClip, randomMonkeyVolume * GameAudioManager.SFXVolume);
        }
    }

    /// <summary>
    /// Maymunun boÄŸulma anÄ±ndaki Ã¶lÃ¼m sesini (whenmonkeydie.mp3) Ã§alar.
    /// YALNIZCA FinishTrigger tetiklendiÄŸinde veya final boÄŸulma sekansÄ± baÅŸladÄ±ÄŸÄ±nda Ã§alÄ±ÅŸÄ±r.
    /// Normal checkpoint respawn veya kaktÃ¼s Ã§arpmalarÄ±nda kesinlikle Ã‡ALMAZ.
    /// </summary>
    public void PlayWhenMonkeyDieSound()
    {
        if (hasPlayedEndingDeathSound) return;
        hasPlayedEndingDeathSound = true;

        if (voiceSource != null && deathClip != null)
        {
            voiceSource.pitch = 1f;
            voiceSource.PlayOneShot(deathClip, deathVolume * GameAudioManager.SFXVolume);
        }
        else if (sfxSource != null && deathClip != null)
        {
            sfxSource.pitch = 1f;
            sfxSource.PlayOneShot(deathClip, deathVolume * GameAudioManager.SFXVolume);
        }
    }

    /// <summary>
    /// Eski metod Ã§aÄŸrÄ±larÄ± iÃ§in: Normal Ã¶lÃ¼mlerde artÄ±k ses Ã§alÄ±nmaz.
    /// </summary>
    public void PlayDeathSound()
    {
        // Normal Ã¶lÃ¼mlerde ses Ã§alÄ±nmasÄ± kapatÄ±ldÄ±. Sadece FinishTrigger ile PlayWhenMonkeyDieSound() tetiklenir.
    }

    /// <summary>
    /// Oyun sonu sel altÄ±nda boÄŸulma sesini Ã§alar (drowingmonkey.mp3).
    /// </summary>
    public void PlayDrowningSound()
    {
        isDrowningActive = true;

        if (runSource != null)
        {
            runSource.Stop();
        }

        // MÃ¼zik sesini boÄŸulma anÄ±nda kÄ±sÄ±p atmosferi derinleÅŸtir
        if (musicSource != null)
        {
            StartCoroutine(FadeOutMusicCoroutine(1.5f));
        }

        if (voiceSource != null && drowningClip != null)
        {
            voiceSource.pitch = 1f;
            voiceSource.PlayOneShot(drowningClip, drowningVolume * GameAudioManager.SFXVolume);
        }
        else if (sfxSource != null && drowningClip != null)
        {
            sfxSource.pitch = 1f;
            sfxSource.PlayOneShot(drowningClip, drowningVolume * GameAudioManager.SFXVolume);
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
