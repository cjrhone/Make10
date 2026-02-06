using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generic utility for loading ScriptableObject assets from Resources (with editor fallback).
/// Eliminates duplicated asset-loading code across ShopManager, DebugUpgradePanel, and UpgradeConfirmWindow.
/// </summary>
public static class DataLoader
{
    /// <summary>
    /// Load all assets of type T from a Resources subfolder, with an editor fallback path.
    /// </summary>
    /// <param name="resourcesFolder">Resources subfolder name (e.g., "Upgrades")</param>
    /// <param name="editorFallbackPath">Editor asset path (e.g., "Assets/Data/Upgrades")</param>
    /// <returns>List of loaded assets</returns>
    public static List<T> LoadAll<T>(string resourcesFolder, string editorFallbackPath) where T : ScriptableObject
    {
        List<T> results = new List<T>();

        // Try Resources.LoadAll first (works in builds)
        T[] loaded = Resources.LoadAll<T>(resourcesFolder);
        if (loaded.Length > 0)
        {
            results.AddRange(loaded);
            return results;
        }

        // Editor fallback for development
        #if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { editorFallbackPath });
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            T asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                results.Add(asset);
        }
        #endif

        return results;
    }

    /// <summary>
    /// Load all UpgradeData assets.
    /// </summary>
    public static List<UpgradeData> LoadUpgrades()
    {
        return LoadAll<UpgradeData>("Upgrades", "Assets/Data/Upgrades");
    }

    /// <summary>
    /// Load all SnackData assets.
    /// </summary>
    public static List<SnackData> LoadSnacks()
    {
        return LoadAll<SnackData>("Snacks", "Assets/Data/Snacks");
    }

    /// <summary>
    /// Load all ArtifactData assets.
    /// </summary>
    public static List<ArtifactData> LoadArtifacts()
    {
        return LoadAll<ArtifactData>("Artifacts", "Assets/Data/Artifacts");
    }
}
