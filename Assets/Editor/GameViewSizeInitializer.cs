#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;

/// <summary>
/// Registers a fixed "iPhone13_1170x2532" Game view resolution on editor load so
/// the whole team sees the same viewport when comparing UI. Game view sizes
/// normally live in the per-machine Library (not version-controlled), so without
/// this the size vanishes on a fresh checkout / Library wipe. Idempotent: only
/// adds the size when it's missing, across the common mobile build-target groups.
///
/// Uses UnityEditor internal reflection (GameViewSizes / GameViewSize) because
/// there is no public API for custom Game view sizes; wrapped in try/catch so an
/// internal-API change in a future Unity version degrades to a no-op, never a
/// compile/runtime break.
/// </summary>
[InitializeOnLoad]
public static class GameViewSizeInitializer
{
    private const int Width = 1170;   // iPhone 13 — 390pt @3x
    private const int Height = 2532;
    private const string Label = "iPhone13_1170x2532";

    static GameViewSizeInitializer()
    {
        // Defer past domain-reload/import churn before touching editor singletons.
        EditorApplication.delayCall += EnsureSizeRegistered;
    }

    private static void EnsureSizeRegistered()
    {
        try
        {
            Assembly editorAsm = typeof(Editor).Assembly;
            Type sizesType = editorAsm.GetType("UnityEditor.GameViewSizes");
            Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singletonType
                .GetProperty("instance", BindingFlags.Public | BindingFlags.Static)
                .GetValue(null);

            Type groupEnumType = editorAsm.GetType("UnityEditor.GameViewSizeGroupType");
            MethodInfo getGroup = sizesType.GetMethod("GetGroup");

            foreach (string groupName in new[] { "Standalone", "Android", "iOS" })
            {
                object groupValue;
                try { groupValue = Enum.Parse(groupEnumType, groupName); }
                catch { continue; } // group type not present in this Unity version
                object group = getGroup.Invoke(sizes, new object[] { Convert.ToInt32(groupValue) });
                AddIfMissing(editorAsm, group);
            }
        }
        catch
        {
            // Internal editor API drifted across Unity versions — non-fatal.
        }
    }

    private static void AddIfMissing(Assembly editorAsm, object group)
    {
        Type groupType = group.GetType();
        int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
        MethodInfo getSize = groupType.GetMethod("GetGameViewSize");
        for (int i = 0; i < total; i++)
        {
            object size = getSize.Invoke(group, new object[] { i });
            string baseText = size.GetType().GetProperty("baseText").GetValue(size) as string;
            if (baseText == Label) return; // already registered for this group
        }

        Type sizeType = editorAsm.GetType("UnityEditor.GameViewSize");
        Type sizeTypeEnum = editorAsm.GetType("UnityEditor.GameViewSizeType");
        ConstructorInfo ctor = sizeType.GetConstructor(
            new[] { sizeTypeEnum, typeof(int), typeof(int), typeof(string) });
        object newSize = ctor.Invoke(new object[]
        {
            Enum.Parse(sizeTypeEnum, "FixedResolution"), Width, Height, Label
        });
        groupType.GetMethod("AddCustomSize").Invoke(group, new object[] { newSize });
    }
}
#endif
