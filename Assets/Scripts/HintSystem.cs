using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Inactivity hint: after <see cref="Settings.hintDelay"/> seconds without a move
/// (Arcade only), highlight a valid swap and repeat every
/// <see cref="Settings.hintRepeatInterval"/> seconds until the player acts.
/// Arcade draws a directional particle trail; Zen pulses the two tiles.
///
/// Plain C# class owned by <see cref="GridManager"/> (R2 step 1). Coroutines run on
/// the host MonoBehaviour so <c>GridManager.FreezeGrid()</c>'s StopAllCoroutines
/// still halts them — R2 step 6 separates that.
/// </summary>
public class HintSystem {
  [System.Serializable]
  public class Settings {
    public bool enableHints = true;
    public float hintDelay = 10f;
    public float hintRepeatInterval = 3f;
    public int hintParticleCount = 5;
    public float hintParticleSpeed = 120f;
    public float hintParticleLifetime = 0.5f;
    public float hintParticleSize = 12f;
    public Color hintParticleColor = new(1f, 0.9f, 0.3f, 0.9f);
  }

  private readonly GridManager grid;
  private readonly MonoBehaviour host;
  private readonly Settings settings;

  private float timeSinceLastMove = 0f;
  private float timeSinceLastHint = 0f;
  private bool hintActive = false;
  private HintMove currentHint = null;
  private readonly List<GameObject> activeHintParticles = new();

  public HintSystem (GridManager grid, MonoBehaviour host, Settings settings) {
    this.grid = grid;
    this.host = host;
    this.settings = settings;
  }

  /// <summary>
  /// Advance the inactivity timer. Call once per frame from the owner's Update.
  /// No-op while hints are disabled, the game is inactive, in Zen, or while the
  /// board is processing.
  /// </summary>
  public void Tick (float deltaTime, bool isProcessing) {
    if (!settings.enableHints) {
      return;
    }

    if (GameManager.Instance == null || !GameManager.Instance.IsGameActive) {
      return;
    }

    if (GameManager.Instance.CurrentMode == GameManager.GameMode.Zen) {
      return; // No hints in Zen — let the player think
    }

    if (isProcessing) {
      return;
    }

    timeSinceLastMove += deltaTime;

    // Check if it's time to show a hint
    if (timeSinceLastMove >= settings.hintDelay) {
      timeSinceLastHint += deltaTime;

      // Show hint periodically
      if (!hintActive || timeSinceLastHint >= settings.hintRepeatInterval) {
        ShowHint();
        timeSinceLastHint = 0f;
      }
    }
  }

  /// <summary>Reset the inactivity timer and clear any visible hint. Call on any player interaction.</summary>
  public void Reset() {
    timeSinceLastMove = 0f;
    timeSinceLastHint = 0f;
    hintActive = false;
    currentHint = null;
    ClearParticles();
  }

  /// <summary>Find a valid move and highlight it immediately (also used by the debug context menu).</summary>
  public void ShowHint() {
    var matchChecker = grid.matchChecker;
    if (matchChecker == null) {
      return;
    }

    // Find a valid move
    currentHint = matchChecker.FindHintMove();

    if (currentHint != null && currentHint.tile != null) {
      hintActive = true;

      if (currentHint.targetTile != null) {
        // Zen: pulse both tiles to highlight the swap pair
        host.StartCoroutine(PulseZenHintTiles(currentHint));
        Debug.Log($"<color=yellow>HINT:</color> Swap {currentHint.tile} ↔ {currentHint.targetTile}");
      }
      else {
        // Arcade: directional particle trail
        host.StartCoroutine(SpawnHintParticles(currentHint));
        Debug.Log($"<color=yellow>HINT:</color> Swipe {currentHint.tile} {currentHint.direction}");
      }
    }
  }

  /// <summary>
  /// Zen hint: gentle scale pulse on both tiles in the hint pair.
  /// Repeating pulses handled by the hint timer re-triggering ShowHint.
  /// </summary>
  private IEnumerator PulseZenHintTiles (HintMove hint) {
    if (hint.tile == null) {
      yield break;
    }

    // Pulse first tile
    AnimationUtilities.PunchScale(hint.tile.GetRectTransform(), 1.1f, 0.25f);
    yield return new WaitForSeconds(0.12f);

    // Pulse second tile (staggered for visual clarity)
    if (hint.targetTile != null) {
      AnimationUtilities.PunchScale(hint.targetTile.GetRectTransform(), 1.1f, 0.25f);
    }
  }

  private IEnumerator SpawnHintParticles (HintMove hint) {
    if (hint.tile == null) {
      yield break;
    }

    var tilePos = hint.tile.GetRectTransform().anchoredPosition;
    var direction = hint.GetDirectionVector();

    // Spawn particles in a burst
    for (var i = 0; i < settings.hintParticleCount; i++) {
      SpawnSingleHintParticle(tilePos, direction, i * 0.06f);
      yield return new WaitForSeconds(0.04f);
    }
  }

  private void SpawnSingleHintParticle (Vector2 startPos, Vector2 direction, float delay) {
    host.StartCoroutine(AnimateHintParticle(startPos, direction, delay));
  }

  private IEnumerator AnimateHintParticle (Vector2 startPos, Vector2 direction, float delay) {
    yield return new WaitForSeconds(delay);

    var scaleFactor = grid.ScaleFactor;

    // Create particle
    var particle = new GameObject("HintParticle");
    particle.transform.SetParent(grid.GridContainer, false);
    activeHintParticles.Add(particle);

    var rt = particle.AddComponent<RectTransform>();

    // Start slightly behind center, end ahead (scaled)
    var startOffset = -20f * scaleFactor;
    var endOffset = 60f * scaleFactor;
    rt.anchoredPosition = startPos + direction * startOffset;
    rt.sizeDelta = new Vector2(settings.hintParticleSize * scaleFactor, settings.hintParticleSize * scaleFactor);
    rt.localEulerAngles = new Vector3(0, 0, 45f); // Diamond shape

    var img = particle.AddComponent<Image>();
    img.color = settings.hintParticleColor;
    img.raycastTarget = false;

    // Animate: move in direction, fade out, shrink
    var elapsed = 0f;
    var velocity = direction * settings.hintParticleSpeed * scaleFactor;

    // Add slight randomness (scaled)
    var wobble = Random.Range(-15f, 15f) * scaleFactor;
    var perpendicular = new Vector2(-direction.y, direction.x);

    while (elapsed < settings.hintParticleLifetime) {
      if (particle == null) {
        yield break;
      }

      elapsed += Time.deltaTime;
      var t = elapsed / settings.hintParticleLifetime;

      // Move forward
      var pos = startPos + direction * Mathf.Lerp(startOffset, endOffset, t);
      pos += perpendicular * Mathf.Sin(t * Mathf.PI * 2f) * wobble * (1f - t);
      rt.anchoredPosition = pos;

      // Fade: appear quickly, fade out slowly
      float alpha;
      if (t < 0.2f) {
        alpha = t / 0.2f; // Fade in
      }
      else {
        alpha = 1f - (t - 0.2f) / 0.8f; // Fade out
      }

      img.color = new Color(settings.hintParticleColor.r, settings.hintParticleColor.g, settings.hintParticleColor.b,
        settings.hintParticleColor.a * alpha);

      // Scale: start small, grow, then shrink
      var scale = Mathf.Sin(t * Mathf.PI) * 1.2f + 0.3f;
      rt.localScale = Vector3.one * scale;

      yield return null;
    }

    // Cleanup
    if (particle != null) {
      activeHintParticles.Remove(particle);
      Object.Destroy(particle);
    }
  }

  /// <summary>Destroy any live hint particles (grid clear / reset).</summary>
  public void ClearParticles() {
    foreach (var p in activeHintParticles) {
      if (p != null) {
        Object.Destroy(p);
      }
    }

    activeHintParticles.Clear();
  }
}
