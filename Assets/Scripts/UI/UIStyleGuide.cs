using UnityEngine;

/// <summary>
/// Centralized style guide for all UI elements in Make10.
/// Reference this for consistent sizing, colors, and spacing across all popups and windows.
///
/// DESIGN PRINCIPLES:
/// ==================
/// 1. Mobile-first: All sizes optimized for vertical 9:16 phone screens
/// 2. Touch-friendly: Minimum 80px tap targets for buttons
/// 3. Readable: Large fonts with high contrast
/// 4. Consistent: Same colors/spacing across all windows
///
/// REFERENCE RESOLUTION: 1080 x 1920 (9:16 vertical)
/// </summary>
public static class UIStyleGuide
{
    // ═══════════════════════════════════════════════════════════════════
    // WINDOW SIZES
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Small alert/confirmation (e.g., "Are you sure?")</summary>
    public static readonly Vector2 WindowSizeSmall = new Vector2(800, 500);

    /// <summary>Medium dialog (e.g., item details, simple choices) - increased height for better readability</summary>
    public static readonly Vector2 WindowSizeMedium = new Vector2(900, 950);

    /// <summary>Large panel (e.g., shop, inventory, upgrade confirm)</summary>
    public static readonly Vector2 WindowSizeLarge = new Vector2(980, 1200);

    /// <summary>Extra large/near-fullscreen (e.g., tutorials, settings)</summary>
    public static readonly Vector2 WindowSizeXLarge = new Vector2(1000, 1500);

    // ═══════════════════════════════════════════════════════════════════
    // FONT SIZES (in points)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Window title in header bar</summary>
    public const int FontSizeWindowTitle = 52;

    /// <summary>Large headline inside content</summary>
    public const int FontSizeHeadline = 56;

    /// <summary>Section headers / item names</summary>
    public const int FontSizeSubheading = 42;

    /// <summary>Standard body text</summary>
    public const int FontSizeBody = 36;

    /// <summary>Secondary/description text</summary>
    public const int FontSizeCaption = 30;

    /// <summary>Small labels / badges</summary>
    public const int FontSizeSmall = 24;

    /// <summary>Button text</summary>
    public const int FontSizeButton = 38;

    // ═══════════════════════════════════════════════════════════════════
    // SPACING & PADDING (in pixels)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Window border thickness</summary>
    public const float BorderThickness = 8f;

    /// <summary>Header bar height</summary>
    public const float HeaderHeight = 110f;

    /// <summary>Padding inside window edges</summary>
    public const float WindowPadding = 40f;

    /// <summary>Space between content elements</summary>
    public const float ElementSpacing = 24f;

    /// <summary>Large gap (between sections)</summary>
    public const float SectionSpacing = 40f;

    /// <summary>Button height (touch-friendly)</summary>
    public const float ButtonHeight = 100f;

    /// <summary>Small button height</summary>
    public const float ButtonHeightSmall = 70f;

    /// <summary>Close button size</summary>
    public const float CloseButtonSize = 80f;

    /// <summary>Icon/image size for item displays</summary>
    public const float ItemIconSize = 120f;

    /// <summary>Large image size (for featured items)</summary>
    public const float LargeImageSize = 200f;

    // ═══════════════════════════════════════════════════════════════════
    // COLORS - WINDOW CHROME
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Dark overlay behind windows</summary>
    public static readonly Color ColorOverlay = new Color(0f, 0f, 0f, 0.8f);

    /// <summary>Window background</summary>
    public static readonly Color ColorWindowBg = new Color(0.06f, 0.06f, 0.10f, 1f);

    /// <summary>Header bar background</summary>
    public static readonly Color ColorHeader = new Color(0.22f, 0.16f, 0.32f, 1f);

    /// <summary>Gold border/frame</summary>
    public static readonly Color ColorBorder = new Color(0.85f, 0.7f, 0.3f, 1f);

    /// <summary>Title text (gold/cream)</summary>
    public static readonly Color ColorTitleText = new Color(1f, 0.95f, 0.75f, 1f);

    // ═══════════════════════════════════════════════════════════════════
    // COLORS - TEXT
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Primary text (off-white)</summary>
    public static readonly Color ColorTextPrimary = new Color(0.95f, 0.95f, 0.95f, 1f);

    /// <summary>Secondary text (light gray)</summary>
    public static readonly Color ColorTextSecondary = new Color(0.75f, 0.75f, 0.8f, 1f);

    /// <summary>Muted/disabled text</summary>
    public static readonly Color ColorTextMuted = new Color(0.5f, 0.5f, 0.55f, 1f);

    /// <summary>Accent text (for highlights)</summary>
    public static readonly Color ColorTextAccent = new Color(1f, 0.85f, 0.4f, 1f);

    // ═══════════════════════════════════════════════════════════════════
    // COLORS - BUTTONS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Primary action button (green)</summary>
    public static readonly Color ColorButtonPrimary = new Color(0.2f, 0.5f, 0.25f, 1f);

    /// <summary>Secondary action button (blue)</summary>
    public static readonly Color ColorButtonSecondary = new Color(0.2f, 0.35f, 0.6f, 1f);

    /// <summary>Neutral/default button (purple)</summary>
    public static readonly Color ColorButtonNeutral = new Color(0.35f, 0.28f, 0.5f, 1f);

    /// <summary>Danger/cancel button (red)</summary>
    public static readonly Color ColorButtonDanger = new Color(0.6f, 0.2f, 0.2f, 1f);

    /// <summary>Disabled button</summary>
    public static readonly Color ColorButtonDisabled = new Color(0.3f, 0.3f, 0.35f, 1f);

    // ═══════════════════════════════════════════════════════════════════
    // COLORS - SEMANTIC
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Success/positive (green)</summary>
    public static readonly Color ColorSuccess = new Color(0.3f, 0.7f, 0.35f, 1f);

    /// <summary>Warning (orange)</summary>
    public static readonly Color ColorWarning = new Color(0.9f, 0.6f, 0.2f, 1f);

    /// <summary>Error/negative (red)</summary>
    public static readonly Color ColorError = new Color(0.8f, 0.25f, 0.25f, 1f);

    /// <summary>Info/highlight (cyan)</summary>
    public static readonly Color ColorInfo = new Color(0.3f, 0.7f, 0.9f, 1f);

    // ═══════════════════════════════════════════════════════════════════
    // COLORS - ITEM RARITY
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Common item</summary>
    public static readonly Color ColorRarityCommon = new Color(0.6f, 0.6f, 0.6f, 1f);

    /// <summary>Uncommon item</summary>
    public static readonly Color ColorRarityUncommon = new Color(0.3f, 0.7f, 0.3f, 1f);

    /// <summary>Rare item</summary>
    public static readonly Color ColorRarityRare = new Color(0.3f, 0.5f, 0.9f, 1f);

    /// <summary>Epic item</summary>
    public static readonly Color ColorRarityEpic = new Color(0.7f, 0.4f, 0.9f, 1f);

    /// <summary>Legendary item</summary>
    public static readonly Color ColorRarityLegendary = new Color(1f, 0.7f, 0.2f, 1f);

    // ═══════════════════════════════════════════════════════════════════
    // ANIMATION
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Window open/close animation duration</summary>
    public const float AnimationDuration = 0.25f;

    /// <summary>Button press animation duration</summary>
    public const float ButtonAnimDuration = 0.1f;

    // ═══════════════════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Get button highlight color (lighter version)</summary>
    public static Color GetButtonHighlight(Color baseColor)
    {
        return new Color(
            Mathf.Min(1f, baseColor.r + 0.2f),
            Mathf.Min(1f, baseColor.g + 0.2f),
            Mathf.Min(1f, baseColor.b + 0.2f),
            baseColor.a
        );
    }

    /// <summary>Get button pressed color (darker version)</summary>
    public static Color GetButtonPressed(Color baseColor)
    {
        return new Color(
            Mathf.Max(0f, baseColor.r - 0.15f),
            Mathf.Max(0f, baseColor.g - 0.15f),
            Mathf.Max(0f, baseColor.b - 0.15f),
            baseColor.a
        );
    }
}
