using UnityEngine;

/// <summary>
/// ScriptableObject defining a snack (passive artifact) that persists for the run.
/// Snacks provide ongoing benefits without requiring player action.
/// </summary>
[CreateAssetMenu(fileName = "NewSnack", menuName = "Make10/Snack Data")]
public class SnackData : ScriptableObject
{
    [Header("Basic Info")]
    [Tooltip("Unique identifier for this snack")]
    public string id;

    [Tooltip("Display name shown in shop and inventory")]
    public string displayName;

    [TextArea(2, 4)]
    [Tooltip("Description shown on card")]
    public string description;

    [Tooltip("Icon displayed on card and in inventory")]
    public Sprite icon;

    [Header("Shop Settings")]
    [Tooltip("Cost in BP")]
    public int cost = 100;

    [Tooltip("Type of snack (determines behavior)")]
    public SnackType snackType;

    [Tooltip("Can only have one of this snack?")]
    public bool isUnique = true;

    [Tooltip("Minimum stage this snack can appear (1-4)")]
    [Range(1, 4)]
    public int minStageRequired = 1;

    [Header("Effect Values")]
    [Tooltip("Primary effect value (meaning depends on snack type)")]
    public float effectValue = 0f;

    [Tooltip("Secondary effect value (for complex snacks)")]
    public float secondaryValue = 0f;

    [Tooltip("Chance-based effect probability (0-1)")]
    [Range(0f, 1f)]
    public float effectChance = 1f;

    [Header("Trigger Settings")]
    [Tooltip("How often can this snack trigger per round?")]
    public int maxTriggersPerRound = -1; // -1 = unlimited

    [Tooltip("Does this snack trigger once per round then disable?")]
    public bool oncePerRound = false;

    /// <summary>
    /// Get description with actual values filled in.
    /// </summary>
    public string GetFormattedDescription()
    {
        return description
            .Replace("{value}", effectValue.ToString("F0"))
            .Replace("{value2}", secondaryValue.ToString("F0"))
            .Replace("{chance}", (effectChance * 100f).ToString("F0"));
    }

    /// <summary>
    /// Get the effective cost after any modifiers.
    /// </summary>
    public int GetEffectiveCost(float priceModifier = 1f)
    {
        return Mathf.RoundToInt(cost * priceModifier);
    }

    /// <summary>
    /// Check if this snack can appear in the current stage.
    /// </summary>
    public bool IsAvailableInStage(int currentStage)
    {
        return currentStage >= minStageRequired;
    }
}
