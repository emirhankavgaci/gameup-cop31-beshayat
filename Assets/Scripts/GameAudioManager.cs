using System;
using UnityEngine;

/// <summary>
/// Oyun genelinde Müzik (Music) ve Ses Efekti (SFX) seviyelerini yöneten,
/// PlayerPrefs ile kalıcı saklayan ve tüm sahnelere bağımsız olarak entegre eden merkezi ses yöneticisi.
/// </summary>
public class GameAudioManager : MonoBehaviour
{
    private const string PREFS_MUSIC_VOLUME = "Game_Music_Volume";
    private const string PREFS_SFX_VOLUME = "Game_SFX_Volume";

    public static GameAudioManager Instance { get; private set; }

    public static event Action<float> OnMusicVolumeChanged;
    public static event Action<float> OnSFXVolumeChanged;

    private static float musicVolume = 0.8f;
    private static float sfxVolume = 0.8f;
    private static bool isInitialized = false;

    public static float MusicVolume
    {
        get
        {
            EnsureInitialized();
            return musicVolume;
        }
        set
        {
            SetMusicVolume(value);
        }
    }

    public static float SFXVolume
    {
        get
        {
            EnsureInitialized();
            return sfxVolume;
        }
        set
        {
            SetSFXVolume(value);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        EnsureInitialized();
    }

    public static void EnsureInitialized()
    {
        if (isInitialized) return;
        isInitialized = true;

        musicVolume = PlayerPrefs.GetFloat(PREFS_MUSIC_VOLUME, 0.8f);
        sfxVolume = PlayerPrefs.GetFloat(PREFS_SFX_VOLUME, 0.8f);

        if (Application.isPlaying && Instance == null)
        {
            GameObject go = new GameObject("[GameAudioManager]");
            Instance = go.AddComponent<GameAudioManager>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureInitialized();
    }

    public static void SetMusicVolume(float volume)
    {
        EnsureInitialized();
        musicVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PREFS_MUSIC_VOLUME, musicVolume);
        PlayerPrefs.Save();
        OnMusicVolumeChanged?.Invoke(musicVolume);
    }

    public static void SetSFXVolume(float volume)
    {
        EnsureInitialized();
        sfxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PREFS_SFX_VOLUME, sfxVolume);
        PlayerPrefs.Save();
        OnSFXVolumeChanged?.Invoke(sfxVolume);
    }
}

