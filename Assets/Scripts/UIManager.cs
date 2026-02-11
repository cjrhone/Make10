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
    public static UIManager Instance { get; private set; }

    [Header("Score Display")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private Slider scoreProgressSlider;
    [SerializeField] private Image scoreProgressFillImage;

    [Header("Score Progress Colors")]
    [SerializeField] private Color scoreProgressStartColor = new Color(0.3f, 0.5f, 0.9f);
    [SerializeField] private Color scoreProgressMidColor = new Color(0.9f, 0.7f, 0.2f);
    [SerializeField] private Color scoreProgressFullColor = new Color(0.3f, 0.9f, 0.3f);

    [Header("Score Progress Glow")]
    [SerializeField] private Image scoreProgressGlow;
    [SerializeField] private Color scoreGlowColor = new Color(1f, 0.9f, 0.5f, 0.6f);
    [SerializeField] private float glowFadeDuration = 0.15f;

    // Pending score for particle-based increment
    private int displayedScore = 0;
    private int pendingScoreToAdd = 0;

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
    [SerializeField] private TMP_Text winScoreText;

    [Header("Win Screen Breakdown")]
    [SerializeField] private TMP_Text scoreLabelText;
    [SerializeField] private TMP_Text scoreValueText;
    [SerializeField] private TMP_Text sessionTimeLabelText;
    [SerializeField] private TMP_Text sessionTimeValueText;
    [SerializeField] private TMP_Text timeBonusLabelText;
    [SerializeField] private TMP_Text timeBonusValueText;
    [SerializeField] private TMP_Text hotStreakLabelText;
    [SerializeField] private TMP_Text hotStreakValueText;
    [SerializeField] private Image breakdownDivider;
    [SerializeField] private TMP_Text totalLabelText;
    [SerializeField] private TMP_Text totalValueText;
    [SerializeField] private float breakdownLineDelay = 0.3f;
    [SerializeField] private float countUpDuration = 0.5f;
    [SerializeField] private float timeBonusPerSecond = 1f;

    [Header("High Score")]
    [SerializeField] private TMP_Text highScoreText;
    [SerializeField] private GameObject newHighScoreBanner;
    [SerializeField] private Color newHighScoreColor = new Color(1f, 0.85f, 0.1f);

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
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

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
        gameManager.OnHotStreakStarted += HandleHotStreakStarted;
        gameManager.OnHotStreakEnded += HandleHotStreakEnded;

        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (gridManager != null)
            gridManager.OnGridUnsolvable += HandleGridUnsolvable;

        // Subscribe to RunManager events
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRoundChanged += HandleRoundChanged;
            RunManager.Instance.OnRunStarted += HandleRunStarted;
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
            gameManager.OnHotStreakStarted -= HandleHotStreakStarted;
            gameManager.OnHotStreakEnded -= HandleHotStreakEnded;
        }

        if (gridManager != null)
            gridManager.OnGridUnsolvable -= HandleGridUnsolvable;

        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRoundChanged -= HandleRoundChanged;
            RunManager.Instance.OnRunStarted -= HandleRunStarted;
        }
    }

    private void InitializeUI()
    {
        // Hide overlays
        SetActiveIfNotNull(winScreen, false);
        SetActiveIfNotNull(finishTextObject, false);
        SetActiveIfNotNull(unsolvablePopup, false);
        SetActiveIfNotNull(multiplierPanel, true);

        // Initialize multiplier display to x1.00
        if (multiplierValueText != null)
        {
            multiplierValueText.text = "x1.00";
            multiplierValueText.transform.localScale = Vector3.one;
            multiplierValueText.color = multiplierTextCoolColor;
        }

        // Initialize from GameManager
        if (gameManager != null)
        {
            if (scoreProgressSlider != null)
            {
                scoreProgressSlider.minValue = 0;
                scoreProgressSlider.maxValue = 1000; // Default max
                scoreProgressSlider.value = 0;
            }

            // Initialize score tracking
            displayedScore = 0;
            pendingScoreToAdd = 0;

            // Hide glow initially
            if (scoreProgressGlow != null)
                scoreProgressGlow.gameObject.SetActive(false);

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

        // Create breakdown UI elements if not assigned in inspector
        EnsureBreakdownElementsExist();
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
        if (delta > 0)
        {
            // Store pending score for particle-based animation
            pendingScoreToAdd += delta;
            SpawnScorePopup(delta);
        }
        else
        {
            // Direct update for non-positive changes (reset, etc.)
            displayedScore = newScore;
            pendingScoreToAdd = 0;
            UpdateScoreDisplay(newScore);
        }
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

    private void HandleRoundChanged(int roundNumber)
    {
        Debug.Log($"Round changed to: {roundNumber}");
    }

    private void HandleRunStarted()
    {
        // Reset score display when new run starts
        displayedScore = 0;
        pendingScoreToAdd = 0;
        UpdateScoreDisplay(0);
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

        // Fixed color without gradient based on win score
        if (scoreProgressFillImage != null)
        {
            scoreProgressFillImage.color = scoreProgressFullColor;
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
        else
        {
            // Multiplier inactive - reset display to x1.00 but keep panel visible
            StopPulse(ref multiplierPulseCoroutine, multiplierValueText?.transform);
            StopMultiplierGlow();
            DeactivateHotStreak();

            if (multiplierValueText != null)
            {
                multiplierValueText.text = "x1.00";
                multiplierValueText.transform.localScale = Vector3.one;
                multiplierValueText.color = multiplierTextCoolColor;
            }

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
        AudioManager.Instance?.PlayWinMusic();
        SetActiveIfNotNull(winScreen, true);
        // Start the animated score breakdown
        StartCoroutine(ShowWinScreenBreakdown());
    }

    private IEnumerator ShowPopupBriefly(GameObject popup, float duration)
    {
        popup.SetActive(true);
        yield return AnimationUtilities.PopIn(popup.transform, 1.1f, 0.2f, 0.05f);

        yield return new WaitForSeconds(duration);

        yield return AnimationUtilities.ScaleOut(popup.transform, 0.15f);
        popup.SetActive(false);
    }

    /// <summary>
    /// Sequential score breakdown on the win screen (Balatro-style).
    /// Each line appears with a count-up animation for numbers.
    /// </summary>
    private IEnumerator ShowWinScreenBreakdown()
    {
        if (gameManager == null) yield break;

        // Get values from GameManager
        int baseScore = gameManager.Score;
        float timeRemaining = gameManager.TimeRemaining;
        float maxMultiplier = gameManager.MaxMultiplierReached;
        float sessionDuration = gameManager.SessionDuration;

        // Calculate breakdown (1 BP per second remaining)
        int timeBonus = Mathf.RoundToInt(timeRemaining * timeBonusPerSecond);
        int subtotal = baseScore + timeBonus;
        int total = Mathf.RoundToInt(subtotal * maxMultiplier);

        // Format session time as MM:SS
        int sessionMinutes = (int)sessionDuration / 60;
        int sessionSeconds = (int)sessionDuration % 60;
        string sessionTimeStr = $"{sessionMinutes:D2}:{sessionSeconds:D2}";

        // Hide all breakdown elements initially
        HideBreakdownElements();

        // Small initial delay after win screen appears
        yield return new WaitForSeconds(0.3f);

        // Line 1: Score - appears and counts up
        if (scoreLabelText != null && scoreValueText != null)
        {
            scoreLabelText.transform.parent.gameObject.SetActive(true);
            scoreLabelText.text = "Score";
            AudioManager.Instance?.PlayButtonClick();
            yield return StartCoroutine(AnimationUtilities.CountUp(scoreValueText, 0, baseScore, countUpDuration, "{0} BP"));
        }

        yield return new WaitForSeconds(breakdownLineDelay);

        // Line 2: Session Time - appears instantly
        if (sessionTimeLabelText != null && sessionTimeValueText != null)
        {
            sessionTimeLabelText.transform.parent.gameObject.SetActive(true);
            sessionTimeLabelText.text = "Session Time";
            sessionTimeValueText.text = sessionTimeStr;
            AudioManager.Instance?.PlayButtonClick();
        }

        yield return new WaitForSeconds(breakdownLineDelay);

        // Line 3: Time Bonus - appears and counts up
        if (timeBonusLabelText != null && timeBonusValueText != null)
        {
            timeBonusLabelText.transform.parent.gameObject.SetActive(true);
            timeBonusLabelText.text = "Time Bonus";
            AudioManager.Instance?.PlayButtonClick();
            yield return StartCoroutine(AnimationUtilities.CountUp(timeBonusValueText, 0, timeBonus, countUpDuration, "+ {0} BP"));
        }

        yield return new WaitForSeconds(breakdownLineDelay);

        // Line 4: Hot Streak multiplier - appears instantly
        if (hotStreakLabelText != null && hotStreakValueText != null)
        {
            hotStreakLabelText.transform.parent.gameObject.SetActive(true);
            hotStreakLabelText.text = "Hot Streak";
            hotStreakValueText.text = $"x{maxMultiplier:F1}";
            AudioManager.Instance?.PlayButtonClick();
        }

        yield return new WaitForSeconds(breakdownLineDelay);

        // Divider line
        if (breakdownDivider != null)
        {
            breakdownDivider.gameObject.SetActive(true);
        }

        yield return new WaitForSeconds(breakdownLineDelay);

        // Line 5: TOTAL - appears and counts up
        if (totalLabelText != null && totalValueText != null)
        {
            totalLabelText.transform.parent.gameObject.SetActive(true);
            totalLabelText.text = "TOTAL";
            AudioManager.Instance?.PlayButtonClick();
            yield return StartCoroutine(AnimationUtilities.CountUp(totalValueText, 0, total, countUpDuration * 1.2f, "{0} BP"));
        }

        // Save BP high score
        gameManager?.CheckAndSaveBPHighScore(total);

        // Show NEW HIGH SCORE banner if applicable
        if (gameManager != null && gameManager.IsNewHighScore)
        {
            yield return new WaitForSeconds(0.2f);
            ShowNewHighScoreBanner();
        }

        // Hide the legacy winScoreText since we're using breakdown
        if (winScoreText != null)
        {
            winScoreText.text = "";
        }
    }

    /// <summary>
    /// Show the NEW HIGH SCORE banner with animation.
    /// </summary>
    private void ShowNewHighScoreBanner()
    {
        if (newHighScoreBanner == null)
        {
            // Create it dynamically if not assigned in inspector
            if (winScreen == null) return;

            Transform breakdownContainer = winScreen.transform.Find("BreakdownContainer");
            if (breakdownContainer == null) return;

            GameObject bannerObj = new GameObject("NewHighScoreBanner");
            bannerObj.transform.SetParent(breakdownContainer, false);

            RectTransform bannerRT = bannerObj.AddComponent<RectTransform>();
            bannerRT.sizeDelta = new Vector2(0, 50f);

            TMP_Text bannerText = bannerObj.AddComponent<TextMeshProUGUI>();
            bannerText.text = "NEW HIGH SCORE!";
            bannerText.fontSize = 42f;
            bannerText.fontStyle = FontStyles.Bold;
            bannerText.alignment = TextAlignmentOptions.Center;
            bannerText.color = newHighScoreColor;

            if (winScoreText != null)
                bannerText.font = winScoreText.font;

            newHighScoreBanner = bannerObj;
        }

        newHighScoreBanner.SetActive(true);
        StartCoroutine(AnimationUtilities.PunchScale(newHighScoreBanner.transform, 1.3f, 0.3f));
        AudioManager.Instance?.PlayButtonClick(); // Celebratory sound
    }

    /// <summary>
    /// Hide all breakdown elements (called before animation starts).
    /// </summary>
    private void HideBreakdownElements()
    {
        // Hide entire rows (parent of label/value pairs)
        if (scoreLabelText != null) scoreLabelText.transform.parent.gameObject.SetActive(false);
        if (sessionTimeLabelText != null) sessionTimeLabelText.transform.parent.gameObject.SetActive(false);
        if (timeBonusLabelText != null) timeBonusLabelText.transform.parent.gameObject.SetActive(false);
        if (hotStreakLabelText != null) hotStreakLabelText.transform.parent.gameObject.SetActive(false);
        if (breakdownDivider != null) breakdownDivider.gameObject.SetActive(false);
        if (totalLabelText != null) totalLabelText.transform.parent.gameObject.SetActive(false);
        if (newHighScoreBanner != null) newHighScoreBanner.SetActive(false);
    }

    /// <summary>
    /// Create score breakdown UI elements at runtime if not assigned in inspector.
    /// </summary>
    private void EnsureBreakdownElementsExist()
    {
        if (winScreen == null) return;

        Transform parent = winScreen.transform;

        // Find or create container for breakdown text
        Transform breakdownContainer = parent.Find("BreakdownContainer");
        if (breakdownContainer == null)
        {
            GameObject containerObj = new GameObject("BreakdownContainer");
            containerObj.transform.SetParent(parent, false);
            RectTransform containerRT = containerObj.AddComponent<RectTransform>();
            containerRT.anchorMin = new Vector2(0.1f, 0.25f);
            containerRT.anchorMax = new Vector2(0.9f, 0.7f);
            containerRT.offsetMin = Vector2.zero;
            containerRT.offsetMax = Vector2.zero;

            // Add vertical layout group
            var vlg = containerObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(10, 10, 10, 10);

            breakdownContainer = containerObj.transform;
        }

        // Create breakdown rows with two-column layout (label left, value right)
        if (scoreLabelText == null || scoreValueText == null)
            (scoreLabelText, scoreValueText) = CreateBreakdownRow(breakdownContainer, "ScoreRow", "Score", "0 BP");

        if (sessionTimeLabelText == null || sessionTimeValueText == null)
            (sessionTimeLabelText, sessionTimeValueText) = CreateBreakdownRow(breakdownContainer, "SessionTimeRow", "Session Time", "00:00");

        if (timeBonusLabelText == null || timeBonusValueText == null)
            (timeBonusLabelText, timeBonusValueText) = CreateBreakdownRow(breakdownContainer, "TimeBonusRow", "Time Bonus", "+ 0 BP");

        if (hotStreakLabelText == null || hotStreakValueText == null)
            (hotStreakLabelText, hotStreakValueText) = CreateBreakdownRow(breakdownContainer, "HotStreakRow", "Hot Streak", "x1.0");

        if (breakdownDivider == null)
            breakdownDivider = CreateDivider(breakdownContainer, "Divider");

        if (totalLabelText == null || totalValueText == null)
            (totalLabelText, totalValueText) = CreateBreakdownRow(breakdownContainer, "TotalRow", "TOTAL", "0 BP", true);
    }

    private (TMP_Text label, TMP_Text value) CreateBreakdownRow(Transform parent, string name, string labelText, string valueText, bool isTotal = false)
    {
        // Create row container with horizontal layout
        GameObject rowObj = new GameObject(name);
        rowObj.transform.SetParent(parent, false);

        RectTransform rowRT = rowObj.AddComponent<RectTransform>();
        rowRT.sizeDelta = new Vector2(0, isTotal ? 55f : 40f);

        var hlg = rowObj.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.spacing = 10f;

        // Create label (left-aligned)
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(rowObj.transform, false);
        RectTransform labelRT = labelObj.AddComponent<RectTransform>();

        TMP_Text label = labelObj.AddComponent<TextMeshProUGUI>();
        label.text = labelText;
        label.fontSize = isTotal ? 40f : 32f;
        label.fontStyle = isTotal ? FontStyles.Bold : FontStyles.Normal;
        label.alignment = TextAlignmentOptions.Left;
        label.color = isTotal ? new Color(1f, 0.9f, 0.3f) : Color.white;

        // Create value (right-aligned)
        GameObject valueObj = new GameObject("Value");
        valueObj.transform.SetParent(rowObj.transform, false);
        RectTransform valueRT = valueObj.AddComponent<RectTransform>();

        TMP_Text value = valueObj.AddComponent<TextMeshProUGUI>();
        value.text = valueText;
        value.fontSize = isTotal ? 40f : 32f;
        value.fontStyle = isTotal ? FontStyles.Bold : FontStyles.Normal;
        value.alignment = TextAlignmentOptions.Right;
        value.color = isTotal ? new Color(1f, 0.9f, 0.3f) : Color.white;

        // Try to use the same font as winScoreText
        if (winScoreText != null)
        {
            label.font = winScoreText.font;
            value.font = winScoreText.font;
        }

        rowObj.SetActive(false);
        return (label, value);
    }

    private Image CreateDivider(Transform parent, string name)
    {
        GameObject dividerObj = new GameObject(name);
        dividerObj.transform.SetParent(parent, false);

        RectTransform rt = dividerObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 6f);

        Image img = dividerObj.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.8f);

        dividerObj.SetActive(false);
        return img;
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
    /// Get a color from a 3-point gradient (0->mid at 50%, mid->end at 100%).
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
    /// Continue button clicked on results screen - restarts the game.
    /// </summary>
    public void OnContinueButtonClicked()
    {
        AudioManager.Instance?.PlayButtonClick();

        // Calculate total BP earned (same formula as breakdown)
        if (gameManager != null)
        {
            int baseScore = gameManager.Score;
            float timeRemaining = gameManager.TimeRemaining;
            float maxMultiplier = gameManager.MaxMultiplierReached;

            int timeBonus = Mathf.RoundToInt(timeRemaining * timeBonusPerSecond);
            int subtotal = baseScore + timeBonus;
            int totalBP = Mathf.RoundToInt(subtotal * maxMultiplier);

            // Add BP to RunManager
            RunManager.Instance?.AddBP(totalBP);

            Debug.Log($"<color=green>[UIManager] Play Again pressed - Added {totalBP} BP to run total</color>");
        }

        // Hide win screen immediately
        SetActiveIfNotNull(winScreen, false);
        HideBreakdownElements();

        // Restart the game with countdown
        SceneFlowManager.Instance?.RestartWithCountdown();
    }

    /// <summary>
    /// Main Menu button clicked on win screen.
    /// </summary>
    public void OnMainMenuButtonClicked()
    {
        AudioManager.Instance?.PlayButtonClick();

        // End the current run
        RunManager.Instance?.EndRun();

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
        SetActiveIfNotNull(finishTextObject, false);

        // Hide breakdown elements
        HideBreakdownElements();

        // Clean up any active effects
        StopPulse(ref timerPulseCoroutine, timerText?.transform);
        StopPulse(ref multiplierPulseCoroutine, multiplierValueText?.transform);
        StopTimeWarningSound();
        DeactivateHotStreak();
        CleanupHotStreakMode();

        // Reset multiplier display to x1.00 (keep panel visible)
        SetActiveIfNotNull(multiplierPanel, true);
        if (multiplierValueText != null)
        {
            multiplierValueText.text = "x1.00";
            multiplierValueText.transform.localScale = Vector3.one;
            multiplierValueText.color = multiplierTextCoolColor;
        }
        lastMultiplierValue = 1f;
    }

    #endregion

    #region Score Progress Bar VFX

    private Coroutine progressBarBounceCoroutine;
    private float currentBounceScale = 1f;
    private float targetBounceScale = 1f;

    /// <summary>
    /// Get the RectTransform of the score progress slider for VFX targeting.
    /// </summary>
    public RectTransform GetScoreProgressSlider()
    {
        return scoreProgressSlider?.GetComponent<RectTransform>();
    }

    /// <summary>
    /// Add to the progress bar's scale. Each particle impact adds a small bump.
    /// </summary>
    /// <param name="bounceScale">Scale to add (e.g., 1.02 adds 2% size)</param>
    /// <param name="duration">Not used - kept for compatibility</param>
    public void BounceProgressBar(float bounceScale, float duration)
    {
        if (scoreProgressSlider == null) return;

        // Accumulate scale instead of interrupting
        float scaleAdd = (bounceScale - 1f) * 0.5f; // Subtler effect
        targetBounceScale = Mathf.Min(targetBounceScale + scaleAdd, 1.25f); // Cap at 25% increase

        // Start the smooth bounce coroutine if not running
        if (progressBarBounceCoroutine == null)
        {
            progressBarBounceCoroutine = StartCoroutine(SmoothBounceCoroutine());
        }
    }

    private IEnumerator SmoothBounceCoroutine()
    {
        Transform sliderTransform = scoreProgressSlider.transform;

        while (true)
        {
            // Smoothly approach target scale
            currentBounceScale = Mathf.Lerp(currentBounceScale, targetBounceScale, Time.deltaTime * 12f);
            sliderTransform.localScale = Vector3.one * currentBounceScale;

            // Decay target back toward 1
            targetBounceScale = Mathf.Lerp(targetBounceScale, 1f, Time.deltaTime * 4f);

            // Exit when settled back to normal
            if (Mathf.Abs(currentBounceScale - 1f) < 0.001f && Mathf.Abs(targetBounceScale - 1f) < 0.001f)
            {
                sliderTransform.localScale = Vector3.one;
                currentBounceScale = 1f;
                targetBounceScale = 1f;
                progressBarBounceCoroutine = null;
                yield break;
            }

            yield return null;
        }
    }

    /// <summary>
    /// Get the amount of pending score available for particle distribution.
    /// </summary>
    public int GetPendingScore()
    {
        return pendingScoreToAdd;
    }

    /// <summary>
    /// Called by TenExplosionVFX when a particle arrives.
    /// Adds a portion of pending score to the displayed score.
    /// </summary>
    /// <param name="pointsPerParticle">How many points this particle represents</param>
    public void OnParticleScoreArrived(int pointsPerParticle)
    {
        if (pendingScoreToAdd <= 0) return;

        // Claim points from pending
        int pointsToAdd = Mathf.Min(pointsPerParticle, pendingScoreToAdd);
        pendingScoreToAdd -= pointsToAdd;
        displayedScore += pointsToAdd;

        // Update the score text with punch animation
        if (scoreText != null)
        {
            scoreText.text = displayedScore.ToString();
            StartCoroutine(AnimationUtilities.PunchScale(scoreText.transform, 1.15f, 0.08f));
        }

        // Update progress bar
        if (scoreProgressSlider != null)
            scoreProgressSlider.value = displayedScore;

        // Update progress bar color - just use the full color
        if (scoreProgressFillImage != null)
        {
            scoreProgressFillImage.color = scoreProgressFullColor;
        }

        // Flash the glow
        FlashProgressGlow();
    }

    /// <summary>
    /// Flash the progress bar glow effect.
    /// </summary>
    public void FlashProgressGlow()
    {
        if (scoreProgressGlow == null) return;

        // Stop any existing glow animation
        if (glowCoroutine != null)
        {
            StopCoroutine(glowCoroutine);
        }

        glowCoroutine = StartCoroutine(FlashGlowCoroutine());
    }

    private Coroutine glowCoroutine;

    private IEnumerator FlashGlowCoroutine()
    {
        scoreProgressGlow.gameObject.SetActive(true);
        scoreProgressGlow.color = scoreGlowColor;

        float elapsed = 0f;
        Color startColor = scoreGlowColor;
        Color endColor = new Color(scoreGlowColor.r, scoreGlowColor.g, scoreGlowColor.b, 0f);

        while (elapsed < glowFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / glowFadeDuration;
            scoreProgressGlow.color = Color.Lerp(startColor, endColor, t);
            yield return null;
        }

        scoreProgressGlow.color = endColor;
        scoreProgressGlow.gameObject.SetActive(false);
        glowCoroutine = null;
    }

    /// <summary>
    /// Flush any remaining pending score immediately (fallback).
    /// </summary>
    public void FlushPendingScore()
    {
        if (pendingScoreToAdd > 0)
        {
            displayedScore += pendingScoreToAdd;
            pendingScoreToAdd = 0;
            UpdateScoreDisplay(displayedScore);
        }
    }

    #endregion

    #region Hot Streak Effect (Fire Particles)

    private void ActivateHotStreak(float multiplier)
    {
        if (!enableHotStreak || hotStreakEffect == null) return;

        hotStreakEffect.Activate(multiplier);
        hotStreakActive = true;

        Debug.Log($"<color=orange>Hot Streak Activated!</color> x{multiplier:F2}");
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
}
