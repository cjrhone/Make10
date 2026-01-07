using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages the 6x6 grid of tiles, handles spawning, swapping, and grid operations.
/// </summary>
public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int gridWidth = 6;
    [SerializeField] private int gridHeight = 6;
    [SerializeField] private float tileSize = 100f;
    [SerializeField] private float tileSpacing = 10f;
    
    [Header("References")]
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private RectTransform gridContainer;
    public MatchChecker matchChecker;
    
    [Header("Animation Settings")]
    [SerializeField] private float tileFallSpeed = 800f; // pixels per second
    [SerializeField] private float tileFallDelay = 0.05f; // stagger delay between columns
    [SerializeField] private float matchFlashDuration = 0.3f; // how long tiles flash before disappearing
    [SerializeField] private float postClearDelay = 0.1f; // pause after clearing before dropping
    
    [Header("Tile Value Weights (must sum to 1.0)")]
    [SerializeField] private float weight0 = 0.08f; // 0 tiles - fewer wildcards
    [SerializeField] private float weight1 = 0.22f; // 1 tiles - reduced
    [SerializeField] private float weight2 = 0.22f; // 2 tiles - reduced
    [SerializeField] private float weight3 = 0.25f; // 3 tiles - increased
    [SerializeField] private float weight4 = 0.16f; // 4 tiles - increased
    [SerializeField] private float weight5 = 0.07f; // 5 tiles - slightly more

    
    // The grid array
    private Tile[,] grid;
    
    // Currently selected tile (null if none)
    private Tile selectedTile;
    
    // Cached weights array
    private float[] weights;
    
    // Is the grid currently processing (animating, clearing, etc.)
    private bool isProcessing = false;
    
    private void Awake()
    {
        // Cache weights
        weights = new float[] { weight0, weight1, weight2, weight3, weight4, weight5 };
        
        // Initialize grid array
        grid = new Tile[gridWidth, gridHeight];
    }
    
    private void OnEnable()
    {
        Tile.OnTileClicked += HandleTileClicked;
    }
    
    private void OnDisable()
    {
        Tile.OnTileClicked -= HandleTileClicked;
    }
    
    private void Start()
    {
        SpawnGrid();
        
        // Check for any matches on initial board and clear them
        StartCoroutine(ProcessMatchesCoroutine());
    }
    
    /// <summary>
    /// Spawn the initial grid of tiles.
    /// </summary>
    public void SpawnGrid()
    {
        // Clear existing tiles if any
        ClearGrid();
        
        // Calculate total grid dimensions
        float totalWidth = gridWidth * tileSize + (gridWidth - 1) * tileSpacing;
        float totalHeight = gridHeight * tileSize + (gridHeight - 1) * tileSpacing;
        
        // Starting position (top-left of grid, centered in container)
        float startX = -totalWidth / 2f + tileSize / 2f;
        float startY = totalHeight / 2f - tileSize / 2f;
        
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                // Calculate position
                float posX = startX + x * (tileSize + tileSpacing);
                float posY = startY - y * (tileSize + tileSpacing);
                
                // Create tile
                Tile tile = CreateTile(x, y, new Vector2(posX, posY));
                grid[x, y] = tile;
            }
        }
        
        Debug.Log($"Grid spawned: {gridWidth}x{gridHeight}");
    }
    
    /// <summary>
    /// Create a single tile at the specified grid position.
    /// </summary>
    private Tile CreateTile(int gridX, int gridY, Vector2 position)
    {
        GameObject tileObj = Instantiate(tilePrefab, gridContainer);
        Tile tile = tileObj.GetComponent<Tile>();
        
        if (tile != null)
        {
            int value = GetWeightedRandomValue();
            tile.Initialize(value, gridX, gridY);
            tile.SetPosition(position);
            
            // Set size
            RectTransform rt = tile.GetRectTransform();
            rt.sizeDelta = new Vector2(tileSize, tileSize);
        }
        
        return tile;
    }
    
    /// <summary>
    /// Get a random tile value (0-4) based on weighted distribution.
    /// </summary>
    private int GetWeightedRandomValue()
    {
        float roll = Random.value;
        float cumulative = 0f;
        
        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
            {
                return i;
            }
        }
        
        return 2; // Fallback to most common
    }
    
    /// <summary>
    /// Handle tile click events.
    /// </summary>
    private void HandleTileClicked(Tile tile)
    {
        // Don't allow interaction while processing
        if (isProcessing) return;
        
        if (selectedTile == null)
        {
            // First selection
            selectedTile = tile;
            tile.Select();
            Debug.Log($"Selected: {tile}");
        }
        else if (selectedTile == tile)
        {
            // Clicked same tile - deselect
            tile.Deselect();
            selectedTile = null;
            Debug.Log("Deselected");
        }
        else
        {
            // Second selection - perform swap
            Tile firstTile = selectedTile;
            Tile secondTile = tile;
            
            // Clear selection reference
            selectedTile = null;
            
            // Reset visual state on BOTH tiles before swap
            firstTile.Deselect();
            secondTile.Deselect();
            
            // Now perform the swap
            SwapTiles(firstTile, secondTile);
        }
    }
    
    /// <summary>
    /// Swap two tiles in the grid.
    /// </summary>
    private void SwapTiles(Tile tileA, Tile tileB)
    {
        Debug.Log($"Swapping {tileA} with {tileB}");
        
        // Store positions
        int axOld = tileA.GridX;
        int ayOld = tileA.GridY;
        int bxOld = tileB.GridX;
        int byOld = tileB.GridY;
        
        // Swap in grid array
        grid[axOld, ayOld] = tileB;
        grid[bxOld, byOld] = tileA;
        
        // Swap grid coordinates on tiles
        tileA.GridX = bxOld;
        tileA.GridY = byOld;
        tileB.GridX = axOld;
        tileB.GridY = ayOld;
        
        // Swap visual positions
        Vector2 posA = tileA.GetRectTransform().anchoredPosition;
        Vector2 posB = tileB.GetRectTransform().anchoredPosition;
        
        tileA.SetPosition(posB);
        tileB.SetPosition(posA);
        
        // Check for matches after swap
        StartCoroutine(ProcessMatchesCoroutine());
    }
    
    /// <summary>
    /// Main cascade loop: Check → Clear → Fall → Spawn → Repeat
    /// </summary>
    /// <summary>
/// Main cascade loop: Check → Clear → Fall → Spawn → Repeat
/// </summary>
    private IEnumerator ProcessMatchesCoroutine()
    {
        isProcessing = true;
        int cascadeCount = 0;
        
        // Notify GameManager cascade is starting
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnCascadeStart();
        }
        
        while (true)
        {
            // Check for matches
            if (matchChecker == null)
            {
                Debug.LogWarning("MatchChecker not assigned!");
                break;
            }
            
            MatchResult result = matchChecker.GetMatchResult();
            
            if (!result.HasMatches)
            {
                // No matches, we're done
                if (cascadeCount > 0)
                {
                    Debug.Log($"Cascade complete! {cascadeCount} chain(s)");
                }
                else
                {
                    Debug.Log("No matches found.");
                }
                break;
            }
            
            cascadeCount++;
            Debug.Log($"<color=yellow>MATCH {cascadeCount}!</color> " +
                    $"{result.matchedRows.Count} rows, {result.matchedColumns.Count} columns, " +
                    $"{result.TotalMatchedTiles} tiles");
            
            // 1. Flash matched tiles
            yield return StartCoroutine(FlashMatchedTiles(result.allMatchedTiles));
            
            // 2. Clear matched tiles
            ClearMatchedTiles(result.allMatchedTiles);
            
            // 3. Notify GameManager for scoring
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnMatchCleared(
                    result.TotalMatchedTiles,
                    result.matchedRows.Count,
                    result.matchedColumns.Count
                );
            }
            
            yield return new WaitForSeconds(postClearDelay);
            
            // 3. Drop remaining tiles down
            yield return StartCoroutine(DropTilesCoroutine());
            
            // 4. Spawn new tiles at top
            yield return StartCoroutine(SpawnNewTilesCoroutine());
            
            // Small pause before next check
            yield return new WaitForSeconds(0.1f);
            
            // Loop continues to check for chain reactions
        }
        
        // Notify GameManager cascade is complete
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnCascadeEnd();
        }
        
        isProcessing = false;
        PrintGridState();
    }
    
    /// <summary>
    /// Flash tiles before they disappear.
    /// </summary>
    /// <summary>
/// Balloon growth effect - tiles grow then pop.
/// </summary>
private IEnumerator FlashMatchedTiles(HashSet<Tile> tiles)
{
    float growDuration = 0.4f;
    float maxScale = 1.4f;
    
    // Gradually grow (balloon filling)
    float elapsed = 0f;
    while (elapsed < growDuration)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / growDuration;
        
        // Ease out - fast at start, slows down (like air resistance)
        float easedT = 1f - Mathf.Pow(1f - t, 3f);
        float currentScale = Mathf.Lerp(1f, maxScale, easedT);
        
        // Also shift color toward bright yellow/white
        Color currentColor = Color.Lerp(Color.white, new Color(1f, 1f, 0.7f), easedT);
        
        foreach (Tile tile in tiles)
        {
            if (tile != null)
            {
                tile.transform.localScale = Vector3.one * currentScale;
                tile.GetComponent<Image>().color = currentColor;
            }
        }
        yield return null;
    }
    
    // Brief hold at max size
    yield return new WaitForSeconds(0.1f);
    
    // Quick "pop" - rapid scale up then gone
    foreach (Tile tile in tiles)
    {
        if (tile != null)
        {
            tile.transform.localScale = Vector3.one * 1.6f;
            tile.GetComponent<Image>().color = Color.white;
        }
    }
    yield return new WaitForSeconds(0.05f);
}
    
    /// <summary>
    /// Remove matched tiles from the grid.
    /// </summary>
    private void ClearMatchedTiles(HashSet<Tile> tiles)
    {
        foreach (Tile tile in tiles)
        {
            if (tile != null)
            {
                // Clear from grid array
                grid[tile.GridX, tile.GridY] = null;
                
                // Destroy the GameObject
                Destroy(tile.gameObject);
            }
        }
        
        Debug.Log($"Cleared {tiles.Count} tiles");
    }
    
    /// <summary>
    /// Drop tiles down to fill empty spaces (simulated physics).
    /// </summary>
    private IEnumerator DropTilesCoroutine()
    {
        bool anyTileDropped = true;
        
        // Keep dropping until no more movement
        while (anyTileDropped)
        {
            anyTileDropped = false;
            List<Coroutine> dropAnimations = new List<Coroutine>();
            
            // Process each column
            for (int x = 0; x < gridWidth; x++)
            {
                // Start from bottom, find empty spaces
                for (int y = gridHeight - 1; y >= 0; y--)
                {
                    if (grid[x, y] == null)
                    {
                        // Found empty space, look for tile above to drop
                        for (int above = y - 1; above >= 0; above--)
                        {
                            if (grid[x, above] != null)
                            {
                                // Found a tile to drop
                                Tile tileToMove = grid[x, above];
                                
                                // Update grid array
                                grid[x, y] = tileToMove;
                                grid[x, above] = null;
                                
                                // Update tile's grid position
                                int oldY = tileToMove.GridY;
                                tileToMove.GridY = y;
                                
                                // Animate the fall
                                Vector2 targetPos = GridToWorldPosition(x, y);
                                dropAnimations.Add(StartCoroutine(AnimateTileFall(tileToMove, targetPos)));
                                
                                anyTileDropped = true;
                                break; // Move to next empty space
                            }
                        }
                    }
                }
            }
            
            // Wait for all drop animations to complete
            foreach (Coroutine c in dropAnimations)
            {
                yield return c;
            }
        }
    }
    
    /// <summary>
    /// Animate a single tile falling to target position.
    /// </summary>
    private IEnumerator AnimateTileFall(Tile tile, Vector2 targetPosition)
    {
        if (tile == null) yield break;
        
        RectTransform rt = tile.GetRectTransform();
        Vector2 startPos = rt.anchoredPosition;
        float distance = Vector2.Distance(startPos, targetPosition);
        float duration = distance / tileFallSpeed;
        
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Ease out bounce effect (simplified)
            float easedT = 1f - Mathf.Pow(1f - t, 2f); // Ease out quad
            
            rt.anchoredPosition = Vector2.Lerp(startPos, targetPosition, easedT);
            yield return null;
        }
        
        // Snap to final position
        rt.anchoredPosition = targetPosition;
        
        // Small bounce at the end
        yield return StartCoroutine(TileLandBounce(tile));
    }
    
    /// <summary>
    /// Small bounce effect when tile lands.
    /// </summary>
    private IEnumerator TileLandBounce(Tile tile)
    {
        if (tile == null) yield break;
        
        // Quick squash and stretch
        Transform t = tile.transform;
        
        // Squash
        t.localScale = new Vector3(1.1f, 0.9f, 1f);
        yield return new WaitForSeconds(0.05f);
        
        // Stretch
        t.localScale = new Vector3(0.95f, 1.05f, 1f);
        yield return new WaitForSeconds(0.05f);
        
        // Settle
        t.localScale = Vector3.one;
    }
    
    /// <summary>
    /// Spawn new tiles at the top to fill empty spaces.
    /// </summary>
    private IEnumerator SpawnNewTilesCoroutine()
    {
        List<Coroutine> spawnAnimations = new List<Coroutine>();
        
        // Check each column for empty spaces
        for (int x = 0; x < gridWidth; x++)
        {
            int emptyCount = 0;
            
            // Count empty spaces in this column (from top)
            for (int y = 0; y < gridHeight; y++)
            {
                if (grid[x, y] == null)
                {
                    emptyCount++;
                }
            }
            
            // Spawn tiles for empty spaces
            int spawnIndex = 0;
            for (int y = 0; y < gridHeight; y++)
            {
                if (grid[x, y] == null)
                {
                    // Calculate spawn position (above the grid)
                    Vector2 spawnPos = GridToWorldPosition(x, -1 - spawnIndex);
                    Vector2 targetPos = GridToWorldPosition(x, y);
                    
                    // Create the tile
                    GameObject tileObj = Instantiate(tilePrefab, gridContainer);
                    Tile newTile = tileObj.GetComponent<Tile>();
                    
                    if (newTile != null)
                    {
                        int value = GetWeightedRandomValue();
                        newTile.Initialize(value, x, y);
                        newTile.SetPosition(spawnPos);
                        
                        // Set size
                        RectTransform rt = newTile.GetRectTransform();
                        rt.sizeDelta = new Vector2(tileSize, tileSize);
                        
                        // Add to grid
                        grid[x, y] = newTile;
                        
                        // Animate falling into place (with stagger delay)
                        float delay = x * tileFallDelay;
                        spawnAnimations.Add(StartCoroutine(AnimateNewTileFall(newTile, targetPos, delay)));
                    }
                    
                    spawnIndex++;
                }
            }
        }
        
        // Wait for all spawn animations
        foreach (Coroutine c in spawnAnimations)
        {
            yield return c;
        }
        
        Debug.Log("New tiles spawned");
    }
    
    /// <summary>
    /// Animate a newly spawned tile falling into place.
    /// </summary>
    private IEnumerator AnimateNewTileFall(Tile tile, Vector2 targetPosition, float delay)
    {
        if (delay > 0)
        {
            yield return new WaitForSeconds(delay);
        }
        
        yield return StartCoroutine(AnimateTileFall(tile, targetPosition));
    }
    
    /// <summary>
    /// Get tile at grid position.
    /// </summary>
    public Tile GetTile(int x, int y)
    {
        if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight)
        {
            return grid[x, y];
        }
        return null;
    }
    
    /// <summary>
    /// Get the entire grid array (for MatchChecker).
    /// </summary>
    public Tile[,] GetGrid()
    {
        return grid;
    }
    
    /// <summary>
    /// Get grid dimensions.
    /// </summary>
    public Vector2Int GetGridSize()
    {
        return new Vector2Int(gridWidth, gridHeight);
    }
    
    /// <summary>
    /// Calculate world position for a grid coordinate.
    /// </summary>
    public Vector2 GridToWorldPosition(int gridX, int gridY)
    {
        float totalWidth = gridWidth * tileSize + (gridWidth - 1) * tileSpacing;
        float totalHeight = gridHeight * tileSize + (gridHeight - 1) * tileSpacing;
        
        float startX = -totalWidth / 2f + tileSize / 2f;
        float startY = totalHeight / 2f - tileSize / 2f;
        
        float posX = startX + gridX * (tileSize + tileSpacing);
        float posY = startY - gridY * (tileSize + tileSpacing);
        
        return new Vector2(posX, posY);
    }
    
    /// <summary>
    /// Clear all tiles from the grid.
    /// </summary>
    public void ClearGrid()
    {
        if (grid != null)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    if (grid[x, y] != null)
                    {
                        Destroy(grid[x, y].gameObject);
                        grid[x, y] = null;
                    }
                }
            }
        }
        
        selectedTile = null;
    }
    
    /// <summary>
    /// Debug: Print current grid state to console.
    /// </summary>
    [ContextMenu("Print Grid State")]
    public void PrintGridState()
    {
        string output = "Grid State:\n";
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                Tile tile = grid[x, y];
                output += tile != null ? tile.Value.ToString() : "X";
                output += " ";
            }
            output += "\n";
        }
        Debug.Log(output);
    }
    
    /// <summary>
    /// Debug: Check row/column sums.
    /// </summary>
    [ContextMenu("Check Sums")]
    public void DebugCheckSums()
    {
        // Check rows
        for (int y = 0; y < gridHeight; y++)
        {
            int sum = 0;
            for (int x = 0; x < gridWidth; x++)
            {
                sum += grid[x, y].Value;
            }
            Debug.Log($"Row {y} sum: {sum}" + (sum == 10 ? " ← MATCH!" : ""));
        }
        
        // Check columns
        for (int x = 0; x < gridWidth; x++)
        {
            int sum = 0;
            for (int y = 0; y < gridHeight; y++)
            {
                sum += grid[x, y].Value;
            }
            Debug.Log($"Column {x} sum: {sum}" + (sum == 10 ? " ← MATCH!" : ""));
        }
    }

    /// <summary>
/// Reset the game (called by UIManager on restart).
/// </summary>
    public void ResetGame()
    {
        // Clear existing grid
        ClearGrid();
        
        // Spawn new grid
        SpawnGrid();
        
        // Check for initial matches
        StartCoroutine(ProcessMatchesCoroutine());
    }
}
