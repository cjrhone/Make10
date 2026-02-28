using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Evaluates the grid for valid matches (rows/columns that sum to a multiple of 10).
/// Pure logic - no MonoBehaviour needed, but using it for easy inspector access.
/// </summary>
public class MatchChecker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager gridManager;
    
    [Header("Settings")]
    
    [Header("Debug")]
    [SerializeField] private bool logMatches = true;
    
    private void Awake()
    {
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }
    }

    /// <summary>
    /// Check if a sum is a valid match (any positive multiple of 10).
    /// </summary>
    private bool IsValidMatch(int sum)
    {
        return sum > 0 && sum % 10 == 0;
    }
    
    /// <summary>
    /// Collect tiles along a line (row or column) and return them with their sum.
    /// When isRow=true, lineIndex is the y coordinate; when false, it's the x coordinate.
    /// </summary>
    private (List<Tile> tiles, int sum) GetLineTiles(Tile[,] grid, Vector2Int gridSize, int lineIndex, bool isRow)
    {
        List<Tile> tiles = new List<Tile>();
        int sum = 0;
        int count = isRow ? gridSize.x : gridSize.y;

        for (int i = 0; i < count; i++)
        {
            Tile tile = isRow ? grid[i, lineIndex] : grid[lineIndex, i];
            if (tile != null)
            {
                tiles.Add(tile);
                sum += tile.Value;
            }
        }

        return (tiles, sum);
    }

    /// <summary>
    /// Check all rows and columns for matches.
    /// Returns a HashSet of tiles that are part of any match.
    /// </summary>
    public HashSet<Tile> CheckForMatches()
    {
        HashSet<Tile> matchedTiles = new HashSet<Tile>();
        Tile[,] grid = gridManager.GetGrid();
        Vector2Int gridSize = gridManager.GetGridSize();

        // Check all rows
        for (int y = 0; y < gridSize.y; y++)
        {
            var (tiles, sum) = GetLineTiles(grid, gridSize, y, isRow: true);
            if (IsValidMatch(sum))
            {
                foreach (Tile tile in tiles)
                    matchedTiles.Add(tile);
                if (logMatches)
                    Debug.Log($"<color=green>ROW {y} MATCH!</color> Sum = {sum}");
            }
        }

        // Check all columns
        for (int x = 0; x < gridSize.x; x++)
        {
            var (tiles, sum) = GetLineTiles(grid, gridSize, x, isRow: false);
            if (IsValidMatch(sum))
            {
                foreach (Tile tile in tiles)
                    matchedTiles.Add(tile);
                if (logMatches)
                    Debug.Log($"<color=cyan>COLUMN {x} MATCH!</color> Sum = {sum}");
            }
        }

        return matchedTiles;
    }

    /// <summary>
    /// Check if there are any matches on the board.
    /// </summary>
    public bool HasMatches()
    {
        return CheckForMatches().Count > 0;
    }

    /// <summary>
    /// Get detailed match info - returns ALL matching rows and columns simultaneously.
    /// </summary>
    public MatchResult GetMatchResult()
    {
        MatchResult result = new MatchResult();
        Tile[,] grid = gridManager.GetGrid();
        Vector2Int gridSize = gridManager.GetGridSize();

        // Check all rows
        for (int y = 0; y < gridSize.y; y++)
        {
            var (tiles, sum) = GetLineTiles(grid, gridSize, y, isRow: true);
            if (IsValidMatch(sum))
            {
                result.matchedRows.Add(y);
                result.rowSums[y] = sum;
                foreach (Tile tile in tiles)
                    result.allMatchedTiles.Add(tile);
            }
        }

        // Check all columns
        for (int x = 0; x < gridSize.x; x++)
        {
            var (tiles, sum) = GetLineTiles(grid, gridSize, x, isRow: false);
            if (IsValidMatch(sum))
            {
                result.matchedColumns.Add(x);
                result.columnSums[x] = sum;
                foreach (Tile tile in tiles)
                    result.allMatchedTiles.Add(tile);
            }
        }

        return result;
    }
    
    /// <summary>
    /// Find a swap that would create a match.
    /// Returns the tile to move and the direction to swipe, or null if no hint found.
    /// In Zen mode, returns a two-tile hint (any pair of free tiles on the board).
    /// </summary>
    public HintMove FindHintMove()
    {
        bool isZen = GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.Zen;
        if (isZen) return FindHintMoveZen();
        return FindHintMoveArcade();
    }

    /// <summary>
    /// Arcade hint: find an adjacent swap that creates a match.
    /// </summary>
    private HintMove FindHintMoveArcade()
    {
        Tile[,] grid = gridManager.GetGrid();
        Vector2Int gridSize = gridManager.GetGridSize();

        // Try every possible adjacent swap (skip locked tiles — can't be swapped)
        for (int y = 0; y < gridSize.y; y++)
        {
            for (int x = 0; x < gridSize.x; x++)
            {
                Tile tile = grid[x, y];
                if (tile == null || tile.IsLocked) continue;

                // Try swapping right
                if (x + 1 < gridSize.x && grid[x + 1, y] != null && !grid[x + 1, y].IsLocked)
                {
                    if (WouldCreateMatch(x, y, x + 1, y))
                        return new HintMove(tile, SwipeDirection.Right);
                }

                // Try swapping down
                if (y + 1 < gridSize.y && grid[x, y + 1] != null && !grid[x, y + 1].IsLocked)
                {
                    if (WouldCreateMatch(x, y, x, y + 1))
                        return new HintMove(tile, SwipeDirection.Down);
                }

                // Try swapping left
                if (x - 1 >= 0 && grid[x - 1, y] != null && !grid[x - 1, y].IsLocked)
                {
                    if (WouldCreateMatch(x, y, x - 1, y))
                        return new HintMove(tile, SwipeDirection.Left);
                }

                // Try swapping up
                if (y - 1 >= 0 && grid[x, y - 1] != null && !grid[x, y - 1].IsLocked)
                {
                    if (WouldCreateMatch(x, y, x, y - 1))
                        return new HintMove(tile, SwipeDirection.Up);
                }
            }
        }

        return null; // No hint found
    }

    /// <summary>
    /// Zen hint: find ANY two free tiles whose swap creates a match.
    /// Brute-force all pairs — trivial on 5×5 (~300 pairs worst case).
    /// </summary>
    private HintMove FindHintMoveZen()
    {
        Tile[,] grid = gridManager.GetGrid();
        Vector2Int gridSize = gridManager.GetGridSize();
        List<Vector2Int> freeCells = GetFreeCells(grid, gridSize);

        for (int i = 0; i < freeCells.Count; i++)
        {
            for (int j = i + 1; j < freeCells.Count; j++)
            {
                if (WouldCreateMatch(freeCells[i].x, freeCells[i].y, freeCells[j].x, freeCells[j].y))
                {
                    Tile tileA = grid[freeCells[i].x, freeCells[i].y];
                    Tile tileB = grid[freeCells[j].x, freeCells[j].y];
                    return new HintMove(tileA, tileB);
                }
            }
        }

        return null;
    }
    
    /// <summary>
    /// Check if swapping tiles at (x1,y1) and (x2,y2) would create a match.
    /// After swap: position (x1,y1) has val2, position (x2,y2) has val1.
    /// </summary>
    private bool WouldCreateMatch(int x1, int y1, int x2, int y2)
    {
        Tile[,] grid = gridManager.GetGrid();
        Vector2Int gridSize = gridManager.GetGridSize();
        
        int val1 = grid[x1, y1].Value; // Original value at (x1,y1) - moves to (x2,y2)
        int val2 = grid[x2, y2].Value; // Original value at (x2,y2) - moves to (x1,y1)
        
        // Helper function to get value at any position after the hypothetical swap
        int GetValueAfterSwap(int x, int y)
        {
            if (x == x1 && y == y1) return val2; // (x1,y1) now has val2
            if (x == x2 && y == y2) return val1; // (x2,y2) now has val1
            return grid[x, y]?.Value ?? 0;
        }
        
        // Check row y1
        int sum = 0;
        for (int x = 0; x < gridSize.x; x++)
            sum += GetValueAfterSwap(x, y1);
        if (IsValidMatch(sum)) return true;
        
        // Check row y2 (only if different from y1)
        if (y1 != y2)
        {
            sum = 0;
            for (int x = 0; x < gridSize.x; x++)
                sum += GetValueAfterSwap(x, y2);
            if (IsValidMatch(sum)) return true;
        }
        
        // Check column x1
        sum = 0;
        for (int y = 0; y < gridSize.y; y++)
            sum += GetValueAfterSwap(x1, y);
        if (IsValidMatch(sum)) return true;
        
        // Check column x2 (only if different from x1)
        if (x1 != x2)
        {
            sum = 0;
            for (int y = 0; y < gridSize.y; y++)
                sum += GetValueAfterSwap(x2, y);
            if (IsValidMatch(sum)) return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Check if any valid swap exists on the board.
    /// Arcade: adjacent swaps only. Zen: any two free tiles.
    /// </summary>
    public bool HasValidMoves()
    {
        bool isZen = GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.Zen;
        if (isZen) return HasValidMovesZen();
        return HasValidMovesArcade();
    }

    /// <summary>
    /// Arcade: check adjacent swaps only (right + down to avoid duplicates).
    /// </summary>
    private bool HasValidMovesArcade()
    {
        Tile[,] grid = gridManager.GetGrid();
        Vector2Int gridSize = gridManager.GetGridSize();

        for (int y = 0; y < gridSize.y; y++)
        {
            for (int x = 0; x < gridSize.x; x++)
            {
                Tile tile = grid[x, y];
                if (tile == null || tile.IsLocked) continue;

                // Check right and down only (avoids double-checking symmetric swaps)
                if (x + 1 < gridSize.x && grid[x + 1, y] != null && !grid[x + 1, y].IsLocked)
                {
                    if (WouldCreateMatch(x, y, x + 1, y)) return true;
                }
                if (y + 1 < gridSize.y && grid[x, y + 1] != null && !grid[x, y + 1].IsLocked)
                {
                    if (WouldCreateMatch(x, y, x, y + 1)) return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Zen: check ALL pairs of free (non-locked) tiles.
    /// Brute-force is fine — worst case ~300 pairs × 4 line checks = ~1200 sums on 5×5.
    /// </summary>
    private bool HasValidMovesZen()
    {
        Tile[,] grid = gridManager.GetGrid();
        Vector2Int gridSize = gridManager.GetGridSize();
        List<Vector2Int> freeCells = GetFreeCells(grid, gridSize);

        for (int i = 0; i < freeCells.Count; i++)
        {
            for (int j = i + 1; j < freeCells.Count; j++)
            {
                if (WouldCreateMatch(freeCells[i].x, freeCells[i].y, freeCells[j].x, freeCells[j].y))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Collect all non-locked, non-null tile positions on the grid.
    /// </summary>
    private List<Vector2Int> GetFreeCells(Tile[,] grid, Vector2Int gridSize)
    {
        List<Vector2Int> free = new List<Vector2Int>();
        for (int y = 0; y < gridSize.y; y++)
            for (int x = 0; x < gridSize.x; x++)
                if (grid[x, y] != null && !grid[x, y].IsLocked)
                    free.Add(new Vector2Int(x, y));
        return free;
    }
    
    private bool CanSum(List<int> values, int count, int target, int startIndex)
    {
        if (count == 0 && target == 0) return true;
        if (count == 0 || target < 0 || startIndex >= values.Count) return false;
        if (values.Count - startIndex < count) return false;
        
        for (int i = startIndex; i < values.Count; i++)
            if (CanSum(values, count - 1, target - values[i], i + 1))
                return true;
        
        return false;
    }
    
    [ContextMenu("Print All Sums")]
    public void DebugPrintAllSums()
    {
        if (gridManager == null)
        {
            Debug.LogError("GridManager not assigned!");
            return;
        }
        
        Tile[,] grid = gridManager.GetGrid();
        Vector2Int gridSize = gridManager.GetGridSize();
        
        string output = "=== GRID SUMS ===\n";
        
        output += "ROWS:\n";
        for (int y = 0; y < gridSize.y; y++)
        {
            int sum = 0;
            string values = "";
            for (int x = 0; x < gridSize.x; x++)
            {
                Tile tile = grid[x, y];
                if (tile != null)
                {
                    sum += tile.Value;
                    values += tile.Value + " ";
                }
            }
            string matchIndicator = IsValidMatch(sum) ? $" ← MATCH! (×{sum/10})" : "";
            output += $"  Row {y}: [{values.Trim()}] = {sum}{matchIndicator}\n";
        }
        
        output += "COLUMNS:\n";
        for (int x = 0; x < gridSize.x; x++)
        {
            int sum = 0;
            string values = "";
            for (int y = 0; y < gridSize.y; y++)
            {
                Tile tile = grid[x, y];
                if (tile != null)
                {
                    sum += tile.Value;
                    values += tile.Value + " ";
                }
            }
            string matchIndicator = IsValidMatch(sum) ? $" ← MATCH! (×{sum/10})" : "";
            output += $"  Col {x}: [{values.Trim()}] = {sum}{matchIndicator}\n";
        }
        
        Debug.Log(output);
    }
    
    /// <summary>
    /// Debug: Test the hint system with detailed verification.
    /// </summary>
    [ContextMenu("Find And Verify Hint")]
    public void DebugFindAndVerifyHint()
    {
        if (gridManager == null)
        {
            Debug.LogError("GridManager not assigned!");
            return;
        }
        
        Tile[,] grid = gridManager.GetGrid();
        Vector2Int gridSize = gridManager.GetGridSize();
        
        HintMove hint = FindHintMove();

        if (hint == null)
        {
            Debug.Log("<color=red>NO HINT FOUND</color> - no single swap creates a match of 10");
            return;
        }

        int x1 = hint.tile.GridX;
        int y1 = hint.tile.GridY;
        int x2, y2;

        if (hint.targetTile != null)
        {
            // Zen two-tile hint
            x2 = hint.targetTile.GridX;
            y2 = hint.targetTile.GridY;
        }
        else
        {
            // Arcade directional hint
            x2 = x1;
            y2 = y1;
            switch (hint.direction)
            {
                case SwipeDirection.Right: x2 = x1 + 1; break;
                case SwipeDirection.Left: x2 = x1 - 1; break;
                case SwipeDirection.Down: y2 = y1 + 1; break;
                case SwipeDirection.Up: y2 = y1 - 1; break;
            }
        }
        
        int val1 = grid[x1, y1].Value;
        int val2 = grid[x2, y2].Value;
        
        Debug.Log($"<color=yellow>HINT:</color> Swap ({x1},{y1}) val={val1} {hint.direction} → ({x2},{y2}) val={val2}");
        
        // Helper to get value after swap
        int GetValueAfterSwap(int x, int y)
        {
            if (x == x1 && y == y1) return val2;
            if (x == x2 && y == y2) return val1;
            return grid[x, y]?.Value ?? 0;
        }
        
        string output = "After swap:\n";
        
        // Check row y1
        int sum = 0;
        string vals = "";
        for (int x = 0; x < gridSize.x; x++)
        {
            int v = GetValueAfterSwap(x, y1);
            sum += v;
            vals += v + " ";
        }
        output += $"  Row {y1}: [{vals.Trim()}] = {sum}" + (IsValidMatch(sum) ? " ← MATCH!" : "") + "\n";
        
        // Check row y2 (if different)
        if (y1 != y2)
        {
            sum = 0;
            vals = "";
            for (int x = 0; x < gridSize.x; x++)
            {
                int v = GetValueAfterSwap(x, y2);
                sum += v;
                vals += v + " ";
            }
            output += $"  Row {y2}: [{vals.Trim()}] = {sum}" + (IsValidMatch(sum) ? " ← MATCH!" : "") + "\n";
        }
        
        // Check column x1
        sum = 0;
        vals = "";
        for (int y = 0; y < gridSize.y; y++)
        {
            int v = GetValueAfterSwap(x1, y);
            sum += v;
            vals += v + " ";
        }
        output += $"  Col {x1}: [{vals.Trim()}] = {sum}" + (IsValidMatch(sum) ? " ← MATCH!" : "") + "\n";
        
        // Check column x2 (if different)
        if (x1 != x2)
        {
            sum = 0;
            vals = "";
            for (int y = 0; y < gridSize.y; y++)
            {
                int v = GetValueAfterSwap(x2, y);
                sum += v;
                vals += v + " ";
            }
            output += $"  Col {x2}: [{vals.Trim()}] = {sum}" + (IsValidMatch(sum) ? " ← MATCH!" : "") + "\n";
        }
        
        Debug.Log(output);
    }
}

/// <summary>
/// Contains detailed information about matches found.
/// </summary>
[System.Serializable]
public class MatchResult
{
    public HashSet<Tile> allMatchedTiles = new HashSet<Tile>();
    public List<int> matchedRows = new List<int>();
    public List<int> matchedColumns = new List<int>();

    /// <summary>Per-line sums: rowIndex → sum (10, 20, 30, 40)</summary>
    public Dictionary<int, int> rowSums = new Dictionary<int, int>();
    /// <summary>Per-line sums: colIndex → sum (10, 20, 30, 40)</summary>
    public Dictionary<int, int> columnSums = new Dictionary<int, int>();

    public bool HasMatches => allMatchedTiles.Count > 0;
    public int TotalMatchedTiles => allMatchedTiles.Count;
    public int TotalLines => matchedRows.Count + matchedColumns.Count;

    public bool IsIntersection(Tile tile)
    {
        return matchedRows.Contains(tile.GridY) && matchedColumns.Contains(tile.GridX);
    }

    /// <summary>
    /// Get the sum for a line by its index in iteration order (rows first, then columns).
    /// Same order as GetPerLineCenters uses.
    /// </summary>
    public int GetLineSumByIndex(int lineIndex)
    {
        int idx = 0;
        foreach (int row in matchedRows)
        {
            if (idx == lineIndex) return rowSums.ContainsKey(row) ? rowSums[row] : 10;
            idx++;
        }
        foreach (int col in matchedColumns)
        {
            if (idx == lineIndex) return columnSums.ContainsKey(col) ? columnSums[col] : 10;
            idx++;
        }
        return 10; // Fallback
    }
}

/// <summary>
/// Represents a hint: which tile to move and in which direction.
/// In Zen mode, targetTile is populated instead of direction (swap-any mechanic).
/// </summary>
public class HintMove
{
    public Tile tile;
    public SwipeDirection direction;
    /// <summary>Second tile for Zen all-pairs hints. Null in Arcade mode.</summary>
    public Tile targetTile;

    /// <summary>Arcade constructor: tile + swipe direction.</summary>
    public HintMove(Tile tile, SwipeDirection direction)
    {
        this.tile = tile;
        this.direction = direction;
        this.targetTile = null;
    }

    /// <summary>Zen constructor: two free tiles to swap (no direction needed).</summary>
    public HintMove(Tile tileA, Tile tileB)
    {
        this.tile = tileA;
        this.targetTile = tileB;
        this.direction = SwipeDirection.Right; // Unused placeholder
    }

    public Vector2 GetDirectionVector()
    {
        return direction switch
        {
            SwipeDirection.Up => Vector2.up,
            SwipeDirection.Down => Vector2.down,
            SwipeDirection.Left => Vector2.left,
            SwipeDirection.Right => Vector2.right,
            _ => Vector2.zero
        };
    }
}
