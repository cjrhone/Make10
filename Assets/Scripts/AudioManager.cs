using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

/// <summary>
/// Centralized audio management with volume controls.
/// Persists settings using PlayerPrefs.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }
    
    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource voiceSource;
    
    [Header("Volume Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float defaultMusicVolume = 0.7f;
    [Range(0f, 1f)]
    [SerializeField] private float defaultSFXVolume = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float defaultVoiceVolume = 1f;
    
    [Header("UI References")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider voiceSlider;
    
    [Header("Audio Clips")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip gameMusic;
    [SerializeField] private AudioClip buttonClickSFX;
    [SerializeField] private AudioClip matchSFX;
    [SerializeField] private AudioClip swapSFX;
    [SerializeField] private AudioClip countdownBeepSFX;
    [SerializeField] private AudioClip countdownGoSFX;
    
    // Current volumes
    private float musicVolume;
    private float sfxVolume;
    private float voiceVolume;
    
    // PlayerPrefs keys
    private const string MUSIC_VOL_KEY = "MusicVolume";
    private const string SFX_VOL_KEY = "SFXVolume";
    private const string VOICE_VOL_KEY = "VoiceVolume";
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // Note: DontDestroyOnLoad removed - using single scene architecture
        
        LoadVolumeSettings();
    }
    
    private void Start()
    {
        SetupSliders();
        ApplyVolumeSettings();
    }
    
    /// <summary>
    /// Load saved volume settings or use defaults.
    /// </summary>
    private void LoadVolumeSettings()
    {
        musicVolume = PlayerPrefs.GetFloat(MUSIC_VOL_KEY, defaultMusicVolume);
        sfxVolume = PlayerPrefs.GetFloat(SFX_VOL_KEY, defaultSFXVolume);
        voiceVolume = PlayerPrefs.GetFloat(VOICE_VOL_KEY, defaultVoiceVolume);
    }
    
    /// <summary>
    /// Save volume settings to PlayerPrefs.
    /// </summary>
    private void SaveVolumeSettings()
    {
        PlayerPrefs.SetFloat(MUSIC_VOL_KEY, musicVolume);
        PlayerPrefs.SetFloat(SFX_VOL_KEY, sfxVolume);
        PlayerPrefs.SetFloat(VOICE_VOL_KEY, voiceVolume);
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// Apply current volume settings to audio sources.
    /// </summary>
    private void ApplyVolumeSettings()
    {
        if (musicSource != null) musicSource.volume = musicVolume;
        if (sfxSource != null) sfxSource.volume = sfxVolume;
        if (voiceSource != null) voiceSource.volume = voiceVolume;
    }
    
    /// <summary>
    /// Setup slider listeners and initial values.
    /// </summary>
    private void SetupSliders()
    {
        if (musicSlider != null)
        {
            musicSlider.value = musicVolume;
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
        }
        
        if (sfxSlider != null)
        {
            sfxSlider.value = sfxVolume;
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        }
        
        if (voiceSlider != null)
        {
            voiceSlider.value = voiceVolume;
            voiceSlider.onValueChanged.AddListener(SetVoiceVolume);
        }
    }
    
    /// <summary>
    /// Refresh sliders if options panel is reopened.
    /// </summary>
    public void RefreshSliders()
    {
        if (musicSlider != null) musicSlider.value = musicVolume;
        if (sfxSlider != null) sfxSlider.value = sfxVolume;
        if (voiceSlider != null) voiceSlider.value = voiceVolume;
    }
    
    // === VOLUME SETTERS ===
    
    public void SetMusicVolume(float volume)
    {
        musicVolume = volume;
        if (musicSource != null) musicSource.volume = volume;
        SaveVolumeSettings();
    }
    
    public void SetSFXVolume(float volume)
    {
        sfxVolume = volume;
        if (sfxSource != null) sfxSource.volume = volume;
        SaveVolumeSettings();
        
        // Play test sound
        PlaySFX(buttonClickSFX);
    }
    
    public void SetVoiceVolume(float volume)
    {
        voiceVolume = volume;
        if (voiceSource != null) voiceSource.volume = volume;
        SaveVolumeSettings();
    }
    
    // === PLAYBACK METHODS ===
    
    /// <summary>
    /// Play background music (loops).
    /// </summary>
    public void PlayMusic(AudioClip clip)
    {
        if (musicSource != null && clip != null)
        {
            musicSource.clip = clip;
            musicSource.loop = true;
            musicSource.Play();
        }
    }
    
    /// <summary>
    /// Play menu music.
    /// </summary>
    public void PlayMenuMusic()
    {
        PlayMusic(menuMusic);
    }
    
    /// <summary>
    /// Play game music.
    /// </summary>
    public void PlayGameMusic()
    {
        PlayMusic(gameMusic);
    }
    
    /// <summary>
    /// Stop music.
    /// </summary>
    public void StopMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }
    
    /// <summary>
    /// Play a one-shot sound effect.
    /// </summary>
    public void PlaySFX(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
        {
            sfxSource.PlayOneShot(clip, sfxVolume);
        }
    }
    
    /// <summary>
    /// Play button click sound.
    /// </summary>
    public void PlayButtonClick()
    {
        PlaySFX(buttonClickSFX);
    }
    
    /// <summary>
    /// Play match/solve sound.
    /// </summary>
    public void PlayMatchSound()
    {
        PlaySFX(matchSFX);
    }
    
    /// <summary>
    /// Play swap sound.
    /// </summary>
    public void PlaySwapSound()
    {
        PlaySFX(swapSFX);
    }
    
    /// <summary>
    /// Play countdown beep.
    /// </summary>
    public void PlayCountdownBeep()
    {
        PlaySFX(countdownBeepSFX);
    }
    
    /// <summary>
    /// Play countdown "GO!" sound.
    /// </summary>
    public void PlayCountdownGo()
    {
        PlaySFX(countdownGoSFX);
    }
    
    /// <summary>
    /// Play voice over clip.
    /// </summary>
    public void PlayVoice(AudioClip clip)
    {
        if (voiceSource != null && clip != null)
        {
            voiceSource.PlayOneShot(clip, voiceVolume);
        }
    }
}
