using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages campaign progression through stages, rounds, and boss fights.
/// Tracks thresholds, handles difficulty scaling, and manages the overall campaign flow.
/// </summary>
public class CampaignManager : MonoBehaviour
{
    public static CampaignManager Instance { get; private set; }

    #region Stage Definitions

    [System.Serializable]
    public class StageData
    {
        public string stageName;
        public int gridSize = 5;
        public int maxNumber = 6; // 0 to maxNumber (inclusive)
        public int[] roundThresholds; // BP required to pass each round
        public int bossHP = 1000;
        public int bossBPReward = 500;
        public int goldStarReward = 1;
    }

    [Header("Stage Configuration")]
    [SerializeField] private List<StageData> stages = new List<StageData>();

    #endregion

    #region State

    [Header("Current State")]
    [SerializeField] private int currentStageIndex = 0;
    [SerializeField] private int currentRoundIndex = 0;
    [SerializeField] private bool isInBossFight = false;
    [SerializeField] private bool isInChillZone = false;
    [SerializeField] private int currentBossHP = 0;
    [SerializeField] private int maxBossHP = 0;

    // Properties
    public int CurrentStage => currentStageIndex + 1; // 1-indexed for display
    public int CurrentRound => currentRoundIndex + 1; // 1-indexed for display
    public bool IsInBossFight => isInBossFight;
    public bool IsInChillZone => isInChillZone;
    public int CurrentBossHP => currentBossHP;
    public int MaxBossHP => maxBossHP;
    public float BossHPPercent => maxBossHP > 0 ? (float)currentBossHP / maxBossHP : 0f;

    // Events
    public event System.Action<int, int> OnStageChanged; // stage, round
    public event System.Action<int> OnRoundChanged; // round
    public event System.Action OnBossFightStarted;
    public event System.Action<int, int> OnBossDamaged; // damage, remainingHP
    public event System.Action<int, int> OnBossDefeated; // bpReward, goldStars
    public event System.Action OnChillZoneEntered;
    public event System.Action OnCampaignCompleted;

    #endregion

    #region Initialization

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        InitializeDefaultStages();
    }

    /// <summary>
    /// Initialize default stage data if not configured in inspector.
    /// </summary>
    private void InitializeDefaultStages()
    {
        if (stages.Count > 0) return;

        // Stage 1: Tutorial-friendly
        stages.Add(new StageData
        {
            stageName = "The Basics",
            gridSize = 4,
            maxNumber = 5,
            roundThresholds = new int[] { 100, 250, 500 },
            bossHP = 1000,
            bossBPReward = 500,
            goldStarReward = 1
        });

        // Stage 2: Getting harder
        stages.Add(new StageData
        {
            stageName = "Stepping Up",
            gridSize = 5,
            maxNumber = 6,
            roundThresholds = new int[] { 300, 600, 900, 1200 },
            bossHP = 2000,
            bossBPReward = 1000,
            goldStarReward = 2
        });

        // Stage 3: Challenging
        stages.Add(new StageData
        {
            stageName = "The Grind",
            gridSize = 5,
            maxNumber = 7,
            roundThresholds = new int[] { 750, 1000, 1250, 1500, 1750 },
            bossHP = 3000,
            bossBPReward = 1500,
            goldStarReward = 3
        });

        // Stage 4: Galactic Boss (endless)
        stages.Add(new StageData
        {
            stageName = "Final Exam",
            gridSize = 5,
            maxNumber = 7,
            roundThresholds = new int[] { }, // Endless - no thresholds
            bossHP = 10000,
            bossBPReward = 0, // True ending instead
            goldStarReward = 5
        });

        Debug.Log("[CampaignManager] Initialized default stages");
    }

    #endregion

    #region Campaign Flow

    /// <summary>
    /// Start a new campaign from stage 1.
    /// </summary>
    public void StartNewCampaign()
    {
        currentStageIndex = 0;
        currentRoundIndex = 0;
        isInBossFight = false;
        isInChillZone = false;
        currentBossHP = 0;
        maxBossHP = 0;

        // Clear player inventory
        PlayerInventory.Instance?.ClearInventory();
        PlayerInventory.Instance?.ApplyDebugStartingBP();

        Debug.Log("[CampaignManager] New campaign started");
        OnStageChanged?.Invoke(CurrentStage, CurrentRound);
    }

    /// <summary>
    /// Get the current round's BP threshold.
    /// </summary>
    public int GetCurrentThreshold()
    {
        if (currentStageIndex >= stages.Count)
        {
            Debug.LogWarning($"[CampaignManager] GetCurrentThreshold: stageIndex {currentStageIndex} >= stages.Count {stages.Count}");
            return 0;
        }

        StageData stage = stages[currentStageIndex];

        if (isInBossFight)
        {
            return stage.bossHP; // Boss HP is the "threshold"
        }

        if (currentRoundIndex >= stage.roundThresholds.Length)
        {
            Debug.LogWarning($"[CampaignManager] GetCurrentThreshold: roundIndex {currentRoundIndex} >= thresholds.Length {stage.roundThresholds.Length} - should be in chill zone!");
            return 0; // Should be in chill zone or boss
        }

        int threshold = stage.roundThresholds[currentRoundIndex];
        Debug.Log($"[CampaignManager] GetCurrentThreshold: Stage {CurrentStage}, Round {CurrentRound}, Threshold = {threshold}");
        return threshold;
    }

    /// <summary>
    /// Get current grid size for the stage.
    /// </summary>
    public int GetCurrentGridSize()
    {
        if (currentStageIndex >= stages.Count) return 5;
        return stages[currentStageIndex].gridSize;
    }

    /// <summary>
    /// Get max number for tile values (0 to this value).
    /// </summary>
    public int GetCurrentMaxNumber()
    {
        if (currentStageIndex >= stages.Count) return 6;
        return stages[currentStageIndex].maxNumber;
    }

    /// <summary>
    /// Get the current stage data.
    /// </summary>
    public StageData GetCurrentStageData()
    {
        if (currentStageIndex >= stages.Count) return null;
        return stages[currentStageIndex];
    }

    /// <summary>
    /// Called when a round is completed (met threshold).
    /// Note: Game over on failure is handled by GameManager - no BP penalty.
    /// </summary>
    public void OnRoundCompleted(int bpEarned)
    {
        // Add BP to run total (no penalty - run ends on failure instead)
        RunManager.Instance?.AddBP(bpEarned);

        // Note: Don't advance round here - shop will call AdvanceRound() when ready
        Debug.Log($"[CampaignManager] Round completed with {bpEarned} BP, waiting for shop to advance");
    }

    /// <summary>
    /// Public method to advance to next round (called by ShopManager).
    /// </summary>
    public void AdvanceRound()
    {
        Debug.Log($"[CampaignManager] AdvanceRound called. Current: Stage {CurrentStage}, Round {CurrentRound}, RoundIndex={currentRoundIndex}");
        AdvanceRoundInternal();
    }

    /// <summary>
    /// Advance to the next round, chill zone, or boss.
    /// </summary>
    private void AdvanceRoundInternal()
    {
        StageData stage = stages[currentStageIndex];

        Debug.Log($"[CampaignManager] Before increment: roundIndex={currentRoundIndex}, thresholds.Length={stage.roundThresholds.Length}");
        currentRoundIndex++;
        Debug.Log($"[CampaignManager] After increment: roundIndex={currentRoundIndex}");

        // Check if all rounds completed
        if (currentRoundIndex >= stage.roundThresholds.Length)
        {
            Debug.Log($"[CampaignManager] All rounds complete - entering chill zone");
            // Enter chill zone before boss
            EnterChillZone();
            return;
        }

        // Reset round tracking
        PlayerInventory.Instance?.ResetRoundTracking();

        int newThreshold = stage.roundThresholds[currentRoundIndex];
        Debug.Log($"[CampaignManager] Advanced to Stage {CurrentStage}, Round {CurrentRound}, NEW THRESHOLD = {newThreshold}");
        OnRoundChanged?.Invoke(CurrentRound);
    }

    /// <summary>
    /// Check if all rounds in the current stage are complete (ready for boss).
    /// </summary>
    public bool AreAllRoundsComplete()
    {
        if (currentStageIndex >= stages.Count) return false;

        StageData stage = stages[currentStageIndex];
        // If round index equals threshold count, we've done all rounds
        return currentRoundIndex >= stage.roundThresholds.Length;
    }

    /// <summary>
    /// Enter the chill zone before a boss fight.
    /// </summary>
    private void EnterChillZone()
    {
        isInChillZone = true;
        Debug.Log($"[CampaignManager] Entered Chill Zone for Stage {CurrentStage}");
        OnChillZoneEntered?.Invoke();
    }

    /// <summary>
    /// Exit chill zone and start boss fight.
    /// </summary>
    public void StartBossFight()
    {
        if (!isInChillZone)
        {
            Debug.LogWarning("[CampaignManager] StartBossFight called but not in chill zone");
            return;
        }

        isInChillZone = false;
        isInBossFight = true;

        StageData stage = stages[currentStageIndex];
        maxBossHP = stage.bossHP;
        currentBossHP = maxBossHP;

        // Reset round tracking
        PlayerInventory.Instance?.ResetRoundTracking();

        Debug.Log($"[CampaignManager] Boss fight started! HP: {currentBossHP}");
        OnBossFightStarted?.Invoke();
    }

    /// <summary>
    /// Deal damage to the current boss.
    /// </summary>
    public void DamageBoss(int damage)
    {
        if (!isInBossFight) return;

        // Apply boss damage multiplier from upgrades
        float damageMultiplier = 1f;
        if (PlayerInventory.Instance != null)
        {
            foreach (var kvp in PlayerInventory.Instance.GetAllUpgrades())
            {
                damageMultiplier += (kvp.Key.bossDamageMultiplier - 1f) * kvp.Value;
            }
        }

        int finalDamage = Mathf.RoundToInt(damage * damageMultiplier);
        currentBossHP = Mathf.Max(0, currentBossHP - finalDamage);

        Debug.Log($"[CampaignManager] Boss damaged for {finalDamage} (base: {damage}). HP: {currentBossHP}/{maxBossHP}");
        OnBossDamaged?.Invoke(finalDamage, currentBossHP);

        if (currentBossHP <= 0)
        {
            OnBossDefeatedInternal();
        }
    }

    /// <summary>
    /// Handle boss defeat.
    /// </summary>
    private void OnBossDefeatedInternal()
    {
        isInBossFight = false;

        StageData stage = stages[currentStageIndex];

        // Award BP and gold stars
        int bpReward = stage.bossBPReward;
        int goldStars = stage.goldStarReward;

        RunManager.Instance?.AddBP(bpReward);
        RunManager.Instance?.AddGoldStars(goldStars);

        Debug.Log($"[CampaignManager] Boss defeated! Rewards: {bpReward} BP, {goldStars} Gold Stars");
        OnBossDefeated?.Invoke(bpReward, goldStars);

        // Check if campaign complete
        if (currentStageIndex >= stages.Count - 1)
        {
            Debug.Log("[CampaignManager] Campaign completed!");
            OnCampaignCompleted?.Invoke();
        }
        else
        {
            // Advance to next stage
            AdvanceStage();
        }
    }

    /// <summary>
    /// Advance to the next stage.
    /// </summary>
    private void AdvanceStage()
    {
        currentStageIndex++;
        currentRoundIndex = 0;
        isInBossFight = false;
        isInChillZone = false;

        Debug.Log($"[CampaignManager] Advanced to Stage {CurrentStage}");
        OnStageChanged?.Invoke(CurrentStage, CurrentRound);
    }

    #endregion

    #region Stage 4 Endless Mode

    /// <summary>
    /// Check if we're in Stage 4 endless mode.
    /// </summary>
    public bool IsEndlessMode()
    {
        return currentStageIndex == 3 && !isInChillZone;
    }

    /// <summary>
    /// Get time bonus per Make10 in endless mode.
    /// </summary>
    public float GetEndlessModeTimeBonusPerMatch()
    {
        return IsEndlessMode() ? 2f : 0f;
    }

    /// <summary>
    /// Get starting time for endless mode.
    /// </summary>
    public float GetEndlessModeStartingTime()
    {
        return 30f;
    }

    #endregion

    #region Queries

    /// <summary>
    /// Get total rounds in current stage (excluding boss).
    /// </summary>
    public int GetTotalRoundsInStage()
    {
        if (currentStageIndex >= stages.Count) return 0;
        return stages[currentStageIndex].roundThresholds.Length;
    }

    /// <summary>
    /// Get stage name for display.
    /// </summary>
    public string GetCurrentStageName()
    {
        if (currentStageIndex >= stages.Count) return "Unknown";
        return stages[currentStageIndex].stageName;
    }

    /// <summary>
    /// Get a formatted progress string.
    /// </summary>
    public string GetProgressString()
    {
        if (isInChillZone)
        {
            return $"Stage {CurrentStage} - Chill Zone";
        }
        else if (isInBossFight)
        {
            return $"Stage {CurrentStage} - BOSS";
        }
        else
        {
            return $"Stage {CurrentStage} - Round {CurrentRound}/{GetTotalRoundsInStage()}";
        }
    }

    #endregion

    #region Debug

    /// <summary>
    /// Debug: Skip to a specific stage.
    /// </summary>
    public void DebugSkipToStage(int stageNumber)
    {
        currentStageIndex = Mathf.Clamp(stageNumber - 1, 0, stages.Count - 1);
        currentRoundIndex = 0;
        isInBossFight = false;
        isInChillZone = false;

        Debug.Log($"[CampaignManager] DEBUG: Skipped to Stage {CurrentStage}");
        OnStageChanged?.Invoke(CurrentStage, CurrentRound);
    }

    /// <summary>
    /// Debug: Skip to boss fight.
    /// </summary>
    public void DebugSkipToBoss()
    {
        isInChillZone = true;
        StartBossFight();
    }

    /// <summary>
    /// Debug: Deal massive damage to boss.
    /// </summary>
    public void DebugKillBoss()
    {
        if (isInBossFight)
        {
            DamageBoss(currentBossHP);
        }
    }

    #endregion
}
