# ✅ PLAN COMPLETE — Arcade Conversion Done

**Status:** All items implemented as of February 2026.

**Summary of changes made:**
- GameManager: Simplified to timer-only rounds, no threshold wins, no boss mode, upgrade queries stubbed
- CampaignManager: Gutted to minimal round counter
- SceneFlowManager: Removed ChillZone/BossFight states, renamed Win→Results, simplified OnGameEnded()
- UIManager: Removed boss HP bar, graduation screen, lose screen; win screen repurposed as results
- ShopManager: Stripped to empty shell (no cards, no purchases)
- GridManager: Progressive tile weights implemented (time-based introduction of 5s, 6s), OnRoundStarted() wired up
- RunManager: Gold stars removed
- PlayerInventory: Gutted to minimal shell (singleton + ClearInventory only)
- Tile.cs: Enhanced glow/upgrade visuals disabled (no upgrades in arcade mode)

**Bug fix:** GridManager.OnRoundStarted() is now called during countdown to properly reset roundStartTime for progressive weights.

---

# Plan: Convert Make10 from Roguelike to Arcade Style

## Design Decisions (from user)
- **Win/Lose:** Timer only. Round ends when time expires. Score = whatever BP you earned. No threshold.
- **Shop:** Keep UI shell, strip all upgrade/snack/artifact purchasing.
- **Stages:** No stages. Tile weights progressively change during gameplay — higher numbers (5, 6, 7) introduced over time.

---

## New Game Flow

```
MainMenu → (Tutorial?) → Countdown → Game → Time's Up → Results → Shop (empty shell) → Next Round → Countdown → ...
```

No "win" or "lose" — every round ends when the timer hits zero. The results screen shows your BP earned that round. The shop exists as a placeholder for future content.

---

## What Gets Removed

### 1. Boss Fight System (across 5 files)
- **CampaignManager.cs**: Remove `StageData` class, `stages` list, all boss methods (`StartBossFight`, `DamageBoss`, `OnBossDefeatedInternal`), boss properties (`IsInBossFight`, `CurrentBossHP`, `MaxBossHP`, `BossHPPercent`), boss events (`OnBossFightStarted`, `OnBossDamaged`, `OnBossDefeated`)
- **GameManager.cs**: Remove `IsBossFight` property, `ActivateBossFight()`, boss damage logic in scoring, F4 debug boss-kill
- **SceneFlowManager.cs**: Remove `BossFight` from GameState enum, `TransitionToBossFightSequence()`, `BossFightCountdownSequence()`
- **UIManager.cs**: Remove boss HP bar fields/methods (`ShowBossUI`, `HideBossUI`, `UpdateBossHP`, `EnsureBossHPPanelExists`, boss event handlers), graduation screen
- **UIStyleGuide.cs**: Remove boss-related style constants if any

### 2. Chill Zone (across 3 files)
- **CampaignManager.cs**: Remove `isInChillZone`, `EnterChillZone()`, `OnChillZoneEntered` event
- **SceneFlowManager.cs**: Remove `ChillZone` from GameState enum, `TransitionToChillZone()`, `OnFightBossPressed()`, chill zone panel creation
- **ShopManager.cs**: Remove chill zone button text/color logic from `UpdateNextRoundButton()`, remove chill zone check in `OnNextRoundPressed()`

### 3. Score Threshold Win Condition (across 3 files)
- **GameManager.cs**: Remove `CurrentRoundThreshold` property, threshold check in `CheckWinCondition()`, threshold check in `TimeUp()` (now timer expiry always ends the round). Remove the concept of "winning" vs "losing" — round just ends.
- **CampaignManager.cs**: Remove `GetCurrentThreshold()`, `roundThresholds` arrays
- **UIManager.cs**: Remove target score text, simplify score progress display (no target, just current score)

### 4. Campaign Stage Progression (across 4 files)
- **CampaignManager.cs**: Gut heavily — remove stage data, stage progression, `AdvanceStage()`, `OnStageChanged` event, `OnCampaignCompleted` event. Keep as a lightweight "round counter" if needed, or fold into RunManager.
- **GridManager.cs**: Remove campaign queries for grid size/maxNumber (hardcode 5×5, manage maxNumber through new progressive weight system)
- **ShopManager.cs**: Remove stage-based filtering of upgrades
- **UIManager.cs**: Remove stage/round campaign display, simplify to just round number

### 5. Gold Stars (across 3 files)
- **RunManager.cs**: Remove `goldStars`, `AddGoldStars()`, `SaveGoldStars()`, `LoadGoldStars()`, `OnGoldStarsChanged`
- **CampaignManager.cs**: Remove `goldStarReward` from stage data
- **UIManager.cs**: Remove gold stars display from graduation screen (screen itself is removed)

### 6. Upgrade/Shop Purchasing (across 7 files)
- **ShopManager.cs**: Remove card spawning, purchase logic, data loading. Keep the panel, title, BP display, and "Next Round" button.
- **ShopCard.cs**: Can leave file in place but no longer instantiated. (Or remove entirely.)
- **UpgradeConfirmWindow.cs**: No longer needed — remove or leave dormant.
- **DebugUpgradePanel.cs**: Remove F1 debug panel (no upgrades to test).
- **PlayerInventory.cs**: Gut all bonus calculation methods. Keep as empty shell or remove entirely. All GameManager queries to PlayerInventory for scoring bonuses get removed.
- **GameManager.cs**: Remove `CacheEffectiveValues()` and all upgrade-related bonus queries during scoring. Use hardcoded base values instead.
- **Tile.cs**: Remove enhanced tile visuals (glow, pulse, shadow) that were upgrade-driven. Simplify `RefreshEnhancedStatus()`.

---

## What Gets Modified

### 7. GameManager.cs — Simplified Round Logic
- Round starts with timer countdown (keep existing timer)
- Scoring stays the same: 10 BP × multiplier per match
- Multiplier/Hot Streak system stays (it's core arcade fun)
- **No win condition** — round ends ONLY when timer hits zero
- `TimeUp()` becomes the sole round-end trigger → always goes to results screen
- Remove the separate win/lose paths — single "Round Over" path
- Keep `OnGameWon`/`OnGameLost` events but repurpose: `TimeUp` fires a single `OnRoundEnded` event (or just always call `OnGameWon` since there's no failure)

### 8. UIManager.cs — Results Screen
- Repurpose win screen as "Round Over" / "Time's Up" results screen
- Show: BP earned this round, time bonus (none since timer expired, unless we add bonus seconds for streaks), multiplier stats
- Remove lose screen entirely (no losing)
- Keep the Balatro-style count-up animation for BP breakdown
- "Continue" button → Shop (empty) → "Next Round"

### 9. SceneFlowManager.cs — Simplified State Machine
**New states:** `Loading, MainMenu, Options, Tutorial1, Tutorial2, Countdown, Game, Results, Shop, Quit`
- Remove: `Win` (rename to `Results`), `ChillZone`, `BossFight`
- `OnGameEnded()` always transitions to Results (no win/lose branching)
- Results → Shop → Game loop

### 10. GridManager.cs — Progressive Tile Weights (NEW FEATURE)
This is the key new mechanic replacing stages:
- **Start of round:** Only tiles 1–4 spawn (maxNumber = 4)
- **As time progresses:** Gradually introduce higher numbers
  - ~20 seconds in: Start spawning 5s (low weight)
  - ~35 seconds in: Start spawning 6s (low weight)
  - ~50 seconds in: Start spawning 7s (low weight, if we want to go that far)
- **Implementation:** Add a time-based or match-count-based system that adjusts `maxNumber` and weights for newly spawned tiles
- **Existing tiles stay** — only new tiles (from cascades/respawns) use updated weights
- This creates natural difficulty ramp within each round
- Could also ramp across rounds (round 2 introduces 5s earlier, etc.)

### 11. ShopManager.cs — Empty Shell
- Keep the shop panel, title, BP display, and "Next Round" button
- Remove card spawning entirely
- Show a placeholder message like "Coming Soon..." or just the empty shop space
- "Next Round" always advances to next round (no chill zone check)

---

## Files Changed Summary

| File | Action | Scope |
|------|--------|-------|
| `CampaignManager.cs` | **Heavy gutting or removal** — replace with lightweight round counter, or fold into RunManager | ~90% removed |
| `GameManager.cs` | Remove boss mode, threshold wins, upgrade queries. Simplify to timer-only rounds | ~30% removed |
| `SceneFlowManager.cs` | Remove ChillZone/BossFight states, simplify transitions | ~25% removed |
| `UIManager.cs` | Remove boss HP bar, graduation screen, campaign display, lose screen. Simplify win→results | ~35% removed |
| `ShopManager.cs` | Remove card spawning/purchasing, chill zone logic. Keep shell | ~60% removed |
| `RunManager.cs` | Remove gold stars | ~15% removed |
| `GridManager.cs` | Remove campaign queries, add progressive tile weight system | Modified |
| `PlayerInventory.cs` | Gut bonus calculations or remove entirely | ~80% removed |
| `Tile.cs` | Remove enhanced visuals (upgrade-driven glow/pulse/shadow) | ~10% removed |
| `ShopCard.cs` | No longer instantiated (leave or remove) | Dormant |
| `UpgradeConfirmWindow.cs` | No longer used (leave or remove) | Dormant |
| `DebugUpgradePanel.cs` | Remove F1 panel | Dormant/removed |

---

## Implementation Order

1. **GameManager** — Remove threshold win condition, boss mode, upgrade queries. Make timer-only.
2. **CampaignManager** — Gut to minimal round counter (or remove and use RunManager).
3. **SceneFlowManager** — Remove ChillZone/BossFight states, simplify flow.
4. **UIManager** — Remove boss UI, graduation, campaign display. Repurpose win→results.
5. **ShopManager** — Strip purchasing, keep shell.
6. **GridManager** — Remove campaign queries, implement progressive tile weights.
7. **RunManager** — Remove gold stars.
8. **PlayerInventory / Tile** — Remove upgrade-driven bonuses and visuals.
9. **Compile & test** — Verify the full game loop works cleanly.

---

## Estimated Scope
- **Lines removed:** ~1000–1500 (mostly deletions)
- **Lines added:** ~50–100 (progressive weight system, placeholder shop text)
- **Files touched:** 9–12
- **Risk:** Medium — removing core flow logic, but mostly deletion work. Progressive tile weights is the only genuinely new code.
