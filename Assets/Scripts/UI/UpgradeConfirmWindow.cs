using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System;

/// <summary>
/// Auto-sizing popup window for confirming upgrade purchases.
/// Shows upgrade icon, name, description, cost, and confirm/cancel buttons.
///
/// Press F3 in play mode to test with a random upgrade.
///
/// USAGE:
/// ======
/// UpgradeConfirmWindow.Instance.ShowUpgrade(upgradeData, onConfirm, onCancel);
///
/// The window will auto-size to fit the content professionally.
/// </summary>
public class UpgradeConfirmWindow : MonoBehaviour
{
    public static UpgradeConfirmWindow Instance { get; private set; }

    [Header("Window Settings")]
    [SerializeField] private float windowWidth = 850f;
    [SerializeField] private float minWindowHeight = 400f;
    [SerializeField] private float maxWindowHeight = 1200f;

    [Header("Content Settings")]
    [SerializeField] private float iconSize = 150f;
    [SerializeField] private Color costColor = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField] private Color descriptionColor = new Color(0.8f, 0.8f, 0.85f, 1f);

    [Header("Debug")]
    [SerializeField] private UpgradeData[] testUpgrades;

    private PopupWindow popupWindow;
    private UpgradeData currentUpgrade;
    private Action onConfirmCallback;
    private Action onCancelCallback;

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
        CreatePopupWindow();
        LoadTestUpgrades();
        Debug.Log("[UpgradeConfirmWindow] Press F3 to test with a random upgrade");
    }

    private void Update()
    {
        // F3 to test with random upgrade
        if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
        {
            TestWithRandomUpgrade();
        }
    }

    private void LoadTestUpgrades()
    {
        #if UNITY_EDITOR
        if (testUpgrades == null || testUpgrades.Length == 0)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:UpgradeData", new[] { "Assets/Data/Upgrades" });
            testUpgrades = new UpgradeData[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                testUpgrades[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<UpgradeData>(path);
            }
            Debug.Log($"[UpgradeConfirmWindow] Loaded {testUpgrades.Length} test upgrades");
        }
        #endif
    }

    private void TestWithRandomUpgrade()
    {
        if (testUpgrades == null || testUpgrades.Length == 0)
        {
            Debug.LogWarning("[UpgradeConfirmWindow] No test upgrades available!");
            return;
        }

        UpgradeData randomUpgrade = testUpgrades[UnityEngine.Random.Range(0, testUpgrades.Length)];
        ShowUpgrade(
            randomUpgrade,
            () => Debug.Log($"<color=green>CONFIRMED purchase of {randomUpgrade.displayName}!</color>"),
            () => Debug.Log($"<color=yellow>Cancelled purchase of {randomUpgrade.displayName}</color>")
        );
    }

    private void CreatePopupWindow()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[UpgradeConfirmWindow] No Canvas found!");
            return;
        }

        GameObject popupObj = new GameObject("UpgradeConfirmPopup");
        popupObj.transform.SetParent(canvas.transform, false);

        popupWindow = popupObj.AddComponent<PopupWindow>();
        popupWindow.SetAutoSizeMode(windowWidth, minWindowHeight, maxWindowHeight);

        popupWindow.OnWindowClosed += HandleWindowClosed;
    }

    /// <summary>
    /// Shows the upgrade confirmation window with the specified upgrade.
    /// </summary>
    /// <param name="upgrade">The upgrade to display</param>
    /// <param name="onConfirm">Called when user confirms purchase</param>
    /// <param name="onCancel">Called when user cancels (optional)</param>
    public void ShowUpgrade(UpgradeData upgrade, Action onConfirm, Action onCancel = null)
    {
        if (popupWindow == null || upgrade == null) return;

        currentUpgrade = upgrade;
        onConfirmCallback = onConfirm;
        onCancelCallback = onCancel;

        BuildUpgradeContent(upgrade);
        popupWindow.Open();
    }

    /// <summary>
    /// Closes the confirmation window without triggering callbacks.
    /// </summary>
    public void Hide()
    {
        if (popupWindow != null)
            popupWindow.Close();
    }

    private void BuildUpgradeContent(UpgradeData upgrade)
    {
        popupWindow.SetTitle("Confirm Purchase");
        popupWindow.ClearContent();

        // Icon (if available) or placeholder
        if (upgrade.icon != null)
        {
            popupWindow.AddImage(upgrade.icon, iconSize);
        }
        else
        {
            // Add a colored placeholder based on upgrade type
            Color placeholderColor = GetUpgradeTypeColor(upgrade.upgradeType);
            popupWindow.AddImagePlaceholder(iconSize, placeholderColor);
        }

        popupWindow.AddSpacer(20);

        // Upgrade name
        popupWindow.AddHeadline(upgrade.displayName);

        popupWindow.AddSpacer(10);

        // Type badge
        string typeText = $"[ {upgrade.upgradeType} ]";
        popupWindow.AddText(typeText, UIStyleGuide.FontSizeCaption, GetUpgradeTypeColor(upgrade.upgradeType),
            TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);

        popupWindow.AddSpacer(20);

        // Description
        if (!string.IsNullOrEmpty(upgrade.description))
        {
            popupWindow.AddBody(upgrade.description, descriptionColor);
            popupWindow.AddSpacer(15);
        }

        // Additional info based on upgrade effects
        string effectsText = GetUpgradeEffectsText(upgrade);
        if (!string.IsNullOrEmpty(effectsText))
        {
            popupWindow.AddDivider();
            popupWindow.AddSpacer(15);
            popupWindow.AddText(effectsText, UIStyleGuide.FontSizeCaption, UIStyleGuide.ColorTextSecondary,
                TMPro.TextAlignmentOptions.Center);
            popupWindow.AddSpacer(15);
        }

        popupWindow.AddDivider();
        popupWindow.AddSpacer(20);

        // Cost display
        int cost = upgrade.GetEffectiveCost();
        string costText = $"Cost: {cost} BP";
        popupWindow.AddText(costText, UIStyleGuide.FontSizeSubheading, costColor,
            TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);

        // Check if player can afford it
        int currentBP = RunManager.Instance?.CurrentBP ?? 0;
        if (currentBP < cost)
        {
            popupWindow.AddText($"(You have {currentBP} BP)", UIStyleGuide.FontSizeCaption, UIStyleGuide.ColorError);
        }
        else
        {
            popupWindow.AddText($"(You have {currentBP} BP)", UIStyleGuide.FontSizeCaption, UIStyleGuide.ColorSuccess);
        }

        popupWindow.AddSpacer(30);

        // Buttons
        bool canAfford = currentBP >= cost;
        popupWindow.AddButtonRow(
            ("Cancel", HandleCancel, UIStyleGuide.ColorButtonDanger),
            ("Purchase", HandleConfirm, canAfford ? UIStyleGuide.ColorButtonPrimary : UIStyleGuide.ColorButtonDisabled)
        );
    }

    private Color GetUpgradeTypeColor(UpgradeType type)
    {
        return type switch
        {
            UpgradeType.EnhancedNumber => new Color(0.3f, 0.7f, 0.4f, 1f),   // Green
            UpgradeType.Multiplier => new Color(0.9f, 0.6f, 0.2f, 1f),       // Orange
            UpgradeType.Time => new Color(0.3f, 0.6f, 0.9f, 1f),             // Blue
            UpgradeType.TileWeight => new Color(0.7f, 0.4f, 0.8f, 1f),       // Purple
            UpgradeType.Combo => new Color(0.9f, 0.3f, 0.5f, 1f),            // Pink
            UpgradeType.RiskReward => new Color(0.9f, 0.2f, 0.2f, 1f),       // Red
            UpgradeType.Information => new Color(0.4f, 0.7f, 0.9f, 1f),      // Light blue
            UpgradeType.Defensive => new Color(0.2f, 0.5f, 0.3f, 1f),        // Dark green
            UpgradeType.BossFight => new Color(0.5f, 0.2f, 0.6f, 1f),        // Dark purple
            UpgradeType.Special => new Color(1f, 0.85f, 0.3f, 1f),           // Gold
            _ => UIStyleGuide.ColorTextMuted
        };
    }

    private string GetUpgradeEffectsText(UpgradeData upgrade)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        if (upgrade.targetNumber >= 0 && upgrade.bonusBPPerInstance > 0)
            sb.AppendLine($"+{upgrade.bonusBPPerInstance} BP per {upgrade.targetNumber} matched");

        if (upgrade.bonusSecondsPerInstance > 0)
            sb.AppendLine($"+{upgrade.bonusSecondsPerInstance}s per instance");

        if (upgrade.startingMultiplierBonus > 0)
            sb.AppendLine($"+{upgrade.startingMultiplierBonus:F1}x starting multiplier");

        if (upgrade.multiplierIncrementBonus > 0)
            sb.AppendLine($"+{upgrade.multiplierIncrementBonus:F2}x multiplier per solve");

        if (upgrade.drainRateReduction > 0)
            sb.AppendLine($"-{upgrade.drainRateReduction * 100:F0}% multiplier drain");

        if (upgrade.bonusStartingSeconds > 0)
            sb.AppendLine($"+{upgrade.bonusStartingSeconds:F0}s starting time");

        if (upgrade.cascadeBonusBP > 0)
            sb.AppendLine($"+{upgrade.cascadeBonusBP} BP per cascade");

        if (upgrade.cascadeMultiplier > 1)
            sb.AppendLine($"{upgrade.cascadeMultiplier:F1}x cascade bonus");

        if (upgrade.doubleChance > 0)
            sb.AppendLine($"{upgrade.doubleChance * 100:F0}% chance for 2x points");

        if (upgrade.isStackable)
            sb.AppendLine($"Stackable (max {(upgrade.maxStacks > 0 ? upgrade.maxStacks.ToString() : "unlimited")})");

        return sb.ToString().TrimEnd();
    }

    private void HandleConfirm()
    {
        if (currentUpgrade != null)
        {
            int cost = currentUpgrade.GetEffectiveCost();
            int currentBP = RunManager.Instance?.CurrentBP ?? 0;

            if (currentBP >= cost)
            {
                onConfirmCallback?.Invoke();
            }
            else
            {
                Debug.Log($"[UpgradeConfirmWindow] Cannot afford {currentUpgrade.displayName} (need {cost} BP, have {currentBP})");
            }
        }
        popupWindow.Close();
    }

    private void HandleCancel()
    {
        onCancelCallback?.Invoke();
        popupWindow.Close();
    }

    private void HandleWindowClosed()
    {
        // Clear state when window closes
        currentUpgrade = null;
        onConfirmCallback = null;
        onCancelCallback = null;
    }
}
