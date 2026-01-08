using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Manages game state: scoring, motivation meter, win/lose conditions.
/// NEW SCORING SYSTEM:
/// - 10 pts per solve (base)
/// - Solve #2 activates multiplier bar (x1.25 displayed, 5 sec timer)
/// - Solve #3+ awards (10 × multiplier) + seconds remaining as bonus
/// - Each solve resets timer to 5 sec and increases multiplier by 0.25
/// - Bar hitting 0 hides panel and resets streak
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    [Header("Game Settings")]
    [SerializeField] private int winScore = 250;
    public int WinScore => winScore;
    [SerializeField] private float startingMotivation = 100f;
    [SerializeField] private float motivationDrainRate = 1f;
    [SerializeField] private float motivationMatchReward = 15f;
    [SerializeField] private float postWinDelay = 0.5f; // delay before win screen
    
    [Header("Scoring")]
    [SerializeField] private int baseMatchScore = 10;
    
    [Header("Multiplier Settings")]
    [SerializeField] private float multiplierDuration = 10f; // seconds
    [SerializeField] private float multiplierDrainRate = 1f; // per second
    [SerializeField] private float multiplierIncrement = 0.25f;
    [SerializeField] private float startingMultiplier = 1.25f;
    [SerializeField] private float streakTimeout = 10f; // time allowed between solves before streak resets
    
    [Header("References")]
    [SerializeField] private UIManager uiManager;
    
    // Current state
    public int Score { get; private set; }
    public float Motivation { get; private set; }
    public bool IsGameActive { get; private set; }
    public bool IsProcessing { get; set; }
    
    // Multiplier state
    private int solveCount = 0;
    private float currentMultiplier = 1f;
    private float multiplierTimer = 0f;
    private bool multiplierActive = false;
    private float timeSinceLastSolve = 0f; // tracks time for streak timeout
    
    // Public accessors for UI
    public bool IsMultiplierActive => multiplierActive;
    public float CurrentMultiplier => currentMultiplier;
    public float MultiplierTimer => multiplierTimer;
    public float MultiplierDuration => multiplierDuration;
    
    // Events for UI updates
    public event Action<int, int> OnScoreChanged; // current, delta
    public event Action<float> OnMotivationChanged;
    public event Action<bool, float, float> OnMultiplierChanged; // active, multiplier (float), timer
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
        StartNewGame();
    }
    
    private void Update()
    {
        if (!IsGameActive) return;
        
        // Drain motivation while not processing
        if (!IsProcessing)
        {
            DrainMotivation(Time.deltaTime);
        }
        
        // Drain multiplier timer if active
        if (multiplierActive)
        {
            DrainMultiplierTimer(Time.deltaTime);
        }
        else if (solveCount > 0)
        {
            // Track streak timeout before multiplier is active
            timeSinceLastSolve += Time.deltaTime;
            if (timeSinceLastSolve >= streakTimeout)
            {
                // Too long since last solve - reset streak
                solveCount = 0;
                timeSinceLastSolve = 0f;
                Debug.Log("<color=red>Streak timeout!</color> Solve count reset.");
            }
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
        
        // Reset multiplier state
        solveCount = 0;
        currentMultiplier = 1f;
        multiplierTimer = 0f;
        multiplierActive = false;
        timeSinceLastSolve = 0f;
        
        OnScoreChanged?.Invoke(Score, 0);
        OnMotivationChanged?.Invoke(Motivation);
        OnMultiplierChanged?.Invoke(false, 1f, 0f);
        
        Debug.Log("Game started!");
    }
    
    /// <summary>
    /// Called when a cascade sequence begins.
    /// </summary>
    public void OnCascadeStart()
    {
        IsProcessing = true;
    }
    
    /// <summary>
    /// Called when a cascade sequence ends.
    /// </summary>
    public void OnCascadeEnd()
    {
        IsProcessing = false;
    }
    
    /// <summary>
    /// Called when tiles are matched and cleared.
    /// </summary>
    public void OnMatchCleared(int tilesCleared, int rowsMatched, int columnsMatched)
    {
        if (!IsGameActive) return;
        
        int linesCleared = rowsMatched + columnsMatched;
        
        // Process each line as a separate solve
        for (int i = 0; i < linesCleared; i++)
        {
            ProcessSingleSolve();
        }
    }
    
    /// <summary>
    /// Process a single "Make 10" solve.
    /// </summary>
    private void ProcessSingleSolve()
    {
        solveCount++;
        timeSinceLastSolve = 0f; // Reset streak timer
        
        int pointsAwarded = 0;
        int bonusSeconds = 0;
        
        if (solveCount == 1)
        {
            // First solve: base points only
            pointsAwarded = baseMatchScore;
            Debug.Log($"<color=green>Solve #1:</color> +{pointsAwarded} pts (base)");
        }
        else if (solveCount == 2)
        {
            // Second solve: base points, ACTIVATE multiplier bar
            pointsAwarded = baseMatchScore;
            ActivateMultiplierBar();
            Debug.Log($"<color=green>Solve #2:</color> +{pointsAwarded} pts | <color=yellow>MULTIPLIER ACTIVATED (x{currentMultiplier:F2} ready)</color>");
        }
        else
        {
            // Third+ solve: multiplied points + bonus seconds
            bonusSeconds = Mathf.FloorToInt(multiplierTimer);
            int multipliedScore = Mathf.RoundToInt(baseMatchScore * currentMultiplier);
            pointsAwarded = multipliedScore + bonusSeconds;
            
            Debug.Log($"<color=green>Solve #{solveCount}:</color> ({baseMatchScore} × {currentMultiplier:F2}) + {bonusSeconds} bonus = <color=cyan>+{pointsAwarded} pts</color>");
            
            // Increase multiplier for next solve
            currentMultiplier += multiplierIncrement;
            
            // Reset timer
            multiplierTimer = multiplierDuration;
            
            // Notify UI of multiplier change
            OnMultiplierChanged?.Invoke(multiplierActive, currentMultiplier, multiplierTimer);
        }
        
        // Add score
        Score += pointsAwarded;
        OnScoreChanged?.Invoke(Score, pointsAwarded);
        
        // Restore motivation
        RestoreMotivation(motivationMatchReward);
        
        // Check win condition
        if (Score >= winScore)
        {
            StartCoroutine(WinGameDelayed());
        }
    }
    
    /// <summary>
    /// Activate the multiplier bar (on solve #2).
    /// </summary>
    private void ActivateMultiplierBar()
    {
        multiplierActive = true;
        multiplierTimer = multiplierDuration;
        currentMultiplier = startingMultiplier; // x1.25 ready for next solve
        
        OnMultiplierChanged?.Invoke(true, currentMultiplier, multiplierTimer);
    }
    
    /// <summary>
    /// Drain the multiplier timer.
    /// </summary>
    private void DrainMultiplierTimer(float deltaTime)
    {
        multiplierTimer -= multiplierDrainRate * deltaTime;
        
        // Notify UI every frame for smooth bar update
        OnMultiplierChanged?.Invoke(multiplierActive, currentMultiplier, multiplierTimer);
        
        if (multiplierTimer <= 0f)
        {
            DeactivateMultiplierBar();
        }
    }
    
    /// <summary>
    /// Deactivate multiplier bar (timer expired).
    /// </summary>
    private void DeactivateMultiplierBar()
    {
        multiplierActive = false;
        multiplierTimer = 0f;
        currentMultiplier = 1f;
        solveCount = 0; // Reset streak
        
        OnMultiplierChanged?.Invoke(false, 1f, 0f);
        
        Debug.Log("<color=red>Multiplier expired!</color> Streak reset.");
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
    /// Player wins with delay for animations to finish.
    /// </summary>
    private IEnumerator WinGameDelayed()
    {
        yield return new WaitForSeconds(postWinDelay);
        WinGame();
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
    }
}
