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
```

---

## Game Flow (Current — Arcade Only)

```
Loading → MainMenu → (Tutorial?) → Countdown → Game → Time's Up → Results → [Continue] → Next Round ...
                                                                    Results → [Main Menu] → MainMenu
```

### Game States (SceneFlowManager)
```
Loading, MainMenu, Tutorial1, Tutorial2, Countdown, Game, Results, Options, Quit
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
★       = 300+ BP
★★      = 600+ BP
★★★     = 1000+ BP
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
3: Green (0.2, 0.7, 0.3)         Background: uniform (0.85, 0.85, 0.85)
4: Coral (0.85, 0.45, 0.35)
```

---

## Key Constants

| Setting | Default | Location |
|---------|---------|----------|
| Game Duration | 60s | GameManager |
| Multiplier Start/Max | ×1.25 / ×3.00 | GameManager |
| Hot Streak | ×5.00, 10s | GameManager |
| Grid Size | 5×5 | GridManager |
| Canvas | 1080×1920 | Canvas |
| Tile Bag Size | 25 | GridManager |
| Speed Bonus | 5 BP within 4s | GameManager |

---

## Known Issues

### Audio Glitching (AudioManager.cs)
All SFX share a single `sfxSource`. The time warning uses `sfxSource.Play()` with `loop = true`, which occupies the source. When match SFX call `PlayOneShot()` on the same source during the danger zone (<10s), sounds drop out or glitch. Root cause: `Play()` and `PlayOneShot()` conflict on the same AudioSource. This will be resolved by the FMOD migration in L3.

### Scoring Opacity
`floor(multiplierTimer)` as a hidden time bonus is opaque to players. `enhancedBonus` is always 0 in arcade mode. Session Time bonus on results screen is always ~60 (1 BP per second × 60s round). These should be reviewed during Zen mode implementation.

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

1. **Zen Mode** — An untimed endless mode that gives players a reason to stay (structural)
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

### L2 — Zen Mode (Endless / Untimed)

**Priority: HIGH — Structural change enabling long play sessions.**

#### Game Design
- **No timer.** Game ends when board has no valid moves (like Threes).
- **Progressive difficulty** via existing solve-based ramp (already performance-based).
- **Multiplier: Option B (No Drain, Manual Reset)** — resets to ×1 on failed swap. Rewards thoughtful play, punishes random guessing.
- **Session length target:** 5-15 min average, 20+ min skilled.

#### Implementation

**GameManager.cs:**
1. Add `GameMode` enum: `{ Arcade, Zen }`
2. Gate timer countdown behind `CurrentMode == GameMode.Arcade`
3. Zen multiplier: no drain, reset on failed swap
4. `ZenGameOver()` parallel to `TimeUp()` — fires when no valid moves
5. `ResetRoundState()` — skip timer setup in Zen mode

**GridManager.cs:**
- After cascade, run solvability check. Arcade: reshuffle. Zen: trigger `ZenGameOver()`.

**SceneFlowManager.cs:**
- Add `GameState.ModeSelect` to enum
- Flow: MainMenu → ModeSelect → [Arcade] → Countdown → Game / [Zen] → Game (no countdown)

**UIManager.cs:**
- Zen: replace timer with "Tiles Cleared" or solve-based "Level" indicator
- Results: "GAME OVER" with Tiles Cleared / Highest Multiplier / Longest Streak / TOTAL
- Separate star thresholds: 500 / 1000 / 2000 BP

**MainMenuUI.cs:** Separate high scores per mode.

**Scope:** Large but modular. Everything is additive.

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

Phase 2 — The Mode
  3. L2: Zen Mode
  4. L7a: Tutorial updates

Phase 3 — The Feel
  5. L3: FMOD migration + adaptive audio
  6. L5: Match VFX reorder + haptics
  7. L4: Ambient life

Phase 4 — The Loop
  8. L6: Shop system + cosmetics
  9. L7b: Credits, leaderboard, final polish

Phase 5 — Music Production (parallel, CJ-driven)
  ∥ 4-stem ambient set (Zen)
  ∥ 4-stem upbeat set (Arcade)
  ∥ Tuned match SFX (or pitch-shift approach)
```

**Phases 1-2 are the minimum viable launch.** Phases 3-4 can be a fast-follow.

---

### Post-Sprint State

**New scripts:** CosmeticData.cs, CosmeticInventory.cs, ShopManager.cs, ShopCard.cs

**Updated singletons:**
```
GameManager.Instance        → + GameMode enum, Zen mode logic
CosmeticInventory.Instance  → NEW: cosmetic unlock/equip persistence
ShopManager.Instance        → NEW: shop UI management
```

**Updated game flow:**
```
Loading → MainMenu → ModeSelect → [Arcade] → Countdown → Game (60s) → Results
                                → [Zen]    → Game (endless) → Game Over → Results
                   → Shop → MainMenu
                   → Leaderboard / Credits / Options → MainMenu
```

**Updated game states:**
```
Loading, MainMenu, ModeSelect, Tutorial1, Tutorial2, Countdown,
Game, Results, Shop, Leaderboard, Options, Credits, Quit
```
