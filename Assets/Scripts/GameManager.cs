using UnityEngine;
using System;

/// <summary>
/// Manages game state: scoring, motivation meter, win/lose conditions.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    [Header("Game Settings")]
    [SerializeField] private int winScore = 200;
    [SerializeField] private float startingMotivation = 100f;
    [SerializeField] private float motivationDrainRate = 1f; // per second while idle
    [SerializeField] private float motivationMatchReward = 15f; // restored per match
    
    [Header("Scoring")]
    [SerializeField] private int baseMatchScore = 10;
    
    [Header("Speed Bonus Thresholds (seconds)")]
    [SerializeField] private float hotStreakTime = 2f;   // x3 multiplier
    [SerializeField] private float quickTime = 5f;       // x2 multiplier
    [SerializeField] private float normalTime = 10f;     // x1.5 multiplier
    
    [Header("Cascade Multipliers")]
    [SerializeField] private float cascade1 = 1.0f;
    [SerializeField] private float cascade2 = 1.5f;
    [SerializeField] private float cascade3 = 2.0f;
    [SerializeField] private float cascade4Plus = 2.5f;
    
    [Header("References")]
    [SerializeField] private UIManager uiManager;
    
    // Current state
    public int Score { get; private set; }
    public float Motivation { get; private set; }
    public bool IsGameActive { get; private set; }
    public bool IsProcessing { get; set; } // Set by GridManager during cascades
    
    // Timing
    private float timeSinceLastMatch;
    private float lastSpeedMultiplier = 1f;
    private int currentCascadeDepth = 0;
    
    // Events for UI updates
    public event Action<int, int> OnScoreChanged; // current, delta
    public event Action<float> OnMotivationChanged;
    public event Action<float, float> OnMultiplierApplied; // speed mult, cascade mult
    public event Action OnGameWon;
    public event Action OnGameLost;
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    private void Start()
    {
        StartNewGame();
    }
    
    private void Update()
    {
        if (!IsGameActive) return;
        
        // Track time since last match
        timeSinceLastMatch += Time.deltaTime;
        
        // Drain motivation while not processing (idle)
        if (!IsProcessing)
        {
            DrainMotivation(Time.deltaTime);
        }
    }
    
    /// <summary>
    /// Start or restart the game.
    /// </summary>
    public void StartNewGame()
    {
        Score = 0;
        Motivation = startingMotivation;
        IsGameActive = true;
        IsProcessing = false;
        timeSinceLastMatch = 0f;
        currentCascadeDepth = 0;
        
        OnScoreChanged?.Invoke(Score, 0);
        OnMotivationChanged?.Invoke(Motivation);
        
        Debug.Log("Game started!");
    }
    
    /// <summary>
    /// Called when a cascade sequence begins.
    /// </summary>
    public void OnCascadeStart()
    {
        IsProcessing = true;
        currentCascadeDepth = 0;
        
        // Calculate speed multiplier based on time since last match
        lastSpeedMultiplier = CalculateSpeedMultiplier();
    }
    
    /// <summary>
    /// Called when a cascade sequence ends.
    /// </summary>
    public void OnCascadeEnd()
    {
        IsProcessing = false;
        timeSinceLastMatch = 0f; // Reset timer after cascade completes
        currentCascadeDepth = 0;
    }
    
    /// <summary>
    /// Called when tiles are matched and cleared.
    /// </summary>
    public void OnMatchCleared(int tilesCleared, int rowsMatched, int columnsMatched)
    {
        if (!IsGameActive) return;
        
        currentCascadeDepth++;
        
        // Calculate multipliers
        float speedMult = lastSpeedMultiplier;
        float cascadeMult = GetCascadeMultiplier(currentCascadeDepth);
        
        // Calculate score
        int linesCleared = rowsMatched + columnsMatched;
        int rawScore = baseMatchScore * linesCleared;
        int finalScore = Mathf.RoundToInt(rawScore * speedMult * cascadeMult);
        
        // Add score
        Score += finalScore;
        
        // Restore motivation
        RestoreMotivation(motivationMatchReward * cascadeMult);
        
        // Fire events
        OnScoreChanged?.Invoke(Score, finalScore);
        OnMultiplierApplied?.Invoke(speedMult, cascadeMult);
        
        Debug.Log($"<color=green>+{finalScore} pts</color> " +
                 $"(base:{rawScore} × speed:{speedMult:F1} × cascade:{cascadeMult:F1}) " +
                 $"| Total: {Score}/{winScore}");
        
        // Check win condition
        if (Score >= winScore)
        {
            WinGame();
        }
    }
    
    /// <summary>
    /// Calculate speed multiplier based on time since last match.
    /// </summary>
    private float CalculateSpeedMultiplier()
    {
        if (timeSinceLastMatch < hotStreakTime)
        {
            Debug.Log("<color=orange>HOT STREAK! ×3</color>");
            return 3f;
        }
        else if (timeSinceLastMatch < quickTime)
        {
            Debug.Log("<color=yellow>Quick! ×2</color>");
            return 2f;
        }
        else if (timeSinceLastMatch < normalTime)
        {
            return 1.5f;
        }
        return 1f;
    }
    
    /// <summary>
    /// Get cascade multiplier based on chain depth.
    /// </summary>
    private float GetCascadeMultiplier(int depth)
    {
        switch (depth)
        {
            case 1: return cascade1;
            case 2: return cascade2;
            case 3: return cascade3;
            default: return cascade4Plus;
        }
    }
    
    /// <summary>
    /// Drain motivation over time.
    /// </summary>
    private void DrainMotivation(float deltaTime)
    {
        Motivation -= motivationDrainRate * deltaTime;
        Motivation = Mathf.Max(0f, Motivation);
        
        OnMotivationChanged?.Invoke(Motivation);
        
        if (Motivation <= 0f)
        {
            LoseGame();
        }
    }
    
    /// <summary>
    /// Restore motivation (from matches).
    /// </summary>
    private void RestoreMotivation(float amount)
    {
        Motivation += amount;
        Motivation = Mathf.Min(startingMotivation, Motivation);
        
        OnMotivationChanged?.Invoke(Motivation);
    }
    
    /// <summary>
    /// Player wins!
    /// </summary>
    private void WinGame()
    {
        IsGameActive = false;
        Debug.Log("<color=cyan>*** YOU WIN! ***</color>");
        OnGameWon?.Invoke();
    }
    
    /// <summary>
    /// Player loses (motivation depleted).
    /// </summary>
    private void LoseGame()
    {
        IsGameActive = false;
        Debug.Log("<color=red>*** GAME OVER - Lost Motivation ***</color>");
        OnGameLost?.Invoke();
    }
    
    /// <summary>
    /// Called when player performs a swap (for motivation purposes).
    /// </summary>
    public void OnPlayerSwap()
    {
        // Could add small motivation boost for activity
        // RestoreMotivation(1f);
    }
}