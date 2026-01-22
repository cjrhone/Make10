using UnityEngine;

/// <summary>
/// ScriptableObject defining an upgrade that can be purchased in the shop.
/// Upgrades are active modifiers that affect gameplay mechanics.
/// </summary>
[CreateAssetMenu(fileName = "NewUpgrade", menuName = "Make10/Upgrade Data")]
public class UpgradeData : ScriptableObject
{
    [Header("Basic Info")]
    [Tooltip("Unique identifier for this upgrade")]
    public string id;

    [Tooltip("Display name shown in shop")]
    public string displayName;

    [TextArea(2, 4)]
    [Tooltip("Description shown on card")]
    public string description;

    [Tooltip("Icon displayed on card (optional)")]
    public Sprite icon;

    [Header("Shop Settings")]
    [Tooltip("Base cost in BP")]
    public int baseCost = 50;

    [Tooltip("Category of upgrade")]
    public UpgradeType upgradeType;

    [Tooltip("Can this upgrade be purchased multiple times?")]
    public bool isStackable = false;

    [Tooltip("Maximum times this can be stacked (0 = unlimited)")]
    public int maxStacks = 3;

    [Tooltip("Minimum stage this upgrade can appear (1-4)")]
    [Range(1, 4)]
    public int minStageRequired = 1;

    [Header("Enhanced Number Settings")]
    [Tooltip("Which number this enhances (0-7), -1 if not applicable")]
    [Range(-1, 7)]
    public int targetNumber = -1;

    [Tooltip("Bonus BP per instance of this number in a match")]
    public int bonusBPPerInstance = 0;

    [Tooltip("Bonus seconds per instance (for Enhanced 0)")]
    public float bonusSecondsPerInstance = 0f;

    [Header("Multiplier Settings")]
    [Tooltip("Bonus to starting multiplier")]
    public float startingMultiplierBonus = 0f;

    [Tooltip("Bonus to multiplier increment per solve")]
    public float multiplierIncrementBonus = 0f;

    [Tooltip("Reduction to multiplier drain rate (0.25 = 25% slower)")]
    [Range(0f, 1f)]
    public float drainRateReduction = 0f;

    [Header("Time Settings")]
    [Tooltip("Bonus seconds added to starting time")]
    public float bonusStartingSeconds = 0f;

    [Tooltip("Multiplier for time bonus BP at round end")]
    public float timeBonusMultiplier = 1f;

    [Header("Tile Weight Settings")]
    [Tooltip("Target number for weight adjustment (-1 for special effects)")]
    [Range(-1, 7)]
    public int weightTargetNumber = -1;

    [Tooltip("Multiplier applied to spawn weight (1.5 = 50% more common)")]
    public float weightMultiplier = 1f;

    [Header("Combo Settings")]
    [Tooltip("Bonus BP for cascade matches")]
    public int cascadeBonusBP = 0;

    [Tooltip("Multiplier for cascade scoring (1.5 = 50% more)")]
    public float cascadeMultiplier = 1f;

    [Header("Risk/Reward Settings")]
    [Tooltip("Chance for double points (0-1)")]
    [Range(0f, 1f)]
    public float doubleChance = 0f;

    [Tooltip("Chance for half points (0-1)")]
    [Range(0f, 1f)]
    public float halfChance = 0f;

    [Tooltip("Overall BP multiplier (can be negative for trade-offs)")]
    public float overallBPMultiplier = 1f;

    [Header("Special Settings")]
    [Tooltip("Does this spawn a Free Space tile?")]
    public bool spawnsFreeSpace = false;

    [Tooltip("Number of Free Space tiles to spawn")]
    public int freeSpaceCount = 0;

    [Header("Boss Fight Settings")]
    [Tooltip("Bonus damage multiplier against bosses")]
    public float bossDamageMultiplier = 1f;

    [Tooltip("Reduction to boss attack effects (0.5 = 50% less impact)")]
    [Range(0f, 1f)]
    public float bossAttackReduction = 0f;

    /// <summary>
    /// Get the effective cost after any modifiers (e.g., Teacher's Pet artifact).
    /// </summary>
    public int GetEffectiveCost(float priceModifier = 1f)
    {
        return Mathf.RoundToInt(baseCost * priceModifier);
    }

    /// <summary>
    /// Check if this upgrade can appear in the current stage.
    /// </summary>
    public bool IsAvailableInStage(int currentStage)
    {
        return currentStage >= minStageRequired;
    }
}
