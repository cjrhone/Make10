using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lets a panel's background Image bleed under the safe-area insets (notch, Dynamic Island,
/// home indicator) while the panel's interactive children stay inside the safe area.
///
/// <see cref="SceneFlowManager"/> parents every panel into a SafeAreaContainer whose rect is
/// shrunk to <c>Screen.safeArea</c>. Backgrounds that live on those panels therefore stop at
/// the inset edge and the camera clear colour shows through as bars at the top and bottom of
/// notched phones.
///
/// This component moves the host's Image onto a "SafeAreaBleed" child (first sibling, so it
/// renders behind everything else on the panel) and stretches that child outward to the root
/// canvas edge on every side where the host is flush with the safe-area container. Sides that
/// are not flush (e.g. the bottom of a panel anchored to the top 40% of the screen) are left
/// alone. On devices without insets the child is the same size as the host and nothing changes.
///
/// The host's own Image is disabled, not removed, so its serialized colour/sprite stay the
/// source of truth: they are mirrored onto the bleed child every frame, so colour tweens and
/// CanvasGroup fades on the panel keep working.
///
/// Panels that carry a <see cref="RectMask2D"/> (MainMenuPanel, GamePanel, CreditsPanel clip
/// their marquee banners during slide transitions) would clip the bleed child back to the
/// panel rect. Every mask between the host and the safe-area container therefore gets negative
/// padding on the bled sides, which widens its clip rect by the same amount. Horizontal
/// clipping is untouched.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class SafeAreaBleed : MonoBehaviour {
  public const string BleedChildName = "SafeAreaBleed";

  [Tooltip("How close (in world/canvas units) a host edge must be to the safe-area edge to count as flush.")]
  [SerializeField] private float flushTolerance = 1f;

  [Tooltip("Safe-area container to compare against. Left empty, the nearest SafeAreaHandler ancestor is used.")]
  [SerializeField] private RectTransform safeArea;

  private RectTransform host;
  private Image hostImage;
  private RectTransform bleed;
  private Image bleedImage;
  private RectTransform canvasRect;
  private bool dirty = true;
  private Vector4 lastHostBounds;
  private Vector4 lastExtension = new(-1f, -1f, -1f, -1f);

  /// <summary>The runtime-created child that actually draws the background, or null before setup.</summary>
  public RectTransform BleedChild => bleed;

  private void Awake() {
    EnsureSetup();
  }

  private void OnEnable() {
    dirty = true;
  }

  private void OnRectTransformDimensionsChange() {
    dirty = true;
  }

  private void LateUpdate() {
    // Position changes (panel slide transitions) do not raise OnRectTransformDimensionsChange,
    // so also re-measure whenever the host's world rect moved.
    if (!dirty && host != null) {
      var bounds = WorldBounds(host);
      if (bounds != lastHostBounds) {
        dirty = true;
      }
    }

    if (dirty) {
      Apply();
    }

    MirrorImage();
  }

  /// <summary>
  /// Recomputes the bleed child's overhang. Safe to call from edit mode and tests; it lazily
  /// creates the child and resolves the canvas / safe-area references.
  /// </summary>
  public void Apply() {
    dirty = false;

    if (!EnsureSetup()) {
      return;
    }

    var safe = ResolveSafeArea();
    var extension = ComputeExtension(host, safe, canvasRect, flushTolerance);
    lastHostBounds = WorldBounds(host);

    if (extension != lastExtension) {
      lastExtension = extension;
      Debug.Log($"[SafeAreaBleed] {name}: host={WorldBounds(host)} safe={(safe != null ? WorldBounds(safe).ToString() : "none")} canvas={WorldBounds(canvasRect)} ext(l,b,r,t)={extension}");
    }

    // Extension is measured in world units; convert to the host's local units.
    var scale = host.lossyScale;
    var sx = Mathf.Approximately(scale.x, 0f) ? 1f : scale.x;
    var sy = Mathf.Approximately(scale.y, 0f) ? 1f : scale.y;

    bleed.offsetMin = new Vector2(-extension.x / sx, -extension.y / sy);
    bleed.offsetMax = new Vector2(extension.z / sx, extension.w / sy);

    WidenAncestorMasks(extension);

    if (bleed.GetSiblingIndex() != 0) {
      bleed.SetAsFirstSibling();
    }

    MirrorImage();
  }

  /// <summary>
  /// Distance (world units) from each host edge to the canvas edge, for every side where the
  /// host edge is flush with the safe-area edge; 0 for sides that are not flush.
  /// Order: x = left, y = bottom, z = right, w = top.
  /// </summary>
  public static Vector4 ComputeExtension (RectTransform host, RectTransform safeArea, RectTransform canvas, float tolerance) {
    if (host == null || safeArea == null || canvas == null) {
      return Vector4.zero;
    }

    var h = WorldBounds(host);
    var s = WorldBounds(safeArea);
    var c = WorldBounds(canvas);

    var left = Mathf.Abs(h.x - s.x) <= tolerance ? Mathf.Max(0f, h.x - c.x) : 0f;
    var bottom = Mathf.Abs(h.y - s.y) <= tolerance ? Mathf.Max(0f, h.y - c.y) : 0f;
    var right = Mathf.Abs(h.z - s.z) <= tolerance ? Mathf.Max(0f, c.z - h.z) : 0f;
    var top = Mathf.Abs(h.w - s.w) <= tolerance ? Mathf.Max(0f, c.w - h.w) : 0f;

    return new Vector4(left, bottom, right, top);
  }

  /// <summary>
  /// RectMask2D clips to its rect plus padding, measured in root-canvas units. Negative padding
  /// widens the clip. Merge (never shrink) so several bleeding panels under one mask, e.g.
  /// GamePanel and its CharacterPanel, all fit.
  /// </summary>
  private void WidenAncestorMasks (Vector4 extensionWorld) {
    var container = ResolveSafeArea();
    var canvasScale = canvasRect.lossyScale;
    var cx = Mathf.Approximately(canvasScale.x, 0f) ? 1f : canvasScale.x;
    var cy = Mathf.Approximately(canvasScale.y, 0f) ? 1f : canvasScale.y;
    var wanted = new Vector4(-extensionWorld.x / cx, -extensionWorld.y / cy, -extensionWorld.z / cx, -extensionWorld.w / cy);

    for (var t = host; t != null && t != container && t != canvasRect; t = t.parent as RectTransform) {
      var mask = t.GetComponent<RectMask2D>();
      if (mask == null) {
        continue;
      }

      var merged = Vector4.Min(mask.padding, wanted);
      if (merged != mask.padding) {
        mask.padding = merged;
        Debug.Log($"[SafeAreaBleed] {name}: widened RectMask2D on {t.name} to padding={merged}");
      }
    }
  }

  /// <summary>World-space (xMin, yMin, xMax, yMax) of a RectTransform.</summary>
  private static Vector4 WorldBounds (RectTransform rt) {
    var corners = new Vector3[4];
    rt.GetWorldCorners(corners); // bottom-left, top-left, top-right, bottom-right
    return new Vector4(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
  }

  private bool EnsureSetup() {
    if (host == null) {
      host = (RectTransform)transform;
    }

    if (hostImage == null) {
      hostImage = GetComponent<Image>();
      if (hostImage == null) {
        Debug.LogWarning($"[SafeAreaBleed] {name} has no Image to bleed; nothing to do.", this);
        return false;
      }
    }

    if (canvasRect == null) {
      var canvas = GetComponentInParent<Canvas>();
      if (canvas != null) {
        canvasRect = (RectTransform)canvas.rootCanvas.transform;
      }
    }

    if (canvasRect == null) {
      return false;
    }

    if (bleed == null) {
      var existing = host.Find(BleedChildName);
      if (existing != null) {
        bleed = (RectTransform)existing;
        bleedImage = bleed.GetComponent<Image>();
      }
      else {
        var go = new GameObject(BleedChildName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(host, false);
        bleed = (RectTransform)go.transform;
        bleed.anchorMin = Vector2.zero;
        bleed.anchorMax = Vector2.one;
        bleed.offsetMin = Vector2.zero;
        bleed.offsetMax = Vector2.zero;
        bleedImage = go.GetComponent<Image>();
      }

      bleedImage.sprite = hostImage.sprite;
      bleedImage.type = hostImage.type;
      bleedImage.preserveAspect = hostImage.preserveAspect;
      bleedImage.fillCenter = hostImage.fillCenter;
      bleedImage.pixelsPerUnitMultiplier = hostImage.pixelsPerUnitMultiplier;
      bleedImage.material = hostImage.material;
      bleedImage.raycastTarget = hostImage.raycastTarget;
      bleedImage.color = hostImage.color;

      hostImage.enabled = false;
    }

    return true;
  }

  private RectTransform ResolveSafeArea() {
    if (safeArea == null) {
      var handler = GetComponentInParent<SafeAreaHandler>();
      if (handler != null) {
        safeArea = (RectTransform)handler.transform;
      }
    }

    return safeArea;
  }

  private void MirrorImage() {
    if (hostImage == null || bleedImage == null) {
      return;
    }

    // Setters early-out when unchanged, so this costs nothing on quiet frames.
    bleedImage.color = hostImage.color;
    if (bleedImage.sprite != hostImage.sprite) {
      bleedImage.sprite = hostImage.sprite;
    }
  }
}
