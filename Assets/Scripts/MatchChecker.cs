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
        // Auto-find GridManager if not assigned
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
        
        // Check all rows (horizontal lines)
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
                // Row matches! Add all tiles to matched set
                foreach (Tile tile in rowTiles)
                {
                    matchedTiles.Add(tile);
                }
                
                if (logMatches)
                {
                    Debug.Log($"<color=green>ROW {y} MATCH!</color> Sum = {rowSum}");
                }
            }
        }
        
        // Check all columns (vertical lines)
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
                // Column matches! Add all tiles to matched set
                foreach (Tile tile in colTiles)
                {
                    matchedTiles.Add(tile);
                }
                
                if (logMatches)
                {
                    Debug.Log($"<color=cyan>COLUMN {x} MATCH!</color> Sum = {colSum}");
                }
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
    /// Get detailed match info for UI/scoring purposes.
    /// Returns only ONE match at a time (first row or column found).
    /// </summary>
    public MatchResult GetMatchResult()
    {
        MatchResult result = new MatchResult();
        Tile[,] grid = gridManager.GetGrid();
        Vector2Int gridSize = gridManager.GetGridSize();
        
        // Check rows first - return immediately on first match
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
                {
                    result.allMatchedTiles.Add(tile);
                }
                return result; // Return after first match found
            }
        }
        
        // Check columns - return immediately on first match
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
                {
                    result.allMatchedTiles.Add(tile);
                }
                return result; // Return after first match found
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Debug: Print current sums for all rows and columns.
    /// </summary>
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
        
        // Rows
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
        
        // Columns
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
    
    // Check if a tile is at an intersection (matched both horizontally and vertically)
    public bool IsIntersection(Tile tile)
    {
        return matchedRows.Contains(tile.GridY) && matchedColumns.Contains(tile.GridX);
    }
}
