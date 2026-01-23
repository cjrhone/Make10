using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Tracks all upgrades, snacks, and artifacts the player has acquired during a run.
/// Provides methods to calculate aggregate bonuses from all items.
/// Resets at the start of each new run.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private int debugStartingBP = 1000;

    // Purchased upgrades with stack counts
    private Dictionary<UpgradeData, int> upgrades = new Dictionary<UpgradeData, int>();

    // Collected snacks
    private List<SnackData> snacks = new List<SnackData>();

    // Collected artifacts
    private List<ArtifactData> artifacts = new List<ArtifactData>();

    // Per-round snack trigger tracking
    private Dictionary<SnackData, int> snackTriggersThisRound = new Dictionary<SnackData, int>();

    // Events
    public event System.Action<UpgradeData, int> OnUpgradeAdded;
    public event System.Action<SnackData> OnSnackAdded;
    public event System.Action<ArtifactData> OnArtifactAdded;
    public event System.Action OnInventoryCleared;

    #region Initialization

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
    /// Clear all items (called at start of new run).
    /// </summary>
    public void ClearInventory()
    {
        upgrades.Clear();
        snacks.Clear();
        artifacts.Clear();
        snackTriggersThisRound.Clear();

        OnInventoryCleared?.Invoke();
        Debug.Log("[PlayerInventory] Inventory cleared for new run");
    }

    /// <summary>
    /// Reset per-round tracking (called at start of each round).
    /// </summary>
    public void ResetRoundTracking()
    {
        snackTriggersThisRound.Clear();
        Debug.Log("[PlayerInventory] Round tracking reset");
    }

    #endregion

    #region Adding Items

    /// <summary>
    /// Add an upgrade to the inventory.
    /// </summary>
    public bool AddUpgrade(UpgradeData upgrade)
    {
        if (upgrade == null) return false;

        if (upgrades.ContainsKey(upgrade))
        {
            // Check stack limit
            if (!upgrade.isStackable)
            {
                Debug.LogWarning($"[PlayerInventory] Cannot stack non-stackable upgrade: {upgrade.displayName}");
                return false;
            }

            if (upgrade.maxStacks > 0 && upgrades[upgrade] >= upgrade.maxStacks)
            {
                Debug.LogWarning($"[PlayerInventory] Upgrade at max stacks: {upgrade.displayName}");
                return false;
            }

            upgrades[upgrade]++;
        }
        else
        {
            upgrades[upgrade] = 1;
        }

        Debug.Log($"[PlayerInventory] Added upgrade: {upgrade.displayName} (x{upgrades[upgrade]})");
        OnUpgradeAdded?.Invoke(upgrade, upgrades[upgrade]);
        return true;
    }

    /// <summary>
    /// Add a snack to the inventory.
    /// </summary>
    public bool AddSnack(SnackData snack)
    {
        if (snack == null) return false;

        // Check if unique and already owned
        if (snack.isUnique && snacks.Any(s => s.snackType == snack.snackType))
        {
            Debug.LogWarning($"[PlayerInventory] Already own unique snack: {snack.displayName}");
            return false;
        }

        snacks.Add(snack);
        Debug.Log($"[PlayerInventory] Added snack: {snack.displayName}");
        OnSnackAdded?.Invoke(snack);
        return true;
    }

    /// <summary>
    /// Add an artifact to the inventory.
    /// </summary>
    public bool AddArtifact(ArtifactData artifact)
    {
        if (artifact == null) return false;

        // Check if already owned
        if (artifacts.Any(a => a.artifactType == artifact.artifactType))
        {
            Debug.LogWarning($"[PlayerInventory] Already own artifact: {artifact.displayName}");
            return false;
        }

        artifacts.Add(artifact);
        Debug.Log($"[PlayerInventory] Added artifact: {artifact.displayName}");
        OnArtifactAdded?.Invoke(artifact);
        return true;
    }

    #endregion

    #region Upgrade Bonuses

    /// <summary>
    /// Get bonus BP for a specific number appearing in a match.
    /// </summary>
    public int GetEnhancedNumberBonus(int number)
    {
        int bonus = 0;

        foreach (var kvp in upgrades)
        {
            UpgradeData upgrade = kvp.Key;
            int stacks = kvp.Value;

            if (upgrade.upgradeType == UpgradeType.EnhancedNumber && upgrade.targetNumber == number)
            {
                bonus += upgrade.bonusBPPerInstance * stacks;
            }
        }

        // Golden Pencil artifact doubles enhanced number bonuses
        if (HasArtifact(ArtifactType.GoldenPencil))
        {
            bonus *= 2;
        }

        // Sticky Notes snack adds +1 to all enhanced bonuses
        if (bonus > 0 && HasSnack(SnackType.StickyNotes))
        {
            bonus += GetSnackCount(SnackType.StickyNotes);
        }

        return bonus;
    }

    /// <summary>
    /// Get bonus seconds for Enhanced 0 (wildcard time bonus).
    /// </summary>
    public float GetEnhancedZeroTimeBonus()
    {
        float bonus = 0f;

        foreach (var kvp in upgrades)
        {
            UpgradeData upgrade = kvp.Key;
            int stacks = kvp.Value;

            if (upgrade.upgradeType == UpgradeType.EnhancedNumber && upgrade.targetNumber == 0)
            {
                bonus += upgrade.bonusSecondsPerInstance * stacks;
            }
        }

        return bonus;
    }

    /// <summary>
    /// Get total starting multiplier bonus.
    /// </summary>
    public float GetStartingMultiplierBonus()
    {
        float bonus = 0f;

        // From upgrades
        foreach (var kvp in upgrades)
        {
            bonus += kvp.Key.startingMultiplierBonus * kvp.Value;
        }

        // Coffee Mug snack
        if (HasSnack(SnackType.CoffeeMug))
        {
            SnackData coffeeMug = GetSnack(SnackType.CoffeeMug);
            if (coffeeMug != null)
            {
                bonus += coffeeMug.effectValue;
            }
        }

        return bonus;
    }

    /// <summary>
    /// Get multiplier increment bonus.
    /// </summary>
    public float GetMultiplierIncrementBonus()
    {
        float bonus = 0f;

        foreach (var kvp in upgrades)
        {
            bonus += kvp.Key.multiplierIncrementBonus * kvp.Value;
        }

        return bonus;
    }

    /// <summary>
    /// Get multiplier drain rate reduction (0-1, where 0.25 = 25% slower).
    /// </summary>
    public float GetDrainRateReduction()
    {
        float reduction = 0f;

        // From upgrades
        foreach (var kvp in upgrades)
        {
            reduction += kvp.Key.drainRateReduction * kvp.Value;
        }

        // Metronome snack
        if (HasSnack(SnackType.Metronome))
        {
            SnackData metronome = GetSnack(SnackType.Metronome);
            if (metronome != null)
            {
                reduction += metronome.effectValue;
            }
        }

        return Mathf.Clamp01(reduction); // Cap at 100% reduction
    }

    /// <summary>
    /// Get bonus starting time in seconds.
    /// </summary>
    public float GetBonusStartingTime()
    {
        float bonus = 0f;

        // From upgrades
        foreach (var kvp in upgrades)
        {
            bonus += kvp.Key.bonusStartingSeconds * kvp.Value;
        }

        // Night Owl artifact
        if (HasArtifact(ArtifactType.NightOwl))
        {
            ArtifactData nightOwl = GetArtifact(ArtifactType.NightOwl);
            if (nightOwl != null)
            {
                bonus += nightOwl.effectValue;
            }
        }

        return bonus;
    }

    /// <summary>
    /// Get base score bonus (added to 10).
    /// </summary>
    public int GetBaseScoreBonus()
    {
        int bonus = 0;

        // Brain Food snack
        if (HasSnack(SnackType.BrainFood))
        {
            SnackData brainFood = GetSnack(SnackType.BrainFood);
            if (brainFood != null)
            {
                bonus += Mathf.RoundToInt(brainFood.effectValue);
            }
        }

        return bonus;
    }

    /// <summary>
    /// Get overall BP multiplier from all sources.
    /// </summary>
    public float GetOverallBPMultiplier()
    {
        float multiplier = 1f;

        // From upgrades (additive)
        foreach (var kvp in upgrades)
        {
            if (kvp.Key.overallBPMultiplier != 1f)
            {
                multiplier += (kvp.Key.overallBPMultiplier - 1f) * kvp.Value;
            }
        }

        // Textbook snack (+10% per)
        if (HasSnack(SnackType.Textbook))
        {
            SnackData textbook = GetSnack(SnackType.Textbook);
            if (textbook != null)
            {
                multiplier += textbook.effectValue * GetSnackCount(SnackType.Textbook);
            }
        }

        // Overachiever artifact (+25%)
        if (HasArtifact(ArtifactType.Overachiever))
        {
            ArtifactData overachiever = GetArtifact(ArtifactType.Overachiever);
            if (overachiever != null)
            {
                multiplier += overachiever.effectValue;
            }
        }

        return multiplier;
    }

    /// <summary>
    /// Get price modifier for shop (Teacher's Pet = -20%).
    /// </summary>
    public float GetPriceModifier()
    {
        float modifier = 1f;

        if (HasArtifact(ArtifactType.TeachersPet))
        {
            ArtifactData teachersPet = GetArtifact(ArtifactType.TeachersPet);
            if (teachersPet != null)
            {
                modifier *= teachersPet.effectValue;
            }
        }

        return modifier;
    }

    /// <summary>
    /// Get Hot Streak duration bonus.
    /// </summary>
    public float GetHotStreakDurationBonus()
    {
        float bonus = 0f;

        // Study Glasses snack
        if (HasSnack(SnackType.StudyGlasses))
        {
            SnackData studyGlasses = GetSnack(SnackType.StudyGlasses);
            if (studyGlasses != null)
            {
                bonus += studyGlasses.effectValue;
            }
        }

        return bonus;
    }

    /// <summary>
    /// Get Hot Streak multiplier bonus (Red Bull = x6 instead of x5).
    /// </summary>
    public float GetHotStreakMultiplierBonus()
    {
        float bonus = 0f;

        // Red Bull snack
        if (HasSnack(SnackType.RedBull))
        {
            SnackData redBull = GetSnack(SnackType.RedBull);
            if (redBull != null)
            {
                bonus += redBull.effectValue;
            }
        }

        return bonus;
    }

    /// <summary>
    /// Get multiplier cap (Night Owl limits to 2.5).
    /// </summary>
    public float GetMultiplierCap()
    {
        if (HasArtifact(ArtifactType.NightOwl))
        {
            ArtifactData nightOwl = GetArtifact(ArtifactType.NightOwl);
            if (nightOwl != null)
            {
                return nightOwl.downsideValue;
            }
        }

        return -1f; // No cap
    }

    /// <summary>
    /// Get Hot Streak trigger threshold (Cramming = 2.5 instead of 3.0).
    /// </summary>
    public float GetHotStreakThreshold()
    {
        if (HasArtifact(ArtifactType.Cramming))
        {
            ArtifactData cramming = GetArtifact(ArtifactType.Cramming);
            if (cramming != null)
            {
                return cramming.effectValue;
            }
        }

        return -1f; // Use default
    }

    /// <summary>
    /// Get hint delay reduction.
    /// </summary>
    public float GetHintDelayReduction()
    {
        float reduction = 0f;

        // Flashcards snack
        if (HasSnack(SnackType.Flashcards))
        {
            SnackData flashcards = GetSnack(SnackType.Flashcards);
            if (flashcards != null)
            {
                reduction += flashcards.effectValue;
            }
        }

        return reduction;
    }

    #endregion

    #region Item Queries

    /// <summary>
    /// Check if player has a specific upgrade by ID.
    /// </summary>
    public bool HasUpgrade(string upgradeId)
    {
        return upgrades.Keys.Any(u => u.id == upgradeId);
    }

    /// <summary>
    /// Check if player has a specific snack by ID.
    /// </summary>
    public bool HasSnack(string snackId)
    {
        return snacks.Any(s => s.id == snackId);
    }

    /// <summary>
    /// Check if player has a specific snack type.
    /// </summary>
    public bool HasSnack(SnackType type)
    {
        return snacks.Any(s => s.snackType == type);
    }

    /// <summary>
    /// Get the snack data for a specific type.
    /// </summary>
    public SnackData GetSnack(SnackType type)
    {
        return snacks.FirstOrDefault(s => s.snackType == type);
    }

    /// <summary>
    /// Get count of a specific snack type.
    /// </summary>
    public int GetSnackCount(SnackType type)
    {
        return snacks.Count(s => s.snackType == type);
    }

    /// <summary>
    /// Try to trigger a snack effect (respects per-round limits).
    /// Returns true if triggered successfully.
    /// </summary>
    public bool TryTriggerSnack(SnackType type)
    {
        SnackData snack = GetSnack(type);
        if (snack == null) return false;

        // Check trigger limits
        if (snack.maxTriggersPerRound > 0)
        {
            if (!snackTriggersThisRound.ContainsKey(snack))
            {
                snackTriggersThisRound[snack] = 0;
            }

            if (snackTriggersThisRound[snack] >= snack.maxTriggersPerRound)
            {
                return false;
            }

            snackTriggersThisRound[snack]++;
        }

        if (snack.oncePerRound)
        {
            if (snackTriggersThisRound.ContainsKey(snack) && snackTriggersThisRound[snack] > 0)
            {
                return false;
            }
            snackTriggersThisRound[snack] = 1;
        }

        Debug.Log($"[PlayerInventory] Snack triggered: {snack.displayName}");
        return true;
    }

    #endregion

    #region Artifact Queries

    /// <summary>
    /// Check if player has a specific artifact type.
    /// </summary>
    public bool HasArtifact(ArtifactType type)
    {
        return artifacts.Any(a => a.artifactType == type);
    }

    /// <summary>
    /// Get the artifact data for a specific type.
    /// </summary>
    public ArtifactData GetArtifact(ArtifactType type)
    {
        return artifacts.FirstOrDefault(a => a.artifactType == type);
    }

    #endregion

    #region Queries

    /// <summary>
    /// Get all owned upgrades with stack counts.
    /// </summary>
    public Dictionary<UpgradeData, int> GetAllUpgrades()
    {
        return new Dictionary<UpgradeData, int>(upgrades);
    }

    /// <summary>
    /// Get all owned snacks.
    /// </summary>
    public List<SnackData> GetAllSnacks()
    {
        return new List<SnackData>(snacks);
    }

    /// <summary>
    /// Get all owned artifacts.
    /// </summary>
    public List<ArtifactData> GetAllArtifacts()
    {
        return new List<ArtifactData>(artifacts);
    }

    /// <summary>
    /// Get total number of items owned.
    /// </summary>
    public int GetTotalItemCount()
    {
        int count = 0;
        foreach (var kvp in upgrades)
        {
            count += kvp.Value;
        }
        count += snacks.Count;
        count += artifacts.Count;
        return count;
    }

    /// <summary>
    /// Check if player can stack another of this upgrade.
    /// </summary>
    public bool CanAddUpgrade(UpgradeData upgrade)
    {
        if (upgrade == null) return false;

        if (!upgrades.ContainsKey(upgrade)) return true;

        if (!upgrade.isStackable) return false;

        if (upgrade.maxStacks > 0 && upgrades[upgrade] >= upgrade.maxStacks) return false;

        return true;
    }

    /// <summary>
    /// Check if player can add this snack.
    /// </summary>
    public bool CanAddSnack(SnackData snack)
    {
        if (snack == null) return false;

        if (snack.isUnique && snacks.Any(s => s.snackType == snack.snackType)) return false;

        return true;
    }

    #endregion

    #region Debug

    /// <summary>
    /// Debug mode: Give player starting BP.
    /// </summary>
    public void ApplyDebugStartingBP()
    {
        if (debugMode && RunManager.Instance != null)
        {
            RunManager.Instance.AddBP(debugStartingBP);
            Debug.Log($"[PlayerInventory] DEBUG: Added {debugStartingBP} starting BP");
        }
    }

    /// <summary>
    /// Debug: Add an upgrade by ID (for testing).
    /// </summary>
    public void DebugAddUpgrade(string upgradeId)
    {
        // This would need a reference to the upgrade database
        Debug.Log($"[PlayerInventory] DEBUG: Would add upgrade '{upgradeId}'");
    }

    /// <summary>
    /// Debug: Print inventory contents.
    /// </summary>
    public void DebugPrintInventory()
    {
        Debug.Log("=== PLAYER INVENTORY ===");

        Debug.Log("UPGRADES:");
        foreach (var kvp in upgrades)
        {
            Debug.Log($"  - {kvp.Key.displayName} x{kvp.Value}");
        }

        Debug.Log("SNACKS:");
        foreach (var snack in snacks)
        {
            Debug.Log($"  - {snack.displayName}");
        }

        Debug.Log("ARTIFACTS:");
        foreach (var artifact in artifacts)
        {
            Debug.Log($"  - {artifact.displayName}");
        }

        Debug.Log("========================");
    }

    #endregion
}
