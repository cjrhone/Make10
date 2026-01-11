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
    
    [Header("Hot Streak Mode UI")]
    [SerializeField] private GameObject hotStreakBackground;
    [SerializeField] private Color hotStreakFireColor1 = new Color(1f, 0.3f, 0.1f); // Red-orange
    [SerializeField] private Color hotStreakFireColor2 = new Color(1f, 0.9f, 0.2f); // Yellow
    [SerializeField] private float hotStreakPulseSpeed = 8f;
    
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
    private Coroutine multiplierGlowCoroutine;
    private Coroutine hotStreakTextPulseCoroutine;
    private bool isTimeWarningPlaying = false;
    private bool isSubscribed = false;
    private bool hotStreakActive = false;
    private bool isInHotStreakMode = false;
    
    // Hot Streak UI elements (created via code)
    private GameObject hotStreakTextObject;
    private TMPro.TMP_Text hotStreakText;
    
    // Multiplier text animation
    private float lastMultiplierValue = 1f;
    [Header("Multiplier Text Animation")]
    [SerializeField] private Color multiplierTextCoolColor = new Color(1f, 0.9f, 0.2f); // Yellow
    [SerializeField] private Color multiplierTextHotColor = new Color(1f, 0.2f, 0.2f); // Red at max
    [SerializeField] private Color multiplierGlowColor = new Color(1f, 0.95f, 0.5f); // Bright flash
    [SerializeField] private float multiplierMinScale = 1f;
    [SerializeField] private float multiplierMaxScale = 1.5f;
    [SerializeField] private float multiplierScaleAtMax = 3f; // What multiplier value = max scale
    
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
        gameManager.OnHotStreakStarted += HandleHotStreakStarted;
        gameManager.OnHotStreakEnded += HandleHotStreakEnded;
        
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
            gameManager.OnHotStreakStarted -= HandleHotStreakStarted;
            gameManager.OnHotStreakEnded -= HandleHotStreakEnded;
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
        
        // Hide hot streak background initially
        if (hotStreakBackground != null)
            hotStreakBackground.SetActive(false);
        
        // Create the HOT-STREAK text object (hidden initially)
        CreateHotStreakText();
    }
    
    private void CreateHotStreakText()
    {
        // Create a canvas for the hot streak text that renders on top
        hotStreakTextObject = new GameObject("HotStreakText");
        hotStreakTextObject.transform.SetParent(transform, false);
        
        // Add RectTransform and position in center of screen
        RectTransform rt = hotStreakTextObject.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(800f, 0f); // Start off-screen right
        rt.sizeDelta = new Vector2(600f, 150f);
        
        // Add TextMeshPro component
        hotStreakText = hotStreakTextObject.AddComponent<TMPro.TextMeshProUGUI>();
        hotStreakText.text = "HOT STREAK!";
        hotStreakText.fontSize = 72;
        hotStreakText.fontStyle = TMPro.FontStyles.Bold;
        hotStreakText.alignment = TMPro.TextAlignmentOptions.Center;
        hotStreakText.color = hotStreakFireColor1;
        
        // Enable gradient for fire effect
        hotStreakText.enableVertexGradient = true;
        hotStreakText.colorGradient = new TMPro.VertexGradient(
            hotStreakFireColor2, // top left - yellow
            hotStreakFireColor2, // top right - yellow  
            hotStreakFireColor1, // bottom left - red
            hotStreakFireColor1  // bottom right - red
        );
        
        hotStreakTextObject.SetActive(false);
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
        CleanupHotStreakMode();
        StartCoroutine(ShowFinishThenResult(true));
    }
    
    private void HandleGameLost()
    {
        StopTimeWarningSound();
        DeactivateHotStreak();
        CleanupHotStreakMode();
        StartCoroutine(ShowFinishThenResult(false));
    }
    
    private void HandleHotStreakStarted()
    {
        StartCoroutine(HotStreakIntroSequence());
    }
    
    private void HandleHotStreakEnded()
    {
        CleanupHotStreakMode();
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
                lastMultiplierValue = multiplier;
                StartCoroutine(AnimationUtilities.PunchScale(multiplierPanel.transform, 1.15f, 0.2f));
                
                // Activate hot streak effect!
                ActivateHotStreak(multiplier);
            }
            
            if (multiplierSlider != null)
                multiplierSlider.value = timer;
            
            if (multiplierValueText != null)
            {
                multiplierValueText.text = $"x{multiplier:F2}";
                
                // Scale text based on multiplier value (bigger multiplier = bigger text)
                float scaleT = Mathf.InverseLerp(1f, multiplierScaleAtMax, multiplier);
                float targetScale = Mathf.Lerp(multiplierMinScale, multiplierMaxScale, scaleT);
                
                // Color temperature: white (cool) at low multiplier, red (hot) at high
                Color temperatureColor = Color.Lerp(multiplierTextCoolColor, multiplierTextHotColor, scaleT);
                
                // If multiplier increased, do a glow + punch animation
                if (multiplier > lastMultiplierValue + 0.01f)
                {
                    TriggerMultiplierGlow(targetScale, temperatureColor);
                    AudioManager.Instance?.PlayMultiplierIncrease();
                }
                else
                {
                    // Just maintain the scale and color
                    multiplierValueText.transform.localScale = Vector3.one * targetScale;
                    multiplierValueText.color = temperatureColor;
                }
                
                lastMultiplierValue = multiplier;
            }
            
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
            StopMultiplierGlow();
            DeactivateHotStreak();
            multiplierPanel.SetActive(false);
            lastMultiplierValue = 1f;
        }
    }
    
    private void TriggerMultiplierGlow(float targetScale, Color targetColor)
    {
        if (multiplierValueText == null) return;
        
        // Stop any existing glow
        StopMultiplierGlow();
        
        multiplierGlowCoroutine = StartCoroutine(MultiplierGlowAnimation(targetScale, targetColor));
    }
    
    private void StopMultiplierGlow()
    {
        if (multiplierGlowCoroutine != null)
        {
            StopCoroutine(multiplierGlowCoroutine);
            multiplierGlowCoroutine = null;
        }
    }
    
    private IEnumerator MultiplierGlowAnimation(float targetScale, Color targetColor)
    {
        if (multiplierValueText == null) yield break;
        
        Transform textTransform = multiplierValueText.transform;
        float startScale = textTransform.localScale.x;
        float punchScale = targetScale * 1.3f; // Overshoot
        Color startColor = multiplierValueText.color;
        
        // Phase 1: Punch up with bright glow flash
        float elapsed = 0f;
        float punchDuration = 0.15f;
        
        while (elapsed < punchDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / punchDuration;
            
            // Scale punch
            float scale = Mathf.Lerp(startScale, punchScale, t);
            textTransform.localScale = Vector3.one * scale;
            
            // Color flash to bright glow
            multiplierValueText.color = Color.Lerp(startColor, multiplierGlowColor, t);
            
            yield return null;
        }
        
        // Phase 2: Settle back to temperature color
        elapsed = 0f;
        float settleDuration = 0.25f;
        
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / settleDuration;
            float smoothT = 1f - Mathf.Pow(1f - t, 3f); // Ease out
            
            // Scale settle
            float scale = Mathf.Lerp(punchScale, targetScale, smoothT);
            textTransform.localScale = Vector3.one * scale;
            
            // Color fade from glow to temperature color
            multiplierValueText.color = Color.Lerp(multiplierGlowColor, targetColor, smoothT);
            
            yield return null;
        }
        
        // Final state
        textTransform.localScale = Vector3.one * targetScale;
        multiplierValueText.color = targetColor;
        multiplierGlowCoroutine = null;
    }
    
    #endregion
    
    #region Hot Streak Mode
    
    private IEnumerator HotStreakIntroSequence()
    {
        Debug.Log("<color=orange>UIManager: Hot Streak intro starting!</color>");
        
        isInHotStreakMode = true;
        
        // Stop game music, play hot streak music
        AudioManager.Instance?.StopMusic();
        AudioManager.Instance?.PlayHotStreakMusic();
        
        // Enable hot streak background
        if (hotStreakBackground != null)
            hotStreakBackground.SetActive(true);
        
        // Show HOT-STREAK text with slide-in animation
        if (hotStreakTextObject != null)
        {
            hotStreakTextObject.SetActive(true);
            RectTransform rt = hotStreakTextObject.GetComponent<RectTransform>();
            
            // Slide in from right
            float slideDuration = 0.4f;
            float elapsed = 0f;
            Vector2 startPos = new Vector2(800f, 0f);
            Vector2 endPos = Vector2.zero;
            
            while (elapsed < slideDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / slideDuration;
                float smoothT = 1f - Mathf.Pow(1f - t, 3f); // Ease out
                rt.anchoredPosition = Vector2.Lerp(startPos, endPos, smoothT);
                yield return null;
            }
            rt.anchoredPosition = endPos;
            
            // Punch scale
            yield return AnimationUtilities.PunchScale(rt, 1.2f, 0.2f);
            
            // Hold for a moment
            yield return new WaitForSeconds(0.8f);
            
            // Slide out to left
            elapsed = 0f;
            startPos = Vector2.zero;
            endPos = new Vector2(-800f, 0f);
            
            while (elapsed < slideDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / slideDuration;
                float smoothT = t * t; // Ease in
                rt.anchoredPosition = Vector2.Lerp(startPos, endPos, smoothT);
                yield return null;
            }
            
            hotStreakTextObject.SetActive(false);
            rt.anchoredPosition = new Vector2(800f, 0f); // Reset for next time
        }
        
        // Start fire pulse effect on multiplier text
        StartHotStreakTextPulse();
    }
    
    private void StartHotStreakTextPulse()
    {
        if (hotStreakTextPulseCoroutine != null)
            StopCoroutine(hotStreakTextPulseCoroutine);
        
        hotStreakTextPulseCoroutine = StartCoroutine(HotStreakTextPulseLoop());
    }
    
    private IEnumerator HotStreakTextPulseLoop()
    {
        while (isInHotStreakMode && multiplierValueText != null)
        {
            float t = (Mathf.Sin(Time.time * hotStreakPulseSpeed) + 1f) / 2f;
            multiplierValueText.color = Color.Lerp(hotStreakFireColor1, hotStreakFireColor2, t);
            
            // Also pulse the scale slightly
            float scale = Mathf.Lerp(multiplierMaxScale, multiplierMaxScale * 1.1f, t);
            multiplierValueText.transform.localScale = Vector3.one * scale;
            
            yield return null;
        }
    }
    
    private void CleanupHotStreakMode()
    {
        Debug.Log("<color=gray>UIManager: Cleaning up Hot Streak mode</color>");
        
        isInHotStreakMode = false;
        
        // Stop fire pulse
        if (hotStreakTextPulseCoroutine != null)
        {
            StopCoroutine(hotStreakTextPulseCoroutine);
            hotStreakTextPulseCoroutine = null;
        }
        
        // Reset multiplier text color
        if (multiplierValueText != null)
        {
            multiplierValueText.color = multiplierTextCoolColor;
            multiplierValueText.transform.localScale = Vector3.one;
        }
        
        // Hide hot streak background
        if (hotStreakBackground != null)
            hotStreakBackground.SetActive(false);
        
        // Hide hot streak text (in case it's still showing)
        if (hotStreakTextObject != null)
            hotStreakTextObject.SetActive(false);
        
        // Resume normal game music
        AudioManager.Instance?.PlayGameMusic();
    }
    
    #endregion
    
    #region Hot Streak Effect (Fire Particles)
    
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
    /// Refresh the target score display based on current difficulty.
    /// Call this when difficulty changes or game starts.
    /// </summary>
    public void RefreshTargetScore()
    {
        if (gameManager == null) return;
        
        int winScore = gameManager.WinScore;
        
        if (targetScoreText != null)
            targetScoreText.text = $"/ {winScore}";
        
        if (scoreProgressSlider != null)
        {
            scoreProgressSlider.maxValue = winScore;
            scoreProgressSlider.value = gameManager.Score;
        }
        
        Debug.Log($"<color=cyan>Target score updated to {winScore}</color>");
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
        CleanupHotStreakMode();
        
        // Hide multiplier panel for fresh start
        SetActiveIfNotNull(multiplierPanel, false);
    }
    
    #endregion
}
