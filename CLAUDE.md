# Claude Notes - Make10 Project

## Project Overview

Make10 is an arcade-style number puzzle game where players swap tiles to create rows/columns summing to exactly 10. Originally created for Brainless Game Jam 2026. Converted from roguelike to arcade style in February 2026.

**Creator:** CJ Rhone / Wizard Bodega

---

## Script Inventory

### Core Game Logic
| Script | Purpose |
|--------|---------|
| `GameManager.cs` | Game state, scoring, multiplier system, hot streak mode. Timer-only rounds (no win threshold). |
| `GridManager.cs` | Grid spawning, tile management, cascade matching, progressive tile weights, hint system |
| `Tile.cs` | Individual tile behavior, click/swipe input, selection state |
| `MatchChecker.cs` | Match detection, row/column sum validation, solvability checks |
| `CampaignManager.cs` | Lightweight round counter (arcade mode shell) |

### Scene Flow & UI
| Script | Purpose |
|--------|---------|
| `SceneFlowManager.cs` | Master scene controller, panel transitions (10 game states) |
| `RunManager.cs` | Persistent run state: BP currency, round progression |
| `UIManager.cs` | Score, timer, multiplier display, results screen, Balatro-style breakdown |
| `MainMenuUI.cs` | Main menu button handlers |
| `PopupWindow.cs` | Reusable popup system with scrollbar and auto-size modes |

### Audio & VFX
| Script | Purpose |
|--------|---------|
| `AudioManager.cs` | Centralized audio (music, SFX, voice), volume persistence |
| `TenExplosionVFX.cs` | Particle explosion on "10" matches, soft glow particles |
| `GridVFX.cs` | Line sweep beams, ambient particles, screen shake, land sparkles |
| `HotStreakEffect.cs` | Fire effects, flames, embers during hot streak mode |
| `AvatarManager.cs` | Character avatar states and animations |
| `LoadingBarVFX.cs` | Loading screen procedural effects |

### Utilities
| Script | Purpose |
|--------|---------|
| `AnimationUtilities.cs` | Static animation library (PunchScale, PopIn, CountUp, etc.) |
| `ParallexBackground.cs` | Parallax scrolling for backgrounds |
| `TutorialDemoWidget.cs` | Tutorial content helper |
| `GlowTextureGenerator.cs` | Procedural soft glow texture generation (circular & diamond) |
| `UIStyleGuide.cs` | Centralized UI styling constants and window sizes |
| `PlayerInventory.cs` | Minimal shell (singleton + ClearInventory). No upgrades in arcade mode. |

---

## Singleton Managers

```
GameManager.Instance        → Game state, scoring, multiplier, hot streak
SceneFlowManager.Instance   → Scene transitions, 10 game states
UIManager.Instance          → UI updates, results screen
AudioManager.Instance       → Audio playback, volume control
RunManager.Instance         → BP currency, round progression
CampaignManager.Instance    → Lightweight round counter
AvatarManager.Instance      → Avatar state machine
TenExplosionVFX.Instance    → Particle effects
GridVFX.Instance            → Line sweeps, ambient particles, screen shake
```

---

## Game Flow (Arcade Mode)

```
Loading → MainMenu → (Tutorial?) → Countdown → Game → Time's Up → Results → [Continue] → Countdown → Next Round ...
                                                                    Results → [Main Menu] → MainMenu
```

No "win" or "lose" — every round ends when the timer hits zero. The results screen shows BP earned with a Balatro-style breakdown, then players continue to the next round or return to the main menu.

### Game States (SceneFlowManager)
- **Loading**: Initialization with progress bar (min 1.5s)
- **MainMenu**: Play, Options, Quit buttons
- **Tutorial1/2**: 2-part onboarding
- **Countdown**: "3...2...1...GO!"
- **Game**: Active gameplay (timer-based rounds)
- **Results**: Score breakdown screen (Balatro-style count-up) with Play Again + Main Menu buttons
- **Options**: Settings overlay
- **Quit**: Exit confirmation

---

## Brain Points (BP) - Game Currency

### How BP is Earned

| Source | Formula | Description |
|--------|---------|-------------|
| Matches | 10 BP × multiplier | Each row/column summing to 10 |
| Hot Streak | ×5 multiplier | During hot streak mode |

### Results Screen Breakdown (Balatro-style)

```
┌─────────────────────────────────────┐
│         YOU ARE A GENIUS!           │
│                                     │
│   Score                    128 BP   │  ← Count-up animation
│   Hot Streak               x2.5     │  ← Instant
│   ──────────────────────────────    │
│   TOTAL                    398 BP   │  ← Count-up animation
│                                     │
│         [Continue]                  │  ← Restarts with countdown (next round)
└─────────────────────────────────────┘
```

---

## Scoring & Multiplier System

### Base Scoring
```
First solve:  10 pts (no multiplier)
Second solve: 10 pts + MULTIPLIER ACTIVATED (×1.25)
Each solve:   10 × multiplier + increment

Multiplier Growth:
- Start: ×1.25
- Increment: +0.25 per solve
- Max: ×3.00
- Beyond max: TRIGGERS HOT STREAK (×5.00)
```

### Hot Streak Mode
- **Trigger**: Multiplier exceeds ×3.00
- **Duration**: 10 seconds
- **Multiplier**: Fixed ×5.00
- **Effects**: Fire VFX, special music, avatar shake

---

## Grid System

### Configuration
- **Size**: 5×5 grid (hardcoded in arcade mode)
- **Tile Values**: 0-6 with weighted distribution
- **Matching**: Rows/columns summing to exactly 10
- **Cascade**: Tiles fall after match, new tiles spawn

### Tile Weights (Base Distribution)
Base weights defined in both `GameManager.cs` (GameSettings class) and `GridManager.cs` (fallback).
Only tiles 0-4 have base weight; tiles 5-7 start at 0 and are introduced by the solve-based ramp:
```
0: 0.12  Grey   (wildcard) — boosted for easy early 10s
1: 0.28  Gold   — boosted primary, easiest combos
2: 0.26  Blue   — dominant (pairs well with 3s)
3: 0.22  Green  — strong mid-range
4: 0.08  Coral  — further reduced, less clutter
5: 0.00  Orange — introduced after 2 solves (ramps to 0.10)
6: 0.00  Purple — introduced after 5 solves (ramps to 0.06)
7: 0.00  Teal   — introduced after 8 solves (ramps to 0.02)
```

### Progressive Difficulty Ramp (Solve-Based)
High tiles (5, 6, 7) are introduced based on **player performance** (number of matches cleared), not elapsed time.
This means struggling players keep getting easy boards, while skilled players face increasing challenge:
- **5s appear**: After 2 solves (start at weight 0.02, ramp to 0.10)
- **6s appear**: After 5 solves (start at weight 0.01, ramp to 0.06)
- **7s appear**: After 8 solves (start at weight 0.005, ramp to 0.02)
- **Full ramp**: At 12 solves, all high tiles are at max weight
- **Low tile reduction**: Tiles 0-4 gently reduce to 85% as high tiles ramp in
- **Method**: `GetWeightedRandomValue()` in `GridManager.cs` reads `GameManager.Instance.SolveCount`
- Configurable via `solvesFor5s`, `solvesFor6s`, `solvesFor7s`, `maxWeight5/6/7`, `solvesToFullRamp`, `baseTileReduction`
- `SolveCount` resets each round via `GameManager.ProcessSingleSolve()`

### Tile Colors
```
0: Grey   (wildcard)
1: Gold   (primary)
2: Blue   (primary)
3: Green  (1+2)
4: Red    (primary)
5: Orange (1+4)
6: Purple (2+4)
```

### Hint System
- Activates after 10 seconds of inactivity
- Shows particle trail indicating valid move
- Repeats every 3 seconds

---

## Key Constants

| Setting | Default | Location |
|---------|---------|----------|
| Game Duration | 60s | GameManager |
| Multiplier Start | ×1.25 | GameManager |
| Multiplier Max | ×3.00 | GameManager |
| Multiplier Increment | +0.25 | GameManager |
| Hot Streak Multiplier | ×5.00 | GameManager |
| Hot Streak Duration | 10s | GameManager |
| Grid Size | 5×5 | GridManager |
| Canvas Resolution | 1080×1920 | Canvas |
| 5s Appear After | 2 solves | GridManager |
| 6s Appear After | 5 solves | GridManager |
| 7s Appear After | 8 solves | GridManager |
| Max Weight 5s | 0.10 | GridManager |
| Max Weight 6s | 0.06 | GridManager |
| Max Weight 7s | 0.02 | GridManager |
| Full Ramp At | 12 solves | GridManager |
| Base Tile Reduction | 0.85 | GridManager |

---

## Event System

### GameManager Events
```csharp
OnScoreChanged(int newScore, int delta)
OnTimeChanged(float timeRemaining)
OnMultiplierChanged(bool active, float mult, float timer)
OnHotStreakStarted()
OnHotStreakEnded()
OnGameWon()     // Fires on every round end (time's up)
```

### RunManager Events
```csharp
OnBPChanged(int currentBP)
OnRoundChanged(int roundNumber)
OnRunStarted()
OnRunEnded()
```

---

## Audio System

### Audio Sources (3 independent)
1. **musicSource**: Background music (loops)
2. **sfxSource**: Sound effects
3. **voiceSource**: Voice/UI feedback

### Volume Persistence
- Saved in PlayerPrefs: "MusicVolume", "SFXVolume", "VoiceVolume"
- Auto-reset if accidentally muted (≤0.01)

---

## Animation Utilities

Available in `AnimationUtilities.cs`:
- **PunchScale**: Scale 1→peak→1 (feedback)
- **PopIn**: Scale 0→overshoot→1 (overlays)
- **ScaleIn/Out**: Fade via scale
- **FadeCanvasGroup**: Alpha fade
- **FloatAndFade**: Upward motion + fade (popups)
- **PulseLoop**: Continuous pulse
- **CountUp**: Number counter (Balatro-style)
- **DropIn**: Drop with elastic bounce

---

## Polish Sprint (February 2026)

### P0 — Shop Removal & Back Button ✓ COMPLETED

**What was done:**
- Deleted `ShopManager.cs` (363 lines) and its `.meta` file
- Removed from `SceneFlowManager.cs`: `shopPanel` field, `GameState.Shop` enum value, `TransitionToShop()`, `TransitionToShopSequence()`, `TransitionFromShopToGame()`, `TransitionFromShopToGameSequence()`, `ReturnToMainMenuFromShop()`, shop case in `GoBack()`, all `shopPanel` references in `InitializePanels()`
- Activated `ReturnMenuButton` on WinScreen in scene (was `m_IsActive: 0`), repositioned side-by-side with PlayAgainButton, updated text from "Menu" to "Main Menu", bumped font size from 24→36
- Added `EnsureResultsButtonsActive()` safety method in `UIManager.cs` — called when winScreen shows, ensures both buttons are active even if scene state is wrong
- Cleaned all shop references from CLAUDE.md (Script Inventory, Singletons, Game Flow, Game States)

**Current results screen buttons (WinScreen):**
- **PlayAgainButton** (left, x=-100) → `OnContinueButtonClicked()` → `RestartWithCountdown()`
- **ReturnMenuButton** (right, x=150) → `OnMainMenuButtonClicked()` → `GoBack()` → MainMenu

**Note:** `ShopCard.cs` and `DataLoader.cs` still contain shop references in comments only — no compile impact. The `shopPanel` GameObject in the Unity scene hierarchy should still be manually deleted in the editor (we can't remove scene GameObjects from outside Unity), but it won't be referenced by any code.

---

### P1 — Star Rating System & Scoring Overhaul

**Star Rating — ✓ IMPLEMENTED**

1-3 star rating displayed on the results screen after the total BP count-up. Stars are created procedurally using TMP ★ characters (no sprite assets needed).

**Implementation:**
- **Thresholds:** `GameManager.cs` — `star1Threshold` (300 BP), `star2Threshold` (600 BP), `star3Threshold` (1000 BP). Serialized fields, tunable in Inspector
- **Calculation:** `GameManager.GetStarRating(int totalBP)` returns 0-3 stars
- **Threshold accessors:** `GameManager.Star1Threshold`, `Star2Threshold`, `Star3Threshold` (read-only)
- **Display:** `UIManager.ShowStarRating(int starsEarned)` — creates 3 star objects in a HorizontalLayoutGroup inside BreakdownContainer. Each star pops in one at a time using `AnimationUtilities.PopIn()`. Earned stars are gold, unearned are dim grey
- **Wired in:** `UIManager.ShowWinScreenBreakdown()` — stars appear after TOTAL count-up, before high score banner
- **Cleanup:** Stars destroyed in `HideBreakdownElements()` when results screen resets
- **Styling:** `starFilledColor` (gold), `starEmptyColor` (dim grey), `starSize` (64), `starRevealDelay` (0.25s) — all serialized

**Star thresholds:**
```
★       = 300+ BP (steady matching)
★★      = 600+ BP (requires consistent multiplier usage)
★★★     = 1000+ BP (requires hot streak mastery)
```

**Scoring Improvements**

Current formula (`GameManager.ProcessSingleSolve()`, lines ~368-422):
```
Solve #1: baseMatchScore (10) + enhanced bonus (always 0 in arcade)
Solve #2: same + activates multiplier at ×1.25
Solve #3+: (10 × multiplier) + floor(multiplierTimer) + enhanced bonus
Hot Streak: ProcessHotStreakSolve() — fixed ×5.00 multiplier
```

Issues to review:
- `floor(multiplierTimer)` as a time bonus is opaque to players — they don't see it
- The jump from solve #2 to #3 introduces multiplier + time bonus simultaneously
- `enhancedBonus` is always 0 in arcade mode (`CalculateEnhancedNumberBonus()` returns 0, line ~427)
- Results breakdown (UIManager lines ~793-861) shows "Score" + "Session Time" + "TOTAL" but Session Time bonus is just `Mathf.RoundToInt(sessionDuration)` — 1 BP per second of game time, which will always be ~60 for a full round

**Leaderboard — Local**

Existing infrastructure:
- `PlayerPrefs` keys: `Make10_HighScore`, `Make10_HighScoreBP`, `Make10_TotalGames` (GameManager lines ~78-80)
- `HighScore` and `HighScoreBP` properties already exposed (line ~91-92)
- `IsNewHighScore` flag set in `TimeUp()` (line ~601)
- Main menu displays best score via `MainMenuUI.UpdateHighScoreDisplay()` (line ~64)
- Results screen shows "NEW HIGH SCORE" banner via `UIManager.ShowNewHighScoreBanner()` (line ~866)

To build a proper leaderboard:
- Store top N scores in PlayerPrefs (JSON array or indexed keys like `Make10_Score_1` through `Make10_Score_10`)
- New UI panel accessible from MainMenu (add button in `MainMenuUI.cs`)
- Could reuse `PopupWindow.cs` for the leaderboard display (scrollbar mode)
- Each entry: rank, score, star rating, date

---

### P2 — Match Animation & VFX Polish

**Current match sequence** (`GridManager.AnimateSolveSequence()`, line ~1200):
```
1. GridVFX.PlayLineSweeps() — beam flash fires (non-blocking coroutine)
2. 0.08s pause
3. AvatarManager.OnSolve() — avatar animation
4. AudioManager.PlayConvergenceSound()
5. Tiles converge toward center (solveConvergeDuration)
6. ShowTenEffectSpectacular() per match line — "10" text + TenExplosionVFX.TriggerExplosion()
   (these fire simultaneously: "10" popup appears at same time as particle explosion)
7. ClearMatchedTiles()
8. GameManager.OnMatchCleared() — scoring
```

**Requested changes:**
- **Reduce beam opacity:** `GridVFX.cs` → `FlashBeam()` (line ~205). The beam is a 4-layer system: glow → core → hot core → sparkles. Adjust alpha values in the BURST/HOLD/FADE phases. Key colors defined at lines ~24-28 (`goldEdgeColor`, etc.)
- **Sequence beam AFTER "10" popup:** Currently beam fires first (step 1), then "10" appears later (step 6). Reverse this: show "10" popup first with a PunchScale, brief hold, THEN fire the beam sweep. This means restructuring `AnimateSolveSequence()` — move `PlayLineSweeps()` call to after `ShowTenEffectSpectacular()`
- **Make "10" punchier:** In `GridManager.ShowTenEffectSpectacular()` (line ~1368), the "10" text uses `AnimationUtilities.PopIn()`. Could increase overshoot, add screen shake on pop, or add a brief hold before fade

**Haptic feedback — New Feature:**
- No haptics exist yet (zero `Vibrate` or `Haptic` references in codebase)
- Add `Handheld.Vibrate()` call in `GridManager.ShowTenEffectSpectacular()` right when the "10" text appears
- For finer control on iOS: use `UnityEngine.iOS.Device.RequestStoreReview` or a haptics plugin
- For Android: `Handheld.Vibrate()` works but is coarse (single buzz). Consider Unity's Input System haptics for gamepad or a native plugin for precise haptic patterns
- Best insertion point: `GridManager.ShowTenEffectSpectacular()` line ~1378, right before/after `TenExplosionVFX.Instance?.TriggerExplosion()`

**Key VFX files for reference:**
- `GridVFX.cs` — Beam flash (line ~205 `FlashBeam()`), screen shake (look for `ShakeScreen`), tile sparkles, ambient particles
- `TenExplosionVFX.cs` — Particle explosion + collection to score slider. Timing: explosion 0.35s → pause 0.1s → collection 0.5s per particle. Colors: small=gold, big=purple
- `HotStreakEffect.cs` — Fire/ember effects during hot streak
- Particle sprites in `Assets/particles/` (30 files)

---

### P3 — Visual Polish

**Tile Number Drop Shadows**

A shadow system already exists in `Tile.cs` but it's built for "enhanced" numbers only:
- `CreateShadowText()` (line ~152) creates a `TextMeshProUGUI` behind the main number
- `shadowOffset` = `(3, -3)`, `shadowColor` = black at 50% alpha (line ~55-57)
- `shadowSoftness` = 0.5 (dilation for soft edge)
- The shadow text is created in `Awake()` (line ~143) but its visibility is tied to the `isEnhanced` flag

**What to change:**
- Make shadow always visible (not gated by enhanced mode)
- Change `shadowColor` to derive from the tile's `NumberColors[Value]` array (line ~99-111) — use a darkened version: `Color.Lerp(NumberColors[value], Color.black, 0.6f)` or similar
- Update `SetValue()` (line ~263) to set shadow color when the tile value changes
- The `NumberColors` array for reference:
  ```
  0: (0.6, 0.6, 0.6)     Grey
  1: (0.85, 0.65, 0.1)    Gold     → shadow: dark gold
  2: (0.15, 0.4, 0.9)     Blue     → shadow: dark blue
  3: (0.2, 0.7, 0.3)      Green    → shadow: dark green
  4: (0.9, 0.2, 0.2)      Red      → shadow: dark red
  5: (0.95, 0.5, 0.1)     Orange   → shadow: dark orange
  6: (0.6, 0.2, 0.75)     Purple   → shadow: dark purple
  ```

**Tile Prefab Structure:**
- `Assets/Prefabs/Tile.prefab` — the base tile prefab
- Runtime hierarchy per tile: Background Image → NumberShadow (TMP) → NumberText (TMP) → SelectionHighlight → EnhancedGlow (Image)
- Background is uniform light grey: `(0.85, 0.85, 0.85)` (Tile.cs line ~93)
- Number text uses `numberScale` multiplier (default 1.0, configurable 0.5-2.0, line ~51)
- Font: whatever TMP font is on the prefab (check prefab for font asset reference)

**Credits/About Section**

No credits UI currently exists in the scripts. The sprint note says "current formatting is broken, needs cleanup" — this likely refers to content in the Unity scene or a popup that's wired but not in the C# scripts. Check:
- `PopupWindow.cs` — reusable popup system, could be used for credits
- `MainMenuUI.cs` — look for any credits/about button handlers
- The Unity scene hierarchy for any Credits or About GameObjects

**To implement credits:**
- Add a credits button to `MainMenuUI.cs`
- Use `PopupWindow.cs` in scrollbar mode for the content
- Content: "Make10 by CJ Rhone / Wizard Bodega, Brainless Game Jam 2026" etc.

---

### P4 — Future (Deferred)

- **New avatar animations:** `AvatarManager.cs` handles state machine. Add new states/triggers
- **Android back button:** Requires Unity Input System integration. `GameManager.cs` already imports `UnityEngine.InputSystem` (line 2). Deferred due to complexity
- **Tutorial popup polish:** `TutorialDemoWidget.cs` exists. Known issue: click-outside-to-dismiss fires prematurely. Related to `PopupWindow.cs` click handling
