using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages the grid of tiles, handles spawning, swapping, and grid operations.
/// Grid size is dynamically set based on difficulty.
/// </summary>
public class GridManager : MonoBehaviour {
  [Header("Grid Settings"), SerializeField] 
  private int gridWidth = 5;

  [SerializeField] private int gridHeight = 5;
  [SerializeField] private float baseTileSpacing = 10f; // Base spacing at reference size
  [SerializeField] private float referenceContainerSize = 550f; // Reference size for proportional scaling
#pragma warning disable CS0414 // Inspector-assigned fields
  [SerializeField] private float baseFontSize = 72f; // Base font size at reference size
#pragma warning restore CS0414

  [Header("Editor Preview"), SerializeField] 
  private Color editorGridLineColor = new(1f, 1f, 0f, 0.5f);

  [SerializeField] private bool showGridLinesInEditor = true;

  // Actual tile size and spacing (calculated based on container size)
  private float tileSize;
  private float tileSpacing;
  private float scaleFactor = 1f; // Container size / reference size

  [Header("References"), SerializeField] 
  private GameObject tilePrefab;

  [SerializeField] private RectTransform gridContainer;
  public MatchChecker matchChecker;

  [Header("Animation Settings"), SerializeField] 
  private float tileFallSpeed = 1600f;

  [SerializeField] private float tileSwapDuration = 0.15f;
  [SerializeField] private float unsolvableResetDelay = 1f;

  [Header("Solve Animation Settings"), SerializeField] 
  private float solveConvergeDuration = 0.3f;

  [SerializeField] private float solveShowTenDuration = 0.4f;
  [SerializeField] private float convergeShrinkAmount = 0.7f;
  [SerializeField] private GameObject tenTextPrefab;

  [Header("Ten Effect Magic Settings"), SerializeField] 
  private int sparkleCount = 12;

  [SerializeField] private float burstRingCount = 2;
  [SerializeField] private Color tenGlowColor = new(1f, 0.9f, 0.3f);
  [SerializeField] private Color sparkleColor = new(1f, 0.95f, 0.6f);

  [Header("Managers"), SerializeField]  private GridValidation gridValidation;
  [SerializeField] private TileWeightManager tileWeightManager;

  [Header("Hint System"), SerializeField] 
  private bool enableHints = true;

  [SerializeField] private float hintDelay = 10f;
  [SerializeField] private float hintRepeatInterval = 3f;
  [SerializeField] private int hintParticleCount = 5;
  [SerializeField] private float hintParticleSpeed = 120f;
  [SerializeField] private float hintParticleLifetime = 0.5f;
  [SerializeField] private float hintParticleSize = 12f;
  [SerializeField] private Color hintParticleColor = new(1f, 0.9f, 0.3f, 0.9f);

  private Tile[,] grid;
  private Tile selectedTile;
  private bool isProcessing = false;

  // Hint system state
  private float timeSinceLastMove = 0f;
  private float timeSinceLastHint = 0f;
  private bool hintActive = false;
  private HintMove currentHint = null;
  private List<GameObject> activeHintParticles = new();

  // Drag-swap state (single-swap-per-gesture)
  private bool isDragging = false;
  private Tile draggedTile = null;

  private int
    dragCurrentGridX,
    dragCurrentGridY; // The dragged tile's origin cell (set in HandleDragStarted, read by HandleDragEnded to snap back / kick off the animated swap)

  private int dragTargetGridX, dragTargetGridY; // Cell the finger is currently over (updated each HandleDragMoved)
  private bool dragHasValidTarget = false; // True when the finger is over a valid in-bounds grid cell

  // MakeZen: track last swapped tiles for merge position logic
  private Tile lastSwappedFirst;
  private Tile lastSwappedSecond;

  // Informational — true when the most recent swap came from a drag gesture.
  // Used by Zen's failed-swap log message; revert behaviour is identical for both
  // drag and tap/swipe (Zen always reverts; Arcade never does).
  private bool wasDragSwap = false;

  public event System.Action OnGridUnsolvable;

  /// <summary>
  /// Data for a single matching line in Zen mode (one row or column).
  /// </summary>
  private struct ZenLineMatch {
    public bool isRow; // true = row match, false = column match
    public int lineIndex; // which row (y) or column (x)
    public int sum; // line sum (10, 20, 30, ...)
    public Tile[] tiles; // all tiles in this line (length = gridWidth or gridHeight)
    public int mergeGridPos; // grid position along the line for the merge tile
  }

  /// <summary>
  /// Called when a round starts to reset progressive tile weight tracking.
  /// Solve-based ramp reads from GameManager.Instance.SolveCount (reset per round by GameManager).
  /// </summary>
  public void OnRoundStarted() {
    Debug.Log("[GridManager] Round started - solve-based weight ramp active");
  }

  /// <summary>
  /// Immediately halt all grid processing and tile interaction.
  /// Used when game ends (win/loss) to prevent cascading auto-wins.
  /// </summary>
  public void FreezeGrid() {
    StopAllCoroutines();
    isProcessing = false;
    Debug.Log("[GridManager] Grid frozen - all processing halted.");
  }

  private void Awake() {
    grid = new Tile[gridWidth, gridHeight];
    CalculateSizesFromContainer();
  }

  /// <summary>
  /// Calculate tile size, spacing, and scale factor based on container size.
  /// </summary>
  private void CalculateSizesFromContainer() {
    if (gridContainer == null) {
      return;
    }

    var containerWidth = gridContainer.sizeDelta.x;
    scaleFactor = containerWidth / referenceContainerSize;
    tileSpacing = baseTileSpacing * scaleFactor;

    var totalSpacing = (gridWidth - 1) * tileSpacing;
    tileSize = (containerWidth - totalSpacing) / gridWidth;
  }

  /// <summary>
  /// Public hook so a responsive layout script (e.g. TabletLayoutAdapter) can re-trigger
  /// tile-size recalculation after it has changed <see cref="gridContainer"/>'s sizeDelta.
  /// Safe to call before tiles are spawned; cached values feed Spawn/Resize logic.
  /// </summary>
  public void RecalculateSizesFromContainer() {
    CalculateSizesFromContainer();
  }

#if UNITY_EDITOR
  /// <summary>
  /// Recalculates grid preview when settings change in the editor.
  /// Resize the gridContainer to change the grid size - tiles will scale to fit.
  /// </summary>
  private void OnValidate() {
    if (gridContainer == null) {
      return;
    }

    // Recalculate sizes based on current container size
    var containerWidth = gridContainer.sizeDelta.x;
    var editorScaleFactor = containerWidth / referenceContainerSize;
    var editorTileSpacing = baseTileSpacing * editorScaleFactor;
    var editorTotalSpacing = (gridWidth - 1) * editorTileSpacing;
    var editorTileSize = (containerWidth - editorTotalSpacing) / gridWidth;

    // Update the private fields for gizmo drawing
    scaleFactor = editorScaleFactor;
    tileSpacing = editorTileSpacing;
    tileSize = editorTileSize;
  }

  /// <summary>
  /// Draws grid lines in the Scene view for visual preview.
  /// </summary>
  private void OnDrawGizmosSelected() {
    if (!showGridLinesInEditor || gridContainer == null) {
      return;
    }

    // Calculate sizes for preview (in case OnValidate hasn't run)
    var containerWidth = gridContainer.sizeDelta.x;
    var previewScaleFactor = containerWidth / referenceContainerSize;
    var previewTileSpacing = baseTileSpacing * previewScaleFactor;
    var previewTotalSpacing = (gridWidth - 1) * previewTileSpacing;
    var previewTileSize = (containerWidth - previewTotalSpacing) / gridWidth;

    var totalWidth = gridWidth * previewTileSize + (gridWidth - 1) * previewTileSpacing;
    var totalHeight = gridHeight * previewTileSize + (gridHeight - 1) * previewTileSpacing;

    // Get the canvas for proper world-space scaling
    var canvas = gridContainer.GetComponentInParent<Canvas>();
    var canvasScale = canvas != null ? canvas.transform.lossyScale.x : 1f;

    // Get world position of the container center
    var containerCenter = gridContainer.position;

    Gizmos.color = editorGridLineColor;

    var startX = -totalWidth / 2f;
    var startY = totalHeight / 2f;

    // Draw tile cells
    for (var y = 0; y < gridHeight; y++)
    for (var x = 0; x < gridWidth; x++) {
      var posX = startX + x * (previewTileSize + previewTileSpacing) + previewTileSize / 2f;
      var posY = startY - y * (previewTileSize + previewTileSpacing) - previewTileSize / 2f;

      var cellCenter = containerCenter + new Vector3(posX * canvasScale, posY * canvasScale, 0);
      var cellSize = new Vector3(previewTileSize * canvasScale, previewTileSize * canvasScale, 0);

      Gizmos.DrawWireCube(cellCenter, cellSize);
    }

    // Draw outer boundary
    Gizmos.color = new Color(editorGridLineColor.r, editorGridLineColor.g, editorGridLineColor.b, 1f);
    var boundarySize = new Vector3(totalWidth * canvasScale, totalHeight * canvasScale, 0);
    Gizmos.DrawWireCube(containerCenter, boundarySize);
  }
#endif

  private void OnEnable() {
    Tile.OnTileClicked += HandleTileClicked;
    Tile.OnTileSwiped += HandleTileSwiped;
    Tile.OnTileDragStarted += HandleDragStarted;
    Tile.OnTileDragMoved += HandleDragMoved;
    Tile.OnTileDragEnded += HandleDragEnded;
  }

  private void OnDisable() {
    Tile.OnTileClicked -= HandleTileClicked;
    Tile.OnTileSwiped -= HandleTileSwiped;
    Tile.OnTileDragStarted -= HandleDragStarted;
    Tile.OnTileDragMoved -= HandleDragMoved;
    Tile.OnTileDragEnded -= HandleDragEnded;
  }

  private void Start() {
    if (SceneFlowManager.Instance == null) {
      Debug.Log("No SceneFlowManager found - auto-starting grid for testing");
      SpawnGrid();
      StartCoroutine(ProcessMatchesCoroutine());
    }
  }

  private void Update() {
    // Only track hint timer when game is active and not processing
    if (!enableHints) {
      return;
    }

    if (GameManager.Instance == null || !GameManager.Instance.IsGameActive) {
      return;
    }

    if (GameManager.Instance.CurrentMode == GameManager.GameMode.Zen) {
      return; // No hints in Zen — let the player think
    }

    if (isProcessing) {
      return;
    }

    timeSinceLastMove += Time.deltaTime;

    // Check if it's time to show a hint
    if (timeSinceLastMove >= hintDelay) {
      timeSinceLastHint += Time.deltaTime;

      // Show hint periodically
      if (!hintActive || timeSinceLastHint >= hintRepeatInterval) {
        ShowHint();
        timeSinceLastHint = 0f;
      }
    }
  }

  #region Hint System

  private void ResetHintTimer() {
    timeSinceLastMove = 0f;
    timeSinceLastHint = 0f;
    hintActive = false;
    currentHint = null;
    ClearHintParticles();
  }

  private void ShowHint() {
    if (matchChecker == null) {
      return;
    }

    // Find a valid move
    currentHint = matchChecker.FindHintMove();

    if (currentHint != null && currentHint.tile != null) {
      hintActive = true;

      if (currentHint.targetTile != null) {
        // Zen: pulse both tiles to highlight the swap pair
        StartCoroutine(PulseZenHintTiles(currentHint));
        Debug.Log($"<color=yellow>HINT:</color> Swap {currentHint.tile} ↔ {currentHint.targetTile}");
      }
      else {
        // Arcade: directional particle trail
        StartCoroutine(SpawnHintParticles(currentHint));
        Debug.Log($"<color=yellow>HINT:</color> Swipe {currentHint.tile} {currentHint.direction}");
      }
    }
  }

  /// <summary>
  /// Zen hint: gentle scale pulse on both tiles in the hint pair.
  /// Repeating pulses handled by the hint timer re-triggering ShowHint.
  /// </summary>
  private IEnumerator PulseZenHintTiles (HintMove hint) {
    if (hint.tile == null) {
      yield break;
    }

    // Pulse first tile
    AnimationUtilities.PunchScale(hint.tile.GetRectTransform(), 1.1f, 0.25f);
    yield return new WaitForSeconds(0.12f);

    // Pulse second tile (staggered for visual clarity)
    if (hint.targetTile != null) {
      AnimationUtilities.PunchScale(hint.targetTile.GetRectTransform(), 1.1f, 0.25f);
    }
  }

  private IEnumerator SpawnHintParticles (HintMove hint) {
    if (hint.tile == null) {
      yield break;
    }

    var tilePos = hint.tile.GetRectTransform().anchoredPosition;
    var direction = hint.GetDirectionVector();

    // Spawn particles in a burst
    for (var i = 0; i < hintParticleCount; i++) {
      SpawnSingleHintParticle(tilePos, direction, i * 0.06f);
      yield return new WaitForSeconds(0.04f);
    }
  }

  private void SpawnSingleHintParticle (Vector2 startPos, Vector2 direction, float delay) {
    StartCoroutine(AnimateHintParticle(startPos, direction, delay));
  }

  private IEnumerator AnimateHintParticle (Vector2 startPos, Vector2 direction, float delay) {
    yield return new WaitForSeconds(delay);

    // Create particle
    var particle = new GameObject("HintParticle");
    particle.transform.SetParent(gridContainer, false);
    activeHintParticles.Add(particle);

    var rt = particle.AddComponent<RectTransform>();

    // Start slightly behind center, end ahead (scaled)
    var startOffset = -20f * scaleFactor;
    var endOffset = 60f * scaleFactor;
    rt.anchoredPosition = startPos + direction * startOffset;
    rt.sizeDelta = new Vector2(hintParticleSize * scaleFactor, hintParticleSize * scaleFactor);
    rt.localEulerAngles = new Vector3(0, 0, 45f); // Diamond shape

    var img = particle.AddComponent<Image>();
    img.color = hintParticleColor;
    img.raycastTarget = false;

    // Animate: move in direction, fade out, shrink
    var elapsed = 0f;
    var velocity = direction * hintParticleSpeed * scaleFactor;

    // Add slight randomness (scaled)
    var wobble = Random.Range(-15f, 15f) * scaleFactor;
    var perpendicular = new Vector2(-direction.y, direction.x);

    while (elapsed < hintParticleLifetime) {
      if (particle == null) {
        yield break;
      }

      elapsed += Time.deltaTime;
      var t = elapsed / hintParticleLifetime;

      // Move forward
      var pos = startPos + direction * Mathf.Lerp(startOffset, endOffset, t);
      pos += perpendicular * Mathf.Sin(t * Mathf.PI * 2f) * wobble * (1f - t);
      rt.anchoredPosition = pos;

      // Fade: appear quickly, fade out slowly
      float alpha;
      if (t < 0.2f) {
        alpha = t / 0.2f; // Fade in
      }
      else {
        alpha = 1f - (t - 0.2f) / 0.8f; // Fade out
      }

      img.color = new Color(hintParticleColor.r, hintParticleColor.g, hintParticleColor.b,
        hintParticleColor.a * alpha);

      // Scale: start small, grow, then shrink
      var scale = Mathf.Sin(t * Mathf.PI) * 1.2f + 0.3f;
      rt.localScale = Vector3.one * scale;

      yield return null;
    }

    // Cleanup
    if (particle != null) {
      activeHintParticles.Remove(particle);
      Destroy(particle);
    }
  }

  private void ClearHintParticles() {
    foreach (var p in activeHintParticles) {
      if (p != null) {
        Destroy(p);
      }
    }

    activeHintParticles.Clear();
  }

  #endregion

  public void SpawnGrid() {
    Debug.Log("GridManager.SpawnGrid() called");

    if (tilePrefab == null) {
      Debug.LogError("GridManager: tilePrefab is not assigned!");
      return;
    }

    // Solve-based ramp reads GameManager.Instance.SolveCount directly — no timer needed

    // Get grid size from GameManager (difficulty-based)
    UpdateGridSizeFromDifficulty();

    ClearGrid();
    ResetHintTimer();

    // Clear swap refs so the first ProcessMatchesCoroutine call
    // (from StartMatchProcessing) doesn't fire OnFailedSwap
    lastSwappedFirst = null;
    lastSwappedSecond = null;
    wasDragSwap = false;

    var totalWidth = gridWidth * tileSize + (gridWidth - 1) * tileSpacing;
    var totalHeight = gridHeight * tileSize + (gridHeight - 1) * tileSpacing;
    var startX = -totalWidth / 2f + tileSize / 2f;
    var startY = totalHeight / 2f - tileSize / 2f;

    for (var y = 0; y < gridHeight; y++)
    for (var x = 0; x < gridWidth; x++) {
      var posX = startX + x * (tileSize + tileSpacing);
      var posY = startY - y * (tileSize + tileSpacing);
      var tile = CreateTile(x, y, new Vector2(posX, posY));
      grid[x, y] = tile;
    }

    // Ensure no rows/columns already sum to 10 — first match must come from the player
    gridValidation.EnsureNoInitialMatches(grid, gridWidth, gridHeight, tileWeightManager);

    Debug.Log($"Grid spawned: {gridWidth}x{gridHeight} (tile size: {tileSize:F0})");

    // Initialize VFX system with grid container (delayed one frame so Canvas layout is calculated)
    if (GridVFX.Instance != null) {
      StartCoroutine(DelayedVFXInit());
    }
  }

  /// <summary>
  /// Wait one frame for Canvas layout to calculate grid container dimensions,
  /// then initialize VFX so ambient particles spawn in the correct area.
  /// </summary>
  private IEnumerator DelayedVFXInit() {
    yield return null; // Wait one frame for layout pass
    if (GridVFX.Instance != null && gridContainer != null) {
      GridVFX.Instance.Initialize(gridContainer);
    }
  }

  /// <summary>
  /// Update grid size based on difficulty settings.
  /// Uses GameManager fallback, defaults to 5x5 serialized values.
  /// </summary>
  private void UpdateGridSizeFromDifficulty() {
    // Try GameManager for difficulty-based grid size
    if (GameManager.Instance != null) {
      var newSize = GameManager.Instance.GetCurrentGridSize();
      if (newSize != gridWidth || newSize != gridHeight) {
        gridWidth = newSize;
        gridHeight = newSize;
        grid = new Tile[gridWidth, gridHeight];
      }
    }
    // Otherwise use serialized defaults (5x5)

    // Always recalculate sizes from container (handles both difficulty change and container resize)
    CalculateSizesFromContainer();
    Debug.Log(
      $"<color=cyan>Grid size: {gridWidth}x{gridHeight}, tile size: {tileSize:F0}, scale: {scaleFactor:F2}</color>");
  }

  private Tile CreateTile (int gridX, int gridY, Vector2 position) {
    var tileObj = Instantiate(tilePrefab, gridContainer);
    var tile = tileObj.GetComponent<Tile>();

    if (tile != null) {
      var value = tileWeightManager.GetWeightedRandomValue();
      tile.Initialize(value, gridX, gridY);
      tile.SetPosition(position);

      var rt = tile.GetRectTransform();
      rt.sizeDelta = new Vector2(tileSize, tileSize);
      // Font size handled by TextMeshPro auto-sizing in prefab
    }

    return tile;
  }


  private void HandleTileClicked (Tile tile) {
    if (isProcessing) {
      return;
    }

    if (isDragging) {
      return;
    }

    if (GameManager.Instance != null && !GameManager.Instance.IsGameActive) {
      return;
    }

    // Reset hint timer on any interaction
    ResetHintTimer();

    if (selectedTile == null) {
      selectedTile = tile;
      tile.Select();
      AudioManager.Instance?.PlayTileSelect();
      Debug.Log($"Selected: {tile}");
    }
    else if (selectedTile == tile) {
      tile.Deselect();
      selectedTile = null;
      Debug.Log("Deselected");
    }
    else {
      // Defensive: reject locked tiles as second selection
      // (Tile.cs should block the event, but guard here too)
      if (tile.IsLocked) {
        Debug.Log($"Second tile is locked — ignoring.");
        return;
      }

      var isZen = GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.Zen;

      // Arcade: must be adjacent. Zen: any two free tiles on the board.
      if (!isZen && !IsAdjacent(selectedTile, tile)) {
        selectedTile.Deselect();
        selectedTile = tile;
        tile.Select();
        Debug.Log($"Not adjacent! Switched selection to: {tile}");
        return;
      }

      var firstTile = selectedTile;
      var secondTile = tile;
      selectedTile = null;
      firstTile.Deselect();
      secondTile.Deselect();
      StartCoroutine(AnimatedSwapCoroutine(firstTile, secondTile));
    }
  }

  private bool IsAdjacent (Tile a, Tile b) {
    var dx = Mathf.Abs(a.GridX - b.GridX);
    var dy = Mathf.Abs(a.GridY - b.GridY);
    return (dx == 1 && dy == 0) || (dx == 0 && dy == 1);
  }

  private void HandleTileSwiped (Tile tile, SwipeDirection direction) {
    if (isProcessing) {
      return;
    }

    if (isDragging) {
      return;
    }

    if (GameManager.Instance != null && !GameManager.Instance.IsGameActive) {
      return;
    }

    // Reset hint timer on any interaction
    ResetHintTimer();

    var neighborX = tile.GridX;
    var neighborY = tile.GridY;

    switch (direction) {
      case SwipeDirection.Up: neighborY -= 1; break;
      case SwipeDirection.Down: neighborY += 1; break;
      case SwipeDirection.Left: neighborX -= 1; break;
      case SwipeDirection.Right: neighborX += 1; break;
    }

    if (neighborX < 0 || neighborX >= gridWidth || neighborY < 0 || neighborY >= gridHeight) {
      Debug.Log($"Swipe {direction} blocked - no tile in that direction");
      return;
    }

    var neighborTile = grid[neighborX, neighborY];
    if (neighborTile == null) {
      Debug.Log($"Swipe {direction} blocked - neighbor tile is null");
      return;
    }

    // Can't swap with a locked tile (MakeZen locked tiles are immovable)
    if (neighborTile.IsLocked) {
      Debug.Log($"Swipe {direction} blocked - neighbor tile is locked");
      return;
    }

    if (selectedTile != null) {
      selectedTile.Deselect();
      selectedTile = null;
    }

    Debug.Log($"Swipe swap: {tile} <-> {neighborTile}");
    StartCoroutine(AnimatedSwapCoroutine(tile, neighborTile));
  }

  /// <summary>
  /// Animate two tiles swapping positions with an arc motion. Single chokepoint
  /// used by every player-initiated swap: tap-tap, swipe, AND drag-release.
  ///
  /// Each call:
  ///   1. Tweens both tiles between their current anchored positions over
  ///      <see cref="tileSwapDuration"/> with a shallow arc.
  ///   2. Commits the grid backing array (grid[] + GridX/GridY on both tiles).
  ///   3. If not a revert, kicks off <see cref="ProcessMatchesCoroutine"/>.
  ///
  /// PARAMETERS:
  ///   isRevert    — true for the Zen failed-swap rewind. No sound, no swap tracking,
  ///                 no match processing afterwards. Arcade never calls with true.
  ///   isDragSwap  — informational only (drives Zen's failed-swap log message and
  ///                 anything else that reads wasDragSwap). Set true when the caller
  ///                 is the drag-release path so Zen logs read "drag swap" instead
  ///                 of "tap-tap swap". Has no effect on revert/non-revert routing
  ///                 since Arcade has no revert and Zen reverts every failed swap.
  /// </summary>
  private IEnumerator AnimatedSwapCoroutine (Tile tileA, Tile tileB, bool isRevert = false, bool isDragSwap = false) {
    isProcessing = true;

    if (!isRevert) {
      ResetHintTimer();
      AudioManager.Instance?.PlaySwapSound();

      // MakeZen: track swapped tiles for merge position logic
      lastSwappedFirst = tileA;
      lastSwappedSecond = tileB;
      wasDragSwap = isDragSwap; // preserves Zen's drag-vs-tap log distinction
    }

    var posA = tileA.GetRectTransform().anchoredPosition;
    var posB = tileB.GetRectTransform().anchoredPosition;

    var elapsed = 0f;
    while (elapsed < tileSwapDuration) {
      elapsed += Time.deltaTime;
      var t = Mathf.Clamp01(elapsed / tileSwapDuration);
      var smoothT = AnimationUtilities.EaseInOutCubic(t);

      // Shallow arc: tiles lift slightly as they cross paths
      var arcOffset = Mathf.Sin(t * Mathf.PI) * 8f;
      var arcA = Vector2.Lerp(posA, posB, smoothT) + new Vector2(0, arcOffset);
      var arcB = Vector2.Lerp(posB, posA, smoothT) + new Vector2(0, arcOffset);

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
    tileA.GridX = bxOld;
    tileA.GridY = byOld;
    tileB.GridX = axOld;
    tileB.GridY = ayOld;

    isProcessing = false;

    if (!isRevert) {
      StartCoroutine(ProcessMatchesCoroutine());
    }
  }

  /// <summary>
  /// Visual feedback for a failed swap: screen shake + red flash on swapped tiles.
  /// Called when a player swap produces no match (Zen or Arcade).
  /// </summary>
  private void PlayFailedSwapFeedback() {
    // Arcade: no feedback — player is already time-pressured, just let them keep going
    if (GameManager.Instance == null || GameManager.Instance.CurrentMode != GameManager.GameMode.Zen) {
      return;
    }

    // Zen: screen shake + red flash + penalty popup (punishes guessing)
    GridVFX.Instance?.TriggerShake(0); // chainCount 0 → base intensity only

    // Flash the swapped tiles red briefly
    if (lastSwappedFirst != null) {
      StartCoroutine(FlashTileRed(lastSwappedFirst));
    }

    if (lastSwappedSecond != null && lastSwappedSecond != lastSwappedFirst) {
      StartCoroutine(FlashTileRed(lastSwappedSecond));
    }

    UIManager.Instance?.ShowPenaltyPopup("-3s");
  }

  /// <summary>
  /// Briefly flash a tile's background red then restore original color.
  /// </summary>
  private IEnumerator FlashTileRed (Tile tile) {
    if (tile == null) {
      yield break;
    }

    var bg = tile.GetComponent<Image>();
    if (bg == null) {
      yield break;
    }

    var originalColor = bg.color;
    var flashColor = new Color(0.9f, 0.2f, 0.2f, 1f); // Red flash

    // Flash on
    var flashDuration = 0.12f;
    var elapsed = 0f;
    while (elapsed < flashDuration) {
      elapsed += Time.deltaTime;
      var t = elapsed / flashDuration;
      bg.color = Color.Lerp(flashColor, originalColor, AnimationUtilities.EaseOutCubic(t));
      yield return null;
    }

    bg.color = originalColor;

    // Quick punch scale to emphasize the error
    yield return AnimationUtilities.PunchScale(tile.transform, 0.9f, 0.1f);
  }

  // ==========================================
  // DRAG-SWAP SYSTEM
  // ==========================================

  /// <summary>
  /// Called when a tile drag begins (after activation threshold is met).
  /// </summary>
  private void HandleDragStarted (Tile tile) {
    if (isProcessing) {
      return;
    }

    if (isDragging) {
      return; // Multitouch guard: a drag is already in flight, reject this one.
    }

    if (GameManager.Instance != null && !GameManager.Instance.IsGameActive) {
      return;
    }

    // Zen: drag-to-swap enabled — tiles stay in place on failed drag (no revert)

    // Clear any click-selection
    if (selectedTile != null) {
      selectedTile.Deselect();
      selectedTile = null;
    }

    isDragging = true;
    draggedTile = tile;
    dragCurrentGridX = tile.GridX;
    dragCurrentGridY = tile.GridY;
    dragTargetGridX = tile.GridX;
    dragTargetGridY = tile.GridY;
    dragHasValidTarget = false; // Not over a different cell yet

    // Bring dragged tile to front so it renders above others
    tile.GetRectTransform().SetAsLastSibling();

    AudioManager.Instance?.PlayTileSelect();
    ResetHintTimer();

    Debug.Log($"Drag started: {tile}");
  }

  /// <summary>
  /// Called every frame during drag with the screen-space position.
  /// Visually follows the finger and tracks which cell is currently under the finger.
  /// Does NOT perform any swap during the drag — the swap happens once on release in HandleDragEnded.
  /// </summary>
  private void HandleDragMoved (Tile tile, Vector2 screenPos) {
    if (!isDragging || tile != draggedTile) {
      return;
    }

    // Convert screen position to canvas-local position
    Camera cam = null; // null works for Screen Space - Overlay canvas
    var canvas = gridContainer.GetComponentInParent<Canvas>();
    if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) {
      cam = canvas.worldCamera;
    }

    Vector2 localPos;
    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
          gridContainer, screenPos, cam, out localPos)) {
      return; // Conversion failed
    }

    // Move the dragged tile to follow the finger/cursor (preview only — no swap yet)
    tile.GetRectTransform().anchoredPosition = localPos;

    // Determine which grid cell the tile center is now over
    var targetCell = GetGridCellAtPosition(localPos);
    var targetX = targetCell.x;
    var targetY = targetCell.y;

    // Track the current target cell. Stay valid only while the finger is in-bounds;
    // if the finger leaves the grid, mark the target invalid so a release outside the grid
    // won't trigger a swap. (The drag is not ended here — the player can still drag back in.)
    if (targetX >= 0 && targetX < gridWidth && targetY >= 0 && targetY < gridHeight) {
      dragTargetGridX = targetX;
      dragTargetGridY = targetY;
      dragHasValidTarget = targetX != dragCurrentGridX || targetY != dragCurrentGridY;
    }
    else {
      dragHasValidTarget = false;
    }
  }

  /// <summary>
  /// Called when the drag ends (finger/mouse released).
  /// Performs at most ONE swap (origin-cell ↔ final target-cell), enforcing mode rules:
  ///   - Arcade: target must be cardinally adjacent. Otherwise the tile snaps back, no swap.
  ///   - Zen:    any unlocked free cell allowed.
  /// If the drag ended outside the grid, on the origin cell, or on a locked tile, no swap occurs.
  /// </summary>
  private void HandleDragEnded (Tile tile) {
    if (!isDragging || tile != draggedTile) {
      return;
    }

    var startX = dragCurrentGridX;
    var startY = dragCurrentGridY;
    var targetX = dragTargetGridX;
    var targetY = dragTargetGridY;
    var hasValidTarget = dragHasValidTarget;

    Debug.Log($"Drag ended: {tile} from [{startX},{startY}] → target [{targetX},{targetY}] hasValid={hasValidTarget}");

    // Decide whether the gesture should commit a swap.
    var shouldSwap = hasValidTarget && (targetX != startX || targetY != startY);

    if (shouldSwap) {
      // Any-distance drag is allowed in both modes (1.0.1 unified swap-revert spec).
      // Reject swap onto a locked tile (Zen) or a missing tile.
      var targetTile = grid[targetX, targetY];
      if (targetTile == null) {
        Debug.Log("Drag target cell empty — no swap.");
        shouldSwap = false;
      }
      else if (targetTile.IsLocked) {
        Debug.Log("Drag target tile is locked — no swap.");
        shouldSwap = false;
      }
    }

    if (!shouldSwap) {
      // Snap the dragged tile back to its origin cell. No match processing, no penalty.
      tile.SetPosition(GridToWorldPosition(startX, startY));
      ClearDragState();
      return;
    }

    // Resolve the displaced tile at the target cell — it'll become the swap partner.
    var displacedTile = grid[targetX, targetY];

    // Path (A) feel: rewind the dragged tile to its origin cell BEFORE the arc swap
    // animation starts. The animation then tweens both tiles between two true grid
    // cells (origin ↔ target) exactly like tap-tap and swipe, instead of starting
    // from wherever the finger happened to release. Keeps the arc math symmetric
    // and matches the gameplay feel the player already knows from tap-tap.
    tile.SetPosition(GridToWorldPosition(startX, startY));

    // Drop drag tracking now — the gesture is fully resolved; everything downstream
    // is just the animated swap. AnimatedSwapCoroutine sets isProcessing=true on entry
    // so input is gated for the 0.15s duration.
    ClearDragState();

    // Run the unified animated swap. isDragSwap=true preserves Zen's drag-vs-tap log
    // distinction (the only thing that reads wasDragSwap today). AnimatedSwapCoroutine
    // commits the grid array post-tween and kicks off ProcessMatchesCoroutine.
    StartCoroutine(AnimatedSwapCoroutine(tile, displacedTile, false, true));
  }

  /// <summary>
  /// Reset all per-drag state so the next gesture starts clean.
  /// </summary>
  private void ClearDragState() {
    isDragging = false;
    draggedTile = null;
    dragHasValidTarget = false;
  }

  /// <summary>
  /// Quick smooth animation sliding a tile to a target position.
  /// Used for displaced tiles during drag-swap.
  /// </summary>
  private IEnumerator SnapTileCoroutine (Tile tile, Vector2 targetPos, float duration) {
    var startPos = tile.GetRectTransform().anchoredPosition;
    var elapsed = 0f;

    while (elapsed < duration) {
      elapsed += Time.deltaTime;
      var t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
      tile.GetRectTransform().anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
      yield return null;
    }

    tile.GetRectTransform().anchoredPosition = targetPos;
  }

  /// <summary>
  /// Convert a canvas-local position to grid cell coordinates.
  /// Reverse of GridToWorldPosition. Clamped to grid bounds.
  /// </summary>
  private Vector2Int GetGridCellAtPosition (Vector2 localPos) {
    var totalWidth = gridWidth * tileSize + (gridWidth - 1) * tileSpacing;
    var totalHeight = gridHeight * tileSize + (gridHeight - 1) * tileSpacing;
    var startX = -totalWidth / 2f + tileSize / 2f;
    var startY = totalHeight / 2f - tileSize / 2f;

    var cellStep = tileSize + tileSpacing;

    var gridX = Mathf.RoundToInt((localPos.x - startX) / cellStep);
    var gridY = Mathf.RoundToInt((startY - localPos.y) / cellStep);

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
  private ZenLineMatch? FindFirstZenMatch() {
    // Check rows first
    for (var y = 0; y < gridHeight; y++) {
      var sum = 0;
      var allPresent = true;
      var lineTiles = new Tile[gridWidth];
      for (var x = 0; x < gridWidth; x++) {
        if (grid[x, y] == null) {
          allPresent = false;
          break;
        }

        lineTiles[x] = grid[x, y];
        sum += grid[x, y].Value;
      }

      if (allPresent && sum > 0 && sum % 10 == 0) {
        return new ZenLineMatch {
          isRow = true,
          lineIndex = y,
          sum = sum,
          tiles = lineTiles
        };
      }
    }

    // Check columns
    for (var x = 0; x < gridWidth; x++) {
      var sum = 0;
      var allPresent = true;
      var lineTiles = new Tile[gridHeight];
      for (var y = 0; y < gridHeight; y++) {
        if (grid[x, y] == null) {
          allPresent = false;
          break;
        }

        lineTiles[y] = grid[x, y];
        sum += grid[x, y].Value;
      }

      if (allPresent && sum > 0 && sum % 10 == 0) {
        return new ZenLineMatch {
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
  private int GetZenMergePosition (ZenLineMatch match) {
    // Prefer where the secondSwapped tile sits on this line
    if (lastSwappedSecond != null) {
      if (match.isRow && lastSwappedSecond.GridY == match.lineIndex) {
        return lastSwappedSecond.GridX;
      }

      if (!match.isRow && lastSwappedSecond.GridX == match.lineIndex) {
        return lastSwappedSecond.GridY;
      }
    }

    // Fallback: firstSwapped position on this line
    if (lastSwappedFirst != null) {
      if (match.isRow && lastSwappedFirst.GridY == match.lineIndex) {
        return lastSwappedFirst.GridX;
      }

      if (!match.isRow && lastSwappedFirst.GridX == match.lineIndex) {
        return lastSwappedFirst.GridY;
      }
    }

    // Final fallback: center of line
    return (match.isRow ? gridWidth : gridHeight) / 2;
  }

  /// <summary>
  /// Animate a single Zen match: beam flash, convergence to merge point,
  /// locked tile creation, other tile removal, scoring.
  /// </summary>
  private IEnumerator AnimateZenMatch (ZenLineMatch match, int cascadeCount) {
    var mergePos = GetZenMergePosition(match);
    match.mergeGridPos = mergePos;

    if (GameManager.Instance != null) {
      GameManager.Instance.IsSolveAnimationPlaying = true;
    }

    // Identify the merge tile and the tiles to remove
    Tile mergeTile;
    int mergeX, mergeY;
    if (match.isRow) {
      mergeX = mergePos;
      mergeY = match.lineIndex;
    }
    else {
      mergeX = match.lineIndex;
      mergeY = mergePos;
    }

    mergeTile = grid[mergeX, mergeY];

    // Build a single-line MatchResult for VFX (beam flash)
    var singleLineResult = new MatchResult();
    foreach (var t in match.tiles) {
      if (t != null) {
        singleLineResult.allMatchedTiles.Add(t);
      }
    }

    if (match.isRow) {
      singleLineResult.matchedRows.Add(match.lineIndex);
      singleLineResult.rowSums[match.lineIndex] = match.sum;
    }
    else {
      singleLineResult.matchedColumns.Add(match.lineIndex);
      singleLineResult.columnSums[match.lineIndex] = match.sum;
    }

    // Fire beam flash (non-blocking)
    if (GridVFX.Instance != null) {
      StartCoroutine(GridVFX.Instance.PlayLineSweeps(singleLineResult, tileSize, tileSpacing));
    }

    yield return new WaitForSeconds(0.08f);

    // Trigger avatar solve animation
    AvatarManager.Instance?.OnSolve();
    AudioManager.Instance?.PlayConvergenceSound();

    // Convergence: all non-merge tiles in the line slide toward the merge tile
    var mergeWorldPos = GridToWorldPosition(mergeX, mergeY);
    var originalPositions = new Dictionary<Tile, Vector2>();
    var tilesToRemove = new List<Tile>();

    foreach (var t in match.tiles) {
      if (t == null) {
        continue;
      }

      originalPositions[t] = t.GetRectTransform().anchoredPosition;
      if (t != mergeTile) {
        tilesToRemove.Add(t);
      }
    }

    // Animate convergence (tiles slide + shrink toward merge position)
    var elapsed = 0f;
    while (elapsed < solveConvergeDuration) {
      elapsed += Time.deltaTime;
      var t = elapsed / solveConvergeDuration;
      var easedT = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

      foreach (var tile in tilesToRemove) {
        if (tile == null || !originalPositions.ContainsKey(tile)) {
          continue;
        }

        var rt = tile.GetRectTransform();
        var startPos = originalPositions[tile];
        rt.anchoredPosition = Vector2.Lerp(startPos, mergeWorldPos, easedT);

        var scale = Mathf.Lerp(1f, convergeShrinkAmount, easedT);
        tile.transform.localScale = Vector3.one * scale;
        tile.transform.localEulerAngles = new Vector3(0, 0, easedT * 180f);

        // Fade tile background and overlays (shine, glow)
        var img = tile.GetComponent<Image>();
        if (img != null) {
          img.color = new Color(0.85f, 0.85f, 0.85f, 1f - easedT);
        }

        tile.SetOverlayAlpha(1f - easedT);
      }

      // Merge tile: subtle grow pulse during convergence
      if (mergeTile != null) {
        var pulseScale = 1f + Mathf.Sin(easedT * Mathf.PI) * 0.08f;
        mergeTile.transform.localScale = Vector3.one * pulseScale;
      }

      yield return null;
    }

    // === Convergence complete: create locked tile, remove others ===

    // Collect original tile values BEFORE modifying/destroying (for scoring)
    var tileValues = new List<int>();
    foreach (var tile in match.tiles) {
      if (tile != null) {
        tileValues.Add(tile.Value);
      }
    }

    // SFX and chain tracking
    var consecutiveCount = gridValidation.RegisterMatch();
    AudioManager.Instance?.PlayTenPopSound(consecutiveCount);
    if (GridVFX.Instance != null) {
      GridVFX.Instance.TriggerShake(consecutiveCount);
      GridVFX.Instance.PulseAmbientParticles();
    }

    // Convert merge tile to locked (set value = line sum)
    if (mergeTile != null) {
      mergeTile.SetValue(match.sum);
      mergeTile.transform.localScale = Vector3.one;
      Debug.Log($"<color=cyan>[Zen]</color> Locked tile created: value {match.sum} at [{mergeX},{mergeY}]");

      // Track Zen stats for difficulty ramp and results screen
      GameManager.Instance?.RecordZenMatch();
      GameManager.Instance?.RecordZenLockedTile(match.sum);
    }

    // Remove the other tiles from the grid
    foreach (var tile in tilesToRemove) {
      if (tile != null) {
        grid[tile.GridX, tile.GridY] = null;
        Destroy(tile.gameObject);
      }
    }

    // Show sum popup at merge position
    yield return StartCoroutine(ShowTenEffectSpectacular(mergeWorldPos, match.sum));

    // Report scoring to GameManager
    GameManager.Instance?.OnMatchCleared(
      match.tiles.Length, // tilesCleared
      match.isRow ? 1 : 0, // rowsMatched
      match.isRow ? 0 : 1, // columnsMatched
      tileValues,
      singleLineResult,
      cascadeCount
    );

    if (GameManager.Instance != null) {
      GameManager.Instance.IsSolveAnimationPlaying = false;
    }
  }

  private IEnumerator ProcessMatchesCoroutine() {
    isProcessing = true;
    var cascadeCount = 0;
    var isZenMode = GameManager.Instance != null
                    && GameManager.Instance.CurrentMode == GameManager.GameMode.Zen;

    GameManager.Instance?.OnCascadeStart();

    while (true) {
      // Stop processing if game is no longer active (e.g. win/loss triggered)
      if (GameManager.Instance != null && !GameManager.Instance.IsGameActive) {
        Debug.Log("Game no longer active - halting cascade processing.");
        break;
      }

      if (matchChecker == null) {
        Debug.LogWarning("MatchChecker not assigned!");
        break;
      }

      // ──────────────────────────────────────────────
      // ZEN MODE: line-by-line merge processing
      // ──────────────────────────────────────────────
      if (isZenMode) {
        var zenMatch = FindFirstZenMatch();

        if (zenMatch == null) {
          if (cascadeCount > 0) {
            Debug.Log($"<color=cyan>[Zen]</color> Cascade complete! {cascadeCount} chain(s)");
          }
          else if (lastSwappedFirst != null || lastSwappedSecond != null) {
            // Only treat as failed swap if this was triggered by an actual player swap.
            // Skip on game start, post-reshuffle, and other non-swap entries.

            // Unified 1.0.1 behaviour: revert visually, no penalty, no feedback.
            // Drag-swap and tap/swipe-swap paths now share identical revert logic.
            Debug.Log(wasDragSwap ?
              "<color=cyan>[Zen]</color> No matches — reverting drag swap." :
              "<color=cyan>[Zen]</color> No matches — reverting tap-tap swap.");

            var revertA = lastSwappedFirst;
            var revertB = lastSwappedSecond;
            if (revertA != null && revertB != null && revertA != revertB) {
              yield return StartCoroutine(AnimatedSwapCoroutine(revertA, revertB, true));
              // Re-lock processing (AnimatedSwapCoroutine sets isProcessing=false on exit)
              // until ProcessMatchesCoroutine ends — prevents input during the revert tail.
              isProcessing = true;
            }
          }
          else {
            Debug.Log("<color=cyan>[Zen]</color> No matches (startup/reshuffle check) — not a failed swap.");
          }

          break;
        }

        cascadeCount++;

        // Clear swap references for chain matches (cascades have no "player" position)
        if (cascadeCount > 1) {
          lastSwappedFirst = null;
          lastSwappedSecond = null;
          wasDragSwap = false;
          GameManager.Instance?.RecordZenChain();
        }

        var match = zenMatch.Value;
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
      var result = matchChecker.GetMatchResult();

      if (!result.HasMatches) {
        if (cascadeCount > 0) {
          Debug.Log($"Cascade complete! {cascadeCount} chain(s)");
        }

        break;
      }

      cascadeCount++;
      Debug.Log($"<color=yellow>MATCH {cascadeCount}!</color> " +
                $"{result.matchedRows.Count} rows, {result.matchedColumns.Count} columns, " +
                $"{result.TotalMatchedTiles} tiles");

      // Collect tile values before clearing for enhanced number bonuses
      var tileValues = new List<int>();
      foreach (var tile in result.allMatchedTiles) {
        if (tile != null) {
          tileValues.Add(tile.Value);
        }
      }

      yield return StartCoroutine(AnimateSolveSequence(result.allMatchedTiles, result, cascadeCount));

      ClearMatchedTiles(result.allMatchedTiles);

      GameManager.Instance?.OnMatchCleared(
        result.TotalMatchedTiles,
        result.matchedRows.Count,
        result.matchedColumns.Count,
        tileValues,
        result,
        cascadeCount // 1 = player swap, 2+ = cascade
      );

      yield return StartCoroutine(DropTilesCoroutine());
      yield return StartCoroutine(SpawnNewTilesCoroutine());
    }

    GameManager.Instance?.OnCascadeEnd();

    // Reset hint timer after cascade completes
    ResetHintTimer();

    if (matchChecker != null && !matchChecker.HasValidMoves()) {
      // Zen mode: use a reshuffle if available, otherwise game over
      if (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.Zen) {
        if (GameManager.Instance.UseReshuffle()) {
          Debug.Log($"<color=cyan>ZEN RESHUFFLE!</color> {GameManager.Instance.ZenReshufflesRemaining} left.");
          // Clear swap refs so the post-reshuffle ProcessMatchesCoroutine
          // doesn't treat the "no matches" result as a failed player swap
          lastSwappedFirst = null;
          lastSwappedSecond = null;
          wasDragSwap = false;
          OnGridUnsolvable?.Invoke();
          yield return new WaitForSeconds(unsolvableResetDelay);
          ResetGridSilent();
          yield break;
        }
        else {
          Debug.Log("<color=red>ZEN GAME OVER!</color> No reshuffles remaining.");
          GameManager.Instance.ZenGameOver();
          yield break;
        }
      }
      else {
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

  private void ResetGridSilent() {
    StartCoroutine(ResetGridWithEffect());
  }

  private IEnumerator ResetGridWithEffect() {
    var isZenMode = GameManager.Instance != null
                    && GameManager.Instance.CurrentMode == GameManager.GameMode.Zen;

    if (isZenMode) {
      // Zen reshuffle: only re-roll non-locked tiles, preserve locked tiles in place
      yield return StartCoroutine(ZenResetGridWithEffect());
      yield break;
    }

    // Arcade mode: full grid reset (original behavior)
    Debug.Log("<color=yellow>Grid reset with visual effect (no points awarded)</color>");

    var allTiles = new List<Tile>();
    for (var y = 0; y < gridHeight; y++)
    for (var x = 0; x < gridWidth; x++) {
      if (grid[x, y] != null) {
        allTiles.Add(grid[x, y]);
      }
    }

    var flashDuration = 0.3f;
    var elapsed = 0f;
    while (elapsed < flashDuration) {
      elapsed += Time.deltaTime;
      var t = Mathf.PingPong(elapsed * 8f, 1f);
      var flashColor = Color.Lerp(Color.white, new Color(1f, 0.3f, 0.3f), t);

      foreach (var tile in allTiles) {
        if (tile != null) {
          var img = tile.GetComponent<Image>();
          if (img != null) {
            img.color = flashColor;
          }
        }
      }

      yield return null;
    }

    var fallDuration = 0.4f;
    elapsed = 0f;

    var originalPositions = new Dictionary<Tile, Vector2>();
    foreach (var tile in allTiles) {
      if (tile != null) {
        originalPositions[tile] = tile.GetRectTransform().anchoredPosition;
      }
    }

    while (elapsed < fallDuration) {
      elapsed += Time.deltaTime;
      var t = elapsed / fallDuration;

      foreach (var tile in allTiles) {
        if (tile != null && originalPositions.ContainsKey(tile)) {
          var rt = tile.GetRectTransform();
          var originalPos = originalPositions[tile];
          var shake = Mathf.Sin(elapsed * 50f) * 5f * scaleFactor * (1f - t);
          var fallDistance = 800f * scaleFactor * t * t;

          rt.anchoredPosition = originalPos + new Vector2(shake, -fallDistance);

          // Fade tile background and overlays (shine, glow)
          var img = tile.GetComponent<Image>();
          if (img != null) {
            var c = img.color;
            c.a = 1f - t;
            img.color = c;
          }

          tile.SetOverlayAlpha(1f - t);

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
  private IEnumerator ZenResetGridWithEffect() {
    Debug.Log("<color=cyan>[Zen]</color> Reshuffle — preserving locked tiles");

    // Collect non-locked tiles for flash animation
    var freeTiles = new List<Tile>();
    for (var y = 0; y < gridHeight; y++)
    for (var x = 0; x < gridWidth; x++) {
      if (grid[x, y] != null && !grid[x, y].IsLocked) {
        freeTiles.Add(grid[x, y]);
      }
    }

    // Flash only free tiles
    var flashDuration = 0.3f;
    var elapsed = 0f;
    while (elapsed < flashDuration) {
      elapsed += Time.deltaTime;
      var t = Mathf.PingPong(elapsed * 8f, 1f);
      var flashColor = Color.Lerp(Color.white, new Color(0.3f, 0.8f, 1f), t); // Cyan flash for Zen

      foreach (var tile in freeTiles) {
        if (tile != null) {
          var img = tile.GetComponent<Image>();
          if (img != null) {
            img.color = flashColor;
          }
        }
      }

      yield return null;
    }

    // Re-roll free tile values (try up to 500 times for a valid board)
    var foundValid = false;
    for (var attempt = 0; attempt < 500; attempt++) {
      // Re-roll all non-locked tiles
      foreach (var tile in freeTiles) {
        if (tile != null && !tile.IsLocked) {
          var newValue = tileWeightManager.GetWeightedRandomValue();
          tile.SetValue(newValue);
        }
      }

      // Check: no initial matches AND has valid moves
      var hasMatch = FindFirstZenMatch() != null;
      var hasValidMoves = matchChecker.HasValidMoves();

      if (!hasMatch && hasValidMoves) {
        foundValid = true;
        Debug.Log($"<color=cyan>[Zen]</color> Valid reshuffle found on attempt {attempt + 1}");
        break;
      }
    }

    if (!foundValid) {
      Debug.LogWarning("[Zen] Could not find ideal reshuffle in 500 attempts — using last result.");
    }

    // Brief scale-bounce on free tiles to visually confirm the reshuffle
    foreach (var tile in freeTiles) {
      if (tile != null) {
        StartCoroutine(AnimationUtilities.PunchScale(tile.transform, 1.15f, 0.12f));
      }
    }

    yield return new WaitForSeconds(0.15f);

    // Resume match processing (shouldn't find matches, but safety check)
    StartCoroutine(ProcessMatchesCoroutine());
  }

  private IEnumerator AnimateSolveSequence (HashSet<Tile> tiles, MatchResult result, int cascadeCount = 1) {
    if (GameManager.Instance != null) {
      GameManager.Instance.IsSolveAnimationPlaying = true;
    }

    // Arcade cascade speed ramp: each cascade level plays faster (floor at 60% of original)
    var speedScale = 1f;
    if (cascadeCount >= 2 && GameManager.Instance != null
                          && GameManager.Instance.CurrentMode == GameManager.GameMode.Arcade)
      // cascadeCount 2 = 0.85x, 3 = 0.72x, 4 = 0.61x, 5+ = 0.60x (floor)
    {
      speedScale = Mathf.Max(0.60f, Mathf.Pow(0.85f, cascadeCount - 1));
    }

    // Fire beam flash (non-blocking — animates on its own, overlaps with convergence)
    if (GridVFX.Instance != null) {
      StartCoroutine(GridVFX.Instance.PlayLineSweeps(result, tileSize, tileSpacing));
    }

    // Brief pause so the beam burst registers visually before convergence starts
    yield return new WaitForSeconds(0.08f * speedScale);

    // Trigger avatar solve animation immediately when converge starts
    AvatarManager.Instance?.OnSolve();

    AudioManager.Instance?.PlayConvergenceSound();

    var centerPos = CalculateMatchCenter(tiles, result);

    var originalPositions = new Dictionary<Tile, Vector2>();
    var originalTextColors = new Dictionary<Tile, Color>();

    foreach (var tile in tiles) {
      if (tile != null) {
        originalPositions[tile] = tile.GetRectTransform().anchoredPosition;
        var numText = tile.GetComponentInChildren<TMPro.TMP_Text>();
        if (numText != null) {
          originalTextColors[tile] = numText.color;
        }
      }
    }

    var scaledConvergeDuration = solveConvergeDuration * speedScale;
    var elapsed = 0f;
    while (elapsed < scaledConvergeDuration) {
      elapsed += Time.deltaTime;
      var t = elapsed / scaledConvergeDuration;
      var easedT = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

      foreach (var tile in tiles) {
        if (tile != null && originalPositions.ContainsKey(tile)) {
          var rt = tile.GetRectTransform();
          var startPos = originalPositions[tile];

          var spiralAngle = easedT * Mathf.PI * 0.5f;
          var toCenter = centerPos - startPos;
          var dist = toCenter.magnitude * (1f - easedT);
          var spiralOffset = new Vector2(
            Mathf.Sin(spiralAngle) * dist * 0.1f,
            Mathf.Cos(spiralAngle) * dist * 0.1f
          );

          rt.anchoredPosition = Vector2.Lerp(startPos, centerPos, easedT) + spiralOffset * (1f - easedT);

          var scale = Mathf.Lerp(1f, convergeShrinkAmount, easedT);
          tile.transform.localScale = Vector3.one * scale;
          tile.transform.localEulerAngles = new Vector3(0, 0, easedT * 180f);

          // Fade tile background and overlays (shine, glow)
          var img = tile.GetComponent<Image>();
          if (img != null) {
            img.color = new Color(0.85f, 0.85f, 0.85f, 1f - easedT);
          }

          tile.SetOverlayAlpha(1f - easedT);

          var numText = tile.GetComponentInChildren<TMPro.TMP_Text>();
          if (numText != null && originalTextColors.ContainsKey(tile)) {
            var originalColor = originalTextColors[tile];
            var brightenedColor = Color.Lerp(originalColor, Color.white, easedT);
            var fadeStart = 0.4f;
            var alphaT = Mathf.Clamp01((easedT - fadeStart) / (1f - fadeStart));
            numText.color = new Color(brightenedColor.r, brightenedColor.g, brightenedColor.b, 1f - alphaT);
          }
        }
      }

      yield return null;
    }

    // === Chain tracking, SFX, and shake — done ONCE regardless of match count ===
    var consecutiveCount = gridValidation.RegisterMatch();

    // Pass cascadeCount for pitch shifting (ramps up during cascade chains)
    AudioManager.Instance?.PlayTenPopSound(cascadeCount);
    if (GridVFX.Instance != null) {
      GridVFX.Instance.TriggerShake(consecutiveCount);
      GridVFX.Instance.PulseAmbientParticles();
    }

    // Compute cascade BP for popup display (Arcade cascades only)
    // NextCascadeBP reads the counter before it increments in OnMatchCleared
    var cascadeBP = 0;
    if (cascadeCount >= 2 && GameManager.Instance != null
                          && GameManager.Instance.CurrentMode == GameManager.GameMode.Arcade) {
      cascadeBP = GameManager.Instance.NextCascadeBP;
    }

    // Spawn popup + explosion for matched lines, with per-line sums
    var lineData = GetPerLineCentersWithSums(result);
    if (lineData.Count <= 1) {
      // Single match — full "10" popup animation at convergence center
      var singleSum = lineData.Count > 0 ? lineData[0].sum : 10;
      yield return StartCoroutine(ShowTenEffectSpectacular(centerPos, singleSum, cascadeBP));
    }
    else {
      // Multiple simultaneous matches — skip individual popups, go straight to combo merge.
      // PlayComboMerge handles everything: spawning numbers, flying them inward,
      // showing the combined total, and particle explosions — all within ~0.55s
      yield return StartCoroutine(PlayComboMerge(lineData, cascadeBP));
    }

    if (GameManager.Instance != null) {
      GameManager.Instance.IsSolveAnimationPlaying = false;
    }
  }

  private Vector2 CalculateMatchCenter (HashSet<Tile> tiles, MatchResult result) {
    // For single-line matches, center on that line
    if (result.TotalLines == 1) {
      if (result.matchedRows.Count == 1) {
        var row = result.matchedRows[0];
        var midX = gridWidth / 2;
        return GridToWorldPosition(midX, row);
      }
      else if (result.matchedColumns.Count == 1) {
        var col = result.matchedColumns[0];
        var midY = gridHeight / 2;
        return GridToWorldPosition(col, midY);
      }
    }

    // For multiple simultaneous matches, center on all matched tiles
    var sum = Vector2.zero;
    var count = 0;
    foreach (var tile in tiles) {
      if (tile != null) {
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
  private List<Vector2> GetPerLineCenters (MatchResult result) {
    var centers = new List<Vector2>();
    var midX = gridWidth / 2;
    var midY = gridHeight / 2;

    foreach (var row in result.matchedRows) {
      centers.Add(GridToWorldPosition(midX, row));
    }

    foreach (var col in result.matchedColumns) {
      centers.Add(GridToWorldPosition(col, midY));
    }

    return centers;
  }

  /// <summary>
  /// Get center positions AND sums for each matched line (rows first, then columns).
  /// </summary>
  private List<(Vector2 center, int sum)> GetPerLineCentersWithSums (MatchResult result) {
    var data = new List<(Vector2, int)>();
    var midX = gridWidth / 2;
    var midY = gridHeight / 2;

    foreach (var row in result.matchedRows) {
      data.Add((GridToWorldPosition(midX, row), result.rowSums.ContainsKey(row) ? result.rowSums[row] : 10));
    }

    foreach (var col in result.matchedColumns) {
      data.Add((GridToWorldPosition(col, midY), result.columnSums.ContainsKey(col) ? result.columnSums[col] : 10));
    }

    return data;
  }

  private IEnumerator ShowTenEffectSpectacular (Vector2 position, int lineSum = 10, int cascadeBP = 0) {
    // Chain tracking, SFX, and shake are handled in AnimateSolveSequence (once per cascade).
    // This method now only handles the visual popup + particle explosion per position.

    // Calculate scale based on consecutive matches + boost for higher sums
    var tenScale = gridValidation.GetTenScale(lineSum);
    Debug.Log(
      $"<color=yellow>Match sum: {lineSum}, Consecutive: {gridValidation.ConsecutiveCount}, Scale: {tenScale:F2}</color>");

    // Trigger particle explosion VFX immediately with the 10 text
    var currentMultiplier = GameManager.Instance?.CurrentMultiplier ?? 1f;
    TenExplosionVFX.Instance?.TriggerExplosion(position, currentMultiplier, gridContainer);

    var effectObjects = new List<GameObject>();

    // Cascade mode: show "+N" instead of the line sum (e.g. "+1", "+2")
    var isCascadePopup = cascadeBP > 0;
    var displayText = isCascadePopup ? $"+{cascadeBP}" : lineSum.ToString();
    var fontScale = isCascadePopup ? 0.6f : 1f; // Smaller text for cascade BP

    var tenObj = new GameObject("TenEffect_Main");
    tenObj.transform.SetParent(gridContainer, false);
    effectObjects.Add(tenObj);

    var tenRT = tenObj.AddComponent<RectTransform>();
    tenRT.anchoredPosition = position;
    tenRT.sizeDelta = new Vector2(280f * scaleFactor * tenScale, 170f * scaleFactor * tenScale);

    TMPro.TMP_Text tenText = tenObj.AddComponent<TMPro.TextMeshProUGUI>();
    tenText.text = displayText;
    tenText.fontSize = 120 * scaleFactor * tenScale * fontScale;
    tenText.fontStyle = TMPro.FontStyles.Bold;
    tenText.alignment = TMPro.TextAlignmentOptions.Center;

    // Color tinting: cascade uses white, normal uses sum-based color
    var sumColor = isCascadePopup ? new Color(1f, 1f, 1f, 0.95f) : GetSumColor(lineSum);
    tenText.color = sumColor;
    if (!isCascadePopup) {
      tenText.enableVertexGradient = true;
      var sumColorLight = Color.Lerp(sumColor, Color.white, 0.5f);
      tenText.colorGradient = new TMPro.VertexGradient(
        sumColorLight,
        sumColorLight,
        sumColor,
        sumColor
      );
    }

    var glowObj = new GameObject("TenEffect_Glow");
    glowObj.transform.SetParent(gridContainer, false);
    glowObj.transform.SetSiblingIndex(tenObj.transform.GetSiblingIndex());
    effectObjects.Add(glowObj);

    var glowRT = glowObj.AddComponent<RectTransform>();
    glowRT.anchoredPosition = position;
    glowRT.sizeDelta = new Vector2(280f * scaleFactor * tenScale, 170f * scaleFactor * tenScale);

    TMPro.TMP_Text glowText = glowObj.AddComponent<TMPro.TextMeshProUGUI>();
    glowText.text = displayText;
    glowText.fontSize = 130 * scaleFactor * tenScale * fontScale;
    glowText.fontStyle = TMPro.FontStyles.Bold;
    var glowColor = isCascadePopup ? Color.white : GetSumColor(lineSum);
    glowText.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.4f);
    glowText.alignment = TMPro.TextAlignmentOptions.Center;

    List<(RectTransform rt, Image img, Vector2 velocity, float rotSpeed)> sparkles = new();

    for (var i = 0; i < sparkleCount; i++) {
      var sparkle = new GameObject($"Sparkle_{i}");
      sparkle.transform.SetParent(gridContainer, false);
      effectObjects.Add(sparkle);

      var sRT = sparkle.AddComponent<RectTransform>();
      sRT.anchoredPosition = position;
      var size = Random.Range(8f, 16f) * scaleFactor;
      sRT.sizeDelta = new Vector2(size, size);
      sRT.localEulerAngles = new Vector3(0, 0, 45f);

      var sImg = sparkle.AddComponent<Image>();
      sImg.color = sparkleColor;
      sImg.raycastTarget = false;

      var angle = i / (float)sparkleCount * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
      var speed = Random.Range(150f, 300f) * scaleFactor;
      var vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
      var rotSpd = Random.Range(-360f, 360f);

      sparkles.Add((sRT, sImg, vel, rotSpd));
    }

    List<(RectTransform rt, Image img, float delay)> rings = new();

    for (var i = 0; i < burstRingCount; i++) {
      var ring = new GameObject($"Ring_{i}");
      ring.transform.SetParent(gridContainer, false);
      ring.transform.SetSiblingIndex(0);
      effectObjects.Add(ring);

      var rRT = ring.AddComponent<RectTransform>();
      rRT.anchoredPosition = position;
      rRT.sizeDelta = new Vector2(20f * scaleFactor, 20f * scaleFactor);

      var rImg = ring.AddComponent<Image>();
      rImg.color = new Color(tenGlowColor.r, tenGlowColor.g, tenGlowColor.b, 0.6f);
      rImg.raycastTarget = false;

      rings.Add((rRT, rImg, i * 0.08f));
    }

    tenObj.transform.localScale = Vector3.zero;
    glowObj.transform.localScale = Vector3.zero;

    var popDuration = 0.12f;
    var elapsed = 0f;

    while (elapsed < popDuration) {
      elapsed += Time.deltaTime;
      var t = elapsed / popDuration;
      var overshoot = 1f + Mathf.Sin(t * Mathf.PI) * 0.3f;
      var scale = Mathf.Lerp(0f, 1f, t) * overshoot;

      tenObj.transform.localScale = Vector3.one * scale;
      glowObj.transform.localScale = Vector3.one * scale * 1.3f;

      yield return null;
    }

    tenObj.transform.localScale = Vector3.one;
    glowObj.transform.localScale = Vector3.one * 1.2f;

    var mainDuration = solveShowTenDuration;
    elapsed = 0f;
    var startPos = position;
    var startColor = tenText.color;
    var startGlowColor = glowText.color;

    while (elapsed < mainDuration) {
      elapsed += Time.deltaTime;
      var t = elapsed / mainDuration;

      var floatY = Mathf.Sin(t * Mathf.PI) * 40f * scaleFactor;
      var pulse = 1f + Mathf.Sin(elapsed * 15f) * 0.08f;

      tenRT.anchoredPosition = startPos + new Vector2(0, floatY);
      tenObj.transform.localScale = Vector3.one * pulse;

      glowRT.anchoredPosition = startPos + new Vector2(0, floatY);
      var glowPulse = 1.2f + Mathf.Sin(elapsed * 12f) * 0.15f;
      glowObj.transform.localScale = Vector3.one * glowPulse;

      var glowAlpha = t < 0.3f ? Mathf.Lerp(0.4f, 0.7f, t / 0.3f) : Mathf.Lerp(0.7f, 0f, (t - 0.3f) / 0.7f);
      glowText.color = new Color(startGlowColor.r, startGlowColor.g, startGlowColor.b, glowAlpha);

      var textAlpha = t < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f);
      tenText.color = new Color(startColor.r, startColor.g, startColor.b, textAlpha);

      foreach (var (sRT, sImg, vel, rotSpd) in sparkles) {
        if (sRT == null) {
          continue;
        }

        var currentPos = sRT.anchoredPosition;
        var gravity = new Vector2(0, -200f * scaleFactor) * Time.deltaTime;
        sRT.anchoredPosition = currentPos + vel * Time.deltaTime + gravity;

        var currentRot = sRT.localEulerAngles.z;
        sRT.localEulerAngles = new Vector3(0, 0, currentRot + rotSpd * Time.deltaTime);

        var sparkleAlpha = 1f - t;
        var sparkleScale = Mathf.Lerp(1f, 0.3f, t);
        sRT.localScale = Vector3.one * sparkleScale;
        sImg.color = new Color(sparkleColor.r, sparkleColor.g, sparkleColor.b, sparkleAlpha);
      }

      foreach (var (rRT, rImg, delay) in rings) {
        if (rRT == null) {
          continue;
        }

        var ringT = Mathf.Clamp01((elapsed - delay) / (mainDuration * 0.6f));
        if (ringT > 0) {
          var ringSize = Mathf.Lerp(20f * scaleFactor, 200f * scaleFactor, ringT);
          rRT.sizeDelta = new Vector2(ringSize, ringSize);

          var ringAlpha = Mathf.Lerp(0.6f, 0f, ringT);
          rImg.color = new Color(rImg.color.r, rImg.color.g, rImg.color.b, ringAlpha);
        }
      }

      yield return null;
    }

    foreach (var obj in effectObjects) {
      if (obj != null) {
        Destroy(obj);
      }
    }
  }

  /// <summary>
  /// Get a color for a line sum value (used for popup text tinting).
  /// </summary>
  private Color GetSumColor (int sum) {
    return sum switch {
      10 => new Color(1f, 0.9f, 0.3f, 1f), // Gold (classic)
      20 => new Color(1f, 0.6f, 0.15f, 1f), // Orange
      30 => new Color(0.85f, 0.3f, 0.85f, 1f), // Purple
      40 => new Color(1f, 0.2f, 0.2f, 1f), // Red
      _ => new Color(1f, 1f, 0.8f, 1f) // White-gold fallback
    };
  }

  /// <summary>
  /// Get a color for the combo merged number based on line count.
  /// </summary>
  private Color GetComboColor (int comboCount) {
    return comboCount switch {
      2 => new Color(1f, 0.75f, 0.15f, 1f), // Bright orange-gold
      3 => new Color(0.9f, 0.35f, 0.9f, 1f), // Purple
      4 => new Color(1f, 0.25f, 0.25f, 1f), // Red
      _ => new Color(1f, 0.1f, 0.1f, 1f) // Deep red (5+ lines)
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
  private IEnumerator PlayComboMerge (List<(Vector2 center, int sum)> lineData, int cascadeBP = 0) {
    var lineCount = lineData.Count;
    var totalSum = 0;
    foreach (var (_, sum) in lineData) {
      totalSum += sum;
    }

    var isUltraCombo = lineCount >= 5;
    var isCascadeCombo = cascadeBP > 0 && !isUltraCombo;

    // Cascade combo: show total cascade BP for all lines in this step
    string displayText;
    if (isCascadeCombo) {
      var totalCascadeBP = 0;
      for (var i = 0; i < lineCount; i++) {
        totalCascadeBP += cascadeBP + i;
      }

      displayText = $"+{totalCascadeBP}";
    }
    else {
      displayText = isUltraCombo ? "1000" : totalSum.ToString();
    }

    // Play combo sound immediately
    if (isUltraCombo) {
      AudioManager.Instance?.PlayUltraComboSound();
    }
    else {
      AudioManager.Instance?.PlayComboSound();
    }

    // Fire particle explosions at each line center (replaces ShowTenEffectSpectacular's explosions)
    var currentMultiplier = GameManager.Instance?.CurrentMultiplier ?? 1f;
    foreach (var (center, _) in lineData) {
      TenExplosionVFX.Instance?.TriggerExplosion(center, currentMultiplier, gridContainer);
    }

    // Create number text at each line center (instantly visible, no pop-in delay)
    var mergeObjects = new List<GameObject>();
    List<(RectTransform rt, Vector2 startPos)> mergeItems = new();

    for (var i = 0; i < lineData.Count; i++) {
      var (center, sum) = lineData[i];
      var numObj = new GameObject($"ComboNumber_{i}");
      numObj.transform.SetParent(gridContainer, false);
      mergeObjects.Add(numObj);

      var rt = numObj.AddComponent<RectTransform>();
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
    var gridCenter = Vector2.zero;
    var mergeDuration = 0.2f;
    var mergeElapsed = 0f;

    while (mergeElapsed < mergeDuration) {
      mergeElapsed += Time.deltaTime;
      var t = mergeElapsed / mergeDuration;
      var easeT = t * t; // Ease-in (accelerating)

      for (var i = 0; i < mergeItems.Count; i++) {
        if (mergeObjects[i] == null) {
          continue;
        }

        var (rt, startPos) = mergeItems[i];
        rt.anchoredPosition = Vector2.Lerp(startPos, gridCenter, easeT);

        var scale = Mathf.Lerp(1f, 0.3f, easeT);
        mergeObjects[i].transform.localScale = Vector3.one * scale;

        var txt = mergeObjects[i].GetComponent<TMPro.TMP_Text>();
        if (txt != null) {
          var c = txt.color;
          c.a = Mathf.Lerp(1f, 0f, easeT);
          txt.color = c;
        }
      }

      yield return null;
    }

    foreach (var obj in mergeObjects) {
      if (obj != null) {
        Destroy(obj);
      }
    }

    // === Phase 2: Merged number pop-in at center (0.1s) ===
    var finalObj = new GameObject("ComboMergedNumber");
    finalObj.transform.SetParent(gridContainer, false);

    var finalRT = finalObj.AddComponent<RectTransform>();
    finalRT.anchoredPosition = gridCenter;
    var finalWidth = isUltraCombo ? 400f : 280f;
    finalRT.sizeDelta = new Vector2(finalWidth * scaleFactor, 170f * scaleFactor);

    TMPro.TMP_Text finalTxt = finalObj.AddComponent<TMPro.TextMeshProUGUI>();
    finalTxt.text = displayText;
    var finalFontSize = isCascadeCombo ? 80f : isUltraCombo ? 160f : 130f;
    finalTxt.fontSize = finalFontSize * scaleFactor;
    finalTxt.fontStyle = TMPro.FontStyles.Bold;
    finalTxt.alignment = TMPro.TextAlignmentOptions.Center;

    var comboColor = isCascadeCombo ? new Color(1f, 1f, 1f, 0.95f)
      : isUltraCombo ? new Color(1f, 0.15f, 0.15f, 1f) : GetComboColor(lineCount);
    finalTxt.color = comboColor;
    if (!isCascadeCombo) {
      finalTxt.enableVertexGradient = true;
      var comboLight = Color.Lerp(comboColor, Color.white, 0.5f);
      finalTxt.colorGradient = new TMPro.VertexGradient(comboLight, comboLight, comboColor, comboColor);
    }

    // Glow behind merged number
    var finalGlow = new GameObject("ComboMergedGlow");
    finalGlow.transform.SetParent(gridContainer, false);
    finalGlow.transform.SetSiblingIndex(finalObj.transform.GetSiblingIndex());

    var glowRT = finalGlow.AddComponent<RectTransform>();
    glowRT.anchoredPosition = gridCenter;
    glowRT.sizeDelta = new Vector2(finalWidth * scaleFactor, 170f * scaleFactor);

    TMPro.TMP_Text glowTxt = finalGlow.AddComponent<TMPro.TextMeshProUGUI>();
    glowTxt.text = displayText;
    glowTxt.fontSize = (finalFontSize + 10f) * scaleFactor;
    glowTxt.fontStyle = TMPro.FontStyles.Bold;
    glowTxt.color = new Color(comboColor.r, comboColor.g, comboColor.b, 0.35f);
    glowTxt.alignment = TMPro.TextAlignmentOptions.Center;

    // Shake + center explosion on merge impact
    if (isUltraCombo) {
      if (GridVFX.Instance != null) {
        GridVFX.Instance.TriggerShake(10);
        GridVFX.Instance.PulseAmbientParticles();
      }

      TenExplosionVFX.Instance?.TriggerExplosion(gridCenter, 5f, gridContainer);
      TenExplosionVFX.Instance?.TriggerExplosion(gridCenter + new Vector2(50f, 30f), 5f, gridContainer);
      TenExplosionVFX.Instance?.TriggerExplosion(gridCenter + new Vector2(-50f, -30f), 5f, gridContainer);
    }
    else {
      if (GridVFX.Instance != null) {
        GridVFX.Instance.TriggerShake(lineCount + 1);
      }

      TenExplosionVFX.Instance?.TriggerExplosion(gridCenter, 2f, gridContainer);
    }

    // Quick pop-in with overshoot (0.1s)
    finalObj.transform.localScale = Vector3.zero;
    finalGlow.transform.localScale = Vector3.zero;
    var popDuration = 0.1f;
    var popElapsed = 0f;

    while (popElapsed < popDuration) {
      popElapsed += Time.deltaTime;
      var t = popElapsed / popDuration;
      var overshoot = Mathf.Lerp(0f, 1f, t) * (1f + Mathf.Sin(t * Mathf.PI) * 0.5f);
      finalObj.transform.localScale = Vector3.one * overshoot;
      finalGlow.transform.localScale = Vector3.one * overshoot * 1.2f;
      yield return null;
    }

    finalObj.transform.localScale = Vector3.one;
    finalGlow.transform.localScale = Vector3.one * 1.2f;

    // === Phase 3: Brief hold + fade (0.25s total, or 0.45s for ultra) ===
    var holdAndFadeDuration = isUltraCombo ? 0.45f : 0.25f;
    var hfElapsed = 0f;
    var holdStart = gridCenter;
    var fadeStartT = 0.5f; // Fade begins at 50% of hold duration

    while (hfElapsed < holdAndFadeDuration) {
      hfElapsed += Time.deltaTime;
      var t = hfElapsed / holdAndFadeDuration;

      // Float upward gently
      finalRT.anchoredPosition = holdStart + new Vector2(0, Mathf.Sin(t * Mathf.PI) * 15f * scaleFactor);
      var pulse = 1f + Mathf.Sin(hfElapsed * 14f) * 0.04f;
      finalObj.transform.localScale = Vector3.one * pulse;

      // Glow pulse + fade
      var glowPulse = 1.2f + Mathf.Sin(hfElapsed * 11f) * 0.08f;
      finalGlow.transform.localScale = Vector3.one * glowPulse;
      var glowAlpha = Mathf.Lerp(0.35f, 0f, t);
      glowTxt.color = new Color(comboColor.r, comboColor.g, comboColor.b, glowAlpha);

      // Text fades out in second half
      if (t > fadeStartT) {
        var fadeT = (t - fadeStartT) / (1f - fadeStartT);
        var c = finalTxt.color;
        c.a = Mathf.Lerp(1f, 0f, fadeT);
        finalTxt.color = c;
        finalObj.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.2f, fadeT);
      }

      yield return null;
    }

    Destroy(finalObj);
    Destroy(finalGlow);
  }

  private void ClearMatchedTiles (HashSet<Tile> tiles) {
    foreach (var tile in tiles) {
      if (tile != null) {
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
  private IEnumerator DropTilesCoroutine() {
    var longestDuration = 0f;

    // For each column, compact all tiles downward in one pass
    for (var x = 0; x < gridWidth; x++) {
      var writeY = gridHeight - 1; // Bottom-most empty slot to fill

      // Scan from bottom to top, packing tiles down
      for (var readY = gridHeight - 1; readY >= 0; readY--) {
        if (grid[x, readY] != null) {
          if (readY != writeY) {
            var tileToMove = grid[x, readY];
            grid[x, writeY] = tileToMove;
            grid[x, readY] = null;
            tileToMove.GridY = writeY;

            var targetPos = GridToWorldPosition(x, writeY);
            var distance = Vector2.Distance(tileToMove.GetRectTransform().anchoredPosition, targetPos);
            var duration = Mathf.Clamp(0.25f * (distance / 200f), 0.12f, 0.35f);
            longestDuration = Mathf.Max(longestDuration, duration);

            StartCoroutine(AnimateTileFall(tileToMove, targetPos));
          }

          writeY--;
        }
      }
    }

    // Wait for the longest fall to finish (capped at 0.35s + elastic bounce time)
    if (longestDuration > 0f) {
      yield return new WaitForSeconds(longestDuration + 0.08f);
    }
  }

  /// <summary>
  /// Animate a single tile falling to target position.
  /// Time-based (0.25s) with EaseOutCubic for a smooth deceleration into landing.
  /// </summary>
  private IEnumerator AnimateTileFall (Tile tile, Vector2 targetPosition) {
    if (tile == null) {
      yield break;
    }

    var rt = tile.GetRectTransform();
    var startPos = rt.anchoredPosition;
    // Time-based duration: 0.25s baseline, slightly longer for big falls, capped at 0.35s
    var distance = Vector2.Distance(startPos, targetPosition);
    var duration = Mathf.Clamp(0.25f * (distance / 200f), 0.12f, 0.35f);

    var elapsed = 0f;
    while (elapsed < duration) {
      elapsed += Time.deltaTime;
      var t = Mathf.Clamp01(elapsed / duration);
      var easedT = AnimationUtilities.EaseOutCubic(t);
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
  private IEnumerator TileLandBounce (Tile tile) {
    if (tile == null) {
      yield break;
    }

    var tr = tile.transform;

    // Sparkle on land
    GridVFX.Instance?.SpawnLandSparkle(tile.GetRectTransform().anchoredPosition, tileSize);

    // Elastic squash-stretch bounce over 0.08s
    var bounceDuration = 0.08f;
    var elapsed = 0f;
    while (elapsed < bounceDuration) {
      elapsed += Time.deltaTime;
      var t = Mathf.Clamp01(elapsed / bounceDuration);
      var eased = AnimationUtilities.EaseOutElastic(t);
      // Squash on impact (wide+short), then spring back to normal
      var scaleX = Mathf.LerpUnclamped(1.1f, 1f, eased);
      var scaleY = Mathf.LerpUnclamped(0.9f, 1f, eased);
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
  private IEnumerator SpawnNewTilesCoroutine() {
    // Collect all empty slots, ordered bottom-row-first so tiles fill from the bottom up
    List<(int x, int y)> emptySlots = new();

    for (var x = 0; x < gridWidth; x++)
    for (var y = gridHeight - 1; y >= 0; y--) {
      if (grid[x, y] == null) {
        emptySlots.Add((x, y));
      }
    }

    if (emptySlots.Count == 0) {
      yield break;
    }

    // Sort: bottom rows first, then left to right (natural fill order)
    emptySlots.Sort((a, b) => {
      if (a.y != b.y) {
        return b.y.CompareTo(a.y);
      }

      return a.x.CompareTo(b.x);
    });

    var lastFallDuration = 0f;

    for (var i = 0; i < emptySlots.Count; i++) {
      var (x, y) = emptySlots[i];

      // Spawn just above the grid
      var spawnPos = GridToWorldPosition(x, -1);
      var tileObj = Instantiate(tilePrefab, gridContainer);
      var newTile = tileObj.GetComponent<Tile>();

      if (newTile != null) {
        var value = tileWeightManager.GetWeightedRandomValue();

        // Light anti-cascade: one re-roll if this tile would complete a match
        if (gridValidation.WouldTileCompleteMatch(grid, gridWidth, gridHeight, x, y, value)) {
          value = tileWeightManager.GetWeightedRandomValue(); // Single re-roll, keep result either way
        }

        newTile.Initialize(value, x, y);
        newTile.SetPosition(spawnPos);
        newTile.GetRectTransform().sizeDelta = new Vector2(tileSize, tileSize);

        grid[x, y] = newTile;

        var targetPos = GridToWorldPosition(x, y);
        var distance = Vector2.Distance(spawnPos, targetPos);
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

  public Tile GetTile (int x, int y) {
    if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight) {
      return grid[x, y];
    }

    return null;
  }

  public Tile[,] GetGrid() {
    return grid;
  }

  public Vector2Int GetGridSize() {
    return new Vector2Int(gridWidth, gridHeight);
  }

  public Vector2 GridToWorldPosition (int gridX, int gridY) {
    var totalWidth = gridWidth * tileSize + (gridWidth - 1) * tileSpacing;
    var totalHeight = gridHeight * tileSize + (gridHeight - 1) * tileSpacing;
    var startX = -totalWidth / 2f + tileSize / 2f;
    var startY = totalHeight / 2f - tileSize / 2f;
    var posX = startX + gridX * (tileSize + tileSpacing);
    var posY = startY - gridY * (tileSize + tileSpacing);
    return new Vector2(posX, posY);
  }

  public void ClearGrid() {
    ClearHintParticles();

    if (grid != null) {
      for (var y = 0; y < gridHeight; y++)
      for (var x = 0; x < gridWidth; x++) {
        if (grid[x, y] != null) {
          Destroy(grid[x, y].gameObject);
          grid[x, y] = null;
        }
      }
    }

    selectedTile = null;
  }

  [ContextMenu("Print Grid State")]
  public void PrintGridState() {
    var output = "Grid State:\n";
    for (var y = 0; y < gridHeight; y++) {
      for (var x = 0; x < gridWidth; x++) {
        var tile = grid[x, y];
        output += tile != null ? tile.Value.ToString() : "X";
        output += " ";
      }

      output += "\n";
    }

    Debug.Log(output);
  }

  [ContextMenu("Check Sums")]
  public void DebugCheckSums() {
    for (var y = 0; y < gridHeight; y++) {
      var sum = 0;
      for (var x = 0; x < gridWidth; x++) {
        sum += grid[x, y].Value;
      }

      Debug.Log($"Row {y} sum: {sum}" + (sum == 10 ? " ← MATCH!" : ""));
    }

    for (var x = 0; x < gridWidth; x++) {
      var sum = 0;
      for (var y = 0; y < gridHeight; y++) {
        sum += grid[x, y].Value;
      }

      Debug.Log($"Column {x} sum: {sum}" + (sum == 10 ? " ← MATCH!" : ""));
    }
  }

  [ContextMenu("Force Show Hint")]
  public void DebugForceHint() {
    ShowHint();
  }

  [ContextMenu("Debug: Set Corner Tiles to Locked (10, 20, 30)")]
  public void DebugSetLockedTiles() {
    if (grid[0, 0] != null) {
      grid[0, 0].SetValue(10);
      Debug.Log("Tile [0,0] → 10 (Gold locked)");
    }

    if (grid[1, 0] != null) {
      grid[1, 0].SetValue(20);
      Debug.Log("Tile [1,0] → 20 (Purple locked)");
    }

    if (grid[2, 0] != null) {
      grid[2, 0].SetValue(30);
      Debug.Log("Tile [2,0] → 30 (Teal locked)");
    }

    Debug.Log("Locked tile test: try clicking/swiping the first 3 tiles in top row — should be unresponsive.");
  }

  public void ResetGame() {
    Debug.Log("GridManager.ResetGame() called - full reset with match processing");

    if (gridContainer == null) {
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

  public void SpawnGridOnly() {
    Debug.Log("GridManager.SpawnGridOnly() called - grid visible, no match processing yet");

    if (gridContainer == null) {
      Debug.LogError("GridManager: gridContainer is not assigned!");
      return;
    }

    // Reset consecutive 10s tracking and tile bag
    gridValidation.ResetConsecutive();
    tileWeightManager.ClearBag();

    ClearGrid();
    SpawnGrid();
  }

  public void StartMatchProcessing() {
    Debug.Log("GridManager.StartMatchProcessing() called - let the freebies flow!");
    ResetHintTimer();
    StartCoroutine(ProcessMatchesCoroutine());
  }

  // --- Test hooks (visible to Make10.Tests.PlayMode via InternalsVisibleTo) ---
  // The swap loop is otherwise pointer-driven (Tile.cs) with no public entry
  // point; these expose just enough for PlayMode integration tests to set up a
  // known board and drive a real swap without reflection.

  /// <summary>Grid column count (test hook).</summary>
  internal int GridColumns => gridWidth;

  /// <summary>Grid row count (test hook).</summary>
  internal int GridRows => gridHeight;

  /// <summary>Tile at a cell, or null if empty (test hook).</summary>
  internal Tile GetTileAt (int x, int y) {
    return grid[x, y];
  }

  /// <summary>True while a swap/match/cascade is resolving (test hook).</summary>
  internal bool IsBusy => isProcessing;

  /// <summary>
  /// Run a player swap through the real chokepoint every tap-tap / swipe / drag
  /// swap funnels into, including match/score/cascade processing (test hook).
  /// </summary>
  internal void BeginSwap (Tile a, Tile b) {
    StartCoroutine(AnimatedSwapCoroutine(a, b));
  }

  #region Zen Save/Resume

  /// <summary>
  /// Write current grid tile values into save data (flat array, row-major).
  /// Called by GameManager.SaveZenState().
  /// </summary>
  public void SaveGridToData (GameManager.ZenSaveData save) {
    save.gridWidth = gridWidth;
    save.gridHeight = gridHeight;
    save.tileValues = new int[gridWidth * gridHeight];

    for (var y = 0; y < gridHeight; y++)
    for (var x = 0; x < gridWidth; x++) {
      var index = y * gridWidth + x;
      save.tileValues[index] = grid[x, y] != null ? grid[x, y].Value : 0;
    }
  }

  /// <summary>
  /// Restore the grid from saved data. Creates tiles with saved values/positions.
  /// Replaces SpawnGridOnly() for resume flow.
  /// </summary>
  public void RestoreGridFromSave (GameManager.ZenSaveData save) {
    Debug.Log($"GridManager.RestoreGridFromSave() — restoring {save.gridWidth}x{save.gridHeight} grid");

    if (gridContainer == null) {
      Debug.LogError("GridManager: gridContainer is not assigned!");
      return;
    }

    gridValidation.ResetConsecutive();
    ClearGrid();

    gridWidth = save.gridWidth;
    gridHeight = save.gridHeight;
    grid = new Tile[gridWidth, gridHeight];

    UpdateGridSizeFromDifficulty();

    // Calculate tile positions (same math as SpawnGrid)
    var totalWidth = gridWidth * tileSize + (gridWidth - 1) * tileSpacing;
    var totalHeight = gridHeight * tileSize + (gridHeight - 1) * tileSpacing;
    var startX = -totalWidth / 2f + tileSize / 2f;
    var startY = totalHeight / 2f - tileSize / 2f;

    for (var y = 0; y < gridHeight; y++)
    for (var x = 0; x < gridWidth; x++) {
      var index = y * gridWidth + x;
      var value = save.tileValues != null && index < save.tileValues.Length ? save.tileValues[index] : 0;

      var posX = startX + x * (tileSize + tileSpacing);
      var posY = startY - y * (tileSize + tileSpacing);

      // Create tile with prefab (same as CreateTile but with explicit value)
      var tileObj = Instantiate(tilePrefab, gridContainer);
      var tile = tileObj.GetComponent<Tile>();
      if (tile != null) {
        tile.Initialize(value, x, y);
        tile.SetPosition(new Vector2(posX, posY));

        var rt = tile.GetRectTransform();
        rt.sizeDelta = new Vector2(tileSize, tileSize);
      }

      grid[x, y] = tile;
    }

    // Initialize VFX system
    StartCoroutine(InitializeVFXDelayed());

    // Clear swap tracking for clean resume
    lastSwappedFirst = null;
    lastSwappedSecond = null;
    wasDragSwap = false;

    Debug.Log($"<color=green>[Zen Restore] Grid rebuilt with {save.tileValues?.Length ?? 0} tiles</color>");
  }

  /// <summary>
  /// Wait one frame for Canvas layout then initialize VFX.
  /// </summary>
  private IEnumerator InitializeVFXDelayed() {
    yield return null;
    if (GridVFX.Instance != null && gridContainer != null) {
      GridVFX.Instance.Initialize(gridContainer);
    }
  }

  #endregion
}