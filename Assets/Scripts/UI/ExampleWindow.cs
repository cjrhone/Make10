using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Example implementation showing how to use the PopupWindow system.
/// Press F2 in play mode to toggle this example window.
///
/// HOW TO CREATE A NEW POPUP WINDOW:
/// =================================
///
/// 1. CREATE THE GAMEOBJECT:
///    - Create empty GameObject under your Canvas
///    - Add PopupWindow component (or your custom subclass)
///    - The UI will be auto-built on Awake
///
/// 2. CONFIGURE IN INSPECTOR:
///    - windowTitle: Title shown in header
///    - windowSize: Pixel dimensions (600x800 works well for mobile)
///    - Colors: Customize the look
///
/// 3. OPEN/CLOSE FROM CODE:
///    ```csharp
///    popupWindow.Open();   // Shows with animation
///    popupWindow.Close();  // Hides with animation
///    ```
///
/// 4. ADD CONTENT:
///    ```csharp
///    popup.SetTitle("My Window");
///    popup.ClearContent();
///    popup.AddText("Welcome!", 28);
///    popup.AddSpacer(20);
///    popup.AddButton("OK", () => popup.Close());
///    ```
///
/// 5. CUSTOM CONTENT:
///    Get the content area and add your own UI:
///    ```csharp
///    Transform content = popup.GetContentArea();
///    myCustomPrefab.transform.SetParent(content, false);
///    ```
///
/// SIZING GUIDE FOR VERTICAL (9:16) MOBILE:
/// ========================================
///
/// Reference resolution: 1080 x 1920
///
/// | Window Type     | Recommended Size | Notes                        |
/// |-----------------|------------------|------------------------------|
/// | Small Dialog    | 500 x 400        | Confirmations, alerts        |
/// | Medium Panel    | 600 x 700        | Settings, item details       |
/// | Large Panel     | 700 x 1000       | Shop, inventory, upgrades    |
/// | Near-Fullscreen | 900 x 1600       | Character sheet, tutorials   |
///
/// Leave ~10-15% margins on sides for comfortable viewing.
/// </summary>
public class ExampleWindow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PopupWindow popupWindow;

    private void Start()
    {
        // Create popup if not assigned
        if (popupWindow == null)
        {
            CreateExamplePopup();
        }

        Debug.Log("[ExampleWindow] Press F2 to toggle the example popup window");
    }

    private void Update()
    {
        // Toggle with F2 using new Input System
        if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
        {
            ToggleWindow();
        }
    }

    public void ToggleWindow()
    {
        if (popupWindow == null) return;

        if (popupWindow.gameObject.activeSelf)
        {
            popupWindow.Close();
        }
        else
        {
            ShowExampleContent();
            popupWindow.Open();
        }
    }

    private void CreateExamplePopup()
    {
        // Find or create canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[ExampleWindow] No Canvas found in scene!");
            return;
        }

        // Create popup GameObject
        GameObject popupObj = new GameObject("ExamplePopupWindow");
        popupObj.transform.SetParent(canvas.transform, false);

        // Add and configure PopupWindow
        popupWindow = popupObj.AddComponent<PopupWindow>();

        // Subscribe to events
        popupWindow.OnWindowOpened += () => Debug.Log("[ExampleWindow] Window opened!");
        popupWindow.OnWindowClosed += () => Debug.Log("[ExampleWindow] Window closed!");
    }

    private void ShowExampleContent()
    {
        if (popupWindow == null) return;

        popupWindow.SetTitle("Example Window");
        popupWindow.ClearContent();

        // Add various content to demonstrate the system using new UIStyleGuide-based API
        popupWindow.AddHeadline("Welcome!");
        popupWindow.AddSpacer(15);
        popupWindow.AddBody("This is a reusable RPG-style popup window.\n\nCustomize colors, size, and content as needed.");
        popupWindow.AddSpacer(30);

        // Example buttons using UIStyleGuide colors
        popupWindow.AddButton("Action 1", () => Debug.Log("Action 1 clicked!"), UIStyleGuide.ColorButtonPrimary);
        popupWindow.AddButton("Action 2", () => Debug.Log("Action 2 clicked!"), UIStyleGuide.ColorButtonSecondary);
        popupWindow.AddSpacer(20);

        popupWindow.AddButton("Close Window", () => popupWindow.Close(), UIStyleGuide.ColorButtonDanger);
    }
}
