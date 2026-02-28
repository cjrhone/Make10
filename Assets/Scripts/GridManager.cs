using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages the grid of tiles, handles spawning, swapping, and grid operations.
/// Grid size is dynamically set based on difficulty.
/// </summary>
public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int gridWidth = 5;
    [SerializeField] private int gridHeight = 5;
    [SerializeField] private float baseTileSpacing = 10f; // Base spacing at reference size
    [SerializeField] private float referenceContainerSize = 550f; // Reference size for proportional scaling
    #pragma warning disable CS0414 // Inspector-assigned fields
    [SerializeField] private float baseFontSize = 72f; // Base font size at reference size
    #pragma warning restore CS0414

    [Header("Editor Preview")]
    [SerializeField] private Color editorGridLineColor = new Color(1f, 1f, 0f, 0.5f);
    [SerializeField] private bool showGridLinesInEditor = true;
    
    // Actual tile size and spacing (calculated based on container size)
    private float tileSize;
    private float tileSpacing;
    private float scaleFactor = 1f; // Container size / reference size
    
    [Header("References")]
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private RectTransform gridContainer;
    public MatchChecker matchChecker;
    
    [Header("Animation Settings")]
    [SerializeField] private float tileFallSpeed = 1600f;
    #pragma warning disable CS0414 // Inspector-assigned, may be used in future tuning
    [SerializeField] private float tileFallDelay = 0.02f;
    [SerializeField] private float postClearDelay = 0.05f;
    #pragma warning restore CS0414
    [SerializeField] private float tileSwapDuration = 0.15f;
    [SerializeField] private float unsolvableResetDelay = 1f;
    
    [Header("Solve Animation Settings")]
    [SerializeField] private float solveConvergeDuration = 0.3f;
    [SerializeField] private float solveShowTenDuration = 0.4f;
    [SerializeField] private float convergeShrinkAmount = 0.7f;
    [SerializeField] private GameObject tenTextPrefab;
    
    [Header("Ten Effect Magic Settings")]
    [SerializeField] private int sparkleCount = 12;
    [SerializeField] private float burstRingCount = 2;
    [SerializeField] private Color tenGlowColor = new Color(1f, 0.9f, 0.3f);
    [SerializeField] private Color sparkleColor = new Color(1f, 0.95f, 0.6f);

    [Header("Managers")]
    [SerializeField] private GridValidation gridValidation;
    [SerializeField] private TileWeightManager tileWeightManager;

    [Header("Hint System")]
    [SerializeField] private bool enableHints = true;
    [SerializeField] private float hintDelay = 10f;
    [SerializeField] private float hintRepeatInterval = 3f;
    [SerializeField] private int hintParticleCount = 5;
    [SerializeField] private float hintParticleSpeed = 120f;
    [SerializeField] private float hintParticleLifetime = 0.5f;
    [SerializeField] private float hintParticleSize = 12f;
    [SerializeField] private Color hintParticleColor = new Color(1f, 0.9f, 0.3f, 0.9f);

    private Tile[,] grid;
    private Tile selectedTile;
    private bool isProcessing = false;

    // Hint system state
    private float timeSinceLastMove = 0f;
    private float timeSinceLastHint = 0f;
    private bool hintActive = false;
    private HintMove currentHint = null;
    private List<GameObject> activeHintParticles = new List<GameObject>();

    // Drag-swap state
    private bool isDragging = false;
    private Tile draggedTile = null;
    private int dragCurrentGridX, dragCurrentGridY;

    // MakeZen: track last swapped tiles for merge position logic
    private Tile lastSwappedFirst;
    private Tile lastSwappedSecond;

    public event System.Action OnGridUnsolvable;

    /// <summary>
    /// Data for a single matching line in Zen mode (one row or column).
    /// </summary>
    private struct ZenLineMatch
    {
        public bool isRow;          // true = row match, false = column match
        public int lineIndex;       // which row (y) or column (x)
        public int sum;             // line sum (10, 20, 30, ...)
        public Tile[] tiles;        // all tiles in this line (length = gridWidth or gridHeight)
        public int mergeGridPos;    // grid position along the line for the merge tile
    }

    /// <summary>
    /// Called when a round starts to reset progressive tile weight tracking.
    /// Solve-based ramp reads from GameManager.Instance.SolveCount (reset per round by GameManager).
    /// </summary>
    public void OnRoundStarted()
    {
        Debug.Log("[GridManager] Round started - solve-based weight ramp active");
    }

    /// <summary>
    /// Immediately halt all grid processing and tile interaction.
    /// Used when game ends (win/loss) to prevent cascading auto-wins.
    /// </summary>
    public void FreezeGrid()
    {
        StopAllCoroutines();
        isProcessing = false;
        Debug.Log("[GridManager] Grid frozen - all processing halted.");
    }

    private void Awake()
    {
        grid = new Tile[gridWidth, gridHeight];
        CalculateSizesFromContainer();
    }

    /// <summary>
    /// Calculate tile size, spacing, and scale factor based on container size.
    /// </summary>
    private void CalculateSizesFromContainer()
    {
        if (gridContainer == null) return;

        float containerWidth = gridContainer.sizeDelta.x;
        scaleFactor = containerWidth / referenceContainerSize;
        tileSpacing = baseTileSpacing * scaleFactor;

        float totalSpacing = (gridWidth - 1) * tileSpacing;
        tileSize = (containerWidth - totalSpacing) / gridWidth;
    }

    #if UNITY_EDITOR
    /// <summary>
    /// Recalculates grid preview when settings change in the editor.
    /// Resize the gridContainer to change the grid size - tiles will scale to fit.
    /// </summary>
    private void OnValidate()
    {
        if (gridContainer == null) return;

        // Recalculate sizes based on current container size
        float containerWidth = gridContainer.sizeDelta.x;
        float editorScaleFactor = containerWidth / referenceContainerSize;
        float editorTileSpacing = baseTileSpacing * editorScaleFactor;
        float editorTotalSpacing = (gridWidth - 1) * editorTileSpacing;
        float editorTileSize = (containerWidth - editorTotalSpacing) / gridWidth;

        // Update the private fields for gizmo drawing
        scaleFactor = editorScaleFactor;
        tileSpacing = editorTileSpacing;
        tileSize = editorTileSize;
    }

    /// <summary>
    /// Draws grid lines in the Scene view for visual preview.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!showGridLinesInEditor || gridContainer == null) return;

        // Calculate sizes for preview (in case OnValidate hasn't run)
        float containerWidth = gridContainer.sizeDelta.x;
        float previewScaleFactor = containerWidth / referenceContainerSize;
        float previewTileSpacing = baseTileSpacing * previewScaleFactor;
        float previewTotalSpacing = (gridWidth - 1) * previewTileSpacing;
        float previewTileSize = (containerWidth - previewTotalSpacing) / gridWidth;

        float totalWidth = gridWidth * previewTileSize + (gridWidth - 1) * previewTileSpacing;
        float totalHeight = gridHeight * previewTileSize + (gridHeight - 1) * previewTileSpacing;

        // Get the canvas for proper world-space scaling
        Canvas canvas = gridContainer.GetComponentInParent<Canvas>();
        float canvasScale = canvas != null ? canvas.transform.lossyScale.x : 1f;

        // Get world position of the container center
        Vector3 containerCenter = gridContainer.position;

        Gizmos.color = editorGridLineColor;

        float startX = -totalWidth / 2f;
        float startY = totalHeight / 2f;

        // Draw tile cells
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                float posX = startX + x * (previewTileSize + previewTileSpacing) + previewTileSize / 2f;
                float posY = startY - y * (previewTileSize + previewTileSpacing) - previewTileSize / 2f;

                Vector3 cellCenter = containerCenter + new Vector3(posX * canvasScale, posY * canvasScale, 0);
                Vector3 cellSize = new Vector3(previewTileSize * canvasScale, previewTileSize * canvasScale, 0);

                Gizmos.DrawWireCube(cellCenter, cellSize);
            }
        }

        // Draw outer boundary
        Gizmos.color = new Color(editorGridLineColor.r, editorGridLineColor.g, editorGridLineColor.b, 1f);
        Vector3 boundarySize = new Vector3(totalWidth * canvasScale, totalHeight * canvasScale, 0);
        Gizmos.DrawWireCube(containerCenter, boundarySize);
    }
    #endif
    
    private void OnEnable()
    {
        Tile.OnTileClicked += HandleTileClicked;
        Tile.OnTileSwiped += HandleTileSwiped;
        Tile.OnTileDragStarted += HandleDragStarted;
        Tile.OnTileDragMoved += HandleDragMoved;
        Tile.OnTileDragEnded += HandleDragEnded;
    }

    private void OnDisable()
    {
        Tile.OnTileClicked -= HandleTileClicked;
        Tile.OnTileSwiped -= HandleTileSwiped;
        Tile.OnTileDragStarted -= HandleDragStarted;
        Tile.OnTileDragMoved -= HandleDragMoved;
        Tile.OnTileDragEnded -= HandleDragEnded;
    }
    
    private void Start()
    {
        if (SceneFlowManager.Instance == null)
        {
            Debug.Log("No SceneFlowManager found - auto-starting grid for testing");
            SpawnGrid();
            StartCoroutine(ProcessMatchesCoroutine());
        }
    }
    
    private void Update()
    {
        // Only track hint timer when game is active and not processing
        if (!enableHints) return;
        if (GameManager.Instance == null || !GameManager.Instance.IsGameActive) return;
        if (isProcessing) return;
        
        timeSinceLastMove += Time.deltaTime;
        
        // Check if it's time to show a hint
        if (timeSinceLastMove >= hintDelay)
        {
            timeSinceLastHint += Time.deltaTime;
            
            // Show hint periodically
            if (!hintActive || timeSinceLastHint >= hintRepeatInterval)
            {
                ShowHint();
                timeSinceLastHint = 0f;
            }
        }
    }
    
    #region Hint System
    
    private void ResetHintTimer()
    {
        timeSinceLastMove = 0f;
        timeSinceLastHint = 0f;
        hintActive = false;
        currentHint = null;
        ClearHintParticles();
    }
    
    private void ShowHint()
    {
        if (matchChecker == null) return;
        
        // Find a valid move
        currentHint = matchChecker.FindHintMove();
        
        if (currentHint != null && currentHint.tile != null)
        {
            hintActive = true;
            StartCoroutine(SpawnHintParticles(currentHint));
            Debug.Log($"<color=yellow>HINT:</color> Swipe {currentHint.tile} {currentHint.direction}");
        }
    }
    
    private IEnumerator SpawnHintParticles(HintMove hint)
    {
        if (hint.tile == null) yield break;
        
        Vector2 tilePos = hint.tile.GetRectTransform().anchoredPosition;
        Vector2 direction = hint.GetDirectionVector();
        
        // Spawn particles in a burst
        for (int i = 0; i < hintParticleCount; i++)
        {
            SpawnSingleHintParticle(tilePos, direction, i * 0.06f);
            yield return new WaitForSeconds(0.04f);
        }
    }
    
    private void SpawnSingleHintParticle(Vector2 startPos, Vector2 direction, float delay)
    {
        StartCoroutine(AnimateHintParticle(startPos, direction, delay));
    }
    
    private IEnumerator AnimateHintParticle(Vector2 startPos, Vector2 direction, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // Create particle
        GameObject particle = new GameObject("HintParticle");
        particle.transform.SetParent(gridContainer, false);
        activeHintParticles.Add(particle);
        
        RectTransform rt = particle.AddComponent<RectTransform>();

        // Start slightly behind center, end ahead (scaled)
        float startOffset = -20f * scaleFactor;
        float endOffset = 60f * scaleFactor;
        rt.anchoredPosition = startPos + direction * startOffset;
        rt.sizeDelta = new Vector2(hintParticleSize * scaleFactor, hintParticleSize * scaleFactor);
        rt.localEulerAngles = new Vector3(0, 0, 45f); // Diamond shape

        Image img = particle.AddComponent<Image>();
        img.color = hintParticleColor;
        img.raycastTarget = false;

        // Animate: move in direction, fade out, shrink
        float elapsed = 0f;
        Vector2 velocity = direction * hintParticleSpeed * scaleFactor;

        // Add slight randomness (scaled)
        float wobble = Random.Range(-15f, 15f) * scaleFactor;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        
        while (elapsed < hintParticleLifetime)
        {
            if (particle == null) yield break;
            
            elapsed += Time.deltaTime;
            float t = elapsed / hintParticleLifetime;
            
            // Move forward
            Vector2 pos = startPos + direction * Mathf.Lerp(startOffset, endOffset, t);
            pos += perpendicular * Mathf.Sin(t * Mathf.PI * 2f) * wobble * (1f - t);
            rt.anchoredPosition = pos;
            
            // Fade: appear quickly, fade out slowly
            float alpha;
            if (t < 0.2f)
                alpha = t / 0.2f; // Fade in
            else
                alpha = 1f - ((t - 0.2f) / 0.8f); // Fade out
            
            img.color = new Color(hintParticleColor.r, hintParticleColor.g, hintParticleColor.b, 
                                  hintParticleColor.a * alpha);
            
            // Scale: start small, grow, then shrink
            float scale = Mathf.Sin(t * Mathf.PI) * 1.2f + 0.3f;
            rt.localScale = Vector3.one * scale;
            
            yield return null;
        }
        
        // Cleanup
        if (particle != null)
        {
            activeHintParticles.Remove(particle);
            Destroy(particle);
        }
    }
    
    private void ClearHintParticles()
    {
        foreach (GameObject p in activeHintParticles)
        {
            if (p != null)
                Destroy(p);
        }
        activeHintParticles.Clear();
    }
    
    #endregion
    
    public void SpawnGrid()
    {
        Debug.Log("GridManager.SpawnGrid() called");

        if (tilePrefab == null)
        {
            Debug.LogError("GridManager: tilePrefab is not assigned!");
            return;
        }

        // Solve-based ramp reads GameManager.Instance.SolveCount directly — no timer needed
        
        // Get grid size from GameManager (difficulty-based)
        UpdateGridSizeFromDifficulty();
        
        ClearGrid();
        ResetHintTimer();
        
        float totalWidth = gridWidth * tileSize + (gridWidth - 1) * tileSpacing;
        float totalHeight = gridHeight * tileSize + (gridHeight - 1) * tileSpacing;
        float startX = -totalWidth / 2f + tileSize / 2f;
        float startY = totalHeight / 2f - tileSize / 2f;
        
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                float posX = startX + x * (tileSize + tileSpacing);
                float posY = startY - y * (tileSize + tileSpacing);
                Tile tile = CreateTile(x, y, new Vector2(posX, posY));
                grid[x, y] = tile;
            }
        }
        
        // Ensure no rows/columns already sum to 10 — first match must come from the player
        gridValidation.EnsureNoInitialMatches(grid, gridWidth, gridHeight, tileWeightManager);

        Debug.Log($"Grid spawned: {gridWidth}x{gridHeight} (tile size: {tileSize:F0})");

        // Initialize VFX system with grid container (delayed one frame so Canvas layout is calculated)
        if (GridVFX.Instance != null)
            StartCoroutine(DelayedVFXInit());
    }
    
    /// <summary>
    /// Wait one frame for Canvas layout to calculate grid container dimensions,
    /// then initialize VFX so ambient particles spawn in the correct area.
    /// </summary>
    private IEnumerator DelayedVFXInit()
    {
        yield return null; // Wait one frame for layout pass
        if (GridVFX.Instance != null && gridContainer != null)
            GridVFX.Instance.Initialize(gridContainer);
    }

    /// <summary>
    /// Update grid size based on difficulty settings.
    /// Uses GameManager fallback, defaults to 5x5 serialized values.
    /// </summary>
    private void UpdateGridSizeFromDifficulty()
    {
        // Try GameManager for difficulty-based grid size
        if (GameManager.Instance != null)
        {
            int newSize = GameManager.Instance.GetCurrentGridSize();
            if (newSize != gridWidth || newSize != gridHeight)
            {
                gridWidth = newSize;
                gridHeight = newSize;
                grid = new Tile[gridWidth, gridHeight];
            }
        }
        // Otherwise use serialized defaults (5x5)

        // Always recalculate sizes from container (handles both difficulty change and container resize)
        CalculateSizesFromContainer();
        Debug.Log($"<color=cyan>Grid size: {gridWidth}x{gridHeight}, tile size: {tileSize:F0}, scale: {scaleFactor:F2}</color>");
    }
    
    private Tile CreateTile(int gridX, int gridY, Vector2 position)
    {
        GameObject tileObj = Instantiate(tilePrefab, gridContainer);
        Tile tile = tileObj.GetComponent<Tile>();

        if (tile != null)
        {
            int value = tileWeightManager.GetWeightedRandomValue();
            tile.Initialize(value, gridX, gridY);
            tile.SetPosition(position);

            RectTransform rt = tile.GetRectTransform();
            rt.sizeDelta = new Vector2(tileSize, tileSize);
            // Font size handled by TextMeshPro auto-sizing in prefab
        }

        return tile;
    }
    

    private void HandleTileClicked(Tile tile)
    {
        if (isProcessing) return;
        if (isDragging) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsGameActive) return;
        
        // Reset hint timer on any interaction
        ResetHintTimer();
        
        if (selectedTile == null)
        {
            selectedTile = tile;
            tile.Select();
            AudioManager.Instance?.PlayTileSelect();
            Debug.Log($"Selected: {tile}");
        }
        else if (selectedTile == tile)
        {
            tile.Deselect();
            selectedTile = null;
            Debug.Log("Deselected");
        }
        else
        {
            if (!IsAdjacent(selectedTile, tile))
            {
                selectedTile.Deselect();
                selectedTile = tile;
                tile.Select();
                Debug.Log($"Not adjacent! Switched selection to: {tile}");
                return;
            }
            
            Tile firstTile = selectedTile;
            Tile secondTile = tile;
            selectedTile = null;
            firstTile.Deselect();
            secondTile.Deselect();
            StartCoroutine(AnimatedSwapCoroutine(firstTile, secondTile));
        }
    }
    
    private bool IsAdjacent(Tile a, Tile b)
    {
        int dx = Mathf.Abs(a.GridX - b.GridX);
        int dy = Mathf.Abs(a.GridY - b.GridY);
        return (dx == 1 && dy == 0) || (dx == 0 && dy == 1);
    }
    
    private void HandleTileSwiped(Tile tile, SwipeDirection direction)
    {
        if (isProcessing) return;
        if (isDragging) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsGameActive) return;
        
        // Reset hint timer on any interaction
        ResetHintTimer();
        
        int neighborX = tile.GridX;
        int neighborY = tile.GridY;
        
        switch (direction)
        {
            case SwipeDirection.Up: neighborY -= 1; break;
            case SwipeDirection.Down: neighborY += 1; break;
            case SwipeDirection.Left: neighborX -= 1; break;
            case SwipeDirection.Right: neighborX += 1; break;
        }
        
        if (neighborX < 0 || neighborX >= gridWidth || neighborY < 0 || neighborY >= gridHeight)
        {
            Debug.Log($"Swipe {direction} blocked - no tile in that direction");
            return;
        }
        
        Tile neighborTile = grid[neighborX, neighborY];
        if (neighborTile == null)
        {
            Debug.Log($"Swipe {direction} blocked - neighbor tile is null");
            return;
        }

        // Can't swap with a locked tile (MakeZen locked tiles are immovable)
        if (neighborTile.IsLocked)
        {
            Debug.Log($"Swipe {direction} blocked - neighbor tile is locked");
            return;
        }

        if (selectedTile != null)
        {
            selectedTile.Deselect();
            selectedTile = null;
        }
        
        Debug.Log($"Swipe swap: {tile} <-> {neighborTile}");
        StartCoroutine(AnimatedSwapCoroutine(tile, neighborTile));
    }
    
    private IEnumerator AnimatedSwapCoroutine(Tile tileA, Tile tileB)
    {
        isProcessing = true;
        ResetHintTimer();
        AudioManager.Instance?.PlaySwapSound();

        // MakeZen: track swapped tiles for merge position logic
        lastSwappedFirst = tileA;
        lastSwappedSecond = tileB;

        Vector2 posA = tileA.GetRectTransform().anchoredPosition;
        Vector2 posB = tileB.GetRectTransform().anchoredPosition;

        float elapsed = 0f;
        while (elapsed < tileSwapDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / tileSwapDuration);
            float smoothT = AnimationUtilities.EaseInOutCubic(t);

            // Shallow arc: tiles lift slightly as they cross paths
            float arcOffset = Mathf.Sin(t * Mathf.PI) * 8f;
            Vector2 arcA = Vector2.Lerp(posA, posB, smoothT) + new Vector2(0, arcOffset);
            Vector2 arcB = Vector2.Lerp(posB, posA, smoothT) + new Vector2(0, arcOffset);

            tileA.GetRectTransform().anchoredPosition = arcA;
            tileB.GetRectTransform().anchoredPosition = arcB;
            yield return null;
        }

        tileA.GetRectTransform().anchoredPosition = posB;
        tileB.GetRectTransform().anchoredPosition = posA;
        
        int axOld = tileA.GridX, ayOld = tileA.GridY;
        int bxOld = tileB.GridX, byOld = tileB.GridY;
        
        grid[axOld, ayOld] = tileB;
        grid[bxOld, byOld] = tileA;
        tileA.GridX = bxOld; tileA.GridY = byOld;
        tileB.GridX = axOld; tileB.GridY = ayOld;
        
        isProcessing = false;
        StartCoroutine(ProcessMatchesCoroutine());
    }

    // ==========================================
    // DRAG-SWAP SYSTEM
    // ==========================================

    /// <summary>
    /// Called when a tile drag begins (after activation threshold is met).
    /// </summary>
    private void HandleDragStarted(Tile tile)
    {
        if (isProcessing) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsGameActive) return;

        // Clear any click-selection
        if (selectedTile != null)
        {
            selectedTile.Deselect();
            selectedTile = null;
        }

        isDragging = true;
        draggedTile = tile;
        dragCurrentGridX = tile.GridX;
        dragCurrentGridY = tile.GridY;

        // Bring dragged tile to front so it renders above others
        tile.GetRectTransform().SetAsLastSibling();

        AudioManager.Instance?.PlayTileSelect();
        ResetHintTimer();

        Debug.Log($"Drag started: {tile}");
    }

    /// <summary>
    /// Called every frame during drag with the screen-space position.
    /// Moves the dragged tile and triggers swaps on cell boundary crossings.
    /// </summary>
    private void HandleDragMoved(Tile tile, Vector2 screenPos)
    {
        if (!isDragging || tile != draggedTile) return;

        // Convert screen position to canvas-local position
        Camera cam = null; // null works for Screen Space - Overlay canvas
        Canvas canvas = gridContainer.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cam = canvas.worldCamera;
        }

        Vector2 localPos;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            gridContainer, screenPos, cam, out localPos))
        {
            return; // Conversion failed
        }

        // Move the dragged tile to follow the finger/cursor
        tile.GetRectTransform().anchoredPosition = localPos;

        // Determine which grid cell the tile center is now over
        Vector2Int targetCell = GetGridCellAtPosition(localPos);
        int targetX = targetCell.x;
        int targetY = targetCell.y;

        // Check if we've crossed into a different cell
        if (targetX != dragCurrentGridX || targetY != dragCurrentGridY)
        {
            // Only swap with adjacent cells (cardinal directions)
            int dx = targetX - dragCurrentGridX;
            int dy = targetY - dragCurrentGridY;

            // If the target is more than 1 step away, step toward it one cell at a time
            if (Mathf.Abs(dx) + Mathf.Abs(dy) > 1)
            {
                // Prefer the axis with the larger delta
                if (Mathf.Abs(dx) >= Mathf.Abs(dy))
                {
                    targetX = dragCurrentGridX + (dx > 0 ? 1 : -1);
                    targetY = dragCurrentGridY;
                }
                else
                {
                    targetX = dragCurrentGridX;
                    targetY = dragCurrentGridY + (dy > 0 ? 1 : -1);
                }
            }

            // Validate bounds
            if (targetX >= 0 && targetX < gridWidth && targetY >= 0 && targetY < gridHeight)
            {
                PerformDragSwap(targetX, targetY);
            }
        }
    }

    /// <summary>
    /// Called when the drag ends (finger/mouse released).
    /// Snaps the tile to its grid position and triggers match processing.
    /// </summary>
    private void HandleDragEnded(Tile tile)
    {
        if (!isDragging || tile != draggedTile) return;

        // Snap the dragged tile to its final grid cell
        tile.SetPosition(GridToWorldPosition(dragCurrentGridX, dragCurrentGridY));

        Debug.Log($"Drag ended: {tile} at [{dragCurrentGridX},{dragCurrentGridY}]");

        // MakeZen: track the dragged tile as both first and second swap
        // (drag-swap doesn't have a clean "two tile" swap, but the dragged tile's
        // final position is the most natural merge point)
        lastSwappedFirst = tile;
        lastSwappedSecond = tile;

        isDragging = false;
        draggedTile = null;

        // Now process any matches created by the drag
        StartCoroutine(ProcessMatchesCoroutine());
    }

    /// <summary>
    /// Instantly swap the dragged tile's grid position with the tile at (targetX, targetY).
    /// The displaced tile animates to the vacated cell; the dragged tile stays under the finger.
    /// </summary>
    private void PerformDragSwap(int targetX, int targetY)
    {
        Tile displacedTile = grid[targetX, targetY];
        if (displacedTile == null) return;
        if (displacedTile.IsLocked) return; // Can't swap with a locked tile

        // Update grid array
        grid[dragCurrentGridX, dragCurrentGridY] = displacedTile;
        grid[targetX, targetY] = draggedTile;

        // Update GridX/GridY on both tiles
        displacedTile.GridX = dragCurrentGridX;
        displacedTile.GridY = dragCurrentGridY;
        draggedTile.GridX = targetX;
        draggedTile.GridY = targetY;

        // Animate the displaced tile sliding to the vacated cell
        Vector2 vacatedPos = GridToWorldPosition(dragCurrentGridX, dragCurrentGridY);
        StartCoroutine(SnapTileCoroutine(displacedTile, vacatedPos, 0.08f));

        // Update drag tracking position
        dragCurrentGridX = targetX;
        dragCurrentGridY = targetY;

        AudioManager.Instance?.PlaySwapSound();
        ResetHintTimer();
    }

    /// <summary>
    /// Quick smooth animation sliding a tile to a target position.
    /// Used for displaced tiles during drag-swap.
    /// </summary>
    private IEnumerator SnapTileCoroutine(Tile tile, Vector2 targetPos, float duration)
    {
        Vector2 startPos = tile.GetRectTransform().anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            tile.GetRectTransform().anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }

        tile.GetRectTransform().anchoredPosition = targetPos;
    }

    /// <summary>
    /// Convert a canvas-local position to grid cell coordinates.
    /// Reverse of GridToWorldPosition. Clamped to grid bounds.
    /// </summary>
    private Vector2Int GetGridCellAtPosition(Vector2 localPos)
    {
        float totalWidth = gridWidth * tileSize + (gridWidth - 1) * tileSpacing;
        float totalHeight = gridHeight * tileSize + (gridHeight - 1) * tileSpacing;
        float startX = -totalWidth / 2f + tileSize / 2f;
        float startY = totalHeight / 2f - tileSize / 2f;

        float cellStep = tileSize + tileSpacing;

        int gridX = Mathf.RoundToInt((localPos.x - startX) / cellStep);
        int gridY = Mathf.RoundToInt((startY - localPos.y) / cellStep);

        // Clamp to grid bounds
        gridX = Mathf.Clamp(gridX, 0, gridWidth - 1);
        gridY = Mathf.Clamp(gridY, 0, gridHeight - 1);

        return new Vector2Int(gridX, gridY);
    }

    // ==========================================
    // MAKEZEN: MERGE & GRAVITY SYSTEM
    // ==========================================

    /// <summary>
    /// Find the first matching line (row or column) on the grid.
    /// Returns null if no matches exist. Used for Zen mode's line-by-line processing.
    /// </summary>
    private ZenLineMatch? FindFirstZenMatch()
    {
        // Check rows first
        for (int y = 0; y < gridHeight; y++)
        {
            int sum = 0;
            bool allPresent = true;
            Tile[] lineTiles = new Tile[gridWidth];
            for (int x = 0; x < gridWidth; x++)
            {
                if (grid[x, y] == null) { allPresent = false; break; }
                lineTiles[x] = grid[x, y];
                sum += grid[x, y].Value;
            }
            if (allPresent && sum > 0 && sum % 10 == 0)
            {
                return new ZenLineMatch
                {
                    isRow = true,
                    lineIndex = y,
                    sum = sum,
                    tiles = lineTiles
                };
            }
        }

        // Check columns
        for (int x = 0; x < gridWidth; x++)
        {
            int sum = 0;
            bool allPresent = true;
            Tile[] lineTiles = new Tile[gridHeight];
            for (int y = 0; y < gridHeight; y++)
            {
                if (grid[x, y] == null) { allPresent = false; break; }
                lineTiles[y] = grid[x, y];
                sum += grid[x, y].Value;
            }
            if (allPresent && sum > 0 && sum % 10 == 0)
            {
                return new ZenLineMatch
                {
                    isRow = false,
                    lineIndex = x,
                    sum = sum,
                    tiles = lineTiles
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Determine where the locked tile should appear after a Zen match.
    /// Priority: second swapped tile position > first swapped > line center.
    /// Port of prototype's getMergePos().
    /// </summary>
    private int GetZenMergePosition(ZenLineMatch match)
    {
        // Prefer where the secondSwapped tile sits on this line
        if (lastSwappedSecond != null)
        {
            if (match.isRow && lastSwappedSecond.GridY == match.lineIndex)
                return lastSwappedSecond.GridX;
            if (!match.isRow && lastSwappedSecond.GridX == match.lineIndex)
                return lastSwappedSecond.GridY;
        }

        // Fallback: firstSwapped position on this line
        if (lastSwappedFirst != null)
        {
            if (match.isRow && lastSwappedFirst.GridY == match.lineIndex)
                return lastSwappedFirst.GridX;
            if (!match.isRow && lastSwappedFirst.GridX == match.lineIndex)
                return lastSwappedFirst.GridY;
        }

        // Final fallback: center of line
        return (match.isRow ? gridWidth : gridHeight) / 2;
    }

    /// <summary>
    /// Animate a single Zen match: beam flash, convergence to merge point,
    /// locked tile creation, other tile removal, scoring.
    /// </summary>
    private IEnumerator AnimateZenMatch(ZenLineMatch match, int cascadeCount)
    {
        int mergePos = GetZenMergePosition(match);
        match.mergeGridPos = mergePos;

        if (GameManager.Instance != null)
            GameManager.Instance.IsSolveAnimationPlaying = true;

        // Identify the merge tile and the tiles to remove
        Tile mergeTile;
        int mergeX, mergeY;
        if (match.isRow)
        {
            mergeX = mergePos;
            mergeY = match.lineIndex;
        }
        else
        {
            mergeX = match.lineIndex;
            mergeY = mergePos;
        }
        mergeTile = grid[mergeX, mergeY];

        // Build a single-line MatchResult for VFX (beam flash)
        MatchResult singleLineResult = new MatchResult();
        foreach (Tile t in match.tiles)
        {
            if (t != null) singleLineResult.allMatchedTiles.Add(t);
        }
        if (match.isRow)
        {
            singleLineResult.matchedRows.Add(match.lineIndex);
            singleLineResult.rowSums[match.lineIndex] = match.sum;
        }
        else
        {
            singleLineResult.matchedColumns.Add(match.lineIndex);
            singleLineResult.columnSums[match.lineIndex] = match.sum;
        }

        // Fire beam flash (non-blocking)
        if (GridVFX.Instance != null)
            StartCoroutine(GridVFX.Instance.PlayLineSweeps(singleLineResult, tileSize, tileSpacing));

        yield return new WaitForSeconds(0.08f);

        // Trigger avatar solve animation
        AvatarManager.Instance?.OnSolve();
        AudioManager.Instance?.PlayConvergenceSound();

        // Convergence: all non-merge tiles in the line slide toward the merge tile
        Vector2 mergeWorldPos = GridToWorldPosition(mergeX, mergeY);
        Dictionary<Tile, Vector2> originalPositions = new Dictionary<Tile, Vector2>();
        List<Tile> tilesToRemove = new List<Tile>();

        foreach (Tile t in match.tiles)
        {
            if (t == null) continue;
            originalPositions[t] = t.GetRectTransform().anchoredPosition;
            if (t != mergeTile)
                tilesToRemove.Add(t);
        }

        // Animate convergence (tiles slide + shrink toward merge position)
        float elapsed = 0f;
        while (elapsed < solveConvergeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / solveConvergeDuration;
            float easedT = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

            foreach (Tile tile in tilesToRemove)
            {
                if (tile == null || !originalPositions.ContainsKey(tile)) continue;

                RectTransform rt = tile.GetRectTransform();
                Vector2 startPos = originalPositions[tile];
                rt.anchoredPosition = Vector2.Lerp(startPos, mergeWorldPos, easedT);

                float scale = Mathf.Lerp(1f, convergeShrinkAmount, easedT);
                tile.transform.localScale = Vector3.one * scale;
                tile.transform.localEulerAngles = new Vector3(0, 0, easedT * 180f);

                // Fade tile background
                Image img = tile.GetComponent<Image>();
                if (img != null)
                    img.color = new Color(0.85f, 0.85f, 0.85f, 1f - easedT);
            }

            // Merge tile: subtle grow pulse during convergence
            if (mergeTile != null)
            {
                float pulseScale = 1f + Mathf.Sin(easedT * Mathf.PI) * 0.08f;
                mergeTile.transform.localScale = Vector3.one * pulseScale;
            }

            yield return null;
        }

        // === Convergence complete: create locked tile, remove others ===

        // Collect original tile values BEFORE modifying/destroying (for scoring)
        List<int> tileValues = new List<int>();
        foreach (Tile tile in match.tiles)
        {
            if (tile != null) tileValues.Add(tile.Value);
        }

        // SFX and chain tracking
        int consecutiveCount = gridValidation.RegisterMatch();
        AudioManager.Instance?.PlayTenPopSound(consecutiveCount);
        if (GridVFX.Instance != null)
        {
            GridVFX.Instance.TriggerShake(consecutiveCount);
            GridVFX.Instance.PulseAmbientParticles();
        }

        // Convert merge tile to locked (set value = line sum)
        if (mergeTile != null)
        {
            mergeTile.SetValue(match.sum);
            mergeTile.transform.localScale = Vector3.one;
            Debug.Log($"<color=cyan>[Zen]</color> Locked tile created: value {match.sum} at [{mergeX},{mergeY}]");
        }

        // Remove the other tiles from the grid
        foreach (Tile tile in tilesToRemove)
        {
            if (tile != null)
            {
                grid[tile.GridX, tile.GridY] = null;
                Destroy(tile.gameObject);
            }
        }

        // Show sum popup at merge position
        yield return StartCoroutine(ShowTenEffectSpectacular(mergeWorldPos, match.sum));

        // Report scoring to GameManager
        GameManager.Instance?.OnMatchCleared(
            match.tiles.Length,        // tilesCleared
            match.isRow ? 1 : 0,       // rowsMatched
            match.isRow ? 0 : 1,       // columnsMatched
            tileValues,
            singleLineResult,
            cascadeCount
        );

        if (GameManager.Instance != null)
            GameManager.Instance.IsSolveAnimationPlaying = false;
    }

    private IEnumerator ProcessMatchesCoroutine()
    {
        isProcessing = true;
        int cascadeCount = 0;
        bool isZenMode = GameManager.Instance != null
            && GameManager.Instance.CurrentMode == GameManager.GameMode.Zen;

        GameManager.Instance?.OnCascadeStart();

        while (true)
        {
            // Stop processing if game is no longer active (e.g. win/loss triggered)
            if (GameManager.Instance != null && !GameManager.Instance.IsGameActive)
            {
                Debug.Log("Game no longer active - halting cascade processing.");
                break;
            }

            if (matchChecker == null)
            {
                Debug.LogWarning("MatchChecker not assigned!");
                break;
            }

            // ──────────────────────────────────────────────
            // ZEN MODE: line-by-line merge processing
            // ──────────────────────────────────────────────
            if (isZenMode)
            {
                ZenLineMatch? zenMatch = FindFirstZenMatch();

                if (zenMatch == null)
                {
                    if (cascadeCount > 0)
                        Debug.Log($"<color=cyan>[Zen]</color> Cascade complete! {cascadeCount} chain(s)");
                    else
                    {
                        Debug.Log("<color=cyan>[Zen]</color> No matches found.");
                        GameManager.Instance?.OnFailedSwap();
                    }
                    break;
                }

                cascadeCount++;

                // Clear swap references for chain matches (cascades have no "player" position)
                if (cascadeCount > 1)
                {
                    lastSwappedFirst = null;
                    lastSwappedSecond = null;
                }

                ZenLineMatch match = zenMatch.Value;
                Debug.Log($"<color=cyan>[Zen] MATCH {cascadeCount}!</color> " +
                    $"{(match.isRow ? "Row" : "Col")} {match.lineIndex}, sum = {match.sum}");

                yield return StartCoroutine(AnimateZenMatch(match, cascadeCount));

                yield return StartCoroutine(DropTilesCoroutine());
                yield return StartCoroutine(SpawnNewTilesCoroutine());

                continue; // Check for chain matches
            }

            // ──────────────────────────────────────────────
            // ARCADE MODE: existing all-at-once processing
            // ──────────────────────────────────────────────
            MatchResult result = matchChecker.GetMatchResult();

            if (!result.HasMatches)
            {
                if (cascadeCount > 0)
                    Debug.Log($"Cascade complete! {cascadeCount} chain(s)");
                else
                {
                    Debug.Log("No matches found.");
                    // Zen mode: reset multiplier on failed swap (no match from player action)
                    GameManager.Instance?.OnFailedSwap();
                }
                break;
            }

            cascadeCount++;
            Debug.Log($"<color=yellow>MATCH {cascadeCount}!</color> " +
                    $"{result.matchedRows.Count} rows, {result.matchedColumns.Count} columns, " +
                    $"{result.TotalMatchedTiles} tiles");

            // Collect tile values before clearing for enhanced number bonuses
            System.Collections.Generic.List<int> tileValues = new System.Collections.Generic.List<int>();
            foreach (Tile tile in result.allMatchedTiles)
            {
                if (tile != null)
                    tileValues.Add(tile.Value);
            }

            yield return StartCoroutine(AnimateSolveSequence(result.allMatchedTiles, result));

            ClearMatchedTiles(result.allMatchedTiles);

            GameManager.Instance?.OnMatchCleared(
                result.TotalMatchedTiles,
                result.matchedRows.Count,
                result.matchedColumns.Count,
                tileValues,
                result,
                cascadeCount    // 1 = player swap, 2+ = cascade
            );

            yield return StartCoroutine(DropTilesCoroutine());
            yield return StartCoroutine(SpawnNewTilesCoroutine());
        }
        
        GameManager.Instance?.OnCascadeEnd();
        
        // Reset hint timer after cascade completes
        ResetHintTimer();
        
        if (matchChecker != null && !matchChecker.HasValidMoves())
        {
            // Zen mode: use a reshuffle if available, otherwise game over
            if (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.Zen)
            {
                if (GameManager.Instance.UseReshuffle())
                {
                    Debug.Log($"<color=cyan>ZEN RESHUFFLE!</color> {GameManager.Instance.ZenReshufflesRemaining} left.");
                    OnGridUnsolvable?.Invoke();
                    yield return new WaitForSeconds(unsolvableResetDelay);
                    ResetGridSilent();
                    yield break;
                }
                else
                {
                    Debug.Log("<color=red>ZEN GAME OVER!</color> No reshuffles remaining.");
                    GameManager.Instance.ZenGameOver();
                    yield break;
                }
            }
            else
            {
                // Arcade mode: always reshuffle
                Debug.Log("<color=red>GRID UNSOLVABLE!</color> No valid moves available. Resetting...");
                OnGridUnsolvable?.Invoke();
                yield return new WaitForSeconds(unsolvableResetDelay);
                ResetGridSilent();
                yield break;
            }
        }
        
        isProcessing = false;
        PrintGridState();
    }
    
    private void ResetGridSilent()
    {
        StartCoroutine(ResetGridWithEffect());
    }
    
    private IEnumerator ResetGridWithEffect()
    {
        bool isZenMode = GameManager.Instance != null
            && GameManager.Instance.CurrentMode == GameManager.GameMode.Zen;

        if (isZenMode)
        {
            // Zen reshuffle: only re-roll non-locked tiles, preserve locked tiles in place
            yield return StartCoroutine(ZenResetGridWithEffect());
            yield break;
        }

        // Arcade mode: full grid reset (original behavior)
        Debug.Log("<color=yellow>Grid reset with visual effect (no points awarded)</color>");

        List<Tile> allTiles = new List<Tile>();
        for (int y = 0; y < gridHeight; y++)
            for (int x = 0; x < gridWidth; x++)
                if (grid[x, y] != null)
                    allTiles.Add(grid[x, y]);

        float flashDuration = 0.3f;
        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.PingPong(elapsed * 8f, 1f);
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

        float fallDuration = 0.4f;
        elapsed = 0f;

        Dictionary<Tile, Vector2> originalPositions = new Dictionary<Tile, Vector2>();
        foreach (Tile tile in allTiles)
            if (tile != null)
                originalPositions[tile] = tile.GetRectTransform().anchoredPosition;

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
                    float shake = Mathf.Sin(elapsed * 50f) * 5f * scaleFactor * (1f - t);
                    float fallDistance = 800f * scaleFactor * t * t;

                    rt.anchoredPosition = originalPos + new Vector2(shake, -fallDistance);

                    Image img = tile.GetComponent<Image>();
                    if (img != null)
                    {
                        Color c = img.color;
                        c.a = 1f - t;
                        img.color = c;
                    }

                    tile.transform.localScale = Vector3.one * (1f - t * 0.3f);
                }
            }
            yield return null;
        }

        ClearGrid();
        SpawnGrid();
        StartCoroutine(ProcessMatchesCoroutine());
    }

    /// <summary>
    /// Zen-specific grid reshuffle: preserves locked tiles in place,
    /// re-rolls only free (non-locked) tiles. Attempts up to 500 times
    /// to generate a board with no initial matches and at least one valid move.
    /// Port of prototype's resetBoard().
    /// </summary>
    private IEnumerator ZenResetGridWithEffect()
    {
        Debug.Log("<color=cyan>[Zen]</color> Reshuffle — preserving locked tiles");

        // Collect non-locked tiles for flash animation
        List<Tile> freeTiles = new List<Tile>();
        for (int y = 0; y < gridHeight; y++)
            for (int x = 0; x < gridWidth; x++)
                if (grid[x, y] != null && !grid[x, y].IsLocked)
                    freeTiles.Add(grid[x, y]);

        // Flash only free tiles
        float flashDuration = 0.3f;
        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.PingPong(elapsed * 8f, 1f);
            Color flashColor = Color.Lerp(Color.white, new Color(0.3f, 0.8f, 1f), t); // Cyan flash for Zen

            foreach (Tile tile in freeTiles)
            {
                if (tile != null)
                {
                    Image img = tile.GetComponent<Image>();
                    if (img != null) img.color = flashColor;
                }
            }
            yield return null;
        }

        // Re-roll free tile values (try up to 500 times for a valid board)
        bool foundValid = false;
        for (int attempt = 0; attempt < 500; attempt++)
        {
            // Re-roll all non-locked tiles
            foreach (Tile tile in freeTiles)
            {
                if (tile != null && !tile.IsLocked)
                {
                    int newValue = tileWeightManager.GetWeightedRandomValue();
                    tile.SetValue(newValue);
                }
            }

            // Check: no initial matches AND has valid moves
            bool hasMatch = FindFirstZenMatch() != null;
            bool hasValidMoves = matchChecker.HasValidMoves();

            if (!hasMatch && hasValidMoves)
            {
                foundValid = true;
                Debug.Log($"<color=cyan>[Zen]</color> Valid reshuffle found on attempt {attempt + 1}");
                break;
            }
        }

        if (!foundValid)
            Debug.LogWarning("[Zen] Could not find ideal reshuffle in 500 attempts — using last result.");

        // Brief scale-bounce on free tiles to visually confirm the reshuffle
        foreach (Tile tile in freeTiles)
        {
            if (tile != null)
                StartCoroutine(AnimationUtilities.PunchScale(tile.transform, 1.15f, 0.12f));
        }

        yield return new WaitForSeconds(0.15f);

        // Resume match processing (shouldn't find matches, but safety check)
        StartCoroutine(ProcessMatchesCoroutine());
    }
    
    private IEnumerator AnimateSolveSequence(HashSet<Tile> tiles, MatchResult result)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.IsSolveAnimationPlaying = true;
        
        // Fire beam flash (non-blocking — animates on its own, overlaps with convergence)
        if (GridVFX.Instance != null)
            StartCoroutine(GridVFX.Instance.PlayLineSweeps(result, tileSize, tileSpacing));

        // Brief pause so the beam burst registers visually before convergence starts
        yield return new WaitForSeconds(0.08f);

        // Trigger avatar solve animation immediately when converge starts
        AvatarManager.Instance?.OnSolve();

        AudioManager.Instance?.PlayConvergenceSound();
        
        Vector2 centerPos = CalculateMatchCenter(tiles, result);
        
        Dictionary<Tile, Vector2> originalPositions = new Dictionary<Tile, Vector2>();
        Dictionary<Tile, Color> originalTextColors = new Dictionary<Tile, Color>();
        
        foreach (Tile tile in tiles)
        {
            if (tile != null)
            {
                originalPositions[tile] = tile.GetRectTransform().anchoredPosition;
                TMPro.TMP_Text numText = tile.GetComponentInChildren<TMPro.TMP_Text>();
                if (numText != null)
                    originalTextColors[tile] = numText.color;
            }
        }
        
        float elapsed = 0f;
        while (elapsed < solveConvergeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / solveConvergeDuration;
            float easedT = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
            
            foreach (Tile tile in tiles)
            {
                if (tile != null && originalPositions.ContainsKey(tile))
                {
                    RectTransform rt = tile.GetRectTransform();
                    Vector2 startPos = originalPositions[tile];
                    
                    float spiralAngle = easedT * Mathf.PI * 0.5f;
                    Vector2 toCenter = centerPos - startPos;
                    float dist = toCenter.magnitude * (1f - easedT);
                    Vector2 spiralOffset = new Vector2(
                        Mathf.Sin(spiralAngle) * dist * 0.1f,
                        Mathf.Cos(spiralAngle) * dist * 0.1f
                    );
                    
                    rt.anchoredPosition = Vector2.Lerp(startPos, centerPos, easedT) + spiralOffset * (1f - easedT);
                    
                    float scale = Mathf.Lerp(1f, convergeShrinkAmount, easedT);
                    tile.transform.localScale = Vector3.one * scale;
                    tile.transform.localEulerAngles = new Vector3(0, 0, easedT * 180f);
                    
                    Image img = tile.GetComponent<Image>();
                    if (img != null)
                        img.color = new Color(0.85f, 0.85f, 0.85f, 1f - easedT);
                    
                    TMPro.TMP_Text numText = tile.GetComponentInChildren<TMPro.TMP_Text>();
                    if (numText != null && originalTextColors.ContainsKey(tile))
                    {
                        Color originalColor = originalTextColors[tile];
                        Color brightenedColor = Color.Lerp(originalColor, Color.white, easedT);
                        float fadeStart = 0.4f;
                        float alphaT = Mathf.Clamp01((easedT - fadeStart) / (1f - fadeStart));
                        numText.color = new Color(brightenedColor.r, brightenedColor.g, brightenedColor.b, 1f - alphaT);
                    }
                }
            }
            yield return null;
        }
        
        // === Chain tracking, SFX, and shake — done ONCE regardless of match count ===
        int consecutiveCount = gridValidation.RegisterMatch();

        AudioManager.Instance?.PlayTenPopSound(consecutiveCount);
        if (GridVFX.Instance != null)
        {
            GridVFX.Instance.TriggerShake(consecutiveCount);
            GridVFX.Instance.PulseAmbientParticles();
        }

        // Spawn popup + explosion for matched lines, with per-line sums
        List<(Vector2 center, int sum)> lineData = GetPerLineCentersWithSums(result);
        if (lineData.Count <= 1)
        {
            // Single match — full "10" popup animation at convergence center
            int singleSum = lineData.Count > 0 ? lineData[0].sum : 10;
            yield return StartCoroutine(ShowTenEffectSpectacular(centerPos, singleSum));
        }
        else
        {
            // Multiple simultaneous matches — skip individual popups, go straight to combo merge.
            // PlayComboMerge handles everything: spawning numbers, flying them inward,
            // showing the combined total, and particle explosions — all within ~0.55s
            yield return StartCoroutine(PlayComboMerge(lineData));
        }

        if (GameManager.Instance != null)
            GameManager.Instance.IsSolveAnimationPlaying = false;
    }
    
    private Vector2 CalculateMatchCenter(HashSet<Tile> tiles, MatchResult result)
    {
        // For single-line matches, center on that line
        if (result.TotalLines == 1)
        {
            if (result.matchedRows.Count == 1)
            {
                int row = result.matchedRows[0];
                int midX = gridWidth / 2;
                return GridToWorldPosition(midX, row);
            }
            else if (result.matchedColumns.Count == 1)
            {
                int col = result.matchedColumns[0];
                int midY = gridHeight / 2;
                return GridToWorldPosition(col, midY);
            }
        }

        // For multiple simultaneous matches, center on all matched tiles
        Vector2 sum = Vector2.zero;
        int count = 0;
        foreach (Tile tile in tiles)
        {
            if (tile != null)
            {
                sum += tile.GetRectTransform().anchoredPosition;
                count++;
            }
        }
        return count > 0 ? sum / count : Vector2.zero;
    }

    /// <summary>
    /// Get a separate center position for each matched row and column.
    /// Used to spawn one "10" popup per match line.
    /// </summary>
    private List<Vector2> GetPerLineCenters(MatchResult result)
    {
        List<Vector2> centers = new List<Vector2>();
        int midX = gridWidth / 2;
        int midY = gridHeight / 2;

        foreach (int row in result.matchedRows)
            centers.Add(GridToWorldPosition(midX, row));

        foreach (int col in result.matchedColumns)
            centers.Add(GridToWorldPosition(col, midY));

        return centers;
    }

    /// <summary>
    /// Get center positions AND sums for each matched line (rows first, then columns).
    /// </summary>
    private List<(Vector2 center, int sum)> GetPerLineCentersWithSums(MatchResult result)
    {
        List<(Vector2, int)> data = new List<(Vector2, int)>();
        int midX = gridWidth / 2;
        int midY = gridHeight / 2;

        foreach (int row in result.matchedRows)
            data.Add((GridToWorldPosition(midX, row), result.rowSums.ContainsKey(row) ? result.rowSums[row] : 10));

        foreach (int col in result.matchedColumns)
            data.Add((GridToWorldPosition(col, midY), result.columnSums.ContainsKey(col) ? result.columnSums[col] : 10));

        return data;
    }
    
    private IEnumerator ShowTenEffectSpectacular(Vector2 position, int lineSum = 10)
    {
        // Chain tracking, SFX, and shake are handled in AnimateSolveSequence (once per cascade).
        // This method now only handles the visual popup + particle explosion per position.

        // Calculate scale based on consecutive matches + boost for higher sums
        float tenScale = gridValidation.GetTenScale(lineSum);
        Debug.Log($"<color=yellow>Match sum: {lineSum}, Consecutive: {gridValidation.ConsecutiveCount}, Scale: {tenScale:F2}</color>");

        // Trigger particle explosion VFX immediately with the 10 text
        float currentMultiplier = GameManager.Instance?.CurrentMultiplier ?? 1f;
        TenExplosionVFX.Instance?.TriggerExplosion(position, currentMultiplier, gridContainer);

        List<GameObject> effectObjects = new List<GameObject>();

        GameObject tenObj = new GameObject("TenEffect_Main");
        tenObj.transform.SetParent(gridContainer, false);
        effectObjects.Add(tenObj);

        RectTransform tenRT = tenObj.AddComponent<RectTransform>();
        tenRT.anchoredPosition = position;
        tenRT.sizeDelta = new Vector2(280f * scaleFactor * tenScale, 170f * scaleFactor * tenScale);

        TMPro.TMP_Text tenText = tenObj.AddComponent<TMPro.TextMeshProUGUI>();
        tenText.text = lineSum.ToString();
        tenText.fontSize = 120 * scaleFactor * tenScale;
        tenText.fontStyle = TMPro.FontStyles.Bold;
        tenText.alignment = TMPro.TextAlignmentOptions.Center;

        // Color tinting based on sum value
        Color sumColor = GetSumColor(lineSum);
        tenText.color = sumColor;
        tenText.enableVertexGradient = true;
        Color sumColorLight = Color.Lerp(sumColor, Color.white, 0.5f);
        tenText.colorGradient = new TMPro.VertexGradient(
            sumColorLight,
            sumColorLight,
            sumColor,
            sumColor
        );

        GameObject glowObj = new GameObject("TenEffect_Glow");
        glowObj.transform.SetParent(gridContainer, false);
        glowObj.transform.SetSiblingIndex(tenObj.transform.GetSiblingIndex());
        effectObjects.Add(glowObj);

        RectTransform glowRT = glowObj.AddComponent<RectTransform>();
        glowRT.anchoredPosition = position;
        glowRT.sizeDelta = new Vector2(280f * scaleFactor * tenScale, 170f * scaleFactor * tenScale);

        TMPro.TMP_Text glowText = glowObj.AddComponent<TMPro.TextMeshProUGUI>();
        glowText.text = lineSum.ToString();
        glowText.fontSize = 130 * scaleFactor * tenScale;
        glowText.fontStyle = TMPro.FontStyles.Bold;
        Color glowColor = GetSumColor(lineSum);
        glowText.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.4f);
        glowText.alignment = TMPro.TextAlignmentOptions.Center;
        
        List<(RectTransform rt, Image img, Vector2 velocity, float rotSpeed)> sparkles = 
            new List<(RectTransform, Image, Vector2, float)>();
        
        for (int i = 0; i < sparkleCount; i++)
        {
            GameObject sparkle = new GameObject($"Sparkle_{i}");
            sparkle.transform.SetParent(gridContainer, false);
            effectObjects.Add(sparkle);
            
            RectTransform sRT = sparkle.AddComponent<RectTransform>();
            sRT.anchoredPosition = position;
            float size = Random.Range(8f, 16f) * scaleFactor;
            sRT.sizeDelta = new Vector2(size, size);
            sRT.localEulerAngles = new Vector3(0, 0, 45f);

            Image sImg = sparkle.AddComponent<Image>();
            sImg.color = sparkleColor;
            sImg.raycastTarget = false;

            float angle = (i / (float)sparkleCount) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
            float speed = Random.Range(150f, 300f) * scaleFactor;
            Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            float rotSpd = Random.Range(-360f, 360f);
            
            sparkles.Add((sRT, sImg, vel, rotSpd));
        }
        
        List<(RectTransform rt, Image img, float delay)> rings = 
            new List<(RectTransform, Image, float)>();
        
        for (int i = 0; i < burstRingCount; i++)
        {
            GameObject ring = new GameObject($"Ring_{i}");
            ring.transform.SetParent(gridContainer, false);
            ring.transform.SetSiblingIndex(0);
            effectObjects.Add(ring);
            
            RectTransform rRT = ring.AddComponent<RectTransform>();
            rRT.anchoredPosition = position;
            rRT.sizeDelta = new Vector2(20f * scaleFactor, 20f * scaleFactor);
            
            Image rImg = ring.AddComponent<Image>();
            rImg.color = new Color(tenGlowColor.r, tenGlowColor.g, tenGlowColor.b, 0.6f);
            rImg.raycastTarget = false;
            
            rings.Add((rRT, rImg, i * 0.08f));
        }
        
        tenObj.transform.localScale = Vector3.zero;
        glowObj.transform.localScale = Vector3.zero;
        
        float popDuration = 0.12f;
        float elapsed = 0f;
        
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / popDuration;
            float overshoot = 1f + Mathf.Sin(t * Mathf.PI) * 0.3f;
            float scale = Mathf.Lerp(0f, 1f, t) * overshoot;
            
            tenObj.transform.localScale = Vector3.one * scale;
            glowObj.transform.localScale = Vector3.one * scale * 1.3f;
            
            yield return null;
        }
        
        tenObj.transform.localScale = Vector3.one;
        glowObj.transform.localScale = Vector3.one * 1.2f;
        
        float mainDuration = solveShowTenDuration;
        elapsed = 0f;
        Vector2 startPos = position;
        Color startColor = tenText.color;
        Color startGlowColor = glowText.color;
        
        while (elapsed < mainDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / mainDuration;
            
            float floatY = Mathf.Sin(t * Mathf.PI) * 40f * scaleFactor;
            float pulse = 1f + Mathf.Sin(elapsed * 15f) * 0.08f;
            
            tenRT.anchoredPosition = startPos + new Vector2(0, floatY);
            tenObj.transform.localScale = Vector3.one * pulse;
            
            glowRT.anchoredPosition = startPos + new Vector2(0, floatY);
            float glowPulse = 1.2f + Mathf.Sin(elapsed * 12f) * 0.15f;
            glowObj.transform.localScale = Vector3.one * glowPulse;
            
            float glowAlpha = t < 0.3f 
                ? Mathf.Lerp(0.4f, 0.7f, t / 0.3f) 
                : Mathf.Lerp(0.7f, 0f, (t - 0.3f) / 0.7f);
            glowText.color = new Color(startGlowColor.r, startGlowColor.g, startGlowColor.b, glowAlpha);
            
            float textAlpha = t < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f);
            tenText.color = new Color(startColor.r, startColor.g, startColor.b, textAlpha);
            
            foreach (var (sRT, sImg, vel, rotSpd) in sparkles)
            {
                if (sRT == null) continue;
                
                Vector2 currentPos = sRT.anchoredPosition;
                Vector2 gravity = new Vector2(0, -200f * scaleFactor) * Time.deltaTime;
                sRT.anchoredPosition = currentPos + vel * Time.deltaTime + gravity;
                
                float currentRot = sRT.localEulerAngles.z;
                sRT.localEulerAngles = new Vector3(0, 0, currentRot + rotSpd * Time.deltaTime);
                
                float sparkleAlpha = 1f - t;
                float sparkleScale = Mathf.Lerp(1f, 0.3f, t);
                sRT.localScale = Vector3.one * sparkleScale;
                sImg.color = new Color(sparkleColor.r, sparkleColor.g, sparkleColor.b, sparkleAlpha);
            }
            
            foreach (var (rRT, rImg, delay) in rings)
            {
                if (rRT == null) continue;
                
                float ringT = Mathf.Clamp01((elapsed - delay) / (mainDuration * 0.6f));
                if (ringT > 0)
                {
                    float ringSize = Mathf.Lerp(20f * scaleFactor, 200f * scaleFactor, ringT);
                    rRT.sizeDelta = new Vector2(ringSize, ringSize);
                    
                    float ringAlpha = Mathf.Lerp(0.6f, 0f, ringT);
                    rImg.color = new Color(rImg.color.r, rImg.color.g, rImg.color.b, ringAlpha);
                }
            }
            
            yield return null;
        }
        
        foreach (GameObject obj in effectObjects)
        {
            if (obj != null)
                Destroy(obj);
        }
    }

    /// <summary>
    /// Get a color for a line sum value (used for popup text tinting).
    /// </summary>
    private Color GetSumColor(int sum)
    {
        return sum switch
        {
            10 => new Color(1f, 0.9f, 0.3f, 1f),     // Gold (classic)
            20 => new Color(1f, 0.6f, 0.15f, 1f),     // Orange
            30 => new Color(0.85f, 0.3f, 0.85f, 1f),  // Purple
            40 => new Color(1f, 0.2f, 0.2f, 1f),      // Red
            _ => new Color(1f, 1f, 0.8f, 1f),          // White-gold fallback
        };
    }

    /// <summary>
    /// Get a color for the combo merged number based on line count.
    /// </summary>
    private Color GetComboColor(int comboCount)
    {
        return comboCount switch
        {
            2 => new Color(1f, 0.75f, 0.15f, 1f),     // Bright orange-gold
            3 => new Color(0.9f, 0.35f, 0.9f, 1f),    // Purple
            4 => new Color(1f, 0.25f, 0.25f, 1f),     // Red
            _ => new Color(1f, 0.1f, 0.1f, 1f),       // Deep red (5+ lines)
        };
    }

    /// <summary>
    /// Combo merge animation: individual line numbers fly inward and combine into a merged total.
    /// For 5+ line combos, displays "1000" with ultra effects.
    /// </summary>
    /// <summary>
    /// Self-contained combo merge: spawns individual numbers at line centers, flies them inward,
    /// shows the merged total, and fires particle explosions. Total duration ~0.55s (matching single match).
    /// For 5+ line ultra combos, timing is slightly extended with extra VFX.
    /// </summary>
    private IEnumerator PlayComboMerge(List<(Vector2 center, int sum)> lineData)
    {
        int lineCount = lineData.Count;
        int totalSum = 0;
        foreach (var (_, sum) in lineData)
            totalSum += sum;

        bool isUltraCombo = lineCount >= 5;
        string displayText = isUltraCombo ? "1000" : totalSum.ToString();

        // Play combo sound immediately
        if (isUltraCombo)
            AudioManager.Instance?.PlayUltraComboSound();
        else
            AudioManager.Instance?.PlayComboSound();

        // Fire particle explosions at each line center (replaces ShowTenEffectSpectacular's explosions)
        float currentMultiplier = GameManager.Instance?.CurrentMultiplier ?? 1f;
        foreach (var (center, _) in lineData)
            TenExplosionVFX.Instance?.TriggerExplosion(center, currentMultiplier, gridContainer);

        // Create number text at each line center (instantly visible, no pop-in delay)
        List<GameObject> mergeObjects = new List<GameObject>();
        List<(RectTransform rt, Vector2 startPos)> mergeItems = new List<(RectTransform, Vector2)>();

        for (int i = 0; i < lineData.Count; i++)
        {
            var (center, sum) = lineData[i];
            GameObject numObj = new GameObject($"ComboNumber_{i}");
            numObj.transform.SetParent(gridContainer, false);
            mergeObjects.Add(numObj);

            RectTransform rt = numObj.AddComponent<RectTransform>();
            rt.anchoredPosition = center;
            rt.sizeDelta = new Vector2(200f * scaleFactor, 120f * scaleFactor);

            TMPro.TMP_Text txt = numObj.AddComponent<TMPro.TextMeshProUGUI>();
            txt.text = sum.ToString();
            txt.fontSize = 90 * scaleFactor;
            txt.fontStyle = TMPro.FontStyles.Bold;
            txt.color = GetSumColor(sum);
            txt.alignment = TMPro.TextAlignmentOptions.Center;

            mergeItems.Add((rt, center));
        }

        // === Phase 1: Numbers fly toward grid center (0.2s) ===
        Vector2 gridCenter = Vector2.zero;
        float mergeDuration = 0.2f;
        float mergeElapsed = 0f;

        while (mergeElapsed < mergeDuration)
        {
            mergeElapsed += Time.deltaTime;
            float t = mergeElapsed / mergeDuration;
            float easeT = t * t; // Ease-in (accelerating)

            for (int i = 0; i < mergeItems.Count; i++)
            {
                if (mergeObjects[i] == null) continue;
                var (rt, startPos) = mergeItems[i];
                rt.anchoredPosition = Vector2.Lerp(startPos, gridCenter, easeT);

                float scale = Mathf.Lerp(1f, 0.3f, easeT);
                mergeObjects[i].transform.localScale = Vector3.one * scale;

                TMPro.TMP_Text txt = mergeObjects[i].GetComponent<TMPro.TMP_Text>();
                if (txt != null)
                {
                    Color c = txt.color;
                    c.a = Mathf.Lerp(1f, 0f, easeT);
                    txt.color = c;
                }
            }
            yield return null;
        }

        foreach (GameObject obj in mergeObjects)
            if (obj != null) Destroy(obj);

        // === Phase 2: Merged number pop-in at center (0.1s) ===
        GameObject finalObj = new GameObject("ComboMergedNumber");
        finalObj.transform.SetParent(gridContainer, false);

        RectTransform finalRT = finalObj.AddComponent<RectTransform>();
        finalRT.anchoredPosition = gridCenter;
        float finalWidth = isUltraCombo ? 400f : 280f;
        finalRT.sizeDelta = new Vector2(finalWidth * scaleFactor, 170f * scaleFactor);

        TMPro.TMP_Text finalTxt = finalObj.AddComponent<TMPro.TextMeshProUGUI>();
        finalTxt.text = displayText;
        float finalFontSize = isUltraCombo ? 160f : 130f;
        finalTxt.fontSize = finalFontSize * scaleFactor;
        finalTxt.fontStyle = TMPro.FontStyles.Bold;
        finalTxt.alignment = TMPro.TextAlignmentOptions.Center;

        Color comboColor = isUltraCombo ? new Color(1f, 0.15f, 0.15f, 1f) : GetComboColor(lineCount);
        finalTxt.color = comboColor;
        finalTxt.enableVertexGradient = true;
        Color comboLight = Color.Lerp(comboColor, Color.white, 0.5f);
        finalTxt.colorGradient = new TMPro.VertexGradient(comboLight, comboLight, comboColor, comboColor);

        // Glow behind merged number
        GameObject finalGlow = new GameObject("ComboMergedGlow");
        finalGlow.transform.SetParent(gridContainer, false);
        finalGlow.transform.SetSiblingIndex(finalObj.transform.GetSiblingIndex());

        RectTransform glowRT = finalGlow.AddComponent<RectTransform>();
        glowRT.anchoredPosition = gridCenter;
        glowRT.sizeDelta = new Vector2(finalWidth * scaleFactor, 170f * scaleFactor);

        TMPro.TMP_Text glowTxt = finalGlow.AddComponent<TMPro.TextMeshProUGUI>();
        glowTxt.text = displayText;
        glowTxt.fontSize = (finalFontSize + 10f) * scaleFactor;
        glowTxt.fontStyle = TMPro.FontStyles.Bold;
        glowTxt.color = new Color(comboColor.r, comboColor.g, comboColor.b, 0.35f);
        glowTxt.alignment = TMPro.TextAlignmentOptions.Center;

        // Shake + center explosion on merge impact
        if (isUltraCombo)
        {
            if (GridVFX.Instance != null)
            {
                GridVFX.Instance.TriggerShake(10);
                GridVFX.Instance.PulseAmbientParticles();
            }
            TenExplosionVFX.Instance?.TriggerExplosion(gridCenter, 5f, gridContainer);
            TenExplosionVFX.Instance?.TriggerExplosion(gridCenter + new Vector2(50f, 30f), 5f, gridContainer);
            TenExplosionVFX.Instance?.TriggerExplosion(gridCenter + new Vector2(-50f, -30f), 5f, gridContainer);
        }
        else
        {
            if (GridVFX.Instance != null)
                GridVFX.Instance.TriggerShake(lineCount + 1);
            TenExplosionVFX.Instance?.TriggerExplosion(gridCenter, 2f, gridContainer);
        }

        // Quick pop-in with overshoot (0.1s)
        finalObj.transform.localScale = Vector3.zero;
        finalGlow.transform.localScale = Vector3.zero;
        float popDuration = 0.1f;
        float popElapsed = 0f;

        while (popElapsed < popDuration)
        {
            popElapsed += Time.deltaTime;
            float t = popElapsed / popDuration;
            float overshoot = Mathf.Lerp(0f, 1f, t) * (1f + Mathf.Sin(t * Mathf.PI) * 0.5f);
            finalObj.transform.localScale = Vector3.one * overshoot;
            finalGlow.transform.localScale = Vector3.one * overshoot * 1.2f;
            yield return null;
        }
        finalObj.transform.localScale = Vector3.one;
        finalGlow.transform.localScale = Vector3.one * 1.2f;

        // === Phase 3: Brief hold + fade (0.25s total, or 0.45s for ultra) ===
        float holdAndFadeDuration = isUltraCombo ? 0.45f : 0.25f;
        float hfElapsed = 0f;
        Vector2 holdStart = gridCenter;
        float fadeStartT = 0.5f; // Fade begins at 50% of hold duration

        while (hfElapsed < holdAndFadeDuration)
        {
            hfElapsed += Time.deltaTime;
            float t = hfElapsed / holdAndFadeDuration;

            // Float upward gently
            finalRT.anchoredPosition = holdStart + new Vector2(0, Mathf.Sin(t * Mathf.PI) * 15f * scaleFactor);
            float pulse = 1f + Mathf.Sin(hfElapsed * 14f) * 0.04f;
            finalObj.transform.localScale = Vector3.one * pulse;

            // Glow pulse + fade
            float glowPulse = 1.2f + Mathf.Sin(hfElapsed * 11f) * 0.08f;
            finalGlow.transform.localScale = Vector3.one * glowPulse;
            float glowAlpha = Mathf.Lerp(0.35f, 0f, t);
            glowTxt.color = new Color(comboColor.r, comboColor.g, comboColor.b, glowAlpha);

            // Text fades out in second half
            if (t > fadeStartT)
            {
                float fadeT = (t - fadeStartT) / (1f - fadeStartT);
                Color c = finalTxt.color;
                c.a = Mathf.Lerp(1f, 0f, fadeT);
                finalTxt.color = c;
                finalObj.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.2f, fadeT);
            }

            yield return null;
        }

        Destroy(finalObj);
        Destroy(finalGlow);
    }

    private void ClearMatchedTiles(HashSet<Tile> tiles)
    {
        foreach (Tile tile in tiles)
        {
            if (tile != null)
            {
                grid[tile.GridX, tile.GridY] = null;
                Destroy(tile.gameObject);
            }
        }
        Debug.Log($"Cleared {tiles.Count} tiles");
    }
    
    /// <summary>
    /// Single-pass drop: calculate ALL final positions at once, animate ALL in parallel.
    /// No more multi-iteration waiting — every tile finds its final slot immediately.
    /// </summary>
    private IEnumerator DropTilesCoroutine()
    {
        float longestDuration = 0f;

        // For each column, compact all tiles downward in one pass
        for (int x = 0; x < gridWidth; x++)
        {
            int writeY = gridHeight - 1; // Bottom-most empty slot to fill

            // Scan from bottom to top, packing tiles down
            for (int readY = gridHeight - 1; readY >= 0; readY--)
            {
                if (grid[x, readY] != null)
                {
                    if (readY != writeY)
                    {
                        Tile tileToMove = grid[x, readY];
                        grid[x, writeY] = tileToMove;
                        grid[x, readY] = null;
                        tileToMove.GridY = writeY;

                        Vector2 targetPos = GridToWorldPosition(x, writeY);
                        float distance = Vector2.Distance(tileToMove.GetRectTransform().anchoredPosition, targetPos);
                        float duration = Mathf.Clamp(0.25f * (distance / 200f), 0.12f, 0.35f);
                        longestDuration = Mathf.Max(longestDuration, duration);

                        StartCoroutine(AnimateTileFall(tileToMove, targetPos));
                    }
                    writeY--;
                }
            }
        }

        // Wait for the longest fall to finish (capped at 0.35s + elastic bounce time)
        if (longestDuration > 0f)
            yield return new WaitForSeconds(longestDuration + 0.08f);
    }

    /// <summary>
    /// Animate a single tile falling to target position.
    /// Time-based (0.25s) with EaseOutCubic for a smooth deceleration into landing.
    /// </summary>
    private IEnumerator AnimateTileFall(Tile tile, Vector2 targetPosition)
    {
        if (tile == null) yield break;

        RectTransform rt = tile.GetRectTransform();
        Vector2 startPos = rt.anchoredPosition;
        // Time-based duration: 0.25s baseline, slightly longer for big falls, capped at 0.35s
        float distance = Vector2.Distance(startPos, targetPosition);
        float duration = Mathf.Clamp(0.25f * (distance / 200f), 0.12f, 0.35f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = AnimationUtilities.EaseOutCubic(t);
            rt.anchoredPosition = Vector2.Lerp(startPos, targetPosition, easedT);
            yield return null;
        }

        rt.anchoredPosition = targetPosition;
        // Fire-and-forget bounce — doesn't block the pipeline
        StartCoroutine(TileLandBounce(tile));
    }

    /// <summary>
    /// Landing bounce — ~5px overshoot with EaseOutElastic over 0.08s.
    /// Pairs with SpawnLandSparkle for visual feedback.
    /// </summary>
    private IEnumerator TileLandBounce(Tile tile)
    {
        if (tile == null) yield break;

        Transform tr = tile.transform;

        // Sparkle on land
        GridVFX.Instance?.SpawnLandSparkle(tile.GetRectTransform().anchoredPosition, tileSize);

        // Elastic squash-stretch bounce over 0.08s
        float bounceDuration = 0.08f;
        float elapsed = 0f;
        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bounceDuration);
            float eased = AnimationUtilities.EaseOutElastic(t);
            // Squash on impact (wide+short), then spring back to normal
            float scaleX = Mathf.LerpUnclamped(1.1f, 1f, eased);
            float scaleY = Mathf.LerpUnclamped(0.9f, 1f, eased);
            tr.localScale = new Vector3(scaleX, scaleY, 1f);
            yield return null;
        }
        tr.localScale = Vector3.one;
    }

    /// <summary>
    /// Spawn new tiles one by one with a small stagger delay between each.
    /// Tiles spawn just above the grid and drop into place. The stagger gives a
    /// satisfying "filling in" cascade rather than everything popping in at once.
    /// </summary>
    private IEnumerator SpawnNewTilesCoroutine()
    {
        // Collect all empty slots, ordered bottom-row-first so tiles fill from the bottom up
        List<(int x, int y)> emptySlots = new List<(int, int)>();

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = gridHeight - 1; y >= 0; y--)
            {
                if (grid[x, y] == null)
                    emptySlots.Add((x, y));
            }
        }

        if (emptySlots.Count == 0) yield break;

        // Sort: bottom rows first, then left to right (natural fill order)
        emptySlots.Sort((a, b) => {
            if (a.y != b.y) return b.y.CompareTo(a.y);
            return a.x.CompareTo(b.x);
        });

        float lastFallDuration = 0f;

        for (int i = 0; i < emptySlots.Count; i++)
        {
            var (x, y) = emptySlots[i];

            // Spawn just above the grid
            Vector2 spawnPos = GridToWorldPosition(x, -1);
            GameObject tileObj = Instantiate(tilePrefab, gridContainer);
            Tile newTile = tileObj.GetComponent<Tile>();

            if (newTile != null)
            {
                int value = tileWeightManager.GetWeightedRandomValue();

                // Light anti-cascade: one re-roll if this tile would complete a match
                if (gridValidation.WouldTileCompleteMatch(grid, gridWidth, gridHeight, x, y, value))
                {
                    value = tileWeightManager.GetWeightedRandomValue();  // Single re-roll, keep result either way
                }

                newTile.Initialize(value, x, y);
                newTile.SetPosition(spawnPos);
                newTile.GetRectTransform().sizeDelta = new Vector2(tileSize, tileSize);

                grid[x, y] = newTile;

                Vector2 targetPos = GridToWorldPosition(x, y);
                float distance = Vector2.Distance(spawnPos, targetPos);
                lastFallDuration = Mathf.Min(distance / tileFallSpeed, 0.4f);

                StartCoroutine(AnimateTileFall(newTile, targetPos));
            }

            // Small stagger between each tile appearing (0.04s feels like a quick cascade)
            yield return new WaitForSeconds(0.04f);
        }

        // Wait for the last tile to finish falling + bounce
        yield return new WaitForSeconds(lastFallDuration + 0.06f);

        Debug.Log($"New tiles spawned: {emptySlots.Count}");
    }
    
    public Tile GetTile(int x, int y)
    {
        if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight)
            return grid[x, y];
        return null;
    }
    
    public Tile[,] GetGrid() => grid;
    public Vector2Int GetGridSize() => new Vector2Int(gridWidth, gridHeight);
    
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
    
    public void ClearGrid()
    {
        ClearHintParticles();
        
        if (grid != null)
        {
            for (int y = 0; y < gridHeight; y++)
                for (int x = 0; x < gridWidth; x++)
                    if (grid[x, y] != null)
                    {
                        Destroy(grid[x, y].gameObject);
                        grid[x, y] = null;
                    }
        }
        selectedTile = null;
    }
    
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
    
    [ContextMenu("Check Sums")]
    public void DebugCheckSums()
    {
        for (int y = 0; y < gridHeight; y++)
        {
            int sum = 0;
            for (int x = 0; x < gridWidth; x++)
                sum += grid[x, y].Value;
            Debug.Log($"Row {y} sum: {sum}" + (sum == 10 ? " ← MATCH!" : ""));
        }
        
        for (int x = 0; x < gridWidth; x++)
        {
            int sum = 0;
            for (int y = 0; y < gridHeight; y++)
                sum += grid[x, y].Value;
            Debug.Log($"Column {x} sum: {sum}" + (sum == 10 ? " ← MATCH!" : ""));
        }
    }
    
    [ContextMenu("Force Show Hint")]
    public void DebugForceHint()
    {
        ShowHint();
    }

    [ContextMenu("Debug: Set Corner Tiles to Locked (10, 20, 30)")]
    public void DebugSetLockedTiles()
    {
        if (grid[0, 0] != null) { grid[0, 0].SetValue(10); Debug.Log("Tile [0,0] → 10 (Gold locked)"); }
        if (grid[1, 0] != null) { grid[1, 0].SetValue(20); Debug.Log("Tile [1,0] → 20 (Purple locked)"); }
        if (grid[2, 0] != null) { grid[2, 0].SetValue(30); Debug.Log("Tile [2,0] → 30 (Teal locked)"); }
        Debug.Log("Locked tile test: try clicking/swiping the first 3 tiles in top row — should be unresponsive.");
    }

    public void ResetGame()
    {
        Debug.Log("GridManager.ResetGame() called - full reset with match processing");

        if (gridContainer == null)
        {
            Debug.LogError("GridManager: gridContainer is not assigned!");
            return;
        }

        // Reset consecutive 10s tracking and tile bag
        gridValidation.ResetConsecutive();
        tileWeightManager.ClearBag();

        ClearGrid();
        SpawnGrid();
        StartCoroutine(ProcessMatchesCoroutine());
    }

    public void SpawnGridOnly()
    {
        Debug.Log("GridManager.SpawnGridOnly() called - grid visible, no match processing yet");

        if (gridContainer == null)
        {
            Debug.LogError("GridManager: gridContainer is not assigned!");
            return;
        }

        // Reset consecutive 10s tracking and tile bag
        gridValidation.ResetConsecutive();
        tileWeightManager.ClearBag();

        ClearGrid();
        SpawnGrid();
    }
    
    public void StartMatchProcessing()
    {
        Debug.Log("GridManager.StartMatchProcessing() called - let the freebies flow!");
        ResetHintTimer();
        StartCoroutine(ProcessMatchesCoroutine());
    }
}
