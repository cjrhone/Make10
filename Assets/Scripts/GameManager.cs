using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Manages game state: scoring, motivation meter, win/lose conditions, difficulty settings.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    #region Difficulty Settings
    
    public enum DifficultyLevel { Easy, Medium, Hard }
    
    [System.Serializable]
    public class DifficultyPreset
    {
        public string name;
        [Header("Tile Weights (must sum to 1.0)")]
        [Range(0, 1)] public float weight0 = 0.15f;
        [Range(0, 1)] public float weight1 = 0.22f;
        [Range(0, 1)] public float weight2 = 0.22f;
        [Range(0, 1)] public float weight3 = 0.17f;
        [Range(0, 1)] public float weight4 = 0.12f;
        [Range(0, 1)] public float weight5 = 0.06f;
        [Range(0, 1)] public float weight6 = 0.06f;
        
        public float[] GetWeights()
        {
            return new float[] { weight0, weight1, weight2, weight3, weight4, weight5, weight6 };
        }
        
        public float TotalWeight()
        {
            return weight0 + weight1 + weight2 + weight3 + weight4 + weight5 + weight6;
        }
    }
    
    [Header("Difficulty Presets")]
    [SerializeField] private DifficultyPreset easyPreset = new DifficultyPreset
    {
        name = "Easy",
        weight0 = 0.10f,  // Limited 0s (too many = unsolvable)
        weight1 = 0.18f,  // Limited 1s (too many = unsolvable)
        weight2 = 0.30f,  // Favor 2s
        weight3 = 0.24f,  // Favor 3s
        weight4 = 0.18f,  // Good amount of 4s
        weight5 = 0.00f,  // No 5s
        weight6 = 0.00f   // No 6s
    };
    
    [SerializeField] private DifficultyPreset mediumPreset = new DifficultyPreset
    {
        name = "Medium",
        weight0 = 0.14f,
        weight1 = 0.24f,
        weight2 = 0.24f,
        weight3 = 0.18f,
        weight4 = 0.12f,
        weight5 = 0.08f,  // Some 5s
        weight6 = 0.00f   // No 6s
    };
    
    [SerializeField] private DifficultyPreset hardPreset = new DifficultyPreset
    {
        name = "Hard",
        weight0 = 0.15f,
        weight1 = 0.22f,
        weight2 = 0.20f,
        weight3 = 0.15f,
        weight4 = 0.12f,
        weight5 = 0.08f,  // 5s present
        weight6 = 0.08f   // 6s present - challenging!
    };
    
    [SerializeField] private DifficultyLevel currentDifficulty = DifficultyLevel.Medium;
    
    public DifficultyLevel CurrentDifficulty => currentDifficulty;
    
    #endregion
    
    [Header("Game Settings")]
    [SerializeField] private int winScore = 250;
    public int WinScore => winScore;
    [SerializeField] private float gameDuration = 60f;
    [SerializeField] private float postWinDelay = 0.5f;
    
    [Header("Scoring")]
    [SerializeField] private int baseMatchScore = 10;
    
    [Header("Multiplier Settings")]
    [SerializeField] private float multiplierDuration = 10f;
    [SerializeField] private float multiplierDrainRate = 1f;
    [SerializeField] private float multiplierIncrement = 0.25f;
    [SerializeField] private float startingMultiplier = 1.25f;
    [SerializeField] private float streakTimeout = 10f;
    
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
    
    // Public accessors for UI
    public bool IsMultiplierActive => multiplierActive;
    public float CurrentMultiplier => currentMultiplier;
    public float MultiplierTimer => multiplierTimer;
    public float MultiplierDuration => multiplierDuration;
    
    // Events for UI updates
    public event Action<int, int> OnScoreChanged;
    public event Action<float> OnTimeChanged;
    public event Action<bool, float, float> OnMultiplierChanged;
    public event Action OnGameWon;
    public event Action OnGameLost;
    
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
    
    #region Difficulty Methods
    
    /// <summary>
    /// Set the difficulty level. Call this before starting a game.
    /// </summary>
    public void SetDifficulty(DifficultyLevel level)
    {
        currentDifficulty = level;
        Debug.Log($"<color=yellow>Difficulty set to: {level}</color>");
        
        // Log the weights for debugging
        float[] weights = GetCurrentWeights();
        string weightStr = $"Weights: ";
        for (int i = 0; i < weights.Length; i++)
            weightStr += $"[{i}]={weights[i]:F2} ";
        Debug.Log(weightStr);
    }
    
    /// <summary>
    /// Get the tile spawn weights for the current difficulty.
    /// </summary>
    public float[] GetCurrentWeights()
    {
        return currentDifficulty switch
        {
            DifficultyLevel.Easy => easyPreset.GetWeights(),
            DifficultyLevel.Medium => mediumPreset.GetWeights(),
            DifficultyLevel.Hard => hardPreset.GetWeights(),
            _ => mediumPreset.GetWeights()
        };
    }
    
    /// <summary>
    /// Get a specific difficulty preset (for UI display, etc.)
    /// </summary>
    public DifficultyPreset GetPreset(DifficultyLevel level)
    {
        return level switch
        {
            DifficultyLevel.Easy => easyPreset,
            DifficultyLevel.Medium => mediumPreset,
            DifficultyLevel.Hard => hardPreset,
            _ => mediumPreset
        };
    }
    
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
        
        OnScoreChanged?.Invoke(Score, 0);
        OnTimeChanged?.Invoke(TimeRemaining);
        OnMultiplierChanged?.Invoke(false, 1f, 0f);
        
        GridManager gridManager = FindFirstObjectByType<GridManager>();
        if (gridManager != null)
        {
            gridManager.ResetGame();
        }
        
        Debug.Log($"Game started! Difficulty: {currentDifficulty}");
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
        
        OnScoreChanged?.Invoke(Score, 0);
        OnTimeChanged?.Invoke(TimeRemaining);
        OnMultiplierChanged?.Invoke(false, 1f, 0f);
        
        Debug.Log($"Game activated! Difficulty: {currentDifficulty}");
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
            multiplierTimer = multiplierDuration;
            
            OnMultiplierChanged?.Invoke(multiplierActive, currentMultiplier, multiplierTimer);
        }
        
        Score += pointsAwarded;
        OnScoreChanged?.Invoke(Score, pointsAwarded);
        
        if (Score >= winScore)
        {
            StartCoroutine(WinGameDelayed());
        }
    }
    
    private void ActivateMultiplierBar()
    {
        multiplierActive = true;
        multiplierTimer = multiplierDuration;
        currentMultiplier = startingMultiplier;
        
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
        
        if (Score >= winScore)
        {
            Debug.Log("<color=cyan>*** TIME'S UP - YOU WIN! ***</color>");
            SceneFlowManager.Instance?.OnGameEnded(true);
            OnGameWon?.Invoke();
        }
        else
        {
            Debug.Log($"<color=red>*** TIME'S UP - GAME OVER ***</color> Score: {Score}/{winScore}");
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
    
    public void OnPlayerSwap()
    {
        // Could add small motivation boost for activity
    }
}
