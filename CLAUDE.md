# Claude Notes - Make10 Project

## Project Overview

Make10 is a number puzzle game where players swap tiles to create rows/columns summing to multiples of 10. Two modes: **Arcade** (60-second sprint) and **MakeZen** (5-minute focused session with locked tiles and board pressure). Originally created for Brainless Game Jam 2026. Converted from roguelike to arcade style in February 2026. MakeZen mode designed February 2026, prototyped in React (`MakeZen/make10zen_v6.jsx`), Unity implementation complete (Sessions A–E, February 2026).

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

### MakeZen Mode (✓ Implemented)
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

## Completed Work

### February 2026 Polish Sprint
- ✓ **P0**: Shop removal, back button cleanup, results screen dual buttons
- ✓ **P1**: Star rating system (3 tiers, animated diamonds, wired into results)
- ✓ **Hint system**: Particle trails after 10s inactivity
- ✓ **Tile number colors**: All 10 values with distinct colors

### MakeZen MVP (Sessions A–E, February 2026) ✓ COMPLETE
- ✓ **Session A**: Locked tile foundation — `IsLocked` property, locked tile rendering/colors, glow/shadow ungating, interaction blocking, MatchChecker awareness
- ✓ **Session A.1**: Extracted `TileWeightManager.cs` and `GridValidation.cs` from GridManager (~340 lines removed, 20 serialized fields moved)
- ✓ **Session B**: Merge & gravity — `ProcessZenMatch()`, merge position logic, convergence animation, locked tile creation, Zen gravity, Zen reshuffle
- ✓ **Session C**: Game flow wiring — 300s timer, 3s failed-swap penalty, Zen stats tracking, ModeSelect state, 7-tier difficulty ramp
- ✓ **Session D**: UI & results — "Well Done." header, Zen stats breakdown, navigation fixes (vertical slide return), locked tile counter HUD, calm timer, per-mode high scores
- ✓ **Session E**: Swap-any mechanic — any-two-free-tiles swapping in Zen, animated revert on failed swap, all-pairs move validation, screen shake + red flash on fail

**Still pending from old sprint (folded into Sessions F–P):**
- Tile shadows for regular tiles (locked tiles have shadows, regular don't) → Session J
- Match VFX reorder (beam before "10") → Session L
- Beam opacity reduction → Session L
- Haptic feedback → Session L
- Credits/About section → Session O
- Leaderboard → Session O
- Tutorial popup click-outside bug → Session O

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

### L2 — MakeZen Mode ✓ COMPLETE

**Status:** Fully implemented across Sessions A–E. See "Completed Work" section for session-by-session summary.

**What shipped:** 5-minute timer (300s), locked tile system (merge/gravity/convergence), swap-any mechanic with animated revert, 7-tier difficulty ramp, failed-swap penalties (-3s + multiplier reset), 3 reshuffles max, full UI/results ("Well Done." screen with Zen stats), per-mode high scores, ModeSelect flow.

**Deferred to polish sessions:** Locked tile glow animations (Session N shader), difficulty label toasts, board pressure indicator bar, dark color scheme.

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

### Sprint Execution Order (Sessions F–P)

All MakeZen MVP sessions (A–E) are complete. Remaining work: bug fixes, code hygiene, and Launch Sprint polish layers.

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

Parallel — Music Production (CJ-driven)
  ∥ 4-stem ambient set (MakeZen)
  ∥ 4-stem upbeat set (Arcade)
  ∥ Tuned match SFX (or pitch-shift approach)
```

**Total estimated time: ~13–15 hours across 11 sessions.**

Track 1 (F/G/H) is ~1.5 hours and should be done first — it prevents bugs from compounding and makes every subsequent session cleaner. Track 2 (I/J/L/M) is the core polish that transforms how the game *feels*. Tracks 3–5 can be interleaved based on asset readiness.

---

### Session F — Critical Bug Fixes & Guards ✓ COMPLETE

1. ~~**Fix `StartNewGame()` crash**~~ — Already resolved. `ResetGame()` exists in GridManager (line 2424).
2. ✓ **Double game-over guard** — Added `if (!IsGameActive) return;` at top of `TimeUp()` in GameManager.cs. Prevents double-fire when `OnFailedSwap()` and `Update()` both trigger in the same frame.
3. ✓ **SafeArea duplication** — Added `if (canvasRect.Find("SafeAreaContainer") != null) return;` in `SetupSafeArea()` in SceneFlowManager.cs. Prevents duplicate containers on Awake re-fire.
4. ~~**Hint particle cleanup**~~ — Already safe. Coroutines self-terminate and null-check before accessing destroyed GameObjects.

**Files touched:** GameManager.cs, SceneFlowManager.cs

---

### Session G — Dead Code Purge (~45 min)

**Why next:** Removing ~1,150 lines of noise makes every future session cleaner and reduces cognitive load.

**Delete entire files (12 files):**
- `PlayerInventory.cs`, `CampaignManager.cs`, `RunManager.cs`, `DataLoader.cs`
- `DebugUpgradePanel.cs`, `ShopCard.cs` *(L6 Shop will rebuild from scratch)*
- `UI/ExampleWindow.cs`, `UI/UpgradeConfirmWindow.cs`
- `Data/ArtifactData.cs`, `Data/SnackData.cs`, `Data/UpgradeData.cs`, `Data/UpgradeType.cs`

**Remove dead methods from active files:**
- **GameManager.cs:** `CalculateEnhancedNumberBonus()`, `CalculateZeroTimeBonus()`, `ApplyPostScoringBonuses()`, `CalculateCommonBonuses()`, `GetCurrentWeights()`, `GetCurrentGridSize()`, unused events `OnEnhancedNumberBonus` / `OnTimeBonus`, field `postWinDelay`
- **GridManager.cs:** `tileFallDelay`, `postClearDelay`
- **Tile.cs:** `numberPulseSpeed`, `numberPulseMinScale`, `numberPulseMaxScale`, `numberBrightenAmount`
- **MatchChecker.cs:** `targetSum` (never read)

**Cleanup:** Grep all `.cs` for references to deleted classes, remove orphaned `.meta` files.

**Files touched:** 16+ files (mostly deletions)

---

### Session H — Constants & Code Quality (~30 min)

**Why:** Makes the codebase readable and prevents magic-number drift during polish sessions.

1. **Extract magic numbers into named constants:**
   - GameManager: `MultiplierDrainRate`, `SpeedBonusThreshold`, `TimeBonusPerMatch`, `ZenFailedSwapPenalty`, `ZenMaxReshuffles`, `HotStreakMultiplier`, `HotStreakDuration`
   - GridManager: `TileFallSpeed`, `HintDelay`, `HintRepeatInterval`
   - Tile: `TileBackgroundGrey`, `LockedTileThreshold`
2. **Add mode helper:** `public bool IsZenMode => CurrentMode == GameMode.Zen;` to GameManager. Replace all 8+ inline checks.
3. **Consistent null checking:** Audit UIManager init methods, add null guards for all serialized panel references.

**Files touched:** GameManager.cs, GridManager.cs, Tile.cs, UIManager.cs

---

### Session I — L0: Easing & Motion Overhaul (~1.5 hours)

**Why:** Every animation currently uses linear Lerp. Single highest-impact polish change.

**Part 1: Easing Library** (AnimationUtilities.cs) — Add 5 static functions: `EaseOutCubic`, `EaseInOutCubic`, `EaseOutBack`, `EaseOutElastic`, `EaseInCubic`.

**Part 2: Thread through existing methods** — `PunchScale` (EaseOutBack/Cubic), `PopIn` (EaseOutBack/Cubic), `FloatAndFade` (Cubic), `ScaleIn` (EaseOutBack), `ScaleOut` (EaseInCubic), `DropIn` (EaseOutElastic).

**Part 3: Tile motion** — Tile fall: time-based (0.25s) with EaseOutCubic + landing bounce. Tile swap: EaseInOutCubic + shallow arc. Countdown pop: EaseOutBack/"GO!" EaseOutElastic. Menu title: replace 3 sine waves with single EaseInOutCubic bob.

**Files touched:** AnimationUtilities.cs, GridManager.cs, SceneFlowManager.cs, MainMenuUI.cs

---

### Session J — L1: Tile Visual Overhaul (~1 hour)

1. **Shadows** — Ungated for locked tiles (Session A), but regular tiles still don't have shadows. Remove gate for ALL tiles. Shadow color: `Color.Lerp(NumberColors[value], Color.black, 0.65f)`.
2. **Tinted backgrounds** — `Color.Lerp(Color.white, NumberColors[value], 0.12f)` replacing uniform grey.
3. **Rounded corners** — White 64×64 rounded-rect PNG (~12px radius), 9-slice, assign to tile prefab. *(Unity Editor.)*
4. **Selection feedback** — `PunchScale(1.15f, 0.1f)` on selection. `Handheld.Vibrate()`. Animate deselection.

**Files touched:** Tile.cs (+ Unity Editor for rounded-rect sprite)

---

### Session K — L3: FMOD Migration + Adaptive Audio (~2–3 hours)

**Part 1: FMOD Setup** — Install plugin, create FMOD Studio project, migrate SFX, replace AudioManager internals (keep public API), set up buses (Master/Music/SFX/Voice).

**Part 2: Adaptive Music** — Single multi-track event with parameters: `Intensity` (0→1), `Inactivity` (0→1), `GameMode` (0/1). Layer triggers: Base (always), Rhythm (mult ≥ 1.5), Melody (mult ≥ 2.0), Intensity (hot streak).

**Part 3: Tuned Match SFX** — Pitch via FMOD parameter: 10→root, 20→major 3rd, 30→perfect 5th. Cascade pitch: level 1→1.0, level 2→1.12, level 3→1.25, level 4→1.5.

**Dependency:** Music stems (4 per mode) are CJ-driven parallel work.

**Files touched:** AudioManager.cs, GameManager.cs, FMOD Studio project (new)

---

### Session L — L5: Match VFX Reorder + Haptics (~1 hour)

1. **Reorder sequence:** convergence → "10" popup (PunchScale EaseOutBack) → 0.12s hold → beam (reduced opacity) → particles → avatar → screen shake → haptic
2. **Beam opacity reduction:** glow 0.6→0.35, core 1.0→0.7, sparkles 0.9→0.6
3. **"10" popup enhancement:** overshoot 1.2→1.4, screen shake on pop, scale by match sum (20→1.2×, 30→1.5×)
4. **Haptics:** `Handheld.Vibrate()` on tile selection, match clear, hot streak trigger, game over

**Files touched:** GridManager.cs, GridVFX.cs

---

### Session M — L4: Ambient Life (~45 min)

1. **Ambient particles:** 15–20 particles, 0.12–0.15 alpha, size variation, tinted to board. Hot streak: 30+, warm. Zen: slower drift (12 px/s), cooler.
2. **Idle tile breathing:** `1.0 + sin(Time.time * 0.5 + tileIndex * 0.3) * 0.008` wave across grid, gated behind `!IsSelected && !isAnimating`.
3. **Score environment feedback:** micro-punch ripple on match, all-tiles flash on hot streak start, particles 2× speed on multiplier increase.

**Files touched:** GridVFX.cs, Tile.cs

---

### Session N — Custom Shaders (~1.5 hours)

**Why:** Zero custom shaders currently. Highest visual-bang-for-buck additions.

1. **Locked Tile Glow** — Animated outer glow with tier-based color. Properties: `_GlowColor`, `_GlowIntensity`, `_PulseSpeed`, `_CornerRadius`. Sinusoidal pulse.
2. **Beam Flash** — Single-quad gold gradient beam with UV scrolling. Replace multi-sprite approach.
3. **Hot Streak Overlay** — Fullscreen heat shimmer/wave distortion. Properties: `_Intensity`, `_DistortionAmount`, `_ColorTint`.

All shaders: built-in pipeline, unlit, UI-compatible (`Overlay` or `Transparent` queue).

**Files touched:** New shader files in Assets/Shaders/, GridVFX.cs, Tile.cs, HotStreakEffect.cs

---

### Session O — L7: Credits, Leaderboard, Tutorial (~1 hour)

1. **Credits:** MainMenuUI button → PopupWindow scrollbar mode. "Make10 by CJ Rhone / Wizard Bodega" + attributions.
2. **Leaderboard:** Top 10 per mode, PlayerPrefs JSON, PopupWindow display.
3. **Tutorial:** Add MakeZen explanation slide. Fix click-outside-to-dismiss bug in PopupWindow.

**Files touched:** MainMenuUI.cs, PopupWindow.cs, UIManager.cs

---

### Session P — L6: Shop System & Cosmetics (~3+ hours, largest session)

**Blocker:** Sprite art (~84 images). Can build the system and wire it up with placeholder sprites.

1. **Data model:** `CosmeticData.cs` (ScriptableObject), `CosmeticInventory.cs` (singleton, PlayerPrefs JSON)
2. **Avatar architecture:** Layered UI Image stack replacing single-image AvatarManager
3. **Shop UI:** Category tabs, scrollable grid, locked/owned/equipped states, live preview
4. **BP persistence:** `RunManager.cs` rebuild with `TotalBP` / `SpendableBP` / `SpendBP()`
5. **Pricing:** Common 200–400, Uncommon 500–800, Rare 1000–1500

**Files touched:** New files (CosmeticData.cs, CosmeticInventory.cs, ShopManager.cs, ShopCard.cs), AvatarManager.cs, RunManager.cs (rebuilt), MainMenuUI.cs, SceneFlowManager.cs

---

### Current Codebase State (Post-MakeZen MVP)

**Scripts added during MakeZen:**
- `TileWeightManager.cs` — tile weights, bag system, 7-tier Zen difficulty ramp
- `GridValidation.cs` — match prevention, anti-cascade, consecutive match tracking

**Scripts modified during MakeZen:**
- `Tile.cs` — IsLocked, locked tile rendering, glow ungating, interaction blocking, swap-any input
- `MatchChecker.cs` — locked tile awareness, all-pairs swap validation for Zen
- `GridManager.cs` — ProcessZenMatch(), merge logic, Zen gravity/reshuffle, swap-any + animated revert
- `GameManager.cs` — 5-min timer, failed swap penalty, Zen stats, mode enum
- `SceneFlowManager.cs` — ModeSelect state, Zen navigation routing
- `UIManager.cs` — locked tile counter, Zen results screen, calm timer, nav fixes
- `MainMenuUI.cs` — per-mode high scores

**Future scripts (L6 Shop):** CosmeticData.cs, CosmeticInventory.cs, ShopManager.cs, ShopCard.cs

**Post-L6 game state additions:**
```
+ Shop, Leaderboard, Credits states
+ CosmeticInventory.Instance, ShopManager.Instance singletons
```
