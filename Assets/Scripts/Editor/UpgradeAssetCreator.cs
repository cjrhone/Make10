using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Editor utility to create sample upgrade, snack, and artifact assets for testing.
/// Access via menu: Make10/Create Sample Data Assets
/// </summary>
public class UpgradeAssetCreator : Editor
{
    private const string UPGRADES_PATH = "Assets/Data/Upgrades/";
    private const string SNACKS_PATH = "Assets/Data/Snacks/";
    private const string ARTIFACTS_PATH = "Assets/Data/Artifacts/";

    [MenuItem("Make10/Create Sample Data Assets")]
    public static void CreateSampleAssets()
    {
        // Ensure directories exist
        EnsureDirectory(UPGRADES_PATH);
        EnsureDirectory(SNACKS_PATH);
        EnsureDirectory(ARTIFACTS_PATH);

        // Create Enhanced Number upgrades (0-7)
        CreateEnhancedNumberUpgrades();

        // Create Multiplier upgrades
        CreateMultiplierUpgrades();

        // Create Time upgrades
        CreateTimeUpgrades();

        // Create Snacks
        CreateSnacks();

        // Create Artifacts
        CreateArtifacts();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[UpgradeAssetCreator] Created all sample data assets!");
    }

    private static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    #region Enhanced Numbers

    private static void CreateEnhancedNumberUpgrades()
    {
        // Enhanced 0 (special - gives time bonus)
        CreateUpgrade("Enhanced0", new UpgradeData
        {
            id = "enhanced_0",
            displayName = "Enhanced 0",
            description = "+5 seconds for each '0' in a Make10 match",
            baseCost = 25,
            upgradeType = UpgradeType.EnhancedNumber,
            isStackable = true,
            maxStacks = 3,
            minStageRequired = 1,
            targetNumber = 0,
            bonusBPPerInstance = 0,
            bonusSecondsPerInstance = 5f
        });

        // Enhanced 1-7
        for (int i = 1; i <= 7; i++)
        {
            int cost = 20 + (i * 10); // 30, 40, 50, 60, 70, 80, 90
            int minStage = i <= 5 ? 1 : (i == 6 ? 2 : 3); // 6 needs Stage 2, 7 needs Stage 3

            CreateUpgrade($"Enhanced{i}", new UpgradeData
            {
                id = $"enhanced_{i}",
                displayName = $"Enhanced {i}",
                description = $"+{i} BP for each '{i}' in a Make10 match",
                baseCost = cost,
                upgradeType = UpgradeType.EnhancedNumber,
                isStackable = true,
                maxStacks = 5,
                minStageRequired = minStage,
                targetNumber = i,
                bonusBPPerInstance = i,
                bonusSecondsPerInstance = 0f
            });
        }
    }

    #endregion

    #region Multiplier Upgrades

    private static void CreateMultiplierUpgrades()
    {
        // Quick Start - Begin at x1.5 instead of x1.25
        CreateUpgrade("QuickStart", new UpgradeData
        {
            id = "quick_start",
            displayName = "Quick Start",
            description = "Begin rounds at x1.5 multiplier instead of x1.25",
            baseCost = 75,
            upgradeType = UpgradeType.Multiplier,
            isStackable = true,
            maxStacks = 2,
            minStageRequired = 1,
            startingMultiplierBonus = 0.25f
        });

        // Momentum - Multiplier increments faster
        CreateUpgrade("Momentum", new UpgradeData
        {
            id = "momentum",
            displayName = "Momentum",
            description = "Multiplier increases +0.10 faster per solve",
            baseCost = 100,
            upgradeType = UpgradeType.Multiplier,
            isStackable = true,
            maxStacks = 3,
            minStageRequired = 1,
            multiplierIncrementBonus = 0.10f
        });

        // Sustain - Multiplier drains slower
        CreateUpgrade("Sustain", new UpgradeData
        {
            id = "sustain",
            displayName = "Sustain",
            description = "Multiplier drains 25% slower",
            baseCost = 80,
            upgradeType = UpgradeType.Multiplier,
            isStackable = true,
            maxStacks = 3,
            minStageRequired = 1,
            drainRateReduction = 0.25f
        });
    }

    #endregion

    #region Time Upgrades

    private static void CreateTimeUpgrades()
    {
        // Extra Credit - More starting time
        CreateUpgrade("ExtraCredit", new UpgradeData
        {
            id = "extra_credit",
            displayName = "Extra Credit",
            description = "+5 seconds to starting time",
            baseCost = 50,
            upgradeType = UpgradeType.Time,
            isStackable = true,
            maxStacks = 4,
            minStageRequired = 1,
            bonusStartingSeconds = 5f
        });
    }

    #endregion

    #region Snacks

    private static void CreateSnacks()
    {
        // Stopwatch - Emergency time
        CreateSnack("Stopwatch", new SnackData
        {
            id = "stopwatch",
            displayName = "Stopwatch",
            description = "When timer hits 0, gain +10 seconds (once per round)",
            cost = 100,
            snackType = SnackType.Stopwatch,
            isUnique = true,
            minStageRequired = 1,
            effectValue = 10f,
            oncePerRound = true
        });

        // 10/10 Sandwich
        CreateSnack("TenTenSandwich", new SnackData
        {
            id = "sandwich",
            displayName = "10/10 Sandwich",
            description = "Every 10th Make10 grants +100 BP",
            cost = 150,
            snackType = SnackType.TenTenSandwich,
            isUnique = true,
            minStageRequired = 1,
            effectValue = 100f
        });

        // Energy Drink - Start in Hot Streak
        CreateSnack("EnergyDrink", new SnackData
        {
            id = "energy_drink",
            displayName = "Energy Drink",
            description = "Start each round in Hot Streak mode!",
            cost = 200,
            snackType = SnackType.EnergyDrink,
            isUnique = true,
            minStageRequired = 2
        });

        // Study Glasses - Hot Streak duration
        CreateSnack("StudyGlasses", new SnackData
        {
            id = "study_glasses",
            displayName = "Study Glasses",
            description = "Hot Streak duration +5 seconds",
            cost = 125,
            snackType = SnackType.StudyGlasses,
            isUnique = true,
            minStageRequired = 1,
            effectValue = 5f
        });

        // Coffee Mug - Starting multiplier
        CreateSnack("CoffeeMug", new SnackData
        {
            id = "coffee_mug",
            displayName = "Coffee Mug",
            description = "Start with x1.25 multiplier bonus",
            cost = 75,
            snackType = SnackType.CoffeeMug,
            isUnique = false,
            minStageRequired = 1,
            effectValue = 0.25f
        });

        // Brain Food - Base score increase
        CreateSnack("BrainFood", new SnackData
        {
            id = "brain_food",
            displayName = "Brain Food",
            description = "Base match score +2 (10 → 12)",
            cost = 100,
            snackType = SnackType.BrainFood,
            isUnique = true,
            minStageRequired = 1,
            effectValue = 2f
        });

        // Calculator - Chance for double
        CreateSnack("Calculator", new SnackData
        {
            id = "calculator",
            displayName = "Calculator",
            description = "5% chance any Make10 counts as double",
            cost = 175,
            snackType = SnackType.Calculator,
            isUnique = true,
            minStageRequired = 2,
            effectChance = 0.05f
        });

        // Textbook - Overall BP multiplier
        CreateSnack("Textbook", new SnackData
        {
            id = "textbook",
            displayName = "Textbook",
            description = "+10% BP from all sources",
            cost = 200,
            snackType = SnackType.Textbook,
            isUnique = false,
            minStageRequired = 2,
            effectValue = 0.10f
        });

        // Metronome - Slower drain
        CreateSnack("Metronome", new SnackData
        {
            id = "metronome",
            displayName = "Metronome",
            description = "Multiplier drains 15% slower",
            cost = 100,
            snackType = SnackType.Metronome,
            isUnique = true,
            minStageRequired = 1,
            effectValue = 0.15f
        });

        // Red Bull - Higher Hot Streak multiplier
        CreateSnack("RedBull", new SnackData
        {
            id = "red_bull",
            displayName = "Red Bull",
            description = "Hot Streak multiplier x6 instead of x5",
            cost = 150,
            snackType = SnackType.RedBull,
            isUnique = true,
            minStageRequired = 2,
            effectValue = 1f // +1 to hot streak multiplier
        });

        // Tutor - Reduced failure penalty
        CreateSnack("Tutor", new SnackData
        {
            id = "tutor",
            displayName = "Tutor",
            description = "Threshold failure penalty reduced to 25%",
            cost = 125,
            snackType = SnackType.Tutor,
            isUnique = true,
            minStageRequired = 1
        });
    }

    #endregion

    #region Artifacts

    private static void CreateArtifacts()
    {
        // Golden Pencil - Double enhanced bonuses
        CreateArtifact("GoldenPencil", new ArtifactData
        {
            id = "golden_pencil",
            displayName = "Golden Pencil",
            description = "All Enhanced Number bonuses are doubled!",
            artifactType = ArtifactType.GoldenPencil
        });

        // Teacher's Pet - Shop discount
        CreateArtifact("TeachersPet", new ArtifactData
        {
            id = "teachers_pet",
            displayName = "Teacher's Pet",
            description = "Shop prices -20%",
            artifactType = ArtifactType.TeachersPet,
            effectValue = 0.8f // Price multiplier
        });

        // Overachiever - Threshold bonus
        CreateArtifact("Overachiever", new ArtifactData
        {
            id = "overachiever",
            displayName = "Overachiever",
            description = "Threshold completion awards +25% BP",
            artifactType = ArtifactType.Overachiever,
            effectValue = 0.25f
        });

        // Night Owl - More time, lower cap
        CreateArtifact("NightOwl", new ArtifactData
        {
            id = "night_owl",
            displayName = "Night Owl",
            description = "+15 seconds base time, but multiplier caps at x2.5",
            artifactType = ArtifactType.NightOwl,
            effectValue = 15f,
            hasDownside = true,
            downsideDescription = "Multiplier caps at x2.5",
            downsideValue = 2.5f
        });

        // Cramming - Earlier Hot Streak
        CreateArtifact("Cramming", new ArtifactData
        {
            id = "cramming",
            displayName = "Cramming",
            description = "Hot Streak triggers at x2.5 instead of x3.0",
            artifactType = ArtifactType.Cramming,
            effectValue = 2.5f // Hot streak threshold
        });
    }

    #endregion

    #region Asset Creation Helpers

    private static void CreateUpgrade(string name, UpgradeData template)
    {
        string path = UPGRADES_PATH + name + ".asset";

        if (AssetDatabase.LoadAssetAtPath<UpgradeData>(path) != null)
        {
            Debug.Log($"Upgrade already exists: {name}");
            return;
        }

        UpgradeData asset = ScriptableObject.CreateInstance<UpgradeData>();

        // Copy all fields from template
        asset.id = template.id;
        asset.displayName = template.displayName;
        asset.description = template.description;
        asset.baseCost = template.baseCost;
        asset.upgradeType = template.upgradeType;
        asset.isStackable = template.isStackable;
        asset.maxStacks = template.maxStacks;
        asset.minStageRequired = template.minStageRequired;
        asset.targetNumber = template.targetNumber;
        asset.bonusBPPerInstance = template.bonusBPPerInstance;
        asset.bonusSecondsPerInstance = template.bonusSecondsPerInstance;
        asset.startingMultiplierBonus = template.startingMultiplierBonus;
        asset.multiplierIncrementBonus = template.multiplierIncrementBonus;
        asset.drainRateReduction = template.drainRateReduction;
        asset.bonusStartingSeconds = template.bonusStartingSeconds;
        asset.overallBPMultiplier = template.overallBPMultiplier > 0 ? template.overallBPMultiplier : 1f;
        asset.bossDamageMultiplier = template.bossDamageMultiplier > 0 ? template.bossDamageMultiplier : 1f;

        AssetDatabase.CreateAsset(asset, path);
        Debug.Log($"Created upgrade: {name}");
    }

    private static void CreateSnack(string name, SnackData template)
    {
        string path = SNACKS_PATH + name + ".asset";

        if (AssetDatabase.LoadAssetAtPath<SnackData>(path) != null)
        {
            Debug.Log($"Snack already exists: {name}");
            return;
        }

        SnackData asset = ScriptableObject.CreateInstance<SnackData>();

        asset.id = template.id;
        asset.displayName = template.displayName;
        asset.description = template.description;
        asset.cost = template.cost;
        asset.snackType = template.snackType;
        asset.isUnique = template.isUnique;
        asset.minStageRequired = template.minStageRequired;
        asset.effectValue = template.effectValue;
        asset.secondaryValue = template.secondaryValue;
        asset.effectChance = template.effectChance > 0 ? template.effectChance : 1f;
        asset.maxTriggersPerRound = template.maxTriggersPerRound;
        asset.oncePerRound = template.oncePerRound;

        AssetDatabase.CreateAsset(asset, path);
        Debug.Log($"Created snack: {name}");
    }

    private static void CreateArtifact(string name, ArtifactData template)
    {
        string path = ARTIFACTS_PATH + name + ".asset";

        if (AssetDatabase.LoadAssetAtPath<ArtifactData>(path) != null)
        {
            Debug.Log($"Artifact already exists: {name}");
            return;
        }

        ArtifactData asset = ScriptableObject.CreateInstance<ArtifactData>();

        asset.id = template.id;
        asset.displayName = template.displayName;
        asset.description = template.description;
        asset.artifactType = template.artifactType;
        asset.effectValue = template.effectValue;
        asset.hasDownside = template.hasDownside;
        asset.downsideDescription = template.downsideDescription;
        asset.downsideValue = template.downsideValue;

        AssetDatabase.CreateAsset(asset, path);
        Debug.Log($"Created artifact: {name}");
    }

    #endregion

    [MenuItem("Make10/Debug/Populate DebugUpgradePanel Assets")]
    public static void PopulateDebugUpgradePanelAssets()
    {
        DebugUpgradePanel panel = Object.FindFirstObjectByType<DebugUpgradePanel>();
        if (panel == null)
        {
            Debug.LogError("DebugUpgradePanel not found in scene!");
            return;
        }

        // Use SerializedObject to properly modify the component
        SerializedObject serializedPanel = new SerializedObject(panel);

        // Populate upgrades
        SerializedProperty upgradesProp = serializedPanel.FindProperty("availableUpgrades");
        upgradesProp.ClearArray();
        string[] upgradeGuids = AssetDatabase.FindAssets("t:UpgradeData", new[] { "Assets/Data/Upgrades" });
        foreach (string guid in upgradeGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            UpgradeData asset = AssetDatabase.LoadAssetAtPath<UpgradeData>(path);
            if (asset != null)
            {
                upgradesProp.arraySize++;
                upgradesProp.GetArrayElementAtIndex(upgradesProp.arraySize - 1).objectReferenceValue = asset;
            }
        }

        // Populate snacks
        SerializedProperty snacksProp = serializedPanel.FindProperty("availableSnacks");
        snacksProp.ClearArray();
        string[] snackGuids = AssetDatabase.FindAssets("t:SnackData", new[] { "Assets/Data/Snacks" });
        foreach (string guid in snackGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SnackData asset = AssetDatabase.LoadAssetAtPath<SnackData>(path);
            if (asset != null)
            {
                snacksProp.arraySize++;
                snacksProp.GetArrayElementAtIndex(snacksProp.arraySize - 1).objectReferenceValue = asset;
            }
        }

        // Populate artifacts
        SerializedProperty artifactsProp = serializedPanel.FindProperty("availableArtifacts");
        artifactsProp.ClearArray();
        string[] artifactGuids = AssetDatabase.FindAssets("t:ArtifactData", new[] { "Assets/Data/Artifacts" });
        foreach (string guid in artifactGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ArtifactData asset = AssetDatabase.LoadAssetAtPath<ArtifactData>(path);
            if (asset != null)
            {
                artifactsProp.arraySize++;
                artifactsProp.GetArrayElementAtIndex(artifactsProp.arraySize - 1).objectReferenceValue = asset;
            }
        }

        serializedPanel.ApplyModifiedProperties();
        EditorUtility.SetDirty(panel);

        Debug.Log($"[UpgradeAssetCreator] Populated DebugUpgradePanel with {upgradeGuids.Length} upgrades, {snackGuids.Length} snacks, {artifactGuids.Length} artifacts");
    }

    [MenuItem("Make10/Debug/Add Test Upgrades to Inventory")]
    public static void AddTestUpgradesToInventory()
    {
        Debug.Log("[UpgradeAssetCreator] Inventory testing disabled in arcade mode — no upgrades/snacks to add.");
    }
}
