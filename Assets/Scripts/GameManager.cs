using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Manages game state: scoring, motivation meter, win/lose conditions, difficulty settings.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    #region Game Settings

    [System.Serializable]
    public class GameSettings
    {
        [Header("Grid Settings")]
        public int gridSize = 5;

        [Header("Win Condition")]
        public int winScore = 100;

        [Header("Tile Weights (must sum to 1.0)")]
        [Range(0, 1)] public float weight0 = 0.12f;
        [Range(0, 1)] public float weight1 = 0.24f;
        [Range(0, 1)] public float weight2 = 0.26f;
        [Range(0, 1)] public float weight3 = 0.20f;
        [Range(0, 1)] public float weight4 = 0.12f;
        [Range(0, 1)] public float weight5 = 0.05f;
        [Range(0, 1)] public float weight6 = 0.01f;

        public float[] GetWeights()
        {
            return new float[] { weight0, weight1, weight2, weight3, weight4, weight5, weight6 };
        }
    }

    [Header("Game Settings")]
    [SerializeField] private GameSettings gameSettings = new GameSettings();

    #endregion
    
    public int WinScore => gameSettings.winScore;
    [SerializeField] private float gameDuration = 60f;
    [SerializeField] private float postWinDelay = 0.5f;
    
    [Header("Scoring")]
    [SerializeField] private int baseMatchScore = 10;
    
    [Header("Multiplier Settings")]
    [SerializeField] private float multiplierDuration = 10f;
    [SerializeField] private float multiplierDrainRate = 1f;
    [SerializeField] private float multiplierIncrement = 0.25f;
    [SerializeField] private float startingMultiplier = 1.25f;
    [SerializeField] private float maxMultiplier = 3f;
    [SerializeField] private float streakTimeout = 10f;
    
    [Header("Hot Streak Mode")]
    [SerializeField] private float hotStreakDuration = 10f;
    [SerializeField] private float hotStreakMultiplier = 5f;
    
    [Header("References")]
    [SerializeField] private UIManager uiManager;
    
    // Current state
    public int Score { get; private set; }
    public float TimeRemaining { get; private set; }
    public float GameDuration => gameDuration;
    public bool IsGameActive { get; private set; }
    public bool IsProcessing { get; set; }
    public bool IsSolveAnimationPlaying { get; set; }
    
    // Multiplier state
    private int solveCount = 0;
    private float currentMultiplier = 1f;
    private float multiplierTimer = 0f;
    private bool multiplierActive = false;
    private float timeSinceLastSolve = 0f;
    private float maxMultiplierReached = 1f;
    
    // Hot Streak state
    private bool hotStreakActive = false;
    private float hotStreakTimer = 0f;
    
    // Public accessors for UI
    public bool IsMultiplierActive => multiplierActive;
    public float CurrentMultiplier => currentMultiplier;
    public float MultiplierTimer => multiplierTimer;
    public float MultiplierDuration => multiplierDuration;
    public bool IsHotStreakActive => hotStreakActive;
    public float HotStreakTimer => hotStreakTimer;
    public float HotStreakDuration => hotStreakDuration;
    public float MaxMultiplierReached => maxMultiplierReached;
    
    // Events for UI updates
    public event Action<int, int> OnScoreChanged;
    public event Action<float> OnTimeChanged;
    public event Action<bool, float, float> OnMultiplierChanged;
    public event Action OnGameWon;
    public event Action OnGameLost;
    public event Action OnHotStreakStarted;
    public event Action<float> OnHotStreakTimerChanged; // passes remaining time
    public event Action OnHotStreakEnded;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    private void Start()
    {
        if (SceneFlowManager.Instance == null)
        {
            Debug.Log("GameManager: No SceneFlowManager found - auto-starting for testing");
            StartNewGame();
        }
        else
        {
            IsGameActive = false;
        }
    }
    
    private void Update()
    {
        if (!IsGameActive) return;
        if (IsSolveAnimationPlaying) return;
        
        // Hot Streak mode - pause main timer, run hot streak timer
        if (hotStreakActive)
        {
            hotStreakTimer -= Time.deltaTime;
            OnHotStreakTimerChanged?.Invoke(hotStreakTimer);
            
            if (hotStreakTimer <= 0f)
            {
                EndHotStreak();
            }
            return; // Skip normal timer drain during hot streak
        }
        
        if (!IsProcessing)
        {
            DrainTime(Time.deltaTime);
        }
        
        if (multiplierActive)
        {
            DrainMultiplierTimer(Time.deltaTime);
        }
        else if (solveCount > 0)
        {
            timeSinceLastSolve += Time.deltaTime;
            if (timeSinceLastSolve >= streakTimeout)
            {
                solveCount = 0;
                timeSinceLastSolve = 0f;
                Debug.Log("<color=red>Streak timeout!</color> Solve count reset.");
            }
        }
    }
    
    #region Settings Accessors

    /// <summary>
    /// Get the tile spawn weights.
    /// </summary>
    public float[] GetCurrentWeights() => gameSettings.GetWeights();

    /// <summary>
    /// Get the grid size.
    /// </summary>
    public int GetCurrentGridSize() => gameSettings.gridSize;

    #endregion
    
    #region Game Flow
    
    /// <summary>
    /// Start or restart the game.
    /// </summary>
    public void StartNewGame()
    {
        Score = 0;
        TimeRemaining = gameDuration;
        IsGameActive = true;
        IsProcessing = false;
        IsSolveAnimationPlaying = false;
        
        solveCount = 0;
        currentMultiplier = 1f;
        multiplierTimer = 0f;
        multiplierActive = false;
        timeSinceLastSolve = 0f;
        hotStreakActive = false;
        hotStreakTimer = 0f;
        maxMultiplierReached = 1f;

        OnScoreChanged?.Invoke(Score, 0);
        OnTimeChanged?.Invoke(TimeRemaining);
        OnMultiplierChanged?.Invoke(false, 1f, 0f);

        // Refresh UI for new difficulty settings
        if (uiManager != null)
            uiManager.RefreshTargetScore();

        GridManager gridManager = FindFirstObjectByType<GridManager>();
        if (gridManager != null)
        {
            gridManager.ResetGame();
        }
        
        // Reset avatar to default state
        AvatarManager.Instance?.ResetToDefault();
        
        Debug.Log($"Game started! Grid: {gameSettings.gridSize}x{gameSettings.gridSize}, Target: {WinScore} BP");
    }
    
    /// <summary>
    /// Deactivate the game (used when returning to main menu).
    /// </summary>
    public void DeactivateGame()
    {
        IsGameActive = false;
        Debug.Log("Game deactivated");
    }
    
    /// <summary>
    /// Activate the game without resetting the grid (used when grid was pre-spawned).
    /// </summary>
    public void ActivateGame()
    {
        Score = 0;
        TimeRemaining = gameDuration;
        IsGameActive = true;
        IsProcessing = false;
        IsSolveAnimationPlaying = false;
        
        solveCount = 0;
        currentMultiplier = 1f;
        multiplierTimer = 0f;
        multiplierActive = false;
        timeSinceLastSolve = 0f;
        hotStreakActive = false;
        hotStreakTimer = 0f;
        maxMultiplierReached = 1f;

        OnScoreChanged?.Invoke(Score, 0);
        OnTimeChanged?.Invoke(TimeRemaining);
        OnMultiplierChanged?.Invoke(false, 1f, 0f);

        // Refresh UI for new difficulty settings
        if (uiManager != null)
            uiManager.RefreshTargetScore();

        // Reset avatar to default state
        AvatarManager.Instance?.ResetToDefault();

        Debug.Log($"Game activated! Grid: {gameSettings.gridSize}x{gameSettings.gridSize}, Target: {WinScore} BP");
    }
    
    public void OnCascadeStart()
    {
        IsProcessing = true;
    }
    
    public void OnCascadeEnd()
    {
        IsProcessing = false;
    }
    
    public void OnMatchCleared(int tilesCleared, int rowsMatched, int columnsMatched)
    {
        if (!IsGameActive) return;
        
        int linesCleared = rowsMatched + columnsMatched;
        
        for (int i = 0; i < linesCleared; i++)
        {
            ProcessSingleSolve();
        }
    }
    
    #endregion
    
    #region Scoring
    
    private void ProcessSingleSolve()
    {
        // During Hot Streak, use special scoring
        if (hotStreakActive)
        {
            ProcessHotStreakSolve();
            return;
        }
        
        solveCount++;
        timeSinceLastSolve = 0f;
        
        int pointsAwarded = 0;
        int bonusSeconds = 0;
        
        if (solveCount == 1)
        {
            pointsAwarded = baseMatchScore;
            Debug.Log($"<color=green>Solve #1:</color> +{pointsAwarded} pts (base)");
        }
        else if (solveCount == 2)
        {
            pointsAwarded = baseMatchScore;
            ActivateMultiplierBar();
            Debug.Log($"<color=green>Solve #2:</color> +{pointsAwarded} pts | <color=yellow>MULTIPLIER ACTIVATED (x{currentMultiplier:F2} ready)</color>");
        }
        else
        {
            bonusSeconds = Mathf.FloorToInt(multiplierTimer);
            int multipliedScore = Mathf.RoundToInt(baseMatchScore * currentMultiplier);
            pointsAwarded = multipliedScore + bonusSeconds;
            
            Debug.Log($"<color=green>Solve #{solveCount}:</color> ({baseMatchScore} × {currentMultiplier:F2}) + {bonusSeconds} bonus = <color=cyan>+{pointsAwarded} pts</color>");

            currentMultiplier += multiplierIncrement;
            maxMultiplierReached = Mathf.Max(maxMultiplierReached, currentMultiplier);
            
            // Check if we've exceeded the max - trigger Hot Streak!
            if (currentMultiplier > maxMultiplier)
            {
                StartCoroutine(TriggerHotStreak());
                return; // Don't process normal scoring, hot streak handles it
            }
            
            multiplierTimer = multiplierDuration;
            OnMultiplierChanged?.Invoke(multiplierActive, currentMultiplier, multiplierTimer);
        }
        
        Score += pointsAwarded;
        OnScoreChanged?.Invoke(Score, pointsAwarded);

        // Check for win condition
        CheckWinCondition();
    }
    
    private void ActivateMultiplierBar()
    {
        multiplierActive = true;
        multiplierTimer = multiplierDuration;
        currentMultiplier = startingMultiplier;
        maxMultiplierReached = Mathf.Max(maxMultiplierReached, currentMultiplier);

        OnMultiplierChanged?.Invoke(true, currentMultiplier, multiplierTimer);
    }
    
    private void DrainMultiplierTimer(float deltaTime)
    {
        multiplierTimer -= multiplierDrainRate * deltaTime;
        
        OnMultiplierChanged?.Invoke(multiplierActive, currentMultiplier, multiplierTimer);
        
        if (multiplierTimer <= 0f)
        {
            DeactivateMultiplierBar();
        }
    }
    
    private void DeactivateMultiplierBar()
    {
        multiplierActive = false;
        multiplierTimer = 0f;
        currentMultiplier = 1f;
        solveCount = 0;

        OnMultiplierChanged?.Invoke(false, 1f, 0f);

        Debug.Log("<color=red>Multiplier expired!</color> Streak reset.");
    }

    /// <summary>
    /// Check if player has reached the win score and trigger win if so.
    /// </summary>
    private void CheckWinCondition()
    {
        if (Score >= WinScore && IsGameActive)
        {
            Debug.Log($"<color=cyan>*** WIN THRESHOLD REACHED! ***</color> Score: {Score}/{WinScore}");
            StartCoroutine(WinGameDelayed());
        }
    }

    #endregion
    
    #region Hot Streak Mode
    
    private IEnumerator TriggerHotStreak()
    {
        Debug.Log("<color=orange>🔥🔥🔥 HOT STREAK ACTIVATED! 🔥🔥🔥</color>");
        
        // Set hot streak state
        hotStreakActive = true;
        hotStreakTimer = hotStreakDuration;
        
        // Set multiplier to hot streak value
        currentMultiplier = hotStreakMultiplier;
        multiplierTimer = hotStreakDuration; // Sync with hot streak duration
        maxMultiplierReached = Mathf.Max(maxMultiplierReached, currentMultiplier);
        
        // Fire event for UI to show intro
        OnHotStreakStarted?.Invoke();
        
        // Trigger avatar hot streak mode
        AvatarManager.Instance?.OnHotStreakStart();
        
        // Also update multiplier display
        OnMultiplierChanged?.Invoke(true, currentMultiplier, multiplierTimer);
        
        yield return null; // Hot streak intro handled by UIManager
    }
    
    private void EndHotStreak()
    {
        Debug.Log("<color=gray>Hot Streak ended!</color>");
        
        hotStreakActive = false;
        hotStreakTimer = 0f;
        
        // Reset multiplier completely
        DeactivateMultiplierBar();
        
        // Fire event for UI cleanup
        OnHotStreakEnded?.Invoke();
        
        // Return avatar to default struggling state
        AvatarManager.Instance?.OnHotStreakEnd();
    }
    
    /// <summary>
    /// Process scoring during Hot Streak (called from ProcessSingleSolve when hot streak is active).
    /// </summary>
    private void ProcessHotStreakSolve()
    {
        int multipliedScore = Mathf.RoundToInt(baseMatchScore * hotStreakMultiplier);
        
        Debug.Log($"<color=orange>🔥 HOT STREAK SOLVE:</color> {baseMatchScore} × {hotStreakMultiplier:F0} = <color=cyan>+{multipliedScore} pts</color>");
        
        Score += multipliedScore;
        OnScoreChanged?.Invoke(Score, multipliedScore);

        // Multiplier stays fixed at x5 during hot streak
        OnMultiplierChanged?.Invoke(true, currentMultiplier, hotStreakTimer);

        // Check for win condition
        CheckWinCondition();
    }
    
    #endregion
    
    #region Timer
    
    private void DrainTime(float deltaTime)
    {
        TimeRemaining -= deltaTime;
        TimeRemaining = Mathf.Max(0f, TimeRemaining);
        
        OnTimeChanged?.Invoke(TimeRemaining);
        
        if (TimeRemaining <= 0f)
        {
            TimeUp();
        }
    }
    
    private void TimeUp()
    {
        IsGameActive = false;
        
        if (Score >= WinScore)
        {
            Debug.Log("<color=cyan>*** TIME'S UP - YOU WIN! ***</color>");
            SceneFlowManager.Instance?.OnGameEnded(true);
            OnGameWon?.Invoke();
        }
        else
        {
            Debug.Log($"<color=red>*** TIME'S UP - GAME OVER ***</color> Score: {Score}/{WinScore}");
            SceneFlowManager.Instance?.OnGameEnded(false);
            OnGameLost?.Invoke();
        }
    }
    
    private IEnumerator WinGameDelayed()
    {
        yield return new WaitForSeconds(postWinDelay);
        WinGame();
    }
    
    private void WinGame()
    {
        IsGameActive = false;
        Debug.Log($"<color=cyan>*** YOU WIN! ***</color> Score: {Score} | Time left: {TimeRemaining:F1}s");
        
        SceneFlowManager.Instance?.OnGameEnded(true);
        OnGameWon?.Invoke();
    }
    
    #endregion
}
