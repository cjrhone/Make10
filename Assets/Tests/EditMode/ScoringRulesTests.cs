using NUnit.Framework;
using GameMode = GameManager.GameMode;

/// <summary>
/// Pins the scoring rules documented in CLAUDE.md "Scoring &amp; Multiplier".
/// Every number here is a spec value; if a test fails, either the code drifted
/// or CLAUDE.md needs a deliberate update — never silently change the test.
/// </summary>
public class ScoringRulesTests {
  // --- Player swap: lineSum × multiplier (+ speed bonus) -------------------

  [TestCase(10, 1f, 10)]
  [TestCase(20, 1f, 20)]
  [TestCase(10, 1.5f, 15)]
  [TestCase(30, 1.5f, 45)]
  [TestCase(10, 2f, 20)]
  [TestCase(10, 2.5f, 25)]
  [TestCase(40, 2.5f, 100)]
  public void PlayerSolve_IsLineSumTimesMultiplier (int lineSum, float multiplier, int expected) {
    Assert.AreEqual(expected, ScoringRules.PlayerSolve(lineSum, multiplier, speedBonus: false));
  }

  [Test]
  public void PlayerSolve_SpeedBonus_AddsFlatFiveBP() {
    Assert.AreEqual(5, ScoringRules.SpeedBonusBP);
    Assert.AreEqual(15, ScoringRules.PlayerSolve(10, 1f, speedBonus: true));
    Assert.AreEqual(30, ScoringRules.PlayerSolve(10, 2.5f, speedBonus: true));
  }

  [Test]
  public void PlayerSolve_SpeedBonus_IsNotMultiplied() {
    var withBonus = ScoringRules.PlayerSolve(10, 2.5f, speedBonus: true);
    var without = ScoringRules.PlayerSolve(10, 2.5f, speedBonus: false);
    Assert.AreEqual(ScoringRules.SpeedBonusBP, withBonus - without);
  }

  [Test]
  public void PlayerSolve_HotStreak_IsTimesFive() {
    Assert.AreEqual(5f, ScoringRules.HotStreakMultiplier);
    Assert.AreEqual(50, ScoringRules.PlayerSolve(10, ScoringRules.HotStreakMultiplier, false));
    Assert.AreEqual(150, ScoringRules.PlayerSolve(30, ScoringRules.HotStreakMultiplier, false));
  }

  [Test]
  public void PlayerSolve_ZenFlat_IsLineSum() {
    // Zen calls PlayerSolve with multiplier 1 and no speed bonus.
    foreach (var sum in new[] { 10, 20, 30, 40, 50 }) {
      Assert.AreEqual(sum, ScoringRules.PlayerSolve(sum, 1f, false));
    }
  }

  // --- Cascade: no multiplier, no speed bonus --------------------------------

  [Test]
  public void CascadeSolve_Arcade_IsPlusOnePlusTwoPlusThree() {
    Assert.AreEqual(1, ScoringRules.CascadeSolve(GameMode.Arcade, 10, chainIndex: 1));
    Assert.AreEqual(2, ScoringRules.CascadeSolve(GameMode.Arcade, 10, chainIndex: 2));
    Assert.AreEqual(3, ScoringRules.CascadeSolve(GameMode.Arcade, 10, chainIndex: 3));
  }

  [Test]
  public void CascadeSolve_Arcade_IgnoresLineSum() {
    Assert.AreEqual(2, ScoringRules.CascadeSolve(GameMode.Arcade, 40, chainIndex: 2));
  }

  [Test]
  public void CascadeSolve_Arcade_ChainTotalsSixForThreeLines() {
    var total = 0;
    for (var i = 1; i <= 3; i++) {
      total += ScoringRules.CascadeSolve(GameMode.Arcade, 10, i);
    }

    Assert.AreEqual(6, total);
  }

  [TestCase(10)]
  [TestCase(20)]
  [TestCase(30)]
  public void CascadeSolve_Zen_IsFlatLineSum (int lineSum) {
    Assert.AreEqual(lineSum, ScoringRules.CascadeSolve(GameMode.Zen, lineSum, chainIndex: 1));
    Assert.AreEqual(lineSum, ScoringRules.CascadeSolve(GameMode.Zen, lineSum, chainIndex: 3));
  }

  // --- Multiplier bar tiers ------------------------------------------------

  [TestCase(0f, 1f)]
  [TestCase(24f, 1f)]
  [TestCase(25f, 1.5f)]
  [TestCase(49f, 1.5f)]
  [TestCase(50f, 2f)]
  [TestCase(74f, 2f)]
  [TestCase(75f, 2.5f)]
  [TestCase(99f, 2.5f)]
  [TestCase(100f, 5f)]
  public void MultiplierForBar_TierBoundaries (float bar, float expected) {
    Assert.AreEqual(expected, ScoringRules.MultiplierForBar(bar));
  }

  [Test]
  public void MultiplierForBar_JustBelowTier_StaysInLowerTier() {
    Assert.AreEqual(1f, ScoringRules.MultiplierForBar(24.9f));
    Assert.AreEqual(1.5f, ScoringRules.MultiplierForBar(49.9f));
    Assert.AreEqual(2f, ScoringRules.MultiplierForBar(74.9f));
    Assert.AreEqual(2.5f, ScoringRules.MultiplierForBar(99.9f));
  }

  [Test]
  public void BarAfterSolve_AddsTen_PerSwap() {
    Assert.AreEqual(10f, ScoringRules.BarGainPerSolve);
    Assert.AreEqual(10f, ScoringRules.BarAfterSolve(0f));
    Assert.AreEqual(60f, ScoringRules.BarAfterSolve(50f));
  }

  [Test]
  public void BarAfterSolve_ClampsAtHundred() {
    Assert.AreEqual(100f, ScoringRules.BarMax);
    Assert.AreEqual(100f, ScoringRules.BarAfterSolve(95f));
    Assert.AreEqual(100f, ScoringRules.BarAfterSolve(100f));
  }

  [Test]
  public void BarAfterSolve_TenSwapsFromZero_ReachesHotStreak() {
    var bar = 0f;
    for (var i = 0; i < 10; i++) {
      bar = ScoringRules.BarAfterSolve(bar);
    }

    Assert.AreEqual(ScoringRules.BarMax, bar);
    Assert.AreEqual(ScoringRules.HotStreakMultiplier, ScoringRules.MultiplierForBar(bar));
  }

  [Test]
  public void BarAfterDrain_DrainsOnePerSecond() {
    Assert.AreEqual(1f, ScoringRules.BarDrainPerSecond);
    Assert.AreEqual(49f, ScoringRules.BarAfterDrain(50f, 1f));
    Assert.AreEqual(49.5f, ScoringRules.BarAfterDrain(50f, 0.5f), 1e-5f);
  }

  [Test]
  public void BarAfterDrain_ClampsAtZero() {
    Assert.AreEqual(0f, ScoringRules.BarAfterDrain(0.5f, 1f));
    Assert.AreEqual(0f, ScoringRules.BarAfterDrain(0f, 1f));
  }

  // --- Star ratings ----------------------------------------------------------

  [TestCase(0, 0)]
  [TestCase(299, 0)]
  [TestCase(300, 1)]
  [TestCase(599, 1)]
  [TestCase(600, 2)]
  [TestCase(999, 2)]
  [TestCase(1000, 3)]
  [TestCase(5000, 3)]
  public void StarsFor_Arcade_300_600_1000 (int bp, int expected) {
    Assert.AreEqual(expected, ScoringRules.StarsFor(GameMode.Arcade, bp));
  }

  [TestCase(0, 0)]
  [TestCase(499, 0)]
  [TestCase(500, 1)]
  [TestCase(999, 1)]
  [TestCase(1000, 2)]
  [TestCase(1999, 2)]
  [TestCase(2000, 3)]
  public void StarsFor_Zen_500_1000_2000 (int bp, int expected) {
    Assert.AreEqual(expected, ScoringRules.StarsFor(GameMode.Zen, bp));
  }

  [Test]
  public void StarThresholds_MatchClaudeMd() {
    Assert.AreEqual((300, 600, 1000), ScoringRules.StarThresholds(GameMode.Arcade));
    Assert.AreEqual((500, 1000, 2000), ScoringRules.StarThresholds(GameMode.Zen));
  }
}
