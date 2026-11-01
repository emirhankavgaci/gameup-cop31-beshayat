using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Music")]
    [SerializeField] private AudioClip backgroundMusic;

    [Header("Sound Effects")]
    [SerializeField] private AudioClip flyEat;
    [SerializeField] private AudioClip friendFeed;
    [SerializeField] private AudioClip mudStep;
    [SerializeField] private AudioClip taskComplete;
    [SerializeField] private AudioClip seedPlant;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (musicSource == null || backgroundMusic == null)
            return;

        musicSource.clip = backgroundMusic;
        musicSource.loop = true;
        musicSource.volume = GameAudioManager.MusicVolume;
        musicSource.Play();
    }

    private void OnEnable()
    {
        GameAudioManager.OnMusicVolumeChanged += HandleMusicVolumeChanged;
    }

    private void OnDisable()
    {
        GameAudioManager.OnMusicVolumeChanged -= HandleMusicVolumeChanged;
    }

    private void HandleMusicVolumeChanged(float vol)
    {
        if (musicSource != null)
        {
            musicSource.volume = vol;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            PlayTaskComplete();
        }
    }

    public void PlayFlyEat()
    {
        PlaySFX(flyEat);
    }

    public void PlayFriendFeed()
    {
        PlaySFX(friendFeed);
    }

    public void PlayMudStep()
    {
        PlaySFX(mudStep);
    }

    public void PlayTaskComplete()
    {
        PlaySFX(taskComplete);
    }

    public void PlaySeedPlant()
    {
        PlaySFX(seedPlant);
    }

    private void PlaySFX(AudioClip clip)
    {
        if (clip == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(clip, GameAudioManager.SFXVolume);
    }
}
