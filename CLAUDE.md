# Claude Notes - Make10 Project

## Project Overview

Make10 is a roguelike-style number puzzle game where players swap tiles to create rows/columns summing to exactly 10. Originally created for Brainless Game Jam 2026.

**Creator:** CJ Rhone / Wizard Bodega

---

## Script Inventory (23 Files)

### Core Game Logic
| Script | Purpose |
|--------|---------|
| `GameManager.cs` | Game state, scoring, multiplier system, hot streak mode, boss fight mode, win/lose conditions |
| `GridManager.cs` | Grid spawning, tile management, cascade matching, dynamic scaling from campaign, hint system |
| `Tile.cs` | Individual tile behavior, click/swipe input, selection state, enhanced visuals |
| `MatchChecker.cs` | Match detection, row/column sum validation, solvability checks |
| `CampaignManager.cs` | Stage/round progression, boss fights, campaign flow, threshold tracking |

### Scene Flow & UI
| Script | Purpose |
|--------|---------|
| `SceneFlowManager.cs` | Master scene controller, panel transitions (9 game states), countdown |
| `RunManager.cs` | Persistent run state: BP currency, round progression |
| `UIManager.cs` | Score, timer, multiplier display, win/lose screens, Balatro-style breakdown |
| `MainMenuUI.cs` | Main menu button handlers |
| `PopupWindow.cs` | Reusable popup system with scrollbar and auto-size modes |
| `UpgradeConfirmWindow.cs` | Fancy upgrade detail popup with effects breakdown |

### Shop System
| Script | Purpose |
|--------|---------|
| `ShopManager.cs` | Shop UI, pyramid layout, real data loading, purchase flow |
| `ShopCard.cs` | Individual cards with type badges, color coding, purchase animations |

### Audio & VFX
| Script | Purpose |
|--------|---------|
| `AudioManager.cs` | Centralized audio (music, SFX, voice), volume persistence |
| `TenExplosionVFX.cs` | Particle explosion on "10" matches, soft glow particles |
| `HotStreakEffect.cs` | Fire effects, flames, embers during hot streak mode |
| `AvatarManager.cs` | Character avatar states and animations |
| `LoadingBarVFX.cs` | Loading screen procedural effects |

### Utilities
| Script | Purpose |
|--------|---------|
| `AnimationUtilities.cs` | Static animation library (PunchScale, PopIn, CountUp, etc.) |
| `ParallaxBackground.cs` | Parallax scrolling for backgrounds |
| `TutorialDemoWidget.cs` | Tutorial content helper |
| `GlowTextureGenerator.cs` | Procedural soft glow texture generation (circular & diamond) |
| `UIStyleGuide.cs` | Centralized UI styling constants and window sizes |

---

## Singleton Managers

```
GameManager.Instance        → Game state, scoring, multiplier, hot streak, boss fight mode
SceneFlowManager.Instance   → Scene transitions, 11 game states (includes ChillZone, BossFight)
UIManager.Instance          → UI updates, win/lose screens, campaign display, boss HP bar
AudioManager.Instance       → Audio playback, volume control
RunManager.Instance         → BP currency, round progression
CampaignManager.Instance    → Stage/round progression, boss fights, campaign flow
ShopManager.Instance        → Shop UI, card purchases, chill zone trigger
AvatarManager.Instance      → Avatar state machine
TenExplosionVFX.Instance    → Particle effects
```

---

## Game Flow

```
Loading → MainMenu → Tutorials → Countdown → Game → Win → Shop
                                              ↓                ↓
                                            Lose          [More Rounds?]
                                              ↓                 ↓ NO
                                          MainMenu      ChillZone → BossFight
                                                              ↓
                                                        [Boss Defeated?]
                                                         YES ↓   ↓ NO
                                                      NextStage  Lose
```

### Game States (SceneFlowManager)
- **Loading**: Initialization with progress bar (min 1.5s)
- **MainMenu**: Play, Options, Quit buttons
- **Tutorial1/2**: 2-part onboarding
- **Countdown**: "3...2...1...GO!" (or "3...2...1...FIGHT!" for boss)
- **Game**: Active gameplay (normal rounds)
- **Win**: Score breakdown screen
- **Shop**: Between-round upgrades (6 cards in pyramid)
- **ChillZone**: Mandatory break before boss fight
- **BossFight**: Timed boss battle (score = damage)
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

### Layout (Inverted Pyramid)

```
┌──────────────────────────────────────┐
│              SHOP                    │
│                                      │
│       [Premium 1]  [Premium 2]       │  ← Top: 2 rare upgrades
│                                      │
│       [Standard 1] [Standard 2]      │  ← Middle: 2 standard upgrades
│                                      │
│         [Snack 1]  [Snack 2]         │  ← Bottom: 2 snacks
│                                      │
│   BP: 250              [NEXT ROUND]  │
└──────────────────────────────────────┘
```

### Card Features
- **Display**: Title, description, cost, type badge (color-coded)
- **Animation**: Subtle floating motion (1.2 speed, 4px amplitude)
- **Interaction**: Click → UpgradeConfirmWindow (fancy popup) → Purchase
- **Purchase**: Deducts BP, card disappears with scale-up animation
- **Color Borders**: Each upgrade type has a distinct border color

### Configuration (ShopManager Inspector)
- `upgradeCardSize`: Upgrade card dimensions (default 380×520)
- `snackCardSize`: Snack card dimensions (default 340×460)
- `topRowUpgrades`: Premium upgrade count (default 2)
- `middleRowUpgrades`: Standard upgrade count (default 2)
- `bottomRowSnacks`: Snack count (default 2)
- `rowSpacing`: Vertical spacing between rows
- `cardSpacingHorizontal`: Horizontal spacing between cards
- `shopMusic`: Custom music clip (falls back to menu music)

### UpgradeConfirmWindow
Fancy auto-sizing popup showing:
- Large upgrade icon (color-coded placeholder if no sprite)
- Upgrade name and type badge
- Full description text
- Effects breakdown
- Cost display
- Current BP balance
- Confirm/Cancel buttons

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

### Enhanced Tile Visuals

When a tile's number has an "Enhanced Number" upgrade, it displays special effects:

**Visual Components:**
1. **Soft Radial Glow** - Colored glow behind the tile matching the number's color
2. **Number Pulse** - Text scales between 1.0→1.15 with slight brightening
3. **Drop Shadow** - Semi-transparent shadow offset (3px, -3px) for 3D depth

**Animation Settings (Tile.cs Inspector):**
| Setting | Default | Description |
|---------|---------|-------------|
| glowPulseSpeed | 2.0 | Glow alpha oscillation speed |
| glowMinAlpha | 0.3 | Minimum glow opacity |
| glowMaxAlpha | 0.7 | Maximum glow opacity |
| glowSize | 1.3 | Glow size relative to tile |
| numberPulseSpeed | 3.0 | Number scale oscillation speed |
| numberPulseMinScale | 1.0 | Minimum number scale |
| numberPulseMaxScale | 1.15 | Maximum number scale |
| numberBrightenAmount | 0.3 | How much to brighten toward white |
| shadowOffset | (3, -3) | Drop shadow position offset |
| shadowColor | (0,0,0,0.5) | Shadow color with alpha |
| shadowSoftness | 0.5 | Font dilation for soft shadow |

**Refresh Methods:**
```csharp
tile.RefreshEnhancedStatus()     // Update single tile
Tile.RefreshAllEnhancedStatus()  // Update all tiles (call after upgrade purchase)
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

## GlowTextureGenerator Utility

Static utility class (`GlowTextureGenerator.cs`) for creating procedural soft glow textures at runtime.

### Available Glow Types

**Circular Glow** - Radial falloff from center (for tile glows, impact flashes)
```csharp
Sprite sprite = GlowTextureGenerator.GetCircularGlowSprite(size: 64, falloffPower: 2f);
GlowTextureGenerator.ApplyCircularGlow(image, textureSize: 64, falloffPower: 1.5f);
```

**Diamond Glow** - Manhattan distance falloff (for rotated square particles)
```csharp
Sprite sprite = GlowTextureGenerator.GetDiamondGlowSprite(size: 64, falloffPower: 2f);
GlowTextureGenerator.ApplyDiamondGlow(image, textureSize: 64, falloffPower: 1.8f);
```

### Parameters
| Parameter | Description |
|-----------|-------------|
| size | Texture resolution in pixels (default 64) |
| falloffPower | How quickly glow fades (1.0 = linear, 2.0+ = sharper edge) |

### Caching
- Textures are cached by size and falloff power
- Avoids regenerating identical textures
- Call `GlowTextureGenerator.ClearCache()` on scene unload if needed

### Usage in Make10
- **Tile.cs**: Circular glow (falloff 1.5) for enhanced number tiles
- **TenExplosionVFX.cs**: Diamond glow (falloff 1.8) for particles, circular (falloff 1.2) for impact flash

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

### 2026-01-23
- **Campaign System Implementation:**
  - `CampaignManager.cs` - stage/round progression, boss fights
  - 4 stages with increasing difficulty (grid size, max numbers, BP thresholds)
  - Boss fight mode with timed encounters (score = damage)
  - Chill Zone break screen before boss fights
  - Dynamic grid sizing from campaign stage settings
  - F4 debug key to instantly complete rounds/kill bosses
  - Shop button changes to "☕ CHILL ZONE" when all rounds complete

- **UIManager Campaign Updates:**
  - Stage/Round display during gameplay
  - Target BP threshold display
  - Boss HP bar (appears during boss fights)
  - Campaign event subscriptions

- **SceneFlowManager States:**
  - Added ChillZone state with auto-generated UI
  - Added BossFight state with boss countdown sequence
  - Campaign integration in Play sequence

- **Enhanced Tile Visual System:**
  - Soft radial glow effect behind enhanced number tiles (using procedural texture)
  - Number text pulse animation (scale 1.0→1.15) with brightness increase
  - Drop shadow effect for enhanced numbers (gives 3D "pop" effect)
  - `GlowTextureGenerator.cs` utility for procedural glow textures
  - Auto-refresh when upgrades are purchased mid-game

- **Shop System Overhaul:**
  - **Inverted Pyramid Layout**: 6 cards total (2 top + 2 middle + 2 bottom snacks)
  - Larger card sizes for better readability
  - Manual positioning instead of layout group (pyramid formation)
  - Dedicated snack row at bottom
  - `UpgradeConfirmWindow.cs` - fancy auto-sizing popup for upgrade details

- **Upgrade Type Color Coding:**
  - EnhancedNumber: Gold (#FFD933)
  - Multiplier: Purple (#B34DE6)
  - Time: Cyan (#4DD9F2)
  - TileWeight: Green (#4DCC66)
  - Combo: Orange (#FF8026)
  - RiskReward: Red (#F24040)
  - Information: Light Blue (#80B3F2)
  - Defensive: Teal (#66A680)
  - BossFight: Crimson (#B32633)
  - Special: Pink/Magenta (#F266B3)
  - Snacks: Teal/Mint (#33BFA6)

- **Particle VFX Improvements:**
  - Soft diamond-shaped glow textures for particles
  - Soft circular glow for impact flashes
  - Better visual polish on explosion effects

- **PopupWindow System:**
  - Added scrollbar support with visible drag handle
  - Auto-size mode for content-fitting windows
  - Configurable scrollbar enable/disable

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

## Campaign System

### Stage Progression

The game features a 4-stage campaign with rounds leading to boss fights:

```
Stage 1: "The Basics" (4x4 grid, max number 5)
├── Round 1: 100 BP threshold
├── Round 2: 250 BP threshold
├── Round 3: 500 BP threshold
├── Chill Zone (break)
└── Boss: 1000 HP

Stage 2: "Stepping Up" (5x5 grid, max number 6)
├── Rounds 1-4: 300/600/900/1200 BP
├── Chill Zone
└── Boss: 2000 HP

Stage 3: "The Grind" (5x5 grid, max number 7)
├── Rounds 1-5: 750/1000/1250/1500/1750 BP
├── Chill Zone
└── Boss: 3000 HP

Stage 4: "Final Exam" (5x5 grid, max number 7)
├── Endless Mode (survive as long as possible)
└── Boss: 10000 HP
```

### Boss Fight Mode

- **Duration**: 60 seconds (configurable)
- **Scoring**: BP earned = damage to boss
- **Win Condition**: Boss HP reaches 0
- **Lose Condition**: Timer runs out
- **UI**: Boss HP bar displays above avatar area

### Campaign Manager Events
```csharp
OnStageChanged(int stage, int round)
OnRoundChanged(int round)
OnBossFightStarted()
OnBossDamaged(int damage, int remainingHP)
OnBossDefeated(int bpReward, int goldStars)
OnChillZoneEntered()
OnCampaignCompleted()
```

### Chill Zone

A mandatory break screen before boss fights with:
- Relaxed atmosphere (optional chill zone music)
- "FIGHT BOSS" button to start boss fight
- Lets players prepare mentally

### Debug Commands

| Key | Action |
|-----|--------|
| F1 | Debug upgrade panel |
| F4 | Complete round instantly (reach BP threshold or kill boss) |

---

## Future Plans

- **Campaign Polish**: Victory screen, stage transition animations
- **Save System**: Persist progress between sessions
- **Character Selection**: Multiple avatars with different abilities
- **More Snacks**: Expand consumable item variety
- **Artifact System**: Rare passive items with powerful effects
- **Boss Visuals**: Custom boss sprites and animations
