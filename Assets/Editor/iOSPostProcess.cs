#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

// Runs after every iOS export (editor button and BuildScript.BuildiOS alike)
// and patches the generated Xcode project's Info.plist.
public static class iOSPostProcess {
  [PostProcessBuild(1)]
  public static void OnPostprocessBuild (BuildTarget target, string pathToBuiltProject) {
    if (target != BuildTarget.iOS) {
      return;
    }

    var plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
    var plist = new PlistDocument();
    plist.ReadFromFile(plistPath);

    // Export compliance. Make10 is offline and uses no encryption beyond what
    // the OS provides, so it is exempt. Without this key App Store Connect
    // blocks every new build behind the "App Encryption Documentation" dialog.
    plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);

    plist.WriteToFile(plistPath);
    Debug.Log("[iOSPostProcess] Info.plist: ITSAppUsesNonExemptEncryption = false");
  }
}
#endif
