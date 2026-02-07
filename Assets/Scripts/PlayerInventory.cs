using UnityEngine;

/// <summary>
/// Minimal inventory shell (arcade mode - no upgrades/snacks/artifacts).
/// Kept for API compatibility with CampaignManager.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

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
    /// Clear inventory (no-op in arcade mode, kept for compatibility).
    /// </summary>
    public void ClearInventory()
    {
        Debug.Log("[PlayerInventory] ClearInventory called (arcade mode - no-op)");
    }
}
