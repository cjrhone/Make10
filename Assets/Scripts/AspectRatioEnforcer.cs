using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Enforces a target aspect ratio by adding black letterbox/pillarbox bars.
/// Attach this to the same GameObject as your main Canvas.
/// </summary>
public class AspectRatioEnforcer : MonoBehaviour
{
    [Header("Target Aspect Ratio")]
    [SerializeField] private float targetWidth = 1024f;
    [SerializeField] private float targetHeight = 768f;
    
    [Header("Bar Settings")]
    [SerializeField] private Color barColor = Color.black;
    
    private Canvas parentCanvas;
    private GameObject letterboxContainer;
    
    private void Start()
    {
        parentCanvas = GetComponent<Canvas>();
        CreateLetterboxBars();
    }
    
    private void CreateLetterboxBars()
    {
        // Create a container for the letterbox bars that renders on top of everything
        letterboxContainer = new GameObject("LetterboxBars");
        letterboxContainer.transform.SetParent(transform, false);
        
        // Add a canvas to ensure bars render on top
        Canvas barCanvas = letterboxContainer.AddComponent<Canvas>();
        barCanvas.overrideSorting = true;
        barCanvas.sortingOrder = 9999; // Render on top of everything
        
        // Make it fill the screen
        RectTransform containerRT = letterboxContainer.GetComponent<RectTransform>();
        containerRT.anchorMin = Vector2.zero;
        containerRT.anchorMax = Vector2.one;
        containerRT.offsetMin = Vector2.zero;
        containerRT.offsetMax = Vector2.zero;
        
        // Create the four bars (top, bottom, left, right)
        CreateBar("TopBar", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1));
        CreateBar("BottomBar", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0));
        CreateBar("LeftBar", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f));
        CreateBar("RightBar", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f));
        
        // Initial update
        UpdateBars();
    }
    
    private GameObject CreateBar(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        GameObject bar = new GameObject(name);
        bar.transform.SetParent(letterboxContainer.transform, false);
        
        RectTransform rt = bar.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        
        Image img = bar.AddComponent<Image>();
        img.color = barColor;
        img.raycastTarget = false; // Don't block input
        
        return bar;
    }
    
    private void Update()
    {
        UpdateBars();
    }
    
    private void UpdateBars()
    {
        if (letterboxContainer == null) return;
        
        float targetAspect = targetWidth / targetHeight;
        float screenAspect = (float)Screen.width / Screen.height;
        
        RectTransform topBar = letterboxContainer.transform.Find("TopBar")?.GetComponent<RectTransform>();
        RectTransform bottomBar = letterboxContainer.transform.Find("BottomBar")?.GetComponent<RectTransform>();
        RectTransform leftBar = letterboxContainer.transform.Find("LeftBar")?.GetComponent<RectTransform>();
        RectTransform rightBar = letterboxContainer.transform.Find("RightBar")?.GetComponent<RectTransform>();
        
        if (topBar == null || bottomBar == null || leftBar == null || rightBar == null) return;
        
        // Get the canvas rect to calculate bar sizes
        RectTransform canvasRT = GetComponent<RectTransform>();
        float canvasWidth = canvasRT.rect.width;
        float canvasHeight = canvasRT.rect.height;
        
        if (screenAspect > targetAspect)
        {
            // Screen is wider than target - need pillarbox (left/right bars)
            // Calculate how much extra width there is
            float targetCanvasWidth = canvasHeight * targetAspect;
            float excessWidth = (canvasWidth - targetCanvasWidth) / 2f;
            
            // Show left/right bars
            leftBar.gameObject.SetActive(true);
            rightBar.gameObject.SetActive(true);
            leftBar.sizeDelta = new Vector2(excessWidth, 0);
            rightBar.sizeDelta = new Vector2(excessWidth, 0);
            
            // Hide top/bottom bars
            topBar.gameObject.SetActive(false);
            bottomBar.gameObject.SetActive(false);
        }
        else if (screenAspect < targetAspect)
        {
            // Screen is taller than target - need letterbox (top/bottom bars)
            float targetCanvasHeight = canvasWidth / targetAspect;
            float excessHeight = (canvasHeight - targetCanvasHeight) / 2f;
            
            // Show top/bottom bars
            topBar.gameObject.SetActive(true);
            bottomBar.gameObject.SetActive(true);
            topBar.sizeDelta = new Vector2(0, excessHeight);
            bottomBar.sizeDelta = new Vector2(0, excessHeight);
            
            // Hide left/right bars
            leftBar.gameObject.SetActive(false);
            rightBar.gameObject.SetActive(false);
        }
        else
        {
            // Perfect match - hide all bars
            topBar.gameObject.SetActive(false);
            bottomBar.gameObject.SetActive(false);
            leftBar.gameObject.SetActive(false);
            rightBar.gameObject.SetActive(false);
        }
    }
}
