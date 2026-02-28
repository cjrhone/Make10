using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;
using System.Collections;

/// <summary>
/// Swipe direction enum for gesture controls.
/// </summary>
public enum SwipeDirection
{
    Up,
    Down,
    Left,
    Right
}

/// <summary>
/// Represents a single number tile in the Make 10 grid.
/// Handles its value, visual state, click and swipe interactions.
/// </summary>
public class Tile : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Visual References")]
    [SerializeField] private TMP_Text numberText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject selectionHighlight;
    [SerializeField] private Image enhancedGlowImage;
    
    [Header("Selection Pulse Settings")]
    [SerializeField] private float pulseMinScale = 1.05f;
    [SerializeField] private float pulseMaxScale = 1.12f;
    [SerializeField] private float pulseSpeed = 4f;
    [SerializeField] private float floatAmount = 8f; // How much the tile floats up/down
    [SerializeField] private float floatSpeed = 3f; // Speed of floating animation

    [Header("Enhanced Glow Settings")]
    [SerializeField] private float glowPulseSpeed = 2f;
    [SerializeField] private float glowMinAlpha = 0.3f;
    [SerializeField] private float glowMaxAlpha = 0.7f;
    [SerializeField] private float glowSize = 1.3f; // Scale relative to tile

    [Header("Enhanced Number Pulse Settings")]
    [SerializeField] private float numberPulseSpeed = 3f;
    [SerializeField] private float numberPulseMinScale = 1.0f;
    [SerializeField] private float numberPulseMaxScale = 1.15f;
    [SerializeField] private float numberBrightenAmount = 0.3f; // How much to brighten the number

    [Header("Number Size")]
    [SerializeField] [Range(0.5f, 2f)] [Tooltip("Scale multiplier for tile numbers. 1 = default, 1.3 = 30% bigger.")]
    private float numberScale = 1f;

    [Header("Enhanced Number Shadow Settings")]
    [SerializeField] private Vector2 shadowOffset = new Vector2(3f, -3f);
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.5f);
    [SerializeField] private float shadowSoftness = 0.5f; // Dilation for soft shadow effect

    [Header("Swipe Settings")]
    [SerializeField] private float swipeThreshold = 30f; // Minimum distance to register swipe
    
    // Properties
    public int Value { get; private set; }
    public int GridX { get; set; }
    public int GridY { get; set; }
    public bool IsSelected { get; private set; }
    public bool IsLocked => Value >= 10;

    // Events
    public static event Action<Tile> OnTileClicked;
    public static event Action<Tile, SwipeDirection> OnTileSwiped;
    public static event Action<Tile> OnTileDragStarted;
    public static event Action<Tile, Vector2> OnTileDragMoved;
    public static event Action<Tile> OnTileDragEnded;
    
    private RectTransform rectTransform;
    private Coroutine pulseCoroutine;
    private Coroutine glowCoroutine;
    private Coroutine deselectCoroutine;
    private Vector2 originalAnchoredPosition; // Store original position for floating
    private bool wasFloating = false; // Track if we actually started floating
    private bool isEnhanced = false; // Track if this tile's number is enhanced
    private TMP_Text shadowText; // Drop shadow for enhanced numbers

    // Swipe tracking
    private Vector2 swipeStartPos;
    private bool isSwiping = false;

    // Drag-swap tracking
    private bool isDragSwapping = false;
    private bool dragStartFired = false;
    private static readonly float dragActivationThreshold = 15f; // px movement before drag activates
    
    // Tile background - uniform grey for all tiles
    private static readonly Color TileBackgroundColor = new Color(0.85f, 0.85f, 0.85f);

    // Number text colors - Color math philosophy:
    // Primaries: 1 (Gold/Yellow), 2 (Blue), 4 (Red)
    // Secondaries: 3 (Green = 1+2), 5 (Orange = 1+4), 6 (Purple = 2+4)
    // Tertiaries: 7 (Teal = 2+3), 8 (Pink = 4+1+mix), 9 (Crimson = deep red)
    private static readonly Color[] NumberColors = new Color[10]
    {
        new Color(0.6f, 0.6f, 0.6f),     // 0 - Grey (neutral wildcard)
        new Color(0.85f, 0.65f, 0.1f),   // 1 - Gold (primary)
        new Color(0.15f, 0.4f, 0.9f),    // 2 - Blue (primary)
        new Color(0.2f, 0.7f, 0.3f),     // 3 - Green (1+2: Gold+Blue)
        new Color(0.85f, 0.45f, 0.35f),   // 4 - Coral (warm, softer than red)
        new Color(0.95f, 0.5f, 0.1f),    // 5 - Orange (1+4: Gold+Red)
        new Color(0.6f, 0.2f, 0.75f),    // 6 - Purple (2+4: Blue+Red)
        new Color(0.1f, 0.7f, 0.7f),     // 7 - Teal
        new Color(0.9f, 0.35f, 0.6f),    // 8 - Pink
        new Color(0.75f, 0.1f, 0.15f)    // 9 - Crimson
    };

    // Locked tile colors (for merged tiles in MakeZen mode)
    // Indexed by: (value / 10 - 1). Value 10 → index 0, value 20 → index 1, etc.
    private static readonly Color[] LockedTileColors = new Color[8]
    {
        new Color(0.94f, 0.82f, 0.37f),   // 10 - Gold (#f0d060)
        new Color(0.82f, 0.56f, 1.00f),   // 20 - Purple (#d090ff)
        new Color(0.31f, 0.91f, 0.82f),   // 30 - Teal (#50e8d0)
        new Color(1.00f, 0.37f, 0.37f),   // 40 - Red (#ff6060)
        new Color(1.00f, 0.62f, 0.19f),   // 50 - Orange (#ffa030)
        new Color(0.37f, 0.62f, 1.00f),   // 60 - Blue (#60a0ff)
        new Color(0.88f, 0.37f, 0.88f),   // 70 - Magenta (#e060e0)
        new Color(0.88f, 0.37f, 0.88f)    // 80+ - Magenta (same as 70)
    };

    /// <summary>Get the color for a locked tile by its value (10, 20, 30, etc.).</summary>
    private static Color GetLockedTileColor(int value)
    {
        if (value < 10) return Color.white;
        int index = Mathf.Min((value / 10) - 1, LockedTileColors.Length - 1);
        return LockedTileColors[index];
    }

    /// <summary>Safe color lookup: returns locked tier color for locked tiles, NumberColors for regular.</summary>
    private Color GetTileColor()
    {
        return IsLocked ? GetLockedTileColor(Value) : NumberColors[Value];
    }

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }

        if (numberText == null)
        {
            numberText = GetComponentInChildren<TMP_Text>(true);
        }

        if (selectionHighlight == null)
        {
            Transform highlightTransform = transform.Find("SelectionHighlight");
            if (highlightTransform != null)
            {
                selectionHighlight = highlightTransform.gameObject;
            }
        }

        // Create enhanced glow image if not assigned
        if (enhancedGlowImage == null)
        {
            CreateEnhancedGlow();
        }

        // Create shadow text for enhanced numbers
        if (shadowText == null && numberText != null)
        {
            CreateShadowText();
        }
    }

    /// <summary>
    /// Creates a drop shadow text element behind the main number text.
    /// </summary>
    private void CreateShadowText()
    {
        GameObject shadowObj = new GameObject("NumberShadow");
        shadowObj.transform.SetParent(transform, false);

        // Position shadow just behind the number text in sibling order
        shadowObj.transform.SetSiblingIndex(numberText.transform.GetSiblingIndex());

        RectTransform shadowRT = shadowObj.AddComponent<RectTransform>();
        // Copy the number text's rect transform settings
        RectTransform numberRT = numberText.GetComponent<RectTransform>();
        shadowRT.anchorMin = numberRT.anchorMin;
        shadowRT.anchorMax = numberRT.anchorMax;
        shadowRT.pivot = numberRT.pivot;
        shadowRT.anchoredPosition = numberRT.anchoredPosition + shadowOffset;
        shadowRT.sizeDelta = numberRT.sizeDelta;

        shadowText = shadowObj.AddComponent<TextMeshProUGUI>();
        shadowText.text = numberText.text;
        shadowText.fontSize = numberText.fontSize;
        shadowText.fontStyle = numberText.fontStyle;
        shadowText.font = numberText.font;
        shadowText.alignment = numberText.alignment;
        shadowText.enableAutoSizing = numberText.enableAutoSizing;
        shadowText.fontSizeMin = numberText.fontSizeMin;
        shadowText.fontSizeMax = numberText.fontSizeMax;
        shadowText.color = shadowColor;
        shadowText.raycastTarget = false;

        // Add slight dilation for softer shadow appearance
        shadowText.fontMaterial = new Material(numberText.fontMaterial);
        shadowText.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, shadowSoftness);

        // Start hidden (only show when enhanced)
        shadowObj.SetActive(false);
    }

    /// <summary>
    /// Creates the enhanced glow image behind the tile.
    /// </summary>
    private void CreateEnhancedGlow()
    {
        GameObject glowObj = new GameObject("EnhancedGlow");
        glowObj.transform.SetParent(transform, false);
        glowObj.transform.SetAsFirstSibling(); // Put behind everything

        RectTransform glowRect = glowObj.AddComponent<RectTransform>();
        glowRect.anchorMin = new Vector2(0.5f, 0.5f);
        glowRect.anchorMax = new Vector2(0.5f, 0.5f);
        glowRect.pivot = new Vector2(0.5f, 0.5f);
        glowRect.anchoredPosition = Vector2.zero;

        // Size relative to tile
        if (rectTransform != null)
        {
            glowRect.sizeDelta = rectTransform.sizeDelta * glowSize;
        }
        else
        {
            glowRect.sizeDelta = new Vector2(120f, 120f); // Default fallback
        }

        enhancedGlowImage = glowObj.AddComponent<Image>();
        enhancedGlowImage.raycastTarget = false;

        // Apply soft circular glow texture
        GlowTextureGenerator.ApplyCircularGlow(enhancedGlowImage, 64, 1.5f);

        // Start hidden
        glowObj.SetActive(false);
    }
    
    private void Start()
    {
        ForceResetVisuals();
    }
    
    /// <summary>
    /// Force all visuals to default state - call this on spawn
    /// </summary>
    private void ForceResetVisuals()
    {
        IsSelected = false;

        if (selectionHighlight != null)
        {
            selectionHighlight.SetActive(false);
        }

        transform.localScale = Vector3.one;

        StopPulse();
        StopGlowPulse();
    }
    
    /// <summary>
    /// Initialize the tile with a value and grid position.
    /// </summary>
    public void Initialize(int value, int gridX, int gridY)
    {
        Value = value;
        GridX = gridX;
        GridY = gridY;
        
        ForceResetVisuals();
        UpdateNumberDisplay();
    }
    
    /// <summary>
    /// Set the tile's numeric value. Regular tiles use 0-9; locked tiles (MakeZen) can be 10+.
    /// In a 5×5 grid with high locked tiles, sums above 70 are reachable (e.g., locked 40 + locked 30 + tiles).
    /// </summary>
    public void SetValue(int value)
    {
        Value = Mathf.Max(value, 0); // No upper cap — locked tiles can reach high sums
        UpdateNumberDisplay();
    }
    
    /// <summary>
    /// Update the number text display and background color.
    /// </summary>
    private void UpdateNumberDisplay()
    {
        if (numberText != null)
        {
            numberText.text = Value.ToString();
            numberText.color = GetTileColor();
            numberText.enabled = true;
            numberText.gameObject.SetActive(true);

            // Apply number scale (adjustable from Inspector on the Tile prefab)
            numberText.transform.localScale = Vector3.one * numberScale;
        }
        else
        {
            Debug.LogError($"Tile [{GridX},{GridY}]: No Text component found!", this);
        }

        // Set background: subtle tint for locked tiles, uniform grey for regular
        if (backgroundImage != null)
        {
            if (IsLocked)
            {
                backgroundImage.color = Color.Lerp(Color.white, GetLockedTileColor(Value), 0.12f);
            }
            else
            {
                backgroundImage.color = TileBackgroundColor;
            }
        }

        // Check if this tile should have enhanced glow (locked tiles get glow)
        UpdateEnhancedGlow();
    }

    /// <summary>
    /// Check if this tile's number has enhanced bonuses and show/hide glow accordingly.
    /// </summary>
    private void UpdateEnhancedGlow()
    {
        isEnhanced = IsLocked;

        if (enhancedGlowImage != null)
        {
            if (isEnhanced)
            {
                enhancedGlowImage.gameObject.SetActive(true);
                Color glowColor = GetLockedTileColor(Value);
                glowColor.a = glowMinAlpha;
                enhancedGlowImage.color = glowColor;
            }
            else
            {
                enhancedGlowImage.gameObject.SetActive(false);
            }
        }

        if (shadowText != null)
        {
            if (isEnhanced)
            {
                shadowText.gameObject.SetActive(true);
                shadowText.text = Value.ToString();
                Color lockedColor = GetLockedTileColor(Value);
                shadowText.color = Color.Lerp(lockedColor, Color.black, 0.65f);
            }
            else
            {
                shadowText.gameObject.SetActive(false);
            }
        }

        if (isEnhanced)
            StartGlowPulse();
        else
            StopGlowPulse();
    }

    /// <summary>
    /// Start the enhanced glow pulse animation.
    /// </summary>
    private void StartGlowPulse()
    {
        StopGlowPulse();
        if (isEnhanced && gameObject.activeInHierarchy)
        {
            glowCoroutine = StartCoroutine(GlowPulseCoroutine());
        }
    }

    /// <summary>
    /// Stop the enhanced glow pulse animation.
    /// </summary>
    private void StopGlowPulse()
    {
        if (glowCoroutine != null)
        {
            StopCoroutine(glowCoroutine);
            glowCoroutine = null;
        }

        // Reset number text scale and color
        if (numberText != null)
        {
            numberText.transform.localScale = Vector3.one * numberScale;
            numberText.color = GetTileColor();
        }

        // Reset shadow scale
        if (shadowText != null)
        {
            shadowText.transform.localScale = Vector3.one * numberScale;
        }
    }

    /// <summary>
    /// Animated glow pulsing for enhanced numbers - includes both glow and number text effects.
    /// </summary>
    private IEnumerator GlowPulseCoroutine()
    {
        Color baseNumberColor = GetTileColor();
        Color brightNumberColor = Color.Lerp(baseNumberColor, Color.white, numberBrightenAmount);

        while (isEnhanced && enhancedGlowImage != null && enhancedGlowImage.gameObject.activeSelf)
        {
            // Glow alpha pulse
            float glowT = (Mathf.Sin(Time.time * glowPulseSpeed) + 1f) / 2f;
            float alpha = Mathf.Lerp(glowMinAlpha, glowMaxAlpha, glowT);

            Color glowColor = GetTileColor();
            glowColor.a = alpha;
            enhancedGlowImage.color = glowColor;

            // Number text pulse (slightly different speed for visual interest)
            float numberT = (Mathf.Sin(Time.time * numberPulseSpeed) + 1f) / 2f;

            // Scale the number text and shadow together
            if (numberText != null)
            {
                float scale = Mathf.Lerp(numberPulseMinScale, numberPulseMaxScale, numberT) * numberScale;
                numberText.transform.localScale = Vector3.one * scale;

                // Brighten the number color
                numberText.color = Color.Lerp(baseNumberColor, brightNumberColor, numberT);

                // Scale shadow to match
                if (shadowText != null)
                {
                    shadowText.transform.localScale = Vector3.one * scale;
                }
            }

            yield return null;
        }

        // Reset number text when no longer enhanced
        if (numberText != null)
        {
            numberText.transform.localScale = Vector3.one * numberScale;
            numberText.color = GetTileColor();
        }

        // Reset shadow scale
        if (shadowText != null)
        {
            shadowText.transform.localScale = Vector3.one * numberScale;
        }
    }

    /// <summary>
    /// Refresh enhanced status (call when upgrades change).
    /// </summary>
    public void RefreshEnhancedStatus()
    {
        isEnhanced = IsLocked;

        if (enhancedGlowImage != null)
            enhancedGlowImage.gameObject.SetActive(isEnhanced);

        if (isEnhanced)
            StartGlowPulse();
        else
            StopGlowPulse();
    }
    
    /// <summary>
    /// Select this tile (visual highlight with pulse and float).
    /// </summary>
    public void Select()
    {
        if (IsLocked) return; // Locked tiles can't be selected

        IsSelected = true;

        // Cancel any in-progress deselect animation
        if (deselectCoroutine != null)
        {
            StopCoroutine(deselectCoroutine);
            deselectCoroutine = null;
        }

        // Store original position for floating
        originalAnchoredPosition = rectTransform.anchoredPosition;
        wasFloating = true;

        if (selectionHighlight != null)
        {
            selectionHighlight.SetActive(true);
        }

        // Snappy punch on first selection
        StartCoroutine(AnimationUtilities.PunchScale(transform, 1.15f, 0.1f));
        StartPulse();
    }
    
    /// <summary>
    /// Deselect this tile.
    /// </summary>
    public void Deselect()
    {
        IsSelected = false;

        if (selectionHighlight != null)
        {
            selectionHighlight.SetActive(false);
        }

        // Restore background color (tinted for locked tiles, grey for regular)
        if (backgroundImage != null)
        {
            if (IsLocked)
                backgroundImage.color = Color.Lerp(Color.white, GetLockedTileColor(Value), 0.12f);
            else
                backgroundImage.color = TileBackgroundColor;
        }

        StopPulse();

        // Animate scale back to 1.0 instead of instant reset
        if (deselectCoroutine != null)
            StopCoroutine(deselectCoroutine);
        deselectCoroutine = StartCoroutine(AnimateDeselect());

        // Only restore position if we were actually floating
        if (wasFloating && rectTransform != null)
        {
            rectTransform.anchoredPosition = originalAnchoredPosition;
            wasFloating = false;
        }
    }

    /// <summary>
    /// Smooth EaseOutCubic scale from current back to 1.0 over 0.08s.
    /// </summary>
    private IEnumerator AnimateDeselect()
    {
        float duration = 0.08f;
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = AnimationUtilities.EaseOutCubic(t);
            transform.localScale = Vector3.Lerp(startScale, Vector3.one, eased);
            yield return null;
        }

        transform.localScale = Vector3.one;
        deselectCoroutine = null;
    }
    
    /// <summary>
    /// Start the selection pulse animation.
    /// </summary>
    private void StartPulse()
    {
        StopPulse();
        pulseCoroutine = StartCoroutine(PulseCoroutine());
    }
    
    /// <summary>
    /// Stop the selection pulse animation.
    /// </summary>
    private void StopPulse()
    {
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }
    }
    
    /// <summary>
    /// Pulsing scale, color, and floating animation while selected.
    /// </summary>
    private IEnumerator PulseCoroutine()
    {
        Color baseColor = TileBackgroundColor;
        Color brightColor = Color.Lerp(baseColor, Color.white, 0.5f); // Brighten by 50%
        
        while (IsSelected)
        {
            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;
            
            // Pulse scale
            float scale = Mathf.Lerp(pulseMinScale, pulseMaxScale, t);
            transform.localScale = Vector3.one * scale;
            
            // Pulse background between grey and bright
            if (backgroundImage != null)
            {
                backgroundImage.color = Color.Lerp(baseColor, brightColor, t);
            }
            
            // Float up and down (using a different frequency for variety)
            float floatOffset = Mathf.Sin(Time.time * floatSpeed) * floatAmount;
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = originalAnchoredPosition + new Vector2(0, floatOffset);
            }
            
            yield return null;
        }
    }
    
    /// <summary>
    /// Handle click/tap input (only fires if not swiping).
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (IsLocked) return; // Locked tiles can't be clicked

        // Don't trigger click if we just did a swipe
        if (!isSwiping)
        {
            OnTileClicked?.Invoke(this);
        }
        
        // Reset for next interaction
        isSwiping = false;
    }
    
    /// <summary>
    /// Begin drag - record start position.
    /// </summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsLocked) return; // Locked tiles can't be dragged

        swipeStartPos = eventData.position;
        isSwiping = false;
        isDragSwapping = false;
        dragStartFired = false;
    }
    
    /// <summary>
    /// During drag - track movement and fire drag events once activation threshold is met.
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (IsLocked) return; // Locked tiles can't be dragged

        Vector2 currentDelta = eventData.position - swipeStartPos;

        // Check if we've crossed the activation threshold to start drag-swapping
        if (!isDragSwapping)
        {
            if (currentDelta.magnitude >= dragActivationThreshold)
            {
                isDragSwapping = true;
            }
            else
            {
                return; // Still in dead zone, wait for more movement
            }
        }

        // Fire drag started once
        if (!dragStartFired)
        {
            dragStartFired = true;
            OnTileDragStarted?.Invoke(this);
        }

        // Report continuous position to GridManager
        OnTileDragMoved?.Invoke(this, eventData.position);
    }
    
    /// <summary>
    /// End drag - either finish a drag-swap or fall through to swipe logic.
    /// </summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        if (IsLocked) return; // Locked tiles can't be dragged

        // If we were drag-swapping, finish the drag and suppress click
        if (isDragSwapping)
        {
            isDragSwapping = false;
            dragStartFired = false;
            isSwiping = true; // Suppress the OnPointerClick that follows
            OnTileDragEnded?.Invoke(this);
            StartCoroutine(ResetSwipingFlag());
            return;
        }

        // Otherwise, fall through to existing swipe logic
        Vector2 swipeEndPos = eventData.position;
        Vector2 swipeDelta = swipeEndPos - swipeStartPos;

        // Check if swipe distance meets threshold
        if (swipeDelta.magnitude < swipeThreshold)
        {
            isSwiping = false;
            return;
        }

        isSwiping = true;

        // Determine swipe direction based on which axis had more movement
        SwipeDirection direction;

        if (Mathf.Abs(swipeDelta.x) > Mathf.Abs(swipeDelta.y))
        {
            // Horizontal swipe
            direction = swipeDelta.x > 0 ? SwipeDirection.Right : SwipeDirection.Left;
        }
        else
        {
            // Vertical swipe
            direction = swipeDelta.y > 0 ? SwipeDirection.Up : SwipeDirection.Down;
        }

        Debug.Log($"Swipe detected on {this}: {direction}");
        OnTileSwiped?.Invoke(this, direction);

        // Reset swiping flag after a frame (in case OnPointerClick doesn't fire)
        StartCoroutine(ResetSwipingFlag());
    }
    
    /// <summary>
    /// Reset swiping flag after a frame delay.
    /// </summary>
    private IEnumerator ResetSwipingFlag()
    {
        yield return null; // Wait one frame
        isSwiping = false;
    }
    
    /// <summary>
    /// Set the tile's anchored position on the canvas.
    /// </summary>
    public void SetPosition(Vector2 position)
    {
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = position;
        }
    }
    
    /// <summary>
    /// Get the RectTransform for animation purposes.
    /// </summary>
    public RectTransform GetRectTransform()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }
        return rectTransform;
    }
    
    private void OnEnable()
    {
        // Re-check enhanced status and restart animations if needed
        if (isEnhanced)
        {
            StartGlowPulse();
        }
    }

    private void OnDisable()
    {
        StopGlowPulse();
        StopPulse();
    }

    /// <summary>
    /// Static method to refresh all tiles' enhanced status.
    /// Call this after purchasing upgrades that affect enhanced numbers.
    /// </summary>
    public static void RefreshAllEnhancedStatus()
    {
        Tile[] allTiles = FindObjectsByType<Tile>(FindObjectsSortMode.None);
        foreach (Tile tile in allTiles)
        {
            tile.RefreshEnhancedStatus();
        }
        Debug.Log($"[Tile] Refreshed enhanced status for {allTiles.Length} tiles");
    }

    public override string ToString()
    {
        return $"Tile[{GridX},{GridY}] = {Value}";
    }
}
