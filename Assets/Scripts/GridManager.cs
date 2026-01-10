using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages the 5x5 grid of tiles, handles spawning, swapping, and grid operations.
/// </summary>
public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int gridWidth = 5;
    [SerializeField] private int gridHeight = 5;
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
    [SerializeField] private float unsolvableResetDelay = 1f;
    
    [Header("Solve Animation Settings")]
    [SerializeField] private float solveConvergeDuration = 0.25f;
    [SerializeField] private float solveShowTenDuration = 0.5f;
    [SerializeField] private float convergeShrinkAmount = 0.7f;
    [SerializeField] private GameObject tenTextPrefab;
    
    [Header("Ten Effect Magic Settings")]
    [SerializeField] private int sparkleCount = 12;
    [SerializeField] private float sparkleDistance = 80f;
    [SerializeField] private float burstRingCount = 2;
    [SerializeField] private Color tenGlowColor = new Color(1f, 0.9f, 0.3f);
    [SerializeField] private Color sparkleColor = new Color(1f, 0.95f, 0.6f);
    
    [Header("Tile Value Weights (must sum to 1.0)")]
    [SerializeField] private float weight0 = 0.15f;
    [SerializeField] private float weight1 = 0.27f;
    [SerializeField] private float weight2 = 0.26f;
    [SerializeField] private float weight3 = 0.17f;
    [SerializeField] private float weight4 = 0.13f;
    [SerializeField] private float weight5 = 0.01f;
    [SerializeField] private float weight6 = 0.01f;

    private Tile[,] grid;
    private Tile selectedTile;
    private float[] weights;
    private bool isProcessing = false;
    
    public event System.Action OnGridUnsolvable;
    
    private void Awake()
    {
        weights = new float[] { weight0, weight1, weight2, weight3, weight4, weight5, weight6 };
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
        if (SceneFlowManager.Instance == null)
        {
            Debug.Log("No SceneFlowManager found - auto-starting grid for testing");
            SpawnGrid();
            StartCoroutine(ProcessMatchesCoroutine());
        }
    }
    
    public void SpawnGrid()
    {
        Debug.Log("GridManager.SpawnGrid() called");
        
        if (tilePrefab == null)
        {
            Debug.LogError("GridManager: tilePrefab is not assigned!");
            return;
        }
        
        ClearGrid();
        
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
        
        Debug.Log($"Grid spawned: {gridWidth}x{gridHeight}");
    }
    
    private Tile CreateTile(int gridX, int gridY, Vector2 position)
    {
        GameObject tileObj = Instantiate(tilePrefab, gridContainer);
        Tile tile = tileObj.GetComponent<Tile>();
        
        if (tile != null)
        {
            int value = GetWeightedRandomValue();
            tile.Initialize(value, gridX, gridY);
            tile.SetPosition(position);
            
            RectTransform rt = tile.GetRectTransform();
            rt.sizeDelta = new Vector2(tileSize, tileSize);
        }
        
        return tile;
    }
    
    private int GetWeightedRandomValue()
    {
        float roll = Random.value;
        float cumulative = 0f;
        
        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
                return i;
        }
        
        return 2;
    }
    
    private void HandleTileClicked(Tile tile)
    {
        if (isProcessing) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsGameActive) return;
        
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
        if (GameManager.Instance != null && !GameManager.Instance.IsGameActive) return;
        
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
        AudioManager.Instance?.PlaySwapSound();
        
        Vector2 posA = tileA.GetRectTransform().anchoredPosition;
        Vector2 posB = tileB.GetRectTransform().anchoredPosition;
        
        float elapsed = 0f;
        while (elapsed < tileSwapDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / tileSwapDuration;
            float smoothT = t * t * (3f - 2f * t);
            
            tileA.GetRectTransform().anchoredPosition = Vector2.Lerp(posA, posB, smoothT);
            tileB.GetRectTransform().anchoredPosition = Vector2.Lerp(posB, posA, smoothT);
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
    
    private IEnumerator ProcessMatchesCoroutine()
    {
        isProcessing = true;
        int cascadeCount = 0;
        
        GameManager.Instance?.OnCascadeStart();
        
        while (true)
        {
            if (matchChecker == null)
            {
                Debug.LogWarning("MatchChecker not assigned!");
                break;
            }
            
            MatchResult result = matchChecker.GetMatchResult();
            
            if (!result.HasMatches)
            {
                if (cascadeCount > 0)
                    Debug.Log($"Cascade complete! {cascadeCount} chain(s)");
                else
                    Debug.Log("No matches found.");
                break;
            }
            
            cascadeCount++;
            Debug.Log($"<color=yellow>MATCH {cascadeCount}!</color> " +
                    $"{result.matchedRows.Count} rows, {result.matchedColumns.Count} columns, " +
                    $"{result.TotalMatchedTiles} tiles");
            
            // NO SOUND HERE - moved to AnimateSolveSequence
            
            yield return StartCoroutine(AnimateSolveSequence(result.allMatchedTiles, result));
            
            ClearMatchedTiles(result.allMatchedTiles);
            
            GameManager.Instance?.OnMatchCleared(
                result.TotalMatchedTiles,
                result.matchedRows.Count,
                result.matchedColumns.Count
            );
            
            yield return new WaitForSeconds(postClearDelay);
            yield return StartCoroutine(DropTilesCoroutine());
            yield return StartCoroutine(SpawnNewTilesCoroutine());
            yield return new WaitForSeconds(0.1f);
        }
        
        GameManager.Instance?.OnCascadeEnd();
        
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
    
    private void ResetGridSilent()
    {
        StartCoroutine(ResetGridWithEffect());
    }
    
    private IEnumerator ResetGridWithEffect()
    {
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
                    float shake = Mathf.Sin(elapsed * 50f) * 5f * (1f - t);
                    float fallDistance = 800f * t * t;
                    
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
    /// Solve animation: tiles converge to center and show spectacular "10".
    /// CONVERGENCE SOUND plays at start, TEN POP SOUND plays when "10" appears.
    /// </summary>
    private IEnumerator AnimateSolveSequence(HashSet<Tile> tiles, MatchResult result)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.IsSolveAnimationPlaying = true;
        
        // *** CONVERGENCE SOUND ***
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
        
        // Phase 1: Converge tiles toward center with spiral motion
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
                    
                    // Add slight spiral motion
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
                    
                    // Rotation as they converge
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
        
        // Phase 2: Show spectacular "10" at center
        yield return StartCoroutine(ShowTenEffectSpectacular(centerPos));
        
        if (GameManager.Instance != null)
            GameManager.Instance.IsSolveAnimationPlaying = false;
    }
    
    private Vector2 CalculateMatchCenter(HashSet<Tile> tiles, MatchResult result)
    {
        if (result.matchedRows.Count > 0)
        {
            int row = result.matchedRows[0];
            int midX = gridWidth / 2;
            return GridToWorldPosition(midX, row);
        }
        else if (result.matchedColumns.Count > 0)
        {
            int col = result.matchedColumns[0];
            int midY = gridHeight / 2;
            return GridToWorldPosition(col, midY);
        }
        
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
    /// SPECTACULAR "10" effect with sparkles, burst rings, glow, and magic!
    /// </summary>
    private IEnumerator ShowTenEffectSpectacular(Vector2 position)
    {
        // *** TEN POP SOUND ***
        AudioManager.Instance?.PlayTenPopSound();
        
        // Container for all effect objects
        List<GameObject> effectObjects = new List<GameObject>();
        
        // === MAIN "10" TEXT ===
        GameObject tenObj = new GameObject("TenEffect_Main");
        tenObj.transform.SetParent(gridContainer, false);
        effectObjects.Add(tenObj);
        
        RectTransform tenRT = tenObj.AddComponent<RectTransform>();
        tenRT.anchoredPosition = position;
        tenRT.sizeDelta = new Vector2(200f, 120f);
        
        TMPro.TMP_Text tenText = tenObj.AddComponent<TMPro.TextMeshProUGUI>();
        tenText.text = "10";
        tenText.fontSize = 82;
        tenText.fontStyle = TMPro.FontStyles.Bold;
        tenText.color = tenGlowColor;
        tenText.alignment = TMPro.TextAlignmentOptions.Center;
        tenText.enableVertexGradient = true;
        tenText.colorGradient = new TMPro.VertexGradient(
            new Color(1f, 1f, 0.8f),      // Top left - bright
            new Color(1f, 1f, 0.8f),      // Top right - bright  
            new Color(1f, 0.8f, 0.2f),    // Bottom left - golden
            new Color(1f, 0.8f, 0.2f)     // Bottom right - golden
        );
        
        // === GLOW BEHIND TEXT ===
        GameObject glowObj = new GameObject("TenEffect_Glow");
        glowObj.transform.SetParent(gridContainer, false);
        glowObj.transform.SetSiblingIndex(tenObj.transform.GetSiblingIndex()); // Behind main text
        effectObjects.Add(glowObj);
        
        RectTransform glowRT = glowObj.AddComponent<RectTransform>();
        glowRT.anchoredPosition = position;
        glowRT.sizeDelta = new Vector2(200f, 120f);
        
        TMPro.TMP_Text glowText = glowObj.AddComponent<TMPro.TextMeshProUGUI>();
        glowText.text = "10";
        glowText.fontSize = 90;
        glowText.fontStyle = TMPro.FontStyles.Bold;
        glowText.color = new Color(1f, 0.95f, 0.5f, 0.4f);
        glowText.alignment = TMPro.TextAlignmentOptions.Center;
        
        // === SPARKLE PARTICLES ===
        List<(RectTransform rt, Image img, Vector2 velocity, float rotSpeed)> sparkles = 
            new List<(RectTransform, Image, Vector2, float)>();
        
        for (int i = 0; i < sparkleCount; i++)
        {
            GameObject sparkle = new GameObject($"Sparkle_{i}");
            sparkle.transform.SetParent(gridContainer, false);
            effectObjects.Add(sparkle);
            
            RectTransform sRT = sparkle.AddComponent<RectTransform>();
            sRT.anchoredPosition = position;
            float size = Random.Range(8f, 16f);
            sRT.sizeDelta = new Vector2(size, size);
            sRT.localEulerAngles = new Vector3(0, 0, 45f); // Diamond shape
            
            Image sImg = sparkle.AddComponent<Image>();
            sImg.color = sparkleColor;
            sImg.raycastTarget = false;
            
            // Random outward velocity
            float angle = (i / (float)sparkleCount) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
            float speed = Random.Range(150f, 300f);
            Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            float rotSpd = Random.Range(-360f, 360f);
            
            sparkles.Add((sRT, sImg, vel, rotSpd));
        }
        
        // === BURST RINGS ===
        List<(RectTransform rt, Image img, float delay)> rings = 
            new List<(RectTransform, Image, float)>();
        
        for (int i = 0; i < burstRingCount; i++)
        {
            GameObject ring = new GameObject($"Ring_{i}");
            ring.transform.SetParent(gridContainer, false);
            ring.transform.SetSiblingIndex(0); // Behind everything
            effectObjects.Add(ring);
            
            RectTransform rRT = ring.AddComponent<RectTransform>();
            rRT.anchoredPosition = position;
            rRT.sizeDelta = new Vector2(20f, 20f);
            
            Image rImg = ring.AddComponent<Image>();
            rImg.color = new Color(tenGlowColor.r, tenGlowColor.g, tenGlowColor.b, 0.6f);
            rImg.raycastTarget = false;
            
            rings.Add((rRT, rImg, i * 0.08f));
        }
        
        // === ANIMATION ===
        tenObj.transform.localScale = Vector3.zero;
        glowObj.transform.localScale = Vector3.zero;
        
        // Pop in with overshoot
        float popDuration = 0.12f;
        float elapsed = 0f;
        
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / popDuration;
            
            // Elastic overshoot
            float overshoot = 1f + Mathf.Sin(t * Mathf.PI) * 0.3f;
            float scale = Mathf.Lerp(0f, 1f, t) * overshoot;
            
            tenObj.transform.localScale = Vector3.one * scale;
            glowObj.transform.localScale = Vector3.one * scale * 1.3f;
            
            yield return null;
        }
        
        // Settle to normal
        tenObj.transform.localScale = Vector3.one;
        glowObj.transform.localScale = Vector3.one * 1.2f;
        
        // Main animation phase - sparkles fly out, rings expand, text pulses and floats
        float mainDuration = solveShowTenDuration;
        elapsed = 0f;
        Vector2 startPos = position;
        Color startColor = tenText.color;
        Color startGlowColor = glowText.color;
        
        while (elapsed < mainDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / mainDuration;
            
            // Text floats up and pulses
            float floatY = Mathf.Sin(t * Mathf.PI) * 40f;
            float pulse = 1f + Mathf.Sin(elapsed * 15f) * 0.08f;
            
            tenRT.anchoredPosition = startPos + new Vector2(0, floatY);
            tenObj.transform.localScale = Vector3.one * pulse;
            
            glowRT.anchoredPosition = startPos + new Vector2(0, floatY);
            float glowPulse = 1.2f + Mathf.Sin(elapsed * 12f) * 0.15f;
            glowObj.transform.localScale = Vector3.one * glowPulse;
            
            // Glow gets brighter then fades
            float glowAlpha = t < 0.3f 
                ? Mathf.Lerp(0.4f, 0.7f, t / 0.3f) 
                : Mathf.Lerp(0.7f, 0f, (t - 0.3f) / 0.7f);
            glowText.color = new Color(startGlowColor.r, startGlowColor.g, startGlowColor.b, glowAlpha);
            
            // Fade out main text in last 40%
            float textAlpha = t < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f);
            tenText.color = new Color(startColor.r, startColor.g, startColor.b, textAlpha);
            
            // Animate sparkles
            foreach (var (sRT, sImg, vel, rotSpd) in sparkles)
            {
                if (sRT == null) continue;
                
                // Move outward with gravity
                Vector2 currentPos = sRT.anchoredPosition;
                Vector2 gravity = new Vector2(0, -200f) * Time.deltaTime;
                sRT.anchoredPosition = currentPos + vel * Time.deltaTime + gravity;
                
                // Rotate
                float currentRot = sRT.localEulerAngles.z;
                sRT.localEulerAngles = new Vector3(0, 0, currentRot + rotSpd * Time.deltaTime);
                
                // Fade and shrink
                float sparkleAlpha = 1f - t;
                float sparkleScale = Mathf.Lerp(1f, 0.3f, t);
                sRT.localScale = Vector3.one * sparkleScale;
                sImg.color = new Color(sparkleColor.r, sparkleColor.g, sparkleColor.b, sparkleAlpha);
            }
            
            // Animate burst rings
            foreach (var (rRT, rImg, delay) in rings)
            {
                if (rRT == null) continue;
                
                float ringT = Mathf.Clamp01((elapsed - delay) / (mainDuration * 0.6f));
                if (ringT > 0)
                {
                    float ringSize = Mathf.Lerp(20f, 200f, ringT);
                    rRT.sizeDelta = new Vector2(ringSize, ringSize);
                    
                    float ringAlpha = Mathf.Lerp(0.6f, 0f, ringT);
                    rImg.color = new Color(rImg.color.r, rImg.color.g, rImg.color.b, ringAlpha);
                }
            }
            
            yield return null;
        }
        
        // Cleanup all effect objects
        foreach (GameObject obj in effectObjects)
        {
            if (obj != null)
                Destroy(obj);
        }
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
    
    private IEnumerator DropTilesCoroutine()
    {
        bool anyTileDropped = true;
        
        while (anyTileDropped)
        {
            anyTileDropped = false;
            List<Coroutine> dropAnimations = new List<Coroutine>();
            
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = gridHeight - 1; y >= 0; y--)
                {
                    if (grid[x, y] == null)
                    {
                        for (int above = y - 1; above >= 0; above--)
                        {
                            if (grid[x, above] != null)
                            {
                                Tile tileToMove = grid[x, above];
                                grid[x, y] = tileToMove;
                                grid[x, above] = null;
                                tileToMove.GridY = y;
                                
                                Vector2 targetPos = GridToWorldPosition(x, y);
                                dropAnimations.Add(StartCoroutine(AnimateTileFall(tileToMove, targetPos)));
                                anyTileDropped = true;
                                break;
                            }
                        }
                    }
                }
            }
            
            foreach (Coroutine c in dropAnimations)
                yield return c;
        }
    }
    
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
            float easedT = 1f - Mathf.Pow(1f - t, 2f);
            rt.anchoredPosition = Vector2.Lerp(startPos, targetPosition, easedT);
            yield return null;
        }
        
        rt.anchoredPosition = targetPosition;
        yield return StartCoroutine(TileLandBounce(tile));
    }
    
    private IEnumerator TileLandBounce(Tile tile)
    {
        if (tile == null) yield break;
        
        Transform t = tile.transform;
        t.localScale = new Vector3(1.1f, 0.9f, 1f);
        yield return new WaitForSeconds(0.05f);
        t.localScale = new Vector3(0.95f, 1.05f, 1f);
        yield return new WaitForSeconds(0.05f);
        t.localScale = Vector3.one;
    }
    
    private IEnumerator SpawnNewTilesCoroutine()
    {
        List<(int x, int y, Tile tile)> tilesToDrop = new List<(int, int, Tile)>();
        
        for (int x = 0; x < gridWidth; x++)
        {
            int spawnIndex = 0;
            for (int y = gridHeight - 1; y >= 0; y--)
            {
                if (grid[x, y] == null)
                {
                    Vector2 spawnPos = GridToWorldPosition(x, -1 - spawnIndex);
                    GameObject tileObj = Instantiate(tilePrefab, gridContainer);
                    Tile newTile = tileObj.GetComponent<Tile>();
                    
                    if (newTile != null)
                    {
                        int value = GetWeightedRandomValue();
                        newTile.Initialize(value, x, y);
                        newTile.SetPosition(spawnPos);
                        newTile.GetRectTransform().sizeDelta = new Vector2(tileSize, tileSize);
                        grid[x, y] = newTile;
                        tilesToDrop.Add((x, y, newTile));
                    }
                    spawnIndex++;
                }
            }
        }
        
        tilesToDrop.Sort((a, b) => {
            if (a.y != b.y) return b.y.CompareTo(a.y);
            return a.x.CompareTo(b.x);
        });
        
        for (int i = 0; i < tilesToDrop.Count; i++)
        {
            var (x, y, tile) = tilesToDrop[i];
            Vector2 targetPos = GridToWorldPosition(x, y);
            StartCoroutine(AnimateTileFall(tile, targetPos));
            yield return new WaitForSeconds(tileFallDelay);
        }
        
        yield return new WaitForSeconds(0.1f);
        Debug.Log("New tiles spawned");
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

    public void ResetGame()
    {
        Debug.Log("GridManager.ResetGame() called");
        
        if (gridContainer == null)
        {
            Debug.LogError("GridManager: gridContainer is not assigned!");
            return;
        }
        
        ClearGrid();
        SpawnGrid();
        StartCoroutine(ProcessMatchesCoroutine());
    }
}
