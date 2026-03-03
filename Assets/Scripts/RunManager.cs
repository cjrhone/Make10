using UnityEngine;
using System;

/// <summary>
/// Manages persistent BP (Brain Points) currency across all sessions.
/// TotalBP = lifetime earned (display stat, never decreases).
/// SpendableBP = current balance (decreases on shop purchases).
/// Also manages per-run state (round number, run active flag).
/// </summary>
public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    // PlayerPrefs keys for persistent BP
    private const string TOTAL_BP_KEY = "Make10_TotalBP";
    private const string SPENDABLE_BP_KEY = "Make10_SpendableBP";

    [Header("Run State")]
    [SerializeField] private int startingBP = 0;

    // Current run state (per-session, resets each run)
    public int CurrentBP { get; private set; }
    public int RoundNumber { get; private set; }
    public bool IsRunActive { get; private set; }

    // Persistent BP (saved to PlayerPrefs)
    public int TotalBP => PlayerPrefs.GetInt(TOTAL_BP_KEY, 0);
    public int SpendableBP => PlayerPrefs.GetInt(SPENDABLE_BP_KEY, 0);

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
    /// Start a new run. Resets per-session BP and round number.
    /// </summary>
    public void StartNewRun()
    {
        CurrentBP = startingBP;
        RoundNumber = 1;
        IsRunActive = true;

        Debug.Log($"<color=cyan>[RunManager] New run started! BP: {CurrentBP}, Round: {RoundNumber}, Lifetime: {TotalBP}, Spendable: {SpendableBP}</color>");

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
    /// Add BP to the player's per-session total (called during gameplay).
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
    /// Add earned BP to both persistent totals (TotalBP and SpendableBP).
    /// Called at end of each round from the results screen.
    /// </summary>
    public void BankBP(int amount)
    {
        if (amount <= 0) return;

        int prevTotal = TotalBP;
        int prevSpendable = SpendableBP;

        PlayerPrefs.SetInt(TOTAL_BP_KEY, prevTotal + amount);
        PlayerPrefs.SetInt(SPENDABLE_BP_KEY, prevSpendable + amount);
        PlayerPrefs.Save();

        Debug.Log($"<color=green>[RunManager] Banked +{amount} BP! Total: {prevTotal} → {TotalBP}, Spendable: {prevSpendable} → {SpendableBP}</color>");

        // Notify listeners (e.g. MainMenuUI BP display) of the updated balance
        OnBPChanged?.Invoke(SpendableBP);
    }

    /// <summary>
    /// Spend BP from the spendable balance (for shop purchases).
    /// Returns false if insufficient funds.
    /// </summary>
    public bool SpendBP(int amount)
    {
        if (amount <= 0) return false;
        int current = SpendableBP;
        if (current < amount)
        {
            Debug.Log($"<color=yellow>[RunManager] Cannot spend {amount} BP - only have {current}</color>");
            return false;
        }

        PlayerPrefs.SetInt(SPENDABLE_BP_KEY, current - amount);
        PlayerPrefs.Save();

        Debug.Log($"<color=orange>[RunManager] -{amount} BP ({current} → {SpendableBP})</color>");

        OnBPChanged?.Invoke(SpendableBP);
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
        return SpendableBP >= cost;
    }
}
