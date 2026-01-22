using UnityEngine;

/// <summary>
/// ScriptableObject defining a rare artifact awarded after boss fights.
/// Artifacts are powerful run-defining effects.
/// </summary>
[CreateAssetMenu(fileName = "NewArtifact", menuName = "Make10/Artifact Data")]
public class ArtifactData : ScriptableObject
{
    [Header("Basic Info")]
    [Tooltip("Unique identifier for this artifact")]
    public string id;

    [Tooltip("Display name shown in selection screen")]
    public string displayName;

    [TextArea(2, 4)]
    [Tooltip("Description of the artifact's effect")]
    public string description;

    [Tooltip("Icon displayed in selection and inventory")]
    public Sprite icon;

    [Header("Availability")]
    [Tooltip("Type of artifact (determines behavior)")]
    public ArtifactType artifactType;

    [Tooltip("Minimum boss defeated to unlock (1-3)")]
    [Range(1, 3)]
    public int minBossRequired = 1;

    [Tooltip("Rarity weight (higher = more likely to appear)")]
    [Range(1, 10)]
    public int rarityWeight = 5;

    [Header("Effect Values")]
    [Tooltip("Primary effect multiplier/value")]
    public float effectValue = 1f;

    [Tooltip("Secondary effect value")]
    public float secondaryValue = 0f;

    [Header("Trade-off Settings")]
    [Tooltip("Does this artifact have a downside?")]
    public bool hasDownside = false;

    [Tooltip("Description of the downside")]
    public string downsideDescription;

    [Tooltip("Downside effect value")]
    public float downsideValue = 0f;

    /// <summary>
    /// Get description with actual values filled in.
    /// </summary>
    public string GetFormattedDescription()
    {
        string result = description
            .Replace("{value}", effectValue.ToString("F0"))
            .Replace("{value2}", secondaryValue.ToString("F0"));

        if (hasDownside)
        {
            result += $"\n<color=#E74C3C>{downsideDescription.Replace("{downside}", downsideValue.ToString("F0"))}</color>";
        }

        return result;
    }

    /// <summary>
    /// Check if this artifact can appear after the given boss.
    /// </summary>
    public bool IsAvailableAfterBoss(int bossNumber)
    {
        return bossNumber >= minBossRequired;
    }
}
