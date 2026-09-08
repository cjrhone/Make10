using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Unity.Android.Types;

// Headless build entry points, invoked from the command line via
//   Unity -batchmode -quit -executeMethod BuildScript.BuildAndroid
//   Unity -batchmode -quit -executeMethod BuildScript.BuildiOS
// (see Tools/build_android.py, Tools/build_ios.py, Tools/build_all.py).
//
// Output path comes from M10_BUILD_OUTPUT:
//   Android - absolute path to the .aab to write.
//   iOS     - absolute path to the directory that receives the Xcode project.
//
// Android keystore passwords are read from the environment so no secret is
// ever stored in the project:
//   M10_KEYSTORE_PASS  - password for the keystore
//   M10_KEYALIAS_PASS  - password for the key alias
// The alias itself lives in Player Settings (alias "make10").
//
// The keystore PATH also lives in Player Settings, but the committed value is
// machine-specific. Set M10_KEYSTORE_PATH to override it at build time so the
// pipeline is portable across machines / CI without touching ProjectSettings.
//
// Versioning: bundleVersion (marketing version) and AndroidBundleVersionCode
// are the single source of truth (bumped by Tools/bump_version.py). The iOS
// build number (CFBundleVersion) is set to the Android version code at build
// time so one bump covers both stores and both stay monotonic.
public static class BuildScript {
  public static void BuildAndroid() {
    var keystorePass = Environment.GetEnvironmentVariable("M10_KEYSTORE_PASS");
    var aliasPass = Environment.GetEnvironmentVariable("M10_KEYALIAS_PASS");
    var keystorePath = Environment.GetEnvironmentVariable("M10_KEYSTORE_PATH");
    var output = RequireOutput("absolute path to the .aab to write");

    if (string.IsNullOrEmpty(keystorePass) || string.IsNullOrEmpty(aliasPass)) {
      Fail("M10_KEYSTORE_PASS and M10_KEYALIAS_PASS must both be set in the environment.");
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

    var options = new BuildPlayerOptions {
      scenes = EnabledScenes(),
      locationPathName = output,
      target = BuildTarget.Android,
      targetGroup = BuildTargetGroup.Android,
      options = BuildOptions.None
    };

    Debug.Log($"[BuildScript] Building AAB -> {output} " +
              $"(v{PlayerSettings.bundleVersion} code {PlayerSettings.Android.bundleVersionCode}, {options.scenes.Length} scene(s))");

    Run(options, output);
  }

  // Exports an Xcode project. Archiving, signing and .ipa export happen in
  // xcodebuild afterwards (Tools/build_ios.py) — Unity never touches certs.
  public static void BuildiOS() {
    var output = RequireOutput("absolute path to the Xcode project directory to write");

    var teamId = Environment.GetEnvironmentVariable("M10_APPLE_TEAM_ID");
    if (!string.IsNullOrEmpty(teamId)) {
      PlayerSettings.iOS.appleDeveloperTeamID = teamId;
    }

    if (string.IsNullOrEmpty(PlayerSettings.iOS.appleDeveloperTeamID)) {
      Fail("Apple Team ID is empty. Set it in Player Settings or export M10_APPLE_TEAM_ID.");
    }

    // Xcode's "Automatically manage signing" picks the dev/distribution cert
    // and provisioning profile per build action, so one export serves both a
    // device run and an App Store archive.
    PlayerSettings.iOS.appleEnableAutomaticSigning = true;

    // CFBundleVersion must be unique per upload to App Store Connect. Reuse the
    // Android version code so Tools/bump_version.py drives both stores.
    var buildNumber = PlayerSettings.Android.bundleVersionCode.ToString();
    PlayerSettings.iOS.buildNumber = buildNumber;

    EditorUserBuildSettings.iOSXcodeBuildConfig = XcodeBuildConfig.Release;

    var options = new BuildPlayerOptions {
      scenes = EnabledScenes(),
      locationPathName = output,
      target = BuildTarget.iOS,
      targetGroup = BuildTargetGroup.iOS,
      options = BuildOptions.None
    };

    Debug.Log($"[BuildScript] Exporting Xcode project -> {output} " +
              $"(v{PlayerSettings.bundleVersion} build {buildNumber}, team {PlayerSettings.iOS.appleDeveloperTeamID}, " +
              $"{options.scenes.Length} scene(s))");

    Run(options, output);
  }

  private static string RequireOutput (string what) {
    var output = Environment.GetEnvironmentVariable("M10_BUILD_OUTPUT");
    if (string.IsNullOrEmpty(output)) {
      Fail($"M10_BUILD_OUTPUT ({what}) must be set.");
    }

    return output;
  }

  private static string[] EnabledScenes() {
    var scenes = EditorBuildSettings.scenes
      .Where(s => s.enabled)
      .Select(s => s.path)
      .ToArray();

    if (scenes.Length == 0) {
      Fail("No enabled scenes in Build Settings — nothing to build.");
    }

    return scenes;
  }

  private static void Run (BuildPlayerOptions options, string output) {
    var report = BuildPipeline.BuildPlayer(options);
    var summary = report.summary;

    if (summary.result == BuildResult.Succeeded) {
      Debug.Log(
        $"[BuildScript] SUCCESS: {summary.totalSize / (1024 * 1024)} MB in {summary.totalTime.TotalSeconds:F0}s -> {output}");
      EditorApplication.Exit(0);
    }
    else {
      Fail($"Build {summary.result} with {summary.totalErrors} error(s).");
    }
  }

  private static void Fail (string message) {
    Debug.LogError("[BuildScript] " + message);
    EditorApplication.Exit(1);
  }
}