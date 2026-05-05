# Claude Notes - Make10 Project

## Project Overview

Make10 is a number puzzle game where players swap tiles to create rows/columns summing to multiples of 10. Two modes: **Arcade** (60-second sprint) and **MakeZen** (5-minute focused session with locked tiles and board pressure).

**Creator:** CJ Rhone / Wizard Bodega
**Origin:** Brainless Game Jam 2026, converted to arcade in Feb 2026.

### Design Philosophy — MakeZen
MakeZen is a 5-minute math meditation. Where Arcade is a sprint, MakeZen gives players room to breathe. Matched tiles converge into immovable "locked" tiles that accumulate, creating rising pressure until the grid fills or time runs out. Three 5-minute sessions ≈ the research-backed dose for the executive-function benefits Tohoku University's Kawashima/Nouchi work documents.

**Reference prototype:** `MakeZen/make10zen_v6.jsx` — complete React implementation.

---

## Tech Stack & Build

- **Engine:** Unity 6000.3.9f1 (Unity 6)
- **Scene:** `Assets/Scenes/Make10Scene.unity` (single-scene project)
- **Canvas:** 1080×1920 portrait
- **Input:** Unity Input System
- **Run:** open project in Unity, press Play.
- **Mobile build:** see `Make10_Mobile_Build_Guide.docx`.

---

## Folder Map

```
Assets/Scripts/        Gameplay logic (managers, tile, grid, vfx)
Assets/Scripts/UI/     SafeArea, PopupWindow, GlowTextureGenerator, UIStyleGuide
Assets/Scenes/         Make10Scene.unity
Assets/Audio/          SFX + music stems
Assets/particles/      Particle textures
MakeZen/               React prototype reference (make10zen_v6.jsx)
WebBuild/, builds/     Build outputs
```

---

## Script Inventory

### Core Gameplay
| Script | Purpose |
|--------|---------|
| `GameManager.cs` | Game state, scoring, multiplier, hot streak, mode enum |
| `GridManager.cs` | Grid spawning, tile management, cascades, hint system, Zen merge/gravity |
| `TileWeightManager.cs` | Tile weights, Tetris-style bag, progressive difficulty ramp |
| `GridValidation.cs` | Initial-match prevention, anti-cascade, consecutive match tracking |
| `Tile.cs` | Tile behavior, click/swipe, selection state, locked rendering |
| `MatchChecker.cs` | Match detection, sum validation, all-pairs swap validation (Zen) |

### Scene Flow & UI
| Script | Purpose |
|--------|---------|
| `SceneFlowManager.cs` | Scene controller, panel transitions, 9 game states |
| `RunManager.cs` | BP currency persistence (rebuild planned in L6 Shop) |
| `UIManager.cs` | Score/timer/multiplier display, results screen, locked tile counter |
| `MainMenuUI.cs` | Main menu + per-mode high scores |
| `UI/PopupWindow.cs` | Reusable popup with scrollbar/auto-size |

### Audio & VFX
| Script | Purpose |
|--------|---------|
| `AudioManager.cs` | Centralized audio (3 AudioSources). FMOD migration planned (L3). |
| `TenExplosionVFX.cs` | Particle explosion on "10" matches |
| `GridVFX.cs` | Line beams, ambient particles, screen shake |
| `HotStreakEffect.cs` | Fire/flames/embers during hot streak |
| `AvatarManager.cs` | Avatar states (single Image, 6 PNGs). Layered rebuild in L6. |
| `LoadingBarVFX.cs` | Loading screen effects |

### Utilities
| Script | Purpose |
|--------|---------|
| `AnimationUtilities.cs` | Static animation library (PunchScale, PopIn, CountUp, etc.) |
| `UI/GlowTextureGenerator.cs` | Procedural soft glow textures |
| `UI/UIStyleGuide.cs` | UI styling constants |

### Stubs (planned but not implemented)
- `CosmeticData.cs`, `ShopManager.cs` — placeholder shells, filled in during Session P.

---

## Singleton Managers

```
GameManager, SceneFlowManager, UIManager, AudioManager,
RunManager, AvatarManager, TenExplosionVFX, GridVFX,
TileWeightManager, GridValidation
```

---

## Game Flow

```
Loading → MainMenu → ModeSelect → [Arcade | MakeZen]
  Arcade:   Countdown → Game (60s)  → Time's Up → Results
  MakeZen:  Game (300s, locked tiles) → Stillness → Results
Results → [Continue/Again] or [Main Menu]
```

**Game States:** Loading, MainMenu, ModeSelect, Tutorial1, Tutorial2, Countdown, Game, Results, Options, Quit.

---

## Scoring & Multiplier (Core Rules)

### Base Scoring
Lines summing to multiples of 10 score that sum as base BP (10-sum → 10 BP, etc.).

```
PLAYER SWAP MATCHES (cascadeCount == 1):
  lineSum × currentMultiplier
  + speedBonus (+5 BP if solved within 4s)
  + time bonus (per line, Arcade only)
  Bar fills +10 per swap

CASCADE MATCHES (cascadeCount >= 2):
  Flat lineSum BP only — no multiplier, no bar fill, no speed bonus.
  Bar freezes during cascade processing.
```

### Arcade Multiplier Bar (0–100)
- +10 per player swap, −1/sec drain, freezes in cascades.
- Tiers: 0–24 ×1.00 · 25–49 ×1.50 · 50–74 ×2.00 · 75–99 ×2.50 · 100 ×5.00 (Hot Streak).
- Hot Streak: ×5.00 for 15s, rainbow bar, countdown shown in avatar region. Resets bar to 0 on expire.

### MakeZen Scoring
Same base / multiplier mechanics, **except**:
- No timer-based decay.
- Failed swap → multiplier resets to ×1, timer −3s.
- No per-match time bonus (fixed 300s minus penalties).
- Locked tiles with high sums score more base BP per match.

### Star Rating
```
Arcade:   ★ 300 / ★★ 600 / ★★★ 1000 BP
MakeZen:  ★ 500 / ★★ 1000 / ★★★ 2000 BP
```

---

## Grid System

- **Size:** 5×5, tile values 0–7 (weighted), tile bag of 25 (Fisher-Yates).
- **Matching:** Rows/columns summing to any multiple of 10.
- **Anti-cascade:** Single re-roll if a new tile would complete a match.
- **Hint:** 10s inactivity → particle trail, repeats every 3s.

### Progressive Difficulty (solve-based)
5s after 2 solves (→ 0.10), 6s after 5 (→ 0.06), 7s after 8 (→ 0.02). Full ramp at 12 solves; low tiles reduce to 85%.

### Tile Colors
```
0 Grey · 1 Gold · 2 Blue · 3 Green · 4 Coral · 5 Orange
6 Purple · 7 Teal · 8 Pink · 9 Crimson
Background: uniform (0.85, 0.85, 0.85)
```

### Locked Tile Colors (MakeZen)
10 Gold · 20 Purple · 30 Teal · 40 Red · 50 Orange · 60 Blue · 70+ Magenta (each with a glow + darker border).

### MakeZen Grid Behavior
Locked tiles (value ≥ 10) can't be selected/swapped, fall with gravity, count toward sums, and only spawn via match convergence. Reshuffles preserve their position/value.

---

## Key Constants

| Setting | Arcade | MakeZen | Location |
|---------|--------|---------|----------|
| Game Duration | 60s | 300s | GameManager |
| Multiplier System | Bar 0–100 | Bar 0–100 (no drain) | GameManager |
| Failed Swap Penalty | None | −3s + multiplier reset | GameManager |
| Hot Streak | ×5.00 / 15s | ×5.00 / 10s | GameManager |
| Max Reshuffles | Unlimited | 3 | GameManager |
| Tile Bag | 25 | 25 | TileWeightManager |
| Speed Bonus | +5 BP within 4s | +5 BP within 4s | GameManager |
| Time Bonus/Match | +1.5s per line | None | GameManager |

---

## Current State

**Shipped:**
- Arcade mode (60s sprint, full scoring + Hot Streak).
- MakeZen mode (300s, locked tiles, swap-any, 7-tier difficulty ramp, Zen results screen, per-mode high scores).
- Star ratings, hint system, tinted tile numbers, polished countdown/results flow.

**In progress (Launch Sprint, Sessions F–P):**
- Code hygiene + bug fixes (F/G/H — F and G complete).
- Polish layers: easing overhaul (L0/I), tile visuals (L1/J), match VFX reorder (L5/L), ambient life (L4/M), shaders (N).
- FMOD migration + adaptive audio (L3/K) — also resolves current SFX glitching where the time-warning loop conflicts with PlayOneShot calls on a shared AudioSource.

**Planned, not built:**
- Shop & cosmetics (L6/P) — paper-doll avatar layers, BP-driven unlocks. Placeholder stubs `CosmeticData.cs` / `ShopManager.cs` exist; `RunManager` will be rebuilt with `TotalBP`/`SpendableBP`/`SpendBP()`.
- Credits, leaderboard, tutorial fixes (L7/O).

---

## Related Docs

- `PLAN.md` — full launch-sprint plan.
- `POST_MAKEZEN_SESSIONS.md` — per-session work logs (Sessions A–E for MakeZen + later polish).
- `Make10_CodeAudit_Feb28.md` — full code audit.
- `Make10_CodeHealth_2026-03-02.md` — code health follow-up.
- `Make10_Mobile_Build_Guide.docx` — Android/iOS build steps.
- `Make10_Upgrades_Documentation.docx` — legacy upgrades reference.
- `README.md` — gamejam-era, stale; do not treat as current.
