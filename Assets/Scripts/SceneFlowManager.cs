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
    [SerializeField] private RectTransform tutorialPanel1;
    [SerializeField] private RectTransform tutorialPanel2;
    [SerializeField] private RectTransform countdownPanel;
    [SerializeField] private RectTransform quitPanel; // "Thanks for playing" panel
    
    [Header("Transition Settings")]
    [SerializeField] private float transitionDuration = 0.4f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Loading Settings")]
    [SerializeField] private Slider loadingProgressBar;
    [SerializeField] private float fakeLoadDuration = 2f;
    
    [Header("Countdown Settings")]
    [SerializeField] private TMPro.TMP_Text countdownText;
    
    [Header("References")]
    [SerializeField] private Canvas mainCanvas;
    
    // Screen width for swipe calculations
    private float screenWidth;
    
    // Current state
    public enum GameState { Loading, MainMenu, Options, Game, Tutorial1, Tutorial2, Countdown, Quit }
    public GameState CurrentState { get; private set; }
    
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
        // Initialize all panels
        InitializePanels();
        
        // Start with loading screen
        StartCoroutine(LoadingSequence());
    }
    
    /// <summary>
    /// Get the canvas width for transitions.
    /// </summary>
    private float GetCanvasWidth()
    {
        if (mainCanvas != null)
        {
            RectTransform canvasRect = mainCanvas.GetComponent<RectTransform>();
            return canvasRect.rect.width;
        }
        return 1024f; // Default fallback
    }
    
    /// <summary>
    /// Set initial panel positions (all hidden except loading).
    /// </summary>
    private void InitializePanels()
    {
        // Hide all panels off-screen to the right
        SetPanelPosition(mainMenuPanel, screenWidth);
        SetPanelPosition(gamePanel, screenWidth);
        SetPanelPosition(optionsPanel, screenWidth);
        SetPanelPosition(tutorialPanel1, screenWidth);
        SetPanelPosition(tutorialPanel2, screenWidth);
        SetPanelPosition(countdownPanel, screenWidth);
        SetPanelPosition(quitPanel, screenWidth);
        
        // Loading panel starts visible (centered)
        SetPanelPosition(loadingPanel, 0);
        
        // Make sure panels are active (just positioned off-screen)
        SetPanelActive(loadingPanel, true);
        SetPanelActive(mainMenuPanel, true);
        SetPanelActive(gamePanel, true);
        SetPanelActive(optionsPanel, false); // Options is overlay, start hidden
        SetPanelActive(tutorialPanel1, true);
        SetPanelActive(tutorialPanel2, true);
        SetPanelActive(countdownPanel, true);
        SetPanelActive(quitPanel, true);
    }
    
    private void SetPanelPosition(RectTransform panel, float xPos)
    {
        if (panel != null)
        {
            Vector2 pos = panel.anchoredPosition;
            pos.x = xPos;
            panel.anchoredPosition = pos;
        }
    }
    
    private void SetPanelActive(RectTransform panel, bool active)
    {
        if (panel != null)
        {
            panel.gameObject.SetActive(active);
        }
    }
    
    /// <summary>
    /// Fake loading sequence with progress bar.
    /// </summary>
    private IEnumerator LoadingSequence()
    {
        CurrentState = GameState.Loading;
        Debug.Log("LoadingSequence started");
        
        float elapsed = 0f;
        while (elapsed < fakeLoadDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / fakeLoadDuration;
            
            if (loadingProgressBar != null)
            {
                loadingProgressBar.value = progress;
            }
            
            yield return null;
        }
        
        Debug.Log("LoadingSequence complete - transitioning to MainMenu");
        
        // Transition to main menu
        yield return StartCoroutine(TransitionToPanel(loadingPanel, mainMenuPanel, true));
        CurrentState = GameState.MainMenu;
        Debug.Log($"Now in MainMenu state. CurrentState = {CurrentState}");
    }
    
    /// <summary>
    /// Smooth swipe transition between panels.
    /// </summary>
    private IEnumerator TransitionToPanel(RectTransform fromPanel, RectTransform toPanel, bool slideLeft)
    {
        float direction = slideLeft ? -1f : 1f;
        
        // Position incoming panel
        SetPanelPosition(toPanel, -direction * screenWidth);
        
        float elapsed = 0f;
        Vector2 fromStart = fromPanel.anchoredPosition;
        Vector2 toStart = toPanel.anchoredPosition;
        
        Vector2 fromEnd = new Vector2(direction * screenWidth, fromStart.y);
        Vector2 toEnd = new Vector2(0, toStart.y);
        
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = transitionCurve.Evaluate(elapsed / transitionDuration);
            
            fromPanel.anchoredPosition = Vector2.Lerp(fromStart, fromEnd, t);
            toPanel.anchoredPosition = Vector2.Lerp(toStart, toEnd, t);
            
            yield return null;
        }
        
        // Snap to final positions
        fromPanel.anchoredPosition = fromEnd;
        toPanel.anchoredPosition = toEnd;
    }
    
    /// <summary>
    /// Fade transition for overlays (options panel).
    /// </summary>
    private IEnumerator FadePanel(RectTransform panel, bool fadeIn)
    {
        Debug.Log($"FadePanel called - fadeIn: {fadeIn}, panel: {(panel != null ? panel.name : "NULL")}");
        
        if (panel == null)
        {
            Debug.LogError("FadePanel: panel is null!");
            yield break;
        }
        
        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
            Debug.Log("Added CanvasGroup to panel");
        }
        
        float startAlpha = fadeIn ? 0f : 1f;
        float endAlpha = fadeIn ? 1f : 0f;
        
        if (fadeIn)
        {
            panel.gameObject.SetActive(true);
            // Make sure it's centered (not off-screen)
            panel.anchoredPosition = Vector2.zero;
            Debug.Log($"Panel activated and centered. Position: {panel.anchoredPosition}");
        }
        
        float elapsed = 0f;
        while (elapsed < transitionDuration * 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (transitionDuration * 0.5f);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            yield return null;
        }
        
        canvasGroup.alpha = endAlpha;
        Debug.Log($"FadePanel complete. Alpha: {endAlpha}");
        
        if (!fadeIn)
        {
            panel.gameObject.SetActive(false);
        }
    }
    
    // === PUBLIC METHODS (called by buttons) ===
    
    /// <summary>
    /// Called by Play button on main menu.
    /// </summary>
    public void OnPlayPressed()
    {
        Debug.Log($"OnPlayPressed called! CurrentState = {CurrentState}");
        if (CurrentState != GameState.MainMenu)
        {
            Debug.LogWarning($"OnPlayPressed ignored - not in MainMenu state (currently {CurrentState})");
            return;
        }
        StartCoroutine(PlaySequence());
    }
    
    private IEnumerator PlaySequence()
    {
        // Transition to game panel first
        yield return StartCoroutine(TransitionToPanel(mainMenuPanel, gamePanel, true));
        CurrentState = GameState.Tutorial1;
        
        // Show tutorial 1 (slide up from bottom or fade in)
        yield return StartCoroutine(ShowTutorialPanel(tutorialPanel1));
    }
    
    /// <summary>
    /// Called by Options button on main menu.
    /// </summary>
    public void OnOptionsPressed()
    {
        Debug.Log($"OnOptionsPressed called! CurrentState = {CurrentState}");
        if (CurrentState != GameState.MainMenu)
        {
            Debug.LogWarning($"OnOptionsPressed ignored - not in MainMenu state");
            return;
        }
        StartCoroutine(FadePanel(optionsPanel, true));
        CurrentState = GameState.Options;
    }
    
    /// <summary>
    /// Called by Close button on options panel.
    /// </summary>
    public void OnOptionsClosePressed()
    {
        if (CurrentState != GameState.Options) return;
        StartCoroutine(CloseOptionsSequence());
    }
    
    private IEnumerator CloseOptionsSequence()
    {
        yield return StartCoroutine(FadePanel(optionsPanel, false));
        CurrentState = GameState.MainMenu;
    }
    
    /// <summary>
    /// Called by Quit button on main menu.
    /// </summary>
    public void OnQuitPressed()
    {
        Debug.Log($"OnQuitPressed called! CurrentState = {CurrentState}");
        if (CurrentState != GameState.MainMenu)
        {
            Debug.LogWarning("OnQuitPressed ignored - not in MainMenu state");
            return;
        }
        StartCoroutine(QuitSequence());
    }
    
    private IEnumerator QuitSequence()
    {
        yield return StartCoroutine(TransitionToPanel(mainMenuPanel, quitPanel, true));
        CurrentState = GameState.Quit;
        
        // Wait a moment then open itch.io
        yield return new WaitForSeconds(1.5f);
        
        // Open itch.io page (replace with your actual URL)
        Application.OpenURL("https://itch.io/"); // TODO: Replace with your game's itch.io URL
        
        // On standalone builds, quit the app
        #if !UNITY_WEBGL
        Application.Quit();
        #endif
    }
    
    /// <summary>
    /// Called by "Ok" button on Tutorial 1.
    /// </summary>
    public void OnTutorial1OkPressed()
    {
        if (CurrentState != GameState.Tutorial1) return;
        StartCoroutine(Tutorial1ToTutorial2());
    }
    
    private IEnumerator Tutorial1ToTutorial2()
    {
        yield return StartCoroutine(HideTutorialPanel(tutorialPanel1));
        CurrentState = GameState.Tutorial2;
        yield return StartCoroutine(ShowTutorialPanel(tutorialPanel2));
    }
    
    /// <summary>
    /// Called by "I've got this" button on Tutorial 2.
    /// </summary>
    public void OnTutorial2GotThisPressed()
    {
        if (CurrentState != GameState.Tutorial2) return;
        StartCoroutine(Tutorial2ToCountdown());
    }
    
    private IEnumerator Tutorial2ToCountdown()
    {
        yield return StartCoroutine(HideTutorialPanel(tutorialPanel2));
        CurrentState = GameState.Countdown;
        yield return StartCoroutine(CountdownSequence());
    }
    
    /// <summary>
    /// Show tutorial panel with animation.
    /// </summary>
    private IEnumerator ShowTutorialPanel(RectTransform panel)
    {
        // Scale up from center
        panel.localScale = Vector3.zero;
        SetPanelPosition(panel, 0); // Center it
        
        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = transitionCurve.Evaluate(elapsed / transitionDuration);
            panel.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);
            yield return null;
        }
        
        panel.localScale = Vector3.one;
    }
    
    /// <summary>
    /// Hide tutorial panel with animation.
    /// </summary>
    private IEnumerator HideTutorialPanel(RectTransform panel)
    {
        float elapsed = 0f;
        while (elapsed < transitionDuration * 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (transitionDuration * 0.5f);
            panel.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
            yield return null;
        }
        
        panel.localScale = Vector3.zero;
        SetPanelPosition(panel, screenWidth); // Move off-screen
    }
    
    /// <summary>
    /// Countdown sequence: 3, 2, 1, GO!
    /// </summary>
    private IEnumerator CountdownSequence()
    {
        SetPanelPosition(countdownPanel, 0);
        countdownPanel.localScale = Vector3.one;
        
        string[] countdownSteps = { "3", "2", "1", "GO!" };
        
        foreach (string step in countdownSteps)
        {
            if (countdownText != null)
            {
                countdownText.text = step;
                
                // Pop animation
                countdownPanel.localScale = Vector3.one * 1.5f;
                float elapsed = 0f;
                while (elapsed < 0.15f)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / 0.15f;
                    countdownPanel.localScale = Vector3.Lerp(Vector3.one * 1.5f, Vector3.one, t);
                    yield return null;
                }
            }
            
            yield return new WaitForSeconds(0.7f);
        }
        
        // Hide countdown, start game
        yield return StartCoroutine(HideTutorialPanel(countdownPanel));
        CurrentState = GameState.Game;
        
        // Notify GameManager to start
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartNewGame();
        }
    }
    
    /// <summary>
    /// Return to main menu (e.g., after game over).
    /// </summary>
    public void ReturnToMainMenu()
    {
        StartCoroutine(ReturnToMainMenuSequence());
    }
    
    private IEnumerator ReturnToMainMenuSequence()
    {
        yield return StartCoroutine(TransitionToPanel(gamePanel, mainMenuPanel, false));
        CurrentState = GameState.MainMenu;
        
        // Reset game panel position for next play
        SetPanelPosition(gamePanel, screenWidth);
    }
    
    /// <summary>
    /// Called by GameManager when the game ends (win or lose).
    /// Syncs the flow state with game state.
    /// </summary>
    public void OnGameEnded(bool won)
    {
        // Update our state to reflect game is no longer active
        // The actual win/lose UI is handled by UIManager
        Debug.Log($"SceneFlowManager: Game ended - {(won ? "WIN" : "LOSE")}");
        
        // We stay in Game state but mark that the game session is over
        // Player can then choose to restart or return to menu
    }
    
    /// <summary>
    /// Check if we're in a playable game state.
    /// </summary>
    public bool IsInGameplay()
    {
        return CurrentState == GameState.Game;
    }
    
    /// <summary>
    /// Skip tutorials and go straight to game (for testing or replay).
    /// </summary>
    public void StartGameImmediate()
    {
        if (CurrentState != GameState.MainMenu) return;
        StartCoroutine(StartGameImmediateSequence());
    }
    
    private IEnumerator StartGameImmediateSequence()
    {
        yield return StartCoroutine(TransitionToPanel(mainMenuPanel, gamePanel, true));
        CurrentState = GameState.Game;
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartNewGame();
        }
    }
}
