using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

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
    #pragma warning disable CS0414
    [SerializeField] private float timeBonusPerSecond = 1f;
    #pragma warning restore CS0414

    [Header("High Score")]
    [SerializeField] private TMP_Text highScoreText;
    [SerializeField] private GameObject newHighScoreBanner;
    [SerializeField] private Color newHighScoreColor = new Color(1f, 0.85f, 0.1f);

    [Header("Star Rating")]
    [SerializeField] private Color starFilledColor = new Color(1f, 0.85f, 0.1f);   // Gold
    [SerializeField] private Color starEmptyColor = new Color(0.35f, 0.35f, 0.35f, 0.4f); // Dim grey
    [SerializeField] private float starSize = 56f;
    [SerializeField] private float starRevealDelay = 0.3f;
    private GameObject starContainer;
    private GameObject resultsTitleObj;
    private GameObject performanceMessageObj;
    private List<GameObject> zenStatLineObjects = new List<GameObject>();
    private TMP_Text zenLockedTileCounterText;
    private GameObject zenLockedTileCounterObj;
    private List<Image> starImages = new List<Image>();

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
    private Coroutine hotStreakRainbowCoroutine;
    private bool isTimeWarningPlaying = false;
    private bool isSubscribed = false;
    private bool runManagerSubscribed = false;
    private bool hotStreakActive = false;
    private bool isInHotStreakMode = false;

    // Pause Menu UI elements (created programmatically)
    private GameObject pauseHamburgerButton;
    private GameObject pauseOverlay;
    private bool pauseMenuCreated = false;

    // Hot Streak UI elements (created via code)
    private GameObject hotStreakTextObject;
    private TMPro.TMP_Text hotStreakText;

    // Hot Streak countdown bar (avatar region)
    private GameObject hotStreakCountdownBarObj;
    private Image hotStreakCountdownFillImage;
    private float hotStreakCountdownMax;

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

        // Late-bind RunManager subscription if it wasn't available during Awake
        if (isSubscribed && !runManagerSubscribed && RunManager.Instance != null)
        {
            RunManager.Instance.OnRoundChanged += HandleRoundChanged;
            RunManager.Instance.OnRunStarted += HandleRunStarted;
            runManagerSubscribed = true;
            Debug.Log("UIManager: Late-bound RunManager events in Start().");
        }

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
        gameManager.OnHotStreakTimerChanged += HandleHotStreakTimerChanged;

        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (gridManager != null)
            gridManager.OnGridUnsolvable += HandleGridUnsolvable;

        // Subscribe to RunManager events
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRoundChanged += HandleRoundChanged;
            RunManager.Instance.OnRunStarted += HandleRunStarted;
            runManagerSubscribed = true;
        }
        else
        {
            Debug.LogWarning("UIManager: RunManager not found during event subscription — will retry in Update.");
            runManagerSubscribed = false;
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
            gameManager.OnHotStreakTimerChanged -= HandleHotStreakTimerChanged;
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
        // Zen mode: hide multiplier panel and label entirely (flat scoring, no multiplier)
        bool isZen = gameManager != null && gameManager.CurrentMode == GameManager.GameMode.Zen;
        SetActiveIfNotNull(multiplierPanel, !isZen);
        // Also hide the "Multiplier" label (sibling of multiplierPanel on StatsPanelBG)
        if (isZen && multiplierPanel != null && multiplierPanel.transform.parent != null)
        {
            Transform label = multiplierPanel.transform.parent.Find("Multiplier");
            if (label != null) label.gameObject.SetActive(false);
        }

        // Initialize multiplier display to x1.00 (Arcade only)
        if (!isZen && multiplierValueText != null)
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

            // Arcade: initialize multiplier slider (Zen has no multiplier panel)
            if (!isZen && multiplierSlider != null)
            {
                multiplierSlider.maxValue = gameManager.MultiplierBarMax;
                multiplierSlider.value = 0f;
            }
        }

        UpdateScoreDisplay(0);
        // Use mode-appropriate timer display on init
        float initDuration = gameManager?.GameDuration ?? 60f;
        if (gameManager != null && gameManager.CurrentMode == GameManager.GameMode.Zen)
            UpdateZenTimerDisplay(initDuration);
        else
            UpdateTimerDisplay(initDuration);

        // Create/show locked tile counter for Zen mode
        CreateZenLockedTileCounter();

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

        // Create pause menu UI (hamburger button + overlay)
        CreatePauseMenuUI();
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

    #region Pause Menu

    /// <summary>
    /// Create the pause menu UI: hamburger button (top-left) + full-screen pause overlay.
    /// Built entirely in code — no Inspector wiring needed.
    /// </summary>
    private void CreatePauseMenuUI()
    {
        if (pauseMenuCreated) return;
        pauseMenuCreated = true;

        // === PAUSE HAMBURGER BUTTON (top-left corner) ===
        // Parent OUTSIDE GamePanel so the button doesn't get overdrawn by
        // CharacterPanel / StatsPanelBG / other game-panel siblings. We attach
        // it to the SafeAreaContainer (Canvas/SafeAreaContainer) — so it respects
        // notches but renders on top of the gameplay layer regardless of sibling
        // order inside GamePanel.
        Canvas rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        Transform safeAreaParent = rootCanvas != null
            ? rootCanvas.transform.Find("SafeAreaContainer")
            : null;
        // Fall back to root canvas, then to UIManager itself if neither exists yet.
        Transform pauseParent = safeAreaParent != null
            ? safeAreaParent
            : (rootCanvas != null ? rootCanvas.transform : transform);

        pauseHamburgerButton = new GameObject("PauseHamburgerButton");
        pauseHamburgerButton.transform.SetParent(pauseParent, false);

        RectTransform hambRT = pauseHamburgerButton.AddComponent<RectTransform>();
        hambRT.anchorMin = new Vector2(0f, 1f); // Top-left of safe area
        hambRT.anchorMax = new Vector2(0f, 1f);
        hambRT.pivot = new Vector2(0f, 1f);
        hambRT.anchoredPosition = new Vector2(20f, -20f);
        hambRT.sizeDelta = new Vector2(96f, 96f);

        // Soft outer glow (slightly larger than the button itself, behind everything)
        GameObject hambGlow = new GameObject("PauseGlow");
        hambGlow.transform.SetParent(pauseHamburgerButton.transform, false);
        RectTransform glowRT = hambGlow.AddComponent<RectTransform>();
        glowRT.anchorMin = Vector2.zero;
        glowRT.anchorMax = Vector2.one;
        glowRT.offsetMin = new Vector2(-18f, -18f);
        glowRT.offsetMax = new Vector2(18f, 18f);
        Image glowImg = hambGlow.AddComponent<Image>();
        GlowTextureGenerator.ApplyCircularGlow(glowImg, 96, 1.8f);
        glowImg.color = new Color(0.95f, 0.78f, 0.30f, 0.55f); // warm gold glow
        glowImg.raycastTarget = false;

        // Circular button background (target graphic for the Button)
        Image hambBg = pauseHamburgerButton.AddComponent<Image>();
        hambBg.sprite = GlowTextureGenerator.GetCircularGlowSprite(96, 6f); // sharper edge
        hambBg.type = Image.Type.Simple;
        hambBg.preserveAspect = true;
        hambBg.color = new Color(0.12f, 0.12f, 0.18f, 0.92f);

        // Inner ring accent — slightly inset, brighter color for clear button edge
        GameObject hambRing = new GameObject("PauseRing");
        hambRing.transform.SetParent(pauseHamburgerButton.transform, false);
        RectTransform ringRT = hambRing.AddComponent<RectTransform>();
        ringRT.anchorMin = Vector2.zero;
        ringRT.anchorMax = Vector2.one;
        ringRT.offsetMin = new Vector2(6f, 6f);
        ringRT.offsetMax = new Vector2(-6f, -6f);
        Image ringImg = hambRing.AddComponent<Image>();
        ringImg.sprite = GlowTextureGenerator.GetCircularGlowSprite(96, 8f); // very sharp ring
        ringImg.type = Image.Type.Simple;
        ringImg.preserveAspect = true;
        ringImg.color = new Color(0.95f, 0.78f, 0.30f, 0.85f); // matches glow → ring of light
        ringImg.raycastTarget = false;

        // Inner fill — sits just inside the ring, gives the icon a clean dark backing
        GameObject hambFill = new GameObject("PauseFill");
        hambFill.transform.SetParent(pauseHamburgerButton.transform, false);
        RectTransform fillRT = hambFill.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = new Vector2(10f, 10f);
        fillRT.offsetMax = new Vector2(-10f, -10f);
        Image fillImg = hambFill.AddComponent<Image>();
        fillImg.sprite = GlowTextureGenerator.GetCircularGlowSprite(96, 6f);
        fillImg.type = Image.Type.Simple;
        fillImg.preserveAspect = true;
        fillImg.color = new Color(0.08f, 0.08f, 0.12f, 0.96f);
        fillImg.raycastTarget = false;

        // Button component — uses the outer bg as targetGraphic so the whole disc is tappable
        Button hambButton = pauseHamburgerButton.AddComponent<Button>();
        hambButton.targetGraphic = hambBg;
        ColorBlock hambColors = hambButton.colors;
        hambColors.normalColor = new Color(1f, 1f, 1f, 1f);
        hambColors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        hambColors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        hambColors.colorMultiplier = 1f;
        hambButton.colors = hambColors;
        hambButton.onClick.AddListener(() => {
            SceneFlowManager.Instance?.OnPausePressed();
        });

        // Pause icon text (two bars) — sits above the fill so it reads on the dark interior
        GameObject hambTextObj = new GameObject("HamburgerIcon");
        hambTextObj.transform.SetParent(pauseHamburgerButton.transform, false);
        RectTransform hambTextRT = hambTextObj.AddComponent<RectTransform>();
        hambTextRT.anchorMin = Vector2.zero;
        hambTextRT.anchorMax = Vector2.one;
        hambTextRT.offsetMin = Vector2.zero;
        hambTextRT.offsetMax = Vector2.zero;

        TextMeshProUGUI hambText = hambTextObj.AddComponent<TextMeshProUGUI>();
        hambText.text = "| |"; // Pause icon (two bars)
        hambText.fontSize = 48f;
        hambText.fontStyle = FontStyles.Bold;
        hambText.alignment = TextAlignmentOptions.Center;
        hambText.color = new Color(1f, 0.92f, 0.6f, 1f); // warm cream — pops on dark fill
        hambText.raycastTarget = false;
        if (scoreText != null) hambText.font = scoreText.font;

        pauseHamburgerButton.SetActive(false); // Hidden until game starts

        // === PAUSE OVERLAY (full-screen, hidden initially) ===
        // Parent to the root Canvas so it renders ON TOP of everything (grid, avatar, etc.)
        // rootCanvas was already resolved above for the hamburger button — reuse it.
        Transform overlayParent = rootCanvas != null ? rootCanvas.transform : transform;

        pauseOverlay = new GameObject("PauseOverlay");
        pauseOverlay.transform.SetParent(overlayParent, false);

        RectTransform overlayRT = pauseOverlay.AddComponent<RectTransform>();
        overlayRT.anchorMin = Vector2.zero;
        overlayRT.anchorMax = Vector2.one;
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;

        // Dark semi-transparent background
        Image overlayBg = pauseOverlay.AddComponent<Image>();
        overlayBg.color = new Color(0.02f, 0.02f, 0.06f, 0.88f);

        // Content container (centered column)
        GameObject contentContainer = new GameObject("PauseContent");
        contentContainer.transform.SetParent(pauseOverlay.transform, false);
        RectTransform contentRT = contentContainer.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0.5f, 0.5f);
        contentRT.anchorMax = new Vector2(0.5f, 0.5f);
        contentRT.sizeDelta = new Vector2(600f, 500f);
        contentRT.anchoredPosition = Vector2.zero;

        // "PAUSED" title
        GameObject pauseTitle = new GameObject("PauseTitle");
        pauseTitle.transform.SetParent(contentContainer.transform, false);
        RectTransform titleRT = pauseTitle.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 1f);
        titleRT.anchorMax = new Vector2(0.5f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, 0f);
        titleRT.sizeDelta = new Vector2(500f, 80f);

        TextMeshProUGUI titleText = pauseTitle.AddComponent<TextMeshProUGUI>();
        titleText.text = "PAUSED";
        titleText.fontSize = 64f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = UIStyleGuide.ColorTextPrimary;
        if (scoreText != null) titleText.font = scoreText.font;

        // Button layout: Resume, Options, Main Menu (stacked vertically)
        float buttonWidth = 480f;
        float buttonHeight = UIStyleGuide.ButtonHeight;
        float buttonSpacing = 24f;
        float startY = -120f; // Below title

        CreatePauseButton(contentContainer.transform, "ResumeButton", "Resume",
            UIStyleGuide.ColorButtonPrimary, startY, buttonWidth, buttonHeight,
            () => SceneFlowManager.Instance?.OnResumePressed());

        CreatePauseButton(contentContainer.transform, "OptionsButton", "Options",
            UIStyleGuide.ColorButtonSecondary, startY - (buttonHeight + buttonSpacing), buttonWidth, buttonHeight,
            () => SceneFlowManager.Instance?.OnPauseOptionsPressed());

        CreatePauseButton(contentContainer.transform, "MainMenuButton", "Save & Quit",
            UIStyleGuide.ColorButtonDanger, startY - 2 * (buttonHeight + buttonSpacing), buttonWidth, buttonHeight,
            () => SceneFlowManager.Instance?.OnPauseMainMenuPressed());

        pauseOverlay.SetActive(false);
    }

    /// <summary>
    /// Helper: Create a styled button for the pause menu.
    /// </summary>
    private void CreatePauseButton(Transform parent, string name, string label, Color bgColor,
        float yPos, float width, float height, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObj = new GameObject(name);
        buttonObj.transform.SetParent(parent, false);

        RectTransform rt = buttonObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, yPos);
        rt.sizeDelta = new Vector2(width, height);

        Image bg = buttonObj.AddComponent<Image>();
        bg.color = bgColor;

        Button button = buttonObj.AddComponent<Button>();
        button.targetGraphic = bg;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        button.colors = colors;
        button.onClick.AddListener(onClick);

        // Button text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        RectTransform textRT = textObj.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = UIStyleGuide.FontSizeButton;
        text.alignment = TextAlignmentOptions.Center;
        text.color = UIStyleGuide.ColorTextPrimary;
        if (scoreText != null) text.font = scoreText.font;
    }

    /// <summary>
    /// Show the pause menu overlay. Called by SceneFlowManager.OnPausePressed().
    /// </summary>
    public void ShowPauseMenu()
    {
        if (pauseOverlay != null)
        {
            pauseOverlay.SetActive(true);
            pauseOverlay.transform.SetAsLastSibling(); // Render on top
        }
        if (pauseHamburgerButton != null)
            pauseHamburgerButton.SetActive(false); // Hide hamburger while paused
    }

    /// <summary>
    /// Hide the pause menu overlay. Called by SceneFlowManager.OnResumePressed().
    /// </summary>
    public void HidePauseMenu()
    {
        if (pauseOverlay != null)
            pauseOverlay.SetActive(false);
        if (pauseHamburgerButton != null)
            pauseHamburgerButton.SetActive(true); // Show hamburger again
    }

    /// <summary>
    /// Show the hamburger button (call when game starts).
    /// </summary>
    public void ShowPauseHamburger()
    {
        if (pauseHamburgerButton != null)
        {
            pauseHamburgerButton.SetActive(true);
            pauseHamburgerButton.transform.SetAsLastSibling(); // Render on top of other UI
        }
    }

    /// <summary>
    /// Hide the hamburger button (call when game ends / returns to menu).
    /// </summary>
    public void HidePauseHamburger()
    {
        if (pauseHamburgerButton != null)
            pauseHamburgerButton.SetActive(false);
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
        // Both modes show the countdown timer
        // Zen uses calmer styling (danger zone only in last 30s)
        if (gameManager != null && gameManager.CurrentMode == GameManager.GameMode.Zen)
        {
            UpdateZenTimerDisplay(timeRemaining);
            return;
        }
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
        HidePauseHamburger();
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

        // Show the pause hamburger button when a game run starts
        ShowPauseHamburger();
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

        // Update locked tile counter for Zen mode
        UpdateZenLockedTileCounter();
    }

    /// <summary>
    /// Create the locked tile counter (◆ count) for Zen mode HUD.
    /// Positioned below the score text. Only visible in Zen mode.
    /// </summary>
    private void CreateZenLockedTileCounter()
    {
        // Destroy old counter if exists
        if (zenLockedTileCounterObj != null)
        {
            Destroy(zenLockedTileCounterObj);
            zenLockedTileCounterObj = null;
            zenLockedTileCounterText = null;
        }

        bool isZen = gameManager != null && gameManager.CurrentMode == GameManager.GameMode.Zen;
        if (!isZen || scoreText == null) return;

        zenLockedTileCounterObj = new GameObject("ZenLockedTileCounter");
        zenLockedTileCounterObj.transform.SetParent(scoreText.transform.parent, false);

        RectTransform rt = zenLockedTileCounterObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, -35f); // Below score text
        rt.sizeDelta = new Vector2(200f, 30f);

        zenLockedTileCounterText = zenLockedTileCounterObj.AddComponent<TextMeshProUGUI>();
        zenLockedTileCounterText.text = "◆ 0";
        zenLockedTileCounterText.fontSize = 24f;
        zenLockedTileCounterText.alignment = TextAlignmentOptions.Center;
        zenLockedTileCounterText.color = new Color(0.94f, 0.82f, 0.38f, 0.85f); // Warm gold, subtle
        zenLockedTileCounterText.font = scoreText.font;
    }

    /// <summary>
    /// Update the locked tile counter text to reflect current count.
    /// </summary>
    private void UpdateZenLockedTileCounter()
    {
        if (zenLockedTileCounterText == null || gameManager == null) return;
        zenLockedTileCounterText.text = $"◆ {gameManager.ZenLockedTileCount}";
    }

    private void UpdateTimerDisplay(float timeRemaining)
    {
        // Re-enable timer elements in case they were hidden by Zen mode
        if (timerSlider != null && !timerSlider.gameObject.activeSelf)
            timerSlider.gameObject.SetActive(true);
        if (timerFillImage != null && !timerFillImage.gameObject.activeSelf)
            timerFillImage.gameObject.SetActive(true);

        // Ensure slider maxValue matches current mode duration (may differ after switching modes)
        if (timerSlider != null && gameManager != null && timerSlider.maxValue != gameManager.GameDuration)
            timerSlider.maxValue = gameManager.GameDuration;

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

    /// <summary>
    /// Zen timer: shows countdown like Arcade but with calmer styling.
    /// Danger zone only triggers in last 30s (vs 10s Arcade).
    /// Formats as M:SS for the longer 5-minute duration.
    /// </summary>
    private void UpdateZenTimerDisplay(float timeRemaining)
    {
        // Re-enable timer elements in case they were previously hidden
        if (timerSlider != null && !timerSlider.gameObject.activeSelf)
            timerSlider.gameObject.SetActive(true);
        if (timerFillImage != null && !timerFillImage.gameObject.activeSelf)
            timerFillImage.gameObject.SetActive(true);

        // Ensure slider maxValue matches Zen duration (InitializeGameUI sets it to Arcade's 60s)
        if (timerSlider != null && gameManager != null && timerSlider.maxValue != gameManager.GameDuration)
            timerSlider.maxValue = gameManager.GameDuration;

        int totalSeconds = Mathf.CeilToInt(timeRemaining);
        int minutes = totalSeconds / 60;
        int secs = totalSeconds % 60;
        string timeStr = minutes > 0 ? $"{minutes}:{secs:D2}" : secs.ToString();

        // Zen danger zone: last 30s warning, last 10s danger
        Color stateColor;
        if (timeRemaining <= 10f)
        {
            stateColor = timerDangerColor;
            StartPulse(ref timerPulseCoroutine, timerText?.transform, 1f, 1.15f, 8f);
            StartTimeWarningSound();
        }
        else if (timeRemaining <= 30f)
        {
            stateColor = timerWarningColor;
            StopPulse(ref timerPulseCoroutine, timerText?.transform);
            StopTimeWarningSound();
        }
        else
        {
            stateColor = new Color(0.5f, 0.8f, 1f); // Calm blue for Zen
            StopPulse(ref timerPulseCoroutine, timerText?.transform);
            StopTimeWarningSound();
        }

        // Update text
        if (timerText != null)
        {
            timerText.text = timeStr;
            if (useTimerTextColorChange)
                timerText.color = stateColor;
        }

        if (timerShadowText != null)
            timerShadowText.text = timeStr;

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

    private void UpdateMultiplierBar(bool active, float multiplier, float barOrTimer)
    {
        if (multiplierPanel == null) return;

        // Zen mode: multiplier panel is hidden, skip all updates
        if (gameManager != null && gameManager.CurrentMode == GameManager.GameMode.Zen) return;

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
                multiplierSlider.value = barOrTimer;

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

            // Timer text: show bar value (Arcade only — Zen early-returns above)
            if (multiplierTimerText != null)
            {
                multiplierTimerText.text = $"{Mathf.RoundToInt(barOrTimer)}";
            }

            // Fill color: cool→hot based on bar fill percentage (Arcade only)
            if (multiplierFillImage != null && !enableHotStreak)
            {
                float fillPct = barOrTimer / (gameManager?.MultiplierBarMax ?? 100f);
                multiplierFillImage.color = Color.Lerp(multiplierLowColor, multiplierFullColor, fillPct);
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

            // Reset slider to 0 (bar empty) — Arcade only, Zen early-returns above
            if (multiplierSlider != null)
            {
                multiplierSlider.value = 0f;
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

        // Start rainbow bar effect in Arcade mode
        if (gameManager != null && gameManager.CurrentMode == GameManager.GameMode.Arcade)
        {
            StartRainbowBar();
            CreateHotStreakCountdownBar();
        }

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

        // Stop rainbow bar effect
        StopRainbowBar();

        // Hide hot streak background
        if (hotStreakBackground != null)
            hotStreakBackground.SetActive(false);

        // Hide hot streak text (in case it's still showing)
        if (hotStreakTextObject != null)
            hotStreakTextObject.SetActive(false);

        // Resume mode-appropriate music after hot streak
        if (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.Zen)
            AudioManager.Instance?.PlayZenMusic();
        else
            AudioManager.Instance?.PlayGameMusic();

        // Destroy countdown bar
        DestroyHotStreakCountdownBar();
    }

    /// <summary>
    /// Creates a Hot Streak countdown bar in the avatar region (upper area).
    /// Shows remaining duration as a draining bar with rainbow fill.
    /// </summary>
    private void CreateHotStreakCountdownBar()
    {
        DestroyHotStreakCountdownBar();

        if (gameManager == null) return;
        hotStreakCountdownMax = gameManager.HotStreakDuration;

        // Find the AvatarManager's transform to position bar near it
        Transform avatarParent = AvatarManager.Instance != null
            ? AvatarManager.Instance.transform.parent
            : null;

        if (avatarParent == null)
        {
            Debug.LogWarning("UIManager: No avatar parent found for Hot Streak countdown bar.");
            return;
        }

        // Create container
        hotStreakCountdownBarObj = new GameObject("HotStreakCountdownBar");
        hotStreakCountdownBarObj.transform.SetParent(avatarParent, false);

        RectTransform barRect = hotStreakCountdownBarObj.AddComponent<RectTransform>();
        // Position below the avatar — anchor to bottom of avatar area
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0.5f, 1f);
        barRect.anchoredPosition = new Vector2(0f, -8f);
        barRect.sizeDelta = new Vector2(0f, 14f);

        // Background (dark)
        Image bgImage = hotStreakCountdownBarObj.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

        // Fill bar child
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(hotStreakCountdownBarObj.transform, false);

        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);
        fillRect.pivot = new Vector2(0f, 0.5f);

        hotStreakCountdownFillImage = fillObj.AddComponent<Image>();
        hotStreakCountdownFillImage.color = new Color(1f, 0.5f, 0.1f); // Orange start

        // Start full
        fillRect.anchorMax = new Vector2(1f, 1f);

        Debug.Log("<color=orange>UIManager: Hot Streak countdown bar created.</color>");
    }

    private void UpdateHotStreakCountdownBar(float remainingTime)
    {
        if (hotStreakCountdownFillImage == null || hotStreakCountdownMax <= 0f) return;

        float fill = Mathf.Clamp01(remainingTime / hotStreakCountdownMax);

        // Scale the fill by adjusting anchorMax.x
        RectTransform fillRect = hotStreakCountdownFillImage.GetComponent<RectTransform>();
        if (fillRect != null)
        {
            fillRect.anchorMax = new Vector2(fill, 1f);
        }

        // Rainbow color cycling (matches the main multiplier bar rainbow)
        float hue = (Time.time * 0.5f) % 1f;
        hotStreakCountdownFillImage.color = Color.HSVToRGB(hue, 0.85f, 1f);
    }

    private void DestroyHotStreakCountdownBar()
    {
        if (hotStreakCountdownBarObj != null)
        {
            Destroy(hotStreakCountdownBarObj);
            hotStreakCountdownBarObj = null;
            hotStreakCountdownFillImage = null;
        }
    }

    private void HandleHotStreakTimerChanged(float remainingTime)
    {
        UpdateHotStreakCountdownBar(remainingTime);
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

    /// <summary>
    /// Show a penalty popup (e.g. "-3s") in red. Reuses the score popup prefab.
    /// </summary>
    public void ShowPenaltyPopup(string message)
    {
        if (scorePopupPrefab == null || scorePopupParent == null) return;

        GameObject popup = Instantiate(scorePopupPrefab, scorePopupParent);
        TMP_Text popupText = popup.GetComponent<TMP_Text>();

        if (popupText != null)
        {
            popupText.text = message;
            popupText.color = new Color(0.9f, 0.2f, 0.2f, 1f); // Red
        }

        StartCoroutine(AnimateAndDestroyPopup(popup));
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

        // Set win screen background to dark navy (matching PopupWindow/tutorial style)
        if (winScreen != null)
        {
            Image winBg = winScreen.GetComponent<Image>();
            if (winBg != null)
                winBg.color = new Color(0.06f, 0.06f, 0.10f, 0.95f); // Dark navy, slight transparency
        }

        EnsureResultsButtonsActive();

        // Confetti celebration for arcade mode (Zen stays calm)
        bool isZen = gameManager != null && gameManager.CurrentMode == GameManager.GameMode.Zen;
        if (!isZen && winScreen != null)
        {
            SpawnConfetti(winScreen.transform, 60, 4f);
        }

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
        float sessionDuration = gameManager.SessionDuration;
        bool isZen = gameManager.CurrentMode == GameManager.GameMode.Zen;

        // Calculate breakdown — Zen has no session time bonus
        int sessionTimeBonus = isZen ? 0 : Mathf.RoundToInt(sessionDuration);
        int total = baseScore + sessionTimeBonus;

        // === PERSIST IMMEDIATELY (before animation, so button clicks can't interrupt) ===
        // Save BP high score (also sets IsNewHighScore flag)
        gameManager?.CheckAndSaveBPHighScore(total);
        // Bank earned BP to persistent totals (TotalBP + SpendableBP)
        RunManager.Instance?.BankBP(total);
        // Cache the high score flag since it needs to survive cleanup
        bool isNewHighScore = gameManager != null && gameManager.IsNewHighScore;

        // Hide all breakdown elements initially
        HideBreakdownElements();

        // Hide the legacy winScoreText since we're using breakdown
        if (winScoreText != null)
            winScoreText.text = "";

        // Small initial delay after win screen appears
        yield return new WaitForSeconds(0.3f);

        // Title header — mode-dependent
        string resultsTitle = isZen ? "Well Done." : "ROUND COMPLETE";

        Transform breakdownContainer = winScreen.transform.Find("BreakdownContainer");
        if (breakdownContainer != null && resultsTitleObj == null)
        {
            resultsTitleObj = new GameObject("ResultsTitle");
            resultsTitleObj.transform.SetParent(breakdownContainer, false);
            resultsTitleObj.transform.SetAsFirstSibling();

            RectTransform titleRT = resultsTitleObj.AddComponent<RectTransform>();
            titleRT.sizeDelta = new Vector2(0, 58f);

            TMP_Text titleText = resultsTitleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = resultsTitle;
            titleText.fontSize = 52f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(0.95f, 0.95f, 0.95f); // Off-white
            if (winScoreText != null) titleText.font = winScoreText.font;
        }
        if (resultsTitleObj != null)
        {
            resultsTitleObj.SetActive(true);
            resultsTitleObj.transform.localScale = Vector3.zero;
            yield return StartCoroutine(AnimationUtilities.PopIn(resultsTitleObj.transform, 1.1f, 0.25f, 0.05f));
        }

        yield return new WaitForSeconds(0.2f);

        // Line 1: Score - appears and counts up
        if (scoreLabelText != null && scoreValueText != null)
        {
            scoreLabelText.transform.parent.gameObject.SetActive(true);
            scoreLabelText.text = "Score";
            AudioManager.Instance?.PlayButtonClick();
            yield return StartCoroutine(AnimationUtilities.CountUp(scoreValueText, 0, baseScore, countUpDuration, "{0} BP"));
        }

        yield return new WaitForSeconds(breakdownLineDelay);

        if (isZen)
        {
            // Zen breakdown: show gameplay stats instead of session time bonus
            yield return StartCoroutine(ShowZenStatsBreakdown(breakdownContainer));

            // Divider line
            if (breakdownDivider != null)
                breakdownDivider.gameObject.SetActive(true);

            yield return new WaitForSeconds(breakdownLineDelay);

            // TOTAL — Zen has no session time bonus, just base score
            if (totalLabelText != null && totalValueText != null)
            {
                totalLabelText.transform.parent.gameObject.SetActive(true);
                totalLabelText.text = "TOTAL";
                AudioManager.Instance?.PlayButtonClick();
                yield return StartCoroutine(AnimationUtilities.CountUp(totalValueText, 0, baseScore, countUpDuration * 1.2f, "{0} BP"));
            }
        }
        else
        {
            // Arcade breakdown: session time bonus
            if (sessionTimeLabelText != null && sessionTimeValueText != null)
            {
                sessionTimeLabelText.transform.parent.gameObject.SetActive(true);
                sessionTimeLabelText.text = "Session Time";
                AudioManager.Instance?.PlayButtonClick();
                yield return StartCoroutine(AnimationUtilities.CountUp(sessionTimeValueText, 0, sessionTimeBonus, countUpDuration, "+ {0} BP"));
            }

            yield return new WaitForSeconds(breakdownLineDelay);

            // Divider line
            if (breakdownDivider != null)
                breakdownDivider.gameObject.SetActive(true);

            yield return new WaitForSeconds(breakdownLineDelay);

            // TOTAL — Arcade includes session time bonus
            if (totalLabelText != null && totalValueText != null)
            {
                totalLabelText.transform.parent.gameObject.SetActive(true);
                totalLabelText.text = "TOTAL";
                AudioManager.Instance?.PlayButtonClick();
                yield return StartCoroutine(AnimationUtilities.CountUp(totalValueText, 0, total, countUpDuration * 1.2f, "{0} BP"));
            }
        }

        // Star rating after total
        yield return new WaitForSeconds(0.4f);
        int starsEarned = gameManager.GetStarRating(total);
        yield return StartCoroutine(ShowStarRating(starsEarned));

        // Performance message based on stars
        if (breakdownContainer != null)
        {
            string perfMsg = GetPerformanceMessage(starsEarned);
            if (!string.IsNullOrEmpty(perfMsg))
            {
                yield return new WaitForSeconds(0.2f);
                ShowPerformanceMessage(breakdownContainer, perfMsg, starsEarned);
            }
        }

        // Show NEW HIGH SCORE banner if applicable (flag was set earlier before animation)
        if (isNewHighScore)
        {
            yield return new WaitForSeconds(0.3f);
            yield return StartCoroutine(ShowNewHighScoreCelebration());
        }
    }

    /// <summary>
    /// Show Zen-specific stats in the results breakdown: Highest Tile, Matches, Chains, Reshuffles Used.
    /// Creates stat lines programmatically inside the BreakdownContainer.
    /// </summary>
    private IEnumerator ShowZenStatsBreakdown(Transform breakdownContainer)
    {
        if (gameManager == null || breakdownContainer == null) yield break;

        int highestTile = gameManager.ZenHighestLockedValue;
        int matches = gameManager.ZenMatchCount;
        int chains = gameManager.ZenChainCount;
        int reshufflesUsed = gameManager.ZenMaxReshuffles - gameManager.ZenReshufflesRemaining;

        // Stat lines: label, value (displayed as string, not BP)
        var zenStats = new (string label, string value)[]
        {
            ("Highest Tile", highestTile > 0 ? highestTile.ToString() : "—"),
            ("Matches", matches.ToString()),
            ("Chains", chains.ToString()),
            ("Reshuffles", $"{reshufflesUsed} / {gameManager.ZenMaxReshuffles}")
        };

        foreach (var stat in zenStats)
        {
            yield return new WaitForSeconds(breakdownLineDelay * 0.6f);

            GameObject lineObj = CreateZenStatLine(breakdownContainer, stat.label, stat.value);
            zenStatLineObjects.Add(lineObj);

            lineObj.SetActive(true);
            lineObj.transform.localScale = Vector3.zero;
            AudioManager.Instance?.PlayButtonClick();
            yield return StartCoroutine(AnimationUtilities.PopIn(lineObj.transform, 1.05f, 0.15f, 0.03f));
        }

        yield return new WaitForSeconds(breakdownLineDelay);
    }

    /// <summary>
    /// Create a single label/value stat line for the Zen results breakdown.
    /// Matches the layout style of existing breakdown rows.
    /// </summary>
    private GameObject CreateZenStatLine(Transform parent, string label, string value)
    {
        GameObject lineObj = new GameObject($"ZenStat_{label}");
        lineObj.transform.SetParent(parent, false);

        RectTransform lineRT = lineObj.AddComponent<RectTransform>();
        lineRT.sizeDelta = new Vector2(0, 40f);

        // Use HorizontalLayoutGroup to space label and value
        var hlg = lineObj.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 20f;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        // Label (left-aligned)
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(lineObj.transform, false);
        RectTransform labelRT = labelObj.AddComponent<RectTransform>();
        labelRT.sizeDelta = new Vector2(0, 40f);

        TMP_Text labelText = labelObj.AddComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = 32f;
        labelText.alignment = TextAlignmentOptions.Left;
        labelText.color = new Color(0.7f, 0.7f, 0.75f); // Soft grey
        if (winScoreText != null) labelText.font = winScoreText.font;

        // Value (right-aligned)
        GameObject valueObj = new GameObject("Value");
        valueObj.transform.SetParent(lineObj.transform, false);
        RectTransform valueRT = valueObj.AddComponent<RectTransform>();
        valueRT.sizeDelta = new Vector2(0, 40f);

        TMP_Text valueText = valueObj.AddComponent<TextMeshProUGUI>();
        valueText.text = value;
        valueText.fontSize = 34f;
        valueText.fontStyle = FontStyles.Bold;
        valueText.alignment = TextAlignmentOptions.Right;
        valueText.color = new Color(0.95f, 0.95f, 0.95f); // Off-white
        if (winScoreText != null) valueText.font = winScoreText.font;

        lineObj.SetActive(false);
        return lineObj;
    }

    /// <summary>
    /// Get a fun performance message based on star count.
    /// </summary>
    private string GetPerformanceMessage(int stars)
    {
        switch (stars)
        {
            case 0: return "Keep practicing!";
            case 1: return "Nice work!";
            case 2: return "Impressive!";
            case 3: return "GENIUS!";
            default: return "";
        }
    }

    /// <summary>
    /// Show a performance text message below the stars with pop animation.
    /// </summary>
    private void ShowPerformanceMessage(Transform parent, string message, int stars)
    {
        if (performanceMessageObj != null) Destroy(performanceMessageObj);

        performanceMessageObj = new GameObject("PerformanceMessage");
        performanceMessageObj.transform.SetParent(parent, false);

        RectTransform msgRT = performanceMessageObj.AddComponent<RectTransform>();
        msgRT.sizeDelta = new Vector2(0, 40f);

        TMP_Text msgText = performanceMessageObj.AddComponent<TextMeshProUGUI>();
        msgText.text = message;
        msgText.fontSize = stars >= 3 ? 40f : 34f;
        msgText.fontStyle = stars >= 2 ? FontStyles.Bold : FontStyles.Normal;
        msgText.alignment = TextAlignmentOptions.Center;
        // Color: gold for 3 stars, light for 1-2, muted for 0
        msgText.color = stars >= 3 ? new Color(1f, 0.85f, 0.4f) :
                        stars >= 1 ? new Color(0.9f, 0.9f, 0.9f) :
                                     new Color(0.6f, 0.6f, 0.65f);
        if (winScoreText != null) msgText.font = winScoreText.font;

        performanceMessageObj.transform.localScale = Vector3.zero;
        StartCoroutine(AnimationUtilities.PopIn(performanceMessageObj.transform, 1.2f, 0.25f, 0.05f));
    }

    /// <summary>
    /// Animated NEW HIGH SCORE celebration with scale-up, glow pulse, and sound.
    /// </summary>
    private IEnumerator ShowNewHighScoreCelebration()
    {
        if (newHighScoreBanner == null)
        {
            // Create it dynamically if not assigned in inspector
            if (winScreen == null) yield break;

            Transform breakdownContainer = winScreen.transform.Find("BreakdownContainer");
            if (breakdownContainer == null) yield break;

            GameObject bannerObj = new GameObject("NewHighScoreBanner");
            bannerObj.transform.SetParent(breakdownContainer, false);

            RectTransform bannerRT = bannerObj.AddComponent<RectTransform>();
            bannerRT.sizeDelta = new Vector2(0, 60f);

            TMP_Text bannerText = bannerObj.AddComponent<TextMeshProUGUI>();
            bannerText.text = "NEW HIGH SCORE!";
            bannerText.fontSize = 48f;
            bannerText.fontStyle = FontStyles.Bold;
            bannerText.alignment = TextAlignmentOptions.Center;
            bannerText.color = newHighScoreColor;

            if (winScoreText != null)
                bannerText.font = winScoreText.font;

            newHighScoreBanner = bannerObj;
        }

        newHighScoreBanner.SetActive(true);
        newHighScoreBanner.transform.localScale = Vector3.zero;

        // Big pop-in with overshoot
        AudioManager.Instance?.PlayFinishSound();
        yield return StartCoroutine(AnimationUtilities.PopIn(newHighScoreBanner.transform, 1.4f, 0.3f, 0.08f));

        // Gold glow pulse (3 cycles)
        TMP_Text text = newHighScoreBanner.GetComponent<TextMeshProUGUI>();
        if (text != null)
        {
            Color brightGold = new Color(1f, 0.95f, 0.6f);
            for (int i = 0; i < 3; i++)
            {
                float elapsed = 0f;
                float pulseDuration = 0.4f;
                while (elapsed < pulseDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / pulseDuration;
                    float glow = Mathf.Sin(t * Mathf.PI); // 0 → 1 → 0
                    text.color = Color.Lerp(newHighScoreColor, brightGold, glow);
                    yield return null;
                }
            }
            text.color = newHighScoreColor; // Settle on gold
        }

        // Screen shake for emphasis
        GridVFX.Instance?.TriggerShake(2);
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
        if (starContainer != null) { Destroy(starContainer); starImages.Clear(); }
        if (resultsTitleObj != null) { Destroy(resultsTitleObj); resultsTitleObj = null; }
        if (performanceMessageObj != null) { Destroy(performanceMessageObj); performanceMessageObj = null; }
        foreach (var obj in zenStatLineObjects) { if (obj != null) Destroy(obj); }
        zenStatLineObjects.Clear();
    }

    /// <summary>
    /// Create and animate the star rating display (3 diamond-glow stars, revealed one at a time).
    /// Uses procedural diamond sprites from GlowTextureGenerator — no font/sprite assets needed.
    /// </summary>
    private IEnumerator ShowStarRating(int starsEarned)
    {
        if (winScreen == null) yield break;

        Transform breakdownContainer = winScreen.transform.Find("BreakdownContainer");
        if (breakdownContainer == null) yield break;

        // Clean up previous stars
        if (starContainer != null) Destroy(starContainer);
        starImages.Clear();

        // Create star container with horizontal layout and top padding for spacing
        starContainer = new GameObject("StarContainer");
        starContainer.transform.SetParent(breakdownContainer, false);

        RectTransform containerRT = starContainer.AddComponent<RectTransform>();
        containerRT.sizeDelta = new Vector2(0, starSize + 12f);

        HorizontalLayoutGroup hlg = starContainer.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 24f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.padding = new RectOffset(0, 0, 6, 0); // Top padding for breathing room

        // Create 3 star images using procedural diamond glow
        for (int i = 0; i < 3; i++)
        {
            GameObject starObj = new GameObject($"Star_{i + 1}");
            starObj.transform.SetParent(starContainer.transform, false);

            RectTransform starRT = starObj.AddComponent<RectTransform>();
            starRT.sizeDelta = new Vector2(starSize, starSize);

            Image starImg = starObj.AddComponent<Image>();
            GlowTextureGenerator.ApplyDiamondGlow(starImg, (int)starSize);
            starImg.color = starEmptyColor;
            starImg.raycastTarget = false;

            // Start at scale 0 for pop-in animation
            starObj.transform.localScale = Vector3.zero;

            starImages.Add(starImg);
        }

        // Reveal each star one at a time with pop animation
        for (int i = 0; i < 3; i++)
        {
            bool earned = (i < starsEarned);

            // Pop-in animation (grey or gold)
            if (earned)
                starImages[i].color = starFilledColor;

            yield return StartCoroutine(AnimationUtilities.PopIn(
                starImages[i].transform, 1.4f, 0.2f, 0.05f));

            if (earned)
                AudioManager.Instance?.PlayButtonClick();

            yield return new WaitForSeconds(starRevealDelay);
        }
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
            containerRT.anchorMin = new Vector2(0.08f, 0.18f);
            containerRT.anchorMax = new Vector2(0.92f, 0.85f);
            containerRT.offsetMin = Vector2.zero;
            containerRT.offsetMax = Vector2.zero;

            // Add vertical layout group
            var vlg = containerObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
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
        rowRT.sizeDelta = new Vector2(0, isTotal ? 60f : 46f);

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
        label.fontSize = isTotal ? 48f : 38f;
        label.fontStyle = isTotal ? FontStyles.Bold : FontStyles.Normal;
        label.alignment = TextAlignmentOptions.Left;
        label.color = isTotal ? new Color(1f, 0.9f, 0.3f) : Color.white;

        // Create value (right-aligned)
        GameObject valueObj = new GameObject("Value");
        valueObj.transform.SetParent(rowObj.transform, false);
        RectTransform valueRT = valueObj.AddComponent<RectTransform>();

        TMP_Text value = valueObj.AddComponent<TextMeshProUGUI>();
        value.text = valueText;
        value.fontSize = isTotal ? 48f : 38f;
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

    /// <summary>
    /// Ensure both "Play Again" and "Main Menu" buttons are active on the results screen.
    /// Handles cases where buttons may have been left inactive in the scene.
    /// </summary>
    private void EnsureResultsButtonsActive()
    {
        if (winScreen == null) return;

        // Find and activate the ReturnMenuButton and PlayAgainButton
        Transform returnMenuBtn = winScreen.transform.Find("ReturnMenuButton");
        if (returnMenuBtn != null)
            returnMenuBtn.gameObject.SetActive(true);

        Transform playAgainBtn = winScreen.transform.Find("PlayAgainButton");
        if (playAgainBtn != null)
            playAgainBtn.gameObject.SetActive(true);
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
    /// Continue button clicked on results screen.
    /// Arcade: restarts with countdown. Zen: returns to main menu.
    /// </summary>
    public void OnContinueButtonClicked()
    {
        AudioManager.Instance?.PlayButtonClick();

        // BP was already banked to persistent storage in ShowWinScreenBreakdown()
        // No need to add again here

        // Hide win screen immediately
        SetActiveIfNotNull(winScreen, false);
        HideBreakdownElements();

        // Zen: return to main menu so player can pick mode again
        // Arcade: restart with countdown
        if (SceneFlowManager.Instance != null && SceneFlowManager.Instance.ResultsFromZen)
        {
            RunManager.Instance?.EndRun();
            CleanupGameOverState();
            SceneFlowManager.Instance.GoBack();
        }
        else
        {
            SceneFlowManager.Instance?.RestartWithCountdown();
        }
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

        // Hide pause UI
        HidePauseHamburger();
        HidePauseMenu();
    }

    /// <summary>
    /// Clean up all game over related state (effects, sounds, panels).
    /// </summary>
    private void CleanupGameOverState()
    {
        // Hide game over screens
        SetActiveIfNotNull(winScreen, false);
        SetActiveIfNotNull(finishTextObject, false);

        // Destroy Zen locked tile counter
        if (zenLockedTileCounterObj != null)
        {
            Destroy(zenLockedTileCounterObj);
            zenLockedTileCounterObj = null;
            zenLockedTileCounterText = null;
        }

        // Hide breakdown elements
        HideBreakdownElements();

        // Clean up confetti
        ClearConfetti();

        // Clean up any active effects
        StopPulse(ref timerPulseCoroutine, timerText?.transform);
        StopPulse(ref multiplierPulseCoroutine, multiplierValueText?.transform);
        StopTimeWarningSound();
        DeactivateHotStreak();
        CleanupHotStreakMode();

        // Reset multiplier display (Zen: hidden, Arcade: show x1.00)
        bool isZen = gameManager != null && gameManager.CurrentMode == GameManager.GameMode.Zen;
        SetActiveIfNotNull(multiplierPanel, !isZen);
        // Hide "Multiplier" label in Zen
        if (isZen && multiplierPanel != null && multiplierPanel.transform.parent != null)
        {
            Transform label = multiplierPanel.transform.parent.Find("Multiplier");
            if (label != null) label.gameObject.SetActive(false);
        }
        if (!isZen && multiplierValueText != null)
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

        StopRainbowBar();
        DestroyHotStreakCountdownBar();

        Debug.Log("<color=gray>Hot streak ended.</color>");
    }

    /// <summary>
    /// Start rainbow color cycling on the multiplier bar fill image.
    /// Called when Hot Streak activates in Arcade mode.
    /// </summary>
    private void StartRainbowBar()
    {
        StopRainbowBar();
        if (multiplierFillImage != null)
        {
            hotStreakRainbowCoroutine = StartCoroutine(RainbowBarCycle());
        }
    }

    /// <summary>
    /// Stop rainbow cycling and reset fill image color.
    /// </summary>
    private void StopRainbowBar()
    {
        if (hotStreakRainbowCoroutine != null)
        {
            StopCoroutine(hotStreakRainbowCoroutine);
            hotStreakRainbowCoroutine = null;
        }

        // Reset fill color to default
        if (multiplierFillImage != null)
        {
            multiplierFillImage.color = multiplierFullColor;
        }
    }

    /// <summary>
    /// Continuously cycles the multiplier bar fill image through rainbow colors.
    /// Runs as a coroutine during Hot Streak in Arcade mode.
    /// </summary>
    private IEnumerator RainbowBarCycle()
    {
        float hue = 0f;
        float cycleSpeed = 0.5f; // Full cycle every 2 seconds

        while (true)
        {
            hue += cycleSpeed * Time.deltaTime;
            if (hue > 1f) hue -= 1f;

            // HSV with full saturation and brightness for vivid rainbow
            Color rainbowColor = Color.HSVToRGB(hue, 0.85f, 1f);
            multiplierFillImage.color = rainbowColor;

            yield return null;
        }
    }

    #endregion

    #region Confetti Celebration

    private readonly List<GameObject> activeConfetti = new List<GameObject>();
    private Coroutine confettiCoroutine;

    // Confetti color palette — festive and vibrant
    private static readonly Color[] confettiColors = new Color[]
    {
        new Color(1f, 0.3f, 0.35f),    // Red
        new Color(1f, 0.85f, 0.2f),     // Gold
        new Color(0.3f, 0.85f, 0.4f),   // Green
        new Color(0.35f, 0.6f, 1f),     // Blue
        new Color(0.9f, 0.4f, 0.9f),    // Pink
        new Color(1f, 0.6f, 0.15f),     // Orange
        new Color(0.5f, 0.85f, 1f),     // Cyan
        new Color(0.85f, 0.7f, 1f),     // Lavender
    };

    /// <summary>
    /// Spawn a burst of UI confetti pieces that fall and spin across the results screen.
    /// </summary>
    private void SpawnConfetti(Transform parent, int count = 60, float duration = 4f)
    {
        if (confettiCoroutine != null)
            StopCoroutine(confettiCoroutine);

        // Clean up any leftover confetti
        ClearConfetti();

        confettiCoroutine = StartCoroutine(ConfettiRoutine(parent, count, duration));
    }

    private IEnumerator ConfettiRoutine(Transform parent, int count, float duration)
    {
        RectTransform parentRect = parent.GetComponent<RectTransform>();
        if (parentRect == null) yield break;

        float parentWidth = parentRect.rect.width;
        float parentHeight = parentRect.rect.height;

        // Spawn confetti in a quick burst (staggered slightly for a natural feel)
        for (int i = 0; i < count; i++)
        {
            SpawnConfettiPiece(parent, parentWidth, parentHeight, duration);

            // Stagger spawns: first 20 come fast, rest trickle in
            if (i < 20)
                yield return null; // 1 frame between
            else if (i % 3 == 0)
                yield return new WaitForSeconds(0.03f);
        }

        // Auto-destroy after 20 seconds as a hard safety ceiling
        yield return new WaitForSeconds(20f);

        ClearConfetti();
    }

    private void SpawnConfettiPiece(Transform parent, float parentWidth, float parentHeight, float duration)
    {
        GameObject piece = new GameObject("Confetti");
        piece.transform.SetParent(parent, false);

        RectTransform rt = piece.AddComponent<RectTransform>();

        // Random confetti size — mix of small rectangles and squares
        bool isSquare = Random.value > 0.6f;
        float w = Random.Range(12f, 24f);
        float h = isSquare ? w : Random.Range(20f, 40f);
        rt.sizeDelta = new Vector2(w, h);

        // Start position: spread across top, slightly above the screen
        float startX = Random.Range(-parentWidth * 0.5f, parentWidth * 0.5f);
        float startY = parentHeight * 0.5f + Random.Range(20f, 120f);
        rt.anchoredPosition = new Vector2(startX, startY);

        Image img = piece.AddComponent<Image>();
        img.color = confettiColors[Random.Range(0, confettiColors.Length)];
        img.raycastTarget = false;

        activeConfetti.Add(piece);

        StartCoroutine(AnimateConfettiPiece(rt, img, parentWidth, parentHeight, duration));
    }

    private IEnumerator AnimateConfettiPiece(RectTransform rt, Image img,
        float parentWidth, float parentHeight, float duration)
    {
        float elapsed = 0f;
        float pieceDuration = duration * Random.Range(0.7f, 1.0f);

        // Per-piece randomized physics
        float fallSpeed = Random.Range(400f, 700f);    // pixels/sec downward
        float swayAmount = Random.Range(40f, 100f);     // horizontal sway amplitude
        float swaySpeed = Random.Range(1.5f, 3.5f);     // sway oscillation speed
        float spinSpeed = Random.Range(180f, 540f);      // degrees/sec
        float spinAxis = Random.value;                    // determines which axis to spin on
        float phaseOffset = Random.Range(0f, Mathf.PI * 2f); // desync sway between pieces
        float drift = Random.Range(-60f, 60f);           // gentle horizontal drift

        Vector2 startPos = rt.anchoredPosition;
        float fadeStart = pieceDuration * 0.7f; // start fading at 70% through

        while (elapsed < pieceDuration)
        {
            if (rt == null) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / pieceDuration;

            // Vertical: accelerating fall (gravity feel)
            float y = startPos.y - fallSpeed * elapsed * (1f + t * 0.3f);

            // Horizontal: sine sway + drift
            float x = startPos.x + Mathf.Sin(elapsed * swaySpeed + phaseOffset) * swayAmount + drift * t;

            rt.anchoredPosition = new Vector2(x, y);

            // Rotation: continuous spin with flutter
            float angle = spinSpeed * elapsed;
            rt.localEulerAngles = new Vector3(0, 0, angle);

            // Simulate tumbling by squashing on one axis via scale
            float tumble = Mathf.Sin(elapsed * spinSpeed * 0.02f + phaseOffset);
            float scaleX = Mathf.Lerp(0.3f, 1f, Mathf.Abs(tumble));
            rt.localScale = new Vector3(scaleX, 1f, 1f);

            // Fade out in the last 30%
            if (elapsed > fadeStart)
            {
                float fadeT = (elapsed - fadeStart) / (pieceDuration - fadeStart);
                Color c = img.color;
                c.a = Mathf.Lerp(1f, 0f, fadeT);
                img.color = c;
            }

            yield return null;
        }

        if (rt != null)
            rt.gameObject.SetActive(false);
    }

    private void ClearConfetti()
    {
        foreach (var piece in activeConfetti)
        {
            if (piece != null)
                Destroy(piece);
        }
        activeConfetti.Clear();
    }

    #endregion
}
