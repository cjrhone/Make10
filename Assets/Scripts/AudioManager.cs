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
    [SerializeField] private AudioSource sfxSource; // Kept for Inspector wiring — seeds sfxPool[0]
    [SerializeField] private AudioSource voiceSource;
    private AudioSource timeWarningSource; // Dedicated source so looping warning doesn't block SFX
    private AudioSource pitchedSfxSource;  // Dedicated source for pitch-shifted SFX (ten pop cascades)

    // SFX pool: round-robin for general SFX (always pitch 1.0)
    private const int SFX_POOL_SIZE = 4;
    private AudioSource[] sfxPool;
    private int sfxPoolIndex = 0;

    // Lightweight throttle for score tick SFX only (particle impacts arrive every 0.03s)
    private float lastScoreTickTime;
    private const float SCORE_TICK_MIN_INTERVAL = 0.06f; // Play every other tick — still sounds continuous

    
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
    [SerializeField] private AudioClip zenMusic;  // Separate track for MakeZen (falls back to gameMusic if null)
    [SerializeField] private AudioClip winMusic;
    [SerializeField] private AudioClip loseMusic;
    [SerializeField] private AudioClip hotStreakMusic;
    
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
    [SerializeField] private AudioClip scoreTickSmallSFX;     // Small particle hits progress bar
    [SerializeField] private AudioClip scoreTickBigSFX;       // Big particle hits progress bar

    [Header("Combo SFX")]
    [SerializeField] private AudioClip comboMergeSFX;           // 2-4 line combo merge sound
    [SerializeField] private AudioClip ultraComboSFX;           // 5-line ultra combo (very rare)

    // Volume state (public read for UI, private write via setters)
    private float musicVolume;
    private float sfxVolume;
    private float voiceVolume;

    public float MusicVolume => musicVolume;
    public float SFXVolume => sfxVolume;
    
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

        // Create dedicated AudioSource for time warning so it never blocks SFX
        timeWarningSource = gameObject.AddComponent<AudioSource>();
        timeWarningSource.playOnAwake = false;

        // Create dedicated AudioSource for pitch-shifted match sounds (isolated from pool)
        pitchedSfxSource = gameObject.AddComponent<AudioSource>();
        pitchedSfxSource.playOnAwake = false;

        // Build SFX pool: slot 0 is the Inspector-wired sfxSource, rest are cloned
        sfxPool = new AudioSource[SFX_POOL_SIZE];
        sfxPool[0] = sfxSource;
        for (int i = 1; i < SFX_POOL_SIZE; i++)
        {
            sfxPool[i] = gameObject.AddComponent<AudioSource>();
            sfxPool[i].playOnAwake = false;
        }

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
        ApplySFXPoolVolume(sfxVolume);
        if (pitchedSfxSource != null) pitchedSfxSource.volume = sfxVolume;
        if (timeWarningSource != null) timeWarningSource.volume = sfxVolume;
        if (voiceSource != null) voiceSource.volume = voiceVolume;
    }

    private void ApplySFXPoolVolume(float volume)
    {
        if (sfxPool == null) return;
        for (int i = 0; i < sfxPool.Length; i++)
        {
            if (sfxPool[i] != null) sfxPool[i].volume = volume;
        }
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
        ApplySFXPoolVolume(volume);
        if (pitchedSfxSource != null) pitchedSfxSource.volume = volume;
        if (timeWarningSource != null) timeWarningSource.volume = volume;
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
    
    public void PlayMenuMusic() => PlayMusic(menuMusic, loop: true);
    public void PlayGameMusic() => PlayMusic(gameMusic, loop: true);
    public void PlayZenMusic() => PlayMusic(zenMusic != null ? zenMusic : gameMusic, loop: true);
    public void PlayWinMusic() => PlayMusic(winMusic, loop: false);
    public void PlayLoseMusic() => PlayMusic(loseMusic, loop: false);
    public void PlayHotStreakMusic() => PlayMusic(hotStreakMusic, loop: true);

    /// <summary>
    /// Play a custom music clip.
    /// </summary>
    public void PlayMusic(AudioClip clip, bool loop = true)
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
    
    public void StopMusic()
    {
        if (musicSource != null)
            musicSource.Stop();
    }
    
    #endregion
    
    #region SFX Playback

    /// <summary>
    /// Get the next AudioSource from the round-robin pool.
    /// Each concurrent sound gets its own source, preventing pitch contamination.
    /// </summary>
    private AudioSource GetNextSFXSource()
    {
        sfxPoolIndex = (sfxPoolIndex + 1) % SFX_POOL_SIZE;
        return sfxPool[sfxPoolIndex];
    }

    private void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        AudioSource source = GetNextSFXSource();
        if (source == null) return;

        source.pitch = 1f;
        source.PlayOneShot(clip, sfxVolume);
    }

    // One-liner SFX methods
    public void PlayButtonClick() => PlaySFX(buttonClickSFX);
    public void PlayConvergenceSound() => PlaySFX(convergenceSFX);
    public void PlayTenPopSound() => PlayTenPopSound(1);
    public void PlayMatchSound() => PlayTenPopSound(1); // Legacy alias for compatibility

    /// <summary>
    /// Play the "10" pop sound with pitch shifting for cascade chains.
    /// chainCount 1 = normal pitch, 2+ = incrementally higher (capped at 1.5).
    /// Uses a dedicated AudioSource so pitch never bleeds into the general pool.
    /// </summary>
    public void PlayTenPopSound(int chainCount)
    {
        if (pitchedSfxSource == null || tenPopSFX == null) return;

        float pitch = Mathf.Min(1.0f + (chainCount - 1) * 0.08f, 1.5f);
        pitchedSfxSource.pitch = pitch;
        pitchedSfxSource.PlayOneShot(tenPopSFX, sfxVolume);
    }
    public void PlaySwapSound() => PlaySFX(swapSFX);
    public void PlayCountdownBeep() => PlaySFX(countdownBeepSFX);
    public void PlayCountdownGo() => PlaySFX(countdownGoSFX);
    public void PlayTransitionSwipe() => PlaySFX(transitionSwipeSFX);
    public void PlayTileSelect() => PlaySFX(tileSelectSFX);
    public void PlayFinishSound() => PlaySFX(finishSFX);
    public void PlayMultiplierIncrease() => PlaySFX(multiplierIncreaseSFX);

    /// <summary>
    /// Score ticks are lightly throttled — particles arrive every 0.03s but we only
    /// play every other one. Still sounds like a continuous stream, avoids pool starvation.
    /// </summary>
    public void PlayScoreTickSmall()
    {
        if (Time.time - lastScoreTickTime < SCORE_TICK_MIN_INTERVAL) return;
        lastScoreTickTime = Time.time;
        PlaySFX(scoreTickSmallSFX);
    }

    public void PlayScoreTickBig()
    {
        // Big ticks are rarer (0.08s stagger) and more important — always play
        PlaySFX(scoreTickBigSFX);
    }

    public void PlayComboSound() => PlaySFX(comboMergeSFX);
    public void PlayUltraComboSound() => PlaySFX(ultraComboSFX);

    #endregion
    
    #region Looping SFX (Time Warning)
    
    public void StartTimeWarning()
    {
        if (timeWarningSource == null || timeWarningSFX == null || timeWarningSource.isPlaying) return;

        timeWarningSource.clip = timeWarningSFX;
        timeWarningSource.loop = true;
        timeWarningSource.volume = sfxVolume;
        timeWarningSource.Play();
    }

    public void StopTimeWarning()
    {
        if (timeWarningSource == null || timeWarningSource.clip != timeWarningSFX) return;

        timeWarningSource.Stop();
        timeWarningSource.loop = false;
        timeWarningSource.clip = null;
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
