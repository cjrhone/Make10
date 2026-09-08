using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles grid validation (initial match prevention, anti-cascade checks)
/// and consecutive match tracking (scale escalation for "10" popups).
/// Extracted from GridManager to isolate grid hygiene + combo state.
/// </summary>
public class GridValidation : MonoBehaviour {
  public static GridValidation Instance { get; private set; }

  [Header("Consecutive 10s Scaling"), SerializeField]
  private float baseTenScale = 1f;

  [SerializeField] private float tenScaleIncrement = 0.15f; // Scale increase per consecutive 10
  [SerializeField] private float maxTenScale = 2f;
  [SerializeField] private float consecutiveResetTime = 2f; // Reset if no 10 in this time
  private int consecutive10Count = 0;
  private float lastTenTime = 0f;

  private void Awake() {
    if (Instance != null && Instance != this) {
      Destroy(gameObject);
      return;
    }

    Instance = this;
  }

  // ──────────────────────────────────────────────
  //  Consecutive Match Tracking
  // ──────────────────────────────────────────────

  /// <summary>
  /// Call at the start of each solve sequence. Resets the streak if enough
  /// time has passed, then increments the counter.
  /// Returns the current consecutive count (1-based) for use in SFX/VFX.
  /// </summary>
  public int RegisterMatch() {
    if (Time.time - lastTenTime > consecutiveResetTime) {
      consecutive10Count = 0;
    }

    consecutive10Count++;
    lastTenTime = Time.time;
    return consecutive10Count;
  }

  /// <summary>
  /// Calculate the popup scale for a "10" effect based on consecutive count and line sum.
  /// </summary>
  public float GetTenScale (int lineSum) {
    var tenScale = Mathf.Min(baseTenScale + (consecutive10Count - 1) * tenScaleIncrement, maxTenScale);
    var sumBoost = (lineSum / 10f - 1f) * 0.15f; // +15% per multiple above 10
    tenScale += sumBoost;
    tenScale = Mathf.Min(tenScale, maxTenScale * 1.5f);
    return tenScale;
  }

  /// <summary>
  /// Current consecutive match count (for SFX pitch, shake intensity, etc.).
  /// </summary>
  public int ConsecutiveCount => consecutive10Count;

  /// <summary>
  /// Reset consecutive tracking (called on grid reset / new round).
  /// </summary>
  public void ResetConsecutive() {
    consecutive10Count = 0;
    lastTenTime = 0f;
  }

  // ──────────────────────────────────────────────
  //  Initial Match Prevention
  // ──────────────────────────────────────────────

  /// <summary>
  /// Ensures no row or column sums to 10 when the grid first spawns.
  /// The first match must come from the player's own swap.
  /// Re-rolls individual tiles to break any pre-existing matches.
  /// </summary>
  public void EnsureNoInitialMatches (Tile[,] grid, int gridWidth, int gridHeight,
    TileWeightManager tileWeightManager) {
    var maxIterations = 50;
    var iteration = 0;

    while (iteration < maxIterations) {
      var matchingLines = FindMatchingLines(grid, gridWidth, gridHeight);

      if (matchingLines.Count == 0) {
        if (iteration > 0) {
          Debug.Log($"[GridValidation] Cleared initial matches in {iteration} iteration(s)");
        }

        return;
      }

      // Pick a random matching line and re-roll one tile to break it
      var line = matchingLines[Random.Range(0, matchingLines.Count)];
      ReRollTileToBreakMatch(grid, gridWidth, gridHeight, line.index, line.isRow, tileWeightManager);
      iteration++;
    }

    Debug.LogWarning(
      $"[GridValidation] EnsureNoInitialMatches hit max iterations ({maxIterations}). Some matches may remain.");
  }

  /// <summary>
  /// Checks if placing a tile with the given value at (x, y) would complete a row or column match.
  /// Used for anti-cascade spawning — approximation is fine since some tiles may not have spawned yet.
  /// </summary>
  public bool WouldTileCompleteMatch (Tile[,] grid, int gridWidth, int gridHeight, int x, int y, int value) {
    // Check row sum
    var rowSum = 0;
    var rowComplete = true;
    for (var cx = 0; cx < gridWidth; cx++) {
      if (cx == x) {
        rowSum += value;
      }
      else if (grid[cx, y] != null) {
        rowSum += grid[cx, y].Value;
      }
      else {
        rowComplete = false;
      }
    }

    if (rowComplete && rowSum > 0 && rowSum % 10 == 0) {
      return true;
    }

    // Check column sum
    var colSum = 0;
    var colComplete = true;
    for (var cy = 0; cy < gridHeight; cy++) {
      if (cy == y) {
        colSum += value;
      }
      else if (grid[x, cy] != null) {
        colSum += grid[x, cy].Value;
      }
      else {
        colComplete = false;
      }
    }

    if (colComplete && colSum > 0 && colSum % 10 == 0) {
      return true;
    }

    return false;
  }

  // ──────────────────────────────────────────────
  //  Private Helpers
  // ──────────────────────────────────────────────

  /// <summary>
  /// Scans all rows and columns, returns any where the sum is a valid match (multiple of 10).
  /// </summary>
  private List<(int index, bool isRow)> FindMatchingLines (Tile[,] grid, int gridWidth, int gridHeight) {
    List<(int index, bool isRow)> matches = new();

    // Check rows
    for (var y = 0; y < gridHeight; y++) {
      var sum = 0;
      var valid = true;
      for (var x = 0; x < gridWidth; x++) {
        if (grid[x, y] == null) {
          valid = false;
          break;
        }

        sum += grid[x, y].Value;
      }

      if (valid && sum > 0 && sum % 10 == 0) {
        matches.Add((y, true));
      }
    }

    // Check columns
    for (var x = 0; x < gridWidth; x++) {
      var sum = 0;
      var valid = true;
      for (var y = 0; y < gridHeight; y++) {
        if (grid[x, y] == null) {
          valid = false;
          break;
        }

        sum += grid[x, y].Value;
      }

      if (valid && sum > 0 && sum % 10 == 0) {
        matches.Add((x, false));
      }
    }

    return matches;
  }

  /// <summary>
  /// Picks a random tile in the given matching line and re-rolls its value
  /// until the line no longer sums to a multiple of 10, also avoiding creating
  /// a new match in the perpendicular direction through that tile.
  /// </summary>
  private void ReRollTileToBreakMatch (Tile[,] grid, int gridWidth, int gridHeight, int lineIndex, bool isRow,
    TileWeightManager tileWeightManager) {
    // Pick a random NON-LOCKED position in the line to re-roll
    var lineLength = isRow ? gridWidth : gridHeight;

    // Collect eligible (non-locked) positions
    var eligiblePositions = new List<int>();
    for (var i = 0; i < lineLength; i++) {
      var tx = isRow ? i : lineIndex;
      var ty = isRow ? lineIndex : i;
      if (grid[tx, ty] != null && !grid[tx, ty].IsLocked) {
        eligiblePositions.Add(i);
      }
    }

    if (eligiblePositions.Count == 0) {
      return; // All tiles locked, can't re-roll
    }

    var pos = eligiblePositions[Random.Range(0, eligiblePositions.Count)];

    var tileX = isRow ? pos : lineIndex;
    var tileY = isRow ? lineIndex : pos;
    var tile = grid[tileX, tileY];

    if (tile == null) {
      return;
    }

    var originalValue = tile.Value;
    var bestValue = originalValue;
    var bestBadness = 2; // Start with worst case (both lines match)

    // Try up to 15 random values to find one that breaks the match
    // without creating a new match in the perpendicular direction
    for (var attempt = 0; attempt < 15; attempt++) {
      var newValue = tileWeightManager.GetWeightedRandomValue();
      if (newValue == originalValue) {
        continue;
      }

      tile.SetValue(newValue);

      // Check if the original line still sums to 10
      var originalLineMatches = CheckLineSum(grid, gridWidth, gridHeight, lineIndex, isRow);

      // Check if the perpendicular line through this tile now sums to 10
      var perpLineMatches = isRow ?
        CheckLineSum(grid, gridWidth, gridHeight, tileX, false) // Check column through this tile
        :
        CheckLineSum(grid, gridWidth, gridHeight, tileY, true); // Check row through this tile

      var badness = (originalLineMatches ? 1 : 0) + (perpLineMatches ? 1 : 0);

      if (badness == 0)
        // Perfect: neither line matches
      {
        return;
      }

      if (badness < bestBadness) {
        bestBadness = badness;
        bestValue = newValue;
      }
    }

    // Use the best value we found (even if not perfect, it reduces matches)
    tile.SetValue(bestValue);
  }

  /// <summary>
  /// Check if a single row or column sums to a valid match (multiple of 10).
  /// </summary>
  private bool CheckLineSum (Tile[,] grid, int gridWidth, int gridHeight, int lineIndex, bool isRow) {
    var sum = 0;
    var length = isRow ? gridWidth : gridHeight;

    for (var i = 0; i < length; i++) {
      var x = isRow ? i : lineIndex;
      var y = isRow ? lineIndex : i;
      if (grid[x, y] == null) {
        return false;
      }

      sum += grid[x, y].Value;
    }

    return sum > 0 && sum % 10 == 0;
  }
}