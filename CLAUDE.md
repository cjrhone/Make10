# Claude Notes - Make10 Project

## Project Overview

Make10 is a number puzzle game where players swap tiles to create rows/columns summing to exactly 10. Originally created for Brainless Game Jam 2026.

**Creator:** CJ Rhone / Wizard Bodega

---

## Brain Points (BP) - Game Currency

**Brain Points (BP)** are the core currency in Make10, used for scoring and purchasing upgrades between runs.

### How BP is Earned

| Source | Formula | Description |
|--------|---------|-------------|
| Matches | 10 BP base | Each row/column summing to 10 |
| Multiplier | Base × multiplier | Streak multiplier increases BP |
| Time Bonus | 1 BP per second | Remaining time at win |

### BP Economy Flow

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   GAMEPLAY      │────▶│   WIN SCREEN    │────▶│  UPGRADE SHOP   │
│                 │     │                 │     │   (Future)      │
│ - Make matches  │     │ - Score: X BP   │     │                 │
│ - Build streaks │     │ - Time: +Y BP   │     │ - Spend BP on   │
│ - Beat timer    │     │ - Multi: ×Z     │     │   upgrades      │
│                 │     │ ─────────────── │     │ - Persist across│
│                 │     │ TOTAL: N BP     │     │   runs          │
└─────────────────┘     └─────────────────┘     └─────────────────┘
```

### Win Screen Breakdown

When player wins, BP is displayed with animated breakdown:
1. **Score** - Base BP earned from matches (left-aligned label, right-aligned value)
2. **Time Bonus** - 1 BP × seconds remaining
3. **Hot Streak** - Max multiplier reached during game
4. **TOTAL** - (Score + Time Bonus) × Hot Streak Multiplier

### Future: Upgrade Shop (To Be Implemented)

BP will be spent between runs on roguelike upgrades:
- **Passive abilities** - Extra time, higher base multiplier
- **Tile modifiers** - Special tiles, wild cards
- **Scoring bonuses** - Combo multipliers, streak rewards

---

## Current Architecture

### Key Files
- `GridManager.cs` - Grid spawning, tile management, dynamic scaling based on container size
- `GameManager.cs` - Game state, scoring, multiplier system
- `Tile.cs` - Individual tile behavior, click/swipe input handling
- `SceneFlowManager.cs` - Panel transitions, loading sequence
- `UIManager.cs` - Score display, timer, UI updates

### Dynamic Grid Scaling
The grid now scales dynamically based on `gridContainer.sizeDelta`. Key settings:
- `referenceContainerSize` (default 550) - baseline for scale calculations
- `scaleFactor = containerWidth / referenceContainerSize`
- All tile sizes, spacing, fonts, and animations scale proportionally

---

## V2 Redesign Vision

### Game Direction Changes

**Removed:**
- Difficulty select screen (Easy/Medium/Hard)
- Fixed time limit (60 seconds)

**New Direction:**
- **Roguelike-style progression** - Score threshold milestones unlock upgrades
- **Endless gameplay** - Play continues until failure condition (TBD)
- **Upgrade system** - Player choices between runs/milestones
- **Dynamic difficulty scaling** - Difficulty increases as player progresses (to be implemented)

### New Game View Layout (Portrait Mode)

```
┌─────────────────────────┐
│    [⚙️]           [🔊]  │
│                         │
│    CHARACTER AVATAR     │  ← TOP SECTION (~40%)
│    (scenic background,  │     - Character portrait
│     birds, decorations) │     - Animated/reactive to gameplay
│                         │     - Settings/sound icons
├─────────────────────────┤
│  SCORE │ TIMER │  x2    │  ← MIDDLE SECTION (stats bar)
│      [████ 250/300]     │     - Score display
│                         │     - Timer or current sum
├─────────────────────────┤     - Multiplier indicator
│                         │     - XP/Progress bar
│   ┌──┬──┬──┬──┐        │
│   ├──┼──┼──┼──┤        │
│   ├──┼──┼──┼──┤        │  ← BOTTOM SECTION (~50%)
│   ├──┼──┼──┼──┤        │     - Gameplay grid (4x5 or 5x5)
│   ├──┼──┼──┼──┤        │     - Thumb-accessible position
│   └──┴──┴──┴──┘        │
│                         │
└─────────────────────────┘
```

### Main Menu
The main menu will have a different layout (to be designed). The above layout is specifically for the **game view** during active gameplay.

### Stats Bar Elements
| Element | Description |
|---------|-------------|
| Score | Current score (e.g., "5.0") |
| Timer/Value | Current selection sum or timer |
| Multiplier | Active multiplier (e.g., "x2") |
| Progress Bar | XP toward next milestone (e.g., "250/300") |

### Character Panel
- Large character portrait with personality
- Scenic background (clouds, birds, environment)
- Character reacts to gameplay events (match, streak, danger)
- Small UI icons in corners (settings, audio)

---

## Implementation Phases

### Phase 1: UI Layout Restructuring ✓
- [x] Reorganize GamePanel into 3 vertical sections
- [x] Top: CharacterPanel (40% - anchors 0.6 to 1.0)
- [x] Middle: StatsPanel (horizontal bar)
- [x] Bottom: GridPanelContainer (50% - anchors 0 to 0.5)

### Phase 2: Stats Bar Implementation ✓
- [x] Create horizontal stats display
- [x] Score, timer, multiplier positioned in stats bar
- [x] Multiplier always visible (shows "x1.00" when inactive)

### Phase 3: Game Flow Changes ✓
- [x] Remove difficulty select screen
- [x] Play button goes directly to game (default Medium difficulty)
- [ ] Score threshold progression (future)
- [ ] Upgrade system (future)

### Phase 4: Character Panel ✓
- [x] CharacterPanel with AvatarImage
- [x] AvatarManager handles reactive states (Struggling, Solve, Scribbling, HotStreak)
- [x] Avatar preserves aspect ratio and scales responsively

### Phase 5: Difficulty Scaling (Future)
- Dynamic difficulty increase during gameplay
- Adjusts tile distribution, spawn rates, etc.
- Triggered by score milestones or time

---

## Ten Explosion VFX System

### Overview
When a "10" match is made, diamond particles explode outward then collect into the ScoreProgressSlider, creating satisfying visual and audio feedback.

### Visual Flow

```
PHASE 1: EXPLOSION (0.3-0.4s)
┌─────────────────────────────┐
│                             │
│        ◇    ◇    ◇         │
│     ◇               ◇      │
│        ◇   10   ◇          │  Particles burst outward
│     ◇               ◇      │  from match center
│        ◇    ◇    ◇         │  (fast → slow, exponential decay)
│                             │
├─────────────────────────────┤
│   [░░░░░░░░░░░░░░░░░░░░]   │  Progress bar (idle)
└─────────────────────────────┘

PHASE 2: PAUSE (0.1s)
┌─────────────────────────────┐
│                             │
│     ◇                 ◇    │
│          ◇       ◇         │
│                             │  Particles hold at peak
│          ◇       ◇         │  (brief moment of anticipation)
│     ◇                 ◇    │
│                             │
├─────────────────────────────┤
│   [░░░░░░░░░░░░░░░░░░░░]   │  Progress bar (idle)
└─────────────────────────────┘

PHASE 3: COLLECTION (0.5-0.6s)
┌─────────────────────────────┐
│                             │
│       ◇ ↘           ↙ ◇    │
│            ↘     ↙         │
│              ↘ ↙           │  Particles curve toward
│           ◇→ ↓ ←◇          │  progress bar
│              ↓             │  (slow → fast, ease-in)
│            ↙   ↘           │
├─────────────────────────────┤
│   [████████░░░░░░░░░░░░]   │  Bar bounces on each impact!
└─────────────────────────────┘
```

### Particle Behavior

| Phase | Duration | Motion | Easing |
|-------|----------|--------|--------|
| Explosion | ~0.35s | Radial outward from center | Fast→Slow (exponential decay) |
| Pause | ~0.1s | Hold at peak position | None |
| Collection | ~0.5s | Curved path to slider | Slow→Fast (ease-in quadratic) |

### Particle Points Formula

Points are calculated from the multiplier, then converted to particles:

```
basePoints = 10
multiplierBonus = floor((multiplier - 1) * 8)
totalPoints = clamp(basePoints + multiplierBonus, 10, 50)

Examples:
┌────────────┬──────────┬──────────────┐
│ Multiplier │ Bonus    │ Total Points │
├────────────┼──────────┼──────────────┤
│ x1.00      │ +0       │ 10           │
│ x1.50      │ +4       │ 14           │
│ x2.00      │ +8       │ 18           │
│ x2.50      │ +12      │ 22           │
│ x3.00      │ +16      │ 26           │
│ x5.00 (HS) │ +32      │ 42           │
│ x6.00+     │ +40      │ 50 (capped)  │
└────────────┴──────────┴──────────────┘
```

### Two-Size Particle System

Particles come in two sizes representing point values:

| Type | Points | Color | Size |
|------|--------|-------|------|
| Small ◇ | 1 point | Gold | 8-12px |
| Big ◆ | 5 points | Purple | 18-22px |

**Conversion Rules:**
- **≤14 points**: All small particles (no big)
- **≥15 points**: Convert excess to big particles

```
if (totalPoints <= 14):
    smallCount = totalPoints
    bigCount = 0
else:
    bigCount = min(floor((totalPoints - 10) / 5), 5)  # max 5 big
    smallCount = totalPoints - (bigCount * 5)
```

**Conversion Examples:**

```
┌──────────┬───────┬───────┬─────────────────┬────────────────┐
│ Points   │ Small │ Big   │ Visual Total    │ Visual Layout  │
├──────────┼───────┼───────┼─────────────────┼────────────────┤
│ 10       │ 10    │ 0     │ 10 particles    │ ◇◇◇◇◇◇◇◇◇◇     │
│ 14       │ 14    │ 0     │ 14 particles    │ ◇◇◇◇◇◇◇◇◇◇◇◇◇◇ │
│ 15       │ 10    │ 1     │ 11 particles    │ ◇◇◇◇◇◇◇◇◇◇ ◆   │
│ 20       │ 10    │ 2     │ 12 particles    │ ◇◇◇◇◇◇◇◇◇◇ ◆◆  │
│ 25       │ 10    │ 3     │ 13 particles    │ ◇◇◇◇◇◇◇◇◇◇ ◆◆◆ │
│ 30       │ 10    │ 4     │ 14 particles    │ ◇◇◇◇◇◇◇◇◇◇ ◆◆◆◆│
│ 35       │ 10    │ 5     │ 15 particles    │ ◇◇◇◇◇◇◇◇◇◇ ◆◆◆◆◆│
│ 50 (max) │ 25    │ 5     │ 30 particles    │ ◇×25 ◆◆◆◆◆     │
└──────────┴───────┴───────┴─────────────────┴────────────────┘
```

### Progress Bar Bounce Feedback

Bounce intensity based on particle type and count:

```
Small particle (◇):
  #1-5:    scale 1.0 → 1.04 → 1.0  (subtle)
  #6-15:   scale 1.0 → 1.06 → 1.0  (medium)
  #16+:    scale 1.0 → 1.08 → 1.0  (strong)

Big particle (◆):
  Always:  scale 1.0 → 1.15 → 1.0  (impactful!)

Bounce duration: 0.08s (snappy)
Small particles staggered ~0.03s apart
Big particles staggered ~0.08s apart (more dramatic)
```

### Audio

- **Small particle impact**: Subtle "tick" sound
- **Big particle impact**: Slightly louder/deeper "thunk" sound
- Pitch varies slightly for variety (±5-10%)
- Volume scaled with SFX settings via AudioManager

### Particle Visual Style

**Small Particles (◇)**
- **Shape**: Diamond (45° rotated square)
- **Color**: Gold glow (`#FFE680` / `rgb(1.0, 0.9, 0.5)`)
- **Size**: 8-12px
- **Rotation**: Spin 120-180°/sec

**Big Particles (◆)**
- **Shape**: Diamond (45° rotated square)
- **Color**: Purple (`#B366FF` / `rgb(0.7, 0.4, 1.0)`)
- **Size**: 18-22px
- **Rotation**: Spin 60-90°/sec (slower, more weighty)

**Both Types:**
- **Alpha**: Full opacity during explosion, slight fade during collection
- **Scale**: Shrink as approaching target (1.0 → 0.6)

### Implementation Files

| File | Changes |
|------|---------|
| `TenExplosionVFX.cs` | **NEW** - Main particle system controller |
| `GridManager.cs` | Call VFX on match, pass origin position + multiplier |
| `UIManager.cs` | Expose ScoreProgressSlider reference, add BounceProgressBar() method |
| `AudioManager.cs` | Add PlayScoreTick() method for particle impact sound |

### Code Reuse

Particle spawning pattern from existing code:
- `AvatarManager.cs` → `SpawnParticle()`, `UpdateParticles()` pattern
- `LoadingBarVFX.cs` → Diamond shape, rotation, color fade
- `GridManager.cs` → `TenEffect()` sparkle spawning

### Mobile Performance Considerations

- Max 30 particles (capped)
- Simple UI Images (no complex shaders)
- Object pooling optional (particles are short-lived)
- Single coroutine manages all particles
- No physics simulation (pure math-based movement)

---

## Technical Notes

### Canvas Reference Resolution
- Currently set to 1080 x 1920 (portrait)
- CanvasScaler using "Scale With Screen Size"

### Grid Sizing
- Grid adapts to container size via `CalculateSizesFromContainer()`
- For new layout, resize gridContainer to fit bottom ~50% of screen

### Audio
- AudioManager singleton handles all music/SFX
- Separate volume controls for music, SFX, voice

---

## Win Screen Score Breakdown

### Two-Column Layout

The win screen uses a clean two-column layout:
- **Left column**: Labels (left-aligned)
- **Right column**: Values (right-aligned)

```
┌─────────────────────────────────────┐
│         YOU ARE A GENIUS!           │
│                                     │
│   Score                    128 BP   │  ← Row 1: count-up animation
│   Time Bonus             + 31 BP    │  ← Row 2: count-up animation
│   Hot Streak               x2.5     │  ← Row 3: instant
│   ──────────────────────────────    │  ← Divider (6px thick)
│   TOTAL                    398 BP   │  ← Row 4: count-up animation
│                                     │
│      [Restart]    [Main Menu]       │
└─────────────────────────────────────┘
```

### UI Structure (Auto-Generated)

```
WinScreen/
└── BreakdownContainer (VerticalLayoutGroup)
    ├── ScoreRow (HorizontalLayoutGroup)
    │   ├── Label (TMP_Text, left-aligned)
    │   └── Value (TMP_Text, right-aligned)
    ├── TimeBonusRow
    ├── HotStreakRow
    ├── Divider (Image, 6px height)
    └── TotalRow
```

### Tweakable Settings (UIManager Inspector)

| Setting | Default | Description |
|---------|---------|-------------|
| `breakdownLineDelay` | 0.3s | Delay between each line appearing |
| `countUpDuration` | 0.5s | How long the number count-up animation takes |
| `timeBonusPerSecond` | 1 | BP awarded per second of remaining time |

### Customizing in Code (UIManager.cs)

| Location | What to Change |
|----------|----------------|
| `CreateBreakdownRow()` | Font size, colors, row height |
| `CreateDivider()` | Divider thickness (default 6px), color |
| `EnsureBreakdownElementsExist()` | Container position, spacing, padding |
| `ShowWinScreenBreakdown()` | Text format strings (e.g., "{0} BP") |

---

## Recent Changes

### 2026-01-21
- Added LoadingBarVFX with math-based procedural effects
- Made loading progress responsive to actual initialization
- Added dynamic grid scaling based on container size
- Fixed LoadingBarVFX auto-find to search children for Slider component
- **V2 Redesign Implementation:**
  - Restructured UI into 3 vertical sections (Character/Stats/Grid)
  - Removed difficulty select - game starts directly with default difficulty
  - Made multiplier display always visible
  - Fixed tile number centering (TextMeshPro alignment + auto-sizing)
  - Character panel with responsive avatar sizing
- **Win Screen Score Breakdown (Balatro-style):**
  - Sequential reveal of Score, Time Bonus, Hot Streak multiplier, and Total
  - Two-column layout: labels left-aligned, values right-aligned
  - Count-up animations for BP values
  - Thicker divider line (6px) between breakdown and total
  - **Brain Points (BP)** introduced as game currency
  - Time bonus: 1 BP per second remaining
  - Tracks max multiplier reached (`GameManager.MaxMultiplierReached`)
  - Auto-generates UI elements at runtime if not assigned in Inspector
