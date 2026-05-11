using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Responsive sizer for the gameplay grid. Runs on every aspect ratio (phone, tablet, foldable)
/// and resizes the GridContainer to fit *its direct parent panel* — not the whole canvas —
/// preserving the container's design aspect ratio so the grid never grows taller than its
/// parent and never overflows into the HUD region above it.
///
/// SCENE CONTEXT (why parent-rect, not canvas-rect):
/// The scene's hierarchy is roughly:
///   Canvas (1080×1920 reference, matchHeight=1)
///     SafeAreaContainer (added at runtime by SceneFlowManager)
///       GamePanel (stretch-fills SafeAreaContainer)
///         GridPanelContainer (anchored 0,0 → 1,0.5 — fills BOTTOM HALF of GamePanel)
///           GridContainer  ← this is what we size
/// So on iPad Air 11" portrait (canvas 1334×1920), GridPanelContainer is ≈ 1334×960. The grid's
/// usable bounding box is the parent's rect, not the canvas. Sizing off the full canvas width
/// overflows the parent vertically and pushes the board into the Multiplier/Score row.
///
/// ALGORITHM:
///   1. Read parent.rect.size as the available bounding box.
///   2. Try width-driven: w = parent.w × widthFillRatio; h = w × gridAspect.
///   3. If h exceeds parent.h × heightFillRatio, constrain by height instead:
///        h = parent.h × heightFillRatio; w = h / gridAspect.
///   4. Clamp w to [minGridWidth, maxGridWidth].
///   5. Call GridManager.RecalculateSizesFromContainer() so cached tile size/spacing update.
///
/// TIMING:
///   • Awake: detect aspect (IsTablet/UIScale statics).
///   • Start: do the resize — by Start, SceneFlowManager.Awake has reparented panels into
///     SafeAreaContainer, so parent.rect reflects the final hierarchy.
///   • [DefaultExecutionOrder(-100)] only governs Awake/Start ordering with other default scripts.
///
/// USAGE:
///   Attach to a persistent root object. Wire <see cref="gridContainer"/> to the same
///   RectTransform GridManager references. Tweak fill ratios per aspect in the Inspector.
/// </summary>
[DefaultExecutionOrder(-100)]
public class TabletLayoutAdapter : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════
    // STATIC ACCESS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>True when the runtime aspect ratio matches a portrait tablet.</summary>
    public static bool IsTablet { get; private set; }

    /// <summary>UI scale factor (1.0 on phone, <see cref="tabletScaleFactor"/> on tablet).</summary>
    public static float UIScale { get; private set; } = 1f;

    // ═══════════════════════════════════════════════════════════════════
    // INSPECTOR
    // ═══════════════════════════════════════════════════════════════════

    [Header("Detection")]
    [Tooltip("Aspect ratio (short / long) above which the device is considered a tablet. " +
             "iPad Pro 12.9\" ≈ 0.75; iPad Air 11\" ≈ 0.69; iPhone 14 ≈ 0.46. Default 0.65 " +
             "catches common iPads while excluding every common phone.")]
    [SerializeField] private float portraitTabletAspectThreshold = 0.65f;

    [Header("Grid Sizing — Phone")]
    [Tooltip("Fraction of parent panel WIDTH the grid may occupy on a PHONE-class aspect.")]
    [Range(0.5f, 1f)]
    [SerializeField] private float phoneWidthFillRatio = 0.92f;

    [Tooltip("Fraction of parent panel HEIGHT the grid may occupy on a PHONE-class aspect. " +
             "Acts as a ceiling: if width-fill produces a taller grid than this allows, " +
             "the height-cap kicks in and the grid shrinks to fit.")]
    [Range(0.5f, 1f)]
    [SerializeField] private float phoneHeightFillRatio = 0.95f;

    [Header("Grid Sizing — Tablet")]
    [Tooltip("Fraction of parent panel WIDTH the grid may occupy on a TABLET-class aspect.")]
    [Range(0.5f, 1f)]
    [SerializeField] private float tabletWidthFillRatio = 0.78f;

    [Tooltip("Fraction of parent panel HEIGHT the grid may occupy on a TABLET-class aspect. " +
             "Lower this if the board still overlaps the HUD row above.")]
    [Range(0.5f, 1f)]
    [SerializeField] private float tabletHeightFillRatio = 0.95f;

    [Header("Grid Sizing — Clamps")]
    [Tooltip("Hard cap on grid width in canvas units. 0 = no cap.")]
    [SerializeField] private float maxGridWidth = 1200f;

    [Tooltip("Hard floor on grid width in canvas units. 0 = no floor.")]
    [SerializeField] private float minGridWidth = 600f;

    [Header("Tablet Extras")]
    [Tooltip("Multiplier applied to <see cref=\"additionalScaledTransforms\"/> on tablet aspect " +
             "ratios. The grid itself is sized via fill ratios, not this multiplier.")]
    [SerializeField] private float tabletScaleFactor = 1.15f;

    [Header("References")]
    [Tooltip("The gameplay grid container — same RectTransform GridManager references.")]
    [SerializeField] private RectTransform gridContainer;

    [Tooltip("Optional GridManager reference. If set, RecalculateSizesFromContainer() is " +
             "called after resizing. Auto-found if null.")]
    [SerializeField] private GridManager gridManager;

    [Tooltip("Any additional RectTransforms to scale alongside the grid on tablet (HUD frames, " +
             "character art, decorative panels). These use the flat tabletScaleFactor.")]
    [SerializeField] private List<RectTransform> additionalScaledTransforms = new List<RectTransform>();

    [Header("Debug")]
    [Tooltip("Verbose logging — leave on while tuning ratios, switch off for release.")]
    [SerializeField] private bool verboseLogging = true;

    // Cached aspect ratio of the original grid container (height / width). Captured once so
    // resizing stays proportional even after sizeDelta has been overwritten.
    private float gridAspect = 1f;

    // ═══════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════

    private void Awake()
    {
        DetectAspect();
        CacheGridAspect();
    }

    private void Start()
    {
        // Defer the resize to Start so SceneFlowManager.Awake (which reparents panels into
        // a SafeAreaContainer) has already run. By Start, parent.rect reflects the final
        // hierarchy including any safe-area inset.
        ApplyGridSize();
        ApplyTabletExtras();
        RefreshGridManager();
    }

    private void DetectAspect()
    {
        float w = Screen.width;
        float h = Screen.height;
        if (w <= 0f || h <= 0f)
        {
            IsTablet = false;
            UIScale = 1f;
            return;
        }

        float shortSide = Mathf.Min(w, h);
        float longSide = Mathf.Max(w, h);
        float aspect = shortSide / longSide;

        IsTablet = aspect > portraitTabletAspectThreshold;
        UIScale = IsTablet ? tabletScaleFactor : 1f;

        if (verboseLogging)
        {
            Debug.Log($"[TabletLayoutAdapter] screen={w}x{h} aspect={aspect:F3} " +
                      $"threshold={portraitTabletAspectThreshold:F2} → IsTablet={IsTablet} UIScale={UIScale:F2}");
        }
    }

    private void CacheGridAspect()
    {
        if (gridContainer == null) return;
        Vector2 d = gridContainer.sizeDelta;
        if (d.x > 0.01f) gridAspect = d.y / d.x;
        if (verboseLogging)
        {
            Debug.Log($"[TabletLayoutAdapter] cached gridAspect (h/w) = {gridAspect:F3} from sizeDelta {d}");
        }
    }

    /// <summary>
    /// Resize the GridContainer to fit its parent panel, preserving the captured aspect.
    /// Width OR height — whichever constrains first — drives the result.
    /// </summary>
    private void ApplyGridSize()
    {
        if (gridContainer == null)
        {
            Debug.LogWarning("[TabletLayoutAdapter] gridContainer not wired — skipping resize.");
            return;
        }

        RectTransform parent = gridContainer.parent as RectTransform;
        if (parent == null)
        {
            Debug.LogWarning("[TabletLayoutAdapter] grid's parent is not a RectTransform — skipping resize.");
            return;
        }

        // Force a layout pass so rect.size reflects current screen + safe-area state.
        Canvas.ForceUpdateCanvases();

        Vector2 parentSize = parent.rect.size;
        if (parentSize.x <= 0f || parentSize.y <= 0f)
        {
            Debug.LogWarning($"[TabletLayoutAdapter] parent rect is degenerate {parentSize} — skipping resize.");
            return;
        }

        float widthFill  = IsTablet ? tabletWidthFillRatio  : phoneWidthFillRatio;
        float heightFill = IsTablet ? tabletHeightFillRatio : phoneHeightFillRatio;

        // Width-driven attempt first
        float targetWidth = parentSize.x * widthFill;
        float targetHeight = targetWidth * gridAspect;

        // If height exceeds its cap, constrain by height instead
        float maxHeight = parentSize.y * heightFill;
        if (targetHeight > maxHeight)
        {
            targetHeight = maxHeight;
            targetWidth = targetHeight / gridAspect;
        }

        // Apply hard clamps last (and rederive matching height)
        if (maxGridWidth > 0f) targetWidth = Mathf.Min(targetWidth, maxGridWidth);
        if (minGridWidth > 0f) targetWidth = Mathf.Max(targetWidth, minGridWidth);
        targetHeight = targetWidth * gridAspect;

        Vector2 before = gridContainer.sizeDelta;
        gridContainer.sizeDelta = new Vector2(targetWidth, targetHeight);

        if (verboseLogging)
        {
            Debug.Log($"[TabletLayoutAdapter] parent={parent.name} parentSize={parentSize} " +
                      $"widthFill={widthFill:F2} heightFill={heightFill:F2} " +
                      $"→ grid.sizeDelta {before} → {gridContainer.sizeDelta} (aspect h/w {gridAspect:F3})");
        }
    }

    private void ApplyTabletExtras()
    {
        if (!IsTablet || Mathf.Approximately(UIScale, 1f)) return;
        if (additionalScaledTransforms == null) return;

        foreach (RectTransform rt in additionalScaledTransforms)
        {
            if (rt == null) continue;
            Vector2 before = rt.sizeDelta;
            rt.sizeDelta = before * UIScale;
            if (verboseLogging)
            {
                Debug.Log($"[TabletLayoutAdapter] {rt.name}: sizeDelta {before} → {rt.sizeDelta}");
            }
        }
    }

    /// <summary>
    /// Tells GridManager to re-derive tile size/spacing from the freshly-sized container.
    /// SpawnGrid() also recalculates internally, so this is a belt-and-suspenders refresh.
    /// </summary>
    private void RefreshGridManager()
    {
        if (gridManager == null) gridManager = FindObjectOfType<GridManager>();
        if (gridManager != null) gridManager.RecalculateSizesFromContainer();
    }
}
