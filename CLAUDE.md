# Claude Notes - Make10 Project

## Project Overview

Make10 is a number puzzle game where players swap tiles to create rows/columns summing to exactly 10. Originally created for Brainless Game Jam 2026.

**Creator:** CJ Rhone / Wizard Bodega

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
