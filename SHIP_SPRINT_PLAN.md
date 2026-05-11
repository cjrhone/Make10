# Make10 — Ship Sprint Plan v2 (1.0.1)

**Sprint goal:** Clear iPad rejection + unified swap-revert behavior + tablet-aware layout + UI polish from existing Claude Design mockups + tutorial + haptics. Ship 1.0.1, return attention to HoF.

**Updated:** 2026-05-04

**Estimated focused dev:** ~32–44h across ~9 working days

**Apple review:** +1–3 days wall-clock

**What changed from v1:** Day 1 done; Day 1.5 fixes added (swap-revert unification + any-distance Arcade drag + dead-board update); Game Center leaderboard cut; UI polish framed as Claude Design mockup implementation.

**Hard constraint:** no scope creep. Anything not on this doc waits for 1.1.

---

## ✅ Day 1 — Drag-to-Swap Constraint Fix *(done 2026-05-04)*

- [x] Day 1 done — drag-swap is single-swap-per-gesture, GridManager.cs only, no commit yet (pending playtest + commit by user).

Visual preview on hover deferred to UI polish day(s).

---

## ✅ Day 2 — Day 1.5 Swap Behavior Unification (~3–5h, 3 files) *(done 2026-05-04 in commit df648fd, audited 2026-05-06)*

The bug context: investigation revealed inconsistent swap-revert behavior across modes (Arcade has no failed-swap revert at all; Zen drag stays in place on no-match; Zen tap-tap reverts with −3s penalty). The new spec is unified: all swaps commit, all revert visually on no-match, no time penalty anywhere. Plus Arcade gets any-distance drag.

### Tasks

- [x] Delete the Arcade adjacency block in `GridManager.cs` `HandleDragEnded` (~lines 864–875). Arcade drag is now any-distance.
- [x] Update Arcade no-match branch in `ProcessMatchesCoroutine` (~lines 1361–1373) to revert tiles visually via `AnimatedSwapCoroutine(..., isRevert: true)`. Currently does nothing.
- [x] Update Zen drag no-match branch in `ProcessMatchesCoroutine` (~lines 1302–1308) to add visual revert. Currently stays in place + plays feedback.
- [x] Update Zen tap-tap no-match branch (~lines 1309–1324) to remove the `OnFailedSwap()` call (preserves visual revert, removes −3s penalty).
- [x] In `GameManager.cs` `OnFailedSwap` (~lines 983–998): make it a no-op. Mark `zenFailedSwapPenalty` field as obsolete (don't have to delete; can flag for 1.1 cleanup).
- [x] In `MatchChecker.cs`: change `HasValidMoves` dispatcher (~lines 286–291) to always call the Zen all-pairs variant since Arcade now allows any-distance drag. Same for `FindHintMove` (~lines 149–151).
- [x] Clean up the misleading "reset multiplier on failed swap" comment at `GridManager.cs:1369` (multiplier reset doesn't exist).

### Acceptance criteria

- Tap-tap or drag-swap that doesn't form a match: tiles snap back to original positions visually in both modes.
- No time penalty fires on any failed swap.
- Arcade drag works for any tile pair (no adjacency check).
- Adjacent tap-tap in Arcade still works.
- Zen reshuffle mechanic (3 charges then game over) still works.
- Arcade dead-board reshuffle still triggers correctly when no playable swap remains, accounting for any-distance drag.

### Test gates

- Arcade: tap two non-adjacent tiles → no swap (tap-tap adjacency rule preserved). Drag any tile to any tile → swap. If no match → revert visually.
- Zen: drag any tile to any tile → swap. If no match → revert. No "−3s" popup. Timer should not deduct.
- Both modes: failed swap doesn't trigger any penalty UI, scoring, or multiplier change.
- Manually create a near-dead-board state in Arcade, confirm reshuffle still fires correctly.

---

## Day 3–4 — iPad Layout + Launch Storyboard (~6–8h)

This is both the App Store rejection fix AND the user's "fill the empty side margins" polish goal. TabletLayoutAdapter has to do meaningful work, not just letterbox.

### Tasks

- [ ] In `Make10Scene.unity`, change root Canvas's `CanvasScaler.m_MatchWidthOrHeight` from `0` to `1` (Match Height). One-line YAML flip. *(verified still `0` 2026-05-06)*
- [x] Create `Assets/Scripts/TabletLayoutAdapter.cs` — runs at scene load, detects tablet aspect (`Screen.width / Screen.height > 0.65f` for portrait tablet), scales `GridContainer.sizeDelta` by ~1.25× and bumps `UIStyleGuide` popup sizes by similar factor. *(scaffolded 2026-05-06; static `IsTablet`/`UIScale` exposed for popup sizing in Days 6–8)*
- [ ] Attach the adapter to the appropriate scene object so it runs on every scene load. *(Inspector wire-up: drag GridContainer into the adapter's `gridContainer` field)*
- [ ] In Unity Player Settings → iOS → Splash Image → Launch Screen Type: configure custom Launch Storyboard (or "Custom Storyboard With Launch Image"). Wire iPad path properly so iOS doesn't fall back to iPhone compatibility mode. *(verified `iOSLaunchScreeniPadType: 0` and `iOSLaunchScreeniPadImage: {fileID: 0}` still — iPad path empty)*
- [ ] If a `LaunchScreen.storyboard` file is needed, create it (Xcode-side after build, or supply via Unity Player Settings depending on Unity 6's exact menu).
- [ ] Add iPad Air 11" portrait preset (1640×2360) to Game View custom resolutions for testing.
- [ ] Build to Xcode (Phase 1 of build flow from earlier walkthrough).

### Acceptance criteria

- iPad portrait Game View at 1640×2360 shows: gameplay grid filling ~85%+ of canvas width, character art reaching canvas edges, no visible empty side margins, popups feeling tablet-sized.
- Phone portrait still looks correct (1080×1920 reference) — no regression.
- Launch storyboard configured for iPad path (no longer falls back to iPhone compat mode).

---

## Day 5 — Physical iPad Verification (~3h)

### Tasks

- [ ] Sideload current build to physical iPad Pro via Xcode (Phase 2–4 of earlier walkthrough).
- [ ] Walk all 5 test gates: main menu, Arcade gameplay, MakeZen gameplay, results screen, options popup. Screenshot each at iPad Pro resolution.
- [ ] Compare against rejection screenshots (Apple's March screenshot + user's pre-fix iPad screenshots).
- [ ] Drag-swap behavior test on iPad touch: 8–10 gestures across both modes, confirm single-swap-per-gesture + revert-on-no-match + Arcade any-distance drag work on real touch hardware.
- [ ] Note any regressions, layout glitches, or unintended consequences.

### Acceptance criteria

- All 5 gates render with no empty side margins.
- Swap-revert behavior identical on iPad touch as in Game View.
- No new crashes or visual artifacts vs pre-sprint baseline.

---

## Day 6–8 — UI Polish: Implement Claude Design Mockups (~12–18h)

This is **implementation, not invention.** User has existing UI mockups in Claude Design that drive this work. The sprint phase is translating those mockups into the Unity scene + UI components. Don't redesign during this phase; if a mockup is ambiguous, ask user to clarify on the mockup side rather than improvising.

### Tasks

- [ ] **Day 6:** Inventory user's Claude Design mockups (user delivers screen-by-screen mockups for: main menu, Arcade HUD, MakeZen HUD, results screen, options popup, score bar/multiplier display, popup window backgrounds). Match each mockup to its current Unity counterpart.
- [ ] **Day 6–7:** Implement the popup window background polish — currently `UIStyleGuide` generates flat dark-navy panels with thin gold borders. If mockup specifies a 9-slice paper-textured panel sprite, swap it in via `PopupWindow.cs`. Likely the single largest perceived-quality bump in the entire UI tier.
- [ ] **Day 7:** Implement HUD-level polish per mockups — score display, multiplier bar, timer, mode-specific UI elements.
- [ ] **Day 7–8:** Implement results screen + options popup per mockups.
- [ ] **Day 8:** Drag-target visual preview from Day 1's deferred item — highlight valid swap targets when drag starts (especially valuable now that Arcade has any-distance drag).
- [ ] **Day 8:** Final cohesion pass — make sure transitions between screens feel intentional, no orphaned old-style elements, all panels consistent with mockup palette.

### Acceptance criteria

- Each Claude Design mockup has a matching implemented screen in Unity.
- Visual cohesion: same panel style, button style, type treatment across all menus and popups.
- Drag preview indicator works in both modes.
- No regressions in existing tutorial flow.

---

## Day 9 — Tutorial Polish + Haptics Combined (~5–7h)

### Tasks

- [ ] Audit current `Tutorial1` and `Tutorial2` states. Identify rough edges (state machine logic gaps, UI clarity, copy issues).
- [ ] Polish tutorial copy + UI per the broader UI mockup language from Days 6–8. Make sure tutorial visuals are consistent with the rest of the polished game.
- [ ] Test tutorial flow end-to-end on a fresh install.
- [ ] Add haptic feedback via Unity's iOS Haptic Plugin or `UnityEngine.iOS.Device`. Light tick on swap, slightly stronger on match, noticeable thud on Hot Streak. Wire into existing scoring code.
- [ ] Test haptics on physical iPad to confirm intensity feels right.

### Acceptance criteria

- Tutorial flow plays cleanly start-to-finish, no broken states.
- Haptics fire on swap, match, and Hot Streak with appropriate intensity.

---

## Day 10 — Final QA + Submit (~3h)

### Tasks

- [ ] Bump version `1.0` → `1.0.1` in Player Settings.
- [ ] Bump Build number by at least 1.
- [ ] Verify orphan `Assets/Images/brainlessgamejambanner.png` is deleted (~30s hygiene from earlier).
- [ ] Verify distribution cert / provisioning profile is non-expired (re-issue via Apple Developer if needed).
- [ ] Verify iOS minimum target is appropriate.
- [ ] Verify all required iOS app icon sizes are present in Asset Catalog.
- [ ] Verify Privacy Policy URL + Support URL on App Store Connect listing are valid.
- [ ] Archive in Xcode (target: "Any iOS Device arm64").
- [ ] Distribute via Organizer → App Store Connect → Upload.
- [ ] Wait for processing in App Store Connect (5–30 min).
- [ ] Optional: enable Internal TestFlight, install via TestFlight on iPad for final sanity pass before submit.
- [ ] Attach build to 1.0.1 version in App Store Connect.
- [ ] Write "What's New" notes mentioning iPad layout fix, swap-revert unification, any-distance Arcade drag, UI polish, tutorial polish, haptics. Apple's reviewer reads this — saves a re-review if they notice unexpected gameplay changes.
- [ ] Submit for App Review.

### Acceptance criteria

- 1.0.1 build successfully uploaded.
- Submission goes through to "Waiting for Review" state.

---

## Days 11+ — Apple Review Cycle (1–3 days wall-clock)

### Tasks

- [ ] Wait for Apple's verdict (no action required).
- [ ] If Approved: release to App Store, update CLAUDE.md to "1.0.1 shipped" state, archive `SHIP_SPRINT_PLAN.md`. **Sprint complete; return to HoF.**
- [ ] If Rejected: read rejection notes carefully, scope a half-week extension if needed, address the specific feedback minimally. Don't improvise scope.

---

## Hidden costs to budget around

- Distribution cert / provisioning expiry — verify before crunch (30-min surprise if expired).
- Unity 6 sometimes surprises on iOS builds — budget 1–2h.
- iPhone playtest doesn't validate iPad drag — physical iPad pass required.
- Orphan asset to delete: `Assets/Images/brainlessgamejambanner.png` — referenced nowhere.
- CLAUDE.md SFX glitching note is stale — `AudioManager.cs` already uses dedicated `timeWarningSource`.
- CLAUDE.md "multiplier reset on failed swap" claim is stale (multiplier reset doesn't exist anywhere). Worth correcting in same pass.
- Apple may flag new issues on re-review — common second-rejection causes: invalid privacy URL, stale support URL, missing icon size, App Store metadata mismatches. 30-min App Store Connect listing audit before submit recommended.

---

## Out of scope (cut for this sprint, save for 1.1+)

- Game Center leaderboard (cut entirely; massive Apple-infrastructure overhead for $0.99 game, can revisit if game gets traction).
- Spinning MakeZen out as its own free app (defer until App Store data tells you whether it's the breakout mode).
- App icon redesign (functional, not blocking).
- Splash wordmark redesign (functional, not blocking).
- Any new gameplay features beyond Day 1 + Day 1.5 fixes.
- iPad-specific dialogs or second-screen layouts beyond TabletLayoutAdapter.
- Unity Cloud / Firebase Analytics / crash reporting (nice-to-have, not blocking).
- Localization.
- Cross-platform leaderboard via PlayFab/Unity Cloud (Android Play Games to be revisited only when Android update lands).

---

## Final ship checklist (do in this order)

- [x] Day 1 done — drag-swap committed and playtested
- [x] Day 1.5 done — swap-revert unified, Arcade any-distance drag works, dead-board accurate
- [ ] Days 3–4 done — iPad Game View shows polished tablet layout, Launch Storyboard configured
- [ ] Day 5 done — physical iPad confirms all gameplay + layout fixes
- [ ] Days 6–8 done — UI matches all Claude Design mockups
- [ ] Day 9 done — tutorial polished, haptics firing correctly
- [ ] Distribution cert verified non-expired
- [ ] Orphan banner asset deleted
- [ ] iOS minimum version target verified
- [ ] All app icon sizes present
- [ ] App Store Connect listing audited (privacy URL, support URL valid)
- [ ] Version bumped to 1.0.1, build number incremented
- [ ] Archive uploaded to App Store Connect
- [ ] Build appears in TestFlight processing complete
- [ ] What's New notes written (mention all changes)
- [ ] Submitted to App Review
- [ ] (Wait for Apple)
- [ ] If accepted: release, update CLAUDE.md to "1.0.1 shipped" state, archive sprint plan
