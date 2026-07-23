using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// PlayMode integration tests for Make10.
///
/// These double as the render-pipeline regression gate for the BRP -> URP
/// migration: the UI/Additive shader test fails if the custom shader stops
/// compiling under the active pipeline, and the Overlay-canvas test pins the
/// invariant (Screen Space - Overlay) that keeps the migration visually safe.
/// The suite must be green on BOTH pipelines.
/// </summary>
public class Make10PlayModeTests
{
    private const string SceneName = "Make10Scene";

    /// <summary>
    /// Waits up to <paramref name="timeoutSeconds"/> real seconds (frame by frame)
    /// for the core singletons to come online after a scene load.
    /// </summary>
    private static IEnumerator WaitForBootstrap(float timeoutSeconds)
    {
        float elapsed = 0f;
        while ((GameManager.Instance == null || SceneFlowManager.Instance == null) && elapsed < timeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // --- Render-pipeline regression gate -------------------------------------

    [Test]
    public void UIAdditiveShader_IsAvailable_AndSupported()
    {
        Shader shader = Shader.Find("UI/Additive");
        Assert.IsNotNull(shader,
            "Shader 'UI/Additive' not found — GridVFX beams/sparkles would silently disappear.");

        Material mat = new Material(shader);
        try
        {
            Assert.IsTrue(mat.shader.isSupported,
                "Shader 'UI/Additive' is not supported on the active render pipeline.");
        }
        finally
        {
            Object.DestroyImmediate(mat);
        }
    }

    // --- Scene / bootstrap ----------------------------------------------------

    [UnityTest]
    public IEnumerator SceneLoads_CoreSingletonsInitialize()
    {
        SceneManager.LoadScene(SceneName);
        yield return null;
        yield return WaitForBootstrap(8f);

        Assert.IsNotNull(GameManager.Instance, "GameManager.Instance is null after scene load.");
        Assert.IsNotNull(SceneFlowManager.Instance, "SceneFlowManager.Instance is null after scene load.");
        Assert.IsNotNull(Object.FindFirstObjectByType<GridManager>(),
            "No GridManager found in the loaded scene.");
    }

    [UnityTest]
    public IEnumerator Scene_UsesScreenSpaceOverlayCanvas()
    {
        SceneManager.LoadScene(SceneName);
        yield return null;
        yield return WaitForBootstrap(8f);

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        Assert.IsNotNull(canvas, "No Canvas found in the scene.");
        // The whole game renders through a Screen Space - Overlay canvas, which
        // bypasses the SRP. This is why BRP -> URP is low risk; if this ever
        // changes, the migration's visual assumptions no longer hold.
        Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode,
            "Root Canvas is not Screen Space - Overlay; render-pipeline assumptions changed.");
    }

    [UnityTest]
    public IEnumerator Scene_HasSingleCamera()
    {
        SceneManager.LoadScene(SceneName);
        yield return null;
        yield return WaitForBootstrap(8f);

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        Assert.AreEqual(1, cameras.Length,
            "Expected exactly one Camera in the scene (baseline is a single orthographic camera).");
    }
}
