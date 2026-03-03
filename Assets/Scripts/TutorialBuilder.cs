using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// Builds and manages beautiful tutorial popups using the PopupWindow system.
/// Replaces the old scene-based grey tutorial panels with styled, animated windows.
/// </summary>
public class TutorialBuilder : MonoBehaviour
{
    public static TutorialBuilder Instance { get; private set; }

    private PopupWindow tutorial1Popup;
    private PopupWindow tutorial2Popup;

    // Tracks whether a popup close was triggered by a content button (GOT IT, LET'S GO, BACK)
    // vs the X close button. Prevents OnTutorialCancelled from firing on normal advancement.
    private bool isAdvancing = false;

    // Callbacks for SceneFlowManager
    public event Action OnTutorial1Complete;
    public event Action OnTutorial2Complete;
    public event Action OnTutorialCancelled;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ==========================================
    // PUBLIC API
    // ==========================================

    public void ShowTutorial1()
    {
        if (tutorial1Popup == null) CreateTutorial1();
        tutorial1Popup.Open();
    }

    public void HideTutorial1()
    {
        if (tutorial1Popup != null) tutorial1Popup.Close();
    }

    public void ShowTutorial2()
    {
        if (tutorial2Popup == null) CreateTutorial2();
        tutorial2Popup.Open();
    }

    public void HideTutorial2()
    {
        if (tutorial2Popup != null) tutorial2Popup.Close();
    }

    public void HideCurrentTutorial()
    {
        if (tutorial1Popup != null && tutorial1Popup.gameObject.activeSelf) tutorial1Popup.Close();
        if (tutorial2Popup != null && tutorial2Popup.gameObject.activeSelf) tutorial2Popup.Close();
    }

    // ==========================================
    // TUTORIAL 1: HOW TO PLAY
    // ==========================================

    private void CreateTutorial1()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[TutorialBuilder] No Canvas found!");
            return;
        }

        GameObject popupObj = new GameObject("Tutorial1Popup");
        popupObj.transform.SetParent(canvas.transform, false);

        tutorial1Popup = popupObj.AddComponent<PopupWindow>();
        tutorial1Popup.SetAutoSizeMode(950f, 500f, 1400f, enableScrollbar: true);

        // Wire close button (X) to cancel tutorial
        // Only fires OnTutorialCancelled if the close wasn't triggered by a content button
        tutorial1Popup.OnWindowClosed += () =>
        {
            if (!isAdvancing)
                OnTutorialCancelled?.Invoke();
            isAdvancing = false;
        };

        BuildTutorial1Content();
    }

    private void BuildTutorial1Content()
    {
        tutorial1Popup.SetTitle("HOW TO PLAY");
        tutorial1Popup.ClearContent();

        // Main headline
        tutorial1Popup.AddText("MAKE 10!", UIStyleGuide.FontSizeHeadline,
            UIStyleGuide.ColorTextAccent, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);

        tutorial1Popup.AddSpacer(8);

        // Description
        tutorial1Popup.AddBody("Swap tiles so any row or column\nadds up to exactly 10");

        tutorial1Popup.AddSpacer(15);

        // Demo widget container
        AddDemoWidget(tutorial1Popup);

        tutorial1Popup.AddSpacer(15);

        // Divider
        tutorial1Popup.AddDivider(UIStyleGuide.ColorBorder);

        tutorial1Popup.AddSpacer(8);

        // Controls section
        tutorial1Popup.AddSubheading("CONTROLS", UIStyleGuide.ColorTextAccent);

        tutorial1Popup.AddSpacer(5);

        tutorial1Popup.AddBody("Tap two adjacent tiles to swap them");
        tutorial1Popup.AddBody("Swipe a tile in any direction");
        tutorial1Popup.AddBody("Hold & drag for multi-swaps");

        tutorial1Popup.AddSpacer(20);

        // GOT IT button
        tutorial1Popup.AddButton("GOT IT", () =>
        {
            isAdvancing = true;
            tutorial1Popup.Close();
            OnTutorial1Complete?.Invoke();
        }, UIStyleGuide.ColorButtonPrimary);

        tutorial1Popup.RefreshAutoSize();
    }

    // ==========================================
    // TUTORIAL 2: SCORING
    // ==========================================

    private void CreateTutorial2()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[TutorialBuilder] No Canvas found!");
            return;
        }

        GameObject popupObj = new GameObject("Tutorial2Popup");
        popupObj.transform.SetParent(canvas.transform, false);

        tutorial2Popup = popupObj.AddComponent<PopupWindow>();
        tutorial2Popup.SetAutoSizeMode(950f, 500f, 1400f, enableScrollbar: true);

        // Wire close button (X) to cancel tutorial
        // Only fires OnTutorialCancelled if the close wasn't triggered by a content button
        tutorial2Popup.OnWindowClosed += () =>
        {
            if (!isAdvancing)
                OnTutorialCancelled?.Invoke();
            isAdvancing = false;
        };

        BuildTutorial2Content();
    }

    private void BuildTutorial2Content()
    {
        tutorial2Popup.SetTitle("SCORING");
        tutorial2Popup.ClearContent();

        // Main headline
        tutorial2Popup.AddText("EARN BIG!", UIStyleGuide.FontSizeHeadline,
            UIStyleGuide.ColorTextAccent, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);

        tutorial2Popup.AddSpacer(8);

        // Description
        tutorial2Popup.AddBody("Every Make 10 earns Brain Points");

        tutorial2Popup.AddSpacer(10);
        tutorial2Popup.AddDivider(UIStyleGuide.ColorBorder);
        tutorial2Popup.AddSpacer(8);

        // Multiplier section (Arcade)
        tutorial2Popup.AddSubheading("MULTIPLIER", UIStyleGuide.ColorTextAccent);
        tutorial2Popup.AddSpacer(5);
        tutorial2Popup.AddBody("Solve quickly to fill the multiplier bar\nand boost your score up to \u00d75!");

        tutorial2Popup.AddSpacer(10);

        // Hot Streak section (Arcade)
        Color hotStreakColor = new Color(1f, 0.5f, 0.15f); // Orange
        tutorial2Popup.AddSubheading("HOT STREAK", hotStreakColor);
        tutorial2Popup.AddSpacer(5);
        tutorial2Popup.AddBody("Fill the bar to trigger Hot Streak!\n15 seconds of \u00d75 scoring");

        tutorial2Popup.AddSpacer(10);

        // Time Bonus section
        tutorial2Popup.AddSubheading("TIME BONUS", UIStyleGuide.ColorInfo);
        tutorial2Popup.AddSpacer(5);
        tutorial2Popup.AddBody("Every second played = +1 BP");

        tutorial2Popup.AddSpacer(20);

        // Button row: BACK + LET'S GO!
        tutorial2Popup.AddButtonRow(
            ("\u2190  BACK", () =>
            {
                isAdvancing = true;
                tutorial2Popup.Close();
                ShowTutorial1();
            }, UIStyleGuide.ColorButtonNeutral),
            ("LET'S GO!", () =>
            {
                isAdvancing = true;
                tutorial2Popup.Close();
                OnTutorial2Complete?.Invoke();
            }, UIStyleGuide.ColorButtonPrimary)
        );

        tutorial2Popup.RefreshAutoSize();
    }

    // ==========================================
    // DEMO WIDGET INTEGRATION
    // ==========================================

    /// <summary>
    /// Creates a TutorialDemoWidget embedded inside the popup's content area.
    /// </summary>
    private void AddDemoWidget(PopupWindow popup)
    {
        Transform contentArea = popup.GetContentArea();
        if (contentArea == null) return;

        // Create a container with fixed height for the demo
        GameObject demoContainer = new GameObject("DemoWidgetContainer");
        demoContainer.transform.SetParent(contentArea, false);

        RectTransform containerRT = demoContainer.AddComponent<RectTransform>();
        containerRT.sizeDelta = new Vector2(0, 80f); // Height for the tile row

        LayoutElement le = demoContainer.AddComponent<LayoutElement>();
        le.minHeight = 80f;
        le.preferredHeight = 80f;
        le.flexibleWidth = 1f;

        // Create inner rect for the demo widget to use as its container
        GameObject innerContainer = new GameObject("DemoInner");
        innerContainer.transform.SetParent(demoContainer.transform, false);

        RectTransform innerRT = innerContainer.AddComponent<RectTransform>();
        innerRT.anchorMin = Vector2.zero;
        innerRT.anchorMax = Vector2.one;
        innerRT.sizeDelta = Vector2.zero;
        innerRT.anchoredPosition = Vector2.zero;

        // Add the demo widget component
        TutorialDemoWidget demoWidget = demoContainer.AddComponent<TutorialDemoWidget>();
        demoWidget.SetContainer(innerRT);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
