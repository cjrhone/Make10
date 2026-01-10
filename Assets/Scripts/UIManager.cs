using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Handles all UI updates: score display, timer, multiplier bar, game over screens.
/// Refactored to use AnimationUtilities for consistent animations.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Score Display")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text targetScoreText;
    [SerializeField] private Slider scoreProgressSlider;
    [SerializeField] private Image scoreProgressFillImage;
    
    [Header("Score Progress Colors")]
    [SerializeField] private Color scoreProgressStartColor = new Color(0.3f, 0.5f, 0.9f);
    [SerializeField] private Color scoreProgressMidColor = new Color(0.9f, 0.7f, 0.2f);
    [SerializeField] private Color scoreProgressFullColor = new Color(0.3f, 0.9f, 0.3f);
    
    [Header("Timer Display")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text timerShadowText;
    [SerializeField] private Image timerFillImage;
    [SerializeField] private Slider timerSlider;
    
    [Header("Timer Colors")]
    [SerializeField] private bool useTimerTextColorChange = false;
    [SerializeField] private bool useTimerFillColorChange = true;
    [SerializeField] private Color timerHealthyColor = new Color(0.3f, 0.8f, 0.3f);
    [SerializeField] private Color timerWarningColor = new Color(0.9f, 0.7f, 0.2f);
    [SerializeField] private Color timerDangerColor = new Color(0.9f, 0.2f, 0.2f);
    [SerializeField] private float timerWarningThreshold = 20f;
    [SerializeField] private float timerDangerThreshold = 10f;
    
    [Header("Multiplier Bar")]
    [SerializeField] private GameObject multiplierPanel;
    [SerializeField] private Slider multiplierSlider;
    [SerializeField] private TMP_Text multiplierValueText;
    [SerializeField] private TMP_Text multiplierTimerText;
    [SerializeField] private Image multiplierFillImage;
    
    [Header("Multiplier Bar Colors")]
    [SerializeField] private Color multiplierFullColor = new Color(1f, 0.8f, 0.2f);
    [SerializeField] private Color multiplierLowColor = new Color(1f, 0.3f, 0.2f);
    [SerializeField] private float multiplierLowThreshold = 2f;
    
    [Header("Hot Streak Effect")]
    [SerializeField] private HotStreakEffect hotStreakEffect;
    [SerializeField] private bool enableHotStreak = true;
    
    [Header("Pulse Settings")]
    [SerializeField] private float pulseMinScale = 1.0f;
    [SerializeField] private float pulseMaxScale = 1.3f;
    [SerializeField] private float pulseSpeed = 4f;
    
    [Header("Score Popup")]
    [SerializeField] private GameObject scorePopupPrefab;
    [SerializeField] private Transform scorePopupParent;
    
    [Header("Game Over")]
    [SerializeField] private GameObject finishTextObject;
    [SerializeField] private float finishTextDuration = 1.5f;
    [SerializeField] private GameObject winScreen;
    [SerializeField] private GameObject loseScreen;
    [SerializeField] private TMP_Text winScoreText;
    [SerializeField] private TMP_Text loseScoreText;
    
    [Header("Unsolvable Grid Popup")]
    [SerializeField] private GameObject unsolvablePopup;
    [SerializeField] private float unsolvablePopupDuration = 1f;
    
    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private GridManager gridManager;
    
    // Coroutine tracking
    private Coroutine timerPulseCoroutine;
    private Coroutine multiplierPulseCoroutine;
    private bool isTimeWarningPlaying = false;
    private bool isSubscribed = false;
    private bool hotStreakActive = false;
    
    #region Initialization
    
    private void Awake()
    {
        TrySubscribeToEvents();
    }
    
    private void Start()
    {
        if (!isSubscribed)
            TrySubscribeToEvents();
        
        InitializeUI();
        Debug.Log("UIManager initialized successfully!");
    }
    
    private void TrySubscribeToEvents()
    {
        if (isSubscribed) return;
        
        if (gameManager == null)
            gameManager = GameManager.Instance;
        
        if (gameManager == null)
        {
            Debug.LogWarning("UIManager: GameManager not found yet, will retry...");
            return;
        }
        
        // Subscribe to events
        gameManager.OnScoreChanged += HandleScoreChanged;
        gameManager.OnTimeChanged += HandleTimeChanged;
        gameManager.OnMultiplierChanged += HandleMultiplierChanged;
        gameManager.OnGameWon += HandleGameWon;
        gameManager.OnGameLost += HandleGameLost;
        
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();
        
        if (gridManager != null)
            gridManager.OnGridUnsolvable += HandleGridUnsolvable;
        
        isSubscribed = true;
    }
    
    private void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.OnScoreChanged -= HandleScoreChanged;
            gameManager.OnTimeChanged -= HandleTimeChanged;
            gameManager.OnMultiplierChanged -= HandleMultiplierChanged;
            gameManager.OnGameWon -= HandleGameWon;
            gameManager.OnGameLost -= HandleGameLost;
        }
        
        if (gridManager != null)
            gridManager.OnGridUnsolvable -= HandleGridUnsolvable;
    }
    
    private void InitializeUI()
    {
        // Hide overlays
        SetActiveIfNotNull(winScreen, false);
        SetActiveIfNotNull(loseScreen, false);
        SetActiveIfNotNull(finishTextObject, false);
        SetActiveIfNotNull(unsolvablePopup, false);
        SetActiveIfNotNull(multiplierPanel, false);
        
        // Initialize from GameManager
        if (gameManager != null)
        {
            if (targetScoreText != null)
                targetScoreText.text = $"/ {gameManager.WinScore}";
            
            if (scoreProgressSlider != null)
            {
                scoreProgressSlider.minValue = 0;
                scoreProgressSlider.maxValue = gameManager.WinScore;
                scoreProgressSlider.value = 0;
            }
            
            if (timerSlider != null)
            {
                timerSlider.maxValue = gameManager.GameDuration;
                timerSlider.value = gameManager.GameDuration;
            }
            
            if (multiplierSlider != null)
            {
                multiplierSlider.maxValue = gameManager.MultiplierDuration;
                multiplierSlider.value = gameManager.MultiplierDuration;
            }
        }
        
        UpdateScoreDisplay(0);
        UpdateTimerDisplay(gameManager?.GameDuration ?? 60f);
        
        // Auto-find HotStreakEffect if not assigned
        if (hotStreakEffect == null && multiplierPanel != null)
            hotStreakEffect = multiplierPanel.GetComponent<HotStreakEffect>();
    }
    
    #endregion
    
    #region Event Handlers
    
    private void HandleScoreChanged(int newScore, int delta)
    {
        UpdateScoreDisplay(newScore);
        if (delta > 0)
            SpawnScorePopup(delta);
    }
    
    private void HandleTimeChanged(float timeRemaining)
    {
        UpdateTimerDisplay(timeRemaining);
    }
    
    private void HandleMultiplierChanged(bool active, float multiplier, float timer)
    {
        UpdateMultiplierBar(active, multiplier, timer);
    }
    
    private void HandleGameWon()
    {
        StopTimeWarningSound();
        DeactivateHotStreak();
        StartCoroutine(ShowFinishThenResult(true));
    }
    
    private void HandleGameLost()
    {
        StopTimeWarningSound();
        DeactivateHotStreak();
        StartCoroutine(ShowFinishThenResult(false));
    }
    
    private void HandleGridUnsolvable()
    {
        if (unsolvablePopup != null)
            StartCoroutine(ShowPopupBriefly(unsolvablePopup, unsolvablePopupDuration));
    }
    
    #endregion
    
    #region Display Updates
    
    private void UpdateScoreDisplay(int score)
    {
        if (scoreText != null)
        {
            scoreText.text = score.ToString();
            StartCoroutine(AnimationUtilities.PunchScale(scoreText.transform, 1.2f, 0.15f));
        }
        
        if (scoreProgressSlider != null)
            scoreProgressSlider.value = score;
        
        // Gradient color based on progress
        if (scoreProgressFillImage != null && gameManager != null)
        {
            float progress = (float)score / gameManager.WinScore;
            scoreProgressFillImage.color = GetGradientColor(progress, 
                scoreProgressStartColor, scoreProgressMidColor, scoreProgressFullColor);
        }
    }
    
    private void UpdateTimerDisplay(float timeRemaining)
    {
        int seconds = Mathf.CeilToInt(timeRemaining);
        
        // Determine state
        TimerState state = GetTimerState(timeRemaining);
        Color stateColor = GetTimerColor(state);
        
        // Handle pulse and warning sounds
        if (state == TimerState.Danger)
        {
            StartPulse(ref timerPulseCoroutine, timerText?.transform, 1f, 1.15f, 8f);
            StartTimeWarningSound();
        }
        else
        {
            StopPulse(ref timerPulseCoroutine, timerText?.transform);
            StopTimeWarningSound();
        }
        
        // Update text
        if (timerText != null)
        {
            timerText.text = seconds.ToString();
            if (useTimerTextColorChange)
                timerText.color = stateColor;
        }
        
        if (timerShadowText != null)
            timerShadowText.text = seconds.ToString();
        
        // Update slider
        if (timerSlider != null)
            timerSlider.value = timeRemaining;
        
        // Update fill
        if (timerFillImage != null && gameManager != null)
        {
            timerFillImage.fillAmount = timeRemaining / gameManager.GameDuration;
            if (useTimerFillColorChange)
                timerFillImage.color = stateColor;
        }
    }
    
    private void UpdateMultiplierBar(bool active, float multiplier, float timer)
    {
        if (multiplierPanel == null) return;
        
        if (active)
        {
            if (!multiplierPanel.activeSelf)
            {
                multiplierPanel.SetActive(true);
                StartPulse(ref multiplierPulseCoroutine, multiplierValueText?.transform, 
                    pulseMinScale, pulseMaxScale, pulseSpeed);
                StartCoroutine(AnimationUtilities.PunchScale(multiplierPanel.transform, 1.15f, 0.2f));
                
                // Activate hot streak effect!
                ActivateHotStreak(multiplier);
            }
            
            if (multiplierSlider != null)
                multiplierSlider.value = timer;
            
            if (multiplierValueText != null)
                multiplierValueText.text = $"x{multiplier:F2}";
            
            if (multiplierTimerText != null)
                multiplierTimerText.text = $"{timer:F1}s";
            
            // Color based on timer (only if hot streak not overriding)
            if (multiplierFillImage != null && !enableHotStreak)
            {
                multiplierFillImage.color = timer <= multiplierLowThreshold
                    ? Color.Lerp(multiplierLowColor, multiplierFullColor, timer / multiplierLowThreshold)
                    : multiplierFullColor;
            }
            
            // Update hot streak intensity as multiplier grows
            UpdateHotStreakIntensity(multiplier);
        }
        else if (multiplierPanel.activeSelf)
        {
            StopPulse(ref multiplierPulseCoroutine, multiplierValueText?.transform);
            DeactivateHotStreak();
            multiplierPanel.SetActive(false);
        }
    }
    
    #endregion
    
    #region Hot Streak
    
    private void ActivateHotStreak(float multiplier)
    {
        if (!enableHotStreak || hotStreakEffect == null) return;
        
        hotStreakEffect.Activate(multiplier);
        hotStreakActive = true;
        
        Debug.Log($"<color=orange>🔥 HOT STREAK ACTIVATED!</color> x{multiplier:F2}");
    }
    
    private void UpdateHotStreakIntensity(float multiplier)
    {
        if (!enableHotStreak || hotStreakEffect == null || !hotStreakActive) return;
        
        hotStreakEffect.UpdateIntensity(multiplier);
    }
    
    private void DeactivateHotStreak()
    {
        if (hotStreakEffect == null || !hotStreakActive) return;
        
        hotStreakEffect.Deactivate();
        hotStreakActive = false;
        
        Debug.Log("<color=gray>Hot streak ended.</color>");
    }
    
    #endregion
    
    #region Animations
    
    private void SpawnScorePopup(int points)
    {
        if (scorePopupPrefab == null || scorePopupParent == null) return;
        
        GameObject popup = Instantiate(scorePopupPrefab, scorePopupParent);
        TMP_Text popupText = popup.GetComponent<TMP_Text>();
        
        if (popupText != null)
            popupText.text = $"+{points}";
        
        StartCoroutine(AnimateAndDestroyPopup(popup));
    }
    
    private IEnumerator AnimateAndDestroyPopup(GameObject popup)
    {
        RectTransform rt = popup.GetComponent<RectTransform>();
        TMP_Text text = popup.GetComponent<TMP_Text>();
        
        yield return AnimationUtilities.FloatAndFade(rt, text, 50f, 0.8f);
        Destroy(popup);
    }
    
    private IEnumerator ShowFinishThenResult(bool isWin)
    {
        // STOP game music immediately when FINISH appears
        AudioManager.Instance?.StopMusic();
        
        // Play finish sound
        AudioManager.Instance?.PlayFinishSound();
        
        // Show FINISH with pop animation
        if (finishTextObject != null)
        {
            finishTextObject.SetActive(true);
            yield return AnimationUtilities.PopIn(finishTextObject.transform, 1.2f, 0.2f, 0.1f);
        }
        
        yield return new WaitForSeconds(finishTextDuration);
        
        SetActiveIfNotNull(finishTextObject, false);
        
        // Show result screen and play appropriate music
        if (isWin)
        {
            AudioManager.Instance?.PlayWinMusic();
            SetActiveIfNotNull(winScreen, true);
            if (winScoreText != null && gameManager != null)
            {
                winScoreText.text = $"Score: {gameManager.Score}\nTime Left: {gameManager.TimeRemaining:F1}s";
            }
        }
        else
        {
            AudioManager.Instance?.PlayLoseMusic();
            SetActiveIfNotNull(loseScreen, true);
            if (loseScoreText != null && gameManager != null)
            {
                loseScoreText.text = $"Score: {gameManager.Score} / {gameManager.WinScore}\nSo close!";
            }
        }
    }
    
    private IEnumerator ShowPopupBriefly(GameObject popup, float duration)
    {
        popup.SetActive(true);
        yield return AnimationUtilities.PopIn(popup.transform, 1.1f, 0.2f, 0.05f);
        
        yield return new WaitForSeconds(duration);
        
        yield return AnimationUtilities.ScaleOut(popup.transform, 0.15f);
        popup.SetActive(false);
    }
    
    #endregion
    
    #region Pulse Management
    
    private void StartPulse(ref Coroutine coroutine, Transform target, float min, float max, float speed)
    {
        if (coroutine == null && target != null)
            coroutine = StartCoroutine(AnimationUtilities.PulseLoop(target, min, max, speed));
    }
    
    private void StopPulse(ref Coroutine coroutine, Transform target)
    {
        if (coroutine != null)
        {
            StopCoroutine(coroutine);
            coroutine = null;
            if (target != null)
                target.localScale = Vector3.one;
        }
    }
    
    #endregion
    
    #region Audio Helpers
    
    private void StartTimeWarningSound()
    {
        if (!isTimeWarningPlaying)
        {
            AudioManager.Instance?.StartTimeWarning();
            isTimeWarningPlaying = true;
        }
    }
    
    private void StopTimeWarningSound()
    {
        if (isTimeWarningPlaying)
        {
            AudioManager.Instance?.StopTimeWarning();
            isTimeWarningPlaying = false;
        }
    }
    
    #endregion
    
    #region Utility Helpers
    
    private enum TimerState { Healthy, Warning, Danger }
    
    private TimerState GetTimerState(float timeRemaining)
    {
        if (timeRemaining <= timerDangerThreshold) return TimerState.Danger;
        if (timeRemaining <= timerWarningThreshold) return TimerState.Warning;
        return TimerState.Healthy;
    }
    
    private Color GetTimerColor(TimerState state)
    {
        return state switch
        {
            TimerState.Danger => timerDangerColor,
            TimerState.Warning => timerWarningColor,
            _ => timerHealthyColor
        };
    }
    
    /// <summary>
    /// Get a color from a 3-point gradient (0→mid at 50%, mid→end at 100%).
    /// </summary>
    private Color GetGradientColor(float progress, Color start, Color mid, Color end)
    {
        if (progress < 0.5f)
            return Color.Lerp(start, mid, progress / 0.5f);
        else
            return Color.Lerp(mid, end, (progress - 0.5f) / 0.5f);
    }
    
    private void SetActiveIfNotNull(GameObject obj, bool active)
    {
        if (obj != null) obj.SetActive(active);
    }
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Restart button clicked on win/lose screen.
    /// </summary>
    public void OnRestartButtonClicked()
    {
        AudioManager.Instance?.PlayButtonClick();
        
        // Clean up and restart
        CleanupGameOverState();
        
        // Use SceneFlowManager to restart with countdown
        if (SceneFlowManager.Instance != null)
        {
            SceneFlowManager.Instance.RestartWithCountdown();
        }
        else
        {
            // Fallback if no SceneFlowManager (for testing)
            Debug.LogWarning("No SceneFlowManager found - starting game directly");
            gameManager?.StartNewGame();
            GridManager grid = gridManager ?? FindFirstObjectByType<GridManager>();
            grid?.ResetGame();
        }
    }
    
    /// <summary>
    /// Main Menu button clicked on win/lose screen.
    /// </summary>
    public void OnMainMenuButtonClicked()
    {
        AudioManager.Instance?.PlayButtonClick();
        
        // Clean up game over state
        CleanupGameOverState();
        
        // Use SceneFlowManager's universal GoBack()
        SceneFlowManager.Instance?.GoBack();
    }
    
    /// <summary>
    /// Hide all game over screens (called by SceneFlowManager when returning to menu).
    /// </summary>
    public void HideAllGameOverScreens()
    {
        SetActiveIfNotNull(winScreen, false);
        SetActiveIfNotNull(loseScreen, false);
        SetActiveIfNotNull(finishTextObject, false);
        
        // Also clean up effects
        CleanupGameOverState();
    }
    
    /// <summary>
    /// Clean up all game over related state (effects, sounds, panels).
    /// </summary>
    private void CleanupGameOverState()
    {
        // Hide game over screens
        SetActiveIfNotNull(winScreen, false);
        SetActiveIfNotNull(loseScreen, false);
        SetActiveIfNotNull(finishTextObject, false);
        
        // Clean up any active effects
        StopPulse(ref timerPulseCoroutine, timerText?.transform);
        StopPulse(ref multiplierPulseCoroutine, multiplierValueText?.transform);
        StopTimeWarningSound();
        DeactivateHotStreak();
        
        // Hide multiplier panel for fresh start
        SetActiveIfNotNull(multiplierPanel, false);
    }
    
    #endregion
}
