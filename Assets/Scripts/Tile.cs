using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;

/// <summary>
/// Represents a single number tile in the Make 10 grid.
/// Handles its value, visual state, and click interactions.
/// </summary>
public class Tile : MonoBehaviour, IPointerClickHandler
{
    [Header("Visual References")]
    [SerializeField] private TMP_Text numberText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject selectionHighlight; // Changed to GameObject for easier enable/disable
    
    [Header("Colors")]
    [SerializeField] private Color normalColor = new Color(0.6f, 0.6f, 0.65f); // Gray 
    [SerializeField] private Color selectedColor = new Color(0.95f, 0.85f, 0.4f); // Yellow
    
    // Properties
    public int Value { get; private set; }
    public int GridX { get; set; }
    public int GridY { get; set; }
    public bool IsSelected { get; private set; }
    
    // Events
    public static event Action<Tile> OnTileClicked;
    
    private RectTransform rectTransform;
    
    // Number colors - dark colors that show on gray background
    private static readonly Color[] NumberColors = new Color[6]
    {
        new Color(0.3f, 0.3f, 0.35f),   // 0 - Dark Gray
        new Color(0.15f, 0.35f, 0.75f), // 1 - Blue
        new Color(0.1f, 0.55f, 0.25f),  // 2 - Green
        new Color(0.8f, 0.45f, 0.1f),   // 3 - Orange
        new Color(0.75f, 0.15f, 0.15f), // 4 - Red
        new Color(0.85f, 0f, 0.85f)  // 5 - Purple

    };

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        
        // Auto-find background image
        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }
        
        // Auto-find text (search in children) - using TextMeshPro
        if (numberText == null)
        {
            numberText = GetComponentInChildren<TMP_Text>(true); // true = include inactive
        }
        
        // Auto-find selection highlight by name
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
        // CRITICAL: Force reset visual state on start
        // This fixes the white tile bug from prefab state
        ForceResetVisuals();
    }
    
    /// <summary>
    /// Force all visuals to default state - call this on spawn
    /// </summary>
    private void ForceResetVisuals()
    {
        IsSelected = false;
        
        // Hide highlight
        if (selectionHighlight != null)
        {
            selectionHighlight.SetActive(false);
        }
        
        // Reset background to normal gray
        if (backgroundImage != null)
        {
            backgroundImage.color = normalColor;
        }
        
        // Reset scale
        transform.localScale = Vector3.one;
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
    /// Set the tile's numeric value (0-4).
    /// </summary>
    public void SetValue(int value)
    {
        Value = Mathf.Clamp(value, 0, 5);
        UpdateNumberDisplay();
    }
    
    /// <summary>
    /// Update the number text display.
    /// </summary>
    private void UpdateNumberDisplay()
    {
        if (numberText != null)
        {
            numberText.text = Value.ToString();
            numberText.color = NumberColors[Value];
            
            // Make sure text is enabled and visible
            numberText.enabled = true;
            numberText.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError($"Tile [{GridX},{GridY}]: No Text component found! " +
                          "Make sure the Tile prefab has a Text child.", this);
        }
    }
    
    /// <summary>
    /// Select this tile (visual highlight).
    /// </summary>
    public void Select()
    {
        IsSelected = true;
        
        if (selectionHighlight != null)
        {
            selectionHighlight.SetActive(true);
        }
        
        if (backgroundImage != null)
        {
            backgroundImage.color = selectedColor;
        }
        
        // Scale up slightly for feedback
        transform.localScale = Vector3.one * 1.1f;
    }
    
    /// <summary>
    /// Deselect this tile.
    /// </summary>
    public void Deselect()
    {
        IsSelected = false;
        
        // IMPORTANT: Use SetActive, not just enabled
        if (selectionHighlight != null)
        {
            selectionHighlight.SetActive(false);
        }
        
        if (backgroundImage != null)
        {
            backgroundImage.color = normalColor;
        }
        
        transform.localScale = Vector3.one;
    }
    
    /// <summary>
    /// Handle click/tap input.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        OnTileClicked?.Invoke(this);
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
    
    // Debug helper
    public override string ToString()
    {
        return $"Tile[{GridX},{GridY}] = {Value}";
    }
}
