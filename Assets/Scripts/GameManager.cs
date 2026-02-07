using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages arcade game state: scoring, multiplier, hot streak, timer.
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

    [Header("Time Bonus")]
    [SerializeField] private float timeBonusPerMatch = 3f;

    [Header("Hot Streak Mode")]
    [SerializeField] private float hotStreakDuration = 10f;
    [SerializeField] private float hotStreakMultiplier = 5f;

    [Header("Debug Mode")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private int debugStartingBP = 500;

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

    // Cached effective values (from upgrades)
    private float effectiveMaxMultiplier;
    private float effectiveHotStreakThreshold;
    private float effectiveHotStreakMultiplier;
    private float effectiveHotStreakDuration;
    private float effectiveMultiplierIncrement;

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
    public event Action OnHotStreakStarted;
    public event Action<float> OnHotStreakTimerChanged; // passes remaining time
    public event Action OnHotStreakEnded;
    public event Action<int> OnEnhancedNumberBonus; // bonus BP from enhanced numbers
    public event Action<float> OnTimeBonus; // time added from snacks/upgrades
    
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

    /// <summary>
    /// Cache effective values (arcade mode uses hardcoded defaults, no upgrades).
    /// </summary>
    private void CacheEffectiveValues()
    {
        effectiveMaxMultiplier = maxMultiplier;
        effectiveHotStreakThreshold = maxMultiplier;
        effectiveHotStreakMultiplier = hotStreakMultiplier;
        effectiveHotStreakDuration = hotStreakDuration;
        effectiveMultiplierIncrement = multiplierIncrement;
    }

    #endregion
    
    #region Game Flow
    
    /// <summary>
    /// Resets all per-round state (score, multiplier, hot streak).
    /// </summary>
    private void ResetRoundState(float duration)
    {
        Score = 0;
        TimeRemaining = duration;
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

        // Reset avatar to default state
        AvatarManager.Instance?.ResetToDefault();
    }

    /// <summary>
    /// Fires standard UI update events after state reset.
    /// </summary>
    private void NotifyUIOfReset()
    {
        OnScoreChanged?.Invoke(Score, 0);
        OnTimeChanged?.Invoke(TimeRemaining);
        OnMultiplierChanged?.Invoke(multiplierActive, currentMultiplier, multiplierTimer);
    }

    /// <summary>
    /// Start or restart the game.
    /// </summary>
    public void StartNewGame()
    {
        CacheEffectiveValues();
        ResetRoundState(gameDuration);
        NotifyUIOfReset();

        GridManager gridManager = FindFirstObjectByType<GridManager>();
        if (gridManager != null)
        {
            gridManager.ResetGame();
        }

        Debug.Log($"Game started! Grid: {gameSettings.gridSize}x{gameSettings.gridSize}");
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
        CacheEffectiveValues();
        ResetRoundState(gameDuration);
        NotifyUIOfReset();

        Debug.Log($"Game activated! Grid: {gameSettings.gridSize}x{gameSettings.gridSize}");
    }
    
    public void OnCascadeStart()
    {
        IsProcessing = true;
    }
    
    public void OnCascadeEnd()
    {
        IsProcessing = false;
    }
    
    /// <summary>
    /// Called when a match is cleared. Original signature for backward compatibility.
    /// </summary>
    public void OnMatchCleared(int tilesCleared, int rowsMatched, int columnsMatched)
    {
        // Call the extended version with no tile values (no enhanced number bonuses)
        OnMatchCleared(tilesCleared, rowsMatched, columnsMatched, null);
    }

    /// <summary>
    /// Called when a match is cleared, with tile values for enhanced number bonuses.
    /// </summary>
    public void OnMatchCleared(int tilesCleared, int rowsMatched, int columnsMatched, List<int> tileValues)
    {
        if (!IsGameActive) return;

        int linesCleared = rowsMatched + columnsMatched;

        // Add time bonus for each line cleared
        if (linesCleared > 0)
        {
            AddTime(timeBonusPerMatch * linesCleared);
        }

        for (int i = 0; i < linesCleared; i++)
        {
            ProcessSingleSolve(tileValues);
        }
    }

    /// <summary>
    /// Add time to the game clock (e.g. as a reward for making matches).
    /// </summary>
    public void AddTime(float seconds)
    {
        if (!IsGameActive) return;
        TimeRemaining += seconds;
        OnTimeChanged?.Invoke(TimeRemaining);
        Debug.Log($"<color=cyan>+{seconds:F1}s added!</color> Timer: {TimeRemaining:F1}s");
    }

    #endregion

    #region Scoring

    /// <summary>
    /// Process a single solve (Make10), applying all upgrade and snack bonuses.
    /// </summary>
    private void ProcessSingleSolve(List<int> tileValues = null)
    {
        // During Hot Streak, use special scoring
        if (hotStreakActive)
        {
            ProcessHotStreakSolve(tileValues);
            return;
        }

        solveCount++;
        timeSinceLastSolve = 0f;

        var (effectiveBaseScore, enhancedBonus) = CalculateCommonBonuses(tileValues);

        int pointsAwarded = 0;

        if (solveCount == 1)
        {
            pointsAwarded = effectiveBaseScore + enhancedBonus;
            Debug.Log($"<color=green>Solve #1:</color> +{pointsAwarded} pts (base: {effectiveBaseScore}, enhanced: +{enhancedBonus})");
        }
        else if (solveCount == 2)
        {
            pointsAwarded = effectiveBaseScore + enhancedBonus;
            ActivateMultiplierBar();
            Debug.Log($"<color=green>Solve #2:</color> +{pointsAwarded} pts | <color=yellow>MULTIPLIER ACTIVATED (x{currentMultiplier:F2} ready)</color>");
        }
        else
        {
            int bonusSeconds = Mathf.FloorToInt(multiplierTimer);
            int multipliedScore = Mathf.RoundToInt(effectiveBaseScore * currentMultiplier);
            pointsAwarded = multipliedScore + bonusSeconds + enhancedBonus;

            Debug.Log($"<color=green>Solve #{solveCount}:</color> ({effectiveBaseScore} × {currentMultiplier:F2}) + {bonusSeconds} time + {enhancedBonus} enhanced = <color=cyan>+{pointsAwarded} pts</color>");

            currentMultiplier += effectiveMultiplierIncrement;
            maxMultiplierReached = Mathf.Max(maxMultiplierReached, currentMultiplier);

            // Check if we've hit the hot streak threshold
            if (currentMultiplier > effectiveHotStreakThreshold)
            {
                StartCoroutine(TriggerHotStreak());
                return; // Don't process normal scoring, hot streak handles it
            }

            // Cap at max multiplier
            currentMultiplier = Mathf.Min(currentMultiplier, effectiveMaxMultiplier);

            multiplierTimer = multiplierDuration;
            OnMultiplierChanged?.Invoke(multiplierActive, currentMultiplier, multiplierTimer);
        }

        int finalPoints = ApplyPostScoringBonuses(pointsAwarded, enhancedBonus, tileValues);
        CommitScore(finalPoints);
    }

    /// <summary>
    /// Calculate bonus BP from enhanced numbers (arcade mode: always 0).
    /// </summary>
    private int CalculateEnhancedNumberBonus(List<int> tileValues)
    {
        return 0;
    }

    /// <summary>
    /// Calculate time bonus from Enhanced 0 (arcade mode: always 0).
    /// </summary>
    private float CalculateZeroTimeBonus(List<int> tileValues)
    {
        return 0f;
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


    #endregion
    
    #region Hot Streak Mode
    
    private IEnumerator TriggerHotStreak()
    {
        Debug.Log($"<color=orange>🔥🔥🔥 HOT STREAK ACTIVATED! 🔥🔥🔥</color> (x{effectiveHotStreakMultiplier} for {effectiveHotStreakDuration}s)");

        // Set hot streak state using cached effective values
        hotStreakActive = true;
        hotStreakTimer = effectiveHotStreakDuration;

        // Set multiplier to hot streak value (with Red Bull bonus)
        currentMultiplier = effectiveHotStreakMultiplier;
        multiplierTimer = effectiveHotStreakDuration; // Sync with hot streak duration
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
    /// Apply post-scoring bonuses (arcade mode: none).
    /// </summary>
    private int ApplyPostScoringBonuses(int pointsAwarded, int enhancedBonus, List<int> tileValues)
    {
        return pointsAwarded;
    }

    /// <summary>
    /// Calculate common scoring components: base score (arcade mode uses hardcoded value).
    /// </summary>
    private (int effectiveBaseScore, int enhancedBonus) CalculateCommonBonuses(List<int> tileValues)
    {
        int effectiveBaseScore = baseMatchScore;
        int enhancedBonus = 0;

        return (effectiveBaseScore, enhancedBonus);
    }

    /// <summary>
    /// Apply final points to score.
    /// </summary>
    private void CommitScore(int finalPoints)
    {
        Score += finalPoints;
        OnScoreChanged?.Invoke(Score, finalPoints);
    }

    /// <summary>
    /// Process scoring during Hot Streak (called from ProcessSingleSolve when hot streak is active).
    /// </summary>
    private void ProcessHotStreakSolve(List<int> tileValues = null)
    {
        var (effectiveBaseScore, enhancedBonus) = CalculateCommonBonuses(tileValues);

        int multipliedScore = Mathf.RoundToInt(effectiveBaseScore * effectiveHotStreakMultiplier);
        int pointsAwarded = multipliedScore + enhancedBonus;

        int finalPoints = ApplyPostScoringBonuses(pointsAwarded, enhancedBonus, tileValues);

        Debug.Log($"<color=orange>🔥 HOT STREAK SOLVE:</color> ({effectiveBaseScore} × {effectiveHotStreakMultiplier:F0}) + {enhancedBonus} enhanced = <color=cyan>+{finalPoints} pts</color>");

        CommitScore(finalPoints);

        // Multiplier stays fixed during hot streak
        OnMultiplierChanged?.Invoke(true, currentMultiplier, hotStreakTimer);
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

        // Freeze the grid immediately to stop any in-progress cascades
        GridManager gm = FindFirstObjectByType<GridManager>();
        gm?.FreezeGrid();

        Debug.Log($"<color=cyan>*** TIME'S UP! ***</color> Score: {Score}");
        SceneFlowManager.Instance?.OnGameEnded();
        OnGameWon?.Invoke();
    }
    
    
    #endregion
}
