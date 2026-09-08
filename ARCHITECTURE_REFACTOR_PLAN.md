# Architecture Refactor Plan

Source: code + architecture review, 2026-09-07 (branch `feat/urp-migration`).
Quick fixes from that review are already applied (see git log). This file covers
the items too large to do inline. Each is independently shippable; order is by
payoff ÷ risk.

Status legend: `[ ]` not started · `[~]` in progress · `[x]` done

---

## R1. Scoring EditMode tests (~half day)

**Why first:** the cascade rule in `GameManager.ProcessCascadeSolve` drifted from
CLAUDE.md and nobody noticed. The 5 PlayMode tests are scene smoke tests; nothing
pins the scoring maths.

**Steps**
1. `[x]` Add `Assets/Tests/EditMode/Make10.Tests.EditMode.asmdef` (references
   `Make10.Runtime`, `nunit.framework.dll`, constraint `UNITY_INCLUDE_TESTS`).
2. `[x]` Extract pure scoring into a static class `ScoringRules`
   (`Assets/Scripts/ScoringRules.cs`):
   - `int PlayerSolve(int lineSum, float multiplier, bool speedBonus)`
   - `int CascadeSolve(GameMode mode, int lineSum, int chainIndex)`
   - `float MultiplierForBar(float bar)` (tiers 0/25/50/75/100)
   - `int StarsFor(GameMode mode, int bp)`
   `GameManager` calls these; behaviour unchanged.
3. `[ ]` Tests: one per rule in CLAUDE.md "Scoring & Multiplier", plus tier
   boundaries (24/25, 49/50, 74/75, 99/100) and the Arcade +1/+2/+3 chain.
4. `[ ]` `TileWeightManager.RefillTileBag`: test bag is always 25 and matches
   largest-remainder expectation for a fixed weight table.

**Done when:** `Unity -runTests -testPlatform EditMode` green; CLAUDE.md scoring
section cites `ScoringRules`.

---

## R2. Split `GridManager` (2680 lines, 35 coroutine sites) (~2 days)

**Why:** every gameplay bug lands here. Zen gravity/merge, Arcade cascades, hints,
drag input and reshuffle share one `isProcessing` flag and one `StopAllCoroutines`.

**Target shape**
```
GridManager        grid array, spawn/clear, tile lookup, SaveGridToData/Restore   (~600)
GridInput          Tile static-event handlers, drag/swipe state, selection       (~400)
CascadeResolver    Arcade match → clear → refill → re-check loop                 (~500)
ZenBoardResolver   merge into locked tile, gravity, reshuffle-with-locks         (~600)
HintSystem         inactivity timer + particle trail                             (~200)
```
Each resolver is a plain C# class owned by `GridManager` and given a
`MonoBehaviour` host for coroutines (or converted to `async`/UniTask later).

**Steps**
1. `[ ]` Move hint timer + `ShowHint*` into `HintSystem` (lowest coupling, proves
   the pattern).
2. `[ ]` Move Zen-only methods (`AnimateZenMatch`, gravity, `ZenResetGridWithEffect`,
   locked-tile helpers) into `ZenBoardResolver`.
3. `[ ]` Move Arcade cascade loop into `CascadeResolver`.
4. `[ ]` Move `Tile.OnTile*` subscriptions and drag state into `GridInput`.
5. `[ ]` Replace the single `isProcessing` bool with an explicit
   `enum BoardPhase { Idle, Swapping, Resolving, Frozen }`; `GridInput` only
   accepts input in `Idle`.
6. `[ ]` `FreezeGrid()` stops only resolver coroutines, not hint/VFX ones.

**Done when:** no file in `Assets/Scripts` exceeds ~800 lines; PlayMode
`ArcadeSwap_CompletesLine_ScoresAndResolves` still green; Zen manual smoke
(merge, gravity, reshuffle ×3, save/resume) passes.

---

## R3. Event-driven VFX and UI (~1 day)

**Why:** `GridManager` reaches into 7 singletons, `UIManager` into 6,
`TenExplosionVFX` calls `UIManager`/`AudioManager` 7 times directly. `GameManager`
already publishes 7 events, so half the plumbing exists.

**Steps**
1. `[ ]` Add to `GameManager`: `OnLineCleared(LineClearInfo)`,
   `OnCascadeStep(int chainIndex)`, `OnZenTileLocked(int value)`.
2. `[ ]` `GridVFX`, `TenExplosionVFX`, `HotStreakEffect`, `AvatarManager` subscribe
   in `OnEnable`, unsubscribe in `OnDisable`. Remove their `*.Instance` reaches.
3. `[ ]` `TenExplosionVFX` particle-arrival → raise `OnParticleScoreArrived`;
   `UIManager` listens for tick SFX + bar bounce (it already owns the slider).
4. `[ ]` `GridManager` keeps only `GameManager` + `AudioManager` references.

**Done when:** `grep -c "\.Instance" Assets/Scripts/*VFX*.cs` is 0 and a VFX
object can be deleted from the scene without a null-ref in gameplay.

---

## R4. Split `UIManager` (2590 lines) and `SceneFlowManager` (1498) (~1.5 days)

**UIManager →**
- `HudView` (score, timer, multiplier bar, hot-streak text)
- `ResultsView` (Arcade + Zen results, stars, count-up)
- `PauseView`
- `UIManager` becomes a thin router that owns the three views.

**SceneFlowManager →**
- Keep the state machine + `RunTransition` guard.
- Move popup builders (`ShowShopComingSoonPopup`, `ShowCreditsPopup`,
  `ShowOptionsPopup`, `ShowZenDisclaimer`, `ShowZenResumePrompt`) into
  `MenuPopups.cs`.
- Move `SetupSafeArea` / panel geometry into `PanelLayout.cs`.

**Also:** make `CurrentState` a precondition, not a post-condition. Set
`CurrentState = GameState.Transitioning` at the top of `TransitionGuard`; the
`isTransitioning` bool added in the quick fix then goes away.

---

## R5. URP 2D Renderer (~1 hour, needs Unity open)

Current: `Make10_URPAsset` uses `UniversalRenderer` (`m_RendererType: 1`).
Portrait UGUI-only game, Screen Space Overlay canvas. HDR is now off.

1. `[ ]` Assets → Create → Rendering → URP 2D Renderer → `Make10_2DRenderer`.
2. `[ ]` On `Make10_URPAsset`, set Renderer List [0] to it.
3. `[ ]` Re-run `Screenshots/` regression montage (see `AGENTS.md`); diff against
   `Screenshots/urp/`.
4. `[ ]` Confirm `Shaders/UIAdditive.shader` still passes
   `UIAdditiveShader_IsAvailable_AndSupported`.

Skip if the montage shows any change on the beam/glow VFX; the gain is small.

---

## R6. `RunManager` cleanup before Shop (Session P) (~2 hours)

- `TotalBP` / `SpendableBP` read `PlayerPrefs` on every access. Cache in fields,
  refresh in `BankBP()` / `SpendBP()`.
- `BankBP()` and `SpendBP()` call `PlayerPrefs.Save()` individually; batch to one
  `Save()` at end of round.
- Unused `RoundNumber` / `OnRoundChanged` (single-round game now): delete or
  document why kept.

---

## Not planned (reviewed, left as-is)

- `GlowTextureGenerator` static cache: now cleared in `SceneFlowManager.OnDestroy`.
  Only matters if the project grows a second scene.
- `AudioManager` shared-source SFX glitch: superseded by the FMOD migration (L3/K).
- `MatchChecker.WouldCreateMatch` null-tile `?? 0` fallback: masks corruption but
  never observed in play; revisit with R2 `BoardPhase`.
