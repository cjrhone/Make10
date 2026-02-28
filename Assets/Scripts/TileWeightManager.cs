using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages tile value weights, progressive difficulty ramp, and Tetris-style tile bag system.
/// Extracted from GridManager to isolate tile distribution logic.
/// </summary>
public class TileWeightManager : MonoBehaviour
{
    public static TileWeightManager Instance { get; private set; }

    [Header("Tile Value Weights (fallback if no GameManager)")]
    [SerializeField] private float weight0 = 0.12f;    // Grey (wildcard) — boosted for easy early 10s
    [SerializeField] private float weight1 = 0.28f;    // Gold — boosted primary, easiest combos
    [SerializeField] private float weight2 = 0.26f;    // Blue — dominant
    [SerializeField] private float weight3 = 0.22f;    // Green — strong mid-range
    [SerializeField] private float weight4 = 0.08f;    // Coral — further reduced
    [SerializeField] private float weight5 = 0f;       // Orange — introduced by solve ramp
    [SerializeField] private float weight6 = 0f;       // Purple — introduced by solve ramp
    [SerializeField] private float weight7 = 0f;       // Teal — introduced by solve ramp

    [Header("Progressive Difficulty - Solve-Based Ramp")]
    [SerializeField] private int solvesFor5s = 2;               // 5s start appearing after this many solves
    [SerializeField] private int solvesFor6s = 5;               // 6s start appearing after this many solves
    [SerializeField] private int solvesFor7s = 8;               // 7s start appearing after this many solves
    [SerializeField] private float maxWeight5 = 0.10f;          // Max weight for 5s at full ramp
    [SerializeField] private float maxWeight6 = 0.06f;          // Max weight for 6s at full ramp
    [SerializeField] private float maxWeight7 = 0.02f;          // Max weight for 7s at full ramp
    [SerializeField] private int solvesToFullRamp = 12;          // Solves needed for all high tiles at max weight
    [SerializeField] private float baseTileReduction = 0.85f;   // Low tiles reduce as high tiles ramp in

    // Tile bag system (Tetris-style consistent distribution)
    private List<int> tileBag = new List<int>();
    private const int BAG_SIZE = 25;  // Refill after 25 draws

    // Cached weight array
    private float[] weights;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        weights = new float[] { weight0, weight1, weight2, weight3, weight4, weight5, weight6, weight7, 0f, 0f };
    }

    /// <summary>
    /// Draw the next tile value from the bag. Refills bag when empty.
    /// </summary>
    public int GetWeightedRandomValue()
    {
        if (tileBag.Count == 0)
            RefillTileBag();

        int value = tileBag[tileBag.Count - 1];
        tileBag.RemoveAt(tileBag.Count - 1);
        return value;
    }

    /// <summary>
    /// Clear the tile bag (used on grid reset / new round).
    /// </summary>
    public void ClearBag()
    {
        tileBag.Clear();
    }

    /// <summary>
    /// Calculate the current adjusted weights based on solve count and progressive ramp.
    /// Used by both the tile bag system and any other weight-dependent logic.
    /// </summary>
    private float[] GetAdjustedWeights()
    {
        int solves = GameManager.Instance != null ? GameManager.Instance.SolveCount : 0;

        // Get base weights from GameManager or fallback (0-4 have weight, 5-7 start at 0)
        float[] currentWeights = GetCurrentWeights();

        // Build adjusted weight array (always 10 elements for tiles 0-9)
        float[] adjustedWeights = new float[10];
        for (int i = 0; i < adjustedWeights.Length && i < currentWeights.Length; i++)
        {
            adjustedWeights[i] = currentWeights[i];
        }

        // Solve-based ramp: high tiles (5, 6, 7) gradually introduced as player clears matches
        float rampProgress = Mathf.Clamp01((float)solves / solvesToFullRamp);

        // 5s: appear after solvesFor5s, ramp to maxWeight5
        if (solves >= solvesFor5s)
        {
            float t5 = Mathf.Clamp01((float)(solves - solvesFor5s) / (solvesToFullRamp - solvesFor5s));
            adjustedWeights[5] = Mathf.Lerp(0.02f, maxWeight5, t5);
        }

        // 6s: appear after solvesFor6s, ramp to maxWeight6
        if (solves >= solvesFor6s)
        {
            float t6 = Mathf.Clamp01((float)(solves - solvesFor6s) / (solvesToFullRamp - solvesFor6s));
            adjustedWeights[6] = Mathf.Lerp(0.01f, maxWeight6, t6);
        }

        // 7s: appear after solvesFor7s, ramp to maxWeight7
        if (solves >= solvesFor7s)
        {
            float t7 = Mathf.Clamp01((float)(solves - solvesFor7s) / (solvesToFullRamp - solvesFor7s));
            adjustedWeights[7] = Mathf.Lerp(0.005f, maxWeight7, t7);
        }

        // Gently reduce base tiles (0-4) as high tiles ramp in, keeping board playable
        float reduction = Mathf.Lerp(1.0f, baseTileReduction, rampProgress);
        for (int i = 0; i <= 4; i++)
        {
            adjustedWeights[i] *= reduction;
        }

        return adjustedWeights;
    }

    /// <summary>
    /// Refill the tile bag with BAG_SIZE tiles distributed according to current weights.
    /// Tetris-style: guarantees consistent distribution over every 25 draws.
    /// </summary>
    private void RefillTileBag()
    {
        tileBag.Clear();

        float[] adjustedWeights = GetAdjustedWeights();

        float totalWeight = 0f;
        for (int i = 0; i < adjustedWeights.Length; i++)
            totalWeight += adjustedWeights[i];

        if (totalWeight <= 0f)
        {
            // Fallback: fill bag with easy tiles only
            for (int i = 0; i < BAG_SIZE; i++)
                tileBag.Add(Random.Range(0, 5));
        }
        else
        {
            int placed = 0;
            int highestWeightTile = 0;
            float highestWeight = 0f;

            for (int i = 0; i < adjustedWeights.Length; i++)
            {
                int count = Mathf.RoundToInt(adjustedWeights[i] / totalWeight * BAG_SIZE);
                for (int j = 0; j < count && placed < BAG_SIZE; j++)
                {
                    tileBag.Add(i);
                    placed++;
                }
                if (adjustedWeights[i] > highestWeight)
                {
                    highestWeight = adjustedWeights[i];
                    highestWeightTile = i;
                }
            }

            // Pad remaining slots with the highest-weight tile
            while (placed < BAG_SIZE)
            {
                tileBag.Add(highestWeightTile);
                placed++;
            }
        }

        // Fisher-Yates shuffle
        for (int i = tileBag.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = tileBag[i];
            tileBag[i] = tileBag[j];
            tileBag[j] = temp;
        }
    }

    /// <summary>
    /// Get tile spawn weights from GameManager (difficulty-based) or use fallback.
    /// </summary>
    private float[] GetCurrentWeights()
    {
        if (GameManager.Instance != null)
        {
            return GameManager.Instance.GetCurrentWeights();
        }

        // Fallback to serialized weights (for testing without GameManager)
        return weights;
    }
}
