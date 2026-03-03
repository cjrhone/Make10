# Make10 — Code Health Check-Up
**Date:** March 2, 2026 | **Post-Session:** MakeZen MVP Complete (Sessions A–E), Bug Fixes (F), Dead Code Purge (G)

---

## Project Snapshot

**Active scripts:** 25 C# files (19 gameplay/UI + 4 UI utilities + 1 editor + 1 shader)
**Custom shaders:** 1 (`UIAdditive.shader` — actively used by GridVFX for beam/glow effects)
**Total estimated LOC:** ~8,500+ across gameplay scripts
**Sessions completed:** A through G | **Remaining:** H through P

---

## Potential Bugs Found

### High Priority

**1. GameManager — StartNewGame() always uses Arcade duration (Line ~353)**
`ResetRoundState(gameDuration)` doesn't check `CurrentMode`. If MakeZen is started via this path, the timer would be 60s instead of 300s. Currently mitigated because Zen uses a separate entry point, but this is fragile — any future refactor that routes through `StartNewGame()` would break Zen timing.

*Safe fix:* `float duration = IsZenMode ? zenGameDuration : gameDuration;`

**2. GameManager — Streak timeout fires in Zen mode (Lines ~262–271)**
The `timeSinceLastSolve >= streakTimeout` block resets `solveCount` to 0 regardless of mode. Per design, Zen multiplier should only reset on failed swaps, never on timeout. This could silently degrade Zen multiplier behavior.

*Safe fix:* Gate the timeout block with `&& CurrentMode == GameMode.Arcade`.

**3. GridManager — DebugCheckSums() null reference (Lines ~2388–2401)**
Both debug methods access `grid[x, y].Value` without null checks. If called during grid transitions (reshuffle, cascade), this will crash.

*Safe fix:* Add `if (grid[x, y] != null)` before accessing `.Value`.

**4. UIManager — Missing coroutine cleanup in OnDestroy (Line ~215)**
Five tracked coroutines (`timerPulseCoroutine`, `multiplierPulseCoroutine`, `multiplierGlowCoroutine`, `hotStreakTextPulseCoroutine`, `progressBarBounceCoroutine`) are never stopped when UIManager is destroyed. If destroyed mid-game, these will throw NullReferenceExceptions on next yield.

*Safe fix:* Add `StopAllCoroutines();` at the top of `OnDestroy()`.

**5. AudioManager — Event listener leak (Line ~128)**
Slider `onValueChanged.AddListener()` calls are made without corresponding `RemoveListener()` in `OnDisable`/`OnDestroy`. Listeners accumulate across scene reloads.

*Safe fix:* Add cleanup in `OnDestroy()`.

### Medium Priority

**6. Tile.cs — `wasFloating` state leak (Line ~81)**
Set to `true` in `Select()` but only conditionally reset in `Deselect()`. If selection is cleared via `ForceResetVisuals()` instead of `Deselect()`, the flag persists, potentially affecting subsequent selection animations.

**7. Tile.cs — Redundant `isEnhanced` field (Line ~82)**
`isEnhanced` is always set to `IsLocked` (line ~342). This duplicate state could diverge during edge cases. Replacing all `isEnhanced` checks with direct `IsLocked` checks would be cleaner and safer.

**8. GridVFX.cs — Missing RectTransform null check in FlashBeam sparkles (Lines ~320–336)**
`sImg.GetComponent<RectTransform>()` result isn't null-checked before use.

**9. PopupWindow.cs — Duplicated EaseOutBack implementation (Lines ~753–758)**
Local copy of `EaseOutBack()` exists separately from `AnimationUtilities.EaseOutBack()`. These can drift apart during Session I easing overhaul.

**10. HotStreakEffect.cs — `hasOriginalPosition` never reset in Deactivate()**
If Activate/Deactivate cycles happen multiple times, the panel position reference goes stale.

---

## Dead / Unused Code

| Item | Location | Notes |
|------|----------|-------|
| `CalculateZeroTimeBonus()` | GameManager ~line 569 | Declared, never called. Safe to remove. |
| `tileFallSpeed = 1600f` | GridManager line 36 | Serialized but never read in code. Superseded by time-based fall. |
| `CanSum()` recursive method | MatchChecker ~lines 358–369 | Never called. Leftover from earlier design. |
| `titleRotateAmount` field | MainMenuUI lines 15–17 | Pragma-suppressed, rotation removed in L0 plan. |
| `GetCurrentSlidePanel()` | SceneFlowManager ~line 296 | Defined but never called anywhere. |
| Commented-out demo logic | TutorialDemoWidget lines 55–77 | ~20 lines of dead comments. |
| Double `PlayerPrefs.Save()` | GameManager lines ~754, ~777 | Second call is redundant (harmless but wasteful). |

**All of the above are safe to remove** — none have downstream dependencies. Removing them would cut ~60 lines and reduce confusion.

---

## Architecture Observations

**UIManager.cs is over 2,100 lines** and handles score display, multiplier updates, hot streak sequences, results breakdown, pause menu, and progress bar VFX. This is the single biggest maintenance risk in the codebase. Splitting into focused managers (ScoreUI, MultiplierUI, ResultsScreen, PauseMenu) would make each Session's changes much safer. *Not urgent, but worth planning for post-launch.*

**FindFirstObjectByType usage** appears in GameManager (~3 calls) and SceneFlowManager (~5 calls) instead of cached singleton references. These are slow lookups that could be replaced with `GridManager.Instance` etc. Low risk but easy wins.

---

## Shader & Polish Opportunities

### Current State
The project has exactly **one custom shader** (`UIAdditive.shader`), which handles additive beam/glow blending with gold gradient, UV scrolling, and edge feathering. It's well-written and actively used by GridVFX for match line sweeps.

### Session N Shader Suggestions (from CLAUDE.md plan)

These three shaders would deliver the highest visual impact with low risk:

**1. Locked Tile Glow Shader**
An animated outer-glow effect for locked tiles in MakeZen. Properties: `_GlowColor` (tier-based: gold/purple/teal/red), `_GlowIntensity`, `_PulseSpeed`, `_CornerRadius`. Sinusoidal pulse creates a "breathing" glow. This replaces the current static color tinting on locked tiles with something that reads as alive and valuable.

*Cowork prompt suggestion:* "Write a Unity UI shader for Make10's locked tiles that creates an animated outer glow. The glow color should be configurable per-tile (gold for 10, purple for 20, teal for 30, red for 40). Include a sinusoidal pulse on intensity. Must work with the built-in render pipeline and Unity's UI canvas system (stencil/clipping support). Reference the existing UIAdditive.shader for canvas compatibility patterns."

**2. Beam Flash Shader**
A single-quad gold gradient beam to replace the current multi-sprite beam approach in GridVFX. UV scrolling along the beam length creates flowing energy. This would simplify the beam rendering code and look more polished.

*Cowork prompt suggestion:* "Write a Unity UI shader for a match-clear beam effect. Single quad with a gold-to-white gradient across its width, UV scrolling along the length for a flowing energy look. Include edge feathering at both ends via smoothstep. Must support additive blending and Unity UI canvas clipping. Keep it unlit, built-in pipeline."

**3. Hot Streak Heat Shimmer**
A fullscreen overlay shader with subtle wave distortion during hot streak mode. Properties: `_Intensity` (0–1 for fade in/out), `_DistortionAmount`, `_ColorTint` (warm orange). This would make hot streak mode feel dramatically different.

*Cowork prompt suggestion:* "Write a Unity UI shader for a fullscreen heat shimmer/distortion overlay. Properties: _Intensity (0-1), _DistortionAmount (pixel offset), _ColorTint (warm color overlay). Use sine-based UV distortion for a heat wave effect. Must work as a UI Image material on a fullscreen RectTransform. Built-in pipeline, transparent queue."

### Other Polish Tasks Well-Suited for Cowork Sessions

**Session H — Constants extraction** is ideal for Cowork: "Read GameManager.cs and extract all magic numbers into named constants. Replace inline values like `0.25f` multiplier increment, `3.0f` max multiplier, `5.0f` hot streak multiplier, `10f` hot streak duration, `4f` speed bonus threshold, etc. with clearly named `private const` declarations at the top of the class. Also add `public bool IsZenMode => CurrentMode == GameMode.Zen;` and replace all inline mode checks."

**Session I — Easing overhaul** is perfect for Cowork: "Add five easing functions to AnimationUtilities.cs: EaseOutCubic, EaseInOutCubic, EaseOutBack, EaseOutElastic, EaseInCubic. Then update PunchScale, PopIn, FloatAndFade, ScaleIn, ScaleOut, and DropIn to use the appropriate easing instead of linear Lerp. Update tile fall in GridManager to use time-based (0.25s) EaseOutCubic with a landing bounce. Update tile swap to EaseInOutCubic with a shallow arc."

**Session J — Tile visuals** is straightforward: "In Tile.cs, remove the `isEnhanced` gate on shadow visibility so all tiles get shadows (not just locked tiles). Add tinted backgrounds by lerping tile background color toward the number color at 12% strength. Add selection punch animation (1.15x scale, 0.1s) on first tile tap."

---

## Safety Checklist

Before implementing any of the above:

- [ ] **Never delete files that are referenced by other scripts** — verify with a project-wide grep first
- [ ] **Test mode-specific changes in both Arcade and Zen** — the two modes share most code paths
- [ ] **Back up PlayerPrefs before modifying RunManager** — high scores and BP are stored there
- [ ] **Shader changes should be additive** — create new shaders rather than modifying UIAdditive.shader
- [ ] **Coroutine cleanup changes should use StopAllCoroutines() cautiously** — ensure no coroutine is expected to survive scene transitions
- [ ] **Constants extraction is safe** — renaming values to constants doesn't change behavior, just readability

---

*Generated by Cowork scheduled task — Make10 Code Health Check-Up*
