using UnityEngine;

/// <summary>
/// Adjusts a RectTransform to fit within Screen.safeArea on devices with
/// notch, Dynamic Island, or home indicator (iPhone X and later).
///
/// Attach to any GameObject with a RectTransform that should respect safe area.
/// The script updates every frame to handle orientation changes (though Make10
/// is portrait-locked, this is defensive).
///
/// Usage: Add this component to a child of your Canvas. All UI that should
/// respect the safe area should be children of this GameObject.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaHandler : MonoBehaviour
{
    private RectTransform rectTransform;
    private Canvas rootCanvas;
    private Rect lastSafeArea;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;

        // Start with full stretch
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        ApplySafeArea();
    }

    private void Update()
    {
        // Only recalculate if safe area changed
        if (Screen.safeArea != lastSafeArea)
        {
            ApplySafeArea();
        }
    }

    private void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;
        lastSafeArea = safeArea;

        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (rootCanvas == null) return;
        }

        // Convert safe area from screen space to canvas space
        // For Screen Space - Overlay canvas, we need to convert pixel values to anchor values
        Vector2 screenSize = new Vector2(Screen.width, Screen.height);

        // Avoid division by zero
        if (screenSize.x <= 0 || screenSize.y <= 0) return;

        // Convert safe area rect to anchor values (0-1 range)
        Vector2 anchorMin = safeArea.position / screenSize;
        Vector2 anchorMax = (safeArea.position + safeArea.size) / screenSize;

        // Clamp to valid range
        anchorMin.x = Mathf.Clamp01(anchorMin.x);
        anchorMin.y = Mathf.Clamp01(anchorMin.y);
        anchorMax.x = Mathf.Clamp01(anchorMax.x);
        anchorMax.y = Mathf.Clamp01(anchorMax.y);

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

#if UNITY_EDITOR
        // Log only when safe area actually changes and has insets
        if (safeArea.x > 0 || safeArea.y > 0 ||
            safeArea.width < screenSize.x || safeArea.height < screenSize.y)
        {
            Debug.Log($"[Make10] SafeArea applied: {safeArea} on screen {screenSize}");
        }
#endif
    }
}
