# Make10

A fast-paced arcade tile-matching puzzle game where you swap numbered tiles to create rows and columns that sum to exactly **10**.

**Created by:** CJ Rhone / [Wizard Bodega](https://wizardbodega.gg)

**Made for:** Brainless Game Jam 2026

---

## How to Play

1. **Swap tiles** by clicking two adjacent tiles or swiping from one tile to a neighbor
2. **Make 10** — Create a row or column where all numbers sum to exactly 10
3. **Build streaks** — Chain consecutive matches to increase your multiplier
4. **Beat the clock** — Score as much BP as possible before the timer runs out

### Controls

| Input | Action |
|-------|--------|
| Click/Tap | Select a tile, then select an adjacent tile to swap |
| Swipe | Press and drag in any direction to swap with neighbor |
| Click selected tile | Deselect/cancel |

---

## Game Flow

```
Main Menu → Tutorial → Countdown → Game → Time's Up → Results → Shop → Next Round → ...
```

Every round is timer-based — no win or lose, just score as high as you can. The results screen shows your BP breakdown (Balatro-style count-up animation), then you move to the shop (currently an empty shell) and on to the next round.

---

## Scoring System

### Base Scoring
- Each matched line (row or column) = **10 BP × current multiplier**

### Multiplier System
- **2 consecutive matches** activates a 1.25x multiplier
- Each additional match increases multiplier by **0.25x**
- Multiplier caps at **3.0x** before triggering Hot Streak

### Hot Streak Mode
When your multiplier exceeds 3.0x, Hot Streak activates:
- **10 seconds** of boosted scoring
- Fixed **5x multiplier** on all matches
- Main timer pauses during Hot Streak
- Fire particle effects and visual flair

---

## Progressive Difficulty

Each round starts with tiles 0–4 only. As the round progresses:
- **~20 seconds in**: 5s start appearing (low weight, ramping up)
- **~40 seconds in**: 6s start appearing (low weight, ramping up)

This creates a natural difficulty curve within every round.

---

## Features

### Gameplay
- Weighted random tile generation with progressive difficulty
- Cascading matches — tiles fall after clears, creating chain reactions
- Automatic grid reset when no valid moves remain
- Hint system shows valid moves after 10 seconds of inactivity

### Visual Polish
- Color-coded tiles (each number 0–6 has a unique color)
- Smooth swap and fall animations with landing bounce
- "10" convergence animation with sparkles, rings, and text popup
- Line sweep beam effects on row/column matches
- Particle explosion → collection VFX (multiplier scales effects)
- Hot Streak fire particles and color pulse effects
- Screen shake on consecutive matches
- Parallax scrolling background
- Animated avatar character with reactive states

### Audio
- Context-sensitive music (menu, gameplay, win, hot streak)
- Sound effects for all interactions
- Separate volume controls for music, SFX, and voice

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
| `GameManager.cs` | Game state, scoring, multiplier, hot streak, timer-only rounds |
| `GridManager.cs` | Grid spawning, tile swapping, cascades, progressive weights, hints |
| `Tile.cs` | Individual tile behavior and input handling |
| `MatchChecker.cs` | Detects rows/columns summing to 10 |
| `SceneFlowManager.cs` | UI panel transitions and game flow |
| `UIManager.cs` | Score display, timer, multiplier bar, results screen |
| `RunManager.cs` | Per-run BP currency and round tracking |
| `CampaignManager.cs` | Lightweight round counter |
| `ShopManager.cs` | Empty shop shell (BP display + Next Round) |
| `AudioManager.cs` | Music and SFX management |
| `AvatarManager.cs` | Animated character with reactive states |
| `GridVFX.cs` | Line sweeps, ambient particles, screen shake |
| `HotStreakEffect.cs` | Fire particle effects during hot streak |
| `TenExplosionVFX.cs` | Particle explosion on match |

---

## Credits

**Game Design & Development:** CJ Rhone

**Studio:** Wizard Bodega

**Event:** Brainless Game Jam 2026

---

## License

All rights reserved. © 2026 Wizard Bodega
