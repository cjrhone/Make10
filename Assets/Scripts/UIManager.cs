using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Handles all UI updates: score display, motivation bar, multiplier bar, game over screens.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Score Display")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text targetScoreText;
    [SerializeField] private Slider scoreProgressSlider; // Progress toward win score
    [SerializeField] private Image scoreProgressFillImage; // Optional fill image for color control
    
    [Header("Score Progress Colors")]
    [SerializeField] private Color scoreProgressStartColor = new Color(0.3f, 0.5f, 0.9f); // Blue
    [SerializeField] private Color scoreProgressMidColor = new Color(0.9f, 0.7f, 0.2f);   // Gold
    [SerializeField] private Color scoreProgressFullColor = new Color(0.3f, 0.9f, 0.3f);  // Green
    
    [Header("Timer Display")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text timerShadowText; // Optional duplicate for drop shadow effect
    [SerializeField] private Image timerFillImage; // Optional circular or bar fill
    [SerializeField] private Slider timerSlider; // Optional slider display
    
    [Header("Timer Colors")]
    [SerializeField] private bool useTimerTextColorChange = false; // Set false to keep your own text colors
    [SerializeField] private bool useTimerFillColorChange = true; // Set true to change fill colors
    [SerializeField] private Color timerHealthyColor = new Color(0.3f, 0.8f, 0.3f);
    [SerializeField] private Color timerWarningColor = new Color(0.9f, 0.7f, 0.2f);
    [SerializeField] private Color timerDangerColor = new Color(0.9f, 0.2f, 0.2f);
    [SerializeField] private float timerWarningThreshold = 20f; // seconds
    [SerializeField] private float timerDangerThreshold = 10f; // seconds
    
    [Header("Multiplier Bar (NEW)")]
    [SerializeField] private GameObject multiplierPanel;
    [SerializeField] private Slider multiplierSlider;
    [SerializeField] private TMP_Text multiplierValueText; // Shows "x2", "x3", etc.
    [SerializeField] private TMP_Text multiplierTimerText; // Shows seconds remaining
    [SerializeField] private Image multiplierFillImage;
    
    [Header("Multiplier Bar Colors")]
    [SerializeField] private Color multiplierFullColor = new Color(1f, 0.8f, 0.2f);   // Gold
    [SerializeField] private Color multiplierLowColor = new Color(1f, 0.3f, 0.2f);    // Red-orange
    [SerializeField] private float multiplierLowThreshold = 2f; // seconds
    
    [Header("Multiplier Pulse Settings")]
    [SerializeField] private float pulseMinScale = 1.0f;
    [SerializeField] private float pulseMaxScale = 1.3f;
    [SerializeField] private float pulseSpeed = 4f;
    
    [Header("Score Popup")]
    [SerializeField] private GameObject scorePopupPrefab;
    [SerializeField] private Transform scorePopupParent;
    
    [Header("Game Over Settings")]
    [SerializeField] private GameObject finishTextObject; // "FINISH" text that appears before win/lose
    [SerializeField] private float finishTextDuration = 1.5f; // How long to show "FINISH"
    
    [Header("Game Over Screens")]
    [SerializeField] private GameObject winScreen;
    [SerializeField] private GameObject loseScreen;
    [SerializeField] private TMP_Text finalScoreText;
    
    [Header("Unsolvable Grid Popup")]
    [SerializeField] private GameObject unsolvablePopup;
    [SerializeField] private float unsolvablePopupDuration = 1f;
    
    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private GridManager gridManager;
    
    private Coroutine timerPulseCoroutine;
    private Coroutine multiplierPulseCoroutine;
    private bool isTimeWarningPlaying = false;
    private bool isSubscribed = false;
    
    private void Awake()
    {
        // Try to subscribe immediately if managers exist
        TrySubscribeToEvents();
    }
    
    private void Start()
    {
        // Retry subscription in case managers weren't ready in Awake
        if (!isSubscribed)
        {
            TrySubscribeToEvents();
        }
        
        InitializeUI();
        Debug.Log("UIManager initialized successfully!");
    }
    
    /// <summary>
    /// Attempt to subscribe to all manager events.
    /// </summary>
    private void TrySubscribeToEvents()
    {
        if (isSubscribed) return;
        
        // Find GameManager
        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }
        
        if (gameManager == null)
        {
            Debug.LogWarning("UIManager: GameManager not found yet, will retry...");
            return;
        }
        
        // Subscribe to GameManager events
        gameManager.OnScoreChanged += HandleScoreChanged;
        gameManager.OnTimeChanged += HandleTimeChanged;
        gameManager.OnMultiplierChanged += HandleMultiplierChanged;
        gameManager.OnGameWon += HandleGameWon;
        gameManager.OnGameLost += HandleGameLost;
        
        // Find and subscribe to GridManager events
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }
        if (gridManager != null)
        {
            gridManager.OnGridUnsolvable += HandleGridUnsolvable;
        }
        
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
        {
            gridManager.OnGridUnsolvable -= HandleGridUnsolvable;
        }
    }
    
    /// <summary>
    /// Set up initial UI state.
    /// </summary>
    private void InitializeUI()
    {
        // Hide game over screens and finish text
        if (winScreen != null) winScreen.SetActive(false);
        if (loseScreen != null) loseScreen.SetActive(false);
        if (finishTextObject != null) finishTextObject.SetActive(false);
        if (unsolvablePopup != null) unsolvablePopup.SetActive(false);
        
        // Hide multiplier panel initially
        if (multiplierPanel != null) multiplierPanel.SetActive(false);
        
        // Set target score text dynamically from GameManager
        if (targetScoreText != null && gameManager != null)
        {
            targetScoreText.text = $"/ {gameManager.WinScore}";
        }
        
        // Initialize score progress bar
        if (scoreProgressSlider != null && gameManager != null)
        {
            scoreProgressSlider.minValue = 0;
            scoreProgressSlider.maxValue = gameManager.WinScore;
            scoreProgressSlider.value = 0;
        }
        
        // Initialize timer slider if present
        if (timerSlider != null && gameManager != null)
        {
            timerSlider.maxValue = gameManager.GameDuration;
            timerSlider.value = gameManager.GameDuration;
        }
        
        // Initialize multiplier bar - read duration from GameManager
        if (multiplierSlider != null && gameManager != null)
        {
            multiplierSlider.maxValue = gameManager.MultiplierDuration;
            multiplierSlider.value = gameManager.MultiplierDuration;
        }
        
        UpdateScoreDisplay(0);
        UpdateTimerDisplay(gameManager != null ? gameManager.GameDuration : 60f);
    }
    
    /// <summary>
    /// Handle score changes.
    /// </summary>
    private void HandleScoreChanged(int newScore, int delta)
    {
        UpdateScoreDisplay(newScore);
        
        if (delta > 0)
        {
            SpawnScorePopup(delta);
        }
    }
    
    /// <summary>
    /// Handle time changes.
    /// </summary>
    private void HandleTimeChanged(float timeRemaining)
    {
        UpdateTimerDisplay(timeRemaining);
    }
    
    /// <summary>
    /// Handle multiplier bar state changes.
    /// </summary>
    private void HandleMultiplierChanged(bool active, float multiplier, float timer)
    {
        UpdateMultiplierBar(active, multiplier, timer);
    }
    
    /// <summary>
    /// Update the score display and progress bar.
    /// </summary>
    private void UpdateScoreDisplay(int score)
    {
        if (scoreText != null)
        {
            scoreText.text = score.ToString();
            StartCoroutine(PunchScale(scoreText.transform, 1.2f, 0.15f));
        }
        
        // Update progress bar
        if (scoreProgressSlider != null)
        {
            scoreProgressSlider.value = score;
        }
        
        // Update progress bar color based on progress
        if (scoreProgressFillImage != null && gameManager != null)
        {
            float progress = (float)score / gameManager.WinScore;
            
            if (progress < 0.5f)
            {
                // Blue to Gold (0% to 50%)
                float t = progress / 0.5f;
                scoreProgressFillImage.color = Color.Lerp(scoreProgressStartColor, scoreProgressMidColor, t);
            }
            else
            {
                // Gold to Green (50% to 100%)
                float t = (progress - 0.5f) / 0.5f;
                scoreProgressFillImage.color = Color.Lerp(scoreProgressMidColor, scoreProgressFullColor, t);
            }
        }
    }
    
    /// <summary>
    /// Update the countdown timer display.
    /// </summary>
    private void UpdateTimerDisplay(float timeRemaining)
    {
        int seconds = Mathf.CeilToInt(timeRemaining);
        
        // Determine current danger state for sounds/pulse (regardless of color settings)
        bool inDanger = timeRemaining <= timerDangerThreshold;
        bool inWarning = timeRemaining <= timerWarningThreshold;
        
        // Handle pulse and warning sounds based on time state
        if (inDanger)
        {
            StartTimerPulse();
            
            // Start warning sound if not already playing
            if (!isTimeWarningPlaying && AudioManager.Instance != null)
            {
                AudioManager.Instance.StartTimeWarning();
                isTimeWarningPlaying = true;
            }
        }
        else
        {
            StopTimerPulse();
            StopTimeWarningSound();
        }
        
        // Update main text display
        if (timerText != null)
        {
            timerText.text = seconds.ToString();
            
            // Only change text color if enabled
            if (useTimerTextColorChange)
            {
                if (inDanger)
                {
                    timerText.color = timerDangerColor;
                }
                else if (inWarning)
                {
                    timerText.color = timerWarningColor;
                }
                else
                {
                    timerText.color = timerHealthyColor;
                }
            }
        }
        
        // Update shadow text (if using duplicate approach)
        if (timerShadowText != null)
        {
            timerShadowText.text = seconds.ToString();
        }
        
        // Update optional slider
        if (timerSlider != null)
        {
            timerSlider.value = timeRemaining;
        }
        
        // Update optional fill image
        if (timerFillImage != null && gameManager != null)
        {
            float fillAmount = timeRemaining / gameManager.GameDuration;
            timerFillImage.fillAmount = fillAmount;
            
            // Only change fill color if enabled
            if (useTimerFillColorChange)
            {
                if (inDanger)
                {
                    timerFillImage.color = timerDangerColor;
                }
                else if (inWarning)
                {
                    timerFillImage.color = timerWarningColor;
                }
                else
                {
                    timerFillImage.color = timerHealthyColor;
                }
            }
        }
    }
    
    /// <summary>
    /// Update the multiplier bar display.
    /// </summary>
    private void UpdateMultiplierBar(bool active, float multiplier, float timer)
    {
        if (multiplierPanel == null) return;
        
        if (active)
        {
            // Show panel
            if (!multiplierPanel.activeSelf)
            {
                multiplierPanel.SetActive(true);
                StartMultiplierPulse();
                
                // Animate panel appearing
                StartCoroutine(PunchScale(multiplierPanel.transform, 1.15f, 0.2f));
            }
            
            // Update slider
            if (multiplierSlider != null)
            {
                multiplierSlider.value = timer;
            }
            
            // Update multiplier text
            if (multiplierValueText != null)
            {
                multiplierValueText.text = $"x{multiplier:F2}";
            }
            
            // Update timer countdown text
            if (multiplierTimerText != null)
            {
                multiplierTimerText.text = $"{timer:F1}s";
            }
            
            // Update bar color based on time remaining
            if (multiplierFillImage != null)
            {
                if (timer <= multiplierLowThreshold)
                {
                    multiplierFillImage.color = Color.Lerp(multiplierLowColor, multiplierFullColor, timer / multiplierLowThreshold);
                }
                else
                {
                    multiplierFillImage.color = multiplierFullColor;
                }
            }
        }
        else
        {
            // Hide panel
            if (multiplierPanel.activeSelf)
            {
                StopMultiplierPulse();
                multiplierPanel.SetActive(false);
            }
        }
    }
    
    /// <summary>
    /// Start pulsing the multiplier text with energy.
    /// </summary>
    private void StartMultiplierPulse()
    {
        if (multiplierPulseCoroutine == null && multiplierValueText != null)
        {
            multiplierPulseCoroutine = StartCoroutine(MultiplierPulseCoroutine());
        }
    }
    
    /// <summary>
    /// Stop the multiplier pulse.
    /// </summary>
    private void StopMultiplierPulse()
    {
        if (multiplierPulseCoroutine != null)
        {
            StopCoroutine(multiplierPulseCoroutine);
            multiplierPulseCoroutine = null;
            
            if (multiplierValueText != null)
            {
                multiplierValueText.transform.localScale = Vector3.one;
            }
        }
    }
    
    /// <summary>
    /// Energetic pulse animation for multiplier text.
    /// </summary>
    private IEnumerator MultiplierPulseCoroutine()
    {
        while (true)
        {
            if (multiplierValueText != null)
            {
                // Smooth sine wave pulse
                float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;
                float scale = Mathf.Lerp(pulseMinScale, pulseMaxScale, t);
                multiplierValueText.transform.localScale = Vector3.one * scale;
                
                // Optional: slight color intensity pulse
                Color baseColor = Color.white;
                Color brightColor = new Color(1f, 1f, 0.7f); // Slight yellow glow
                multiplierValueText.color = Color.Lerp(baseColor, brightColor, t);
            }
            yield return null;
        }
    }
    
    /// <summary>
    /// Start pulsing the timer when in danger zone.
    /// </summary>
    private void StartTimerPulse()
    {
        if (timerPulseCoroutine == null && timerText != null)
        {
            timerPulseCoroutine = StartCoroutine(TimerPulseCoroutine());
        }
    }
    
    /// <summary>
    /// Stop the timer pulse.
    /// </summary>
    private void StopTimerPulse()
    {
        if (timerPulseCoroutine != null)
        {
            StopCoroutine(timerPulseCoroutine);
            timerPulseCoroutine = null;
            
            if (timerText != null)
            {
                timerText.transform.localScale = Vector3.one;
            }
        }
    }
    
    /// <summary>
    /// Stop the time warning sound.
    /// </summary>
    private void StopTimeWarningSound()
    {
        if (isTimeWarningPlaying && AudioManager.Instance != null)
        {
            AudioManager.Instance.StopTimeWarning();
            isTimeWarningPlaying = false;
        }
    }
    
    /// <summary>
    /// Pulse animation for danger state (low time).
    /// </summary>
    private IEnumerator TimerPulseCoroutine()
    {
        while (true)
        {
            if (timerText != null)
            {
                float t = (Mathf.Sin(Time.time * 8f) + 1f) / 2f;
                float scale = Mathf.Lerp(1f, 1.15f, t);
                timerText.transform.localScale = Vector3.one * scale;
            }
            yield return null;
        }
    }
    
    /// <summary>
    /// Spawn floating score popup.
    /// </summary>
    private void SpawnScorePopup(int points)
    {
        if (scorePopupPrefab == null || scorePopupParent == null) return;
        
        GameObject popup = Instantiate(scorePopupPrefab, scorePopupParent);
        TMP_Text popupText = popup.GetComponent<TMP_Text>();
        
        if (popupText != null)
        {
            popupText.text = $"+{points}";
        }
        
        StartCoroutine(AnimateScorePopup(popup));
    }
    
    /// <summary>
    /// Animate score popup floating up and fading.
    /// </summary>
    private IEnumerator AnimateScorePopup(GameObject popup)
    {
        RectTransform rt = popup.GetComponent<RectTransform>();
        TMP_Text text = popup.GetComponent<TMP_Text>();
        
        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos = startPos + new Vector2(0, 50f);
        float duration = 0.8f;
        float elapsed = 0f;
        
        Color startColor = text.color;
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            text.color = Color.Lerp(startColor, endColor, t);
            
            yield return null;
        }
        
        Destroy(popup);
    }
    
    /// <summary>
    /// Quick scale punch effect.
    /// </summary>
    private IEnumerator PunchScale(Transform target, float punchScale, float duration)
    {
        float elapsed = 0f;
        float halfDuration = duration / 2f;
        
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            float scale = Mathf.Lerp(1f, punchScale, t);
            target.localScale = Vector3.one * scale;
            yield return null;
        }
        
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            float scale = Mathf.Lerp(punchScale, 1f, t);
            target.localScale = Vector3.one * scale;
            yield return null;
        }
        
        target.localScale = Vector3.one;
    }
    
    /// <summary>
    /// Handle win state.
    /// </summary>
    private void HandleGameWon()
    {
        // Stop warning sound
        StopTimeWarningSound();
        
        // Show FINISH sequence
        StartCoroutine(ShowFinishThenResult(true));
    }
    
    /// <summary>
    /// Handle lose state (time ran out before reaching goal).
    /// </summary>
    private void HandleGameLost()
    {
        // Stop warning sound
        StopTimeWarningSound();
        
        // Show FINISH sequence
        StartCoroutine(ShowFinishThenResult(false));
    }
    
    /// <summary>
    /// Show "FINISH" text, then reveal win/lose screen.
    /// </summary>
    private IEnumerator ShowFinishThenResult(bool isWin)
    {
        // Play finish sound
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayFinishSound();
        
        // Show FINISH text with pop animation
        if (finishTextObject != null)
        {
            finishTextObject.SetActive(true);
            finishTextObject.transform.localScale = Vector3.zero;
            
            // Pop in
            float elapsed = 0f;
            while (elapsed < 0.2f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.2f;
                float scale = Mathf.Lerp(0f, 1.2f, t);
                finishTextObject.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            
            // Settle to normal
            elapsed = 0f;
            while (elapsed < 0.1f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.1f;
                float scale = Mathf.Lerp(1.2f, 1f, t);
                finishTextObject.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            
            finishTextObject.transform.localScale = Vector3.one;
        }
        
        // Wait for finish duration
        yield return new WaitForSeconds(finishTextDuration);
        
        // Hide FINISH text
        if (finishTextObject != null)
        {
            finishTextObject.SetActive(false);
        }
        
        // Show appropriate result screen
        if (isWin)
        {
            if (winScreen != null)
            {
                winScreen.SetActive(true);
            }
            
            if (finalScoreText != null)
            {
                float timeLeft = gameManager != null ? gameManager.TimeRemaining : 0f;
                finalScoreText.text = $"Score: {gameManager.Score}\nTime Left: {timeLeft:F1}s";
            }
        }
        else
        {
            if (loseScreen != null)
            {
                loseScreen.SetActive(true);
            }
            
            if (finalScoreText != null)
            {
                int target = gameManager != null ? gameManager.WinScore : 250;
                finalScoreText.text = $"Score: {gameManager.Score} / {target}\nSo close!";
            }
        }
    }
    
    /// <summary>
    /// Handle unsolvable grid - show popup.
    /// </summary>
    private void HandleGridUnsolvable()
    {
        if (unsolvablePopup != null)
        {
            StartCoroutine(ShowUnsolvablePopup());
        }
    }
    
    /// <summary>
    /// Show and hide the unsolvable popup.
    /// </summary>
    private IEnumerator ShowUnsolvablePopup()
    {
        unsolvablePopup.SetActive(true);
        
        // Scale in
        unsolvablePopup.transform.localScale = Vector3.zero;
        float elapsed = 0f;
        float scaleInTime = 0.2f;
        
        while (elapsed < scaleInTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / scaleInTime;
            float scale = Mathf.Lerp(0f, 1.1f, t);
            unsolvablePopup.transform.localScale = Vector3.one * scale;
            yield return null;
        }
        
        unsolvablePopup.transform.localScale = Vector3.one;
        
        yield return new WaitForSeconds(unsolvablePopupDuration);
        
        // Scale out
        elapsed = 0f;
        float scaleOutTime = 0.15f;
        
        while (elapsed < scaleOutTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / scaleOutTime;
            float scale = Mathf.Lerp(1f, 0f, t);
            unsolvablePopup.transform.localScale = Vector3.one * scale;
            yield return null;
        }
        
        unsolvablePopup.SetActive(false);
    }
    
    /// <summary>
    /// Called by restart button.
    /// </summary>
    public void OnRestartButtonClicked()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();
        
        if (winScreen != null) winScreen.SetActive(false);
        if (loseScreen != null) loseScreen.SetActive(false);
        
        // Stop any active pulses and sounds
        StopTimerPulse();
        StopMultiplierPulse();
        StopTimeWarningSound();
        
        if (gameManager != null)
        {
            gameManager.StartNewGame();
        }
        
        GridManager gridManager = FindFirstObjectByType<GridManager>();
        if (gridManager != null)
        {
            gridManager.ResetGame();
        }
    }
}
