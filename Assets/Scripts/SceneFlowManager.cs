using UnityEngine;
using UnityEngine.UI;
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
    [SerializeField] private RectTransform shopPanel;
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
    
    // Screen width for swipe calculations
    private float screenWidth;
    
    // Current state
    public enum GameState { Loading, MainMenu, Options, Game, Results, Shop, Tutorial1, Tutorial2, Countdown, Quit }
    public GameState CurrentState { get; private set; }
    
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
    
    private void InitializePanels()
    {
        // Force all sliding panels to stretch-fill their parent canvas.
        // This prevents the thin gap/sliver at screen edges caused by panels
        // with center-based anchors and fixed widths that don't perfectly match
        // the canvas size after CanvasScaler adjustments.
        RectTransform[] slidingPanels = { loadingPanel, mainMenuPanel, gamePanel,
            shopPanel, quitPanel, countdownPanel };

        foreach (var panel in slidingPanels)
            EnsurePanelFillsScreen(panel);

        // Position all panels off-screen except loading
        RectTransform[] offScreenPanels = { mainMenuPanel, gamePanel, shopPanel, optionsPanel,
            countdownPanel, quitPanel };

        foreach (var panel in offScreenPanels)
            SetPanelPosition(panel, screenWidth);

        SetPanelPosition(loadingPanel, 0);

        // Set active states
        SetPanelActive(loadingPanel, true);
        SetPanelActive(mainMenuPanel, true);
        SetPanelActive(gamePanel, true);
        SetPanelActive(shopPanel, true);
        SetPanelActive(optionsPanel, false); // Options is overlay
        SetPanelActive(countdownPanel, true);
        SetPanelActive(quitPanel, true);

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
            case GameState.Results:
                StartCoroutine(ReturnToMainMenuFromGame());
                break;

            // Shop state → end run and return to main menu
            case GameState.Shop:
                StartCoroutine(ReturnToMainMenuFromShop());
                break;

            // Tutorial states → could go back to difficulty or cancel entirely
            case GameState.Tutorial1:
            case GameState.Tutorial2:
                StartCoroutine(CancelTutorialToMainMenu());
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
    /// Return to main menu from shop (ends the run).
    /// </summary>
    private IEnumerator ReturnToMainMenuFromShop()
    {
        Debug.Log("ReturnToMainMenuFromShop - ending run...");

        // Stop any music
        AudioManager.Instance?.StopMusic();

        // End the run
        RunManager.Instance?.EndRun();

        // Hide shop UI
        ShopManager.Instance?.HideShop();

        // Slide back to main menu
        yield return SlideTransition(shopPanel, mainMenuPanel, slideLeft: false);
        CurrentState = GameState.MainMenu;
        SetPanelPosition(shopPanel, screenWidth);

        // Start menu music
        AudioManager.Instance?.PlayMenuMusic();

        Debug.Log("Returned to MainMenu from Shop");
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

        // Recalculate canvas width after layout pass for accurate slide positioning
        yield return null;
        screenWidth = GetCanvasWidth();

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

        // Start a new campaign and run
        CampaignManager.Instance?.StartNewCampaign();
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

                if (step == finalWord)
                    AudioManager.Instance?.PlayCountdownGo();
                else
                    AudioManager.Instance?.PlayCountdownBeep();

                yield return CountdownPop();
            }

            yield return new WaitForSeconds(countdownStepDuration);
        }

        Debug.Log($"Countdown complete - entering {targetState}");
        yield return HidePanel(countdownPanel);
        CurrentState = targetState;

        AudioManager.Instance?.PlayGameMusic();
        onComplete?.Invoke();

        Debug.Log($"Countdown sequence complete ({targetState})");
    }
    
    private IEnumerator CountdownPop()
    {
        countdownPanel.localScale = Vector3.one * 1.5f;
        float elapsed = 0f;
        while (elapsed < 0.15f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.15f;
            countdownPanel.localScale = Vector3.Lerp(Vector3.one * 1.5f, Vector3.one, t);
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
    /// Play button pressed - starts game directly.
    /// </summary>
    public void OnPlayPressed()
    {
        Debug.Log($"OnPlayPressed called! CurrentState = {CurrentState}");
        HandleButton(GameState.MainMenu, () =>
        {
            StartCoroutine(PlaySequence());
        });
    }

    public void OnOptionsPressed()
    {
        Debug.Log($"OnOptionsPressed called! CurrentState = {CurrentState}");
        HandleButton(GameState.MainMenu, () =>
        {
            StartCoroutine(FadeTransition(optionsPanel, fadeIn: true));
            CurrentState = GameState.Options;
        });
    }
    
    // Legacy method - now calls GoBack()
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
        Debug.Log($"SceneFlowManager: Game ended - showing results");
        CurrentState = GameState.Results;
    }

    /// <summary>
    /// Transition from results screen to shop (called by UIManager on Continue press).
    /// </summary>
    public void TransitionToShop()
    {
        if (CurrentState != GameState.Results && CurrentState != GameState.Game)
        {
            Debug.LogWarning($"TransitionToShop called from invalid state: {CurrentState}");
            return;
        }

        StartCoroutine(TransitionToShopSequence());
    }

    private IEnumerator TransitionToShopSequence()
    {
        Debug.Log("TransitionToShopSequence - sliding grid out, shop in");

        // Stop any win music
        AudioManager.Instance?.StopMusic();

        // Slide game panel out (left), shop panel in (from right)
        yield return SlideTransition(gamePanel, shopPanel, slideLeft: true);

        CurrentState = GameState.Shop;

        // Reset game panel position for later
        SetPanelPosition(gamePanel, screenWidth);

        // Notify ShopManager to show (if it exists) - it handles its own music
        ShopManager.Instance?.ShowShop();

        Debug.Log("Now in Shop state");
    }

    /// <summary>
    /// Transition from shop back to game for next round (called by ShopManager).
    /// </summary>
    public void TransitionFromShopToGame()
    {
        if (CurrentState != GameState.Shop)
        {
            Debug.LogWarning($"TransitionFromShopToGame called from invalid state: {CurrentState}");
            return;
        }

        StartCoroutine(TransitionFromShopToGameSequence());
    }

    private IEnumerator TransitionFromShopToGameSequence()
    {
        Debug.Log("TransitionFromShopToGameSequence - sliding shop out, grid in");

        // Hide shop UI first
        ShopManager.Instance?.HideShop();

        // Advance to next round
        RunManager.Instance?.AdvanceRound();

        // Spawn new grid (visible during transition)
        GridManager gridManager = FindFirstObjectByType<GridManager>();
        gridManager?.SpawnGridOnly();

        // Slide shop panel out (left), game panel in (from right)
        yield return SlideTransition(shopPanel, gamePanel, slideLeft: true);

        // Reset shop panel position for later
        SetPanelPosition(shopPanel, screenWidth);

        // Run countdown sequence
        CurrentState = GameState.Countdown;
        yield return CountdownSequence();

        Debug.Log("Next round started");
    }
    
    public bool IsInGameplay() => CurrentState == GameState.Game;

    
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
