using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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
public class PopupWindow : MonoBehaviour {
  public enum WindowSize {
    Small,
    Medium,
    Large,
    XLarge,
    Custom,
    AutoSize
  }

  [Header("Window Settings"), SerializeField] 
  private string windowTitle = "Window Title";

  [SerializeField] private WindowSize sizePreset = WindowSize.Medium;
  [SerializeField] private Vector2 customSize = new(900, 750);
  [SerializeField] private bool closeOnOverlayClick = true;

  [Header("Auto-Size Settings (when sizePreset = AutoSize)"), Tooltip("Fixed width for auto-sizing window"),
   SerializeField]
  private float autoSizeWidth = 800f;

  [Tooltip("Minimum height for auto-sizing window"), SerializeField] 
  private float autoSizeMinHeight = 300f;

  [Tooltip("Maximum height for auto-sizing window (0 = no limit)"), SerializeField] 
  private float autoSizeMaxHeight = 1400f;

  [Tooltip("Extra padding added to content height"), SerializeField] 
  private float autoSizePadding = 60f;

  [Header("Scrollbar Settings"), Tooltip("Show a visible scrollbar when content exceeds window height"), SerializeField]
  private bool showScrollbar = true;

  [Tooltip("Width of the scrollbar in pixels"), SerializeField] 
  private float scrollbarWidth = 20f;

  [Tooltip("Color of the scrollbar background track"), SerializeField] 
  private Color scrollbarTrackColor = new(0.1f, 0.1f, 0.15f, 0.8f);

  [Tooltip("Color of the draggable scrollbar handle"), SerializeField] 
  private Color scrollbarHandleColor = new(0.5f, 0.45f, 0.6f, 1f);

  [Tooltip("Color of the handle when hovered/pressed"), SerializeField] 
  private Color scrollbarHandleHoverColor = new(0.65f, 0.6f, 0.75f, 1f);

  [Tooltip("Padding between scrollbar and content"), SerializeField] 
  private float scrollbarPadding = 8f;

  [Header("References (Auto-created if null)"), SerializeField] 
  private GameObject darkBackground;

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

  private Vector2 WindowSizePixels {
    get {
      return sizePreset switch {
        WindowSize.Small => UIStyleGuide.WindowSizeSmall,
        WindowSize.Medium => UIStyleGuide.WindowSizeMedium,
        WindowSize.Large => UIStyleGuide.WindowSizeLarge,
        WindowSize.XLarge => UIStyleGuide.WindowSizeXLarge,
        WindowSize.AutoSize => new Vector2(autoSizeWidth, autoSizeMinHeight), // Initial size, will be adjusted
        _ => customSize
      };
    }
  }

  /// <summary>
  /// Padding (in canvas units) between the popup edge and the canvas/safe-area edge.
  /// Applied on every side when ClampSizeToCanvas() shrinks an oversized popup.
  /// </summary>
  private const float CanvasEdgePadding = 30f;

  /// <summary>
  /// Clamp a desired popup size so it never exceeds the parent canvas/safe-area rect.
  /// On phones with a tall portrait aspect (canvas width &lt; design width), this
  /// shrinks the popup to fit horizontally. Heights are clamped the same way so
  /// auto-size popups stay on-screen on shorter devices.
  /// Returns the input unmodified if no canvas is found yet (e.g., very early Awake).
  /// </summary>
  private Vector2 ClampSizeToCanvas (Vector2 size) {
    // Use the immediate parent rect — popups live under SafeAreaContainer, which
    // accounts for notches. Falling back to the root canvas if needed.
    var reference = transform.parent as RectTransform;
    if (reference == null) {
      var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
      if (canvas != null) {
        reference = canvas.GetComponent<RectTransform>();
      }
    }

    if (reference == null) {
      return size;
    }

    var maxW = Mathf.Max(0f, reference.rect.width - CanvasEdgePadding * 2f);
    var maxH = Mathf.Max(0f, reference.rect.height - CanvasEdgePadding * 2f);

    // Keep aspect-ratio of the original design when shrinking width — otherwise
    // a 980×1200 popup on a 880-wide canvas would look squashed.
    if (size.x > maxW && size.x > 0f) {
      var scale = maxW / size.x;
      size = new Vector2(maxW, size.y * scale);
    }

    if (size.y > maxH && size.y > 0f) {
      var scale = maxH / size.y;
      size = new Vector2(size.x * scale, maxH);
    }

    return size;
  }

  /// <summary>
  /// Recompute and apply windowContainer.sizeDelta from the current canvas size.
  /// Call this on Open() so orientation/resolution changes between Awake and Open
  /// are respected. Cheap — just clamps + assigns sizeDelta.
  /// </summary>
  private void ApplyClampedWindowSize() {
    if (windowContainer == null) {
      return;
    }

    if (IsAutoSize) {
      return; // AutoSize path runs through RefreshAutoSize()
    }

    var windowRect = windowContainer.GetComponent<RectTransform>();
    if (windowRect == null) {
      return;
    }

    var clamped = ClampSizeToCanvas(WindowSizePixels);
    if (windowRect.sizeDelta != clamped) {
      windowRect.sizeDelta = clamped;
    }
  }

  private void Awake() {
    if (windowContainer == null) {
      BuildWindowUI();
    }

    gameObject.SetActive(false);
  }

  #region Public API

  /// <summary>Opens the window with animation</summary>
  public void Open() {
    if (isOpen) {
      return;
    }

    gameObject.SetActive(true);
    isOpen = true;

    // Re-flow window sizing now that the popup is ACTIVE. LayoutRebuilder doesn't
    // measure RectTransforms under inactive parents reliably, so any RefreshAutoSize
    // call made from BuildXxxContent (which runs during Awake while we're inactive)
    // gets stale content-height readings — the window stays at min height and the
    // bottom-most element (the confirm button) ends up cropped by the viewport mask.
    // Calling it here, post-activation, fixes that.
    if (IsAutoSize) {
      RefreshAutoSize();
    }
    else {
      ApplyClampedWindowSize();
    }

    if (animationCoroutine != null) {
      StopCoroutine(animationCoroutine);
    }

    animationCoroutine = StartCoroutine(AnimateOpen());

    OnWindowOpened?.Invoke();
  }

  /// <summary>Closes the window with animation</summary>
  public void Close() {
    if (!isOpen) {
      return;
    }

    if (animationCoroutine != null) {
      StopCoroutine(animationCoroutine);
    }

    animationCoroutine = StartCoroutine(AnimateClose());
  }

  /// <summary>Sets the window title</summary>
  public void SetTitle (string title) {
    windowTitle = title;
    if (titleText != null) {
      titleText.text = title;
    }
  }

  /// <summary>Gets the content area transform for custom content</summary>
  public Transform GetContentArea() {
    return contentArea;
  }

  /// <summary>Clears all content from the window</summary>
  public void ClearContent() {
    if (contentArea == null) {
      return;
    }

    foreach (Transform child in contentArea) {
      Destroy(child.gameObject);
    }
  }

  /// <summary>Scrolls content to top</summary>
  public void ScrollToTop() {
    if (scrollRect != null) {
      scrollRect.normalizedPosition = new Vector2(0, 1);
    }
  }

  /// <summary>
  /// Recalculates the window size to fit content (only works in AutoSize mode).
  /// Call this after adding all content to have the window resize to fit.
  ///
  /// On narrow canvases (iPhone portrait), the design width <c>autoSizeWidth</c>
  /// can exceed the canvas. We clamp width FIRST so the ContentSizeFitter remeasures
  /// at the actual deliverable width — text wraps to more lines when narrower, so
  /// measuring before clamping would understate the height and crop content.
  /// </summary>
  public void RefreshAutoSize() {
    if (!IsAutoSize || windowContainer == null || contentArea == null) {
      return;
    }

    var windowRect = windowContainer.GetComponent<RectTransform>();

    // ── Step 1: lock the window WIDTH to its clamped value before measuring.
    var canvasW = GetReferenceRectSize().x;
    var maxW = Mathf.Max(0f, canvasW - CanvasEdgePadding * 2f);
    var finalWidth = autoSizeWidth > 0f ? Mathf.Min(autoSizeWidth, maxW) : maxW;
    windowRect.sizeDelta = new Vector2(finalWidth, windowRect.sizeDelta.y);

    // ── Step 2: force the content layout to re-flow at that width.
    // Rebuild the WindowContainer first so the Viewport (which derives from the
    // window's RectTransform) sees the new width before the ContentArea remeasures.
    // Without this, the first call after a width change reports the previous
    // content height and the popup ends up too short on narrow canvases.
    Canvas.ForceUpdateCanvases();
    LayoutRebuilder.ForceRebuildLayoutImmediate(windowRect);
    LayoutRebuilder.ForceRebuildLayoutImmediate(contentArea.GetComponent<RectTransform>());

    var contentRect = contentArea.GetComponent<RectTransform>();
    var contentHeight = contentRect.rect.height;

    // ── Step 3: compute total height needed and apply min/max + canvas-height cap.
    var totalHeight = contentHeight + UIStyleGuide.HeaderHeight + autoSizePadding + UIStyleGuide.WindowPadding * 2;
    totalHeight = Mathf.Max(totalHeight, autoSizeMinHeight);
    if (autoSizeMaxHeight > 0) {
      totalHeight = Mathf.Min(totalHeight, autoSizeMaxHeight);
    }

    var canvasH = GetReferenceRectSize().y;
    var maxH = Mathf.Max(0f, canvasH - CanvasEdgePadding * 2f);
    totalHeight = Mathf.Min(totalHeight, maxH);

    windowRect.sizeDelta = new Vector2(finalWidth, totalHeight);

    Debug.Log(
      $"[PopupWindow] AutoSize: width={finalWidth:F0} (design {autoSizeWidth:F0}), content={contentHeight:F0}, total={totalHeight:F0}");
  }

  /// <summary>
  /// Returns the size of the rect this popup is laid out against (parent →
  /// rootCanvas → fallback). Used by both width and height clamps.
  /// </summary>
  private Vector2 GetReferenceRectSize() {
    var reference = transform.parent as RectTransform;
    if (reference == null) {
      var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
      if (canvas != null) {
        reference = canvas.GetComponent<RectTransform>();
      }
    }

    return reference != null ? reference.rect.size : new Vector2(float.MaxValue, float.MaxValue);
  }

  /// <summary>
  /// Sets the window to auto-size mode with specified parameters.
  /// </summary>
  /// <param name="width">Fixed width of the window</param>
  /// <param name="minHeight">Minimum height of the window</param>
  /// <param name="maxHeight">Maximum height (0 = no limit)</param>
  /// <param name="enableScrollbar">Whether to show the scrollbar (default true)</param>
  public void SetAutoSizeMode (float width = 800f, float minHeight = 300f, float maxHeight = 1400f,
    bool enableScrollbar = true) {
    sizePreset = WindowSize.AutoSize;
    autoSizeWidth = width;
    autoSizeMinHeight = minHeight;
    autoSizeMaxHeight = maxHeight;
    showScrollbar = enableScrollbar;
  }

  #endregion

  #region Content Builders

  /// <summary>Add text with full control</summary>
  public TextMeshProUGUI AddText (string text, int fontSize = 0, Color? color = null,
    TextAlignmentOptions alignment = TextAlignmentOptions.Center, FontStyles style = FontStyles.Normal) {
    if (fontSize <= 0) {
      fontSize = UIStyleGuide.FontSizeBody;
    }

    var textColor = color ?? UIStyleGuide.ColorTextPrimary;

    var textObj = CreateContentElement("Text");

    var tmp = textObj.AddComponent<TextMeshProUGUI>();
    tmp.text = text;
    tmp.fontSize = fontSize;
    tmp.color = textColor;
    tmp.alignment = alignment;
    tmp.fontStyle = style;
    tmp.textWrappingMode = TextWrappingModes.Normal;
    tmp.overflowMode = TextOverflowModes.Truncate;
    tmp.lineSpacing = 8f;

    // Let the layout system determine height based on content
    var le = textObj.AddComponent<LayoutElement>();
    le.flexibleWidth = 1;
    // Don't set fixed height - let ContentSizeFitter handle it

    // Add ContentSizeFitter to auto-size based on text
    var csf = textObj.AddComponent<ContentSizeFitter>();
    csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

    return tmp;
  }

  /// <summary>Add a headline (large, bold)</summary>
  public TextMeshProUGUI AddHeadline (string text) {
    return AddText(text, UIStyleGuide.FontSizeHeadline, UIStyleGuide.ColorTextPrimary,
      TextAlignmentOptions.Center, FontStyles.Bold);
  }

  /// <summary>Add a subheading</summary>
  public TextMeshProUGUI AddSubheading (string text, Color? color = null) {
    return AddText(text, UIStyleGuide.FontSizeSubheading, color ?? UIStyleGuide.ColorTextPrimary,
      TextAlignmentOptions.Center, FontStyles.Bold);
  }

  /// <summary>Add body text</summary>
  public TextMeshProUGUI AddBody (string text, Color? color = null) {
    return AddText(text, UIStyleGuide.FontSizeBody, color ?? UIStyleGuide.ColorTextSecondary,
      TextAlignmentOptions.Center, FontStyles.Normal);
  }

  /// <summary>Add an image/icon</summary>
  public Image AddImage (Sprite sprite, float size = 0, Color? tint = null) {
    if (size <= 0) {
      size = UIStyleGuide.ItemIconSize;
    }

    var imgObj = CreateContentElement("Image");

    var le = imgObj.AddComponent<LayoutElement>();
    le.preferredWidth = size;
    le.preferredHeight = size;
    le.minHeight = size;

    var img = imgObj.AddComponent<Image>();
    img.sprite = sprite;
    img.color = tint ?? Color.white;
    img.preserveAspect = true;

    return img;
  }

  /// <summary>Add a placeholder image (colored box)</summary>
  public Image AddImagePlaceholder (float size = 0, Color? color = null) {
    if (size <= 0) {
      size = UIStyleGuide.ItemIconSize;
    }

    var imgObj = CreateContentElement("ImagePlaceholder");

    var le = imgObj.AddComponent<LayoutElement>();
    le.preferredWidth = size;
    le.preferredHeight = size;
    le.minHeight = size;

    var img = imgObj.AddComponent<Image>();
    img.color = color ?? UIStyleGuide.ColorTextMuted;

    return img;
  }

  /// <summary>Add vertical spacing</summary>
  public void AddSpacer (float height = 0) {
    if (height <= 0) {
      height = UIStyleGuide.ElementSpacing;
    }

    var spacer = CreateContentElement("Spacer");
    var le = spacer.AddComponent<LayoutElement>();
    le.minHeight = height;
    le.preferredHeight = height;
    le.flexibleHeight = 0;
  }

  /// <summary>Add a horizontal divider line</summary>
  public void AddDivider (Color? color = null) {
    var divider = CreateContentElement("Divider");

    var le = divider.AddComponent<LayoutElement>();
    le.minHeight = 2;
    le.preferredHeight = 2;
    le.flexibleWidth = 1;

    var img = divider.AddComponent<Image>();
    img.color = color ?? new Color(1f, 1f, 1f, 0.2f);
  }

  /// <summary>
  /// Add a labeled slider with a percentage readout.
  /// Returns the Slider component so callers can read its value.
  /// </summary>
  public Slider AddSlider (string label, float initialValue, Action<float> onValueChanged,
    Color? fillColor = null, float min = 0f, float max = 1f) {
    var sliderFill = fillColor ?? UIStyleGuide.ColorButtonSecondary;

    // Container for label row + slider
    var container = CreateContentElement(label + "_SliderGroup");
    var containerLe = container.AddComponent<LayoutElement>();
    containerLe.minHeight = 120f;
    containerLe.preferredHeight = 120f;
    containerLe.flexibleWidth = 1;

    var vlg = container.AddComponent<VerticalLayoutGroup>();
    vlg.childControlWidth = true;
    vlg.childControlHeight = true;
    vlg.childForceExpandWidth = true;
    vlg.childForceExpandHeight = false;
    vlg.spacing = 8f;

    // --- Label row (label left, percentage right) ---
    var labelRow = CreateUIElement("LabelRow", container.transform);
    var labelRowLe = labelRow.AddComponent<LayoutElement>();
    labelRowLe.minHeight = 40f;
    labelRowLe.preferredHeight = 40f;
    labelRowLe.flexibleWidth = 1;

    // Label text (left-aligned)
    var labelObj = CreateUIElement("Label", labelRow.transform);
    var labelRect = labelObj.GetComponent<RectTransform>();
    labelRect.anchorMin = new Vector2(0, 0);
    labelRect.anchorMax = new Vector2(0.7f, 1);
    labelRect.sizeDelta = Vector2.zero;
    labelRect.offsetMin = Vector2.zero;
    labelRect.offsetMax = Vector2.zero;

    var labelTmp = labelObj.AddComponent<TextMeshProUGUI>();
    labelTmp.text = label;
    labelTmp.fontSize = UIStyleGuide.FontSizeBody;
    labelTmp.color = UIStyleGuide.ColorTextPrimary;
    labelTmp.alignment = TextAlignmentOptions.MidlineLeft;

    // Value text (right-aligned percentage)
    var valueObj = CreateUIElement("Value", labelRow.transform);
    var valueRect = valueObj.GetComponent<RectTransform>();
    valueRect.anchorMin = new Vector2(0.7f, 0);
    valueRect.anchorMax = new Vector2(1, 1);
    valueRect.sizeDelta = Vector2.zero;
    valueRect.offsetMin = Vector2.zero;
    valueRect.offsetMax = Vector2.zero;

    var valueTmp = valueObj.AddComponent<TextMeshProUGUI>();
    valueTmp.text = $"{Mathf.RoundToInt(initialValue / max * 100)}%";
    valueTmp.fontSize = UIStyleGuide.FontSizeBody;
    valueTmp.color = UIStyleGuide.ColorTextAccent;
    valueTmp.alignment = TextAlignmentOptions.MidlineRight;

    // --- Slider ---
    var sliderObj = CreateUIElement("Slider", container.transform);
    var sliderLe = sliderObj.AddComponent<LayoutElement>();
    sliderLe.minHeight = 60f;
    sliderLe.preferredHeight = 60f;
    sliderLe.flexibleWidth = 1;

    // Track background
    var trackObj = CreateUIElement("Track", sliderObj.transform);
    var trackRect = trackObj.GetComponent<RectTransform>();
    trackRect.anchorMin = new Vector2(0, 0.3f);
    trackRect.anchorMax = new Vector2(1, 0.7f);
    trackRect.sizeDelta = Vector2.zero;
    trackRect.offsetMin = Vector2.zero;
    trackRect.offsetMax = Vector2.zero;
    var trackImg = trackObj.AddComponent<Image>();
    trackImg.color = new Color(0.15f, 0.15f, 0.2f, 1f);

    // Fill area
    var fillAreaObj = CreateUIElement("FillArea", sliderObj.transform);
    var fillAreaRect = fillAreaObj.GetComponent<RectTransform>();
    fillAreaRect.anchorMin = new Vector2(0, 0.3f);
    fillAreaRect.anchorMax = new Vector2(1, 0.7f);
    fillAreaRect.sizeDelta = Vector2.zero;
    fillAreaRect.offsetMin = Vector2.zero;
    fillAreaRect.offsetMax = Vector2.zero;

    // Fill
    var fillObj = CreateUIElement("Fill", fillAreaObj.transform);
    var fillRect = fillObj.GetComponent<RectTransform>();
    fillRect.anchorMin = Vector2.zero;
    fillRect.anchorMax = Vector2.one;
    fillRect.sizeDelta = Vector2.zero;
    var fillImg = fillObj.AddComponent<Image>();
    fillImg.color = sliderFill;

    // Handle slide area
    var handleAreaObj = CreateUIElement("HandleArea", sliderObj.transform);
    var handleAreaRect = handleAreaObj.GetComponent<RectTransform>();
    handleAreaRect.anchorMin = Vector2.zero;
    handleAreaRect.anchorMax = Vector2.one;
    handleAreaRect.sizeDelta = Vector2.zero;
    handleAreaRect.offsetMin = Vector2.zero;
    handleAreaRect.offsetMax = Vector2.zero;

    // Handle
    var handleObj = CreateUIElement("Handle", handleAreaObj.transform);
    var handleRect = handleObj.GetComponent<RectTransform>();
    handleRect.sizeDelta = new Vector2(50f, 50f);
    var handleImg = handleObj.AddComponent<Image>();
    handleImg.color = Color.white;

    // Allow drag-to-scroll through slider area
    sliderObj.AddComponent<ScrollPassthrough>();

    // Wire up Slider component
    var slider = sliderObj.AddComponent<Slider>();
    slider.minValue = min;
    slider.maxValue = max;
    slider.wholeNumbers = false;
    slider.targetGraphic = handleImg;
    slider.fillRect = fillRect;
    slider.handleRect = handleRect;
    slider.value = initialValue;

    // Update percentage text + invoke callback on change
    var capturedMax = max;
    slider.onValueChanged.AddListener((val) => {
      valueTmp.text = $"{Mathf.RoundToInt(val / capturedMax * 100)}%";
      onValueChanged?.Invoke(val);
    });

    return slider;
  }

  /// <summary>Add a single button</summary>
  public Button AddButton (string label, Action onClick, Color? buttonColor = null, bool isSmall = false) {
    var bgColor = buttonColor ?? UIStyleGuide.ColorButtonNeutral;
    var height = isSmall ? UIStyleGuide.ButtonHeightSmall : UIStyleGuide.ButtonHeight;

    var btnObj = CreateContentElement(label + "_Button");

    var le = btnObj.AddComponent<LayoutElement>();
    le.minHeight = height;
    le.preferredHeight = height;
    le.flexibleWidth = 1;

    var btnBg = btnObj.AddComponent<Image>();
    btnBg.color = bgColor;

    var btn = btnObj.AddComponent<Button>();
    btn.targetGraphic = btnBg;

    var colors = btn.colors;
    colors.normalColor = bgColor;
    colors.highlightedColor = UIStyleGuide.GetButtonHighlight(bgColor);
    colors.pressedColor = UIStyleGuide.GetButtonPressed(bgColor);
    colors.selectedColor = bgColor;
    btn.colors = colors;

    if (onClick != null) {
      btn.onClick.AddListener(() => onClick());
    }

    // Allow drag-to-scroll through buttons
    btnObj.AddComponent<ScrollPassthrough>();

    // Button text
    var textObj = CreateUIElement("Text", btnObj.transform);
    var textRect = textObj.GetComponent<RectTransform>();
    textRect.anchorMin = Vector2.zero;
    textRect.anchorMax = Vector2.one;
    textRect.sizeDelta = Vector2.zero;

    var btnText = textObj.AddComponent<TextMeshProUGUI>();
    btnText.text = label;
    btnText.fontSize = isSmall ? UIStyleGuide.FontSizeBody : UIStyleGuide.FontSizeButton;
    btnText.fontStyle = FontStyles.Bold;
    btnText.color = Color.white;
    btnText.alignment = TextAlignmentOptions.Center;

    return btn;
  }

  /// <summary>Add a row of buttons (e.g., Cancel/Confirm)</summary>
  public void AddButtonRow (params (string label, Action onClick, Color color)[] buttons) {
    var rowObj = CreateContentElement("ButtonRow");

    var rowLe = rowObj.AddComponent<LayoutElement>();
    rowLe.minHeight = UIStyleGuide.ButtonHeight;
    rowLe.preferredHeight = UIStyleGuide.ButtonHeight;
    rowLe.flexibleWidth = 1;

    var hlg = rowObj.AddComponent<HorizontalLayoutGroup>();
    hlg.childControlWidth = true;
    hlg.childControlHeight = true;
    hlg.childForceExpandWidth = true;
    hlg.childForceExpandHeight = true;
    hlg.spacing = UIStyleGuide.ElementSpacing;

    foreach (var (label, onClick, color) in buttons) {
      var btnObj = CreateUIElement(label + "_Button", rowObj.transform);

      var btnBg = btnObj.AddComponent<Image>();
      btnBg.color = color;

      var btn = btnObj.AddComponent<Button>();
      btn.targetGraphic = btnBg;

      var colors = btn.colors;
      colors.normalColor = color;
      colors.highlightedColor = UIStyleGuide.GetButtonHighlight(color);
      colors.pressedColor = UIStyleGuide.GetButtonPressed(color);
      btn.colors = colors;

      if (onClick != null) {
        btn.onClick.AddListener(() => onClick());
      }

      // Allow drag-to-scroll through buttons
      btnObj.AddComponent<ScrollPassthrough>();

      var textObj = CreateUIElement("Text", btnObj.transform);
      var textRect = textObj.GetComponent<RectTransform>();
      textRect.anchorMin = Vector2.zero;
      textRect.anchorMax = Vector2.one;
      textRect.sizeDelta = Vector2.zero;

      var btnText = textObj.AddComponent<TextMeshProUGUI>();
      btnText.text = label;
      btnText.fontSize = UIStyleGuide.FontSizeButton;
      btnText.fontStyle = FontStyles.Bold;
      btnText.color = Color.white;
      btnText.alignment = TextAlignmentOptions.Center;
    }
  }

  #endregion

  #region UI Building

  private void BuildWindowUI() {
    // Clamp the design size so we never build a popup wider than the canvas.
    // ApplyClampedWindowSize() will reapply on Open() in case canvas size
    // changes between Awake and the first open.
    var size = ClampSizeToCanvas(WindowSizePixels);

    // Root setup
    var rt = GetComponent<RectTransform>();
    if (rt == null) {
      rt = gameObject.AddComponent<RectTransform>();
    }

    rt.anchorMin = Vector2.zero;
    rt.anchorMax = Vector2.one;
    rt.sizeDelta = Vector2.zero;
    rt.anchoredPosition = Vector2.zero;

    canvasGroup = gameObject.AddComponent<CanvasGroup>();

    // === DARK OVERLAY ===
    darkBackground = CreateUIElement("DarkOverlay", transform);
    var overlayImg = darkBackground.AddComponent<Image>();
    overlayImg.color = UIStyleGuide.ColorOverlay;
    var overlayRect = darkBackground.GetComponent<RectTransform>();
    overlayRect.anchorMin = Vector2.zero;
    overlayRect.anchorMax = Vector2.one;
    overlayRect.sizeDelta = Vector2.zero;

    if (closeOnOverlayClick) {
      var overlayBtn = darkBackground.AddComponent<Button>();
      overlayBtn.transition = Selectable.Transition.None;
      overlayBtn.onClick.AddListener(Close);
    }

    // === WINDOW CONTAINER ===
    windowContainer = CreateUIElement("WindowContainer", transform);
    var windowRect = windowContainer.GetComponent<RectTransform>();
    windowRect.anchorMin = new Vector2(0.5f, 0.5f);
    windowRect.anchorMax = new Vector2(0.5f, 0.5f);
    windowRect.pivot = new Vector2(0.5f, 0.5f);
    windowRect.sizeDelta = size;
    windowRect.anchoredPosition = Vector2.zero;

    // === BORDER ===
    var border = CreateUIElement("Border", windowContainer.transform);
    var borderImg = border.AddComponent<Image>();
    borderImg.color = UIStyleGuide.ColorBorder;
    var borderRect = border.GetComponent<RectTransform>();
    borderRect.anchorMin = Vector2.zero;
    borderRect.anchorMax = Vector2.one;
    borderRect.sizeDelta = new Vector2(UIStyleGuide.BorderThickness * 2, UIStyleGuide.BorderThickness * 2);
    borderRect.anchoredPosition = Vector2.zero;

    // === BACKGROUND ===
    var windowBg = CreateUIElement("Background", windowContainer.transform);
    var bgImg = windowBg.AddComponent<Image>();
    bgImg.color = UIStyleGuide.ColorWindowBg;
    var bgRect = windowBg.GetComponent<RectTransform>();
    bgRect.anchorMin = Vector2.zero;
    bgRect.anchorMax = Vector2.one;
    bgRect.sizeDelta = Vector2.zero;
    bgRect.anchoredPosition = Vector2.zero;

    // === HEADER ===
    var header = CreateUIElement("Header", windowContainer.transform);
    var headerImg = header.AddComponent<Image>();
    headerImg.color = UIStyleGuide.ColorHeader;
    var headerRect = header.GetComponent<RectTransform>();
    headerRect.anchorMin = new Vector2(0, 1);
    headerRect.anchorMax = new Vector2(1, 1);
    headerRect.pivot = new Vector2(0.5f, 1);
    headerRect.sizeDelta = new Vector2(0, UIStyleGuide.HeaderHeight);
    headerRect.anchoredPosition = Vector2.zero;

    // === TITLE TEXT ===
    var titleObj = CreateUIElement("TitleText", header.transform);
    titleText = titleObj.AddComponent<TextMeshProUGUI>();
    titleText.text = windowTitle;
    titleText.fontSize = UIStyleGuide.FontSizeWindowTitle;
    titleText.fontStyle = FontStyles.Bold;
    titleText.color = UIStyleGuide.ColorTitleText;
    titleText.alignment = TextAlignmentOptions.Center;
    titleText.textWrappingMode = TextWrappingModes.NoWrap;
    titleText.overflowMode = TextOverflowModes.Ellipsis;
    var titleRect = titleObj.GetComponent<RectTransform>();
    titleRect.anchorMin = new Vector2(0.05f, 0);
    titleRect.anchorMax = new Vector2(0.85f, 1);
    titleRect.sizeDelta = Vector2.zero;
    titleRect.anchoredPosition = Vector2.zero;

    // === CLOSE BUTTON ===
    var closeObj = CreateUIElement("CloseButton", header.transform);
    var closeBg = closeObj.AddComponent<Image>();
    closeBg.color = UIStyleGuide.ColorButtonDanger;
    closeButton = closeObj.AddComponent<Button>();
    closeButton.targetGraphic = closeBg;
    closeButton.onClick.AddListener(Close);

    var closeColors = closeButton.colors;
    closeColors.highlightedColor = UIStyleGuide.GetButtonHighlight(UIStyleGuide.ColorButtonDanger);
    closeColors.pressedColor = UIStyleGuide.GetButtonPressed(UIStyleGuide.ColorButtonDanger);
    closeButton.colors = closeColors;

    var closeRect = closeObj.GetComponent<RectTransform>();
    closeRect.anchorMin = new Vector2(1, 0.5f);
    closeRect.anchorMax = new Vector2(1, 0.5f);
    closeRect.pivot = new Vector2(1, 0.5f);
    closeRect.sizeDelta = new Vector2(UIStyleGuide.CloseButtonSize, UIStyleGuide.CloseButtonSize);
    closeRect.anchoredPosition = new Vector2(-15, 0);

    // X text
    var xText = CreateUIElement("X", closeObj.transform);
    var xTmp = xText.AddComponent<TextMeshProUGUI>();
    xTmp.text = "✕";
    xTmp.fontSize = 48;
    xTmp.color = Color.white;
    xTmp.alignment = TextAlignmentOptions.Center;
    var xRect = xText.GetComponent<RectTransform>();
    xRect.anchorMin = Vector2.zero;
    xRect.anchorMax = Vector2.one;
    xRect.sizeDelta = Vector2.zero;

    // === SCROLL VIEW ===
    var scrollViewObj = CreateUIElement("ScrollView", windowContainer.transform);
    scrollRect = scrollViewObj.AddComponent<ScrollRect>();
    scrollRect.horizontal = false;
    scrollRect.vertical = true;
    scrollRect.scrollSensitivity = 30f;
    scrollRect.movementType = ScrollRect.MovementType.Elastic;
    scrollRect.elasticity = 0.1f;
    scrollRect.inertia = true;
    scrollRect.decelerationRate = 0.12f;

    var scrollViewRect = scrollViewObj.GetComponent<RectTransform>();
    scrollViewRect.anchorMin = Vector2.zero;
    scrollViewRect.anchorMax = Vector2.one;
    scrollViewRect.offsetMin = new Vector2(UIStyleGuide.WindowPadding, UIStyleGuide.WindowPadding);
    // Leave space for scrollbar on the right if enabled
    var rightOffset = showScrollbar ?
      UIStyleGuide.WindowPadding + scrollbarWidth + scrollbarPadding :
      UIStyleGuide.WindowPadding;
    scrollViewRect.offsetMax = new Vector2(-rightOffset, -(UIStyleGuide.HeaderHeight + 10));

    // === VIEWPORT ===
    var viewport = CreateUIElement("Viewport", scrollViewObj.transform);
    viewport.AddComponent<RectMask2D>();
    // Transparent image ensures drags anywhere in the viewport register with ScrollRect
    var viewportImg = viewport.AddComponent<Image>();
    viewportImg.color = Color.clear;
    viewportImg.raycastTarget = true;
    var viewportRect = viewport.GetComponent<RectTransform>();
    viewportRect.anchorMin = Vector2.zero;
    viewportRect.anchorMax = Vector2.one;
    viewportRect.sizeDelta = Vector2.zero;
    viewportRect.anchoredPosition = Vector2.zero;

    // === CONTENT AREA ===
    var content = CreateUIElement("ContentArea", viewport.transform);
    contentArea = content.transform;

    var contentRect = content.GetComponent<RectTransform>();
    contentRect.anchorMin = new Vector2(0, 1);
    contentRect.anchorMax = new Vector2(1, 1);
    contentRect.pivot = new Vector2(0.5f, 1);
    contentRect.sizeDelta = Vector2.zero;
    contentRect.anchoredPosition = Vector2.zero;

    var vlg = content.AddComponent<VerticalLayoutGroup>();
    vlg.childControlWidth = true;
    vlg.childControlHeight = true;
    vlg.childForceExpandWidth = true;
    vlg.childForceExpandHeight = false;
    vlg.spacing = UIStyleGuide.ElementSpacing;
    vlg.padding = new RectOffset(10, 10, 10, 10);
    vlg.childAlignment = TextAnchor.UpperCenter;

    var csf = content.AddComponent<ContentSizeFitter>();
    csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

    scrollRect.content = contentRect;
    scrollRect.viewport = viewportRect;

    // === VERTICAL SCROLLBAR ===
    if (showScrollbar) {
      BuildScrollbar(windowContainer.transform);
      scrollRect.verticalScrollbar = verticalScrollbar;
      scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
    }

    Debug.Log($"[PopupWindow] Built: {windowTitle} ({size.x}x{size.y})");
  }

  private GameObject CreateUIElement (string name, Transform parent) {
    var obj = new GameObject(name);
    obj.AddComponent<RectTransform>();
    obj.transform.SetParent(parent, false);
    return obj;
  }

  private GameObject CreateContentElement (string name) {
    return CreateUIElement(name, contentArea);
  }

  /// <summary>
  /// Builds a visible scrollbar with track and draggable handle.
  /// </summary>
  private void BuildScrollbar (Transform parent) {
    // === SCROLLBAR CONTAINER ===
    var scrollbarObj = CreateUIElement("VerticalScrollbar", parent);
    var scrollbarRect = scrollbarObj.GetComponent<RectTransform>();

    // Position on the right side of the window
    scrollbarRect.anchorMin = new Vector2(1, 0);
    scrollbarRect.anchorMax = new Vector2(1, 1);
    scrollbarRect.pivot = new Vector2(1, 0.5f);
    scrollbarRect.sizeDelta = new Vector2(scrollbarWidth, 0);
    scrollbarRect.offsetMin = new Vector2(-UIStyleGuide.WindowPadding - scrollbarWidth, UIStyleGuide.WindowPadding);
    scrollbarRect.offsetMax = new Vector2(-UIStyleGuide.WindowPadding, -(UIStyleGuide.HeaderHeight + 10));

    // Track background
    var trackImage = scrollbarObj.AddComponent<Image>();
    trackImage.color = scrollbarTrackColor;

    // === SCROLLBAR COMPONENT ===
    verticalScrollbar = scrollbarObj.AddComponent<Scrollbar>();
    verticalScrollbar.direction = Scrollbar.Direction.BottomToTop;

    // === SLIDING AREA (required for proper scrollbar behavior) ===
    var slidingArea = CreateUIElement("SlidingArea", scrollbarObj.transform);
    var slidingRect = slidingArea.GetComponent<RectTransform>();
    slidingRect.anchorMin = Vector2.zero;
    slidingRect.anchorMax = Vector2.one;
    slidingRect.sizeDelta = new Vector2(-4, -4); // Small inset from track edges
    slidingRect.anchoredPosition = Vector2.zero;

    // === HANDLE ===
    var handleObj = CreateUIElement("Handle", slidingArea.transform);
    var handleRect = handleObj.GetComponent<RectTransform>();
    handleRect.anchorMin = Vector2.zero;
    handleRect.anchorMax = Vector2.one;
    handleRect.sizeDelta = Vector2.zero;
    handleRect.anchoredPosition = Vector2.zero;

    var handleImage = handleObj.AddComponent<Image>();
    handleImage.color = scrollbarHandleColor;

    // Configure scrollbar
    verticalScrollbar.targetGraphic = handleImage;
    verticalScrollbar.handleRect = handleRect;

    // Set up hover/press color transitions
    var colors = verticalScrollbar.colors;
    colors.normalColor = scrollbarHandleColor;
    colors.highlightedColor = scrollbarHandleHoverColor;
    colors.pressedColor = scrollbarHandleHoverColor;
    colors.selectedColor = scrollbarHandleColor;
    colors.fadeDuration = 0.1f;
    verticalScrollbar.colors = colors;
  }

  #endregion

  #region Animation

  private IEnumerator AnimateOpen() {
    // Auto-size the window before animating if in AutoSize mode
    if (IsAutoSize) {
      yield return null; // Wait one frame for layout to settle
      RefreshAutoSize();
    }

    var elapsed = 0f;
    var duration = UIStyleGuide.AnimationDuration;
    var windowTransform = windowContainer.transform;

    windowTransform.localScale = Vector3.one * 0.7f;
    canvasGroup.alpha = 0f;

    while (elapsed < duration) {
      elapsed += Time.unscaledDeltaTime;
      var t = elapsed / duration;

      var scale = EaseOutBack(t);
      windowTransform.localScale = Vector3.one * scale;
      canvasGroup.alpha = Mathf.Lerp(0f, 1f, t * 2f);

      yield return null;
    }

    windowTransform.localScale = Vector3.one;
    canvasGroup.alpha = 1f;
  }

  private IEnumerator AnimateClose() {
    var elapsed = 0f;
    var duration = UIStyleGuide.AnimationDuration * 0.7f;
    var windowTransform = windowContainer.transform;

    while (elapsed < duration) {
      elapsed += Time.unscaledDeltaTime;
      var t = elapsed / duration;

      var scale = Mathf.Lerp(1f, 0.85f, t);
      windowTransform.localScale = Vector3.one * scale;
      canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);

      yield return null;
    }

    isOpen = false;
    gameObject.SetActive(false);
    OnWindowClosed?.Invoke();
  }

  private float EaseOutBack (float t) {
    const float c1 = 1.70158f;
    const float c3 = c1 + 1f;
    return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
  }

  #endregion
}

/// <summary>
/// Forwards drag events from interactive child elements (buttons, sliders, etc.)
/// to a parent ScrollRect so content-area dragging scrolls the window.
/// Attach to any child that would otherwise swallow drag input.
/// </summary>
public class ScrollPassthrough : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler {
  private ScrollRect parentScrollRect;

  private void Awake() {
    parentScrollRect = GetComponentInParent<ScrollRect>();
  }

  public void OnBeginDrag (PointerEventData eventData) {
    parentScrollRect?.OnBeginDrag(eventData);
  }

  public void OnDrag (PointerEventData eventData) {
    parentScrollRect?.OnDrag(eventData);
  }

  public void OnEndDrag (PointerEventData eventData) {
    parentScrollRect?.OnEndDrag(eventData);
  }
}