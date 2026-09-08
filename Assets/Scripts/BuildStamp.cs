using UnityEngine;

/// <summary>
/// What this build IS, as one string for a bug report: "1.2.0" in a player,
/// "1.2.0+1a8be87" in the editor.
///
/// The version half is Application.version, which Unity fills from
/// PlayerSettings.bundleVersion. release-please syncs bundleVersion to
/// version.txt in every release PR, so the label on the main menu moves with
/// each release with no per-build work. Same shape as Hot Trash Summer's stamp.
/// </summary>
public static class BuildStamp
{
    private static string cached;

    public static string Version
    {
        get
        {
            if (cached != null) return cached;

#if UNITY_EDITOR
            string sha = EditorSha();
            cached = string.IsNullOrEmpty(sha) ? Application.version : $"{Application.version}+{sha}";
#else
            cached = Application.version;
#endif
            return cached;
        }
    }

#if UNITY_EDITOR
    private static string EditorSha()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git", "rev-parse --short HEAD")
            {
                WorkingDirectory = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..")),
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var p = System.Diagnostics.Process.Start(psi))
            {
                string sha = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit();
                return p.ExitCode == 0 ? sha : null;
            }
        }
        catch (System.Exception e)
        {
            // No git on PATH, or not a repo: not worth a console error on every open.
            Debug.LogWarning($"[BuildStamp] Could not read git sha: {e.Message}");
            return null;
        }
    }
#endif
}
