using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Controls the flow between game states/panels.
/// Handles transitions with smooth swipe animations.
/// </summary>
public class SceneFlowManager : MonoBehaviour
{
    public static SceneFlowManager Instance { get; private set; }
    
    [Header("Panels")]
    [SerializeField] private RectTransform loadingPanel;
    [SerializeField] private RectTransform mainMenuPanel;
    [SerializeField] private RectTransform optionsPanel;
    [SerializeField] private RectTransform gamePanel;
    [SerializeField] private RectTransform tutorialPanel1;
    [SerializeField] private RectTransform tutorialPanel2;
    [SerializeField] private RectTransform countdownPanel;
    [SerializeField] private RectTransform quitPanel;
    
    [Header("Transition Settings")]
    [SerializeField] private float transitionDuration = 0.4f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Loading Settings")]
    [SerializeField] private Slider loadingProgressBar;
    [SerializeField] private float minLoadDuration = 1.5f; // Minimum time to show loading (for VFX)
    [SerializeField] private float progressSmoothSpeed = 3f; // How fast progress bar catches up
    
    [Header("Countdown Settings")]
    [SerializeField] private TMPro.TMP_Text countdownText;
    [SerializeField] private float countdownStepDuration = 0.7f;
    
    [Header("References")]
    [SerializeField] private Canvas mainCanvas;
    
    // Screen dimensions for transition calculations
    private float screenWidth;
    private float screenHeight;

    // Current state
    public enum GameState { Loading, MainMenu, Options, Game, ZenGame, Results, Tutorial1, Tutorial2, Countdown, Quit, Paused }
    public GameState CurrentState { get; private set; }

    // Track what state we paused FROM so we can resume correctly
    private GameState stateBeforePause;

    // Track which mode the Results screen originated from (for correct back-navigation)
    public bool ResultsFromZen => resultsFromZen;
    private bool resultsFromZen = false;
    
    #region Initialization
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Set up safe area container for notch/Dynamic Island iPhones
        SetupSafeArea();

        screenWidth = GetCanvasWidth();
        screenHeight = GetCanvasHeight();
    }
    
    private void Start()
    {
        InitializePanels();
        StartCoroutine(LoadingSequence());
    }

    /// <summary>
    /// Creates a SafeArea container under the Canvas and moves all children into it.
    /// This ensures UI respects the safe area on notched/Dynamic Island iPhones.
    /// Runs once in Awake before any panel logic.
    /// </summary>
    private void SetupSafeArea()
    {
        if (mainCanvas == null) return;

        RectTransform canvasRect = mainCanvas.GetComponent<RectTransform>();
        if (canvasRect == null) return;

        // Guard against duplicate SafeArea containers (Awake can re-fire on scene reload)
        if (canvasRect.Find("SafeAreaContainer") != null) return;

        // Create SafeArea container
        GameObject safeAreaGO = new GameObject("SafeAreaContainer");
        RectTransform safeAreaRect = safeAreaGO.AddComponent<RectTransform>();

        // Parent to Canvas first
        safeAreaGO.transform.SetParent(canvasRect, false);

        // Full stretch to fill Canvas
        safeAreaRect.anchorMin = Vector2.zero;
        safeAreaRect.anchorMax = Vector2.one;
        safeAreaRect.offsetMin = Vector2.zero;
        safeAreaRect.offsetMax = Vector2.zero;

        // Add SafeAreaHandler component to adjust anchors based on Screen.safeArea
        safeAreaGO.AddComponent<SafeAreaHandler>();

        // Collect current Canvas children (skip the SafeAreaContainer itself)
        var childrenToMove = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < canvasRect.childCount; i++)
        {
            Transform child = canvasRect.GetChild(i);
            if (child == safeAreaGO.transform) continue;
            childrenToMove.Add(child);
        }

        // Reparent all panels into SafeArea container (preserves sibling order)
        foreach (Transform child in childrenToMove)
        {
            child.SetParent(safeAreaRect, false);
        }

        Debug.Log($"[Make10] SafeArea: Moved {childrenToMove.Count} panels into SafeAreaContainer.");
    }

    private float GetCanvasWidth()
    {
        if (mainCanvas != null)
            return mainCanvas.GetComponent<RectTransform>().rect.width;
        return 1024f;
    }

    private float GetCanvasHeight()
    {
        if (mainCanvas != null)
            return mainCanvas.GetComponent<RectTransform>().rect.height;
        return 1920f;
    }
    
    private void InitializePanels()
    {
        // Force all sliding panels to stretch-fill their parent canvas.
        // This prevents the thin gap/sliver at screen edges caused by panels
        // with center-based anchors and fixed widths that don't perfectly match
        // the canvas size after CanvasScaler adjustments.
        RectTransform[] slidingPanels = { loadingPanel, mainMenuPanel, gamePanel,
            quitPanel, countdownPanel };

        foreach (var panel in slidingPanels)
            EnsurePanelFillsScreen(panel);

        // Position all panels off-screen except loading
        RectTransform[] offScreenPanels = { mainMenuPanel, gamePanel, optionsPanel,
            countdownPanel, quitPanel };

        foreach (var panel in offScreenPanels)
            SetPanelPosition(panel, screenWidth);

        SetPanelPosition(loadingPanel, 0);

        // Set active states
        SetPanelActive(loadingPanel, true);
        SetPanelActive(mainMenuPanel, true);
        SetPanelActive(gamePanel, true);
        SetPanelActive(optionsPanel, false); // Options is overlay
        SetPanelActive(countdownPanel, true);
        SetPanelActive(quitPanel, true);

        // Fix options panel anchoring — ensure it's centered and properly sized
        EnsureOptionsPanelAnchored();

        // Hide old tutorial panels (replaced by TutorialBuilder popups)
        SetPanelActive(tutorialPanel1, false);
        SetPanelActive(tutorialPanel2, false);

        // Initialize TutorialBuilder and wire callbacks
        InitializeTutorialBuilder();
    }

    private void InitializeTutorialBuilder()
    {
        // Create TutorialBuilder singleton if it doesn't exist
        if (TutorialBuilder.Instance == null)
        {
            GameObject builderObj = new GameObject("TutorialBuilder");
            builderObj.AddComponent<TutorialBuilder>();
        }

        TutorialBuilder.Instance.OnTutorial1Complete += () =>
        {
            HandleButton(GameState.Tutorial1, () => StartCoroutine(Tutorial1To2()));
        };

        TutorialBuilder.Instance.OnTutorial2Complete += () =>
        {
            HandleButton(GameState.Tutorial2, () => StartCoroutine(Tutorial2ToCountdown()));
        };

        TutorialBuilder.Instance.OnTutorialCancelled += () =>
        {
            if (CurrentState == GameState.Tutorial1 || CurrentState == GameState.Tutorial2)
            {
                StartCoroutine(CancelTutorialToMainMenu());
            }
        };
    }
    
    #endregion
    
    #region Panel Helpers
    
    /// <summary>
    /// Force a panel to use stretch anchors so it always matches the canvas size exactly.
    /// Prevents sub-pixel gaps at screen edges from CanvasScaler aspect ratio adjustments.
    /// anchoredPosition.x = 0 still means "fill the screen" with stretch anchors.
    /// </summary>
    private void EnsurePanelFillsScreen(RectTransform panel)
    {
        if (panel == null) return;
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = Vector2.zero;
    }

    private void SetPanelPosition(RectTransform panel, float xPos)
    {
        if (panel == null) return;
        Vector2 pos = panel.anchoredPosition;
        pos.x = xPos;
        panel.anchoredPosition = pos;
    }
    
    private void SetPanelActive(RectTransform panel, bool active)
    {
        if (panel != null)
            panel.gameObject.SetActive(active);
    }

    /// <summary>
    /// Ensure the Options panel is properly anchored to center of screen
    /// with correct sizing. Fixes misposition issues from Inspector defaults.
    /// </summary>
    private void EnsureOptionsPanelAnchored()
    {
        if (optionsPanel == null) return;

        // Options is a centered overlay, not a full-screen slide panel
        optionsPanel.anchorMin = new Vector2(0.5f, 0.5f);
        optionsPanel.anchorMax = new Vector2(0.5f, 0.5f);
        optionsPanel.pivot = new Vector2(0.5f, 0.5f);
        optionsPanel.anchoredPosition = Vector2.zero;

        // Use UIStyleGuide large window size for consistency
        optionsPanel.sizeDelta = UIStyleGuide.WindowSizeLarge;

        // Ensure it has a CanvasGroup for fade transitions
        if (optionsPanel.GetComponent<CanvasGroup>() == null)
            optionsPanel.gameObject.AddComponent<CanvasGroup>();

        Debug.Log($"[Make10] Options panel anchored: center, size {UIStyleGuide.WindowSizeLarge}");
    }
    
    /// <summary>
    /// Get the overlay panel for the current state (if any).
    /// </summary>
    private RectTransform GetCurrentOverlayPanel()
    {
        return CurrentState switch
        {
            GameState.Options => optionsPanel,
            _ => null
        };
    }
    
    /// <summary>
    /// Get the slide panel for the current state (if any).
    /// </summary>
    private RectTransform GetCurrentSlidePanel()
    {
        return CurrentState switch
        {
            GameState.Quit => quitPanel,
            GameState.Game => gamePanel,
            _ => null
        };
    }
    
    /// <summary>
    /// Check if current state uses an overlay (fade) transition.
    /// </summary>
    private bool IsOverlayState(GameState state)
    {
        return state == GameState.Options;
    }
    
    /// <summary>
    /// Check if current state uses a slide transition.
    /// </summary>
    private bool IsSlideState(GameState state)
    {
        return state == GameState.Quit || state == GameState.Game;
    }
    
    #endregion
    
    #region Universal Navigation
    
    /// <summary>
    /// Universal back button handler - call this from ANY back button.
    /// Automatically determines the correct transition based on current state.
    /// </summary>
    public void GoBack()
    {
        Debug.Log($"GoBack() called from state: {CurrentState}");
        AudioManager.Instance?.PlayButtonClick();

        switch (CurrentState)
        {
            // Overlay panels → fade out to MainMenu
            case GameState.Options:
                StartCoroutine(CloseOverlayToMainMenu(optionsPanel));
                break;

            // Slide panels → slide back to MainMenu
            case GameState.Quit:
                StartCoroutine(SlideBackToMainMenu(quitPanel));
                break;

            // Game state → full cleanup and return to main menu
            case GameState.Game:
                StartCoroutine(ReturnToMainMenuFromGame());
                break;

            // Results → route based on which mode we came from
            case GameState.Results:
                if (resultsFromZen)
                    StartCoroutine(ReturnToMainMenuFromZen());
                else
                    StartCoroutine(ReturnToMainMenuFromGame());
                break;

            // Zen game → vertical slide back down to main menu
            case GameState.ZenGame:
                StartCoroutine(ReturnToMainMenuFromZen());
                break;

            // Tutorial states → could go back to difficulty or cancel entirely
            case GameState.Tutorial1:
            case GameState.Tutorial2:
                StartCoroutine(CancelTutorialToMainMenu());
                break;

            // Paused → resume the game
            case GameState.Paused:
                OnResumePressed();
                break;

            default:
                Debug.LogWarning($"GoBack() not handled for state: {CurrentState}");
                break;
        }
    }
    
    /// <summary>
    /// Close an overlay panel with fade and return to main menu.
    /// </summary>
    private IEnumerator CloseOverlayToMainMenu(RectTransform overlayPanel)
    {
        yield return FadeTransition(overlayPanel, fadeIn: false);
        CurrentState = GameState.MainMenu;
        Debug.Log("Returned to MainMenu from overlay");
    }
    
    /// <summary>
    /// Slide a panel back and return to main menu.
    /// </summary>
    private IEnumerator SlideBackToMainMenu(RectTransform slidePanel)
    {
        yield return SlideTransition(slidePanel, mainMenuPanel, slideLeft: false);
        CurrentState = GameState.MainMenu;
        SetPanelPosition(slidePanel, screenWidth);
        Debug.Log("Returned to MainMenu from slide panel");
    }
    
    /// <summary>
    /// Return to main menu from the game (handles cleanup).
    /// </summary>
    private IEnumerator ReturnToMainMenuFromGame()
    {
        Debug.Log("ReturnToMainMenuFromGame - cleaning up...");

        // Stop any music (game music, win/lose music)
        AudioManager.Instance?.StopMusic();

        // End the run
        RunManager.Instance?.EndRun();

        // Deactivate the game
        GameManager.Instance?.DeactivateGame();

        // Clear the grid
        GridManager gridManager = FindFirstObjectByType<GridManager>();
        gridManager?.ClearGrid();

        // Notify UIManager to hide any win/lose screens
        UIManager uiManager = FindFirstObjectByType<UIManager>();
        uiManager?.HideAllGameOverScreens();

        // Slide back to main menu
        yield return SlideTransition(gamePanel, mainMenuPanel, slideLeft: false);
        CurrentState = GameState.MainMenu;
        SetPanelPosition(gamePanel, screenWidth);

        // Start menu music
        AudioManager.Instance?.PlayMenuMusic();

        Debug.Log("Returned to MainMenu from Game");
    }

    /// <summary>
    /// Return to main menu from Zen mode (vertical slide down).
    /// </summary>
    private IEnumerator ReturnToMainMenuFromZen()
    {
        Debug.Log("ReturnToMainMenuFromZen - cleaning up...");

        AudioManager.Instance?.StopMusic();
        RunManager.Instance?.EndRun();
        GameManager.Instance?.DeactivateGame();

        // Reset game mode back to Arcade (default)
        GameManager.Instance?.SetGameMode(GameManager.GameMode.Arcade);

        GridManager gridManager = FindFirstObjectByType<GridManager>();
        gridManager?.ClearGrid();

        UIManager uiManager = FindFirstObjectByType<UIManager>();
        uiManager?.HideAllGameOverScreens();

        // Vertical slide: game panel slides down, main menu enters from above
        yield return VerticalSlideTransition(gamePanel, mainMenuPanel, slideUp: true);
        CurrentState = GameState.MainMenu;

        // Reset game panel position for future horizontal transitions
        SetPanelPosition(gamePanel, screenWidth);
        SetPanelVerticalPosition(gamePanel, 0);

        AudioManager.Instance?.PlayMenuMusic();

        Debug.Log("Returned to MainMenu from Zen");
    }

    /// <summary>
    /// Cancel from tutorials and return to main menu.
    /// </summary>
    private IEnumerator CancelTutorialToMainMenu()
    {
        Debug.Log("Canceling tutorial, returning to main menu...");

        // Tutorial popup already closed by TutorialBuilder close button
        TutorialBuilder.Instance?.HideCurrentTutorial();

        yield return new WaitForSeconds(0.15f);

        // Clear the grid (it was spawned for tutorials)
        GridManager gridManager = FindFirstObjectByType<GridManager>();
        gridManager?.ClearGrid();

        // Slide back to main menu
        yield return SlideTransition(gamePanel, mainMenuPanel, slideLeft: false);
        CurrentState = GameState.MainMenu;
        SetPanelPosition(gamePanel, screenWidth);

        // Start menu music
        AudioManager.Instance?.PlayMenuMusic();

        Debug.Log("Returned to MainMenu from Tutorial");
    }
    
    #endregion
    
    #region Core Sequences
    
    // Loading progress state
    private float loadingDisplayProgress = 0f;

    private IEnumerator LoadingSequence()
    {
        CurrentState = GameState.Loading;
        Debug.Log("LoadingSequence started - tracking actual initialization");

        // Recalculate canvas dimensions after layout pass for accurate slide positioning
        yield return null;
        screenWidth = GetCanvasWidth();
        screenHeight = GetCanvasHeight();

        // Fast path: if all managers are already initialized, skip the loading bar
        bool allReady = (AudioManager.Instance != null &&
                         GameManager.Instance != null &&
                         FindFirstObjectByType<GridManager>() != null);

        if (allReady)
        {
            Debug.Log("LoadingSequence: All managers ready — skipping to MainMenu");

            // Brief flash of loading screen (just enough for visual continuity)
            yield return new WaitForSeconds(0.3f);

            PlayAudio(() => AudioManager.Instance?.PlayMenuMusic());
            yield return SlideTransition(loadingPanel, mainMenuPanel, slideLeft: true);
            SetPanelActive(loadingPanel, false);
            CurrentState = GameState.MainMenu;
            Debug.Log($"Now in MainMenu state (fast path)");
            yield break;
        }

        // Normal path: show loading progress while managers initialize
        loadingDisplayProgress = 0f;
        float startTime = Time.time;

        if (loadingProgressBar != null)
            loadingProgressBar.value = 0f;

        // Step 1: Initialize core systems (0% - 20%)
        Debug.Log("Loading: Initializing core systems...");
        yield return SmoothProgressTo(0.2f);

        // Step 2: Wait for AudioManager (20% - 40%)
        Debug.Log("Loading: Initializing audio...");
        while (AudioManager.Instance == null)
        {
            yield return null;
        }
        yield return SmoothProgressTo(0.4f);

        // Step 3: Wait for GameManager (40% - 60%)
        Debug.Log("Loading: Initializing game manager...");
        while (GameManager.Instance == null)
        {
            yield return null;
        }
        yield return SmoothProgressTo(0.6f);

        // Step 4: Warm up prefabs / verify GridManager (60% - 80%)
        Debug.Log("Loading: Preparing game components...");
        GridManager gridManager = FindFirstObjectByType<GridManager>();
        if (gridManager != null)
        {
            Debug.Log("Loading: GridManager ready");
        }
        yield return SmoothProgressTo(0.8f);

        // Step 5: Final preparations (80% - 100%)
        Debug.Log("Loading: Final preparations...");
        yield return SmoothProgressTo(1f);

        // Ensure minimum load time for VFX to show
        float elapsed = Time.time - startTime;
        if (elapsed < minLoadDuration)
        {
            yield return new WaitForSeconds(minLoadDuration - elapsed);
        }

        // Small pause at 100% before transition
        yield return new WaitForSeconds(0.3f);

        Debug.Log("LoadingSequence complete - transitioning to MainMenu");

        // Start menu music
        PlayAudio(() => AudioManager.Instance?.PlayMenuMusic());

        // Transition to main menu
        yield return SlideTransition(loadingPanel, mainMenuPanel, slideLeft: true);
        SetPanelActive(loadingPanel, false);
        CurrentState = GameState.MainMenu;
        Debug.Log($"Now in MainMenu state");
    }

    /// <summary>
    /// Smoothly animate progress bar to target value.
    /// </summary>
    private IEnumerator SmoothProgressTo(float targetProgress)
    {
        while (loadingDisplayProgress < targetProgress - 0.001f)
        {
            loadingDisplayProgress = Mathf.Lerp(loadingDisplayProgress, targetProgress, Time.deltaTime * progressSmoothSpeed);

            // Snap if very close
            if (targetProgress - loadingDisplayProgress < 0.01f)
                loadingDisplayProgress = targetProgress;

            if (loadingProgressBar != null)
                loadingProgressBar.value = loadingDisplayProgress;

            yield return null;
        }

        loadingDisplayProgress = targetProgress;
        if (loadingProgressBar != null)
            loadingProgressBar.value = loadingDisplayProgress;
    }
    
    private IEnumerator PlaySequence()
    {
        // Stop menu music
        AudioManager.Instance?.StopMusic();

        RunManager.Instance?.StartNewRun();

        // Transition to game panel
        yield return SlideTransition(mainMenuPanel, gamePanel, slideLeft: true);

        // Spawn grid (visible behind tutorials) but DON'T process matches yet!
        FindFirstObjectByType<GridManager>()?.SpawnGridOnly();

        yield return new WaitForSeconds(0.1f);

        // Show tutorials (using PopupWindow-based TutorialBuilder)
        CurrentState = GameState.Tutorial1;
        TutorialBuilder.Instance?.ShowTutorial1();
    }
    
    private IEnumerator CountdownSequence()
    {
        yield return RunCountdown("GO!", GameState.Game, () =>
        {
            GameManager.Instance?.ActivateGame();
            FindFirstObjectByType<GridManager>()?.OnRoundStarted();
            FindFirstObjectByType<GridManager>()?.StartMatchProcessing();
        });
    }

    /// <summary>
    /// Shared countdown logic used by both normal rounds and boss fights.
    /// </summary>
    private IEnumerator RunCountdown(string finalWord, GameState targetState, System.Action onComplete)
    {
        Debug.Log($"Countdown started (final: {finalWord})");
        SetPanelPosition(countdownPanel, 0);
        countdownPanel.localScale = Vector3.one;

        string[] steps = { "3", "2", "1", finalWord };

        foreach (string step in steps)
        {
            if (countdownText != null)
            {
                countdownText.text = step;

                bool isFinal = (step == finalWord);
                if (isFinal)
                    AudioManager.Instance?.PlayCountdownGo();
                else
                    AudioManager.Instance?.PlayCountdownBeep();

                yield return CountdownPop(isFinal);
            }

            yield return new WaitForSeconds(countdownStepDuration);
        }

        Debug.Log($"Countdown complete - entering {targetState}");
        yield return HidePanel(countdownPanel);
        CurrentState = targetState;

        // Play mode-appropriate music
        if (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.Zen)
            AudioManager.Instance?.PlayZenMusic();
        else
            AudioManager.Instance?.PlayGameMusic();
        onComplete?.Invoke();

        Debug.Log($"Countdown sequence complete ({targetState})");
    }
    
    /// <summary>
    /// Countdown number pop — EaseOutBack for "3/2/1", larger EaseOutElastic for "GO!".
    /// </summary>
    private IEnumerator CountdownPop(bool isFinalWord = false)
    {
        float startScale = isFinalWord ? 1.8f : 1.5f;
        float duration = isFinalWord ? 0.2f : 0.15f;

        countdownPanel.localScale = Vector3.one * startScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = isFinalWord
                ? AnimationUtilities.EaseOutElastic(t)
                : AnimationUtilities.EaseOutBack(t);
            // EaseOutBack/Elastic go from 0→1, so we map startScale→1
            float scale = Mathf.LerpUnclamped(startScale, 1f, eased);
            countdownPanel.localScale = Vector3.one * scale;
            yield return null;
        }
        countdownPanel.localScale = Vector3.one;
    }
    
    #endregion
    
    #region Transition Animations
    
    private IEnumerator SlideTransition(RectTransform from, RectTransform to, bool slideLeft)
    {
        AudioManager.Instance?.PlayTransitionSwipe();
        
        float direction = slideLeft ? -1f : 1f;
        SetPanelPosition(to, -direction * screenWidth);
        
        Vector2 fromStart = from.anchoredPosition;
        Vector2 toStart = to.anchoredPosition;
        Vector2 fromEnd = new Vector2(direction * screenWidth, fromStart.y);
        Vector2 toEnd = new Vector2(0, toStart.y);
        
        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = transitionCurve.Evaluate(elapsed / transitionDuration);
            
            from.anchoredPosition = Vector2.Lerp(fromStart, fromEnd, t);
            to.anchoredPosition = Vector2.Lerp(toStart, toEnd, t);
            yield return null;
        }
        
        from.anchoredPosition = fromEnd;
        to.anchoredPosition = toEnd;
    }
    
    /// <summary>
    /// Vertical slide transition — slides panels up or down.
    /// slideUp=true: 'from' slides up off-screen, 'to' enters from below.
    /// </summary>
    private IEnumerator VerticalSlideTransition(RectTransform from, RectTransform to, bool slideUp)
    {
        AudioManager.Instance?.PlayTransitionSwipe();

        float direction = slideUp ? 1f : -1f;

        // Position 'to' panel below (or above) screen
        SetPanelVerticalPosition(to, -direction * screenHeight);

        Vector2 fromStart = from.anchoredPosition;
        Vector2 toStart = to.anchoredPosition;
        Vector2 fromEnd = new Vector2(fromStart.x, direction * screenHeight);
        Vector2 toEnd = new Vector2(toStart.x, 0);

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = transitionCurve.Evaluate(elapsed / transitionDuration);

            from.anchoredPosition = Vector2.Lerp(fromStart, fromEnd, t);
            to.anchoredPosition = Vector2.Lerp(toStart, toEnd, t);
            yield return null;
        }

        from.anchoredPosition = fromEnd;
        to.anchoredPosition = toEnd;
    }

    private void SetPanelVerticalPosition(RectTransform panel, float yPos)
    {
        if (panel == null) return;
        Vector2 pos = panel.anchoredPosition;
        pos.y = yPos;
        panel.anchoredPosition = pos;
    }

    private IEnumerator FadeTransition(RectTransform panel, bool fadeIn)
    {
        if (panel == null) yield break;
        
        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        if (group == null)
            group = panel.gameObject.AddComponent<CanvasGroup>();
        
        if (fadeIn)
        {
            panel.gameObject.SetActive(true);
            panel.anchoredPosition = Vector2.zero;
        }
        
        yield return AnimationUtilities.FadeCanvasGroup(group, fadeIn, transitionDuration * 0.5f);
        
        if (!fadeIn)
            panel.gameObject.SetActive(false);
    }
    
    private IEnumerator ShowPanel(RectTransform panel)
    {
        SetPanelPosition(panel, 0);
        yield return AnimationUtilities.ScaleIn(panel, transitionDuration, 1f, transitionCurve);
    }
    
    private IEnumerator HidePanel(RectTransform panel)
    {
        yield return AnimationUtilities.ScaleOut(panel, transitionDuration * 0.5f);
        SetPanelPosition(panel, screenWidth);
    }
    
    #endregion
    
    #region Button Handlers
    
    private void HandleButton(GameState requiredState, System.Action action)
    {
        if (CurrentState != requiredState)
        {
            Debug.LogWarning($"Button ignored - not in {requiredState} state (currently {CurrentState})");
            return;
        }
        
        AudioManager.Instance?.PlayButtonClick();
        action?.Invoke();
    }
    
    /// <summary>
    /// Play button pressed - starts Arcade mode directly.
    /// </summary>
    public void OnPlayPressed()
    {
        Debug.Log($"OnPlayPressed called! CurrentState = {CurrentState}");
        HandleButton(GameState.MainMenu, () =>
        {
            StartCoroutine(PlaySequence());
        });
    }

    /// <summary>
    /// Zen button pressed — vertical slide up into Zen Mode game.
    /// Wire this to ZenButton's onClick in Inspector.
    /// </summary>
    public void OnZenPressed()
    {
        Debug.Log($"OnZenPressed called! CurrentState = {CurrentState}");
        HandleButton(GameState.MainMenu, () =>
        {
            StartCoroutine(ZenPlaySequence());
        });
    }

    private IEnumerator ZenPlaySequence()
    {
        // Stop menu music
        AudioManager.Instance?.StopMusic();

        // Set game mode to Zen
        GameManager.Instance?.SetGameMode(GameManager.GameMode.Zen);

        RunManager.Instance?.StartNewRun();

        // Reset game panel X position (it starts off-screen right from InitializePanels)
        SetPanelPosition(gamePanel, 0);

        // Vertical slide: main menu scrolls up, game panel enters from below
        yield return VerticalSlideTransition(mainMenuPanel, gamePanel, slideUp: false);

        // Spawn grid
        FindFirstObjectByType<GridManager>()?.SpawnGridOnly();
        yield return new WaitForSeconds(0.1f);

        // Activate game immediately (no countdown in Zen mode)
        CurrentState = GameState.ZenGame;
        GameManager.Instance?.ActivateGame();
        FindFirstObjectByType<GridManager>()?.OnRoundStarted();
        FindFirstObjectByType<GridManager>()?.StartMatchProcessing();

        // Start zen music
        AudioManager.Instance?.PlayZenMusic();

        Debug.Log("Zen Mode started — no timer, no countdown");
    }

    public void OnOptionsPressed()
    {
        Debug.Log($"OnOptionsPressed called! CurrentState = {CurrentState}");
        HandleButton(GameState.MainMenu, () =>
        {
            AudioManager.Instance?.PlayButtonClick();
            ShowOptionsPopup();
        });
    }

    // Legacy method - kept for backward compatibility
    public void OnOptionsClosePressed() => GoBack();
    
    public void OnQuitPressed()
    {
        Debug.Log($"OnQuitPressed called! CurrentState = {CurrentState}");
        HandleButton(GameState.MainMenu, () => StartCoroutine(QuitSequence()));
    }
    
    private IEnumerator QuitSequence()
    {
        yield return SlideTransition(mainMenuPanel, quitPanel, slideLeft: true);
        CurrentState = GameState.Quit;
        Debug.Log("Quit panel shown.");
    }
    
    // Legacy method - now calls GoBack()
    public void OnQuitBackPressed() => GoBack();
    
    public void OnItchIOButtonPressed()
    {
        AudioManager.Instance?.PlayButtonClick();
        Debug.Log("Opening itch.io...");
        Application.OpenURL("https://itch.io/");
    }

    /// <summary>
    /// Credits button pressed — opens a PopupWindow with credits info.
    /// Wire this to the Credits button's onClick in Inspector (or called from MainMenuUI).
    /// </summary>
    public void OnCreditsPressed()
    {
        Debug.Log($"OnCreditsPressed called! CurrentState = {CurrentState}");
        if (CurrentState != GameState.MainMenu) return;
        AudioManager.Instance?.PlayButtonClick();
        ShowCreditsPopup();
    }

    /// <summary>
    /// Shop button pressed — shows "Coming Soon" feedback.
    /// Wire this to the Shop button's onClick in Inspector (or called from MainMenuUI).
    /// </summary>
    public void OnShopPressed()
    {
        Debug.Log($"OnShopPressed called! CurrentState = {CurrentState}");
        if (CurrentState != GameState.MainMenu) return;
        AudioManager.Instance?.PlayButtonClick();
        // Shop is greyed out — this is a safety fallback if somehow clicked
        Debug.Log("Shop coming soon!");
    }

    /// <summary>
    /// Pause button pressed (hamburger menu during gameplay).
    /// Freezes time and shows pause overlay.
    /// </summary>
    public void OnPausePressed()
    {
        if (CurrentState != GameState.Game && CurrentState != GameState.ZenGame) return;

        Debug.Log($"Game paused from state: {CurrentState}");
        AudioManager.Instance?.PlayButtonClick();

        stateBeforePause = CurrentState;
        CurrentState = GameState.Paused;
        Time.timeScale = 0f;

        // Tell UIManager to show pause overlay
        UIManager uiManager = FindFirstObjectByType<UIManager>();
        uiManager?.ShowPauseMenu();
    }

    /// <summary>
    /// Resume button pressed — unpauses and returns to gameplay.
    /// </summary>
    public void OnResumePressed()
    {
        if (CurrentState != GameState.Paused) return;

        Debug.Log($"Game resumed to state: {stateBeforePause}");
        AudioManager.Instance?.PlayButtonClick();

        Time.timeScale = 1f;
        CurrentState = stateBeforePause;

        UIManager uiManager = FindFirstObjectByType<UIManager>();
        uiManager?.HidePauseMenu();
    }

    /// <summary>
    /// Main Menu button pressed from pause menu — quits current game.
    /// </summary>
    public void OnPauseMainMenuPressed()
    {
        if (CurrentState != GameState.Paused) return;

        Debug.Log("Returning to main menu from pause");
        AudioManager.Instance?.PlayButtonClick();

        // Unpause time first
        Time.timeScale = 1f;

        // Hide pause menu
        UIManager uiManager = FindFirstObjectByType<UIManager>();
        uiManager?.HidePauseMenu();

        // Route to the correct return-to-menu based on which mode we paused from
        if (stateBeforePause == GameState.ZenGame)
        {
            CurrentState = GameState.ZenGame; // Temporarily set so GoBack routes correctly
            GoBack();
        }
        else
        {
            CurrentState = GameState.Game;
            GoBack();
        }
    }

    /// <summary>
    /// Options button pressed from pause menu — opens options popup overlay.
    /// </summary>
    public void OnPauseOptionsPressed()
    {
        if (CurrentState != GameState.Paused) return;

        Debug.Log("Opening options from pause menu");
        AudioManager.Instance?.PlayButtonClick();
        ShowOptionsPopup();
    }

    /// <summary>
    /// Close options panel and return to pause menu (when opened from pause).
    /// Legacy — the PopupWindow now handles its own close.
    /// </summary>
    public void OnPauseOptionsClosePressed()
    {
        Debug.Log("Closing options, returning to pause menu");
        AudioManager.Instance?.PlayButtonClick();
    }
    
    public void OnTutorial1OkPressed()
    {
        HandleButton(GameState.Tutorial1, () => StartCoroutine(Tutorial1To2()));
    }
    
    private IEnumerator Tutorial1To2()
    {
        // Tutorial1 popup already closed by TutorialBuilder button callback
        CurrentState = GameState.Tutorial2;
        yield return new WaitForSeconds(0.15f); // Brief pause between tutorials
        TutorialBuilder.Instance?.ShowTutorial2();
    }
    
    public void OnTutorial2GotThisPressed()
    {
        HandleButton(GameState.Tutorial2, () => StartCoroutine(Tutorial2ToCountdown()));
    }
    
    private IEnumerator Tutorial2ToCountdown()
    {
        // Tutorial2 popup already closed by TutorialBuilder button callback
        CurrentState = GameState.Countdown;
        yield return new WaitForSeconds(0.15f); // Brief pause before countdown
        yield return CountdownSequence();
    }
    
    #endregion
    
    #region Credits

    /// <summary>
    /// Show the credits popup using PopupWindow system.
    /// </summary>
    private void ShowCreditsPopup()
    {
        // Find or create a PopupWindow
        PopupWindow popup = FindFirstObjectByType<PopupWindow>();
        if (popup == null)
        {
            GameObject popupObj = new GameObject("CreditsPopup");
            popupObj.transform.SetParent(mainCanvas.transform, false);
            popup = popupObj.AddComponent<PopupWindow>();
        }

        popup.SetTitle("Credits");
        popup.ClearContent();

        popup.AddText("MAKE 10", UIStyleGuide.FontSizeHeadline, UIStyleGuide.ColorTextAccent,
            TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
        popup.AddSpacer(10f);
        popup.AddText("A Number Puzzle Game", UIStyleGuide.FontSizeSubheading, UIStyleGuide.ColorTextSecondary,
            TMPro.TextAlignmentOptions.Center);
        popup.AddSpacer(20f);
        popup.AddDivider();
        popup.AddSpacer(10f);

        popup.AddText("Created by", UIStyleGuide.FontSizeCaption, UIStyleGuide.ColorTextSecondary,
            TMPro.TextAlignmentOptions.Center);
        popup.AddText("CJ Rhone", UIStyleGuide.FontSizeSubheading, UIStyleGuide.ColorTextPrimary,
            TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
        popup.AddText("Wizard Bodega", UIStyleGuide.FontSizeBody, UIStyleGuide.ColorTextAccent,
            TMPro.TextAlignmentOptions.Center);
        popup.AddSpacer(20f);
        popup.AddDivider();
        popup.AddSpacer(10f);

        popup.AddText("Design & Programming", UIStyleGuide.FontSizeCaption, UIStyleGuide.ColorTextSecondary,
            TMPro.TextAlignmentOptions.Center);
        popup.AddText("CJ Rhone", UIStyleGuide.FontSizeBody, UIStyleGuide.ColorTextPrimary,
            TMPro.TextAlignmentOptions.Center);
        popup.AddSpacer(10f);

        popup.AddText("Made for", UIStyleGuide.FontSizeCaption, UIStyleGuide.ColorTextSecondary,
            TMPro.TextAlignmentOptions.Center);
        popup.AddText("Brainless Game Jam 2026", UIStyleGuide.FontSizeBody, UIStyleGuide.ColorTextPrimary,
            TMPro.TextAlignmentOptions.Center);
        popup.AddSpacer(20f);
        popup.AddDivider();
        popup.AddSpacer(10f);

        popup.AddText("Built with Unity", UIStyleGuide.FontSizeCaption, UIStyleGuide.ColorTextMuted,
            TMPro.TextAlignmentOptions.Center);
        popup.AddSpacer(20f);

        popup.AddButton("Close", () => popup.Close(), UIStyleGuide.ColorButtonPrimary);

        popup.Open();
    }

    #endregion

    #region Options

    /// <summary>
    /// Show the options popup using PopupWindow system.
    /// Works from both main menu and pause menu — popup is self-contained.
    /// </summary>
    private void ShowOptionsPopup()
    {
        // Create a fresh PopupWindow each time (same pattern as credits)
        PopupWindow popup = FindFirstObjectByType<PopupWindow>();
        if (popup == null)
        {
            GameObject popupObj = new GameObject("OptionsPopup");
            popupObj.transform.SetParent(mainCanvas.transform, false);
            popup = popupObj.AddComponent<PopupWindow>();
        }

        popup.SetTitle("Options");
        popup.ClearContent();
        popup.SetAutoSizeMode(900f, 400f, 1000f, false);

        popup.AddSpacer(10f);

        // --- Music Volume ---
        float currentMusic = AudioManager.Instance != null ? AudioManager.Instance.MusicVolume : 0.7f;
        popup.AddSlider("Music", currentMusic, (val) =>
        {
            AudioManager.Instance?.SetMusicVolume(val);
        }, UIStyleGuide.ColorButtonSecondary);

        popup.AddSpacer(10f);

        // --- SFX Volume ---
        float currentSFX = AudioManager.Instance != null ? AudioManager.Instance.SFXVolume : 1f;
        popup.AddSlider("Sound Effects", currentSFX, (val) =>
        {
            AudioManager.Instance?.SetSFXVolume(val);
        }, UIStyleGuide.ColorButtonPrimary);

        popup.AddSpacer(20f);
        popup.AddDivider();
        popup.AddSpacer(15f);

        popup.AddButton("Done", () =>
        {
            AudioManager.Instance?.PlayButtonClick();
            popup.Close();
        }, UIStyleGuide.ColorButtonPrimary);

        popup.Open();
    }

    #endregion

    #region Public Utilities

    /// <summary>
    /// Return to main menu from anywhere (legacy method, use GoBack() instead).
    /// </summary>
    public void ReturnToMainMenu()
    {
        GoBack();
    }
    
    public void OnGameEnded()
    {
        Debug.Log($"SceneFlowManager: Game ended - showing results (from {CurrentState})");
        resultsFromZen = (CurrentState == GameState.ZenGame);
        CurrentState = GameState.Results;
    }

    public bool IsInGameplay() => CurrentState == GameState.Game || CurrentState == GameState.ZenGame || CurrentState == GameState.Paused;

    
    public void RestartWithCountdown()
    {
        StartCoroutine(RestartWithCountdownSequence());
    }
    
    private IEnumerator RestartWithCountdownSequence()
    {
        Debug.Log("RestartWithCountdown - spawning grid and starting countdown");

        // Stop any current music (win/lose music)
        AudioManager.Instance?.StopMusic();

        // Hide any win/lose screens first
        UIManager uiManager = FindFirstObjectByType<UIManager>();
        uiManager?.HideAllGameOverScreens();

        // Advance to next round
        RunManager.Instance?.AdvanceRound();

        // Spawn the grid (visible during countdown) but DON'T process matches yet
        GridManager gridManager = FindFirstObjectByType<GridManager>();
        gridManager?.SpawnGridOnly();

        // Reset game state (score, timer, etc.) but don't activate yet
        GameManager.Instance?.StartNewGame();
        
        yield return new WaitForSeconds(0.1f);
        
        // Run countdown sequence
        CurrentState = GameState.Countdown;
        yield return CountdownSequence();
    }
    
    public void StartGameImmediate()
    {
        if (CurrentState != GameState.MainMenu) return;
        StartCoroutine(StartGameImmediateSequence());
    }
    
    private IEnumerator StartGameImmediateSequence()
    {
        yield return SlideTransition(mainMenuPanel, gamePanel, slideLeft: true);
        CurrentState = GameState.Game;
        GameManager.Instance?.StartNewGame();
    }
    
    #endregion
    
    #region Audio Helper
    
    private void PlayAudio(System.Action playAction)
    {
        playAction?.Invoke();
    }
    
    #endregion
}
