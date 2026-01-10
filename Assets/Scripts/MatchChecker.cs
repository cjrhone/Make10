using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Evaluates the grid for valid matches (rows/columns that sum to 10).
/// Pure logic - no MonoBehaviour needed, but using it for easy inspector access.
/// </summary>
public class MatchChecker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager gridManager;
    
    [Header("Settings")]
    [SerializeField] private int targetSum = 10;
    
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
            List<Tile> rowTiles = new List<Tile>();
            int rowSum = 0;
            
            for (int x = 0; x < gridSize.x; x++)
            {
                Tile tile = grid[x, y];
                if (tile != null)
                {
                    rowTiles.Add(tile);
                    rowSum += tile.Value;
                }
            }
            
            if (rowSum == targetSum)
            {
                foreach (Tile tile in rowTiles)
                    matchedTiles.Add(tile);
                
                if (logMatches)
                    Debug.Log($"<color=green>ROW {y} MATCH!</color> Sum = {rowSum}");
            }
        }
        
        // Check all columns
        for (int x = 0; x < gridSize.x; x++)
        {
            List<Tile> colTiles = new List<Tile>();
            int colSum = 0;
            
            for (int y = 0; y < gridSize.y; y++)
            {
                Tile tile = grid[x, y];
                if (tile != null)
                {
                    colTiles.Add(tile);
                    colSum += tile.Value;
                }
            }
            
            if (colSum == targetSum)
            {
                foreach (Tile tile in colTiles)
                    matchedTiles.Add(tile);
                
                if (logMatches)
                    Debug.Log($"<color=cyan>COLUMN {x} MATCH!</color> Sum = {colSum}");
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
    /// Get detailed match info - returns only ONE match at a time.
    /// </summary>
    public MatchResult GetMatchResult()
    {
        MatchResult result = new MatchResult();
        Tile[,] grid = gridManager.GetGrid();
        Vector2Int gridSize = gridManager.GetGridSize();
        
        // Check rows first
        for (int y = 0; y < gridSize.y; y++)
        {
            int rowSum = 0;
            List<Tile> rowTiles = new List<Tile>();
            
            for (int x = 0; x < gridSize.x; x++)
            {
                Tile tile = grid[x, y];
                if (tile != null)
                {
                    rowTiles.Add(tile);
                    rowSum += tile.Value;
                }
            }
            
            if (rowSum == targetSum)
            {
                result.matchedRows.Add(y);
                foreach (Tile tile in rowTiles)
                    result.allMatchedTiles.Add(tile);
                return result;
            }
        }
        
        // Check columns
        for (int x = 0; x < gridSize.x; x++)
        {
            int colSum = 0;
            List<Tile> colTiles = new List<Tile>();
            
            for (int y = 0; y < gridSize.y; y++)
            {
                Tile tile = grid[x, y];
                if (tile != null)
                {
                    colTiles.Add(tile);
                    colSum += tile.Value;
                }
            }
            
            if (colSum == targetSum)
            {
                result.matchedColumns.Add(x);
                foreach (Tile tile in colTiles)
                    result.allMatchedTiles.Add(tile);
                return result;
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Find a swap that would create a match.
    /// Returns the tile to move and the direction to swipe, or null if no hint found.
    /// </summary>
    public HintMove FindHintMove()
    {
        Tile[,] grid = gridManager.GetGrid();
        Vector2Int gridSize = gridManager.GetGridSize();
        
        // Try every possible adjacent swap
        for (int y = 0; y < gridSize.y; y++)
        {
            for (int x = 0; x < gridSize.x; x++)
            {
                Tile tile = grid[x, y];
                if (tile == null) continue;
                
                // Try swapping right
                if (x + 1 < gridSize.x && grid[x + 1, y] != null)
                {
                    if (WouldCreateMatch(x, y, x + 1, y))
                        return new HintMove(tile, SwipeDirection.Right);
                }
                
                // Try swapping down
                if (y + 1 < gridSize.y && grid[x, y + 1] != null)
                {
                    if (WouldCreateMatch(x, y, x, y + 1))
                        return new HintMove(tile, SwipeDirection.Down);
                }
                
                // Try swapping left
                if (x - 1 >= 0 && grid[x - 1, y] != null)
                {
                    if (WouldCreateMatch(x, y, x - 1, y))
                        return new HintMove(tile, SwipeDirection.Left);
                }
                
                // Try swapping up
                if (y - 1 >= 0 && grid[x, y - 1] != null)
                {
                    if (WouldCreateMatch(x, y, x, y - 1))
                        return new HintMove(tile, SwipeDirection.Up);
                }
            }
        }
        
        return null; // No hint found
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
        if (sum == targetSum) return true;
        
        // Check row y2 (only if different from y1)
        if (y1 != y2)
        {
            sum = 0;
            for (int x = 0; x < gridSize.x; x++)
                sum += GetValueAfterSwap(x, y2);
            if (sum == targetSum) return true;
        }
        
        // Check column x1
        sum = 0;
        for (int y = 0; y < gridSize.y; y++)
            sum += GetValueAfterSwap(x1, y);
        if (sum == targetSum) return true;
        
        // Check column x2 (only if different from x1)
        if (x1 != x2)
        {
            sum = 0;
            for (int y = 0; y < gridSize.y; y++)
                sum += GetValueAfterSwap(x2, y);
            if (sum == targetSum) return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Check if any combination of tiles can sum to 10.
    /// </summary>
    public bool HasValidMoves()
    {
        Tile[,] grid = gridManager.GetGrid();
        Vector2Int gridSize = gridManager.GetGridSize();
        
        List<int> allValues = new List<int>();
        for (int y = 0; y < gridSize.y; y++)
            for (int x = 0; x < gridSize.x; x++)
                if (grid[x, y] != null)
                    allValues.Add(grid[x, y].Value);
        
        int tilesPerLine = gridSize.x;
        return CanSum(allValues, tilesPerLine, targetSum, 0);
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
            string matchIndicator = (sum == targetSum) ? " ← MATCH!" : "";
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
            string matchIndicator = (sum == targetSum) ? " ← MATCH!" : "";
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
        int x2 = x1, y2 = y1;
        
        switch (hint.direction)
        {
            case SwipeDirection.Right: x2 = x1 + 1; break;
            case SwipeDirection.Left: x2 = x1 - 1; break;
            case SwipeDirection.Down: y2 = y1 + 1; break;
            case SwipeDirection.Up: y2 = y1 - 1; break;
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
        output += $"  Row {y1}: [{vals.Trim()}] = {sum}" + (sum == 10 ? " ← MATCH!" : "") + "\n";
        
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
            output += $"  Row {y2}: [{vals.Trim()}] = {sum}" + (sum == 10 ? " ← MATCH!" : "") + "\n";
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
        output += $"  Col {x1}: [{vals.Trim()}] = {sum}" + (sum == 10 ? " ← MATCH!" : "") + "\n";
        
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
            output += $"  Col {x2}: [{vals.Trim()}] = {sum}" + (sum == 10 ? " ← MATCH!" : "") + "\n";
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
    
    public bool HasMatches => allMatchedTiles.Count > 0;
    public int TotalMatchedTiles => allMatchedTiles.Count;
    public int TotalLines => matchedRows.Count + matchedColumns.Count;
    
    public bool IsIntersection(Tile tile)
    {
        return matchedRows.Contains(tile.GridY) && matchedColumns.Contains(tile.GridX);
    }
}

/// <summary>
/// Represents a hint: which tile to move and in which direction.
/// </summary>
public class HintMove
{
    public Tile tile;
    public SwipeDirection direction;
    
    public HintMove(Tile tile, SwipeDirection direction)
    {
        this.tile = tile;
        this.direction = direction;
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
