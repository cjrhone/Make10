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
    /// Coroutines in the loaded scene tween on Time.deltaTime; the Editor only
    /// ticks those while the Game view has focus unless runInBackground is set.
    /// The test runner can run unfocused, so pin it on for the whole suite.
    /// </summary>
    [OneTimeSetUp]
    public void EnableBackgroundExecution()
    {
        Application.runInBackground = true;
    }

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

    // --- Core swap gameplay loop (P2) ----------------------------------------

    /// <summary>
    /// End-to-end Arcade swap: complete a line that sums to a multiple of 10 and
    /// verify the whole loop — match detection, scoring, and cascade/gravity refill.
    ///
    /// We reach the Game state without the tutorial/countdown UI (which needs
    /// popup interaction and seconds of real time) by spawning the grid, forcing a
    /// known solvable board, then activating the round. The swap itself goes through
    /// GridManager's real swap chokepoint (via the <c>BeginSwap</c> test hook) — the
    /// single path every tap-tap / swipe / drag swap funnels into — so match/score/
    /// cascade logic runs exactly as it does in play. Overwriting tile values makes
    /// the fixture fully deterministic without depending on the weighted tile bag.
    /// </summary>
    [UnityTest]
    public IEnumerator ArcadeSwap_CompletesLine_ScoresAndResolves()
    {
        SceneManager.LoadScene(SceneName);
        yield return null;
        yield return WaitForBootstrap(8f);

        GameManager gm = GameManager.Instance;
        GridManager grid = Object.FindFirstObjectByType<GridManager>();
        Assert.IsNotNull(gm, "GameManager.Instance is null after scene load.");
        Assert.IsNotNull(grid, "No GridManager found in the loaded scene.");

        gm.SetGameMode(GameManager.GameMode.Arcade);
        grid.SpawnGridOnly();       // build the grid + tiles, no match processing yet
        yield return null;          // let CreateTile / layout settle

        int gw = grid.GridColumns;
        int gh = grid.GridRows;
        // The fixture math (below) assumes the shipped 5x5 board. Fail loudly with a
        // clear reason if the grid size ever changes rather than mis-asserting.
        Assert.AreEqual(5, gw, "Fixture assumes a 5-wide grid.");
        Assert.AreEqual(5, gh, "Fixture assumes a 5-tall grid.");

        // Known board — no row/column is a multiple of 10 yet:
        //   col0 = 3,1,1,1,1 = 7    col1 = 6,2,1,1,1 = 11    row0 = 3,6,1,1,1 = 12
        // Swapping [0,0]<->[1,0] moves the 6 into col0:
        //   col0 = 6,1,1,1,1 = 10   col1 = 3,2,1,1,1 = 8     row0 unchanged = 12
        // => exactly one completed line (col0), sum 10.
        for (int y = 0; y < gh; y++)
            for (int x = 0; x < gw; x++)
                grid.GetTileAt(x, y).SetValue(1);
        grid.GetTileAt(0, 0).SetValue(3);
        grid.GetTileAt(1, 0).SetValue(6);
        grid.GetTileAt(1, 1).SetValue(2);

        gm.ActivateGame();          // IsGameActive = true, Score = 0, bar = 0, solveCount = 0
        grid.OnRoundStarted();
        Assert.IsTrue(gm.IsGameActive, "Game did not activate.");
        Assert.AreEqual(0, gm.Score, "Score should start at 0.");

        Tile tileA = grid.GetTileAt(0, 0);   // value 3
        Tile tileB = grid.GetTileAt(1, 0);   // value 6 (horizontal neighbor)
        int scoreBefore = gm.Score;

        grid.BeginSwap(tileA, tileB);

        // Wait for swap animation -> match -> cascade -> gravity/refill to fully resolve.
        yield return null;
        yield return null;
        float elapsed = 0f;
        while (grid.IsBusy && elapsed < 12f)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        Assert.IsFalse(grid.IsBusy, "Grid never finished processing the swap (timed out).");

        // --- Scoring (CLAUDE.md: lineSum x multiplier + speed bonus) --------------
        // First player solve: lineSum 10 x1.00 (bar 0->10 => x1.00 tier) + 0 speed
        // bonus (lastPlayerSolveTime unset on round start) = 10 BP. Any excess is
        // cascade BP from the refill, so assert a lower bound on Score but pin the
        // multiplier-bar and solve-count facts exactly (cascades touch neither).
        Assert.Greater(gm.Score, scoreBefore, "Score did not increase after completing a line.");
        Assert.GreaterOrEqual(gm.Score, 10, "Player match should award at least the line sum (10 BP).");
        Assert.AreEqual(1, gm.SolveCount, "Exactly one player solve expected (cascades don't count).");
        // The bar gains +10 on the player swap, then drains ~1 BP/sec while the
        // cascade and this poll run — so it lands in (0, 10], not an exact value.
        Assert.Greater(gm.MultiplierBar, 0f, "Multiplier bar should be active after a player swap (was 0).");
        Assert.LessOrEqual(gm.MultiplierBar, 10f, "Multiplier bar exceeded a single swap's +10 fill.");
        Assert.IsTrue(gm.IsMultiplierActive, "Multiplier should register as active after the swap.");

        // --- Cascade / gravity resolved: board fully refilled ---------------------
        for (int y = 0; y < gh; y++)
            for (int x = 0; x < gw; x++)
                Assert.IsNotNull(grid.GetTileAt(x, y),
                    $"Cell [{x},{y}] is empty — gravity/refill did not resolve.");

        Assert.IsTrue(gm.IsGameActive, "Game unexpectedly ended during the swap loop.");
    }
}
