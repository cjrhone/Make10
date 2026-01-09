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
    
    [Header("Selection Pulse Settings")]
    [SerializeField] private float pulseMinScale = 1.05f;
    [SerializeField] private float pulseMaxScale = 1.12f;
    [SerializeField] private float pulseSpeed = 4f;
    [SerializeField] private float floatAmount = 8f; // How much the tile floats up/down
    [SerializeField] private float floatSpeed = 3f; // Speed of floating animation
    
    [Header("Swipe Settings")]
    [SerializeField] private float swipeThreshold = 30f; // Minimum distance to register swipe
    
    // Properties
    public int Value { get; private set; }
    public int GridX { get; set; }
    public int GridY { get; set; }
    public bool IsSelected { get; private set; }
    
    // Events
    public static event Action<Tile> OnTileClicked;
    public static event Action<Tile, SwipeDirection> OnTileSwiped;
    
    private RectTransform rectTransform;
    private Coroutine pulseCoroutine;
    private Vector2 originalAnchoredPosition; // Store original position for floating
    private bool wasFloating = false; // Track if we actually started floating
    
    // Swipe tracking
    private Vector2 swipeStartPos;
    private bool isSwiping = false;
    
    // Tile background - uniform grey for all tiles
    private static readonly Color TileBackgroundColor = new Color(0.85f, 0.85f, 0.85f); // Light grey
    
    // Number text colors - Color math philosophy:
    // Primaries: 1 (Gold/Yellow), 2 (Blue), 4 (Red)
    // Secondaries: 3 (Green = 1+2), 5 (Orange = 1+4), 6 (Purple = 2+4)
    private static readonly Color[] NumberColors = new Color[7]
    {
        new Color(0.6f, 0.6f, 0.6f),     // 0 - Grey (neutral wildcard)
        new Color(0.85f, 0.65f, 0.1f),   // 1 - Gold (primary)
        new Color(0.15f, 0.4f, 0.9f),    // 2 - Blue (primary)
        new Color(0.2f, 0.7f, 0.3f),     // 3 - Green (1+2: Gold+Blue)
        new Color(0.9f, 0.2f, 0.2f),     // 4 - Red (primary)
        new Color(0.95f, 0.5f, 0.1f),    // 5 - Orange (1+4: Gold+Red)
        new Color(0.6f, 0.2f, 0.75f)     // 6 - Purple (2+4: Blue+Red)
    };

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
    /// Set the tile's numeric value (0-6).
    /// </summary>
    public void SetValue(int value)
    {
        Value = Mathf.Clamp(value, 0, 6);
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
            numberText.color = NumberColors[Value]; // Colored numbers
            numberText.enabled = true;
            numberText.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError($"Tile [{GridX},{GridY}]: No Text component found!", this);
        }
        
        // Set uniform grey background for all tiles
        if (backgroundImage != null)
        {
            backgroundImage.color = TileBackgroundColor;
        }
    }
    
    /// <summary>
    /// Select this tile (visual highlight with pulse and float).
    /// </summary>
    public void Select()
    {
        IsSelected = true;
        
        // Store original position for floating
        originalAnchoredPosition = rectTransform.anchoredPosition;
        wasFloating = true;
        
        if (selectionHighlight != null)
        {
            selectionHighlight.SetActive(true);
        }
        
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
        
        // Restore uniform grey background
        if (backgroundImage != null)
        {
            backgroundImage.color = TileBackgroundColor;
        }
        
        StopPulse();
        transform.localScale = Vector3.one;
        
        // Only restore position if we were actually floating
        if (wasFloating && rectTransform != null)
        {
            rectTransform.anchoredPosition = originalAnchoredPosition;
            wasFloating = false;
        }
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
        swipeStartPos = eventData.position;
        isSwiping = false;
    }
    
    /// <summary>
    /// During drag - required for end drag to fire.
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        // Required for OnEndDrag to work, but we don't need to do anything here
    }
    
    /// <summary>
    /// End drag - calculate swipe direction and fire event.
    /// </summary>
    public void OnEndDrag(PointerEventData eventData)
    {
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
    
    public override string ToString()
    {
        return $"Tile[{GridX},{GridY}] = {Value}";
    }
}
