# Make10 — Post-MakeZen Session Plan
**Created:** February 28, 2026 | **Status:** All MakeZen MVP sessions (A–E) complete

---

## Current State

MakeZen is feature-complete: locked tiles, merge/gravity, swap-any mechanic, 7-tier difficulty ramp, 300s timer with failed-swap penalties, full UI/results, and animated reverts. The remaining work falls into three tracks: **bug fixes**, **code hygiene**, and **Launch Sprint polish layers** (L0–L7).

Sessions are ordered by dependency and impact. Each is scoped to a single sitting.

---

## Session F — Critical Bug Fixes & Guards (~30 min)

**Why first:** These are crash risks and logic errors that could bite you during playtesting.

1. **Fix `StartNewGame()` crash** — `GameManager.cs` calls `gridManager.ResetGame()`, which doesn't exist. Replace with correct reset call or remove the dead code path entirely.
2. **Double game-over guard** — `OnFailedSwap()` can call `TimeUp()` in the same frame as `Update()`. Add `if (!isGameActive) return;` guard at the top of `TimeUp()`.
3. **SafeArea duplication** — `SceneFlowManager.SetupSafeArea()` creates a new container every `Awake()` call. Add a null check: `if (safeAreaContainer != null) return;`.
4. **Hint particle cleanup** — `ClearHintParticles()` should call `StopAllCoroutines()` on hint particle GameObjects before destroying them, or track coroutines and stop them explicitly.

**Files touched:** GameManager.cs, SceneFlowManager.cs, GridManager.cs

---

## Session G — Dead Code Purge (~45 min)

**Why next:** Removing 1,150 lines of noise makes every future session cleaner and reduces cognitive load.

### Delete entire files (12 files):
- `PlayerInventory.cs`
- `CampaignManager.cs`
- `RunManager.cs`
- `DataLoader.cs`
- `DebugUpgradePanel.cs`
- `ShopCard.cs` *(note: L6 Shop will rebuild this from scratch)*
- `UI/ExampleWindow.cs`
- `UI/UpgradeConfirmWindow.cs`
- `Data/ArtifactData.cs`
- `Data/SnackData.cs`
- `Data/UpgradeData.cs`
- `Data/UpgradeType.cs`

### Remove dead methods from active files:
- **GameManager.cs:** `CalculateEnhancedNumberBonus()`, `CalculateZeroTimeBonus()`, `ApplyPostScoringBonuses()`, `CalculateCommonBonuses()`, `GetCurrentWeights()`, `GetCurrentGridSize()`, unused events `OnEnhancedNumberBonus` / `OnTimeBonus`, field `postWinDelay`
- **GridManager.cs:** `tileFallDelay`, `postClearDelay`
- **Tile.cs:** `numberPulseSpeed`, `numberPulseMinScale`, `numberPulseMaxScale`, `numberBrightenAmount`
- **MatchChecker.cs:** `targetSum` (never read)

### Cleanup references:
- Grep all `.cs` files for references to deleted classes, remove/comment out
- Remove `#pragma warning disable` lines for fields that no longer exist
- Delete any orphaned `.meta` files

**Files touched:** 16+ files (mostly deletions)

---

## Session H — Constants & Code Quality (~30 min)

**Why:** Makes the codebase readable and prevents magic-number drift during polish sessions.

1. **Extract magic numbers into named constants:**
   - GameManager: `MultiplierDrainRate`, `SpeedBonusThreshold`, `TimeBonusPerMatch`, `ZenFailedSwapPenalty`, `ZenMaxReshuffles`, `HotStreakMultiplier`, `HotStreakDuration`
   - GridManager: `TileFallSpeed`, `HintDelay`, `HintRepeatInterval`
   - Tile: `TileBackgroundGrey`, `LockedTileThreshold`
2. **Add mode helper:** `public bool IsZenMode => CurrentMode == GameMode.Zen;` to GameManager. Replace all 8+ inline checks.
3. **Consistent null checking:** Audit UIManager init methods, add null guards for all serialized panel references.

**Files touched:** GameManager.cs, GridManager.cs, Tile.cs, UIManager.cs

---

## Session I — L0: Easing & Motion Overhaul (~1.5 hours)

**Why:** Every animation currently uses linear Lerp. This is the single highest-impact polish change — it makes both modes feel alive.

### Part 1: Easing Library (AnimationUtilities.cs)
Add 5 static easing functions:
- `EaseOutCubic(t)` — landing, settling
- `EaseInOutCubic(t)` — transitions
- `EaseOutBack(t)` — popups, UI overshoot
- `EaseOutElastic(t)` — springy bounce
- `EaseInCubic(t)` — anticipation

### Part 2: Thread through existing methods
- `PunchScale()` → EaseOutBack up, EaseOutCubic down
- `PopIn()` → EaseOutBack pop, EaseOutCubic settle
- `FloatAndFade()` → EaseOutCubic position, EaseInCubic alpha
- `ScaleIn()` → EaseOutBack
- `ScaleOut()` → EaseInCubic
- `DropIn()` → EaseOutElastic

### Part 3: Tile motion
- Tile fall: time-based (0.25s) with EaseOutCubic + landing bounce (~5px, EaseOutElastic, 0.08s)
- Tile swap: EaseInOutCubic + shallow arc (`yOffset = sin(t * PI) * 8f`)
- Countdown pop: EaseOutBack (1.5→1), "GO!" uses EaseOutElastic (1.8→1)
- Menu title: replace 3 overlapping sine waves with single smooth EaseInOutCubic bob

**Files touched:** AnimationUtilities.cs, GridManager.cs, SceneFlowManager.cs, MainMenuUI.cs

---

## Session J — L1: Tile Visual Overhaul (~1 hour)

**Why:** Tiles are the main thing players look at. Small visual upgrades compound.

1. **Shadows** — Already built in `Tile.cs`, gated behind `isEnhanced`. Ungating was done for locked tiles (Session A), but regular tiles still don't have shadows. Remove the gate for ALL tiles. Derive shadow color: `Color.Lerp(NumberColors[value], Color.black, 0.65f)`.
2. **Tinted backgrounds** — Replace uniform grey with subtle tint: `Color.Lerp(Color.white, NumberColors[value], 0.12f)`.
3. **Rounded corners** — Create a white 64×64 rounded-rect PNG (~12px radius), 9-slice it, assign to tile prefab Background Image. *(Requires Unity Editor.)*
4. **Selection feedback** — `PunchScale(1.15f, 0.1f)` on first selection. `Handheld.Vibrate()` for haptic. Animate deselection (EaseOutCubic scale to 1.0 over 0.08s).

**Files touched:** Tile.cs (+ Unity Editor for rounded-rect sprite)

---

## Session K — L3: FMOD Migration + Adaptive Audio (~2–3 hours)

**Why:** Fixes the audio dropout bug AND enables adaptive music. This is the experiential layer that makes matches *feel good*.

### Part 1: FMOD Setup
- Install FMOD for Unity plugin
- Create FMOD Studio project
- Migrate existing .wav SFX into FMOD events
- Replace AudioManager internals (keep public API, swap to `RuntimeManager.PlayOneShot()`)
- Set up FMOD buses: Master, Music, SFX, Voice

### Part 2: Adaptive Music Architecture
- Single multi-track event with parameters: `Intensity` (0→1), `Inactivity` (0→1), `GameMode` (0/1)
- Layer triggers: Base (always), Rhythm (mult ≥ 1.5), Melody (mult ≥ 2.0), Intensity (hot streak)
- Parameter updates in `GameManager.Update()`

### Part 3: Tuned Match SFX
- Pitch via FMOD parameter: 10-sum → root, 20-sum → major 3rd, 30-sum → perfect 5th
- Cascade pitch: level 1→1.0, level 2→1.12, level 3→1.25, level 4→1.5

**Dependency:** Music stems (4 per mode) are CJ-driven parallel work.

**Files touched:** AudioManager.cs, GameManager.cs, FMOD Studio project (new)

---

## Session L — L5: Match VFX Reorder + Haptics (~1 hour)

**Why:** The match sequence currently fires beam before the "10" popup, which buries the payoff moment.

1. **Reorder sequence:** convergence → "10" popup (PunchScale EaseOutBack) → 0.12s hold → beam (reduced opacity) → particles → avatar → screen shake → haptic
2. **Beam opacity reduction:** glow 0.6→0.35, core 1.0→0.7, sparkles 0.9→0.6
3. **"10" popup enhancement:** increase overshoot 1.2→1.4, add screen shake on pop, scale by match sum (20→1.2×, 30→1.5×)
4. **Haptics:** `Handheld.Vibrate()` on tile selection, match clear, hot streak trigger, game over

**Files touched:** GridManager.cs, GridVFX.cs

---

## Session M — L4: Ambient Life (~45 min)

**Why:** The world between matches should feel alive, not static.

1. **Ambient particles:** 15–20 particles, 0.12–0.15 alpha, size variation, tinted to board. Hot streak: 30+, 0.25 alpha, warm. Zen: slower drift (12 px/s), cooler.
2. **Idle tile breathing:** `1.0 + sin(Time.time * 0.5 + tileIndex * 0.3) * 0.008` — wave across grid, gated behind `!IsSelected && !isAnimating`.
3. **Score environment feedback:** non-matched tiles micro-punch (1.02, 0.06s) with ripple on match. Hot streak start: all tiles flash bright (30% white, 0.15s). Multiplier increase: particles 2× speed briefly.

**Files touched:** GridVFX.cs, Tile.cs

---

## Session N — Shaders (~1.5 hours)

**Why:** Zero custom shaders currently. These are the highest visual-bang-for-buck additions.

1. **Locked Tile Glow** — Animated outer glow with tier-based color. Properties: `_GlowColor`, `_GlowIntensity`, `_PulseSpeed`, `_CornerRadius`. Sinusoidal pulse.
2. **Beam Flash** — Single-quad gold gradient beam with UV scrolling. Replace multi-sprite approach in GridVFX.
3. **Hot Streak Overlay** — Fullscreen heat shimmer/wave distortion. Properties: `_Intensity`, `_DistortionAmount`, `_ColorTint`.

All shaders: built-in pipeline, unlit, UI-compatible (`Overlay` or `Transparent` queue).

**Files touched:** New shader files in Assets/Shaders/, GridVFX.cs, Tile.cs, HotStreakEffect.cs

---

## Session O — L7: Credits, Leaderboard, Tutorial Polish (~1 hour)

**Why:** Pre-ship essentials.

1. **Credits:** MainMenuUI button → PopupWindow scrollbar mode. "Make10 by CJ Rhone / Wizard Bodega" + attributions.
2. **Leaderboard:** Top 10 per mode, PlayerPrefs JSON, PopupWindow display.
3. **Tutorial:** Add MakeZen explanation slide. Fix click-outside-to-dismiss bug in PopupWindow.

**Files touched:** MainMenuUI.cs, PopupWindow.cs, UIManager.cs

---

## Session P — L6: Shop System & Cosmetics (~3+ hours, largest session)

**Why:** Progression loop for replayability. Depends on art pipeline (~84 sprites).

1. **Data model:** `CosmeticData.cs` (ScriptableObject), `CosmeticInventory.cs` (singleton, PlayerPrefs JSON)
2. **Avatar architecture:** Layered UI Image stack replacing single-image AvatarManager
3. **Shop UI:** Category tabs, scrollable grid, locked/owned/equipped states, live preview
4. **BP persistence:** `RunManager.cs` rebuild with `TotalBP` / `SpendableBP` / `SpendBP()`
5. **Pricing:** Common 200–400, Uncommon 500–800, Rare 1000–1500

**Blocker:** Sprite art (~84 images). Can build the system and wire it up with placeholder sprites.

**Files touched:** New files (CosmeticData.cs, CosmeticInventory.cs, ShopManager.cs, ShopCard.cs), AvatarManager.cs, RunManager.cs (rebuilt), MainMenuUI.cs, SceneFlowManager.cs

---

## Recommended Execution Order

```
Track 1 — Stability (do first)
  F  Bug fixes & guards              30 min
  G  Dead code purge                 45 min
  H  Constants & code quality        30 min

Track 2 — The Feel (makes both modes better)
  I  L0: Easing overhaul             1.5 hr
  J  L1: Tile visuals                1 hr
  L  L5: Match VFX reorder           1 hr
  M  L4: Ambient life                45 min

Track 3 — Audio (parallel when stems ready)
  K  L3: FMOD + adaptive audio       2-3 hr

Track 4 — Visual Flair
  N  Shaders                         1.5 hr

Track 5 — Ship Features
  O  L7: Credits, leaderboard        1 hr
  P  L6: Shop & cosmetics            3+ hr (blocked on art)
```

**Total estimated time: ~13–15 hours across 11 sessions.**

Track 1 (F/G/H) is ~1.5 hours and should be done first — it prevents bugs from compounding and makes every subsequent session cleaner. Track 2 (I/J/L/M) is the core polish that transforms how the game *feels*. Tracks 3–5 can be interleaved based on asset readiness.
