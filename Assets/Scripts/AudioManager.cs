using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Centralized audio management with volume controls.
/// Persists settings using PlayerPrefs.
/// Refactored for cleaner organization.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }
    
    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource voiceSource;
    
    [Header("Volume Defaults")]
    [Range(0f, 1f)] [SerializeField] private float defaultMusicVolume = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float defaultSFXVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float defaultVoiceVolume = 1f;
    
    [Header("UI Sliders")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider voiceSlider;
    
    [Header("Music Clips")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip gameMusic;
    [SerializeField] private AudioClip winMusic;
    [SerializeField] private AudioClip loseMusic;
    
    [Header("SFX Clips")]
    [SerializeField] private AudioClip buttonClickSFX;
    [SerializeField] private AudioClip convergenceSFX;   // Tiles converging together
    [SerializeField] private AudioClip tenPopSFX;        // "10" appears after solve
    [SerializeField] private AudioClip swapSFX;
    [SerializeField] private AudioClip countdownBeepSFX;
    [SerializeField] private AudioClip countdownGoSFX;
    [SerializeField] private AudioClip transitionSwipeSFX;
    [SerializeField] private AudioClip tileSelectSFX;
    [SerializeField] private AudioClip timeWarningSFX;
    [SerializeField] private AudioClip finishSFX;
    [SerializeField] private AudioClip multiplierIncreaseSFX; // Multiplier goes up
    
    // Volume state
    private float musicVolume;
    private float sfxVolume;
    private float voiceVolume;
    
    // PlayerPrefs keys
    private const string MUSIC_VOL_KEY = "MusicVolume";
    private const string SFX_VOL_KEY = "SFXVolume";
    private const string VOICE_VOL_KEY = "VoiceVolume";
    
    #region Initialization
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        LoadVolumeSettings();
    }
    
    private void Start()
    {
        SetupSliders();
        ApplyVolumeSettings();
        ValidateAudioListener();
    }
    
    private void LoadVolumeSettings()
    {
        musicVolume = PlayerPrefs.GetFloat(MUSIC_VOL_KEY, defaultMusicVolume);
        sfxVolume = PlayerPrefs.GetFloat(SFX_VOL_KEY, defaultSFXVolume);
        voiceVolume = PlayerPrefs.GetFloat(VOICE_VOL_KEY, defaultVoiceVolume);
        
        // Reset if accidentally muted
        if (musicVolume <= 0.01f)
        {
            Debug.LogWarning($"Music volume was {musicVolume}, resetting to default");
            musicVolume = defaultMusicVolume;
            SaveVolumeSettings();
        }
        
        Debug.Log($"AudioManager loaded: Music={musicVolume:F2}, SFX={sfxVolume:F2}, Voice={voiceVolume:F2}");
    }
    
    private void SaveVolumeSettings()
    {
        PlayerPrefs.SetFloat(MUSIC_VOL_KEY, musicVolume);
        PlayerPrefs.SetFloat(SFX_VOL_KEY, sfxVolume);
        PlayerPrefs.SetFloat(VOICE_VOL_KEY, voiceVolume);
        PlayerPrefs.Save();
    }
    
    private void ApplyVolumeSettings()
    {
        if (musicSource != null) musicSource.volume = musicVolume;
        if (sfxSource != null) sfxSource.volume = sfxVolume;
        if (voiceSource != null) voiceSource.volume = voiceVolume;
    }
    
    private void SetupSliders()
    {
        SetupSlider(musicSlider, musicVolume, SetMusicVolume);
        SetupSlider(sfxSlider, sfxVolume, SetSFXVolume);
        SetupSlider(voiceSlider, voiceVolume, SetVoiceVolume);
    }
    
    private void SetupSlider(Slider slider, float value, UnityEngine.Events.UnityAction<float> callback)
    {
        if (slider == null) return;
        slider.value = value;
        slider.onValueChanged.AddListener(callback);
    }
    
    private void ValidateAudioListener()
    {
        var listener = FindFirstObjectByType<AudioListener>();
        if (listener == null)
            Debug.LogError("AudioManager: No AudioListener found! Audio won't play.");
        else
            Debug.Log($"AudioManager: AudioListener on {listener.gameObject.name}");
    }
    
    public void RefreshSliders()
    {
        if (musicSlider != null) musicSlider.value = musicVolume;
        if (sfxSlider != null) sfxSlider.value = sfxVolume;
        if (voiceSlider != null) voiceSlider.value = voiceVolume;
    }
    
    #endregion
    
    #region Volume Setters
    
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
        PlayButtonClick(); // Test sound
    }
    
    public void SetVoiceVolume(float volume)
    {
        voiceVolume = volume;
        if (voiceSource != null) voiceSource.volume = volume;
        SaveVolumeSettings();
    }
    
    #endregion
    
    #region Music Playback
    
    private void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (musicSource == null || clip == null)
        {
            Debug.LogWarning($"PlayMusic failed: source={musicSource != null}, clip={clip != null}");
            return;
        }
        
        musicSource.clip = clip;
        musicSource.volume = musicVolume;
        musicSource.loop = loop;
        musicSource.Play();
        Debug.Log($"Playing music: {clip.name} at volume {musicVolume} (loop={loop})");
    }
    
    public void PlayMenuMusic() => PlayMusic(menuMusic, loop: true);
    public void PlayGameMusic() => PlayMusic(gameMusic, loop: true);
    public void PlayWinMusic() => PlayMusic(winMusic, loop: false);
    public void PlayLoseMusic() => PlayMusic(loseMusic, loop: false);
    
    public void StopMusic()
    {
        if (musicSource != null)
            musicSource.Stop();
    }
    
    #endregion
    
    #region SFX Playback
    
    private void PlaySFX(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
            sfxSource.PlayOneShot(clip, sfxVolume);
    }
    
    // One-liner SFX methods
    public void PlayButtonClick() => PlaySFX(buttonClickSFX);
    public void PlayConvergenceSound() => PlaySFX(convergenceSFX);
    public void PlayTenPopSound() => PlaySFX(tenPopSFX);
    public void PlayMatchSound() => PlaySFX(tenPopSFX); // Legacy alias for compatibility
    public void PlaySwapSound() => PlaySFX(swapSFX);
    public void PlayCountdownBeep() => PlaySFX(countdownBeepSFX);
    public void PlayCountdownGo() => PlaySFX(countdownGoSFX);
    public void PlayTransitionSwipe() => PlaySFX(transitionSwipeSFX);
    public void PlayTileSelect() => PlaySFX(tileSelectSFX);
    public void PlayFinishSound() => PlaySFX(finishSFX);
    public void PlayMultiplierIncrease() => PlaySFX(multiplierIncreaseSFX);
    
    #endregion
    
    #region Looping SFX (Time Warning)
    
    public void StartTimeWarning()
    {
        if (sfxSource == null || timeWarningSFX == null || sfxSource.isPlaying) return;
        
        sfxSource.clip = timeWarningSFX;
        sfxSource.loop = true;
        sfxSource.Play();
    }
    
    public void StopTimeWarning()
    {
        if (sfxSource == null || sfxSource.clip != timeWarningSFX) return;
        
        sfxSource.Stop();
        sfxSource.loop = false;
        sfxSource.clip = null;
    }
    
    #endregion
    
    #region Voice Playback
    
    public void PlayVoice(AudioClip clip)
    {
        if (voiceSource != null && clip != null)
            voiceSource.PlayOneShot(clip, voiceVolume);
    }
    
    #endregion
}
