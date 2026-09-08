using UnityEngine;

/// <summary>
/// Pure scoring rules for Make10. No state, no Unity lifecycle — every method is a
/// function of its arguments so the maths can be pinned by EditMode tests.
/// <see cref="GameManager"/> owns the per-round state (bar level, streak, timers)
/// and delegates every number to this class. See CLAUDE.md "Scoring &amp; Multiplier".
/// </summary>
public static class ScoringRules {
  // --- Arcade multiplier bar -------------------------------------------------

  /// <summary>Bar range is 0..BarMax. Reaching BarMax triggers Hot Streak.</summary>
  public const float BarMax = 100f;

  /// <summary>Bar gain per player swap (once per swap, not per line).</summary>
  public const float BarGainPerSolve = 10f;

  /// <summary>Bar drain per second while idle (frozen during cascades and Hot Streak).</summary>
  public const float BarDrainPerSecond = 1f;

  /// <summary>Multiplier applied while Hot Streak is active (bar at BarMax).</summary>
  public const float HotStreakMultiplier = 5f;

  // --- Speed bonus -----------------------------------------------------------

  /// <summary>Flat BP added to a player-swap line when solved within the speed window.</summary>
  public const int SpeedBonusBP = 5;

  // --- Star thresholds (BP) --------------------------------------------------

  public const int ArcadeStar1 = 300;
  public const int ArcadeStar2 = 600;
  public const int ArcadeStar3 = 1000;

  public const int ZenStar1 = 500;
  public const int ZenStar2 = 1000;
  public const int ZenStar3 = 2000;

  /// <summary>
  /// BP for one line cleared by a player swap:
  /// <c>round(lineSum × multiplier) + (speedBonus ? SpeedBonusBP : 0)</c>.
  /// Zen passes multiplier 1 and no speed bonus (flat lineSum).
  /// Hot Streak passes <see cref="HotStreakMultiplier"/> and no speed bonus.
  /// </summary>
  public static int PlayerSolve (int lineSum, float multiplier, bool speedBonus) {
    var multiplied = Mathf.RoundToInt(lineSum * multiplier);
    return multiplied + (speedBonus ? SpeedBonusBP : 0);
  }

  /// <summary>
  /// BP for one line cleared during an auto-cascade (cascadeCount ≥ 2).
  /// No multiplier, no speed bonus, no bar fill.
  /// Arcade: +1, +2, +3… per line in the chain (<paramref name="chainIndex"/> is 1-based
  /// and resets each chain). Zen: flat <paramref name="lineSum"/>.
  /// </summary>
  public static int CascadeSolve (GameManager.GameMode mode, int lineSum, int chainIndex) {
    return mode == GameManager.GameMode.Arcade ? chainIndex : lineSum;
  }

  /// <summary>
  /// Multiplier tier for a bar level.
  /// 0–24 ×1.00 · 25–49 ×1.50 · 50–74 ×2.00 · 75–99 ×2.50 · 100 ×5.00 (Hot Streak).
  /// </summary>
  public static float MultiplierForBar (float bar) {
    if (bar >= BarMax) {
      return HotStreakMultiplier;
    }

    if (bar >= 75f) {
      return 2.5f;
    }

    if (bar >= 50f) {
      return 2f;
    }

    if (bar >= 25f) {
      return 1.5f;
    }

    return 1f;
  }

  /// <summary>Bar level after one player swap: +BarGainPerSolve, clamped to BarMax.</summary>
  public static float BarAfterSolve (float bar) {
    return Mathf.Min(bar + BarGainPerSolve, BarMax);
  }

  /// <summary>Bar level after <paramref name="deltaTime"/> seconds of idle drain, clamped to 0.</summary>
  public static float BarAfterDrain (float bar, float deltaTime) {
    return Mathf.Max(0f, bar - BarDrainPerSecond * deltaTime);
  }

  /// <summary>Star thresholds (1★, 2★, 3★) for a mode.</summary>
  public static (int star1, int star2, int star3) StarThresholds (GameManager.GameMode mode) {
    return mode == GameManager.GameMode.Zen ? (ZenStar1, ZenStar2, ZenStar3) : (ArcadeStar1, ArcadeStar2, ArcadeStar3);
  }

  /// <summary>Star rating (0–3) for total BP earned in a round.</summary>
  public static int StarsFor (GameManager.GameMode mode, int bp) {
    var (t1, t2, t3) = StarThresholds(mode);
    if (bp >= t3) {
      return 3;
    }

    if (bp >= t2) {
      return 2;
    }

    if (bp >= t1) {
      return 1;
    }

    return 0;
  }
}