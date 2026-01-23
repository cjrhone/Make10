# Campaign Implementation Plan

This document outlines the full plan for wiring up the campaign system in Make10. The core `CampaignManager.cs` logic is ~80% complete but not integrated with the game flow.

---

## Decisions Made

### Boss Fight Style
- **Simple: Score = Damage** - Normal gameplay, BP earned damages boss
- 60 second timer for boss fights (can be upgraded later)
- No complex attack waves for initial implementation

### Round Completion Flow
- **Hybrid approach**:
  1. Round ends → Show avatar image (based on performance)
  2. Show results breakdown
  3. Show "Continue" button after results
  4. → Shop screen
  5. "Next Round" from shop (or "Chill Zone" button if all rounds complete)
  6. Chill Zone → Boss Fight

### Fail/Retry Behavior
- **Run ends on game over** - Back to main menu, start fresh
- No BP penalty on failed rounds (removed)

### Chill Zone
- **Mandatory break screen** before boss
- Simple "Chill Zone" text with button to start boss when ready
- Music hook for later (field for chill zone music clip)

### Campaign Start
- **Always start fresh** at Stage 1, Round 1
- Save/continue system deferred for later

### Stage 4 (Endless)
- Keep playing until you lose
- No win condition, just survive as long as possible

### UI Display
- Show **both** Stage/Round AND threshold target during gameplay
- Boss HP bar appears **above avatar area** (within vertical viewport)

### Debug Commands
- **F4**: Instantly complete round (reach BP threshold) for quick testing

---

## Stage Configuration (Already in CampaignManager)

```
Stage 1: "The Basics"
  - Grid: 4x4, Max Number: 5
  - Rounds: 3 (thresholds: 100, 250, 500 BP)
  - Boss HP: 1000

Stage 2: "Stepping Up"
  - Grid: 5x5, Max Number: 6
  - Rounds: 4 (thresholds: 300, 600, 900, 1200 BP)
  - Boss HP: 2000

Stage 3: "The Grind"
  - Grid: 5x5, Max Number: 7
  - Rounds: 5 (thresholds: 750, 1000, 1250, 1500, 1750 BP)
  - Boss HP: 3000

Stage 4: "Final Exam" (Endless)
  - Grid: 5x5, Max Number: 7
  - Rounds: Endless (no thresholds)
  - Boss HP: 10000
```

---

## Implementation Phases

### Phase 1: Wire Game Flow to Campaign (~45 min)

**1.1 GameManager Changes**
- Add `isBossFight` flag
- Add `currentRoundThreshold` property (from CampaignManager)
- Modify `TriggerWin()` to call `CampaignManager.OnRoundCompleted()`
- Modify `TriggerLose()` to end the run (back to main menu)
- Add F4 debug key to instantly reach threshold

**1.2 SceneFlowManager Changes**
- Add new game states: `ChillZone`, `BossFight`
- Hook "Play" button to `CampaignManager.StartNewCampaign()`
- Create transitions for chill zone and boss fight

**1.3 ShopManager Changes**
- Change "Next Round" button behavior:
  - If more rounds remaining → advance to next round
  - If all rounds complete → show "Chill Zone" button instead
- Button leads to chill zone, not directly to game

### Phase 2: Campaign UI Elements (~1 hour)

**2.1 In-Game Progress Display (UIManager)**
Add to game UI header:
```
┌─────────────────────────────┐
│ Stage 2 - Round 3/4         │
│ Target: 900 BP              │
└─────────────────────────────┘
```

**2.2 Chill Zone Panel (SceneFlowManager)**
New panel:
```
┌─────────────────────────────┐
│      ☕ CHILL ZONE ☕        │
│                             │
│  [FIGHT BOSS]               │
└─────────────────────────────┘
```
- Simple placeholder text
- Single button to start boss fight
- AudioClip field for future chill music

**2.3 Boss HP Bar (UIManager)**
Position: Above avatar area
```
┌─────────────────────────────┐
│  🎓 THE PROFESSOR           │
│  [████████░░░░] 65%         │
├─────────────────────────────┤
│     [Avatar Area]           │
```

### Phase 3: Boss Fight Mode (~1 hour)

**3.1 GameManager Boss Mode**
- When `isBossFight = true`:
  - Timer = 60 seconds (configurable)
  - BP earned = damage to boss
  - Call `CampaignManager.DamageBoss(bpEarned)` on each score
  - Win condition: Boss HP <= 0
  - Lose condition: Timer runs out

**3.2 Boss Defeat Flow**
```
BossHP <= 0
  → CampaignManager.OnBossDefeated fires
  → Show victory screen (BP reward + Gold Stars)
  → If Stage 4: Campaign Complete screen
  → Else: Advance to next stage → Shop → Round 1
```

**3.3 Boss UI During Fight**
- Hide score progress bar
- Show boss HP bar instead
- Show "BOSS FIGHT" indicator
- Timer still visible

### Phase 4: Dynamic Grid Settings (~20 min)

**4.1 GridManager Changes**
- Add `Initialize(int gridSize, int maxNumber)` method
- Call from GameManager when starting round
- Pull values from `CampaignManager.GetCurrentStageData()`

### Phase 5: Campaign Complete Screen (~30 min)

**5.1 Victory Screen**
```
┌─────────────────────────────┐
│   🎓 GRADUATION DAY! 🎓     │
│                             │
│ You've mastered Make 10!    │
│                             │
│ Total Gold Stars: ⭐⭐⭐⭐⭐   │
│ Final Score: 15,000 BP      │
│                             │
│ [Main Menu]                 │
└─────────────────────────────┘
```

---

## Key Integration Points

### Files to Modify

| File | Changes |
|------|---------|
| `GameManager.cs` | Add boss mode, threshold checking, F4 debug, call CampaignManager |
| `SceneFlowManager.cs` | Add ChillZone/BossFight states, wire Play button to campaign |
| `UIManager.cs` | Add stage/round display, boss HP bar, campaign complete screen |
| `ShopManager.cs` | Change Next Round to check for chill zone trigger |
| `GridManager.cs` | Add dynamic grid size/max number from campaign |
| `CampaignManager.cs` | Minor tweaks, remove penalty code |

### Event Connections

```csharp
// Already defined in CampaignManager, need to subscribe:
OnStageChanged(stage, round)     → UIManager.UpdateStageDisplay()
OnRoundChanged(round)            → UIManager.UpdateRoundDisplay()
OnBossFightStarted()             → UIManager.ShowBossUI()
OnBossDamaged(damage, remaining) → UIManager.UpdateBossHP()
OnBossDefeated(bp, stars)        → Show victory, advance stage
OnChillZoneEntered()             → SceneFlowManager.ShowChillZone()
OnCampaignCompleted()            → Show graduation screen
```

### New Debug Commands

| Key | Action |
|-----|--------|
| F1 | Debug upgrade panel (existing) |
| F2 | Example popup window (existing) |
| F3 | Upgrade confirm window (existing) |
| **F4** | **Complete round instantly (reach BP threshold)** |

---

## Implementation Order (Recommended)

1. **GameManager + CampaignManager wiring** - Core flow
2. **F4 debug command** - Enable quick testing
3. **UIManager stage/round display** - See progress
4. **ShopManager chill zone trigger** - Shop → Chill Zone flow
5. **Chill Zone panel** - Break screen before boss
6. **Boss fight mode** - GameManager boss flag + damage
7. **Boss HP bar UI** - Visual feedback during boss
8. **Dynamic grid settings** - Per-stage grid size
9. **Campaign complete screen** - Victory celebration
10. **Polish & edge cases** - Stage 4 endless, transitions

---

## Estimated Time

- **Core Integration**: ~2-3 hours
- **UI Elements**: ~1-2 hours
- **Boss Fight**: ~1 hour
- **Polish**: ~1 hour
- **Total**: ~5-7 hours

---

## Questions Resolved

| Question | Answer |
|----------|--------|
| Boss fight style | Simple (score = damage) |
| Round fail penalty | Removed - run ends |
| Chill zone | Mandatory, simple text + button |
| Stage 4 endless | Play until lose |
| Boss visuals | Placeholders for now |
| Save system | Deferred |
| Debug testing | F4 to complete round |

---

## Notes for Implementation

- `CampaignManager.cs` already has most logic - focus on **integration**
- Events are already defined - just need to **subscribe** to them
- Boss HP percentage is already calculated: `CampaignManager.BossHPPercent`
- Stage data is hardcoded but complete in `InitializeDefaultStages()`
- Gold Stars tracked in `RunManager` via `AddGoldStars()`
