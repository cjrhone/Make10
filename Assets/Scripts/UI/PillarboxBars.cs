using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pillarboxes a panel whose artwork was composed for a fixed design width.
///
/// The CharacterPanel art (2048×2732 avatar sprites, aspect-fit and left-aligned) is composed
/// for the 1080-unit reference canvas; the panel's flat blue matches the sprite's own blue so
/// the scene reads as one continuous image on phones. On wider canvases (tablets, foldables,
/// anything with short/long aspect above ~0.56) the panel keeps stretching but the sprite does
/// not, so the sprite's left edge becomes visible inside the panel.
///
/// This component adds two opaque bars, one per side, that cover everything outside a centered
/// <see cref="designWidth"/> window. On canvases narrower than the design width the bars have
/// zero width and draw nothing. Bars are created at runtime as the panel's last children so
/// they render above the art and any decorative children.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class PillarboxBars : MonoBehaviour {
  [Tooltip("Width in canvas units of the composition the art was designed for. Everything outside " +
           "a centered window of this width is covered by the bars.")]
  [SerializeField] private float designWidth = 1080f;

  [SerializeField] private Color barColor = new(0.03f, 0.03f, 0.04f, 1f);

  [Tooltip("Extra overlap in canvas units so the bar edge hides any 1px seam from anti-aliased art.")]
  [SerializeField] private float bleed = 1f;

  private RectTransform panel;
  private RectTransform leftBar;
  private RectTransform rightBar;

  private void Awake() {
    panel = (RectTransform)transform;
    leftBar = CreateBar("PillarboxLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
    rightBar = CreateBar("PillarboxRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f));
    Apply();
  }

  private void OnEnable() {
    Apply();
  }

  private void OnRectTransformDimensionsChange() {
    if (panel != null) {
      Apply();
    }
  }

  private void OnValidate() {
    if (Application.isPlaying && panel != null) {
      Apply();
    }
  }

  private RectTransform CreateBar (string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot) {
    var go = new GameObject(name, typeof(RectTransform), typeof(Image));
    go.transform.SetParent(panel, false);

    var rt = (RectTransform)go.transform;
    rt.anchorMin = anchorMin;
    rt.anchorMax = anchorMax;
    rt.pivot = pivot;
    rt.anchoredPosition = Vector2.zero;
    rt.sizeDelta = Vector2.zero; // width set in Apply; height stretches with the panel

    var img = go.GetComponent<Image>();
    img.color = barColor;
    img.raycastTarget = false;

    return rt;
  }

  private void Apply() {
    if (leftBar == null || rightBar == null) {
      return;
    }

    var overflow = panel.rect.width - designWidth;
    var barWidth = overflow > 0f ? overflow * 0.5f + bleed : 0f;

    leftBar.sizeDelta = new Vector2(barWidth, 0f);
    rightBar.sizeDelta = new Vector2(barWidth, 0f);

    // Keep bars on top of the art and any decorative children added later.
    leftBar.SetAsLastSibling();
    rightBar.SetAsLastSibling();

    var active = barWidth > 0f;
    if (leftBar.gameObject.activeSelf != active) {
      leftBar.gameObject.SetActive(active);
      rightBar.gameObject.SetActive(active);
    }
  }
}
