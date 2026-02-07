using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Runtime debug panel for testing upgrades, snacks, and artifacts.
/// Press F1 to toggle visibility during play.
/// </summary>
public class DebugUpgradePanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform contentParent;
    [SerializeField] private Button closeButton;

    [Header("Data Assets")]
    [SerializeField] private List<UpgradeData> availableUpgrades = new List<UpgradeData>();
    [SerializeField] private List<SnackData> availableSnacks = new List<SnackData>();
    [SerializeField] private List<ArtifactData> availableArtifacts = new List<ArtifactData>();

    private bool isVisible = false;

    private void Start()
    {
        // Auto-load assets if not assigned
        if (availableUpgrades.Count == 0)
            LoadAllUpgrades();
        if (availableSnacks.Count == 0)
            LoadAllSnacks();
        if (availableArtifacts.Count == 0)
            LoadAllArtifacts();

        Debug.Log($"[DebugUpgradePanel] Start - Upgrades: {availableUpgrades.Count}, Snacks: {availableSnacks.Count}, Artifacts: {availableArtifacts.Count}");

        // Create UI if needed
        if (panelRoot == null)
            CreateDebugUI();

        // ALWAYS start hidden
        isVisible = false;
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
            Debug.Log("[DebugUpgradePanel] Panel hidden on start. Press F1 to show.");
        }
    }

    private void Update()
    {
        // Use new Input System - F1 to toggle
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
        {
            TogglePanel();
        }
    }

    public void TogglePanel()
    {
        isVisible = !isVisible;
        if (panelRoot != null)
            panelRoot.SetActive(isVisible);

        Debug.Log($"[DebugUpgradePanel] Panel {(isVisible ? "SHOWN" : "HIDDEN")} - Press F1 to toggle");
    }

    private void LoadAllUpgrades()
    {
        availableUpgrades = DataLoader.LoadUpgrades();
        Debug.Log($"[DebugUpgradePanel] Loaded {availableUpgrades.Count} upgrades");
    }

    private void LoadAllSnacks()
    {
        availableSnacks = DataLoader.LoadSnacks();
        Debug.Log($"[DebugUpgradePanel] Loaded {availableSnacks.Count} snacks");
    }

    private void LoadAllArtifacts()
    {
        availableArtifacts = DataLoader.LoadArtifacts();
        Debug.Log($"[DebugUpgradePanel] Loaded {availableArtifacts.Count} artifacts");
    }

    private void CreateDebugUI()
    {
        Debug.Log($"[DebugUpgradePanel] CreateDebugUI called. Upgrades: {availableUpgrades.Count}, Snacks: {availableSnacks.Count}, Artifacts: {availableArtifacts.Count}");

        // Create panel root with its own canvas for proper overlay
        panelRoot = new GameObject("DebugPanelRoot");
        // Don't parent to anything - standalone overlay canvas

        Canvas canvas = panelRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = panelRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        panelRoot.AddComponent<GraphicRaycaster>();

        // Full panel container on left side - use screen-relative anchors
        GameObject panel = new GameObject("Panel");
        panel.AddComponent<RectTransform>();
        panel.transform.SetParent(panelRoot.transform, false);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.08f, 0.12f, 0.97f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        // Anchor to left side, stretch vertically
        panelRect.anchorMin = new Vector2(0, 0);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 0.5f);
        panelRect.offsetMin = new Vector2(10, 10); // 10px from left and bottom
        panelRect.offsetMax = new Vector2(310, -10); // 300px wide, 10px from top

        // Title bar
        GameObject titleBar = new GameObject("TitleBar");
        titleBar.AddComponent<RectTransform>();
        titleBar.transform.SetParent(panel.transform, false);
        Image titleBg = titleBar.AddComponent<Image>();
        titleBg.color = new Color(0.15f, 0.4f, 0.15f, 1f);
        RectTransform titleRect = titleBar.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.offsetMin = new Vector2(0, -40);
        titleRect.offsetMax = Vector2.zero;

        GameObject titleObj = new GameObject("TitleText");
        titleObj.AddComponent<RectTransform>();
        titleObj.transform.SetParent(titleBar.transform, false);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "DEBUG PANEL (F1)";
        titleText.fontSize = 18;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.verticalAlignment = VerticalAlignmentOptions.Middle;
        RectTransform titleTextRect = titleObj.GetComponent<RectTransform>();
        titleTextRect.anchorMin = Vector2.zero;
        titleTextRect.anchorMax = Vector2.one;
        titleTextRect.offsetMin = Vector2.zero;
        titleTextRect.offsetMax = Vector2.zero;

        // Scroll View area - fill remaining space below title
        GameObject scrollView = new GameObject("ScrollView");
        scrollView.AddComponent<RectTransform>();
        scrollView.transform.SetParent(panel.transform, false);
        Image scrollBg = scrollView.AddComponent<Image>();
        scrollBg.color = new Color(0.05f, 0.05f, 0.08f, 1f);
        ScrollRect scrollRect = scrollView.AddComponent<ScrollRect>();
        scrollRect.scrollSensitivity = 25f;
        RectTransform scrollRectTransform = scrollView.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = Vector2.zero;
        scrollRectTransform.anchorMax = Vector2.one;
        scrollRectTransform.offsetMin = new Vector2(5, 5);
        scrollRectTransform.offsetMax = new Vector2(-5, -45); // Below title bar

        // Viewport with mask
        GameObject viewport = new GameObject("Viewport");
        viewport.AddComponent<RectTransform>();
        viewport.transform.SetParent(scrollView.transform, false);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = Color.white;
        viewport.AddComponent<RectMask2D>(); // Use RectMask2D instead of Mask
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        // Content container - must add RectTransform explicitly for UI
        GameObject content = new GameObject("Content");
        content.AddComponent<RectTransform>(); // Required for UI elements!
        content.transform.SetParent(viewport.transform, false);
        contentParent = content.transform;

        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 3;
        vlg.padding = new RectOffset(5, 5, 5, 5);
        vlg.childAlignment = TextAnchor.UpperCenter;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        Debug.Log($"[DebugUpgradePanel] UI structure created. contentParent valid: {contentParent != null}");

        // Add buttons
        int buttonCount = 0;

        CreateSectionHeader("=== UPGRADES ===");
        foreach (var upgrade in availableUpgrades)
        {
            CreateUpgradeButton(upgrade);
            buttonCount++;
        }

        CreateSectionHeader("=== SNACKS ===");
        foreach (var snack in availableSnacks)
        {
            CreateSnackButton(snack);
            buttonCount++;
        }

        CreateSectionHeader("=== ARTIFACTS ===");
        foreach (var artifact in availableArtifacts)
        {
            CreateArtifactButton(artifact);
            buttonCount++;
        }

        CreateSectionHeader("=== ACTIONS ===");
        CreateActionButton("Print Inventory", () => Debug.Log("[DebugUpgradePanel] Inventory disabled in arcade mode"));
        CreateActionButton("Add 500 BP", () => RunManager.Instance?.AddBP(500));
        CreateActionButton("Clear Inventory", () => PlayerInventory.Instance?.ClearInventory());
        buttonCount += 3;

        Debug.Log($"[DebugUpgradePanel] Created {buttonCount} buttons. Content children: {contentParent.childCount}");
    }

    private void CreateSectionHeader(string text)
    {
        GameObject headerObj = new GameObject("Header_" + text);
        headerObj.AddComponent<RectTransform>();
        headerObj.transform.SetParent(contentParent, false);

        LayoutElement le = headerObj.AddComponent<LayoutElement>();
        le.minHeight = 28;
        le.preferredHeight = 28;

        TextMeshProUGUI headerText = headerObj.AddComponent<TextMeshProUGUI>();
        headerText.text = text;
        headerText.fontSize = 16;
        headerText.fontStyle = FontStyles.Bold;
        headerText.color = Color.cyan;
        headerText.alignment = TextAlignmentOptions.Center;
    }

    private GameObject CreateButton(string name, Color bgColor, string labelText, System.Action onClick)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.AddComponent<RectTransform>();
        btnObj.transform.SetParent(contentParent, false);

        LayoutElement le = btnObj.AddComponent<LayoutElement>();
        le.minHeight = 36;
        le.preferredHeight = 36;

        Image btnImage = btnObj.AddComponent<Image>();
        btnImage.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImage;

        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(bgColor.r + 0.2f, bgColor.g + 0.2f, bgColor.b + 0.2f, 1f);
        colors.pressedColor = new Color(bgColor.r - 0.1f, bgColor.g - 0.1f, bgColor.b - 0.1f, 1f);
        btn.colors = colors;

        btn.onClick.AddListener(() => onClick?.Invoke());

        GameObject textObj = new GameObject("Text");
        textObj.AddComponent<RectTransform>();
        textObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI btnText = textObj.AddComponent<TextMeshProUGUI>();
        btnText.text = labelText;
        btnText.fontSize = 14;
        btnText.color = Color.white;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.margin = new Vector4(5, 0, 5, 0);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        return btnObj;
    }

    private void CreateUpgradeButton(UpgradeData upgrade)
    {
        if (upgrade == null) return;
        CreateButton(
            upgrade.displayName,
            new Color(0.15f, 0.35f, 0.15f, 1f),
            $"{upgrade.displayName} ({upgrade.baseCost}BP)",
            () => Debug.Log($"<color=yellow>[Arcade Mode] Upgrades disabled: {upgrade.displayName}</color>")
        );
    }

    private void CreateSnackButton(SnackData snack)
    {
        if (snack == null) return;
        CreateButton(
            snack.displayName,
            new Color(0.35f, 0.25f, 0.1f, 1f),
            $"{snack.displayName} ({snack.cost}BP)",
            () => Debug.Log($"<color=yellow>[Arcade Mode] Snacks disabled: {snack.displayName}</color>")
        );
    }

    private void CreateArtifactButton(ArtifactData artifact)
    {
        if (artifact == null) return;
        CreateButton(
            artifact.displayName,
            new Color(0.4f, 0.2f, 0.4f, 1f),
            artifact.displayName,
            () => Debug.Log($"<color=yellow>[Arcade Mode] Artifacts disabled: {artifact.displayName}</color>")
        );
    }

    private void CreateActionButton(string label, System.Action action)
    {
        CreateButton(label, new Color(0.2f, 0.2f, 0.4f, 1f), label, action);
    }
}
