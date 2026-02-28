# Claude Notes - Make10 Project

## Project Overview

Make10 is a number puzzle game where players swap tiles to create rows/columns summing to multiples of 10. Two modes: **Arcade** (60-second sprint) and **MakeZen** (5-minute focused session with locked tiles and board pressure). Originally created for Brainless Game Jam 2026. Converted from roguelike to arcade style in February 2026. MakeZen mode designed February 2026, prototyped in React (`MakeZen/make10zen_v6.jsx`), pending Unity port.

**Creator:** CJ Rhone / Wizard Bodega

### Design Philosophy — MakeZen
MakeZen is a 5-minute math meditation. Where Arcade is a sprint (frantic, adrenaline), MakeZen gives players room to breathe and think. Matched tiles converge into immovable "locked" tiles that accumulate on the board, creating rising pressure until the grid fills or time runs out. Research from Tohoku University (Kawashima/Nouchi) shows 15-20 minutes of daily simple arithmetic practice improves executive function, focused attention, and processing speed. Three 5-minute MakeZen sessions = the research-backed optimal dose.

**Reference prototype:** `MakeZen/make10zen_v6.jsx` — complete React implementation with locked tiles, 7-tier difficulty ramp, merge positioning, reshuffle system, and "stillness" game-over screen.

---

## Script Inventory

### Core Game Logic
| Script | Purpose |
|--------|---------|
| `GameManager.cs` | Game state, scoring, multiplier system, hot streak mode. Timer-only rounds (no win threshold). |
| `GridManager.cs` | Grid spawning, tile management, cascade matching, hint system |
| `TileWeightManager.cs` | Tile value weights, progressive difficulty ramp, Tetris-style tile bag |
| `GridValidation.cs` | Initial match prevention, anti-cascade checks, consecutive match tracking/scaling |
| `Tile.cs` | Individual tile behavior, click/swipe input, selection state |
| `MatchChecker.cs` | Match detection, row/column sum validation, solvability checks |
| `CampaignManager.cs` | Lightweight round counter (arcade mode shell) |

### Scene Flow & UI
| Script | Purpose |
|--------|---------|
| `SceneFlowManager.cs` | Master scene controller, panel transitions (9 game states) |
| `RunManager.cs` | Persistent run state: BP currency, round progression |
| `UIManager.cs` | Score, timer, multiplier display, results screen, Balatro-style breakdown |
| `MainMenuUI.cs` | Main menu button handlers |
| `PopupWindow.cs` | Reusable popup system with scrollbar and auto-size modes |

### Audio & VFX
| Script | Purpose |
|--------|---------|
| `AudioManager.cs` | Centralized audio (music, SFX, voice), volume persistence. 3 AudioSources. |
| `TenExplosionVFX.cs` | Particle explosion on "10" matches, soft glow particles |
| `GridVFX.cs` | Line sweep beams, ambient particles, screen shake, land sparkles |
| `HotStreakEffect.cs` | Fire effects, flames, embers during hot streak mode |
| `AvatarManager.cs` | Character avatar states and animations (single Image, 6 static PNGs) |
| `LoadingBarVFX.cs` | Loading screen procedural effects |

### Utilities
| Script | Purpose |
|--------|---------|
| `AnimationUtilities.cs` | Static animation library (PunchScale, PopIn, CountUp, etc.) — all linear Lerp except CountUp |
| `GlowTextureGenerator.cs` | Procedural soft glow texture generation (circular & diamond) |
| `UIStyleGuide.cs` | Centralized UI styling constants and window sizes |
| `PlayerInventory.cs` | Minimal shell (singleton + ClearInventory). No upgrades in arcade mode. |

---

## Singleton Managers

```
GameManager.Instance        → Game state, scoring, multiplier, hot streak
SceneFlowManager.Instance   → Scene transitions, 9 game states
UIManager.Instance          → UI updates, results screen
AudioManager.Instance       → Audio playback, volume control
RunManager.Instance         → BP currency, round progression
CampaignManager.Instance    → Lightweight round counter
AvatarManager.Instance      → Avatar state machine
TenExplosionVFX.Instance    → Particle effects
GridVFX.Instance            → Line sweeps, ambient particles, screen shake
TileWeightManager.Instance  → Tile value weights, tile bag, progressive difficulty ramp
GridValidation.Instance     → Match prevention, anti-cascade, consecutive match tracking
```

---

## Game Flow

### Arcade Mode (Current)
```
Loading → MainMenu → ModeSelect → Arcade → Countdown → Game (60s) → Time's Up → Results → [Continue] → Next Round ...
                                                                                  Results → [Main Menu] → MainMenu
```

### MakeZen Mode (In Development)
```
Loading → MainMenu → ModeSelect → MakeZen → Game (300s, locked tiles) → Stillness → Results → [Again] → MakeZen
                                                                                     Results → [Main Menu] → MainMenu
```

### Game States (SceneFlowManager)
```
Loading, MainMenu, ModeSelect, Tutorial1, Tutorial2, Countdown, Game, Results, Options, Quit
```

---

## Scoring & Multiplier System

### Base Scoring
```
Lines matching a multiple of 10 score their sum as base BP:
  10-sum → 10 BP, 20-sum → 20 BP, 30-sum → 30 BP

PLAYER SWAP MATCHES (cascadeCount == 1):
  Solve #1: lineSum pts (no multiplier)
  Solve #2: lineSum pts + MULTIPLIER ACTIVATED (×1.25)
  Solve #3+: lineSum × multiplier + speedBonus
  Also: adds time bonus (timeBonusPerMatch per line)

CASCADE MATCHES (cascadeCount >= 2):
  Flat lineSum BP only — no multiplier, no time bonus, no speed bonus.
  Does NOT increment SolveCount or trigger hot streak.
```

### Multiplier & Hot Streak
```
Multiplier: ×1.25 start → +0.25 per solve → ×3.00 max → triggers Hot Streak (×5.00, 10s)
Speed Bonus: +5 BP if solved within 4s of last player solve
```

### Star Rating (✓ Implemented)
```
Arcade:    ★ = 300+ BP    ★★ = 600+ BP    ★★★ = 1000+ BP
MakeZen:   ★ = 500+ BP    ★★ = 1000+ BP   ★★★ = 2000+ BP
```

### MakeZen Scoring Differences
```
Same base scoring as Arcade (10/20/30/40 BP per line sum).
Same multiplier mechanics EXCEPT:
  - Multiplier does NOT drain over time (no timer-based decay)
  - Multiplier resets to ×1 on FAILED SWAP (punishes guessing)
  - Failed swap also deducts 3 seconds from timer
  - No time bonus per match (timer is fixed 300s minus penalties)
  - Hot streak still triggers at ×3.00 threshold
Locked tiles with high sums (20, 30, 40) score more base BP per match,
rewarding players who build up bigger locked tiles.
```

---

## Grid System

- **Size**: 5×5, tile values 0-7 with weighted distribution
- **Matching**: Rows/columns summing to any multiple of 10
- **Tile bag**: Tetris-style bag of 25 tiles, Fisher-Yates shuffled
- **Anti-cascade**: Single re-roll if new tile would complete a match
- **Hint system**: 10s inactivity → particle trail, repeats every 3s

### Progressive Difficulty (Solve-Based)
```
5s appear after 2 solves (ramps to weight 0.10)
6s appear after 5 solves (ramps to weight 0.06)
7s appear after 8 solves (ramps to weight 0.02)
Full ramp at 12 solves. Low tiles reduce to 85%.
```

### Tile Colors
```
0: Grey (0.6, 0.6, 0.6)          5: Orange (0.95, 0.5, 0.1)
1: Gold (0.85, 0.65, 0.1)        6: Purple (0.6, 0.2, 0.75)
2: Blue (0.15, 0.4, 0.9)         7: Teal (0.1, 0.7, 0.7)
3: Green (0.2, 0.7, 0.3)         8: Pink (0.9, 0.35, 0.6)
4: Coral (0.85, 0.45, 0.35)      9: Crimson (0.75, 0.1, 0.15)
Background: uniform (0.85, 0.85, 0.85)
```

### Locked Tile Colors (MakeZen — from prototype)
```
10: Gold glow    (#f0d060, border #806828)
20: Purple glow  (#d090ff, border #6a3890)
30: Teal glow    (#50e8d0, border #208870)
40: Red glow     (#ff6060, border #903030)
50: Orange glow  (#ffa030, border #906020)
60: Blue glow    (#60a0ff, border #305090)
70+: Magenta glow (#e060e0, border #803080)
```

### MakeZen Grid Behavior
```
Locked tiles (value ≥ 10):
  - Can't be selected or swapped by player
  - Fall with gravity like normal tiles
  - Participate in row/col sum calculations (a locked 10 + four 0s = another 10!)
  - Only created via match convergence, never spawned randomly
  - Reshuffles skip locked tiles (preserve position and value)
  - Board pressure: as locked tiles accumulate, fewer free cells remain
```

---

## Key Constants

| Setting | Arcade | MakeZen | Location |
|---------|--------|---------|----------|
| Game Duration | 60s | 300s (5 min) | GameManager |
| Multiplier Start/Max | ×1.25 / ×3.00 | ×1.25 / ×3.00 | GameManager |
| Multiplier Drain | Timer-based (10s) | No drain (reset on fail) | GameManager |
| Failed Swap Penalty | None | -3s + multiplier reset | GameManager |
| Hot Streak | ×5.00, 10s | ×5.00, 10s | GameManager |
| Grid Size | 5×5 | 5×5 | GridManager |
| Max Reshuffles | Unlimited | 3 | GameManager |
| Canvas | 1080×1920 | 1080×1920 | Canvas |
| Tile Bag Size | 25 | 25 | TileWeightManager |
| Speed Bonus | 5 BP within 4s | 5 BP within 4s | GameManager |
| Time Bonus/Match | +1.5s per line | None | GameManager |

---

## Known Issues

### Audio Glitching (AudioManager.cs)
All SFX share a single `sfxSource`. The time warning uses `sfxSource.Play()` with `loop = true`, which occupies the source. When match SFX call `PlayOneShot()` on the same source during the danger zone (<10s), sounds drop out or glitch. Root cause: `Play()` and `PlayOneShot()` conflict on the same AudioSource. This will be resolved by the FMOD migration in L3.

### Scoring Opacity
`floor(multiplierTimer)` as a hidden time bonus is opaque to players. `enhancedBonus` is always 0 in arcade mode. Session Time bonus on results screen is always ~60 (1 BP per second × 60s round). These should be reviewed during MakeZen implementation.

---

## Completed Work (February 2026 Polish Sprint)

- ✓ **P0**: Shop removal, back button cleanup, results screen dual buttons
- ✓ **P1**: Star rating system (3 tiers, animated diamonds, wired into results)
- ✓ **Hint system**: Particle trails after 10s inactivity
- ✓ **Tile number colors**: All 10 values with distinct colors

**Still pending from old sprint (folded into Launch Sprint):**
- Tile shadows (built but gated behind `isEnhanced`) → L1
- Match VFX reorder (beam before "10") → L5
- Beam opacity reduction → L5
- Haptic feedback → L5
- Credits/About section → L7
- Leaderboard → L7
- Tutorial popup click-outside bug → L7

---

## Launch Sprint — "The Cake" (February/March 2026)

### Design Philosophy

Make10's arcade mode is a 60-second math sprint. It's functional, but it doesn't let players enter a flow state. Games like Holedown, Threes, and Tetris Effect succeed because they give players room to breathe — strategic pacing, cohesive aesthetics, and audio that responds to gameplay. This sprint transforms Make10 from a single-mode puzzle test into a game with two distinct moods, musical responsiveness, and the visual cohesion of a polished mobile title.

**The four layers:**

1. **MakeZen Mode** — A 5-minute focused mode with locked tiles that gives players a reason to stay (structural)
2. **Adaptive Audio** — Matches that sound good and build an evolving soundscape (experiential)
3. **Aesthetic Cohesion** — Easing, color, rounded forms, consistent motion, ambient life (polish)
4. **Shop & Cosmetics** — 2D paper doll avatar system with BP-driven unlocks (progression)

**Reference games:** Holedown (cohesive aesthetic, upgrade loop), Threes ("one more turn" retention), Tetris Effect (musical responsiveness, stem layering)

---

### L0 — Easing & Motion Overhaul

**Priority: CRITICAL**

Every animation uses linear `Mathf.Lerp` or raw sine waves. This makes the game feel mechanical.

#### Easing Library (AnimationUtilities.cs)

```
New static methods:
  EaseOutCubic(float t)     → 1 - (1-t)³           — landing, settling
  EaseInOutCubic(float t)   → smooth S-curve        — transitions
  EaseOutBack(float t)      → overshoot then settle — popups, UI
  EaseOutElastic(float t)   → springy bounce        — tile landing
  EaseInCubic(float t)      → slow start, fast end  — anticipation
```

#### Methods to Update

| Method | Current | New | Why |
|--------|---------|-----|-----|
| `PunchScale()` | Linear 2-part | EaseOutBack up, EaseOutCubic down | Snappy punch |
| `PopIn()` | Linear 2-part | EaseOutBack pop, EaseOutCubic settle | Overshoot and settle |
| `FloatAndFade()` | Linear | EaseOutCubic position, EaseInCubic alpha | Decelerate as rising |
| `ScaleIn()` | Linear/curve | EaseOutBack default (keep curve override) | Panels overshoot |
| `ScaleOut()` | Linear | EaseInCubic | Anticipation before shrink |
| `DropIn()` | Custom bounce | EaseOutElastic | Cleaner bounce |
| `CountUp()` | Already EaseOutCubic ✓ | No change | Already good |

#### Tile Fall (GridManager.cs)
Current: `tileFallSpeed = 1600f` constant velocity. Change to time-based (0.25s) with `EaseOutCubic`. Add landing bounce (~5px overshoot, `EaseOutElastic` over 0.08s). Pairs with existing `SpawnLandSparkle()`.

#### Tile Swap (GridManager.cs)
Current: `swapDuration = 0.15f` linear Lerp. Change to `EaseInOutCubic`. Add shallow arc: `yOffset = Mathf.Sin(t * Mathf.PI) * 8f`.

#### Countdown Pop (SceneFlowManager.cs)
Current: 1.5→1 linear. Change to `EaseOutBack`. "GO!" uses larger overshoot (1.8→1, `EaseOutElastic`).

#### Menu Title (MainMenuUI.cs)
Current: 3 overlapping sine waves (chaos). Change to single smooth bob with `EaseInOutCubic`. Remove rotation wobble and scale pulse.

**Scope:** Medium. Easing functions are trivial one-liners. Threading through existing methods is methodical.

---

### L1 — Tile Visual Overhaul

**Priority: HIGH**

#### A. Shadows (Already Built — Ungating Only)
Shadow system in `Tile.cs` (`CreateShadowText()`) is gated behind `isEnhanced` (always `false`).
1. Remove `isEnhanced` gate on shadow visibility
2. Remove forced `SetActive(false)` in `UpdateEnhancedGlow()`
3. In `SetValue()`, derive shadow color: `Color.Lerp(NumberColors[value], Color.black, 0.65f)`
**Scope:** ~10 lines.

#### B. Tinted Backgrounds
All tiles are uniform grey. Add subtle tint: `Color.Lerp(Color.white, NumberColors[value], 0.12f)`
Apply in `SetValue()` to `backgroundImage.color`.
**Scope:** 2-3 lines.

#### C. Rounded Corners
Add a white rounded-rect PNG (64×64, ~12px radius), 9-slice in Sprite Editor, assign to tile prefab Background Image.
**Scope:** One asset + prefab change in Unity Editor.

#### D. Selection Feedback
Add `PunchScale(1.15f, 0.1f)` on first selection. Add `Handheld.Vibrate()` for haptic. Animate deselection (EaseOutCubic scale to 1.0 over 0.08s) instead of instant reset.
**Scope:** ~10 lines in `Tile.cs`.

---

### L2 — MakeZen Mode (5-Minute Focused Sessions)

**Priority: HIGH — Structural change enabling extended, thoughtful play.**

**Reference prototype:** `MakeZen/make10zen_v6.jsx` (complete React implementation — use as ground truth for edge cases)

#### Game Design

- **5-minute timer** (300s). Game ends when timer expires OR board has no valid moves and no reshuffles remain.
- **Locked tiles** — the core new mechanic. When a row/col sums to a multiple of 10, tiles converge into a single immovable tile displaying the sum (10, 20, 30, etc.). Locked tiles can't be selected/swapped, participate in row/col sums (enabling 20→30→40+ chains), only move via gravity, and gradually fill the board creating pressure.
- **Failed swap penalty** — no lives. Failed swaps shake the screen, revert the tiles, deduct 3 seconds from timer, and reset multiplier to ×1.
- **Multiplier: No Drain, Manual Reset** — multiplier doesn't decay over time (unlike Arcade). Resets to ×1 on failed swap. Rewards thoughtful play, punishes random guessing.
- **Reshuffles** — 3 max per session. When no valid moves exist, non-locked tiles reshuffle automatically. When reshuffles are exhausted and no moves remain, game ends early.
- **Progressive difficulty** — 7-tier ramp based on match count (not time), matching prototype:
  ```
  0-7 matches:   tiles 0-4   "calm"
  8-17 matches:  tiles 0-5   "gentle"
  18-29 matches: tiles 0-6   "steady"
  30-44 matches: tiles 0-7   "rising"
  45-64 matches: tiles 0-8   "focused"
  65-89 matches: tiles 0-9   "deep"
  90+ matches:   tiles 1-9   "mastery" (no more 0s)
  ```
- **Session length target:** 5 min per round, 3 rounds = 15 min (research-backed cognitive training dose).

#### Locked Tile System (NEW — Core Mechanic)

**Tile.cs additions:**
```
bool IsLocked             — true when Value >= 10
Color[] LockedTileColors  — tier-based colors matching prototype:
  10: Gold (#f0d060)       40: Red (#ff6060)
  20: Purple (#d090ff)     50: Orange (#ffa030)
  30: Teal (#50e8d0)       60+: Blue (#60a0ff)
```

1. Add `IsLocked` property: `public bool IsLocked => Value >= 10;`
2. In `SetValue()`: if locked, apply locked-tier color to number text, enable glow system (reuse existing `enhancedGlowImage` + `shadowText` — currently gated behind `isEnhanced` which is always false), set background to subtle tinted gradient
3. Block selection: `HandleTileClicked` / `HandleTileSwiped` / drag handlers must early-return if `tile.IsLocked`
4. Display: locked tiles show their sum value (10, 20, 30...) with smaller font, diamond marker (◆) in corner
5. **Ungating the glow/shadow system**: Remove `isEnhanced = false` hardcoding in `UpdateEnhancedGlow()`. Instead: `isEnhanced = IsLocked;`. Shadow color derived from locked tier color.

**Scope:** ~30-40 lines changed in Tile.cs. Glow/shadow infrastructure already built.

#### Match → Merge → Gravity Flow (GridManager.cs)

The current `ProcessMatchesCoroutine` flow is: detect match → animate → clear tiles → drop → spawn new. MakeZen changes the middle steps:

**New flow for MakeZen matches:**
```
1. Detect match (existing MatchChecker — no changes needed for detection)
2. Animate convergence: matched tiles shrink/slide toward merge position
3. Create locked tile: at merge position, set Value = line sum (10, 20, 30...)
4. Remove other tiles in the matched line (not the merge tile)
5. Gravity: ALL tiles fall (including locked), filling gaps
6. Spawn new tiles: only in empty cells, only non-locked values (from tile bag)
7. Anti-cascade check: re-roll if new tile would complete a match
8. Repeat from step 1 (chain detection — locked tiles participate in sums)
```

**Merge position logic** (from prototype `getMergePos()`):
- Prefer the position where the second-tapped tile landed on the matching line
- Fallback: position of first-tapped tile on the matching line
- Final fallback: center of line (index 2)

**Key implementation details:**
- `ProcessMatchesCoroutine()`: Add `if (GameManager.Instance.CurrentMode == GameMode.Zen)` branch that calls new `ProcessZenMatch()` instead of `ClearMatchedTiles()` + `SpawnNewTilesCoroutine()`
- `ProcessZenMatch(MatchResult, Tile firstSwapped, Tile secondSwapped)`: Handles convergence animation, locked tile creation, selective removal, gravity, and spawning
- Gravity must move ALL tiles (locked included) — locked tiles are heavy but not anchored
- `SpawnNewTilesCoroutine()` for Zen: skip cells occupied by locked tiles
- `ResetGridSilent()` for Zen: only reshuffle non-locked tiles (preserve locked positions and values)

**Scope:** Medium-large. ~150-200 new lines. Most complex single change.

#### MatchChecker.cs Modifications

1. `HasValidMoves()` / `FindHintMove()`: Skip locked tiles as swap candidates (can't select them), but include their values in row/col sum calculations
2. Sum calculation already handles any tile value — no change needed for detecting 20/30/40 sums
3. `FindAllSwaps()` (internal): Filter `getFreeCells()` equivalent — only iterate non-locked tiles

**Scope:** ~20-30 lines changed.

#### GameManager.cs Modifications

**Already implemented (✓):**
- ✓ `GameMode` enum (`Arcade`, `Zen`)
- ✓ Timer gated behind `CurrentMode == GameMode.Arcade`
- ✓ `OnFailedSwap()` — resets multiplier in Zen
- ✓ `UseReshuffle()` / `zenReshufflesRemaining`
- ✓ `ZenGameOver()` — fires when no moves + no reshuffles
- ✓ Zen-specific high scores and star thresholds
- ✓ Multiplier doesn't drain in Zen (already gated)

**New for MakeZen:**
1. Change `ActivateGame()`: Zen uses 300s (5 min) instead of 99999f
2. `OnFailedSwap()`: Add 3-second time penalty (`TimeRemaining -= 3f`) alongside multiplier reset
3. Add MakeZen stats tracking:
   ```
   int zenLockedTileCount      — total locked tiles created this session
   int zenHighestLockedValue   — highest single locked tile (10, 20, 30...)
   int zenChainCount           — total cascade chains
   int zenMatchCount           — total matches (for difficulty ramp)
   ```
4. Expose `ZenMatchCount` for GridManager's difficulty ramp lookup
5. Star thresholds for MakeZen: 500 / 1000 / 2000 BP (already set)

**Scope:** ~40 lines. Mostly additive.

#### SceneFlowManager.cs Modifications

1. Add `GameState.ModeSelect` to enum
2. Add ModeSelect panel reference
3. Flow: MainMenu → ModeSelect → [Arcade] → Countdown → Game / [MakeZen] → Game (no countdown, starts immediately)
4. `OnGameEnded()`: route to Results regardless of mode (already works)

**Scope:** ~30 lines. Pattern matches existing panel transitions.

#### UIManager.cs Modifications

1. MakeZen timer display: same countdown but styled differently (calmer color, no danger-zone red flash until last 30s)
2. Add locked tile counter display (small "◆ 7" indicator near score)
3. Results screen for MakeZen:
   - Header: "STILLNESS" (matching prototype) instead of "TIME'S UP"
   - Stats: Score / Highest Tile / Matches / Chains / Reshuffles Used
   - Star rating with Zen thresholds
4. High scores: already mode-separated (✓)

**Scope:** ~60 lines. Mostly conditional UI branching.

#### MainMenuUI.cs Modifications

- ModeSelect buttons: "ARCADE" and "MAKEZEN"
- Separate high score display per mode (already persisted, just needs display)

**Scope:** ~15 lines.

#### Difficulty Ramp (TileWeightManager.cs)

Extend existing solve-based ramp to match prototype's 7-tier system:

```
Current (Arcade):              New (MakeZen):
  5s after 2 solves              5s after 8 matches (gentle)
  6s after 5 solves              6s after 18 matches (steady)
  7s after 8 solves              7s after 30 matches (rising)
  Full ramp at 12               8s after 45 matches (focused)
                                 9s after 65 matches (deep)
                                 Drop 0s after 90 matches (mastery)
```

Add `GetZenDifficulty(int matchCount)` to TileWeightManager returning `(int minTile, int maxTile, string label)`. Gate behind mode check in `GetAdjustedWeights()`.

**Scope:** ~30 lines. Parallel to existing ramp, not replacing it.

#### What's Explicitly OUT of MVP (Playtest Build)

- ❌ Dark color scheme from prototype (keep existing Unity visuals)
- ❌ Difficulty label toasts ("calm", "gentle" popups)
- ❌ Board pressure indicator bar
- ❌ Locked tile glow animations (simple color tint for now, fancy glow in polish pass)
- ❌ Convergence particle effects (use existing match VFX)
- ❌ Any changes to Arcade mode behavior

**Total scope estimate:** ~350-450 new/changed lines across 6 files. TileWeightManager.cs and GridValidation.cs already created (Session A.1). GridManager is ~1,920 lines (trimmed from 2,292).

---

### L3 — Adaptive Audio & FMOD Migration

**Priority: HIGH — Experiential layer + fixes current audio glitching.**

#### Why FMOD

The current AudioManager has 3 Unity AudioSources. All SFX share one source, causing dropout when the time warning loop (`Play()`) conflicts with match sounds (`PlayOneShot()`). Building an adaptive 4-stem music system on top of this would be fragile.

FMOD solves both problems:
- **Polyphony management**: Proper voice pooling, no SFX dropout
- **Stem layering**: Multi-track events with parameter-driven mixing (multiplier → music intensity is just a parameter)
- **Pitch shifting**: Parameter on match events, no manual `sfxSource.pitch` hacking
- **Synchronization**: Stems stay in sync natively
- **Free** for indie games under $200k revenue

#### FMOD Integration Steps

1. **Install FMOD for Unity** plugin (Unity Package Manager or .unitypackage from fmod.com)
2. **Create FMOD Studio project** alongside Unity project
3. **Migrate existing SFX**: Import current .wav files into FMOD, create events for each
4. **Replace AudioManager internals**: Swap `AudioSource.PlayOneShot()` calls with `RuntimeManager.PlayOneShot()` or event instances
5. **Keep AudioManager as facade**: Public API stays the same (`PlayTenPopSound()`, etc.), internal implementation changes to FMOD calls
6. **Volume control**: Use FMOD buses (Master, Music, SFX, Voice) instead of per-source volume

#### Adaptive Music Architecture (FMOD Studio)

Create a single multi-track music event with game parameters:

```
FMOD Parameter: "Intensity" (0.0 → 1.0)
  0.0  = Base layer only (ambient pad)
  0.3  = + Rhythm layer
  0.6  = + Melody layer
  1.0  = + Intensity layer (hot streak)

FMOD Parameter: "Inactivity" (0.0 → 1.0)
  Layers fade out as inactivity increases

FMOD Parameter: "GameMode" (0 = Arcade, 1 = Zen)
  Selects between stem sets (upbeat vs ambient)
```

**Layer trigger logic (GameManager.Update → FMOD parameter updates):**

| Layer | Trigger ON | Trigger OFF |
|-------|-----------|-------------|
| Base (pad) | Game starts | Game ends |
| Rhythm | Multiplier ≥ ×1.5 OR 3+ solves | Below ×1.25 OR 8s inactivity |
| Melody | Multiplier ≥ ×2.0 OR 6+ solves | Below ×1.5 OR 12s inactivity |
| Intensity | Hot streak active | Hot streak ends |

#### Tuned Match SFX (FMOD)

Pitch shifting via FMOD event parameters instead of `sfxSource.pitch`:

```
Match sum 10 → pitch 1.0 (root note)
Match sum 20 → pitch 1.25 (major 3rd)
Match sum 30 → pitch 1.5 (perfect 5th)

Cascade level 1 → pitch 1.0
Cascade level 2 → pitch 1.12
Cascade level 3 → pitch 1.25
Cascade level 4 → pitch 1.5
```

#### Music Assets Needed (CJ — Parallel Track)

Per mode (Arcade + Zen = 8 stems total):
1. **Base**: Ambient pad / drone, harmonically simple, seamless loop
2. **Rhythm**: Light percussion, same BPM/key as base
3. **Melody**: Arpeggiated pattern, reward layer
4. **Intensity**: Energetic, hot streak soundtrack

All 4 stems per mode: same duration, same BPM, same key, designed to layer.

**Scope:** Medium-large. FMOD integration is a one-time investment. Music production is the long pole.

---

### L4 — Ambient Life & Environmental Polish

**Priority: MEDIUM**

#### Ambient Particles (GridVFX.cs)
Current: 8 particles, 18 px/sec, 0.06 alpha (nearly invisible).
Change: 15-20 particles, 0.12-0.15 alpha, size variation (0.5×-1.5×), tinted to dominant board color. Hot streak: 30+, 0.25 alpha, warm tint. Zen: slower drift (12 px/sec), cooler colors.

#### Idle Tile Breathing
`1.0 + sin(Time.time * 0.5 + tileIndex * 0.3) * 0.008` — wave effect across grid, 0.8% variation. Gate behind `!IsSelected && !isAnimating` in `Tile.cs` Update.

#### Score Environment Feedback
- Match: non-matched tiles micro-punch (1.02 scale, 0.06s) with stagger ripple
- Hot streak start: all tiles flash bright (30% white lerp, 0.15s)
- Multiplier increase: particles briefly 2× speed

**Scope:** Small-medium.

---

### L5 — Match VFX & Sequence Polish

**Priority: MEDIUM**

#### Reorder Match Sequence (GridManager.AnimateSolveSequence)
Current: beam → pause → avatar → convergence → "10" popup
New: convergence → "10" popup (PunchScale EaseOutBack) → 0.12s hold → beam (reduced opacity) → particles → avatar → screen shake → haptic

#### Beam Opacity (GridVFX.cs FlashBeam)
Reduce: glow 0.6→0.35, core full→0.7, hot core 1.4×→0.9×, sparkles 0.9→0.6

#### "10" Popup Enhancement
Increase PopIn overshoot 1.2→1.4. Add screen shake on pop. Hold 0.12s longer before fade. Scale by match sum (20→1.2×, 30→1.5×).

#### Haptics
`Handheld.Vibrate()` on: tile selection, match clear, hot streak trigger, game over.

**Scope:** Medium.

---

### L6 — Shop System & Cosmetics

**Priority: MEDIUM-HIGH — Progression loop for replayability.**

#### Shop Design
Accessible from main menu. BP earned from both modes unlocks 2D sprite overlay cosmetics.

| Category | Slot | Launch Items |
|----------|------|-------------|
| Eyes | 1 | 4 (Default + 3) |
| Hair | 1 | 4 (Default + 3) |
| Clothes | 1 | 3 (Default + 2) |
| Hat | 1 | 3 (None + 2) |

14 items × ~6 poses = ~84 sprites (small partial images, not full redraws).

#### Avatar Architecture (AvatarManager.cs)
Current: Single Image, 6 static PNGs. New: Layered UI Image stack:
```
AvatarContainer → BodyLayer → ClothesLayer → EyesLayer → HairLayer → HatLayer
```
Existing coroutine animations apply to parent transform (all layers move together).

#### Data Model
- `CosmeticData.cs` — ScriptableObject: id, displayName, category, bpCost, poseSprites[], poseOffsets[], isDefault
- `CosmeticInventory.cs` — Singleton: unlockedItems (HashSet), equippedItems (per category), persisted via PlayerPrefs JSON

#### Shop UI
```
[Back]        SHOP        BP: 1240
[Avatar Preview — live paper doll]
[Eyes] [Hair] [Clothes] [Hat]  ← category tabs
[Scrollable item grid: locked/owned/equipped states]
```
Purchase flow: tap locked item → preview on avatar → confirm → deduct BP → auto-equip.

#### BP Persistence (RunManager.cs)
- `"Make10_TotalBP"` — lifetime earned (never decreases)
- `"Make10_SpendableBP"` — current balance (decreases on purchase)
- `SpendBP(int amount)` → returns bool

#### Pricing
```
Common:   200-400 BP  (1-2 arcade rounds)
Uncommon: 500-800 BP  (3-4 rounds or 1 zen session)
Rare:     1000-1500 BP (dedicated play)
```

#### New Scripts
| Script | Purpose |
|--------|---------|
| `CosmeticData.cs` | ScriptableObject: cosmetic item definition |
| `CosmeticInventory.cs` | Singleton: persistent unlock/equip state |
| `ShopManager.cs` | Shop UI: categories, items, purchase flow |
| `ShopCard.cs` | Individual item card in shop grid |

**Scope:** Large. Art pipeline (~84 sprites) is the long pole.

---

### L7 — Remaining Polish

**Priority: LOW-MEDIUM**

- **Credits**: Button in MainMenuUI → PopupWindow scrollbar mode. "Make10 by CJ Rhone / Wizard Bodega"
- **Leaderboard**: Top 10 per mode in PlayerPrefs JSON. PopupWindow display.
- **Tutorial**: Add mode explanation slide. Fix click-outside-to-dismiss bug in PopupWindow.
- **Android back button**: Deferred (Unity Input System complexity).

---

### Sprint Execution Order

```
Phase 1 — Foundation
  1. L0: Easing overhaul
  2. L1: Tile visual overhaul

Phase 2 — MakeZen MVP (5 implementation sessions)
  Session A: Locked tile system (Tile.cs) + MatchChecker awareness ✓
  Session A.1: Extract TileWeightManager + GridValidation from GridManager ✓
  Session B: Merge/gravity flow (GridManager.cs — ProcessZenMatch)  ← NEXT
  Session C: Game flow wiring (GameManager, SceneFlowManager, difficulty ramp)
  Session D: UI + results + navigation fixes (UIManager, SceneFlowManager, MainMenuUI)
  Session E: Zen swap-any mechanic + animated revert (Tile.cs, GridManager.cs, MatchChecker.cs)

Phase 3 — The Feel
  5. L3: FMOD migration + adaptive audio
  6. L5: Match VFX reorder + haptics
  7. L4: Ambient life

Phase 4 — The Loop
  8. L6: Shop system + cosmetics
  9. L7b: Credits, leaderboard, final polish

Phase 5 — Music Production (parallel, CJ-driven)
  ∥ 4-stem ambient set (MakeZen)
  ∥ 4-stem upbeat set (Arcade)
  ∥ Tuned match SFX (or pitch-shift approach)
```

**Phase 2 (MakeZen MVP) is the current priority.** Phases 1 can be done before or after — easing makes both modes feel better but isn't blocking. Phase 2 sessions are designed to be tackled one per sitting, each building on the last.

### MakeZen Implementation Sessions (Detailed)

#### Session A — Locked Tile Foundation (~1-2 hours)
**Files:** `Tile.cs`, `MatchChecker.cs`
**Goal:** Tiles can be "locked" and the system recognizes them.

1. Add `IsLocked` property to Tile (`Value >= 10`)
2. Add `LockedTileColors` array (gold/purple/teal/red/orange/blue tiers)
3. Modify `SetValue()`: if locked → apply tier color, enable glow/shadow, set tinted background
4. Ungating: change `UpdateEnhancedGlow()` to activate for locked tiles
5. Block interaction: guard `HandleTileClicked`, `HandleTileSwiped`, drag handlers
6. MatchChecker: update `HasValidMoves()` and `FindHintMove()` to skip locked tiles as swap candidates
7. **Test:** Manually set a tile to value 10 in the editor → verify it renders as locked, can't be selected, glow works

#### Session A.1 — Extract TileWeightManager + GridValidation ✓ COMPLETE
**Files:** `GridManager.cs` (extract from), `TileWeightManager.cs` (new), `GridValidation.cs` (new)
**Goal:** Reduce GridManager complexity by extracting self-contained subsystems into dedicated managers.

**What was extracted:**

1. **TileWeightManager.cs** (new singleton) — tile value weights (weight0-7), progressive difficulty ramp (solvesFor5s/6s/7s, maxWeight5/6/7, solvesToFullRamp, baseTileReduction), Tetris-style tile bag system (GetWeightedRandomValue, RefillTileBag, GetAdjustedWeights, GetCurrentWeights). Public API: `GetWeightedRandomValue()`, `ClearBag()`.

2. **GridValidation.cs** (new singleton) — consecutive 10s scaling (baseTenScale, tenScaleIncrement, maxTenScale, consecutiveResetTime), initial match prevention (EnsureNoInitialMatches, FindMatchingLines, ReRollTileToBreakMatch, CheckLineSum), anti-cascade checks (WouldTileCompleteMatch). Public API: `RegisterMatch()`, `GetTenScale(lineSum)`, `ConsecutiveCount`, `ResetConsecutive()`, `EnsureNoInitialMatches(grid, w, h, twm)`, `WouldTileCompleteMatch(grid, w, h, x, y, val)`.

**GridManager.cs changes:** Removed ~340 lines and 20 serialized fields. Added `[SerializeField] private TileWeightManager tileWeightManager` and `[SerializeField] private GridValidation gridValidation` under a "Managers" header. All call sites updated to delegate through these managers.

**🎮 Unity Editor setup (required for each new manager):**
- Create GameObject, add component, copy serialized values, drag reference into GridManager Inspector.

**Future: Session C will add `GetZenDifficulty()` to TileWeightManager.**

#### Session B — Merge & Gravity (~2-3 hours, most complex)
**Files:** `GridManager.cs` (primary), `GridValidation.cs` (anti-cascade calls already delegated)
**Goal:** Matches create locked tiles instead of clearing the line.

**Key context for Session B:**
- `GridManager.cs` is now ~1,920 lines (down from 2,292 after A.1 extractions)
- Anti-cascade check is `gridValidation.WouldTileCompleteMatch(grid, gridWidth, gridHeight, x, y, value)` — already wired
- Tile spawning uses `tileWeightManager.GetWeightedRandomValue()` — already wired
- Locked tile rendering (Tile.cs) and MatchChecker awareness already done in Session A
- Reference prototype: `MakeZen/make10zen_v6.jsx` — use `getMergePos()` and `processMatches()` as ground truth

**Steps:**
1. Add `ProcessZenMatch(MatchResult, Tile firstSwapped, Tile secondSwapped)` method
2. Implement merge position logic (from prototype's `getMergePos`)
3. Convergence animation: matched tiles shrink/slide toward merge position
4. At merge position: call `tile.SetValue(lineSum)` — tile becomes locked
5. Remove other tiles in matched line (Destroy)
6. Gravity: drop ALL tiles (including locked) to fill gaps
7. Spawn new tiles only in empty cells, using tile bag (non-locked values only)
8. Wire into `ProcessMatchesCoroutine`: mode branch → `ProcessZenMatch` for Zen
9. Update `ResetGridSilent()`: only reshuffle non-locked tiles
10. **Test:** Play in Zen mode → make a match → verify locked tile appears, gravity works, chains detect correctly

#### Session C — Game Flow Wiring (~1-2 hours)
**Files:** `GameManager.cs`, `SceneFlowManager.cs`, `TileWeightManager.cs`
**Goal:** MakeZen is a playable mode from the menu.

1. GameManager: Change Zen timer to 300s, add 3s penalty to `OnFailedSwap()`, add Zen stats tracking
2. SceneFlowManager: Add `ModeSelect` state, wire panel transitions
3. TileWeightManager: Add `GetZenDifficulty()` for 7-tier ramp, gate in `GetAdjustedWeights()`
4. Wire ModeSelect → SetGameMode → ActivateGame flow
5. **Test:** Launch game → select MakeZen → 5-min timer starts → failed swap costs 3s → game ends at 0:00

#### Session D — UI, Results & Polish (~1-2 hours)
**Files:** `UIManager.cs`, `SceneFlowManager.cs`, `MainMenuUI.cs`
**Goal:** MakeZen has proper UI, correct navigation, and a satisfying results screen.

1. **Results header**: Change Zen results title from "GAME OVER" to "Well Done." (line 944 in UIManager.cs)
2. **Zen results breakdown**: Replace "Survival Time" line with Zen-specific stats:
   - Highest Tile (GameManager.ZenHighestLockedValue)
   - Matches (GameManager.ZenMatchCount)
   - Chains (GameManager.ZenChainCount)
   - Reshuffles Used (zenMaxReshuffles - ZenReshufflesRemaining)
   Keep Score line and Total as-is (scoring math unchanged).
3. **Navigation bug fix — Return to Main Menu**: `OnGameEnded()` sets state to `GameState.Results`, so `GoBack()` routes through Arcade return path (horizontal slide) instead of `ReturnToMainMenuFromZen()` (vertical slide). Fix: track originating mode in a field (e.g., `private GameManager.GameMode resultsOriginMode`) set in `OnGameEnded()`, then branch in `GoBack()` Results case.
4. **Navigation bug fix — Play Again from Zen**: Currently calls `RestartWithCountdown()` (Arcade restart with countdown). For Zen: "Play Again" should return to MainMenu (vertical slide) so player can choose mode again. Branch in `UIManager.OnContinueButtonClicked()` based on mode.
5. **Locked tile counter on HUD**: Small "◆ 7" indicator near score, only visible in Zen mode. GameManager already exposes `ZenLockedTileCount`. Create programmatically in UIManager like other HUD elements.
6. **Calm timer verification**: `UpdateZenTimerDisplay()` already exists. Verify danger-zone red flash uses 30s threshold (not Arcade's 10s). Calmer color during normal play.
7. **Per-mode high score on MainMenu**: High scores already persisted separately. Display Zen high score alongside Arcade high score (or show relevant one based on last-played mode).
8. **Test:** Complete full MakeZen round → "Well Done." header → Zen stats display correctly → Return to Menu uses vertical slide → Play Again returns to MainMenu → high score saved per mode

#### Session E — Zen Swap-Any Mechanic (~2-3 hours)
**Files:** `Tile.cs`, `GridManager.cs`, `MatchChecker.cs`
**Goal:** Zen mode uses a "swap any two free tiles" mechanic with animated revert on failed swaps.

**Design:** In Zen mode, players can swap ANY two non-locked tiles on the board (not just adjacent). The swap always animates forward first. If the swap results in a match, normal match processing continues. If the swap does NOT result in a match, tiles animate back to their original positions with screen shake + red flash + existing 3s penalty + multiplier reset.

This creates a more deliberate, strategic feel — players scan the whole board for combinations rather than frantically swapping neighbors.

**Tile.cs — Input Changes:**
1. Remove adjacency requirement for Zen mode. Currently tile selection validates that second-selected tile is adjacent to first. In Zen: any non-locked tile is a valid second selection.
2. Selection flow stays the same: tap tile 1 → tap tile 2 → attempt swap.
3. Guard: both tiles must have `!IsLocked`. Already implemented for first selection; ensure second selection also checks.
4. Visual feedback: first selected tile stays highlighted while player picks second tile (already works).

**GridManager.cs — Swap System Changes:**
1. **`AttemptSwap()` or equivalent**: Currently validates adjacency, then calls swap animation. For Zen:
   - Skip adjacency check (mode-gated)
   - Animate swap (tiles travel to each other's grid positions — may need longer duration for distant tiles, or keep constant)
   - After swap animation completes: run match detection on the new board state
   - If match found → proceed to `ProcessMatchesCoroutine()` as normal
   - If NO match found → call new `RevertSwapCoroutine(tileA, tileB)`:
     a. Animate tiles back to original positions (reverse the swap animation)
     b. Trigger screen shake (existing `GridVFX.Instance?.TriggerScreenShake()`)
     c. Red flash on grid background (brief Color.Lerp to red, 0.15s)
     d. Call `GameManager.Instance.OnFailedSwap()` (already handles 3s penalty + multiplier reset)
   - Reset grid array to pre-swap state before revert animation
2. **Swap animation for non-adjacent tiles**: Current arc animation (`yOffset = Mathf.Sin(t * Mathf.PI) * 8f`) works for adjacent. For distant tiles, consider:
   - Same arc but scaled to distance
   - Or straight-line path (simpler, still reads clearly)
   - Duration: fixed 0.2s regardless of distance (keeps game feel snappy)
3. **Mode gating**: All changes behind `GameManager.Instance.CurrentMode == GameManager.GameMode.Zen`. Arcade swap behavior unchanged.

**MatchChecker.cs — Validation Changes:**
1. **`HasValidMoves()` for Zen**: Must check ALL pairs of free (non-locked) tiles, not just adjacent pairs. For a 5×5 grid with N free tiles, that's N*(N-1)/2 pairs (worst case ~300). For each pair: simulate swap → check all 10 lines (5 rows + 5 cols) for multiples of 10 → undo swap. This is more expensive but still trivial for a 5×5 grid.
2. **`FindHintMove()` for Zen**: Same — iterate all free tile pairs, return first valid swap. Consider caching or early-exit optimization.
3. **`FindAllSwaps()` internal**: Same expansion for Zen mode.
4. **Performance note**: Even brute-force all-pairs on 5×5 is <300 checks × 10 lines = 3000 sum checks. Negligible.

**Existing infrastructure to reuse:**
- `GameManager.OnFailedSwap()` — already handles 3s penalty + multiplier reset (✓)
- `GridVFX.Instance.TriggerScreenShake()` — already exists (✓)
- Screen shake already fires on failed swap in current code (verify)
- Swap animation coroutine in GridManager — extend rather than rewrite

**Key edge cases:**
- Player taps same tile twice → deselect (already handled)
- Player taps locked tile as second selection → ignore, keep first selection active
- Swap creates multiple matches simultaneously → process all (existing cascade system handles this)
- Swap animation interrupted by game-over timer → cancel gracefully

**What's explicitly NOT in this session:**
- ❌ Changing Arcade swap behavior (stays adjacent-only)
- ❌ Visual indicator showing valid swap targets (highlight possible pairs) — future polish
- ❌ Drag-to-swap for distant tiles (tap-tap only for non-adjacent)

**Scope estimate:** ~150-200 new/changed lines across 3 files. Primary complexity is in GridManager swap flow and MatchChecker all-pairs validation.

---

### Post-Sprint State (After MakeZen MVP)

**New scripts (already created):**
- `TileWeightManager.cs` — extracted from GridManager: tile weights, bag system, difficulty ramp (Session A.1)
- `GridValidation.cs` — extracted from GridManager: match prevention, anti-cascade, consecutive match tracking (Session A.1)

**Future sprint scripts:** CosmeticData.cs, CosmeticInventory.cs, ShopManager.cs, ShopCard.cs (L6)

**Modified scripts (MakeZen MVP):**
```
Tile.cs              → + IsLocked, locked tile rendering, glow ungating, interaction blocking (Session A ✓)
MatchChecker.cs      → + locked tile awareness in move validation (Session A ✓)
GridManager.cs       → - tile weights, match prevention, consecutive tracking (extracted A.1 ✓)
                       + ProcessZenMatch(), merge logic, Zen gravity, Zen reshuffle (Session B)
                       + Swap-any mechanic, animated revert on failed swap (Session E)
MatchChecker.cs      → + locked tile awareness in move validation (Session A ✓)
                       + All-pairs swap validation for Zen (Session E)
TileWeightManager.cs → NEW ✓, + 7-tier Zen difficulty ramp (Session C)
GridValidation.cs    → NEW ✓ (no further MakeZen changes expected)
GameManager.cs       → + 5-min timer, failed swap penalty, Zen stats tracking (Session C)
SceneFlowManager.cs  → + Zen game state, navigation routing fix (Session C + D)
UIManager.cs         → + locked tile counter, Zen results screen, calm timer, nav fixes (Session D)
MainMenuUI.cs        → + per-mode high scores (Session D)
```

**Updated singletons:**
```
GameManager.Instance        → + MakeZen timer (300s), failed swap penalty, Zen stats
TileWeightManager.Instance  → NEW ✓ (tile weights, bag, progressive ramp)
GridValidation.Instance     → NEW ✓ (match prevention, consecutive tracking)
```

**Updated game flow:**
```
Loading → MainMenu → ModeSelect → [Arcade]  → Countdown → Game (60s) → Time's Up → Results
                                → [MakeZen] → Game (300s, locked tiles) → Stillness → Results
                   → Options → MainMenu
```

**Updated game states:**
```
Loading, MainMenu, ModeSelect, Tutorial1, Tutorial2, Countdown,
Game, Results, Options, Quit
```

**Post-L6 additions (future):**
```
+ Shop, Leaderboard, Credits states
+ CosmeticInventory.Instance, ShopManager.Instance singletons
```
