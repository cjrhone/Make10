using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Detects tablet-class aspect ratios at scene load and scales key UI surfaces so that
/// the gameplay grid + character art fill more of the tablet canvas (no empty side
/// margins). Pairs with the Canvas's <c>CanvasScaler.matchWidthOrHeight = 1</c> flip
/// in <c>Make10Scene.unity</c>.
///
/// USAGE:
/// 1. Attach this component to a persistent scene object that runs at game start
///    (e.g. the SceneFlowManager / GameManager root, or a dedicated empty GameObject
///    under the root Canvas).
/// 2. Wire <see cref="gridContainer"/> to the same RectTransform that GridManager's
///    <c>gridContainer</c> field references — typically the "GridContainer" RectTransform
///    in the scene hierarchy.
/// 3. Optionally drag any other RectTransforms (HUD frames, character art) into
///    <see cref="additionalScaledTransforms"/> to scale them in lockstep.
///
/// EXPOSED STATICS:
/// <see cref="IsTablet"/> and <see cref="UIScale"/> are set during Awake and can be
/// consumed by other UI code (e.g. PopupWindow.cs) to bump popup sizes on tablet
/// without hard-coding tablet checks at every call site.
/// </summary>
public class TabletLayoutAdapter : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════
    // STATIC ACCESS (set during Awake; consumed by other UI code)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>True when the runtime aspect ratio matches a portrait tablet.</summary>
    public static bool IsTablet { get; private set; }

    /// <summary>UI scale factor (1.0 on phone, <see cref="tabletScaleFactor"/> on tablet).</summary>
    public static float UIScale { get; private set; } = 1f;

    // ═══════════════════════════════════════════════════════════════════
    // INSPECTOR
    // ═══════════════════════════════════════════════════════════════════

    [Header("Detection")]
    [Tooltip("Aspect ratio (width / height) above which the device is considered a tablet " +
             "in portrait mode. iPad Pro 12.9\" portrait ≈ 0.75; iPad Air 11\" portrait ≈ 0.69; " +
             "iPhone 14 portrait ≈ 0.46. Default 0.65 catches all common iPads while excluding " +
             "every common phone.")]
    [SerializeField] private float portraitTabletAspectThreshold = 0.65f;

    [Header("Scaling")]
    [Tooltip("Multiplier applied to scaled RectTransform sizeDeltas on tablet aspect ratios.")]
    [SerializeField] private float tabletScaleFactor = 1.25f;

    [Header("References")]
    [Tooltip("The gameplay grid container — same RectTransform GridManager references. " +
             "Scaled on tablet so the board reaches close to canvas edges.")]
    [SerializeField] private RectTransform gridContainer;

    [Tooltip("Any additional RectTransforms to scale alongside the grid (HUD frames, " +
             "character art, decorative panels).")]
    [SerializeField] private List<RectTransform> additionalScaledTransforms = new List<RectTransform>();

    // ═══════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════

    private void Awake()
    {
        DetectAspect();
        ApplyScaling();
    }

    private void DetectAspect()
    {
        // Use min/max to make detection orientation-agnostic. We care whether the device's
        // shorter dimension is "fat" relative to its longer one — which is what makes
        // a tablet a tablet vs a phone, regardless of which way it's currently held.
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

        Debug.Log($"[TabletLayoutAdapter] screen={w}x{h} aspect={aspect:F3} " +
                  $"threshold={portraitTabletAspectThreshold:F2} → IsTablet={IsTablet} UIScale={UIScale:F2}");
    }

    private void ApplyScaling()
    {
        if (!IsTablet || Mathf.Approximately(UIScale, 1f)) return;

        if (gridContainer != null)
        {
            ScaleSizeDelta(gridContainer);
        }
        else
        {
            Debug.LogWarning("[TabletLayoutAdapter] gridContainer not wired — grid will not " +
                             "rescale on tablet. Drag the GridContainer RectTransform into " +
                             "the Inspector field.");
        }

        foreach (RectTransform rt in additionalScaledTransforms)
        {
            if (rt != null) ScaleSizeDelta(rt);
        }
    }

    private void ScaleSizeDelta(RectTransform rt)
    {
        Vector2 before = rt.sizeDelta;
        rt.sizeDelta = before * UIScale;
        Debug.Log($"[TabletLayoutAdapter] {rt.name}: sizeDelta {before} → {rt.sizeDelta}");
    }
}
