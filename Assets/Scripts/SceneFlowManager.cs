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
    [SerializeField] private RectTransform chillZonePanel;

    [Header("Chill Zone Settings")]
    [SerializeField] private AudioClip chillZoneMusic;
    
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
    public enum GameState { Loading, MainMenu, Options, Game, Win, Shop, Tutorial1, Tutorial2, Countdown, Quit, ChillZone, BossFight }
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
        screenWidth = GetCanvasWidth();
    }
    
    private void Start()
    {
        InitializePanels();
        StartCoroutine(LoadingSequence());
    }
    
    private float GetCanvasWidth()
    {
        if (mainCanvas != null)
            return mainCanvas.GetComponent<RectTransform>().rect.width;
        return 1024f;
    }
    
    private void InitializePanels()
    {
        // Create chill zone panel if it doesn't exist
        EnsureChillZonePanelExists();

        // Position all panels off-screen except loading
        RectTransform[] offScreenPanels = { mainMenuPanel, gamePanel, shopPanel, optionsPanel,
            tutorialPanel1, tutorialPanel2, countdownPanel, quitPanel, chillZonePanel };

        foreach (var panel in offScreenPanels)
            SetPanelPosition(panel, screenWidth);

        SetPanelPosition(loadingPanel, 0);

        // Set active states
        SetPanelActive(loadingPanel, true);
        SetPanelActive(mainMenuPanel, true);
        SetPanelActive(gamePanel, true);
        SetPanelActive(shopPanel, true);
        SetPanelActive(optionsPanel, false); // Options is overlay
        SetPanelActive(tutorialPanel1, true);
        SetPanelActive(tutorialPanel2, true);
        SetPanelActive(countdownPanel, true);
        SetPanelActive(quitPanel, true);
        SetPanelActive(chillZonePanel, true);
    }

    /// <summary>
    /// Create the chill zone panel if not assigned.
    /// </summary>
    private void EnsureChillZonePanelExists()
    {
        if (chillZonePanel != null) return;

        // Create panel
        GameObject panelObj = new GameObject("ChillZonePanel");
        panelObj.transform.SetParent(mainCanvas.transform, false);

        chillZonePanel = panelObj.AddComponent<RectTransform>();
        chillZonePanel.anchorMin = Vector2.zero;
        chillZonePanel.anchorMax = Vector2.one;
        chillZonePanel.offsetMin = Vector2.zero;
        chillZonePanel.offsetMax = Vector2.zero;

        // Background
        UnityEngine.UI.Image bg = panelObj.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0.05f, 0.08f, 0.15f, 0.98f);

        // Title text
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(panelObj.transform, false);

        RectTransform titleRT = titleObj.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 0.65f);
        titleRT.anchorMax = new Vector2(0.5f, 0.75f);
        titleRT.sizeDelta = new Vector2(600f, 120f);
        titleRT.anchoredPosition = Vector2.zero;

        TMPro.TMP_Text titleText = titleObj.AddComponent<TMPro.TextMeshProUGUI>();
        titleText.text = "☕ CHILL ZONE ☕";
        titleText.fontSize = 64f;
        titleText.fontStyle = TMPro.FontStyles.Bold;
        titleText.color = new Color(0.7f, 0.85f, 1f);
        titleText.alignment = TMPro.TextAlignmentOptions.Center;

        // Subtitle text
        GameObject subtitleObj = new GameObject("SubtitleText");
        subtitleObj.transform.SetParent(panelObj.transform, false);

        RectTransform subtitleRT = subtitleObj.AddComponent<RectTransform>();
        subtitleRT.anchorMin = new Vector2(0.5f, 0.5f);
        subtitleRT.anchorMax = new Vector2(0.5f, 0.6f);
        subtitleRT.sizeDelta = new Vector2(500f, 80f);
        subtitleRT.anchoredPosition = Vector2.zero;

        TMPro.TMP_Text subtitleText = subtitleObj.AddComponent<TMPro.TextMeshProUGUI>();
        subtitleText.text = "Take a breath.\nThe boss fight awaits...";
        subtitleText.fontSize = 32f;
        subtitleText.color = new Color(0.6f, 0.7f, 0.8f);
        subtitleText.alignment = TMPro.TextAlignmentOptions.Center;

        // Fight Boss button
        GameObject buttonObj = new GameObject("FightBossButton");
        buttonObj.transform.SetParent(panelObj.transform, false);

        RectTransform buttonRT = buttonObj.AddComponent<RectTransform>();
        buttonRT.anchorMin = new Vector2(0.5f, 0.25f);
        buttonRT.anchorMax = new Vector2(0.5f, 0.35f);
        buttonRT.sizeDelta = new Vector2(350f, 80f);
        buttonRT.anchoredPosition = Vector2.zero;

        UnityEngine.UI.Image buttonBg = buttonObj.AddComponent<UnityEngine.UI.Image>();
        buttonBg.color = new Color(0.8f, 0.2f, 0.25f);

        UnityEngine.UI.Button button = buttonObj.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = buttonBg;
        button.onClick.AddListener(OnFightBossPressed);

        // Button text
        GameObject buttonTextObj = new GameObject("Text");
        buttonTextObj.transform.SetParent(buttonObj.transform, false);

        RectTransform btnTextRT = buttonTextObj.AddComponent<RectTransform>();
        btnTextRT.anchorMin = Vector2.zero;
        btnTextRT.anchorMax = Vector2.one;
        btnTextRT.offsetMin = Vector2.zero;
        btnTextRT.offsetMax = Vector2.zero;

        TMPro.TMP_Text buttonText = buttonTextObj.AddComponent<TMPro.TextMeshProUGUI>();
        buttonText.text = "FIGHT BOSS";
        buttonText.fontSize = 36f;
        buttonText.fontStyle = TMPro.FontStyles.Bold;
        buttonText.color = Color.white;
        buttonText.alignment = TMPro.TextAlignmentOptions.Center;

        Debug.Log("[SceneFlowManager] Chill Zone panel created");
    }
    
    #endregion
    
    #region Panel Helpers
    
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
            case GameState.Win:
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
        
        // Hide current tutorial panel
        RectTransform currentTutorial = CurrentState == GameState.Tutorial1 ? tutorialPanel1 : tutorialPanel2;
        yield return HidePanel(currentTutorial);
        
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

        // Show tutorials
        CurrentState = GameState.Tutorial1;
        yield return ShowPanel(tutorialPanel1);
    }
    
    private IEnumerator CountdownSequence()
    {
        Debug.Log("CountdownSequence started");
        SetPanelPosition(countdownPanel, 0);
        countdownPanel.localScale = Vector3.one;
        
        string[] steps = { "3", "2", "1", "GO!" };
        
        foreach (string step in steps)
        {
            if (countdownText != null)
            {
                countdownText.text = step;
                
                // Play sound
                if (step == "GO!")
                    AudioManager.Instance?.PlayCountdownGo();
                else
                    AudioManager.Instance?.PlayCountdownBeep();
                
                // Pop animation
                yield return CountdownPop();
            }
            
            yield return new WaitForSeconds(countdownStepDuration);
        }
        
        // Hide countdown and start game
        Debug.Log("Countdown complete - starting game");
        yield return HidePanel(countdownPanel);
        CurrentState = GameState.Game;
        
        // Start game music
        Debug.Log("Starting game music...");
        AudioManager.Instance?.PlayGameMusic();
        
        // Activate gameplay (starts timer, enables scoring)
        Debug.Log("Activating game...");
        GameManager.Instance?.ActivateGame();
        
        // NOW process matches - this is where the freebies happen!
        Debug.Log("Starting match processing - let the freebies flow!");
        FindFirstObjectByType<GridManager>()?.StartMatchProcessing();
        
        Debug.Log("CountdownSequence complete");
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
        yield return HidePanel(tutorialPanel1);
        CurrentState = GameState.Tutorial2;
        yield return ShowPanel(tutorialPanel2);
    }
    
    public void OnTutorial2GotThisPressed()
    {
        HandleButton(GameState.Tutorial2, () => StartCoroutine(Tutorial2ToCountdown()));
    }
    
    private IEnumerator Tutorial2ToCountdown()
    {
        yield return HidePanel(tutorialPanel2);
        CurrentState = GameState.Countdown;
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
    
    public void OnGameEnded(bool won)
    {
        Debug.Log($"SceneFlowManager: Game ended - {(won ? "WIN" : "LOSE")}");
        if (won)
        {
            CurrentState = GameState.Win;
        }
    }

    /// <summary>
    /// Transition from win screen to shop (called by UIManager on Continue press).
    /// </summary>
    public void TransitionToShop()
    {
        if (CurrentState != GameState.Win && CurrentState != GameState.Game)
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
    
    public bool IsInGameplay() => CurrentState == GameState.Game || CurrentState == GameState.BossFight;

    /// <summary>
    /// Transition to Chill Zone (called by ShopManager when all rounds complete).
    /// </summary>
    public void TransitionToChillZone()
    {
        if (CurrentState != GameState.Shop)
        {
            Debug.LogWarning($"TransitionToChillZone called from invalid state: {CurrentState}");
            return;
        }

        StartCoroutine(TransitionToChillZoneSequence());
    }

    private IEnumerator TransitionToChillZoneSequence()
    {
        Debug.Log("TransitionToChillZoneSequence - entering chill zone");

        // Stop shop music
        AudioManager.Instance?.StopMusic();

        // Hide shop UI
        ShopManager.Instance?.HideShop();

        // Slide shop panel out, chill zone in
        yield return SlideTransition(shopPanel, chillZonePanel, slideLeft: true);

        CurrentState = GameState.ChillZone;
        SetPanelPosition(shopPanel, screenWidth);

        // Play chill zone music if available
        if (chillZoneMusic != null)
        {
            AudioManager.Instance?.PlayMusic(chillZoneMusic);
        }
        else
        {
            AudioManager.Instance?.PlayMenuMusic();
        }

        Debug.Log("Now in ChillZone state");
    }

    /// <summary>
    /// Fight Boss button pressed in chill zone.
    /// </summary>
    public void OnFightBossPressed()
    {
        if (CurrentState != GameState.ChillZone)
        {
            Debug.LogWarning($"OnFightBossPressed called from invalid state: {CurrentState}");
            return;
        }

        AudioManager.Instance?.PlayButtonClick();
        StartCoroutine(TransitionToBossFightSequence());
    }

    private IEnumerator TransitionToBossFightSequence()
    {
        Debug.Log("TransitionToBossFightSequence - starting boss fight");

        // Stop chill zone music
        AudioManager.Instance?.StopMusic();

        // Notify CampaignManager to start boss fight
        CampaignManager.Instance?.StartBossFight();

        // Spawn grid for boss fight
        GridManager gridManager = FindFirstObjectByType<GridManager>();
        gridManager?.SpawnGridOnly();

        // Slide chill zone out, game panel in
        yield return SlideTransition(chillZonePanel, gamePanel, slideLeft: true);

        SetPanelPosition(chillZonePanel, screenWidth);

        // Run countdown then start boss fight
        CurrentState = GameState.Countdown;
        yield return BossFightCountdownSequence();
    }

    private IEnumerator BossFightCountdownSequence()
    {
        Debug.Log("BossFightCountdownSequence started");
        SetPanelPosition(countdownPanel, 0);
        countdownPanel.localScale = Vector3.one;

        string[] steps = { "3", "2", "1", "FIGHT!" };

        foreach (string step in steps)
        {
            if (countdownText != null)
            {
                countdownText.text = step;

                // Play sound
                if (step == "FIGHT!")
                    AudioManager.Instance?.PlayCountdownGo();
                else
                    AudioManager.Instance?.PlayCountdownBeep();

                // Pop animation
                yield return CountdownPop();
            }

            yield return new WaitForSeconds(countdownStepDuration);
        }

        // Hide countdown and start boss fight
        Debug.Log("Boss countdown complete - starting boss fight");
        yield return HidePanel(countdownPanel);
        CurrentState = GameState.BossFight;

        // Start boss fight music
        Debug.Log("Starting boss fight music...");
        AudioManager.Instance?.PlayGameMusic(); // TODO: Boss-specific music

        // Activate boss fight mode
        Debug.Log("Activating boss fight...");
        GameManager.Instance?.ActivateBossFight();

        // Start match processing
        Debug.Log("Starting match processing for boss fight!");
        FindFirstObjectByType<GridManager>()?.StartMatchProcessing();

        Debug.Log("BossFightCountdownSequence complete");
    }
    
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
