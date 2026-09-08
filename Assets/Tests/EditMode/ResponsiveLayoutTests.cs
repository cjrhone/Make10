using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Layout regression across phone / tablet / foldable aspects, without play mode.
///
/// The Canvas uses CanvasScaler matchHeight = 1, so at runtime the canvas is always 1920 units
/// tall and (1920 × aspect) units wide. Each test clones a scene panel under a throwaway
/// world-space canvas of exactly that size, forces a layout + TMP mesh pass, and asserts:
///   • short labels (≤ 3 words, no newline) render on one line and do not overflow their box;
///   • every active Button sits fully inside the canvas;
///   • the results-screen button pair is the same size and centered as a pair.
///
/// Caught for real by this file's first run: the "Make10" title wrapping to two lines on 20:9
/// phones, and "Play Again" / "Main Menu" having different widths and font sizes.
///
/// Not covered: anything created at runtime (grid tiles, results breakdown, stars) and safe-area
/// insets. Use ResolutionContactSheet (Make10 menu) for the visual pass.
/// </summary>
public class ResponsiveLayoutTests {
  private const string ScenePath = "Assets/Scenes/Make10Scene.unity";
  private const float CanvasHeight = 1920f;
  private const float Tolerance = 1.5f; // canvas units

  /// <summary>Mirror of ResolutionContactSheet.Devices, kept inline so the test assembly stays editor-tool-free.</summary>
  private static readonly (string name, int w, int h)[] Devices = {
    ("iPhoneSE", 750, 1334),
    ("BudgetAndroid", 720, 1600),
    ("Pixel8", 1080, 2400),
    ("iPhone15", 1179, 2556),
    ("iPhone15ProMax", 1290, 2796),
    ("GalaxyS24Ultra", 1440, 3120),
    ("iPadMini", 1488, 2266),
    ("iPadAir11", 1640, 2360),
    ("iPadPro13", 2064, 2752),
    ("AndroidTab16x10", 1600, 2560),
    ("PixelFoldInner", 1840, 2208),
  };

  private static readonly string[] Panels = { "MainMenuPanel", "WinScreen", "GamePanel" };

  /// <summary>Labels that are allowed to overflow or wrap by design (marquee banners, decorative fills).</summary>
  private static readonly string[] IgnoredLabelNameFragments = { "Banner", "Parallax", "Marquee" };

  public static IEnumerable<TestCaseData> DeviceCases =>
    Devices.Select(d => new TestCaseData(d.name, d.w, d.h).SetName($"{{m}}({d.name}_{d.w}x{d.h})"));

  private Scene gameScene;
  private bool openedGameScene;

  [OneTimeSetUp]
  public void OpenScene() {
    gameScene = SceneManager.GetSceneByPath(ScenePath);
    if (gameScene.IsValid() && gameScene.isLoaded) {
      return; // already open in the editor — use it as-is
    }

    try {
      gameScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
    }
    catch (System.InvalidOperationException) {
      // Batch-mode / test-runner editors start with an untitled unsaved scene, which Unity
      // refuses to combine additively. Fall back to a single open (offering to save first).
      Assert.IsTrue(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo(),
        "Save or discard the current scene before running layout tests.");
      gameScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    openedGameScene = true;
    Assert.IsTrue(gameScene.isLoaded, $"Could not load {ScenePath}");
  }

  [OneTimeTearDown]
  public void CloseScene() {
    // Only close what we opened additively; a Single open is left in place (there is nothing to go back to).
    if (openedGameScene && gameScene.IsValid() && SceneManager.sceneCount > 1) {
      EditorSceneManager.CloseScene(gameScene, true);
    }
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Tests
  // ─────────────────────────────────────────────────────────────────────────

  [TestCaseSource(nameof(DeviceCases))]
  public void ShortLabels_RenderOnOneLine_AndFitTheirBox (string device, int w, int h) {
    var failures = new List<string>();

    foreach (var panelName in Panels) {
      var (root, clone) = LayOut(panelName, w, h);
      foreach (var label in clone.GetComponentsInChildren<TMP_Text>(false)) {
        if (!IsShortLabel(label)) {
          continue;
        }

        label.ForceMeshUpdate(true, true);
        var lines = label.textInfo.lineCount;
        var boxWidth = label.rectTransform.rect.width - label.margin.x - label.margin.z;
        // textBounds is the rendered extent after auto-size; preferredWidth is a layout query at the
        // base font size and over-reports for auto-sized labels. Horizontal only: several HUD labels
        // sit in deliberately short boxes with Overflow mode, which is fine vertically.
        var renderedWidth = label.textBounds.size.x;
        var overflowsWidth = renderedWidth > boxWidth + Tolerance;

        if (lines > 1 || overflowsWidth) {
          failures.Add(
            $"{panelName}/{Path(label.transform, clone.transform)} \"{label.text}\": lines={lines} " +
            $"renderedWidth={renderedWidth:F0} box={boxWidth:F0} fontSize={label.fontSize:F1}");
        }
      }

      Object.DestroyImmediate(root);
    }

    Assert.IsEmpty(failures, $"[{device} {w}x{h}] labels wrap or overflow:\n  " + string.Join("\n  ", failures));
  }

  [TestCaseSource(nameof(DeviceCases))]
  public void Buttons_StayInsideCanvas (string device, int w, int h) {
    var failures = new List<string>();

    foreach (var panelName in Panels) {
      var (root, clone) = LayOut(panelName, w, h);
      var canvasRect = WorldRect((RectTransform)root.transform);

      foreach (var button in clone.GetComponentsInChildren<Button>(false)) {
        var r = WorldRect((RectTransform)button.transform);
        if (r.xMin < canvasRect.xMin - Tolerance || r.xMax > canvasRect.xMax + Tolerance ||
            r.yMin < canvasRect.yMin - Tolerance || r.yMax > canvasRect.yMax + Tolerance) {
          failures.Add($"{panelName}/{Path(button.transform, clone.transform)} rect={r} canvas={canvasRect}");
        }
      }

      Object.DestroyImmediate(root);
    }

    Assert.IsEmpty(failures, $"[{device} {w}x{h}] buttons leave the canvas:\n  " + string.Join("\n  ", failures));
  }

  [TestCaseSource(nameof(DeviceCases))]
  public void ResultsButtons_MatchAndAreCenteredAsPair (string device, int w, int h) {
    var (root, clone) = LayOut("WinScreen", w, h);
    var panel = WorldRect((RectTransform)clone.transform);
    var playAgain = WorldRect((RectTransform)clone.transform.Find("PlayAgainButton"));
    var mainMenu = WorldRect((RectTransform)clone.transform.Find("ReturnMenuButton"));

    var playAgainLabel = clone.transform.Find("PlayAgainButton").GetComponentInChildren<TMP_Text>(true);
    var mainMenuLabel = clone.transform.Find("ReturnMenuButton").GetComponentInChildren<TMP_Text>(true);

    Object.DestroyImmediate(root);

    Assert.AreEqual(playAgain.width, mainMenu.width, Tolerance, "button widths differ");
    Assert.AreEqual(playAgain.height, mainMenu.height, Tolerance, "button heights differ");
    Assert.AreEqual(playAgain.center.y, mainMenu.center.y, Tolerance, "buttons not on the same row");

    var leftMargin = playAgain.xMin - panel.xMin;
    var rightMargin = panel.xMax - mainMenu.xMax;
    Assert.AreEqual(leftMargin, rightMargin, Tolerance,
      $"button pair off-center: left margin {leftMargin:F1}, right margin {rightMargin:F1}");

    Assert.AreEqual(playAgainLabel.fontSize, mainMenuLabel.fontSize, 0.01f,
      $"label font sizes differ: \"{playAgainLabel.text}\"={playAgainLabel.fontSize} vs \"{mainMenuLabel.text}\"={mainMenuLabel.fontSize}");
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Harness
  // ─────────────────────────────────────────────────────────────────────────

  /// <summary>Clone <paramref name="panelName"/> from the game scene under a canvas sized for the device.</summary>
  private (GameObject root, GameObject clone) LayOut (string panelName, int w, int h) {
    var source = FindInScene(panelName);
    Assert.IsNotNull(source, $"Panel '{panelName}' not found in {ScenePath}");

    // Plain new/Instantiate/DestroyImmediate does not go through Undo, so the scene is not dirtied.
    var root = new GameObject($"Canvas_{w}x{h}", typeof(RectTransform), typeof(Canvas));
    root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
    var rootRect = (RectTransform)root.transform;
    rootRect.sizeDelta = new Vector2(CanvasHeight * w / h, CanvasHeight);

    var clone = Object.Instantiate(source, rootRect, false);
    clone.name = panelName;
    clone.SetActive(true);

    Canvas.ForceUpdateCanvases();
    LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
    foreach (var layoutRoot in clone.GetComponentsInChildren<RectTransform>(true)) {
      LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);
    }

    Canvas.ForceUpdateCanvases();
    return (root, clone);
  }

  private GameObject FindInScene (string name) {
    foreach (var rootGo in gameScene.GetRootGameObjects()) {
      foreach (var t in rootGo.GetComponentsInChildren<Transform>(true)) {
        if (t.name == name) {
          return t.gameObject;
        }
      }
    }

    return null;
  }

  private static bool IsShortLabel (TMP_Text label) {
    if (!label.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(label.text) || label.text.Contains("\n")) {
      return false;
    }

    if (IgnoredLabelNameFragments.Any(f => label.gameObject.name.Contains(f))) {
      return false;
    }

    return label.text.Trim().Split(' ').Length <= 3;
  }

  private static Rect WorldRect (RectTransform rt) {
    var corners = new Vector3[4];
    rt.GetWorldCorners(corners);
    return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
  }

  private static string Path (Transform t, Transform stopAt) {
    var parts = new List<string>();
    while (t != null && t != stopAt) {
      parts.Insert(0, t.name);
      t = t.parent;
    }

    return string.Join("/", parts);
  }
}
