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
    [SerializeField] private RectTransform difficultyPanel;
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
    [SerializeField] private float fakeLoadDuration = 2f;
    
    [Header("Countdown Settings")]
    [SerializeField] private TMPro.TMP_Text countdownText;
    [SerializeField] private float countdownStepDuration = 0.7f;
    
    [Header("References")]
    [SerializeField] private Canvas mainCanvas;
    
    // Screen width for swipe calculations
    private float screenWidth;
    
    // Current state
    public enum GameState { Loading, MainMenu, DifficultySelect, Options, Game, Tutorial1, Tutorial2, Countdown, Quit }
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
        // Position all panels off-screen except loading
        RectTransform[] offScreenPanels = { mainMenuPanel, difficultyPanel, gamePanel, optionsPanel, 
            tutorialPanel1, tutorialPanel2, countdownPanel, quitPanel };
        
        foreach (var panel in offScreenPanels)
            SetPanelPosition(panel, screenWidth);
        
        SetPanelPosition(loadingPanel, 0);
        
        // Set active states
        SetPanelActive(loadingPanel, true);
        SetPanelActive(mainMenuPanel, true);
        SetPanelActive(difficultyPanel, false); // Difficulty is overlay popup
        SetPanelActive(gamePanel, true);
        SetPanelActive(optionsPanel, false); // Options is overlay
        SetPanelActive(tutorialPanel1, true);
        SetPanelActive(tutorialPanel2, true);
        SetPanelActive(countdownPanel, true);
        SetPanelActive(quitPanel, true);
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
            GameState.DifficultySelect => difficultyPanel,
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
        return state == GameState.DifficultySelect || state == GameState.Options;
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
            case GameState.DifficultySelect:
                StartCoroutine(CloseOverlayToMainMenu(difficultyPanel));
                break;
                
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
    
    private IEnumerator LoadingSequence()
    {
        CurrentState = GameState.Loading;
        Debug.Log("LoadingSequence started");
        
        // Animate loading bar
        float elapsed = 0f;
        while (elapsed < fakeLoadDuration)
        {
            elapsed += Time.deltaTime;
            if (loadingProgressBar != null)
                loadingProgressBar.value = elapsed / fakeLoadDuration;
            yield return null;
        }
        
        Debug.Log("LoadingSequence complete - transitioning to MainMenu");
        
        // Start menu music
        PlayAudio(() => AudioManager.Instance?.PlayMenuMusic());
        
        // Transition to main menu
        yield return SlideTransition(loadingPanel, mainMenuPanel, slideLeft: true);
        CurrentState = GameState.MainMenu;
        Debug.Log($"Now in MainMenu state");
    }
    
    private IEnumerator PlaySequence()
    {
        // Stop menu music
        AudioManager.Instance?.StopMusic();
        
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
    /// Play button pressed - show difficulty selection.
    /// </summary>
    public void OnPlayPressed()
    {
        Debug.Log($"OnPlayPressed called! CurrentState = {CurrentState}");
        HandleButton(GameState.MainMenu, () =>
        {
            StartCoroutine(FadeTransition(difficultyPanel, fadeIn: true));
            CurrentState = GameState.DifficultySelect;
        });
    }
    
    // Legacy method - now calls GoBack()
    public void OnDifficultyBackPressed() => GoBack();
    
    /// <summary>
    /// Easy difficulty selected.
    /// </summary>
    public void OnEasyPressed()
    {
        HandleButton(GameState.DifficultySelect, () =>
        {
            GameManager.Instance?.SetDifficulty(GameManager.DifficultyLevel.Easy);
            StartCoroutine(StartGameFromDifficulty());
        });
    }
    
    /// <summary>
    /// Medium difficulty selected.
    /// </summary>
    public void OnMediumPressed()
    {
        HandleButton(GameState.DifficultySelect, () =>
        {
            GameManager.Instance?.SetDifficulty(GameManager.DifficultyLevel.Medium);
            StartCoroutine(StartGameFromDifficulty());
        });
    }
    
    /// <summary>
    /// Hard difficulty selected.
    /// </summary>
    public void OnHardPressed()
    {
        HandleButton(GameState.DifficultySelect, () =>
        {
            GameManager.Instance?.SetDifficulty(GameManager.DifficultyLevel.Hard);
            StartCoroutine(StartGameFromDifficulty());
        });
    }
    
    /// <summary>
    /// Start the game after difficulty is selected.
    /// </summary>
    private IEnumerator StartGameFromDifficulty()
    {
        // Hide difficulty panel
        yield return FadeTransition(difficultyPanel, fadeIn: false);
        
        // Continue with normal play sequence
        yield return PlaySequence();
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
