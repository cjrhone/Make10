# Make10

A fast-paced tile-matching puzzle game where you swap numbered tiles to create rows and columns that sum to exactly **10**.

**Created by:** CJ Rhone / [Wizard Bodega](https://wizardbodega.com)

**Made for:** Brainless Game Jam 2026

---

## How to Play

1. **Swap tiles** by clicking two adjacent tiles or swiping from one tile to a neighbor
2. **Make 10** - Create a row or column where all numbers sum to exactly 10
3. **Beat the clock** - Reach the target score before 60 seconds runs out
4. **Chain matches** - Build your multiplier with consecutive matches for higher scores

### Controls

| Input | Action |
|-------|--------|
| Click/Tap | Select a tile, then select an adjacent tile to swap |
| Swipe | Press and drag in any direction to swap with neighbor |
| Click selected tile | Deselect/cancel |

---

## Game Modes

| Difficulty | Grid Size | Target Score | Description |
|------------|-----------|--------------|-------------|
| **Easy** | 4×4 | 250 pts | Fewer zeros, balanced tile distribution |
| **Medium** | 5×5 | 300 pts | Standard challenge |
| **Hard** | 5×5 | 500 pts | More zeros and high-value tiles |

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
- Weighted random tile generation based on difficulty
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
│   ├── Scripts/          # 15 C# gameplay scripts
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
| `MainMenuUI.cs` | Title screen animations |
| `TutorialDemoWidget.cs` | Tutorial demonstration system |
| `AnimationUtilities.cs` | Shared animation helpers |
| `AspectRatioEnforcer.cs` | Responsive layout handling |
| `CameraLetterbox.cs` | Letterbox for extreme aspect ratios |
| `ParallaxBackground.cs` | Scrolling background layers |

---

## Game Flow

```
Loading Screen
      ↓
Main Menu
      ↓
Difficulty Select (Easy / Medium / Hard)
      ↓
Tutorial (Optional)
      ↓
Countdown (3... 2... 1... Go!)
      ↓
Gameplay (60 seconds)
      ↓
Results Screen (Win / Lose)
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
