using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Unity.Android.Types;

// Headless Android build entry point, invoked from the command line via
//   Unity -batchmode -quit -executeMethod BuildScript.BuildAndroid
// (see Tools/build_android.py). Produces a signed .aab ready for Google Play.
//
// Keystore passwords are read from the environment so no secret is ever stored
// in the project:
//   M10_KEYSTORE_PASS  - password for the keystore
//   M10_KEYALIAS_PASS  - password for the key alias
// The alias itself lives in Player Settings (alias "make10").
//
// The keystore PATH also lives in Player Settings, but the committed value is
// machine-specific. Set M10_KEYSTORE_PATH to override it at build time so the
// pipeline is portable across machines / CI without touching ProjectSettings.
//
// Output path comes from M10_BUILD_OUTPUT (absolute path to the .aab to write).
public static class BuildScript {
  public static void BuildAndroid() {
    var keystorePass = Environment.GetEnvironmentVariable("M10_KEYSTORE_PASS");
    var aliasPass = Environment.GetEnvironmentVariable("M10_KEYALIAS_PASS");
    var keystorePath = Environment.GetEnvironmentVariable("M10_KEYSTORE_PATH");
    var output = Environment.GetEnvironmentVariable("M10_BUILD_OUTPUT");

    if (string.IsNullOrEmpty(keystorePass) || string.IsNullOrEmpty(aliasPass)) {
      Fail("M10_KEYSTORE_PASS and M10_KEYALIAS_PASS must both be set in the environment.");
    }

    if (string.IsNullOrEmpty(output)) {
      Fail("M10_BUILD_OUTPUT (absolute path to the .aab to write) must be set.");
    }

    // Sign with the existing upload keystore configured in Player Settings.
    PlayerSettings.Android.useCustomKeystore = true;
    // Optional env override so the committed keystore path stays machine-agnostic.
    if (!string.IsNullOrEmpty(keystorePath)) {
      PlayerSettings.Android.keystoreName = keystorePath;
    }
    PlayerSettings.Android.keystorePass = keystorePass;
    PlayerSettings.Android.keyaliasPass = aliasPass;

    // Build an App Bundle (.aab), not an APK — Play needs the bundle.
    EditorUserBuildSettings.buildAppBundle = true;

    // Emit native debug symbols as a symbols.zip next to the .aab so Google Play
    // can symbolicate native crashes and ANRs. SymbolTable is enough for ANR
    // stack symbolication (Full also embeds DWARF debug info — much larger).
    UserBuildSettings.DebugSymbols.level = DebugSymbolLevel.SymbolTable;
    UserBuildSettings.DebugSymbols.format = DebugSymbolFormat.Zip;

    var scenes = EditorBuildSettings.scenes
      .Where(s => s.enabled)
      .Select(s => s.path)
      .ToArray();

    if (scenes.Length == 0) {
      Fail("No enabled scenes in Build Settings — nothing to build.");
    }

    var options = new BuildPlayerOptions {
      scenes = scenes,
      locationPathName = output,
      target = BuildTarget.Android,
      targetGroup = BuildTargetGroup.Android,
      options = BuildOptions.None
    };

    Debug.Log($"[BuildScript] Building AAB -> {output} " +
              $"(v{PlayerSettings.bundleVersion} code {PlayerSettings.Android.bundleVersionCode}, {scenes.Length} scene(s))");

    var report = BuildPipeline.BuildPlayer(options);
    var summary = report.summary;

    if (summary.result == BuildResult.Succeeded) {
      Debug.Log($"[BuildScript] SUCCESS: {summary.totalSize / (1024 * 1024)} MB in {summary.totalTime.TotalSeconds:F0}s -> {output}");
      EditorApplication.Exit(0);
    }
    else {
      Fail($"Build {summary.result} with {summary.totalErrors} error(s).");
    }
  }

  private static void Fail(string message) {
    Debug.LogError("[BuildScript] " + message);
    EditorApplication.Exit(1);
  }
}
