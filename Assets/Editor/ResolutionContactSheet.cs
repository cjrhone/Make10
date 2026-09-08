#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Automated multi-resolution UI capture. For each device in <see cref="Devices"/> it:
///   1. Selects (adding if needed) a fixed-resolution Game view size via editor reflection.
///   2. Enters play mode, waits for MainMenu, captures.
///   3. Starts an Arcade round (StartGameImmediate), captures the game screen.
///   4. Forces the timer to 0.5s, waits for Results, captures.
///   5. Exits play mode and moves to the next device.
///
/// One play session per device is required because TabletLayoutAdapter and SafeAreaHandler
/// decide layout during Awake/Start. State is persisted in SessionState so the sequence
/// survives the domain reload that entering play mode triggers.
///
/// Output: PNGs named "{index:00}_{device}_{WxH}__{menu|game|results}.png" in the output
/// folder, plus a "DONE" marker file when the whole run finishes. Stitch them with
/// ImageMagick (see Tools/contact_sheet.sh) or any image tool.
///
/// PlayerPrefs written by the throwaway rounds (BP bank, games played, high scores) are
/// snapshotted before the run and restored afterwards.
///
/// Limitation: the editor Game view reports Screen.safeArea as the full screen, so notch /
/// home-indicator insets are NOT exercised here. Use Device Simulator or a device for those.
///
/// Run: menu "Make10/Capture Resolution Contact Sheet", or
///      ResolutionContactSheet.Run("/absolute/output/dir") from editor code.
/// </summary>
[InitializeOnLoad]
public static class ResolutionContactSheet {
  public struct Device {
    public string Name;
    public int Width;
    public int Height;

    public Device (string name, int width, int height) {
      Name = name;
      Width = width;
      Height = height;
    }
  }

  /// <summary>Portrait device set. Short/long aspect noted so tablet threshold (0.65) cases are obvious.</summary>
  public static readonly Device[] Devices = {
    // Phones
    new("iPhoneSE", 750, 1334), // 0.562  16:9 legacy
    new("BudgetAndroid", 720, 1600), // 0.450  20:9
    new("Pixel8", 1080, 2400), // 0.450  20:9
    new("iPhone15", 1179, 2556), // 0.461  19.5:9
    new("iPhone15ProMax", 1290, 2796), // 0.461
    new("GalaxyS24Ultra", 1440, 3120), // 0.462
    // Tablets / foldables
    new("iPadMini", 1488, 2266), // 0.657  just over tablet threshold
    new("iPadAir11", 1640, 2360), // 0.695
    new("iPadPro13", 2064, 2752), // 0.750  3:4
    new("AndroidTab16x10", 1600, 2560), // 0.625  under threshold → phone layout on tablet glass
    new("PixelFoldInner", 1840, 2208), // 0.833  near-square stress case
  };

  private const string KeyActive = "Make10.RCS.Active";
  private const string KeyIndex = "Make10.RCS.Index";
  private const string KeyStage = "Make10.RCS.Stage";
  private const string KeyStageTime = "Make10.RCS.StageTime";
  private const string KeyOutDir = "Make10.RCS.OutDir";
  private const string KeyPrefsSnapshot = "Make10.RCS.Prefs";
  private const string KeyLastCapture = "Make10.RCS.LastCapture";
  private const string KeyFilter = "Make10.RCS.Filter";
  private const string KeyRetries = "Make10.RCS.Retries";
  private const string KeyLastFrame = "Make10.RCS.LastFrame";
  private const string KeyLastFrameTime = "Make10.RCS.LastFrameTime";

  private static readonly string[] PrefKeys = {
    "Make10_TotalBP", "Make10_SpendableBP", "Make10_HighScore", "Make10_HighScoreBP", "Make10_TotalGames",
  };

  private const float MenuSettleSeconds = 1.0f;
  private const float GameSettleSeconds = 2.5f;
  private const float ResultsSettleSeconds = 5.0f;
  private const float CaptureFlushSeconds = 1.0f;
  private const float CaptureFlushTimeoutSeconds = 10f;

  static ResolutionContactSheet() {
    EditorApplication.update += Tick;
  }

  [MenuItem("Make10/Capture Resolution Contact Sheet")]
  public static void RunFromMenu() {
    var dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds", "ResolutionShots");
    Run(dir);
  }

  [MenuItem("Make10/Abort Resolution Capture")]
  public static void Abort() {
    SessionState.EraseBool(KeyActive);
    RestorePrefs();
    Debug.Log("[ResolutionContactSheet] Aborted.");
  }

  public static bool IsRunning => SessionState.GetBool(KeyActive, false);

  /// <param name="outDir">Absolute output folder.</param>
  /// <param name="deviceFilter">Optional comma-separated device names to capture; null = all.</param>
  public static void Run (string outDir, string deviceFilter = null) {
    if (EditorApplication.isPlayingOrWillChangePlaymode) {
      Debug.LogError("[ResolutionContactSheet] Exit play mode before starting a capture run.");
      return;
    }

    Directory.CreateDirectory(outDir);
    var done = Path.Combine(outDir, "DONE");
    if (File.Exists(done)) {
      File.Delete(done);
    }

    SnapshotPrefs();
    SessionState.SetString(KeyOutDir, outDir);
    SessionState.SetString(KeyFilter, deviceFilter ?? "");
    SessionState.SetInt(KeyIndex, 0);
    SessionState.SetString(KeyStage, "begin");
    SessionState.SetFloat(KeyStageTime, 0f);
    SessionState.SetBool(KeyActive, true);
    Debug.Log($"[ResolutionContactSheet] Starting run: {Devices.Length} devices → {outDir}");
  }

  // ─────────────────────────────────────────────────────────────────────────
  // State machine (driven by EditorApplication.update, survives domain reload)
  // ─────────────────────────────────────────────────────────────────────────

  private static void Tick() {
    if (!IsRunning) {
      return;
    }

    var index = SessionState.GetInt(KeyIndex, 0);
    if (index >= Devices.Length) {
      Finish();
      return;
    }

    var dev = Devices[index];
    var stage = SessionState.GetString(KeyStage, "begin");

    if (EditorApplication.isPlaying) {
      KickStalledPlayerLoop();
    }

    try {
      switch (stage) {
        case "begin":
          if (EditorApplication.isPlayingOrWillChangePlaymode) {
            return;
          }

          if (!PassesFilter(dev)) {
            SessionState.SetInt(KeyIndex, index + 1);
            return;
          }

          SetGameViewSize(dev.Width, dev.Height);
          FocusGameView();
          SetStage("enterPlay");
          EditorApplication.isPlaying = true;
          return;

        case "enterPlay":
          if (!EditorApplication.isPlaying) {
            return;
          }

          SetStage("waitMenu");
          return;

        case "waitMenu": {
          var sfm = SceneFlowManager.Instance;
          if (sfm == null || sfm.CurrentState != SceneFlowManager.GameState.MainMenu) {
            return;
          }

          if (!Settled(MenuSettleSeconds)) {
            return;
          }

          Capture(index, dev, "menu");
          SetStage("startGame");
          return;
        }

        case "startGame":
          if (!Settled(CaptureFlushSeconds) || !LastCaptureWritten()) {
            return;
          }

          SceneFlowManager.Instance.StartGameImmediate();
          SetStage("waitGame");
          return;

        case "waitGame": {
          var sfm = SceneFlowManager.Instance;
          if (sfm.CurrentState != SceneFlowManager.GameState.Game) {
            return;
          }

          if (!Settled(GameSettleSeconds)) {
            return;
          }

          Capture(index, dev, "game");
          SetStage("endGame");
          return;
        }

        case "endGame":
          if (!Settled(CaptureFlushSeconds) || !LastCaptureWritten()) {
            return;
          }

          ForceTimerToZero();
          SetStage("waitResults");
          return;

        case "waitResults": {
          var sfm = SceneFlowManager.Instance;
          if (sfm.CurrentState != SceneFlowManager.GameState.Results) {
            return;
          }

          if (!Settled(ResultsSettleSeconds)) {
            return;
          }

          Capture(index, dev, "results");
          SetStage("exitPlay");
          return;
        }

        case "exitPlay":
          if (!Settled(CaptureFlushSeconds) || !LastCaptureWritten()) {
            return;
          }

          EditorApplication.isPlaying = false;
          SetStage("afterExit");
          return;

        case "afterExit":
          if (EditorApplication.isPlayingOrWillChangePlaymode) {
            return;
          }

          SessionState.SetInt(KeyIndex, index + 1);
          SetStage("begin");
          return;
      }
    }
    catch (Exception e) {
      Debug.LogError($"[ResolutionContactSheet] Failed at device {dev.Name} stage {stage}: {e}");
      SessionState.EraseBool(KeyActive);
      RestorePrefs();
      if (EditorApplication.isPlaying) {
        EditorApplication.isPlaying = false;
      }
    }
  }

  private const float StallSeconds = 3f;

  /// <summary>
  /// With the Unity app in the background the Game view can stop rendering entirely
  /// (Time.frameCount frozen at 1) even though EditorApplication.update keeps ticking.
  /// Re-issuing the Window/General/Game menu item reliably restarts it.
  /// </summary>
  private static void KickStalledPlayerLoop() {
    var now = (float)EditorApplication.timeSinceStartup;
    var frame = Time.frameCount;
    var lastFrame = SessionState.GetInt(KeyLastFrame, -1);
    var lastTime = SessionState.GetFloat(KeyLastFrameTime, 0f);

    if (frame != lastFrame) {
      SessionState.SetInt(KeyLastFrame, frame);
      SessionState.SetFloat(KeyLastFrameTime, now);
      return;
    }

    if (now - lastTime < StallSeconds) {
      return;
    }

    Debug.LogWarning($"[ResolutionContactSheet] Player loop stalled at frame {frame} for {StallSeconds}s — re-opening Game view.");
    EditorApplication.ExecuteMenuItem("Window/General/Game");
    EditorApplication.QueuePlayerLoopUpdate();
    SessionState.SetFloat(KeyLastFrameTime, now);
  }

  private static bool PassesFilter (Device dev) {
    var filter = SessionState.GetString(KeyFilter, "");
    if (string.IsNullOrEmpty(filter)) {
      return true;
    }

    foreach (var name in filter.Split(',')) {
      if (name.Trim().Equals(dev.Name, StringComparison.OrdinalIgnoreCase)) {
        return true;
      }
    }

    return false;
  }

  private static void SetStage (string stage) {
    SessionState.SetString(KeyStage, stage);
    SessionState.SetFloat(KeyStageTime, 0f);
  }

  /// <summary>True once <paramref name="seconds"/> have elapsed since this check first ran in the current stage.</summary>
  private static bool Settled (float seconds) {
    var now = (float)EditorApplication.timeSinceStartup;
    var started = SessionState.GetFloat(KeyStageTime, 0f);
    if (started <= 0f) {
      SessionState.SetFloat(KeyStageTime, now);
      return false;
    }

    return now - started >= seconds;
  }

  private static void Capture (int index, Device dev, string screen) {
    var outDir = SessionState.GetString(KeyOutDir, "");
    var file = $"{index:00}_{dev.Name}_{dev.Width}x{dev.Height}__{screen}.png";
    var path = Path.Combine(outDir, file);
    if (File.Exists(path)) {
      File.Delete(path);
    }

    SessionState.SetString(KeyLastCapture, path);
    SessionState.SetInt(KeyRetries, 0);
    RequestCapture(path);
    Debug.Log($"[ResolutionContactSheet] {dev.Name} {screen}: Screen={Screen.width}x{Screen.height} → {file}");
  }

  /// <summary>
  /// ScreenCapture writes asynchronously on a later rendered frame. Leaving play mode (or
  /// switching Game view size) before the file lands produces a capture of the wrong frame,
  /// so every post-capture stage also waits for the PNG to exist on disk.
  /// </summary>
  private static bool LastCaptureWritten() {
    var path = SessionState.GetString(KeyLastCapture, "");
    if (string.IsNullOrEmpty(path) || File.Exists(path)) {
      return true;
    }

    var started = SessionState.GetFloat(KeyStageTime, 0f);
    if (started > 0f && EditorApplication.timeSinceStartup - started > CaptureFlushTimeoutSeconds) {
      var retries = SessionState.GetInt(KeyRetries, 0);
      if (retries < 1) {
        // Seen once on a specific resolution: the Game view stopped producing frames after the
        // request was queued. Re-request and force a repaint before giving up.
        SessionState.SetInt(KeyRetries, retries + 1);
        SessionState.SetFloat(KeyStageTime, (float)EditorApplication.timeSinceStartup);
        Debug.LogWarning($"[ResolutionContactSheet] Capture not flushed after {CaptureFlushTimeoutSeconds}s, retrying: {Path.GetFileName(path)}");
        RequestCapture(path);
        return false;
      }

      Debug.LogWarning($"[ResolutionContactSheet] Capture never flushed: {Path.GetFileName(path)} — continuing.");
      return true;
    }

    return false;
  }

  /// <summary>Queue the screenshot and poke the Game view so a frame is actually rendered to satisfy it.</summary>
  private static void RequestCapture (string path) {
    ScreenCapture.CaptureScreenshot(path, 1);
    var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
    var gameView = EditorWindow.GetWindow(gameViewType);
    gameView.Repaint();
    EditorApplication.QueuePlayerLoopUpdate();
  }

  private static void ForceTimerToZero() {
    var gm = GameManager.Instance;
    if (gm == null) {
      throw new InvalidOperationException("GameManager.Instance is null");
    }

    var prop = typeof(GameManager).GetProperty("TimeRemaining");
    prop.SetValue(gm, 0.5f, null);
  }

  private static void Finish() {
    SessionState.EraseBool(KeyActive);
    RestorePrefs();
    var outDir = SessionState.GetString(KeyOutDir, "");
    File.WriteAllText(Path.Combine(outDir, "DONE"), DateTime.Now.ToString("o"));
    Debug.Log($"[ResolutionContactSheet] Done. {Devices.Length} devices × 3 screens → {outDir}");
  }

  // ─────────────────────────────────────────────────────────────────────────
  // PlayerPrefs protection
  // ─────────────────────────────────────────────────────────────────────────

  private static void SnapshotPrefs() {
    var parts = new string[PrefKeys.Length];
    for (var i = 0; i < PrefKeys.Length; i++) {
      parts[i] = PlayerPrefs.HasKey(PrefKeys[i]) ? PlayerPrefs.GetInt(PrefKeys[i]).ToString() : "";
    }

    SessionState.SetString(KeyPrefsSnapshot, string.Join("|", parts));
  }

  private static void RestorePrefs() {
    var raw = SessionState.GetString(KeyPrefsSnapshot, null);
    if (string.IsNullOrEmpty(raw)) {
      return;
    }

    var parts = raw.Split('|');
    for (var i = 0; i < PrefKeys.Length && i < parts.Length; i++) {
      if (parts[i] == "") {
        PlayerPrefs.DeleteKey(PrefKeys[i]);
      }
      else {
        PlayerPrefs.SetInt(PrefKeys[i], int.Parse(parts[i]));
      }
    }

    PlayerPrefs.Save();
    SessionState.EraseString(KeyPrefsSnapshot);
    Debug.Log("[ResolutionContactSheet] PlayerPrefs restored to pre-run values.");
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Game view size via editor reflection (no public API)
  // ─────────────────────────────────────────────────────────────────────────

  private static void SetGameViewSize (int width, int height) {
    var editorAsm = typeof(Editor).Assembly;
    var sizesType = editorAsm.GetType("UnityEditor.GameViewSizes");
    var singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
    var sizes = singletonType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
    var group = sizesType.GetProperty("currentGroup", BindingFlags.Public | BindingFlags.Instance).GetValue(sizes);
    var groupType = group.GetType();

    var label = $"RCS_{width}x{height}";
    var total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
    var getSize = groupType.GetMethod("GetGameViewSize");
    var foundIndex = -1;
    for (var i = 0; i < total; i++) {
      var size = getSize.Invoke(group, new object[] { i });
      var baseText = size.GetType().GetProperty("baseText").GetValue(size) as string;
      if (baseText == label) {
        foundIndex = i;
        break;
      }
    }

    if (foundIndex < 0) {
      var sizeType = editorAsm.GetType("UnityEditor.GameViewSize");
      var sizeTypeEnum = editorAsm.GetType("UnityEditor.GameViewSizeType");
      var ctor = sizeType.GetConstructor(new[] { sizeTypeEnum, typeof(int), typeof(int), typeof(string) });
      var newSize = ctor.Invoke(new object[] { Enum.Parse(sizeTypeEnum, "FixedResolution"), width, height, label });
      groupType.GetMethod("AddCustomSize").Invoke(group, new object[] { newSize });
      foundIndex = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1;
    }

    var gameViewType = editorAsm.GetType("UnityEditor.GameView");
    var gameView = EditorWindow.GetWindow(gameViewType);
    var selected = gameViewType.GetProperty("selectedSizeIndex",
      BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    selected.SetValue(gameView, foundIndex, null);
    gameView.Repaint();
  }

  private static void FocusGameView() {
    var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
    var gameView = EditorWindow.GetWindow(gameViewType);
    gameView.Show();
    gameView.Focus();
  }
}
#endif
