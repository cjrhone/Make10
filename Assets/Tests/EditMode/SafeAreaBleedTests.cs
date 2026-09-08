using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pins SafeAreaBleed geometry without play mode: a 887×1920 canvas (iPhone 13 aspect at
/// matchHeight = 1) with a SafeAreaContainer shrunk by iPhone 13 insets. The bleed child of a
/// full-screen panel must reach the canvas edge on the inset sides only, and a panel anchored to
/// the top region must bleed upward but not downward.
/// </summary>
public class SafeAreaBleedTests {
  private const float CanvasW = 887f;
  private const float CanvasH = 1920f;
  private const float TopInset = 1920f * 141f / 2532f; // notch, canvas units
  private const float BottomInset = 1920f * 102f / 2532f; // home indicator, canvas units
  private const float Tol = 0.01f;

  private GameObject root;
  private RectTransform canvas;
  private RectTransform safeArea;

  [SetUp]
  public void SetUp() {
    root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
    canvas = (RectTransform)root.transform;
    canvas.sizeDelta = new Vector2(CanvasW, CanvasH);

    var container = new GameObject("SafeAreaContainer", typeof(RectTransform), typeof(SafeAreaHandler));
    container.transform.SetParent(canvas, false);
    safeArea = (RectTransform)container.transform;
    safeArea.anchorMin = new Vector2(0f, BottomInset / CanvasH);
    safeArea.anchorMax = new Vector2(1f, 1f - TopInset / CanvasH);
    safeArea.offsetMin = Vector2.zero;
    safeArea.offsetMax = Vector2.zero;
  }

  [TearDown]
  public void TearDown() {
    Object.DestroyImmediate(root);
  }

  private RectTransform MakePanel (string name, Vector2 anchorMin, Vector2 anchorMax) {
    var go = new GameObject(name, typeof(RectTransform), typeof(Image));
    go.transform.SetParent(safeArea, false);
    var rt = (RectTransform)go.transform;
    rt.anchorMin = anchorMin;
    rt.anchorMax = anchorMax;
    rt.offsetMin = Vector2.zero;
    rt.offsetMax = Vector2.zero;
    return rt;
  }

  private static Vector4 WorldBounds (RectTransform rt) {
    var c = new Vector3[4];
    rt.GetWorldCorners(c);
    return new Vector4(c[0].x, c[0].y, c[2].x, c[2].y);
  }

  [Test]
  public void FullScreenPanel_BleedsToCanvasTopAndBottom_NotSides() {
    var panel = MakePanel("MainMenuPanel", Vector2.zero, Vector2.one);
    var bleed = panel.gameObject.AddComponent<SafeAreaBleed>();
    bleed.Apply();

    var child = bleed.BleedChild;
    Assert.IsNotNull(child, "bleed child should be created on Apply");
    Assert.AreEqual(0, child.GetSiblingIndex(), "bleed child must render behind the panel's other children");

    var b = WorldBounds(child);
    var c = WorldBounds(canvas);
    Assert.AreEqual(c.x, b.x, Tol, "left");
    Assert.AreEqual(c.y, b.y, Tol, "bottom should reach canvas edge under the home indicator");
    Assert.AreEqual(c.z, b.z, Tol, "right");
    Assert.AreEqual(c.w, b.w, Tol, "top should reach canvas edge under the notch");

    Assert.IsFalse(panel.GetComponent<Image>().enabled, "host Image is disabled; the bleed child draws instead");
    Assert.IsTrue(child.GetComponent<Image>().enabled);
  }

  [Test]
  public void TopAnchoredPanel_BleedsUpOnly() {
    var panel = MakePanel("CharacterPanel", new Vector2(0f, 0.6f), Vector2.one);
    var bleed = panel.gameObject.AddComponent<SafeAreaBleed>();
    bleed.Apply();

    var b = WorldBounds(bleed.BleedChild);
    var p = WorldBounds(panel);
    var c = WorldBounds(canvas);
    Assert.AreEqual(c.w, b.w, Tol, "top reaches canvas edge");
    Assert.AreEqual(p.y, b.y, Tol, "bottom edge is not flush with safe area, so it stays put");
    Assert.AreEqual(p.x, b.x, Tol, "left unchanged");
    Assert.AreEqual(p.z, b.z, Tol, "right unchanged");
  }

  [Test]
  public void NoInsets_BleedChildMatchesHost() {
    safeArea.anchorMin = Vector2.zero;
    safeArea.anchorMax = Vector2.one;

    var panel = MakePanel("GamePanel", Vector2.zero, Vector2.one);
    var bleed = panel.gameObject.AddComponent<SafeAreaBleed>();
    bleed.Apply();

    Assert.AreEqual(Vector2.zero, bleed.BleedChild.offsetMin);
    Assert.AreEqual(Vector2.zero, bleed.BleedChild.offsetMax);
  }

  [Test]
  public void Apply_IsIdempotent_ReusesChild() {
    var panel = MakePanel("ShopPanel", Vector2.zero, Vector2.one);
    var bleed = panel.gameObject.AddComponent<SafeAreaBleed>();
    bleed.Apply();
    bleed.Apply();

    Assert.AreEqual(1, panel.childCount, "second Apply must not spawn a second bleed child");
  }
}
