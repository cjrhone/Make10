using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages game state: scoring, multiplier, hot streak, timer.
/// Supports Arcade (timed) and Zen (untimed/endless) modes.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    /// <summary>
    /// Game mode — Arcade is timed (60s), Zen is timed (300s) with failed-swap penalties.
    /// </summary>
    public enum GameMode { Arcade, Zen }
    public GameMode CurrentMode { get; private set; } = GameMode.Arcade;

    #region Game Settings

    [System.Serializable]
    public class GameSettings
    {
        [Header("Grid Settings")]
        public int gridSize = 5;

        [Header("Win Condition")]
        public int winScore = 100;

        [Header("Tile Weights (base weights for tiles 0-9)")]
        [Range(0, 1)] public float weight0 = 0.12f;   // Grey (wildcard) — boosted for easy early 10s
        [Range(0, 1)] public float weight1 = 0.28f;    // Gold — boosted primary, easiest combos
        [Range(0, 1)] public float weight2 = 0.26f;    // Blue — dominant (pairs well with 3s)
        [Range(0, 1)] public float weight3 = 0.22f;    // Green — strong mid-range
        [Range(0, 1)] public float weight4 = 0.08f;    // Coral — further reduced, less clutter
        [Range(0, 1)] public float weight5 = 0f;       // Orange — introduced by solve ramp
        [Range(0, 1)] public float weight6 = 0f;       // Purple — introduced by solve ramp
        [Range(0, 1)] public float weight7 = 0f;       // Teal — introduced by solve ramp
        [Range(0, 1)] public float weight8 = 0f;
        [Range(0, 1)] public float weight9 = 0f;

        public float[] GetWeights()
        {
            return new float[] { weight0, weight1, weight2, weight3, weight4, weight5, weight6, weight7, weight8, weight9 };
        }
    }

    [Header("Game Settings")]
    [SerializeField] private GameSettings gameSettings = new GameSettings();

    #endregion

    public int WinScore => gameSettings.winScore;
    [SerializeField] private float gameDuration = 60f;
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
    [SerializeField] private float timeBonusPerMatch = 1.5f;

    [Header("Hot Streak Mode")]
    [SerializeField] private float hotStreakDuration = 10f;
    [SerializeField] private float hotStreakMultiplier = 5f;

    [Header("Speed Bonus")]
    [SerializeField] private float speedBonusThreshold = 4f;  // Seconds to qualify
    [SerializeField] private int speedBonusAmount = 5;          // Bonus BP

    [Header("Star Rating Thresholds (BP)")]
    [SerializeField] private int star1Threshold = 300;
    [SerializeField] private int star2Threshold = 600;
    [SerializeField] private int star3Threshold = 1000;

    [Header("Debug Mode")]
    #pragma warning disable CS0414 // Inspector-assigned fields
    [SerializeField] private bool debugMode = false;
    [SerializeField] private int debugStartingBP = 500;
    #pragma warning restore CS0414

    [Header("References")]
    [SerializeField] private UIManager uiManager;

    [Header("Zen Mode Settings")]
    [SerializeField] private float zenGameDuration = 300f;  // 5 minutes
    [SerializeField] private float zenFailedSwapPenalty = 3f; // Seconds deducted on bad swap
    [SerializeField] private int zenMaxReshuffles = 3;
    [SerializeField] private int zenStar1Threshold = 500;
    [SerializeField] private int zenStar2Threshold = 1000;
    [SerializeField] private int zenStar3Threshold = 2000;

    // High Score persistence keys
    private const string HIGH_SCORE_KEY = "Make10_HighScore";
    private const string HIGH_SCORE_BP_KEY = "Make10_HighScoreBP";
    private const string TOTAL_GAMES_KEY = "Make10_TotalGames";
    private const string ZEN_HIGH_SCORE_KEY = "Make10_ZenHighScore";
    private const string ZEN_HIGH_SCORE_BP_KEY = "Make10_ZenHighScoreBP";
    private const string ZEN_TOTAL_GAMES_KEY = "Make10_ZenTotalGames";

    // Zen mode tracking
    private int zenReshufflesRemaining;
    public int ZenReshufflesRemaining => zenReshufflesRemaining;
    public int ZenMaxReshuffles => zenMaxReshuffles;

    // Zen stats (per-session, reset each game)
    private int zenMatchCount;
    private int zenLockedTileCount;
    private int zenHighestLockedValue;
    private int zenChainCount;
    public int ZenMatchCount => zenMatchCount;
    public int ZenLockedTileCount => zenLockedTileCount;
    public int ZenHighestLockedValue => zenHighestLockedValue;
    public int ZenChainCount => zenChainCount;

    // Current state
    public int Score { get; private set; }
    public float TimeRemaining { get; private set; }
    public float GameDuration => CurrentMode == GameMode.Zen ? zenGameDuration : gameDuration;
    public bool IsGameActive { get; private set; }
    public bool IsProcessing { get; set; }
    public bool IsSolveAnimationPlaying { get; set; }

    // High score tracking (mode-aware)
    public int HighScore => PlayerPrefs.GetInt(
        CurrentMode == GameMode.Zen ? ZEN_HIGH_SCORE_KEY : HIGH_SCORE_KEY, 0);
    public int HighScoreBP => PlayerPrefs.GetInt(
        CurrentMode == GameMode.Zen ? ZEN_HIGH_SCORE_BP_KEY : HIGH_SCORE_BP_KEY, 0);
    public int TotalGamesPlayed => PlayerPrefs.GetInt(
        CurrentMode == GameMode.Zen ? ZEN_TOTAL_GAMES_KEY : TOTAL_GAMES_KEY, 0);
    public bool IsNewHighScore { get; private set; }

    /// <summary>
    /// Calculate star rating (0-3) based on total BP earned this round.
    /// Zen mode uses higher thresholds (longer sessions = more BP).
    /// </summary>
    public int GetStarRating(int totalBP)
    {
        int t1 = CurrentMode == GameMode.Zen ? zenStar1Threshold : star1Threshold;
        int t2 = CurrentMode == GameMode.Zen ? zenStar2Threshold : star2Threshold;
        int t3 = CurrentMode == GameMode.Zen ? zenStar3Threshold : star3Threshold;
        if (totalBP >= t3) return 3;
        if (totalBP >= t2) return 2;
        if (totalBP >= t1) return 1;
        return 0;
    }

    public int Star1Threshold => CurrentMode == GameMode.Zen ? zenStar1Threshold : star1Threshold;
    public int Star2Threshold => CurrentMode == GameMode.Zen ? zenStar2Threshold : star2Threshold;
    public int Star3Threshold => CurrentMode == GameMode.Zen ? zenStar3Threshold : star3Threshold;

    // Multiplier state (SolveCount exposed for performance-based tile weight ramp)
    private int solveCount = 0;
    public int SolveCount => solveCount;
    private float currentMultiplier = 1f;
    private float multiplierTimer = 0f;
    private bool multiplierActive = false;
    private float timeSinceLastSolve = 0f;
    private float maxMultiplierReached = 1f;

    // Hot Streak state
    private bool hotStreakActive = false;
    private float hotStreakTimer = 0f;

    // Speed bonus tracking
    private float lastPlayerSolveTime = -999f;

    // Session time tracking (wall-clock, independent of countdown timer)
    private float sessionStartTime;
    private float lastSessionDuration;

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
    public float SessionDuration => IsGameActive ? Time.time - sessionStartTime : lastSessionDuration;

    // Events for UI updates
    public event Action<int, int> OnScoreChanged;
    public event Action<float> OnTimeChanged;
    public event Action<bool, float, float> OnMultiplierChanged;
    public event Action OnGameWon;
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

        // Target 60fps on mobile for smooth gameplay
        Application.targetFrameRate = 60;
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

        // Drain timer in both modes (Arcade: 60s, Zen: 300s)
        if (!IsProcessing)
        {
            DrainTime(Time.deltaTime);
        }

        // Zen mode: multiplier doesn't drain on a timer (resets on failed swap instead)
        if (multiplierActive && !IsProcessing && CurrentMode == GameMode.Arcade)
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
        lastPlayerSolveTime = -999f;
        sessionStartTime = Time.time;
        lastSessionDuration = 0f;

        // Zen mode: initialize reshuffles and stats
        zenReshufflesRemaining = zenMaxReshuffles;
        zenMatchCount = 0;
        zenLockedTileCount = 0;
        zenHighestLockedValue = 0;
        zenChainCount = 0;

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
    /// Set the game mode before activating. Call this before ActivateGame().
    /// </summary>
    public void SetGameMode(GameMode mode)
    {
        CurrentMode = mode;
        Debug.Log($"Game mode set to: {mode}");
    }

    /// <summary>
    /// Activate the game without resetting the grid (used when grid was pre-spawned).
    /// </summary>
    public void ActivateGame()
    {
        CacheEffectiveValues();
        float duration = CurrentMode == GameMode.Zen ? zenGameDuration : gameDuration;
        ResetRoundState(duration);
        NotifyUIOfReset();

        Debug.Log($"Game activated! Mode: {CurrentMode}, Duration: {duration}s, Grid: {gameSettings.gridSize}x{gameSettings.gridSize}");
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
    /// cascadeCount: 1 = player swap match, 2+ = auto-cascade match.
    /// </summary>
    public void OnMatchCleared(int tilesCleared, int rowsMatched, int columnsMatched, List<int> tileValues, MatchResult matchResult = null, int cascadeCount = 1)
    {
        if (!IsGameActive) return;

        int linesCleared = rowsMatched + columnsMatched;
        bool isPlayerMatch = (cascadeCount <= 1);

        if (isPlayerMatch)
        {
            // PLAYER SWAP: full scoring with multiplier, speed bonus
            // Time bonus only in Arcade (Zen timer is fixed 300s minus penalties)
            if (linesCleared > 0 && CurrentMode == GameMode.Arcade)
            {
                AddTime(timeBonusPerMatch * linesCleared);
            }

            for (int i = 0; i < linesCleared; i++)
            {
                int lineBaseScore = (matchResult != null) ? matchResult.GetLineSumByIndex(i) : baseMatchScore;
                ProcessSingleSolve(tileValues, lineBaseScore);
            }
        }
        else
        {
            // CASCADE: flat base BP only, no time bonus, no multiplier interaction
            for (int i = 0; i < linesCleared; i++)
            {
                int lineBaseScore = (matchResult != null) ? matchResult.GetLineSumByIndex(i) : baseMatchScore;
                ProcessCascadeSolve(lineBaseScore);
            }
        }

        // Ultra combo bonus: 5+ simultaneous lines awards a flat 1000 BP bonus (regardless of cascade)
        if (linesCleared >= 5)
        {
            Debug.Log($"<color=red>★★★ ULTRA COMBO! {linesCleared} LINES! +1000 BONUS BP ★★★</color>");
            CommitScore(1000);
        }
    }

    /// <summary>
    /// Process a cascade solve — flat base BP, no multiplier/time interaction.
    /// Still increments SolveCount for progressive difficulty ramp.
    /// </summary>
    private void ProcessCascadeSolve(int lineBaseScore)
    {
        Debug.Log($"<color=grey>[CASCADE]</color> +{lineBaseScore} BP (flat, no multiplier)");
        CommitScore(lineBaseScore);
        // Note: does NOT increment solveCount, touch multiplier, or trigger hot streak
        // SolveCount used for tile weight ramp still comes from solveCount (player solves only)
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
    /// Process a single solve, applying all upgrade and snack bonuses.
    /// lineBaseScore is the actual sum of the matched line (10, 20, 30, or 40).
    /// </summary>
    private void ProcessSingleSolve(List<int> tileValues = null, int lineBaseScore = 10)
    {
        // During Hot Streak, use special scoring
        if (hotStreakActive)
        {
            ProcessHotStreakSolve(tileValues, lineBaseScore);
            return;
        }

        solveCount++;
        timeSinceLastSolve = 0f;

        var (effectiveBaseScore, enhancedBonus) = CalculateCommonBonuses(tileValues);
        // Override base score with the line's actual sum
        effectiveBaseScore = lineBaseScore;

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

        // Speed bonus: reward fast consecutive player solves
        int speedBonus = 0;
        float timeSinceLastPlayerSolve = Time.time - lastPlayerSolveTime;
        if (lastPlayerSolveTime > 0f && timeSinceLastPlayerSolve <= speedBonusThreshold)
        {
            speedBonus = speedBonusAmount;
            Debug.Log($"<color=magenta>⚡ SPEED BONUS! +{speedBonus} BP (solved in {timeSinceLastPlayerSolve:F1}s)</color>");
        }
        lastPlayerSolveTime = Time.time;

        int finalPoints = ApplyPostScoringBonuses(pointsAwarded + speedBonus, enhancedBonus, tileValues);
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
    /// lineBaseScore is the actual sum of the matched line (10, 20, 30, or 40).
    /// </summary>
    private void ProcessHotStreakSolve(List<int> tileValues = null, int lineBaseScore = 10)
    {
        var (effectiveBaseScore, enhancedBonus) = CalculateCommonBonuses(tileValues);
        // Override base score with the line's actual sum
        effectiveBaseScore = lineBaseScore;

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
        // Guard against double game-over (OnFailedSwap + Update can both trigger in same frame)
        if (!IsGameActive) return;

        // Freeze session duration before deactivating
        lastSessionDuration = Time.time - sessionStartTime;
        IsGameActive = false;

        // Freeze the grid immediately to stop any in-progress cascades
        GridManager gm = FindFirstObjectByType<GridManager>();
        gm?.FreezeGrid();

        // Use mode-appropriate persistence keys
        string gamesKey = CurrentMode == GameMode.Zen ? ZEN_TOTAL_GAMES_KEY : TOTAL_GAMES_KEY;
        string hsKey = CurrentMode == GameMode.Zen ? ZEN_HIGH_SCORE_KEY : HIGH_SCORE_KEY;

        // Track total games played
        int gamesPlayed = PlayerPrefs.GetInt(gamesKey, 0) + 1;
        PlayerPrefs.SetInt(gamesKey, gamesPlayed);

        // Save raw score high score (legacy key, kept for backward compat)
        if (Score > PlayerPrefs.GetInt(hsKey, 0))
        {
            PlayerPrefs.SetInt(hsKey, Score);
        }

        // IsNewHighScore will be set later by CheckAndSaveBPHighScore() in UIManager
        // after total BP (including session time bonus) is calculated
        IsNewHighScore = false;

        PlayerPrefs.Save();

        string endMessage = CurrentMode == GameMode.Zen ? "STILLNESS" : "TIME'S UP";
        Debug.Log($"<color=cyan>*** {endMessage}! ***</color> Score: {Score} | Session: {lastSessionDuration:F1}s | Games: {gamesPlayed}");
        SceneFlowManager.Instance?.OnGameEnded();
        OnGameWon?.Invoke();
    }

    /// <summary>
    /// Save the total BP earned this round as high score if it's a new record.
    /// Called from UIManager after BP calculation. Sets IsNewHighScore flag.
    /// </summary>
    public void CheckAndSaveBPHighScore(int totalBP)
    {
        string key = CurrentMode == GameMode.Zen ? ZEN_HIGH_SCORE_BP_KEY : HIGH_SCORE_BP_KEY;
        int currentBest = PlayerPrefs.GetInt(key, 0);

        // Set IsNewHighScore based on total BP (the real player-facing score)
        IsNewHighScore = totalBP > currentBest;

        if (IsNewHighScore)
        {
            PlayerPrefs.SetInt(key, totalBP);
            PlayerPrefs.Save();
            Debug.Log($"<color=yellow>*** NEW BP HIGH SCORE ({CurrentMode}): {totalBP} (prev: {currentBest})! ***</color>");
        }
    }


    #endregion

    #region Zen Mode

    /// <summary>
    /// Use one reshuffle in Zen mode. Returns true if reshuffle was available.
    /// Called by GridManager when board has no valid moves.
    /// </summary>
    public bool UseReshuffle()
    {
        if (CurrentMode != GameMode.Zen) return false;
        if (zenReshufflesRemaining <= 0) return false;

        zenReshufflesRemaining--;
        Debug.Log($"<color=cyan>Zen reshuffle used! {zenReshufflesRemaining} remaining.</color>");
        return true;
    }

    /// <summary>
    /// Called when a swap produces no match (failed swap).
    /// In Zen mode, this resets the multiplier — punishes random guessing.
    /// </summary>
    public void OnFailedSwap()
    {
        if (CurrentMode != GameMode.Zen) return;

        // Time penalty: always applies on failed swap
        TimeRemaining -= zenFailedSwapPenalty;
        TimeRemaining = Mathf.Max(0f, TimeRemaining);
        OnTimeChanged?.Invoke(TimeRemaining);
        Debug.Log($"<color=red>[Zen] Failed swap — -{zenFailedSwapPenalty}s! Timer: {TimeRemaining:F1}s</color>");

        // Multiplier reset: only if multiplier was active
        if (multiplierActive)
        {
            Debug.Log("<color=red>[Zen] Multiplier reset!</color>");
            DeactivateMultiplierBar();
        }

        // Check if time penalty caused game over
        if (TimeRemaining <= 0f)
        {
            TimeUp();
        }
    }

    /// <summary>
    /// Record a match in Zen mode (used for difficulty ramp).
    /// Called by GridManager after each successful match line.
    /// </summary>
    public void RecordZenMatch()
    {
        if (CurrentMode != GameMode.Zen) return;
        zenMatchCount++;
    }

    /// <summary>
    /// Record a locked tile creation in Zen mode.
    /// Called by GridManager when tiles merge into a locked tile.
    /// </summary>
    public void RecordZenLockedTile(int lockedValue)
    {
        if (CurrentMode != GameMode.Zen) return;
        zenLockedTileCount++;
        if (lockedValue > zenHighestLockedValue)
            zenHighestLockedValue = lockedValue;
    }

    /// <summary>
    /// Record a cascade chain in Zen mode.
    /// Called by GridManager when a cascade triggers additional matches.
    /// </summary>
    public void RecordZenChain()
    {
        if (CurrentMode != GameMode.Zen) return;
        zenChainCount++;
    }

    /// <summary>
    /// Zen mode game over — no valid moves and no reshuffles remaining.
    /// </summary>
    public void ZenGameOver()
    {
        if (!IsGameActive) return;

        Debug.Log("<color=cyan>*** ZEN GAME OVER — No valid moves remaining! ***</color>");

        // Freeze session duration
        lastSessionDuration = Time.time - sessionStartTime;
        IsGameActive = false;

        // Freeze the grid
        GridManager gm = FindFirstObjectByType<GridManager>();
        gm?.FreezeGrid();

        // Track total games
        string gamesKey = ZEN_TOTAL_GAMES_KEY;
        int gamesPlayed = PlayerPrefs.GetInt(gamesKey, 0) + 1;
        PlayerPrefs.SetInt(gamesKey, gamesPlayed);

        // Save raw score high score (legacy key)
        string hsKey = ZEN_HIGH_SCORE_KEY;
        if (Score > PlayerPrefs.GetInt(hsKey, 0))
        {
            PlayerPrefs.SetInt(hsKey, Score);
        }

        // IsNewHighScore will be set later by CheckAndSaveBPHighScore() in UIManager
        IsNewHighScore = false;

        PlayerPrefs.Save();

        SceneFlowManager.Instance?.OnGameEnded();
        OnGameWon?.Invoke();
    }

    #endregion
}
