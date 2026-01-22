# Make10

A fast-paced tile-matching puzzle game where you swap numbered tiles to create rows and columns that sum to exactly **10**.

**Created by:** CJ Rhone / [Wizard Bodega](https://wizardbodega.com)

**Made for:** Brainless Game Jam 2026

---

## How to Play

1. **Swap tiles** by clicking two adjacent tiles or swiping from one tile to a neighbor
2. **Make 10** - Create a row or column where all numbers sum to exactly 10
3. **Build streaks** - Chain consecutive matches to increase your multiplier
4. **Progress through milestones** - Reach score thresholds to unlock upgrades

### Controls

| Input | Action |
|-------|--------|
| Click/Tap | Select a tile, then select an adjacent tile to swap |
| Swipe | Press and drag in any direction to swap with neighbor |
| Click selected tile | Deselect/cancel |

---

## V2 Redesign (In Progress)

Make10 is being redesigned with a **roguelike-style progression system**:

### New Direction
- **No difficulty select** - Game starts and scales dynamically
- **Score threshold progression** - Reach milestones to unlock upgrades
- **Endless gameplay** - Play continues with increasing challenge
- **Upgrade system** - Choose enhancements as you progress
- **Dynamic difficulty scaling** - Challenge increases based on your performance

### New Game View Layout

```
┌─────────────────────────┐
│                         │
│    CHARACTER AVATAR     │  ← Top: Character portrait
│    (scenic background)  │     with reactive animations
│                         │
├─────────────────────────┤
│  SCORE │ TIMER │  x2    │  ← Middle: Stats bar
│      [████ 250/300]     │     (score, timer, multiplier, XP)
├─────────────────────────┤
│                         │
│   ┌──┬──┬──┬──┐        │
│   ├──┼──┼──┼──┤        │  ← Bottom: Gameplay grid
│   ├──┼──┼──┼──┤        │     (thumb-accessible)
│   ├──┼──┼──┼──┤        │
│   └──┴──┴──┴──┘        │
│                         │
└─────────────────────────┘
```

---

## Scoring System

### Base Scoring
- Each matched line (row or column) = **10 points × current multiplier**

### Multiplier System
- **2 consecutive matches** activates a 1.25x multiplier
- Each additional match increases multiplier by **0.25x**
- Multiplier caps at **3.0x** before triggering Hot Streak
- Bonus seconds added based on remaining multiplier timer

### Hot Streak Mode
When your multiplier exceeds 3.0x, Hot Streak activates:
- **10 seconds** of boosted scoring
- Fixed **5x multiplier** on all matches
- Main timer pauses during Hot Streak
- Fire particle effects and visual flair

---

## Features

### Gameplay
- Weighted random tile generation
- Cascading matches - tiles fall after clears, creating chain reactions
- Automatic grid reset when no valid moves remain
- Hint system shows valid moves after 10 seconds of inactivity

### Visual Polish
- Color-coded tiles (each number 0-6 has a unique color)
- Smooth swap and fall animations
- "10" effect with glow, sparkles, and expanding rings
- Convergence animation - matched tiles spiral to center
- Score popups floating upward
- Hot Streak fire particles and color pulse effects
- Parallax scrolling background
- Animated avatar character with state changes

### Audio
- Context-sensitive music (menu, gameplay, win, lose, hot streak)
- Sound effects for all interactions
- Separate volume controls for music, SFX, and voice

### Accessibility
- Color-coded values (not relying on numbers alone)
- Clear visual feedback for all interactions
- Multiple input methods (click and swipe)
- Responsive design for various screen sizes

---

## Technical Details

### Platform
- **Engine:** Unity 2022+
- **Build Target:** WebGL (responsive web for mobile/desktop browsers)
- **Architecture:** Single scene (`Make10Scene.unity`)

### Project Structure

```
Make10/
├── Assets/
│   ├── Scripts/          # C# gameplay scripts
│   ├── Scenes/           # Make10Scene.unity
│   ├── Prefabs/          # Tile.prefab, ScorePopup.prefab
│   ├── Images/           # Avatar sprites
│   ├── Audio/            # Music & SFX
│   ├── Materials/        # Shaders & visual effects
│   └── TextMesh Pro/     # Font resources
├── Packages/             # Unity packages
├── ProjectSettings/      # Unity configuration
└── Builds/               # WebGL output
```

### Core Scripts

| Script | Purpose |
|--------|---------|
| `GameManager.cs` | Game state, scoring, multiplier, win/lose conditions |
| `GridManager.cs` | Grid spawning, tile swapping, cascades, hints |
| `Tile.cs` | Individual tile behavior and input handling |
| `MatchChecker.cs` | Detects rows/columns summing to 10 |
| `SceneFlowManager.cs` | UI panel transitions and game flow |
| `UIManager.cs` | Score display, timer, multiplier bar, popups |
| `AvatarManager.cs` | Animated character with reactive states |
| `HotStreakEffect.cs` | Fire particle effects during hot streak |
| `AudioManager.cs` | Music and SFX management |

---

## Game Flow (V2)

```
Loading Screen
      ↓
Main Menu
      ↓
Gameplay (Endless with milestones)
      ↓
    [Milestone reached → Choose upgrade]
      ↓
    [Continue playing...]
      ↓
Game Over
      ↓
Results / Stats
      ↓
Return to Main Menu
```

---

## Credits

**Game Design & Development:** CJ Rhone

**Studio:** Wizard Bodega

**Event:** Brainless Game Jam 2026

---

## License

All rights reserved. © 2026 Wizard Bodega
