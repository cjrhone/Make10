# Make10 — Code Hygiene Check-Up
**Date:** February 28, 2026 | **Auditor:** Claude (automated daily scan)

---

## Executive Summary

Scanned 36 C# scripts across the Make10 Unity project. The codebase is functional for Arcade mode and has solid Session A/A.1 MakeZen groundwork, but carries significant dead weight from the old roguelike/upgrade system. One critical bug exists (a method call to a nonexistent function), and the audio system has a known race condition. The project uses the **built-in render pipeline** with no custom shaders — there's an untapped opportunity for UI shaders to elevate the game's polish.

---

## 1. Potential Bugs

### CRITICAL — Will Crash at Runtime
**GameManager.cs → `StartNewGame()`** calls `gridManager.ResetGame()`, but `GridManager` has no `ResetGame()` method. This is a hard NullReferenceException. The intended call is likely `SpawnGrid()` or a combination of reset + respawn. If this code path is reachable (e.g., from CampaignManager's round progression), it will crash.

### HIGH — Audio Dropout (Known)
**AudioManager.cs** — All SFX share a single `sfxSource`. The time warning uses `sfxSource.Play()` with `loop = true`, which locks the source. When match SFX call `PlayOneShot()` on the same occupied source during the danger zone (<10s), sounds drop out. This is documented in CLAUDE.md and queued for the FMOD migration (L3).

### MEDIUM — Double Game-Over Trigger
**GameManager.cs → `OnFailedSwap()`** — In Zen mode, if the 3-second penalty brings `TimeRemaining` to exactly 0 or below, `TimeUp()` fires here. But `Update()` also calls `TimeUp()` when the timer reaches 0. If both execute in the same frame, game-over logic fires twice, potentially causing duplicate results screens or scoring anomalies.

**Fix:** Add a `gameEnded` guard flag, or check `isGameActive` before calling `TimeUp()` in `OnFailedSwap()`.

### MEDIUM — SafeArea Duplication
**SceneFlowManager.cs → `SetupSafeArea()`** creates a new `SafeAreaContainer` GameObject in `Awake()` with no deduplication check. On scene reload or if `Awake()` runs multiple times, duplicate containers stack up.

### LOW — Hint Particle Orphaning
**GridManager.cs → `SpawnHintParticles()`** — If `FreezeGrid()` is called while hint particles are mid-animation, their coroutines orphan. `ClearHintParticles()` destroys the GameObjects but doesn't explicitly stop running coroutines attached to them.

---

## 2. Dead Code & Unused Files

### Entire Vestigial Systems (Safe to Delete)
These are all remnants of the pre-arcade roguelike design. None are called by active game code:

| File | Lines | Why It's Dead |
|------|-------|---------------|
| `PlayerInventory.cs` | ~28 | Empty shell. `ClearInventory()` is a no-op. |
| `CampaignManager.cs` | ~80 | Round counter. `OnRoundChanged` event has zero subscribers. |
| `RunManager.cs` | ~60 | BP currency manager. `AddBP()`, `SpendBP()`, `AdvanceRound()` never called. |
| `Data/UpgradeData.cs` | ~50 | ScriptableObject for upgrades that don't exist in arcade. |
| `Data/SnackData.cs` | ~40 | ScriptableObject for consumables. Never referenced. |
| `Data/ArtifactData.cs` | ~40 | ScriptableObject for artifacts. Never referenced. |
| `Data/UpgradeType.cs` | ~20 | Enum for upgrade categories. Only used by dead systems. |
| `DataLoader.cs` | ~30 | Utility for loading ScriptableObjects. Only used by DebugUpgradePanel. |
| `DebugUpgradePanel.cs` | ~340 | F1-toggle debug panel. Should not ship. |
| `ShopCard.cs` | ~380 | Full shop UI card with animations. Comment says "purchasing disabled." |
| `UI/ExampleWindow.cs` | ~20 | Template/example PopupWindow. |
| `UI/UpgradeConfirmWindow.cs` | ~60 | Upgrade purchase confirmation popup. No callers. |

**Total dead weight: ~1,150 lines across 12 files.**

### Dead Methods in Active Files

**GameManager.cs:**
- `CalculateEnhancedNumberBonus()` — always returns 0
- `CalculateZeroTimeBonus()` — always returns 0f
- `ApplyPostScoringBonuses()` — returns input unchanged
- `CalculateCommonBonuses()` — returns (baseScore, 0)
- `GetCurrentWeights()` — never called (TileWeightManager owns this now)
- `GetCurrentGridSize()` — returns hardcoded 5, only one caller
- Events `OnEnhancedNumberBonus`, `OnTimeBonus` — declared, never fired (suppressed with `#pragma warning disable`)
- Field `postWinDelay` — serialized, never read

**GridManager.cs:**
- `tileFallDelay`, `postClearDelay` — serialized, never used (pragma-suppressed)

**Tile.cs:**
- `numberPulseSpeed`, `numberPulseMinScale`, `numberPulseMaxScale`, `numberBrightenAmount` — "Enhanced Number Pulse" settings, no code uses them
- `isEnhanced` — always false in Arcade. Will be repurposed for locked tiles in Session A, but currently dead

**MatchChecker.cs:**
- `targetSum = 10` — serialized but never read (code checks for "any multiple of 10")

---

## 3. Code Smells

### Magic Numbers
50+ hardcoded values across the codebase. The most impactful ones to extract as named constants:

- GameManager: `10f` (multiplier drain), `4f` (speed bonus window), `1.5f` (time bonus), `3f` (Zen penalty), `3` (max reshuffles)
- GridManager: `1600f` (tile fall speed), `10f` (hint delay), `3f` (hint repeat)
- Tile: `Color(0.85f, 0.85f, 0.85f)` (background grey) appears in multiple places

### Repeated Mode Checks
`CurrentMode == GameMode.Zen` appears 8+ times across files with no helper. Suggest adding `public bool IsZenMode => CurrentMode == GameMode.Zen;` to GameManager.

### GridManager Is a God Object (~1,920 lines)
Handles grid spawning, tile input (click/swipe/drag), match processing, cascade logic, hint system, and VFX triggering. Candidates for extraction: InputHandler, MatchProcessor, HintSystem.

### Inconsistent Null Checking
AudioManager carefully null-checks all sources. UIManager sometimes checks, sometimes assumes. No consistent pattern.

---

## 4. Shader & Visual Polish Suggestions

The project currently uses **zero custom shaders** (only TextMeshPro's default shaders). Since this is the built-in render pipeline, you can write simple unlit UI shaders. Here are high-impact opportunities you could prompt Claude to implement in a Cowork session:

### A. Locked Tile Glow Shader (MakeZen — High Impact)
**What:** A UI shader for locked tiles that creates a soft animated glow/pulse around the tile border, with color determined by the locked value tier (gold for 10, purple for 20, etc.).
**Why:** The CLAUDE.md notes "locked tile glow animations" as deferred from MVP. A shader-based glow is cheaper than particle-based and looks better.
**Prompt idea:** *"Write a Unity UI shader (built-in pipeline) that takes a base color, glow color, glow intensity, and pulse speed as properties. It should render a rounded rectangle with a soft outer glow that pulses. I'll apply it to locked tiles in MakeZen mode via a custom Material on the tile's Image component."*

### B. Beam Flash Shader (Match VFX — Medium Impact)
**What:** Replace the current multi-sprite beam effect with a single-quad shader that draws a gold-to-transparent gradient beam with UV scrolling.
**Why:** GridVFX already has `uvScrollSpeed` and `gradientPower` serialized fields but uses multiple Image sprites to fake the effect. A shader would be cleaner and more performant.
**Prompt idea:** *"Write a Unity UI shader for a horizontal/vertical beam flash effect. It should have a gold gradient from center to edges, UV scrolling for shimmer, and an alpha that fades in then out over time. Properties: beam color, edge color, scroll speed, fade progress (0-1)."*

### C. Hot Streak Background Shader (Atmosphere — Medium Impact)
**What:** An animated background shader that subtly shifts colors, adds heat distortion or wave effects during hot streak mode.
**Why:** HotStreakEffect currently uses fire particles. A background shader would add atmospheric depth without particle overhead.
**Prompt idea:** *"Write a Unity UI shader for a fullscreen background overlay. It should create a subtle animated heat shimmer/wave distortion effect with warm color tinting. Properties: intensity (0-1), distortion amount, color tint, animation speed. I'll control intensity from C# when hot streak activates."*

### D. Tile Selection Highlight Shader (Polish — Low-Medium Impact)
**What:** A shader-based selection ring/highlight that pulses around the selected tile, replacing the current scale-based feedback.
**Why:** L1 plans `PunchScale(1.15f)` for selection, but a shader ring looks more polished (see Holedown's selection feedback).
**Prompt idea:** *"Write a Unity UI shader that renders a rounded rectangle outline (not filled) with an animated dash/pulse pattern. Properties: outline color, outline width, dash speed, corner radius. For tile selection feedback."*

### E. Ambient Particle Shader (Performance — Low Impact)
**What:** Replace the current CPU-spawned ambient particles (8-20 individual GameObjects with Image components) with a single shader quad that renders multiple animated dots.
**Why:** GridVFX creates/destroys particle GameObjects in code. A shader could render all ambient particles in a single draw call.

---

## 5. Suggested Cowork Session Prompts

Here are ready-to-use prompts for future sessions:

### Dead Code Cleanup (30 min)
> "Delete the following unused files from my Make10 Unity project: PlayerInventory.cs, CampaignManager.cs, RunManager.cs, DataLoader.cs, DebugUpgradePanel.cs, ShopCard.cs, ExampleWindow.cs, UpgradeConfirmWindow.cs, and the entire Data/ folder (ArtifactData.cs, SnackData.cs, UpgradeData.cs, UpgradeType.cs). Then search all remaining .cs files for any references to these deleted classes and remove/comment them out. Finally, remove the dead methods in GameManager.cs: CalculateEnhancedNumberBonus, CalculateZeroTimeBonus, ApplyPostScoringBonuses, CalculateCommonBonuses, GetCurrentWeights, GetCurrentGridSize."

### Magic Number Extraction (20 min)
> "In my Make10 GameManager.cs, extract all magic numbers into named constants at the top of the class. Key ones: multiplier drain rate (10f), speed bonus threshold (4f), time bonus per match (1.5f), zen failed swap penalty (3f), zen max reshuffles (3), hot streak multiplier (5f), hot streak duration (10f). Do the same for GridManager.cs: tile fall speed (1600f), hint delay (10f), hint repeat interval (3f)."

### Double Game-Over Fix (10 min)
> "In GameManager.cs, the OnFailedSwap() method for Zen mode deducts 3 seconds and then checks if TimeRemaining <= 0 to call TimeUp(). But Update() also calls TimeUp() when the timer reaches 0, causing potential double-firing. Add a guard so TimeUp() can only execute once per round."

### Locked Tile Glow Shader (45 min)
> "Write a Unity UI shader (built-in render pipeline, unlit) for Make10's locked tiles. It should render a rounded rectangle with a soft animated outer glow. Properties: _BaseColor (tile background), _GlowColor (tier color — gold, purple, teal, etc.), _GlowIntensity (0-2), _PulseSpeed (0-3), _CornerRadius (0-0.5). The glow should pulse sinusoidally and extend ~4px beyond the tile bounds. Save it as Assets/Shaders/LockedTileGlow.shader."

---

## 6. Project Health Score

| Area | Score | Notes |
|------|-------|-------|
| Core Gameplay Logic | 8/10 | Solid. Match detection, scoring, grid mechanics all work. |
| Code Cleanliness | 5/10 | ~1,150 lines of dead code. 50+ magic numbers. God object. |
| Bug Risk | 6/10 | 1 critical crash bug, 1 known audio issue, 1 double-fire risk. |
| Visual Polish | 6/10 | Functional VFX but no custom shaders. All linear easing. |
| MakeZen Readiness | 7/10 | Sessions A & A.1 complete. Tile.cs and MatchChecker prepared. Session B (merge/gravity) is next. |
| Architecture | 6/10 | Singleton pattern works but creates tight coupling. GridManager needs decomposition. |

**Overall: 6.3/10** — Solid foundation, needs cleanup before the Launch Sprint to avoid compounding technical debt.
