using System.Linq;
using NUnit.Framework;

/// <summary>
/// Pins the tile-bag distribution (CLAUDE.md "Grid System": tile bag of 25,
/// Tetris-style). RefillTileBag itself reads the GameManager singleton and
/// UnityEngine.Random, so the pure largest-remainder step is tested directly.
/// </summary>
public class TileWeightManagerTests {
  /// <summary>Serialized defaults on TileWeightManager (weight0..weight7, then 8/9 = 0).</summary>
  private static readonly float[] DefaultWeights =
    { 0.12f, 0.28f, 0.26f, 0.22f, 0.08f, 0f, 0f, 0f, 0f, 0f };

  [Test]
  public void BagSize_IsTwentyFive() {
    Assert.AreEqual(25, TileWeightManager.BAG_SIZE);
  }

  [Test]
  public void BagCountsFor_DefaultWeights_SumsToBagSize() {
    var counts = TileWeightManager.BagCountsFor(DefaultWeights, TileWeightManager.BAG_SIZE);
    Assert.AreEqual(TileWeightManager.BAG_SIZE, counts.Sum());
  }

  [Test]
  public void BagCountsFor_DefaultWeights_MatchesLargestRemainder() {
    // total 0.96 → exact slots: 3.125, 7.292, 6.771, 5.729, 2.083 (floor sum 23).
    // Two leftover slots go to the largest remainders: value 2 (.771), value 3 (.729).
    var counts = TileWeightManager.BagCountsFor(DefaultWeights, 25);
    CollectionAssert.AreEqual(new[] { 3, 7, 7, 6, 2, 0, 0, 0, 0, 0 }, counts);
  }

  [Test]
  public void BagCountsFor_ZeroWeightValues_NeverAppear() {
    var counts = TileWeightManager.BagCountsFor(DefaultWeights, 25);
    for (var v = 5; v <= 9; v++) {
      Assert.AreEqual(0, counts[v], $"value {v} has zero weight but got {counts[v]} slots");
    }
  }

  [Test]
  public void BagCountsFor_ExactSplit_NoRemainderPass() {
    var counts = TileWeightManager.BagCountsFor(new[] { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f }, 25);
    CollectionAssert.AreEqual(new[] { 5, 5, 5, 5, 5 }, counts);
  }

  [Test]
  public void BagCountsFor_TiedRemainders_LowerIndexWins() {
    // 25/3 = 8.333 each; one leftover slot; strict '>' keeps the first bucket.
    var counts = TileWeightManager.BagCountsFor(new[] { 1f, 1f, 1f }, 25);
    CollectionAssert.AreEqual(new[] { 9, 8, 8 }, counts);
  }

  [Test]
  public void BagCountsFor_UnnormalisedWeights_SameAsNormalised() {
    var a = TileWeightManager.BagCountsFor(DefaultWeights, 25);
    var b = TileWeightManager.BagCountsFor(DefaultWeights.Select(w => w * 100f).ToArray(), 25);
    CollectionAssert.AreEqual(a, b);
  }

  [Test]
  public void BagCountsFor_FullArcadeRamp_StillSumsToBagSize() {
    // Approximates GetArcadeAdjustedWeights at 12 solves: base ×0.85, 5/6/7 at max.
    var ramped = new[] {
      0.12f * 0.85f, 0.28f * 0.85f, 0.26f * 0.85f, 0.22f * 0.85f, 0.08f * 0.85f,
      0.10f, 0.06f, 0.02f, 0f, 0f
    };
    var counts = TileWeightManager.BagCountsFor(ramped, TileWeightManager.BAG_SIZE);
    Assert.AreEqual(TileWeightManager.BAG_SIZE, counts.Sum());
    Assert.Greater(counts[5], 0, "5s should be in the bag at full ramp");
  }
}