using UnityEngine;
using System;

/// <summary>
/// Manages persistent state across rounds within a single run.
/// Tracks BP (Brain Points) currency and round progression.
/// </summary>
public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    [Header("Run State")]
    [SerializeField] private int startingBP = 0;

    // Current run state
    public int CurrentBP { get; private set; }
    public int RoundNumber { get; private set; }
    public bool IsRunActive { get; private set; }

    // Events for UI updates
    public event Action<int> OnBPChanged;
    public event Action<int> OnRoundChanged;
    public event Action OnRunStarted;
    public event Action OnRunEnded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Start a new run. Resets BP and round number.
    /// </summary>
    public void StartNewRun()
    {
        CurrentBP = startingBP;
        RoundNumber = 1;
        IsRunActive = true;

        Debug.Log($"<color=cyan>[RunManager] New run started! BP: {CurrentBP}, Round: {RoundNumber}</color>");

        OnBPChanged?.Invoke(CurrentBP);
        OnRoundChanged?.Invoke(RoundNumber);
        OnRunStarted?.Invoke();
    }

    /// <summary>
    /// End the current run (player quit or lost).
    /// </summary>
    public void EndRun()
    {
        IsRunActive = false;

        Debug.Log($"<color=red>[RunManager] Run ended. Final BP: {CurrentBP}, Rounds completed: {RoundNumber - 1}</color>");

        OnRunEnded?.Invoke();
    }

    /// <summary>
    /// Add BP to the player's total (called when winning a round).
    /// </summary>
    public void AddBP(int amount)
    {
        if (amount <= 0) return;

        int previousBP = CurrentBP;
        CurrentBP += amount;

        Debug.Log($"<color=green>[RunManager] +{amount} BP ({previousBP} → {CurrentBP})</color>");

        OnBPChanged?.Invoke(CurrentBP);
    }

    /// <summary>
    /// Spend BP on an upgrade (returns false if insufficient funds).
    /// </summary>
    public bool SpendBP(int amount)
    {
        if (amount <= 0) return false;
        if (CurrentBP < amount)
        {
            Debug.Log($"<color=yellow>[RunManager] Cannot spend {amount} BP - only have {CurrentBP}</color>");
            return false;
        }

        int previousBP = CurrentBP;
        CurrentBP -= amount;

        Debug.Log($"<color=orange>[RunManager] -{amount} BP ({previousBP} → {CurrentBP})</color>");

        OnBPChanged?.Invoke(CurrentBP);
        return true;
    }

    /// <summary>
    /// Advance to the next round.
    /// </summary>
    public void AdvanceRound()
    {
        RoundNumber++;

        Debug.Log($"<color=cyan>[RunManager] Advanced to Round {RoundNumber}</color>");

        OnRoundChanged?.Invoke(RoundNumber);
    }

    /// <summary>
    /// Check if player can afford a cost.
    /// </summary>
    public bool CanAfford(int cost)
    {
        return CurrentBP >= cost;
    }
}
