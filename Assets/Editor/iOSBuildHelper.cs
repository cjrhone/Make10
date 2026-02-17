#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

/// <summary>
/// Editor utility for Make10 iOS build preparation.
/// Menu: Make10 > iOS Build Setup
/// </summary>
public class iOSBuildHelper : EditorWindow
{
    private const string ICON_FOLDER = "Assets/Icons";
    private const string BUILD_PATH = "Builds/iOS";

    [MenuItem("Make10/iOS Build Setup", false, 100)]
    public static void ShowWindow()
    {
        var window = GetWindow<iOSBuildHelper>("iOS Build Setup");
        window.minSize = new Vector2(400, 520);
    }

    [MenuItem("Make10/Open Player Settings (iOS)", false, 101)]
    public static void OpenPlayerSettings()
    {
        SettingsService.OpenProjectSettings("Project/Player");
    }

    private Vector2 scrollPos;

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("Make10 — iOS Build Setup", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // ── Status Checks ──
        GUILayout.Label("Pre-Flight Checks", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        DrawCheck("Bundle ID", PlayerSettings.applicationIdentifier == "com.wizardbodega.make10"
            ? "com.wizardbodega.make10" : PlayerSettings.applicationIdentifier,
            PlayerSettings.applicationIdentifier == "com.wizardbodega.make10");

        DrawCheck("Version", PlayerSettings.bundleVersion,
            !string.IsNullOrEmpty(PlayerSettings.bundleVersion) && PlayerSettings.bundleVersion != "0.1");

        DrawCheck("Build Number", PlayerSettings.iOS.buildNumber,
            !string.IsNullOrEmpty(PlayerSettings.iOS.buildNumber) && PlayerSettings.iOS.buildNumber != "0");

        // Use scriptingBackend check without the obsolete enum comparison
        DrawCheck("Orientation", PlayerSettings.defaultInterfaceOrientation.ToString(),
            PlayerSettings.defaultInterfaceOrientation == UIOrientation.Portrait);

        DrawCheck("Auto Signing", PlayerSettings.iOS.appleEnableAutomaticSigning ? "Enabled" : "Disabled",
            PlayerSettings.iOS.appleEnableAutomaticSigning);

        string teamId = PlayerSettings.iOS.appleDeveloperTeamID;
        DrawCheck("Team ID", string.IsNullOrEmpty(teamId) ? "NOT SET" : teamId,
            !string.IsNullOrEmpty(teamId));

        DrawCheck("Target iOS", PlayerSettings.iOS.targetOSVersionString,
            !string.IsNullOrEmpty(PlayerSettings.iOS.targetOSVersionString));

        // Check if icon files exist
        bool hasIcons = Directory.Exists(ICON_FOLDER) &&
            Directory.GetFiles(ICON_FOLDER, "icon_*.png").Length > 0;
        int iconCount = hasIcons ? Directory.GetFiles(ICON_FOLDER, "icon_*.png").Length : 0;
        DrawCheck("Icon Files", hasIcons ? $"{iconCount} found in {ICON_FOLDER}" : "MISSING", hasIcons);

        EditorGUILayout.Space(15);

        // ── Icon Setup ──
        GUILayout.Label("Icon Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        if (hasIcons)
        {
            EditorGUILayout.HelpBox(
                $"Found {iconCount} icon files in {ICON_FOLDER}.\n\n" +
                "Click below to set the default app icon. For iOS-specific icon slots,\n" +
                "open Player Settings > iOS > Icon and drag your icons from Assets/Icons/.",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox($"No icon files found in {ICON_FOLDER}!", MessageType.Error);
        }

        if (GUILayout.Button("Set Default Icon (1024x1024)", GUILayout.Height(28)))
        {
            AssignDefaultIcon();
        }

        EditorGUILayout.Space(3);

        if (GUILayout.Button("Open Player Settings > iOS Icons", GUILayout.Height(28)))
        {
            SettingsService.OpenProjectSettings("Project/Player");
            Debug.Log("[Make10] Opened Player Settings. Switch to the iOS tab, then expand the Icon section to assign icons.");
        }

        EditorGUILayout.Space(3);

        if (GUILayout.Button("Ping Icon Folder in Project", GUILayout.Height(25)))
        {
            var folder = AssetDatabase.LoadAssetAtPath<Object>(ICON_FOLDER);
            if (folder != null)
            {
                EditorGUIUtility.PingObject(folder);
                Selection.activeObject = folder;
            }
        }

        EditorGUILayout.Space(15);

        // ── Launch Screen ──
        GUILayout.Label("Launch Screen", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        EditorGUILayout.HelpBox(
            "Launch screen has been pre-configured in ProjectSettings to use\n" +
            "splash_1080x1920.png with a matching dark background.\n\n" +
            "To change it: Player Settings > iOS > Splash Image > Launch Screen",
            MessageType.Info);

        if (GUILayout.Button("Open Player Settings > iOS Splash", GUILayout.Height(25)))
        {
            SettingsService.OpenProjectSettings("Project/Player");
        }

        EditorGUILayout.Space(15);

        // ── Team ID ──
        GUILayout.Label("Apple Developer Team ID", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "Enter your Apple Developer Team ID from developer.apple.com.\n" +
            "Go to Account > Membership Details to find your Team ID.",
            MessageType.Info);

        string newTeamId = EditorGUILayout.TextField("Team ID", PlayerSettings.iOS.appleDeveloperTeamID);
        if (newTeamId != PlayerSettings.iOS.appleDeveloperTeamID)
        {
            PlayerSettings.iOS.appleDeveloperTeamID = newTeamId;
            Debug.Log($"[Make10] Set Apple Developer Team ID to: {newTeamId}");
        }

        EditorGUILayout.Space(15);

        // ── Build ──
        GUILayout.Label("Build", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        EditorGUILayout.HelpBox(
            "This will build an Xcode project to Builds/iOS.\n" +
            "You'll then open it in Xcode to archive and submit to App Store Connect.",
            MessageType.Info);

        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
        if (GUILayout.Button("BUILD iOS (Xcode Project)", GUILayout.Height(40)))
        {
            BuildiOS();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(10);
        EditorGUILayout.EndScrollView();
    }

    private void DrawCheck(string label, string value, bool pass)
    {
        EditorGUILayout.BeginHorizontal();
        var style = new GUIStyle(EditorStyles.label);
        style.richText = true;
        string icon = pass ? "<color=#22aa22>✓</color>" : "<color=#cc3333>✗</color>";
        GUILayout.Label(icon, style, GUILayout.Width(20));
        GUILayout.Label($"{label}: {value}");
        EditorGUILayout.EndHorizontal();
    }

    // ─── Icon Assignment ───

    private static void AssignDefaultIcon()
    {
        var icon1024 = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ICON_FOLDER}/icon_1024x1024.png");
        if (icon1024 == null)
        {
            // Try the source icon as fallback
            icon1024 = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ICON_FOLDER}/icon_source_1024.png");
        }

        if (icon1024 == null)
        {
            Debug.LogError("[Make10] No 1024x1024 icon found in Assets/Icons/!");
            EditorUtility.DisplayDialog("Icon Not Found",
                "Could not find icon_1024x1024.png or icon_source_1024.png in Assets/Icons/.",
                "OK");
            return;
        }

        // Set the default (fallback) icon used across all platforms
        Texture2D[] defaultIcons = { icon1024 };
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, defaultIcons);

        AssetDatabase.SaveAssets();
        Debug.Log("[Make10] ✓ Default icon set to " + AssetDatabase.GetAssetPath(icon1024));
        EditorUtility.DisplayDialog("Icon Set",
            "Default app icon has been set to your 1024x1024 icon.\n\n" +
            "For iOS-specific icon slots, go to:\n" +
            "Player Settings > iOS tab > Icon section\n" +
            "and drag your icons from Assets/Icons/ into each slot.",
            "OK");
    }

    // ─── Build ───

    private static void BuildiOS()
    {
        string buildPath = Path.Combine(Directory.GetCurrentDirectory(), BUILD_PATH);

        if (!Directory.Exists(buildPath))
        {
            Directory.CreateDirectory(buildPath);
        }

        // Get all scenes in build settings
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("[Make10] No scenes found in Build Settings! Add your scene first.");
            EditorUtility.DisplayDialog("Build Error",
                "No scenes found in Build Settings.\nGo to File > Build Settings and add Make10Scene.",
                "OK");
            return;
        }

        // Pre-build validation
        if (string.IsNullOrEmpty(PlayerSettings.iOS.appleDeveloperTeamID))
        {
            bool proceed = EditorUtility.DisplayDialog("Warning: No Team ID",
                "Apple Developer Team ID is not set.\n\n" +
                "The Xcode project will build but you'll need to set the team in Xcode before archiving.\n\n" +
                "Continue anyway?",
                "Continue", "Cancel");
            if (!proceed) return;
        }

        Debug.Log($"[Make10] Starting iOS build to {buildPath}...");
        Debug.Log($"[Make10] Scenes: {string.Join(", ", scenes)}");

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = buildPath,
            target = BuildTarget.iOS,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"[Make10] ✓ iOS build succeeded! ({report.summary.totalTime.TotalSeconds:F1}s)");
            Debug.Log($"[Make10] Xcode project at: {buildPath}");
            EditorUtility.DisplayDialog("Build Succeeded!",
                $"iOS Xcode project built successfully!\n\n" +
                $"Location: {buildPath}\n\n" +
                "Next steps:\n" +
                "1. Open the .xcodeproj in Xcode\n" +
                "2. Select your Team in Signing & Capabilities\n" +
                "3. Product > Archive\n" +
                "4. Distribute App > App Store Connect",
                "OK");
            EditorUtility.RevealInFinder(buildPath);
        }
        else
        {
            Debug.LogError($"[Make10] Build failed: {report.summary.result}");
            EditorUtility.DisplayDialog("Build Failed",
                $"iOS build failed with {report.summary.totalErrors} error(s).\n\nCheck the Console for details.",
                "OK");
        }
    }
}
#endif
