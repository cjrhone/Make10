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

### Shop System
| Script | Purpose |
|--------|---------|
| `ShopManager.cs` | Empty shop shell — BP display, "Next Round" button. No purchases. |

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
ShopManager.Instance        → Empty shop shell, "Next Round" button
AvatarManager.Instance      → Avatar state machine
TenExplosionVFX.Instance    → Particle effects
GridVFX.Instance            → Line sweeps, ambient particles, screen shake
```

---

## Game Flow (Arcade Mode)

```
Loading → MainMenu → (Tutorial?) → Countdown → Game → Time's Up → Results → Shop (empty) → Next Round → Countdown → ...
```

No "win" or "lose" — every round ends when the timer hits zero. The results screen shows BP earned. The shop exists as a placeholder for future content.

### Game States (SceneFlowManager)
- **Loading**: Initialization with progress bar (min 1.5s)
- **MainMenu**: Play, Options, Quit buttons
- **Tutorial1/2**: 2-part onboarding
- **Countdown**: "3...2...1...GO!"
- **Game**: Active gameplay (timer-based rounds)
- **Results**: Score breakdown screen (Balatro-style count-up)
- **Shop**: Empty shell between rounds (BP display + Next Round button)
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
│         [Continue]                  │  ← Leads to shop
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

### Progressive Tile Weights (Arcade Difficulty)
The key arcade mechanic — difficulty ramps within each round:
- **Start of round**: Only tiles 0-4 spawn
- **~20 seconds in**: 5s begin appearing (low initial weight, ramps up)
- **~40 seconds in**: 6s begin appearing (low initial weight, ramps up)
- Configurable via `startingMaxNumber`, `introduce5AtTime`, `introduce6AtTime`, `newNumberInitialWeight`, `weightRampSpeed`
- `OnRoundStarted()` resets the timer each round

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
| Introduce 5s at | 20s | GridManager |
| Introduce 6s at | 40s | GridManager |

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
