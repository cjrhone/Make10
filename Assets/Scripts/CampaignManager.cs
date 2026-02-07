using UnityEngine;

/// <summary>
/// Minimal round counter. Tracks current round number and fires round change events.
/// </summary>
public class CampaignManager : MonoBehaviour
{
    public static CampaignManager Instance { get; private set; }

    [SerializeField] private int currentRoundNumber = 1; // 1-indexed

    public int CurrentRound => currentRoundNumber;

    public event System.Action<int> OnRoundChanged; // round

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
    /// Start a new campaign by resetting the round counter to 1 and clearing inventory.
    /// </summary>
    public void StartNewCampaign()
    {
        currentRoundNumber = 1;
        PlayerInventory.Instance?.ClearInventory();
        Debug.Log("[CampaignManager] New campaign started");
    }

    /// <summary>
    /// Advance to the next round and fire the OnRoundChanged event.
    /// </summary>
    public void AdvanceRound()
    {
        currentRoundNumber++;
        Debug.Log($"[CampaignManager] Advanced to Round {currentRoundNumber}");
        OnRoundChanged?.Invoke(currentRoundNumber);
    }
}
