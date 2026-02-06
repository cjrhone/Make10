using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages game state: scoring, motivation meter, win/lose conditions, difficulty settings.
/// Integrates with PlayerInventory for upgrade/snack bonuses.
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

    [Header("Debug Mode")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private int debugStartingBP = 500;

    [Header("Boss Fight Settings")]
    [SerializeField] private float bossFightDuration = 60f;

    [Header("References")]
    [SerializeField] private UIManager uiManager;

    // Campaign/Boss state
    public bool IsBossFight { get; private set; }
    public int CurrentRoundThreshold => CampaignManager.Instance?.GetCurrentThreshold() ?? WinScore;

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

    // Tracking for snack triggers
    private int matchCountThisRound = 0;
    private bool stopwatchUsedThisRound = false;

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
    public event Action OnGameLost;
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

        // F4 Debug: Instantly complete round (reach BP threshold)
        if (Keyboard.current != null && Keyboard.current.f4Key.wasPressedThisFrame)
        {
            DebugCompleteRound();
        }

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

    /// <summary>
    /// Debug: Instantly complete the current round by reaching BP threshold.
    /// </summary>
    private void DebugCompleteRound()
    {
        if (!IsGameActive) return;

        if (IsBossFight)
        {
            // Kill the boss instantly
            Debug.Log("<color=magenta>[DEBUG F4] Killing boss instantly!</color>");
            CampaignManager.Instance?.DebugKillBoss();
        }
        else
        {
            // Set score to threshold
            int threshold = CurrentRoundThreshold;
            int pointsNeeded = threshold - Score;

            if (pointsNeeded > 0)
            {
                Debug.Log($"<color=magenta>[DEBUG F4] Adding {pointsNeeded} points to reach threshold {threshold}</color>");
                Score += pointsNeeded;
                OnScoreChanged?.Invoke(Score, pointsNeeded);
                CheckWinCondition();
            }
            else
            {
                Debug.Log("<color=magenta>[DEBUG F4] Already at or above threshold!</color>");
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
    /// Cache effective values from PlayerInventory for performance.
    /// Called at game start to avoid repeated lookups during gameplay.
    /// </summary>
    private void CacheEffectiveValues()
    {
        if (PlayerInventory.Instance == null)
        {
            // Default values when no inventory
            effectiveMaxMultiplier = maxMultiplier;
            effectiveHotStreakThreshold = maxMultiplier;
            effectiveHotStreakMultiplier = hotStreakMultiplier;
            effectiveHotStreakDuration = hotStreakDuration;
            effectiveMultiplierIncrement = multiplierIncrement;
            return;
        }

        // Night Owl caps multiplier
        float multiplierCap = PlayerInventory.Instance.GetMultiplierCap();
        effectiveMaxMultiplier = multiplierCap > 0 ? multiplierCap : maxMultiplier;

        // Cramming lowers hot streak trigger threshold
        float hotStreakThreshold = PlayerInventory.Instance.GetHotStreakThreshold();
        effectiveHotStreakThreshold = hotStreakThreshold > 0 ? hotStreakThreshold : maxMultiplier;

        // Red Bull increases hot streak multiplier
        effectiveHotStreakMultiplier = hotStreakMultiplier + PlayerInventory.Instance.GetHotStreakMultiplierBonus();

        // Study Glasses extends hot streak duration
        effectiveHotStreakDuration = hotStreakDuration + PlayerInventory.Instance.GetHotStreakDurationBonus();

        // Momentum increases multiplier increment
        effectiveMultiplierIncrement = multiplierIncrement + PlayerInventory.Instance.GetMultiplierIncrementBonus();

        Debug.Log($"[GameManager] Cached values - MaxMult: {effectiveMaxMultiplier:F2}, HotStreak@{effectiveHotStreakThreshold:F2}x, " +
                  $"HSMult: {effectiveHotStreakMultiplier:F0}x, HSDur: {effectiveHotStreakDuration}s, MultInc: +{effectiveMultiplierIncrement:F2}");
    }

    #endregion
    
    #region Game Flow
    
    /// <summary>
    /// Resets all per-round state (score, multiplier, hot streak, tracking).
    /// Shared by StartNewGame, ActivateGame, and ActivateBossFight.
    /// </summary>
    private void ResetRoundState(float duration)
    {
        Score = 0;
        TimeRemaining = duration;
        IsGameActive = true;
        IsProcessing = false;
        IsSolveAnimationPlaying = false;

        solveCount = 0;
        matchCountThisRound = 0;
        stopwatchUsedThisRound = false;
        currentMultiplier = 1f;
        multiplierTimer = 0f;
        multiplierActive = false;
        timeSinceLastSolve = 0f;
        hotStreakActive = false;
        hotStreakTimer = 0f;
        maxMultiplierReached = 1f;

        // Cache effective values from upgrades
        CacheEffectiveValues();

        // Reset avatar to default state
        AvatarManager.Instance?.ResetToDefault();

        // Reset round tracking for snacks
        PlayerInventory.Instance?.ResetRoundTracking();
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
        ResetRoundState(gameDuration);
        NotifyUIOfReset();

        // Refresh UI for new difficulty settings
        if (uiManager != null)
            uiManager.RefreshTargetScore();

        GridManager gridManager = FindFirstObjectByType<GridManager>();
        if (gridManager != null)
        {
            gridManager.ResetGame();
        }

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
    /// Activate the game for a boss fight (special mode with timer).
    /// </summary>
    public void ActivateBossFight()
    {
        IsBossFight = true;
        ResetRoundState(bossFightDuration);
        NotifyUIOfReset();

        Debug.Log($"<color=red>BOSS FIGHT ACTIVATED!</color> Timer: {bossFightDuration}s");
    }

    /// <summary>
    /// Activate the game without resetting the grid (used when grid was pre-spawned).
    /// </summary>
    public void ActivateGame()
    {
        // Apply time bonuses from inventory
        float bonusTime = PlayerInventory.Instance?.GetBonusStartingTime() ?? 0f;
        ResetRoundState(gameDuration + bonusTime);

        // Apply starting multiplier bonuses from inventory
        float multiplierBonus = PlayerInventory.Instance?.GetStartingMultiplierBonus() ?? 0f;
        currentMultiplier = 1f + multiplierBonus;
        multiplierActive = multiplierBonus > 0f; // Start with multiplier active if we have bonuses

        // Check for Energy Drink snack (start in Hot Streak)
        bool startInHotStreak = PlayerInventory.Instance?.HasSnack(SnackType.EnergyDrink) == true;
        if (startInHotStreak)
        {
            StartCoroutine(TriggerHotStreak());
        }

        maxMultiplierReached = currentMultiplier;

        NotifyUIOfReset();

        // Refresh UI for new difficulty settings
        if (uiManager != null)
            uiManager.RefreshTargetScore();

        Debug.Log($"Game activated! Grid: {gameSettings.gridSize}x{gameSettings.gridSize}, Target: {WinScore} BP, Bonus Time: +{bonusTime}s, Start Mult: x{currentMultiplier:F2}");
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

        for (int i = 0; i < linesCleared; i++)
        {
            ProcessSingleSolve(tileValues);
        }
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
        matchCountThisRound++;
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
    /// Calculate bonus BP from enhanced numbers in the matched tiles.
    /// </summary>
    private int CalculateEnhancedNumberBonus(List<int> tileValues)
    {
        if (tileValues == null || tileValues.Count == 0 || PlayerInventory.Instance == null)
            return 0;

        int totalBonus = 0;
        foreach (int value in tileValues)
        {
            int bonus = PlayerInventory.Instance.GetEnhancedNumberBonus(value);
            if (bonus > 0)
            {
                totalBonus += bonus;
            }
        }
        return totalBonus;
    }

    /// <summary>
    /// Calculate time bonus from Enhanced 0 (zeros in match give time).
    /// </summary>
    private float CalculateZeroTimeBonus(List<int> tileValues)
    {
        if (tileValues == null || PlayerInventory.Instance == null)
            return 0f;

        int zeroCount = 0;
        foreach (int value in tileValues)
        {
            if (value == 0) zeroCount++;
        }

        if (zeroCount > 0)
        {
            float bonusPerZero = PlayerInventory.Instance.GetEnhancedZeroTimeBonus();
            return zeroCount * bonusPerZero;
        }
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
        // Apply drain rate reduction from upgrades (Sustain, Metronome)
        float drainReduction = PlayerInventory.Instance?.GetDrainRateReduction() ?? 0f;
        float effectiveDrainRate = multiplierDrainRate * (1f - drainReduction);

        multiplierTimer -= effectiveDrainRate * deltaTime;

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
    /// Also handles boss damage during boss fights.
    /// </summary>
    private void CheckWinCondition()
    {
        if (!IsGameActive) return;

        if (IsBossFight)
        {
            // In boss fight, score = damage to boss
            // Damage is applied as we score
            CampaignManager.Instance?.DamageBoss(Score);

            // Check if boss is defeated
            if (CampaignManager.Instance != null && CampaignManager.Instance.CurrentBossHP <= 0)
            {
                Debug.Log($"<color=cyan>*** BOSS DEFEATED! ***</color>");
                StartCoroutine(WinGameDelayed());
            }
        }
        else
        {
            // Normal round - check against threshold
            int threshold = CurrentRoundThreshold;
            if (Score >= threshold)
            {
                Debug.Log($"<color=cyan>*** WIN THRESHOLD REACHED! ***</color> Score: {Score}/{threshold}");
                StartCoroutine(WinGameDelayed());
            }
        }
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
    /// Apply shared post-scoring bonuses (BP multiplier, Calculator, 10/10 Sandwich).
    /// Returns the final adjusted point total.
    /// </summary>
    private int ApplyPostScoringBonuses(int pointsAwarded, int enhancedBonus, List<int> tileValues)
    {
        // Apply overall BP multiplier (Textbook, Overachiever)
        float bpMultiplier = PlayerInventory.Instance?.GetOverallBPMultiplier() ?? 1f;
        int finalPoints = Mathf.RoundToInt(pointsAwarded * bpMultiplier);

        if (bpMultiplier > 1f)
        {
            Debug.Log($"<color=magenta>BP Multiplier:</color> {pointsAwarded} × {bpMultiplier:F2} = {finalPoints}");
        }

        // Check for Calculator snack (chance for double points)
        if (PlayerInventory.Instance?.HasSnack(SnackType.Calculator) == true)
        {
            SnackData calculator = PlayerInventory.Instance.GetSnack(SnackType.Calculator);
            if (calculator != null && UnityEngine.Random.value < calculator.effectChance)
            {
                Debug.Log($"<color=cyan>🎲 CALCULATOR TRIGGERED!</color> Double points: {finalPoints} → {finalPoints * 2}");
                finalPoints *= 2;
            }
        }

        // Check for 10/10 Sandwich (bonus every 10th Make10)
        if (matchCountThisRound % 10 == 0 && PlayerInventory.Instance?.HasSnack(SnackType.TenTenSandwich) == true)
        {
            SnackData sandwich = PlayerInventory.Instance.GetSnack(SnackType.TenTenSandwich);
            int sandwichBonus = Mathf.RoundToInt(sandwich?.effectValue ?? 100);
            finalPoints += sandwichBonus;
            Debug.Log($"<color=yellow>🥪 10/10 SANDWICH!</color> Match #{matchCountThisRound} bonus: +{sandwichBonus} BP");
        }

        // Notify if enhanced bonus was applied
        if (enhancedBonus > 0)
        {
            OnEnhancedNumberBonus?.Invoke(enhancedBonus);
        }

        return finalPoints;
    }

    /// <summary>
    /// Calculate common scoring components: base score, enhanced bonus, and zero time bonus.
    /// </summary>
    private (int effectiveBaseScore, int enhancedBonus) CalculateCommonBonuses(List<int> tileValues)
    {
        int effectiveBaseScore = baseMatchScore + (PlayerInventory.Instance?.GetBaseScoreBonus() ?? 0);
        int enhancedBonus = CalculateEnhancedNumberBonus(tileValues);

        // Apply time bonus from Enhanced 0
        float zeroTimeBonus = CalculateZeroTimeBonus(tileValues);
        if (zeroTimeBonus > 0)
        {
            TimeRemaining += zeroTimeBonus;
            OnTimeBonus?.Invoke(zeroTimeBonus);
            OnTimeChanged?.Invoke(TimeRemaining);
            Debug.Log($"<color=yellow>Enhanced 0:</color> +{zeroTimeBonus}s from zeros in match");
        }

        return (effectiveBaseScore, enhancedBonus);
    }

    /// <summary>
    /// Apply final points to score and check win condition.
    /// </summary>
    private void CommitScore(int finalPoints)
    {
        Score += finalPoints;
        OnScoreChanged?.Invoke(Score, finalPoints);
        CheckWinCondition();
    }

    /// <summary>
    /// Process scoring during Hot Streak (called from ProcessSingleSolve when hot streak is active).
    /// </summary>
    private void ProcessHotStreakSolve(List<int> tileValues = null)
    {
        matchCountThisRound++;

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
        // Check for Stopwatch snack (emergency time when timer hits 0)
        if (!stopwatchUsedThisRound && PlayerInventory.Instance != null)
        {
            if (PlayerInventory.Instance.TryTriggerSnack(SnackType.Stopwatch))
            {
                SnackData stopwatch = PlayerInventory.Instance.GetSnack(SnackType.Stopwatch);
                float bonusTime = stopwatch?.effectValue ?? 10f;

                TimeRemaining = bonusTime;
                stopwatchUsedThisRound = true;

                Debug.Log($"<color=yellow>⏱️ STOPWATCH TRIGGERED!</color> +{bonusTime} seconds!");
                OnTimeBonus?.Invoke(bonusTime);
                OnTimeChanged?.Invoke(TimeRemaining);
                return; // Don't end game
            }
        }

        IsGameActive = false;
        int threshold = CurrentRoundThreshold;

        if (IsBossFight)
        {
            // Boss fight time up = failed to defeat boss
            Debug.Log($"<color=red>*** BOSS FIGHT FAILED - TIME'S UP ***</color>");
            IsBossFight = false;
            SceneFlowManager.Instance?.OnGameEnded(false);
            OnGameLost?.Invoke();
        }
        else if (Score >= threshold)
        {
            Debug.Log("<color=cyan>*** TIME'S UP - YOU WIN! ***</color>");
            SceneFlowManager.Instance?.OnGameEnded(true);
            OnGameWon?.Invoke();
        }
        else
        {
            Debug.Log($"<color=red>*** TIME'S UP - GAME OVER ***</color> Score: {Score}/{threshold}");
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

        if (IsBossFight)
        {
            Debug.Log($"<color=cyan>*** BOSS DEFEATED! ***</color> Score: {Score} | Time left: {TimeRemaining:F1}s");
            IsBossFight = false;
            // CampaignManager handles boss defeat rewards and stage advancement
        }
        else
        {
            Debug.Log($"<color=cyan>*** YOU WIN! ***</color> Score: {Score} | Time left: {TimeRemaining:F1}s");
            // Notify CampaignManager that round is completed (BP will be added via UIManager's Continue flow)
            // Note: UIManager.OnContinueButtonClicked() handles adding BP to RunManager
        }

        SceneFlowManager.Instance?.OnGameEnded(true);
        OnGameWon?.Invoke();
    }
    
    #endregion
}
