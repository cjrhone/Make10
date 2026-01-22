# Claude Notes - Make10 Project

## Project Overview

Make10 is a roguelike-style number puzzle game where players swap tiles to create rows/columns summing to exactly 10. Originally created for Brainless Game Jam 2026.

**Creator:** CJ Rhone / Wizard Bodega

---

## Script Inventory (18 Files)

### Core Game Logic
| Script | Purpose |
|--------|---------|
| `GameManager.cs` | Game state, scoring, multiplier system, hot streak mode, win/lose conditions |
| `GridManager.cs` | Grid spawning, tile management, cascade matching, dynamic scaling, hint system |
| `Tile.cs` | Individual tile behavior, click/swipe input, selection state, values 0-6 |
| `MatchChecker.cs` | Match detection, row/column sum validation, solvability checks |

### Scene Flow & UI
| Script | Purpose |
|--------|---------|
| `SceneFlowManager.cs` | Master scene controller, panel transitions (9 game states), countdown |
| `RunManager.cs` | Persistent run state: BP currency, round progression |
| `UIManager.cs` | Score, timer, multiplier display, win/lose screens, Balatro-style breakdown |
| `MainMenuUI.cs` | Main menu button handlers |

### Shop System
| Script | Purpose |
|--------|---------|
| `ShopManager.cs` | Shop UI, card management, BP display, purchase confirmation popup |
| `ShopCard.cs` | Individual upgrade cards with hover/purchase animations |

### Audio & VFX
| Script | Purpose |
|--------|---------|
| `AudioManager.cs` | Centralized audio (music, SFX, voice), volume persistence |
| `TenExplosionVFX.cs` | Particle explosion on "10" matches, collection into progress bar |
| `HotStreakEffect.cs` | Fire effects, flames, embers during hot streak mode |
| `AvatarManager.cs` | Character avatar states and animations |
| `LoadingBarVFX.cs` | Loading screen procedural effects |

### Utilities
| Script | Purpose |
|--------|---------|
| `AnimationUtilities.cs` | Static animation library (PunchScale, PopIn, CountUp, etc.) |
| `ParallaxBackground.cs` | Parallax scrolling for backgrounds |
| `TutorialDemoWidget.cs` | Tutorial content helper |

---

## Singleton Managers

```
GameManager.Instance        → Game state, scoring, multiplier, hot streak
SceneFlowManager.Instance   → Scene transitions, 9 game states
UIManager.Instance          → UI updates, win/lose screens
AudioManager.Instance       → Audio playback, volume control
RunManager.Instance         → BP currency, round progression
ShopManager.Instance        → Shop UI, card purchases
AvatarManager.Instance      → Avatar state machine
TenExplosionVFX.Instance    → Particle effects
```

---

## Game Flow

```
Loading → MainMenu → Tutorials → Countdown → Game → Win → Shop → Game (next round)
                                              ↓
                                            Lose → Results → MainMenu
```

### Game States (SceneFlowManager)
- **Loading**: Initialization with progress bar (min 1.5s)
- **MainMenu**: Play, Options, Quit buttons
- **Tutorial1/2**: 2-part onboarding
- **Countdown**: "3...2...1...GO!"
- **Game**: Active gameplay
- **Win**: Score breakdown screen
- **Shop**: Between-round upgrades
- **Options**: Settings overlay
- **Quit**: Exit confirmation

---

## Brain Points (BP) - Game Currency

### How BP is Earned

| Source | Formula | Description |
|--------|---------|-------------|
| Matches | 10 BP × multiplier | Each row/column summing to 10 |
| Time Bonus | 1 BP per second | Remaining time at win |
| Hot Streak | ×5 multiplier | During hot streak mode |

### BP Flow

```
GAMEPLAY → WIN SCREEN → SHOP → NEXT ROUND
   │           │          │         │
   │     Calculate:       │    Spend BP on
   │     - Score          │    upgrade cards
   │     - Time Bonus     │         │
   │     - × Multiplier   │         ↓
   │           │          │    RunManager.SpendBP()
   │           ↓          │
   │     RunManager.AddBP()
   │           │
   └───────────┴──────────────────────────────────→ Persists across rounds
```

### Win Screen Breakdown (Balatro-style)

```
┌─────────────────────────────────────┐
│         YOU ARE A GENIUS!           │
│                                     │
│   Score                    128 BP   │  ← Count-up animation
│   Time Bonus             + 31 BP    │  ← Count-up animation
│   Hot Streak               x2.5     │  ← Instant
│   ──────────────────────────────    │
│   TOTAL                    398 BP   │  ← Count-up animation
│                                     │
│         [Continue]                  │  ← Leads to shop
└─────────────────────────────────────┘
```

---

## Shop System

### Layout

```
┌──────────────────────────────────────┐
│              SHOP                    │
│                                      │
│    [Card 1]  [Card 2]  [Card 3]     │  ← Floating animation
│                                      │
│   BP: 250                            │  ← Bottom-left, count-up
│                                      │
│           [NEXT ROUND]               │
└──────────────────────────────────────┘
```

### Card Features
- **Display**: Title, description, cost (in gold text)
- **Animation**: Subtle floating motion (1.2 speed, 4px amplitude)
- **Interaction**: Click → Confirmation popup → Purchase
- **Purchase**: Deducts BP, card disappears with scale-up animation
- **Positioning**: Cards hold position when one is removed (layout frozen after spawn)

### Configuration (ShopManager Inspector)
- `cardSize`: Card dimensions (default 520×760)
- `shopMusic`: Custom music clip (falls back to menu music)
- `cardCount`: Number of cards to spawn
- `cardSpawnDelay`: Stagger between card appearances

### Placeholder Cards
| Title | Description | Cost |
|-------|-------------|------|
| Power Up | Increase your base score | 50 BP |
| Time Boost | Add extra seconds to the clock | 75 BP |
| Multiplier | Start with a higher multiplier | 100 BP |

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

### Win Condition
- Score **100 BP** before time expires
- Time limit: 60 seconds

---

## Grid System

### Configuration
- **Size**: 5×5 grid
- **Tile Values**: 0-6 with weighted distribution
- **Matching**: Rows/columns summing to exactly 10
- **Cascade**: Tiles fall after match, new tiles spawn

### Dynamic Scaling
```
Reference Size: 550px
Scale Factor = containerWidth / 550
All elements scale proportionally
```

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

## Ten Explosion VFX

### Three-Phase Animation

```
PHASE 1: EXPLOSION (0.35s)
Particles burst outward from match center
Motion: Fast → Slow (exponential decay)

PHASE 2: PAUSE (0.1s)
Particles hold at peak position
Brief anticipation moment

PHASE 3: COLLECTION (0.5s)
Particles curve toward progress bar
Motion: Slow → Fast (ease-in quadratic)
Impact: Bar bounces, score increments
```

### Particle Types
| Type | Points | Color | Size |
|------|--------|-------|------|
| Small ◇ | 1 pt | Gold | 8-12px |
| Big ◆ | 5 pts | Purple | 18-22px |

### Points to Particles
- ≤14 points: All small particles
- ≥15 points: Mix of small + big (max 5 big)

---

## Audio System

### Audio Sources (3 independent)
1. **musicSource**: Background music (loops)
2. **sfxSource**: Sound effects
3. **voiceSource**: Voice/UI feedback

### Music Tracks
- MenuMusic, GameMusic, WinMusic, LoseMusic, HotStreakMusic
- Shop uses configurable `shopMusic` or falls back to menu music

### Volume Persistence
- Saved in PlayerPrefs: "MusicVolume", "SFXVolume", "VoiceVolume"
- Auto-reset if accidentally muted (≤0.01)

---

## UI Layout (Game View)

```
┌─────────────────────────┐
│    [⚙️]           [🔊]  │
│                         │
│    CHARACTER AVATAR     │  ← TOP (40%)
│    (reactive states)    │
│                         │
├─────────────────────────┤
│ ROUND │ SCORE │ x2.00   │  ← MIDDLE (Stats bar)
│      [████ 50/100]      │
├─────────────────────────┤
│                         │
│   ┌──┬──┬──┬──┬──┐     │  ← BOTTOM (50%)
│   │  │  │  │  │  │     │
│   ├──┼──┼──┼──┼──┤     │
│   │  │  │  │  │  │     │
│   └──┴──┴──┴──┴──┘     │
│                         │
└─────────────────────────┘
```

---

## Key Constants

| Setting | Default | Location |
|---------|---------|----------|
| Win Score | 100 BP | GameManager |
| Game Duration | 60s | GameManager |
| Multiplier Start | ×1.25 | GameManager |
| Multiplier Max | ×3.00 | GameManager |
| Multiplier Increment | +0.25 | GameManager |
| Hot Streak Multiplier | ×5.00 | GameManager |
| Hot Streak Duration | 10s | GameManager |
| Grid Size | 5×5 | GameManager |
| Card Size | 520×760 | ShopManager |
| Canvas Resolution | 1080×1920 | Canvas |

---

## Event System

### GameManager Events
```csharp
OnScoreChanged(int newScore, int delta)
OnTimeChanged(float timeRemaining)
OnMultiplierChanged(bool active, float mult, float timer)
OnHotStreakStarted()
OnHotStreakEnded()
OnGameWon()
OnGameLost()
```

### RunManager Events
```csharp
OnBPChanged(int currentBP)
OnRoundChanged(int roundNumber)
OnRunStarted()
OnRunEnded()
```

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

## Recent Changes

### 2026-01-22
- **Shop System Implementation:**
  - ShopManager with auto-generated UI
  - ShopCard with floating animation and purchase flow
  - Purchase confirmation popup (Confirm/Cancel)
  - Cards display cost in gold text
  - Cards hold position when one is purchased
  - Configurable shop music field
  - Music stops on "Next Round" click
  - BP display moved to bottom-left

- **Run System:**
  - RunManager for BP and round tracking
  - Continue button on win screen → shop transition
  - Round display in game UI
  - BP persists across rounds within a run

- **Bug Fixes:**
  - Fixed card stacking (wait for layout before capturing position)
  - Fixed card centering (disable ContentSizeFitter after spawn)
  - Text auto-sizing for different card sizes

### 2026-01-21
- Win Screen Score Breakdown (Balatro-style)
- Ten Explosion VFX with particle collection
- Hot Streak fire effects
- V2 UI layout (Character/Stats/Grid sections)
- Dynamic grid scaling
- Brain Points (BP) currency introduced

---

## Future Plans

- **Roguelike Progression**: Score milestones unlock permanent upgrades
- **Real Upgrade Cards**: Actual effects (time boost, multiplier start, etc.)
- **Dynamic Difficulty**: Tile distribution changes per round
- **Character Selection**: Multiple avatars with different abilities
- **Save System**: Persist progress between sessions
