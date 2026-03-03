using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;

/// <summary>
/// Reusable RPG-style popup window with animation and customizable content.
/// Uses UIStyleGuide for consistent sizing and colors.
///
/// WINDOW STRUCTURE:
/// =================
/// PopupWindow (full-screen container)
/// ├── DarkOverlay (dims background, clickable to close)
/// └── WindowContainer (centered, animated)
///     ├── Border (gold frame)
///     ├── Background (dark panel)
///     ├── Header (purple bar)
///     │   ├── TitleText
///     │   └── CloseButton (X)
///     └── ContentArea (scrollable, vertical layout)
///         └── [Your content here]
///
/// USAGE EXAMPLES:
/// ===============
///
/// // Simple alert:
/// popup.SetTitle("Alert");
/// popup.AddText("Something happened!", UIStyleGuide.FontSizeHeadline);
/// popup.AddButton("OK", () => popup.Close(), UIStyleGuide.ColorButtonPrimary);
///
/// // Item confirmation with image:
/// popup.SetTitle("Purchase Item");
/// popup.AddImage(itemSprite, 150);
/// popup.AddText(itemName, UIStyleGuide.FontSizeSubheading);
/// popup.AddText(itemDescription, UIStyleGuide.FontSizeBody, UIStyleGuide.ColorTextSecondary);
/// popup.AddText("Cost: 100 BP", UIStyleGuide.FontSizeBody, UIStyleGuide.ColorTextAccent);
/// popup.AddButtonRow(
///     ("Cancel", () => popup.Close(), UIStyleGuide.ColorButtonDanger),
///     ("Buy", () => Purchase(), UIStyleGuide.ColorButtonPrimary)
/// );
/// </summary>
public class PopupWindow : MonoBehaviour
{
    public enum WindowSize { Small, Medium, Large, XLarge, Custom, AutoSize }

    [Header("Window Settings")]
    [SerializeField] private string windowTitle = "Window Title";
    [SerializeField] private WindowSize sizePreset = WindowSize.Medium;
    [SerializeField] private Vector2 customSize = new Vector2(900, 750);
    [SerializeField] private bool closeOnOverlayClick = true;

    [Header("Auto-Size Settings (when sizePreset = AutoSize)")]
    [Tooltip("Fixed width for auto-sizing window")]
    [SerializeField] private float autoSizeWidth = 800f;
    [Tooltip("Minimum height for auto-sizing window")]
    [SerializeField] private float autoSizeMinHeight = 300f;
    [Tooltip("Maximum height for auto-sizing window (0 = no limit)")]
    [SerializeField] private float autoSizeMaxHeight = 1400f;
    [Tooltip("Extra padding added to content height")]
    [SerializeField] private float autoSizePadding = 60f;

    [Header("Scrollbar Settings")]
    [Tooltip("Show a visible scrollbar when content exceeds window height")]
    [SerializeField] private bool showScrollbar = true;
    [Tooltip("Width of the scrollbar in pixels")]
    [SerializeField] private float scrollbarWidth = 20f;
    [Tooltip("Color of the scrollbar background track")]
    [SerializeField] private Color scrollbarTrackColor = new Color(0.1f, 0.1f, 0.15f, 0.8f);
    [Tooltip("Color of the draggable scrollbar handle")]
    [SerializeField] private Color scrollbarHandleColor = new Color(0.5f, 0.45f, 0.6f, 1f);
    [Tooltip("Color of the handle when hovered/pressed")]
    [SerializeField] private Color scrollbarHandleHoverColor = new Color(0.65f, 0.6f, 0.75f, 1f);
    [Tooltip("Padding between scrollbar and content")]
    [SerializeField] private float scrollbarPadding = 8f;

    [Header("References (Auto-created if null)")]
    [SerializeField] private GameObject darkBackground;
    [SerializeField] private GameObject windowContainer;
    [SerializeField] private Transform contentArea;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Button closeButton;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Scrollbar verticalScrollbar;

    // Events
    public event Action OnWindowOpened;
    public event Action OnWindowClosed;

    private CanvasGroup canvasGroup;
    private bool isOpen = false;
    private Coroutine animationCoroutine;

    private bool IsAutoSize => sizePreset == WindowSize.AutoSize;

    private Vector2 WindowSizePixels
    {
        get
        {
            return sizePreset switch
            {
                WindowSize.Small => UIStyleGuide.WindowSizeSmall,
                WindowSize.Medium => UIStyleGuide.WindowSizeMedium,
                WindowSize.Large => UIStyleGuide.WindowSizeLarge,
                WindowSize.XLarge => UIStyleGuide.WindowSizeXLarge,
                WindowSize.AutoSize => new Vector2(autoSizeWidth, autoSizeMinHeight), // Initial size, will be adjusted
                _ => customSize
            };
        }
    }

    private void Awake()
    {
        if (windowContainer == null)
            BuildWindowUI();

        gameObject.SetActive(false);
    }

    #region Public API

    /// <summary>Opens the window with animation</summary>
    public void Open()
    {
        if (isOpen) return;

        gameObject.SetActive(true);
        isOpen = true;

        if (animationCoroutine != null)
            StopCoroutine(animationCoroutine);
        animationCoroutine = StartCoroutine(AnimateOpen());

        OnWindowOpened?.Invoke();
    }

    /// <summary>Closes the window with animation</summary>
    public void Close()
    {
        if (!isOpen) return;

        if (animationCoroutine != null)
            StopCoroutine(animationCoroutine);
        animationCoroutine = StartCoroutine(AnimateClose());
    }

    /// <summary>Sets the window title</summary>
    public void SetTitle(string title)
    {
        windowTitle = title;
        if (titleText != null)
            titleText.text = title;
    }

    /// <summary>Gets the content area transform for custom content</summary>
    public Transform GetContentArea() => contentArea;

    /// <summary>Clears all content from the window</summary>
    public void ClearContent()
    {
        if (contentArea == null) return;
        foreach (Transform child in contentArea)
            Destroy(child.gameObject);
    }

    /// <summary>Scrolls content to top</summary>
    public void ScrollToTop()
    {
        if (scrollRect != null)
            scrollRect.normalizedPosition = new Vector2(0, 1);
    }

    /// <summary>
    /// Recalculates the window size to fit content (only works in AutoSize mode).
    /// Call this after adding all content to have the window resize to fit.
    /// </summary>
    public void RefreshAutoSize()
    {
        if (!IsAutoSize || windowContainer == null || contentArea == null) return;

        // Force layout rebuild to get accurate content size
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentArea.GetComponent<RectTransform>());

        // Get content height
        RectTransform contentRect = contentArea.GetComponent<RectTransform>();
        float contentHeight = contentRect.rect.height;

        // Calculate total window height needed
        float totalHeight = contentHeight + UIStyleGuide.HeaderHeight + autoSizePadding + UIStyleGuide.WindowPadding * 2;

        // Clamp to min/max
        totalHeight = Mathf.Max(totalHeight, autoSizeMinHeight);
        if (autoSizeMaxHeight > 0)
            totalHeight = Mathf.Min(totalHeight, autoSizeMaxHeight);

        // Update window container size
        RectTransform windowRect = windowContainer.GetComponent<RectTransform>();
        windowRect.sizeDelta = new Vector2(autoSizeWidth, totalHeight);

        Debug.Log($"[PopupWindow] AutoSize: content={contentHeight:F0}px, total={totalHeight:F0}px");
    }

    /// <summary>
    /// Sets the window to auto-size mode with specified parameters.
    /// </summary>
    /// <param name="width">Fixed width of the window</param>
    /// <param name="minHeight">Minimum height of the window</param>
    /// <param name="maxHeight">Maximum height (0 = no limit)</param>
    /// <param name="enableScrollbar">Whether to show the scrollbar (default true)</param>
    public void SetAutoSizeMode(float width = 800f, float minHeight = 300f, float maxHeight = 1400f, bool enableScrollbar = true)
    {
        sizePreset = WindowSize.AutoSize;
        autoSizeWidth = width;
        autoSizeMinHeight = minHeight;
        autoSizeMaxHeight = maxHeight;
        showScrollbar = enableScrollbar;
    }

    #endregion

    #region Content Builders

    /// <summary>Add text with full control</summary>
    public TextMeshProUGUI AddText(string text, int fontSize = 0, Color? color = null,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center, FontStyles style = FontStyles.Normal)
    {
        if (fontSize <= 0) fontSize = UIStyleGuide.FontSizeBody;
        Color textColor = color ?? UIStyleGuide.ColorTextPrimary;

        GameObject textObj = CreateContentElement("Text");

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = textColor;
        tmp.alignment = alignment;
        tmp.fontStyle = style;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Truncate;
        tmp.lineSpacing = 8f;

        // Let the layout system determine height based on content
        LayoutElement le = textObj.AddComponent<LayoutElement>();
        le.flexibleWidth = 1;
        // Don't set fixed height - let ContentSizeFitter handle it

        // Add ContentSizeFitter to auto-size based on text
        ContentSizeFitter csf = textObj.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return tmp;
    }

    /// <summary>Add a headline (large, bold)</summary>
    public TextMeshProUGUI AddHeadline(string text)
    {
        return AddText(text, UIStyleGuide.FontSizeHeadline, UIStyleGuide.ColorTextPrimary,
            TextAlignmentOptions.Center, FontStyles.Bold);
    }

    /// <summary>Add a subheading</summary>
    public TextMeshProUGUI AddSubheading(string text, Color? color = null)
    {
        return AddText(text, UIStyleGuide.FontSizeSubheading, color ?? UIStyleGuide.ColorTextPrimary,
            TextAlignmentOptions.Center, FontStyles.Bold);
    }

    /// <summary>Add body text</summary>
    public TextMeshProUGUI AddBody(string text, Color? color = null)
    {
        return AddText(text, UIStyleGuide.FontSizeBody, color ?? UIStyleGuide.ColorTextSecondary,
            TextAlignmentOptions.Center, FontStyles.Normal);
    }

    /// <summary>Add an image/icon</summary>
    public Image AddImage(Sprite sprite, float size = 0, Color? tint = null)
    {
        if (size <= 0) size = UIStyleGuide.ItemIconSize;

        GameObject imgObj = CreateContentElement("Image");

        LayoutElement le = imgObj.AddComponent<LayoutElement>();
        le.preferredWidth = size;
        le.preferredHeight = size;
        le.minHeight = size;

        Image img = imgObj.AddComponent<Image>();
        img.sprite = sprite;
        img.color = tint ?? Color.white;
        img.preserveAspect = true;

        return img;
    }

    /// <summary>Add a placeholder image (colored box)</summary>
    public Image AddImagePlaceholder(float size = 0, Color? color = null)
    {
        if (size <= 0) size = UIStyleGuide.ItemIconSize;

        GameObject imgObj = CreateContentElement("ImagePlaceholder");

        LayoutElement le = imgObj.AddComponent<LayoutElement>();
        le.preferredWidth = size;
        le.preferredHeight = size;
        le.minHeight = size;

        Image img = imgObj.AddComponent<Image>();
        img.color = color ?? UIStyleGuide.ColorTextMuted;

        return img;
    }

    /// <summary>Add vertical spacing</summary>
    public void AddSpacer(float height = 0)
    {
        if (height <= 0) height = UIStyleGuide.ElementSpacing;

        GameObject spacer = CreateContentElement("Spacer");
        LayoutElement le = spacer.AddComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;
        le.flexibleHeight = 0;
    }

    /// <summary>Add a horizontal divider line</summary>
    public void AddDivider(Color? color = null)
    {
        GameObject divider = CreateContentElement("Divider");

        LayoutElement le = divider.AddComponent<LayoutElement>();
        le.minHeight = 2;
        le.preferredHeight = 2;
        le.flexibleWidth = 1;

        Image img = divider.AddComponent<Image>();
        img.color = color ?? new Color(1f, 1f, 1f, 0.2f);
    }

    /// <summary>
    /// Add a labeled slider with a percentage readout.
    /// Returns the Slider component so callers can read its value.
    /// </summary>
    public Slider AddSlider(string label, float initialValue, Action<float> onValueChanged,
        Color? fillColor = null, float min = 0f, float max = 1f)
    {
        Color sliderFill = fillColor ?? UIStyleGuide.ColorButtonSecondary;

        // Container for label row + slider
        GameObject container = CreateContentElement(label + "_SliderGroup");
        LayoutElement containerLe = container.AddComponent<LayoutElement>();
        containerLe.minHeight = 120f;
        containerLe.preferredHeight = 120f;
        containerLe.flexibleWidth = 1;

        VerticalLayoutGroup vlg = container.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 8f;

        // --- Label row (label left, percentage right) ---
        GameObject labelRow = CreateUIElement("LabelRow", container.transform);
        LayoutElement labelRowLe = labelRow.AddComponent<LayoutElement>();
        labelRowLe.minHeight = 40f;
        labelRowLe.preferredHeight = 40f;
        labelRowLe.flexibleWidth = 1;

        // Label text (left-aligned)
        GameObject labelObj = CreateUIElement("Label", labelRow.transform);
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 0);
        labelRect.anchorMax = new Vector2(0.7f, 1);
        labelRect.sizeDelta = Vector2.zero;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI labelTmp = labelObj.AddComponent<TextMeshProUGUI>();
        labelTmp.text = label;
        labelTmp.fontSize = UIStyleGuide.FontSizeBody;
        labelTmp.color = UIStyleGuide.ColorTextPrimary;
        labelTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Value text (right-aligned percentage)
        GameObject valueObj = CreateUIElement("Value", labelRow.transform);
        RectTransform valueRect = valueObj.GetComponent<RectTransform>();
        valueRect.anchorMin = new Vector2(0.7f, 0);
        valueRect.anchorMax = new Vector2(1, 1);
        valueRect.sizeDelta = Vector2.zero;
        valueRect.offsetMin = Vector2.zero;
        valueRect.offsetMax = Vector2.zero;

        TextMeshProUGUI valueTmp = valueObj.AddComponent<TextMeshProUGUI>();
        valueTmp.text = $"{Mathf.RoundToInt(initialValue / max * 100)}%";
        valueTmp.fontSize = UIStyleGuide.FontSizeBody;
        valueTmp.color = UIStyleGuide.ColorTextAccent;
        valueTmp.alignment = TextAlignmentOptions.MidlineRight;

        // --- Slider ---
        GameObject sliderObj = CreateUIElement("Slider", container.transform);
        LayoutElement sliderLe = sliderObj.AddComponent<LayoutElement>();
        sliderLe.minHeight = 60f;
        sliderLe.preferredHeight = 60f;
        sliderLe.flexibleWidth = 1;

        // Track background
        GameObject trackObj = CreateUIElement("Track", sliderObj.transform);
        RectTransform trackRect = trackObj.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0, 0.3f);
        trackRect.anchorMax = new Vector2(1, 0.7f);
        trackRect.sizeDelta = Vector2.zero;
        trackRect.offsetMin = Vector2.zero;
        trackRect.offsetMax = Vector2.zero;
        Image trackImg = trackObj.AddComponent<Image>();
        trackImg.color = new Color(0.15f, 0.15f, 0.2f, 1f);

        // Fill area
        GameObject fillAreaObj = CreateUIElement("FillArea", sliderObj.transform);
        RectTransform fillAreaRect = fillAreaObj.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0, 0.3f);
        fillAreaRect.anchorMax = new Vector2(1, 0.7f);
        fillAreaRect.sizeDelta = Vector2.zero;
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;

        // Fill
        GameObject fillObj = CreateUIElement("Fill", fillAreaObj.transform);
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        Image fillImg = fillObj.AddComponent<Image>();
        fillImg.color = sliderFill;

        // Handle slide area
        GameObject handleAreaObj = CreateUIElement("HandleArea", sliderObj.transform);
        RectTransform handleAreaRect = handleAreaObj.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.sizeDelta = Vector2.zero;
        handleAreaRect.offsetMin = Vector2.zero;
        handleAreaRect.offsetMax = Vector2.zero;

        // Handle
        GameObject handleObj = CreateUIElement("Handle", handleAreaObj.transform);
        RectTransform handleRect = handleObj.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(50f, 50f);
        Image handleImg = handleObj.AddComponent<Image>();
        handleImg.color = Color.white;

        // Wire up Slider component
        Slider slider = sliderObj.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = false;
        slider.targetGraphic = handleImg;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.value = initialValue;

        // Update percentage text + invoke callback on change
        float capturedMax = max;
        slider.onValueChanged.AddListener((val) =>
        {
            valueTmp.text = $"{Mathf.RoundToInt(val / capturedMax * 100)}%";
            onValueChanged?.Invoke(val);
        });

        return slider;
    }

    /// <summary>Add a single button</summary>
    public Button AddButton(string label, Action onClick, Color? buttonColor = null, bool isSmall = false)
    {
        Color bgColor = buttonColor ?? UIStyleGuide.ColorButtonNeutral;
        float height = isSmall ? UIStyleGuide.ButtonHeightSmall : UIStyleGuide.ButtonHeight;

        GameObject btnObj = CreateContentElement(label + "_Button");

        LayoutElement le = btnObj.AddComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;
        le.flexibleWidth = 1;

        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnBg;

        ColorBlock colors = btn.colors;
        colors.normalColor = bgColor;
        colors.highlightedColor = UIStyleGuide.GetButtonHighlight(bgColor);
        colors.pressedColor = UIStyleGuide.GetButtonPressed(bgColor);
        colors.selectedColor = bgColor;
        btn.colors = colors;

        if (onClick != null)
            btn.onClick.AddListener(() => onClick());

        // Button text
        GameObject textObj = CreateUIElement("Text", btnObj.transform);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI btnText = textObj.AddComponent<TextMeshProUGUI>();
        btnText.text = label;
        btnText.fontSize = isSmall ? UIStyleGuide.FontSizeBody : UIStyleGuide.FontSizeButton;
        btnText.fontStyle = FontStyles.Bold;
        btnText.color = Color.white;
        btnText.alignment = TextAlignmentOptions.Center;

        return btn;
    }

    /// <summary>Add a row of buttons (e.g., Cancel/Confirm)</summary>
    public void AddButtonRow(params (string label, Action onClick, Color color)[] buttons)
    {
        GameObject rowObj = CreateContentElement("ButtonRow");

        LayoutElement rowLe = rowObj.AddComponent<LayoutElement>();
        rowLe.minHeight = UIStyleGuide.ButtonHeight;
        rowLe.preferredHeight = UIStyleGuide.ButtonHeight;
        rowLe.flexibleWidth = 1;

        HorizontalLayoutGroup hlg = rowObj.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.spacing = UIStyleGuide.ElementSpacing;

        foreach (var (label, onClick, color) in buttons)
        {
            GameObject btnObj = CreateUIElement(label + "_Button", rowObj.transform);

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = color;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnBg;

            ColorBlock colors = btn.colors;
            colors.normalColor = color;
            colors.highlightedColor = UIStyleGuide.GetButtonHighlight(color);
            colors.pressedColor = UIStyleGuide.GetButtonPressed(color);
            btn.colors = colors;

            if (onClick != null)
                btn.onClick.AddListener(() => onClick());

            GameObject textObj = CreateUIElement("Text", btnObj.transform);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI btnText = textObj.AddComponent<TextMeshProUGUI>();
            btnText.text = label;
            btnText.fontSize = UIStyleGuide.FontSizeButton;
            btnText.fontStyle = FontStyles.Bold;
            btnText.color = Color.white;
            btnText.alignment = TextAlignmentOptions.Center;
        }
    }

    #endregion

    #region UI Building

    private void BuildWindowUI()
    {
        Vector2 size = WindowSizePixels;

        // Root setup
        RectTransform rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // === DARK OVERLAY ===
        darkBackground = CreateUIElement("DarkOverlay", transform);
        Image overlayImg = darkBackground.AddComponent<Image>();
        overlayImg.color = UIStyleGuide.ColorOverlay;
        RectTransform overlayRect = darkBackground.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.sizeDelta = Vector2.zero;

        if (closeOnOverlayClick)
        {
            Button overlayBtn = darkBackground.AddComponent<Button>();
            overlayBtn.transition = Selectable.Transition.None;
            overlayBtn.onClick.AddListener(Close);
        }

        // === WINDOW CONTAINER ===
        windowContainer = CreateUIElement("WindowContainer", transform);
        RectTransform windowRect = windowContainer.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = size;
        windowRect.anchoredPosition = Vector2.zero;

        // === BORDER ===
        GameObject border = CreateUIElement("Border", windowContainer.transform);
        Image borderImg = border.AddComponent<Image>();
        borderImg.color = UIStyleGuide.ColorBorder;
        RectTransform borderRect = border.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = new Vector2(UIStyleGuide.BorderThickness * 2, UIStyleGuide.BorderThickness * 2);
        borderRect.anchoredPosition = Vector2.zero;

        // === BACKGROUND ===
        GameObject windowBg = CreateUIElement("Background", windowContainer.transform);
        Image bgImg = windowBg.AddComponent<Image>();
        bgImg.color = UIStyleGuide.ColorWindowBg;
        RectTransform bgRect = windowBg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;

        // === HEADER ===
        GameObject header = CreateUIElement("Header", windowContainer.transform);
        Image headerImg = header.AddComponent<Image>();
        headerImg.color = UIStyleGuide.ColorHeader;
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = new Vector2(1, 1);
        headerRect.pivot = new Vector2(0.5f, 1);
        headerRect.sizeDelta = new Vector2(0, UIStyleGuide.HeaderHeight);
        headerRect.anchoredPosition = Vector2.zero;

        // === TITLE TEXT ===
        GameObject titleObj = CreateUIElement("TitleText", header.transform);
        titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = windowTitle;
        titleText.fontSize = UIStyleGuide.FontSizeWindowTitle;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = UIStyleGuide.ColorTitleText;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.textWrappingMode = TextWrappingModes.NoWrap;
        titleText.overflowMode = TextOverflowModes.Ellipsis;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.05f, 0);
        titleRect.anchorMax = new Vector2(0.85f, 1);
        titleRect.sizeDelta = Vector2.zero;
        titleRect.anchoredPosition = Vector2.zero;

        // === CLOSE BUTTON ===
        GameObject closeObj = CreateUIElement("CloseButton", header.transform);
        Image closeBg = closeObj.AddComponent<Image>();
        closeBg.color = UIStyleGuide.ColorButtonDanger;
        closeButton = closeObj.AddComponent<Button>();
        closeButton.targetGraphic = closeBg;
        closeButton.onClick.AddListener(Close);

        ColorBlock closeColors = closeButton.colors;
        closeColors.highlightedColor = UIStyleGuide.GetButtonHighlight(UIStyleGuide.ColorButtonDanger);
        closeColors.pressedColor = UIStyleGuide.GetButtonPressed(UIStyleGuide.ColorButtonDanger);
        closeButton.colors = closeColors;

        RectTransform closeRect = closeObj.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1, 0.5f);
        closeRect.anchorMax = new Vector2(1, 0.5f);
        closeRect.pivot = new Vector2(1, 0.5f);
        closeRect.sizeDelta = new Vector2(UIStyleGuide.CloseButtonSize, UIStyleGuide.CloseButtonSize);
        closeRect.anchoredPosition = new Vector2(-15, 0);

        // X text
        GameObject xText = CreateUIElement("X", closeObj.transform);
        TextMeshProUGUI xTmp = xText.AddComponent<TextMeshProUGUI>();
        xTmp.text = "✕";
        xTmp.fontSize = 48;
        xTmp.color = Color.white;
        xTmp.alignment = TextAlignmentOptions.Center;
        RectTransform xRect = xText.GetComponent<RectTransform>();
        xRect.anchorMin = Vector2.zero;
        xRect.anchorMax = Vector2.one;
        xRect.sizeDelta = Vector2.zero;

        // === SCROLL VIEW ===
        GameObject scrollViewObj = CreateUIElement("ScrollView", windowContainer.transform);
        scrollRect = scrollViewObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 30f;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        RectTransform scrollViewRect = scrollViewObj.GetComponent<RectTransform>();
        scrollViewRect.anchorMin = Vector2.zero;
        scrollViewRect.anchorMax = Vector2.one;
        scrollViewRect.offsetMin = new Vector2(UIStyleGuide.WindowPadding, UIStyleGuide.WindowPadding);
        // Leave space for scrollbar on the right if enabled
        float rightOffset = showScrollbar ? (UIStyleGuide.WindowPadding + scrollbarWidth + scrollbarPadding) : UIStyleGuide.WindowPadding;
        scrollViewRect.offsetMax = new Vector2(-rightOffset, -(UIStyleGuide.HeaderHeight + 10));

        // === VIEWPORT ===
        GameObject viewport = CreateUIElement("Viewport", scrollViewObj.transform);
        viewport.AddComponent<RectMask2D>();
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportRect.anchoredPosition = Vector2.zero;

        // === CONTENT AREA ===
        GameObject content = CreateUIElement("ContentArea", viewport.transform);
        contentArea = content.transform;

        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = Vector2.zero;
        contentRect.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = UIStyleGuide.ElementSpacing;
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.childAlignment = TextAnchor.UpperCenter;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;

        // === VERTICAL SCROLLBAR ===
        if (showScrollbar)
        {
            BuildScrollbar(windowContainer.transform);
            scrollRect.verticalScrollbar = verticalScrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        }

        Debug.Log($"[PopupWindow] Built: {windowTitle} ({size.x}x{size.y})");
    }

    private GameObject CreateUIElement(string name, Transform parent)
    {
        GameObject obj = new GameObject(name);
        obj.AddComponent<RectTransform>();
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private GameObject CreateContentElement(string name)
    {
        return CreateUIElement(name, contentArea);
    }

    /// <summary>
    /// Builds a visible scrollbar with track and draggable handle.
    /// </summary>
    private void BuildScrollbar(Transform parent)
    {
        // === SCROLLBAR CONTAINER ===
        GameObject scrollbarObj = CreateUIElement("VerticalScrollbar", parent);
        RectTransform scrollbarRect = scrollbarObj.GetComponent<RectTransform>();

        // Position on the right side of the window
        scrollbarRect.anchorMin = new Vector2(1, 0);
        scrollbarRect.anchorMax = new Vector2(1, 1);
        scrollbarRect.pivot = new Vector2(1, 0.5f);
        scrollbarRect.sizeDelta = new Vector2(scrollbarWidth, 0);
        scrollbarRect.offsetMin = new Vector2(-UIStyleGuide.WindowPadding - scrollbarWidth, UIStyleGuide.WindowPadding);
        scrollbarRect.offsetMax = new Vector2(-UIStyleGuide.WindowPadding, -(UIStyleGuide.HeaderHeight + 10));

        // Track background
        Image trackImage = scrollbarObj.AddComponent<Image>();
        trackImage.color = scrollbarTrackColor;

        // === SCROLLBAR COMPONENT ===
        verticalScrollbar = scrollbarObj.AddComponent<Scrollbar>();
        verticalScrollbar.direction = Scrollbar.Direction.BottomToTop;

        // === SLIDING AREA (required for proper scrollbar behavior) ===
        GameObject slidingArea = CreateUIElement("SlidingArea", scrollbarObj.transform);
        RectTransform slidingRect = slidingArea.GetComponent<RectTransform>();
        slidingRect.anchorMin = Vector2.zero;
        slidingRect.anchorMax = Vector2.one;
        slidingRect.sizeDelta = new Vector2(-4, -4); // Small inset from track edges
        slidingRect.anchoredPosition = Vector2.zero;

        // === HANDLE ===
        GameObject handleObj = CreateUIElement("Handle", slidingArea.transform);
        RectTransform handleRect = handleObj.GetComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.sizeDelta = Vector2.zero;
        handleRect.anchoredPosition = Vector2.zero;

        Image handleImage = handleObj.AddComponent<Image>();
        handleImage.color = scrollbarHandleColor;

        // Configure scrollbar
        verticalScrollbar.targetGraphic = handleImage;
        verticalScrollbar.handleRect = handleRect;

        // Set up hover/press color transitions
        ColorBlock colors = verticalScrollbar.colors;
        colors.normalColor = scrollbarHandleColor;
        colors.highlightedColor = scrollbarHandleHoverColor;
        colors.pressedColor = scrollbarHandleHoverColor;
        colors.selectedColor = scrollbarHandleColor;
        colors.fadeDuration = 0.1f;
        verticalScrollbar.colors = colors;
    }

    #endregion

    #region Animation

    private IEnumerator AnimateOpen()
    {
        // Auto-size the window before animating if in AutoSize mode
        if (IsAutoSize)
        {
            yield return null; // Wait one frame for layout to settle
            RefreshAutoSize();
        }

        float elapsed = 0f;
        float duration = UIStyleGuide.AnimationDuration;
        Transform windowTransform = windowContainer.transform;

        windowTransform.localScale = Vector3.one * 0.7f;
        canvasGroup.alpha = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            float scale = EaseOutBack(t);
            windowTransform.localScale = Vector3.one * scale;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t * 2f);

            yield return null;
        }

        windowTransform.localScale = Vector3.one;
        canvasGroup.alpha = 1f;
    }

    private IEnumerator AnimateClose()
    {
        float elapsed = 0f;
        float duration = UIStyleGuide.AnimationDuration * 0.7f;
        Transform windowTransform = windowContainer.transform;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            float scale = Mathf.Lerp(1f, 0.85f, t);
            windowTransform.localScale = Vector3.one * scale;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);

            yield return null;
        }

        isOpen = false;
        gameObject.SetActive(false);
        OnWindowClosed?.Invoke();
    }

    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    #endregion
}
