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
    [SerializeField] private float tileFallSpeed = 800f;
    [SerializeField] private float tileFallDelay = 0.05f;
    [SerializeField] private float postClearDelay = 0.1f;
    [SerializeField] private float tileSwapDuration = 0.15f;
    [SerializeField] private float unsolvableResetDelay = 1f; // delay before auto-reset
    
    [Header("Tile Value Weights (must sum to 1.0)")]
    [SerializeField] private float weight0 = 0.15f; // 0 tiles - wildcard
    [SerializeField] private float weight1 = 0.22f; // 1 tiles - common
    [SerializeField] private float weight2 = 0.20f; // 2 tiles
    [SerializeField] private float weight3 = 0.15f; // 3 tiles
    [SerializeField] private float weight4 = 0.12f; // 4 tiles
    [SerializeField] private float weight5 = 0.08f; // 5 tiles
    [SerializeField] private float weight6 = 0.08f; // 6 tiles - rare

    
    // The grid array
    private Tile[,] grid;
    
    // Currently selected tile (null if none)
    private Tile selectedTile;
    
    // Cached weights array
    private float[] weights;
    
    // Is the grid currently processing (animating, clearing, etc.)
    private bool isProcessing = false;
    
    // Event for unsolvable grid notification
    public event System.Action OnGridUnsolvable;
    
    private void Awake()
    {
        // Cache weights
        weights = new float[] { weight0, weight1, weight2, weight3, weight4, weight5, weight6 };
        
        // Initialize grid array
        grid = new Tile[gridWidth, gridHeight];
    }
    
    private void OnEnable()
    {
        Tile.OnTileClicked += HandleTileClicked;
        Tile.OnTileSwiped += HandleTileSwiped;
    }
    
    private void OnDisable()
    {
        Tile.OnTileClicked -= HandleTileClicked;
        Tile.OnTileSwiped -= HandleTileSwiped;
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
            // Second selection - check if adjacent
            if (!IsAdjacent(selectedTile, tile))
            {
                // Not adjacent - deselect old, select new
                selectedTile.Deselect();
                selectedTile = tile;
                tile.Select();
                Debug.Log($"Not adjacent! Switched selection to: {tile}");
                return;
            }
            
            // Adjacent - perform swap
            Tile firstTile = selectedTile;
            Tile secondTile = tile;
            
            // Clear selection reference
            selectedTile = null;
            
            // Reset visual state on BOTH tiles before swap
            firstTile.Deselect();
            secondTile.Deselect();
            
            // Now perform the animated swap
            StartCoroutine(AnimatedSwapCoroutine(firstTile, secondTile));
        }
    }
    
    /// <summary>
    /// Check if two tiles are adjacent (up, down, left, right only).
    /// </summary>
    private bool IsAdjacent(Tile a, Tile b)
    {
        int dx = Mathf.Abs(a.GridX - b.GridX);
        int dy = Mathf.Abs(a.GridY - b.GridY);
        
        // Adjacent means exactly 1 step in one direction, 0 in the other
        return (dx == 1 && dy == 0) || (dx == 0 && dy == 1);
    }
    
    /// <summary>
    /// Handle swipe gestures on tiles.
    /// </summary>
    private void HandleTileSwiped(Tile tile, SwipeDirection direction)
    {
        // Don't allow interaction while processing
        if (isProcessing) return;
        
        // Calculate neighbor position based on swipe direction
        int neighborX = tile.GridX;
        int neighborY = tile.GridY;
        
        switch (direction)
        {
            case SwipeDirection.Up:
                neighborY -= 1; // Up in grid = lower Y index
                break;
            case SwipeDirection.Down:
                neighborY += 1; // Down in grid = higher Y index
                break;
            case SwipeDirection.Left:
                neighborX -= 1;
                break;
            case SwipeDirection.Right:
                neighborX += 1;
                break;
        }
        
        // Check bounds
        if (neighborX < 0 || neighborX >= gridWidth || neighborY < 0 || neighborY >= gridHeight)
        {
            Debug.Log($"Swipe {direction} blocked - no tile in that direction");
            return;
        }
        
        // Get neighbor tile
        Tile neighborTile = grid[neighborX, neighborY];
        if (neighborTile == null)
        {
            Debug.Log($"Swipe {direction} blocked - neighbor tile is null");
            return;
        }
        
        // Clear any existing selection
        if (selectedTile != null)
        {
            selectedTile.Deselect();
            selectedTile = null;
        }
        
        // Perform the swap
        Debug.Log($"Swipe swap: {tile} <-> {neighborTile}");
        StartCoroutine(AnimatedSwapCoroutine(tile, neighborTile));
    }
    
    /// <summary>
    /// Animate the swap then process matches.
    /// </summary>
    private IEnumerator AnimatedSwapCoroutine(Tile tileA, Tile tileB)
    {
        isProcessing = true;
        
        // Get positions
        Vector2 posA = tileA.GetRectTransform().anchoredPosition;
        Vector2 posB = tileB.GetRectTransform().anchoredPosition;
        
        // Animate both tiles moving to each other's positions
        float elapsed = 0f;
        while (elapsed < tileSwapDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / tileSwapDuration;
            
            // Smooth step for nicer feel
            float smoothT = t * t * (3f - 2f * t);
            
            tileA.GetRectTransform().anchoredPosition = Vector2.Lerp(posA, posB, smoothT);
            tileB.GetRectTransform().anchoredPosition = Vector2.Lerp(posB, posA, smoothT);
            
            yield return null;
        }
        
        // Snap to final positions
        tileA.GetRectTransform().anchoredPosition = posB;
        tileB.GetRectTransform().anchoredPosition = posA;
        
        // Update grid array
        int axOld = tileA.GridX;
        int ayOld = tileA.GridY;
        int bxOld = tileB.GridX;
        int byOld = tileB.GridY;
        
        grid[axOld, ayOld] = tileB;
        grid[bxOld, byOld] = tileA;
        
        tileA.GridX = bxOld;
        tileA.GridY = byOld;
        tileB.GridX = axOld;
        tileB.GridY = ayOld;
        
        isProcessing = false;
        
        // Check for matches after swap
        StartCoroutine(ProcessMatchesCoroutine());
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
        
        // Check if grid is solvable
        if (matchChecker != null && !matchChecker.HasValidMoves())
        {
            Debug.Log("<color=red>GRID UNSOLVABLE!</color> No valid moves available. Resetting...");
            OnGridUnsolvable?.Invoke();
            yield return new WaitForSeconds(unsolvableResetDelay);
            ResetGridSilent();
            yield break;
        }
        
        isProcessing = false;
        PrintGridState();
    }
    
    /// <summary>
    /// Reset the grid without awarding points (for unsolvable situations).
    /// </summary>
    private void ResetGridSilent()
    {
        StartCoroutine(ResetGridWithEffect());
    }
    
    /// <summary>
    /// Visual effect for resetting unsolvable grid.
    /// </summary>
    private IEnumerator ResetGridWithEffect()
    {
        Debug.Log("<color=yellow>Grid reset with visual effect (no points awarded)</color>");
        
        // Collect all current tiles
        List<Tile> allTiles = new List<Tile>();
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                if (grid[x, y] != null)
                {
                    allTiles.Add(grid[x, y]);
                }
            }
        }
        
        // Flash all tiles red
        float flashDuration = 0.3f;
        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.PingPong(elapsed * 8f, 1f); // Fast flicker
            Color flashColor = Color.Lerp(Color.white, new Color(1f, 0.3f, 0.3f), t);
            
            foreach (Tile tile in allTiles)
            {
                if (tile != null)
                {
                    Image img = tile.GetComponent<Image>();
                    if (img != null) img.color = flashColor;
                }
            }
            yield return null;
        }
        
        // Shake and fall animation
        float fallDuration = 0.4f;
        elapsed = 0f;
        
        // Store original positions
        Dictionary<Tile, Vector2> originalPositions = new Dictionary<Tile, Vector2>();
        foreach (Tile tile in allTiles)
        {
            if (tile != null)
            {
                originalPositions[tile] = tile.GetRectTransform().anchoredPosition;
            }
        }
        
        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fallDuration;
            
            foreach (Tile tile in allTiles)
            {
                if (tile != null && originalPositions.ContainsKey(tile))
                {
                    RectTransform rt = tile.GetRectTransform();
                    Vector2 originalPos = originalPositions[tile];
                    
                    // Shake horizontally
                    float shake = Mathf.Sin(elapsed * 50f) * 5f * (1f - t);
                    
                    // Fall down with acceleration
                    float fallDistance = 800f * t * t;
                    
                    rt.anchoredPosition = originalPos + new Vector2(shake, -fallDistance);
                    
                    // Fade out
                    Image img = tile.GetComponent<Image>();
                    if (img != null)
                    {
                        Color c = img.color;
                        c.a = 1f - t;
                        img.color = c;
                    }
                    
                    // Shrink slightly
                    tile.transform.localScale = Vector3.one * (1f - t * 0.3f);
                }
            }
            yield return null;
        }
        
        // Clear and respawn
        ClearGrid();
        SpawnGrid();
        
        // Check for initial matches
        StartCoroutine(ProcessMatchesCoroutine());
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
