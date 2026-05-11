# Make10 Sprint Log

Persistent memory across Scrummy briefings. One block per standup. Newest at the bottom.

---

## 2026-05-06 — Day 2 of 9

- No commits since SHIP_SPRINT_PLAN.md was filed (2026-05-04). Nothing auto-checked.
- SHIP_SPRINT_PLAN.md itself is still untracked in git — flagged user to commit it so future audits can rely on it.
- Working tree has ~5 uncommitted RectTransform edits in `Make10Scene.unity` (anchors + reparenting). Not a clean match to any Day 2 or Day 3–4 task. Flagged for user to clarify; not auto-checked.
- Today's focus surfaced: Day 2 — Day 1.5 swap behavior unification across `GridManager.cs`, `GameManager.cs`, `MatchChecker.cs`. ~3–5h, 3 files, 7 sub-tasks, single commit on completion.
- Watch items raised: Day 6–8 mockup inventory not started; distribution cert expiry unverified; orphan `brainlessgamejambanner.png` still present; version still `1.0`, build number empty.
- Sprint pacing: one calendar day of inertia between Day 1 commit (2026-05-04) and today. On the line, not behind yet.

## 2026-05-06 — Day 2 of 9 (afternoon refresh)

- Same-day re-run of the standup. No new commits, working tree unchanged from the morning brief — same RectTransform edits still uncommitted, same files untracked.
- Nothing auto-checked. Day 2 (Day 1.5 swap behavior unification) is still the open job; the morning's task list and acceptance gates carry forward unchanged.
- Inertia is now two calendar days post Day 1 commit. Still on the line, but a third quiet day pushes the sprint to "behind."
- Watch items unchanged from morning: dirty working tree (decide: commit or stash before Day 2 work), Day 6–8 mockup inventory still not started, distribution cert unverified, orphan banner asset still present, version still 1.0.

## 2026-05-06 — Day 2 of 9 (deep audit + Day 3–4 kickoff)

- **Plot twist.** Deep source audit revealed Day 2 (Day 1.5 swap behavior unification) was actually shipped in commit `df648fd` ("Repo cleanup..."), not the prior morning's read. All 7 sub-tasks verified live in code:
  - `GridManager.cs` HandleDragEnded — adjacency block removed, comment "Any-distance drag is allowed in both modes (1.0.1 unified swap-revert spec)".
  - `GridManager.cs` Zen + Arcade no-match branches — both use `AnimatedSwapCoroutine(..., isRevert: true)` with `isProcessing` re-lock, comment "Unified 1.0.1 behaviour".
  - Zen tap-tap branch — no `OnFailedSwap()` call.
  - `GameManager.OnFailedSwap()` — no-op with 1.0.1 comment, `zenFailedSwapPenalty` flagged as preserved-for-Inspector-stability only.
  - `MatchChecker.HasValidMoves()` and `FindHintMove()` — both dispatch unconditionally to Zen all-pairs variants; Arcade variants retained as private dead code with 1.0.1 deprecation comment.
  - Stale "reset multiplier on failed swap" comment scrubbed.
- SHIP_SPRINT_PLAN.md updated: Day 2 marked done with audit annotation; Day 1 + Day 1.5 ticked in final ship checklist.
- **Day 3–4 partially advanced:** scaffolded `Assets/Scripts/TabletLayoutAdapter.cs` — orientation-agnostic aspect detection (threshold 0.65), scales `gridContainer.sizeDelta` by 1.25× on tablet, exposes static `IsTablet`/`UIScale` for popup sizing later. Needs Inspector wire-up + scene attach.
- **Day 3–4 still open:** `CanvasScaler.m_MatchWidthOrHeight` still `0` (needs flip to `1`); Launch Storyboard iPad path still empty (`iOSLaunchScreeniPadType: 0`, `iOSLaunchScreeniPadImage: {fileID: 0}`); iPad 1640×2360 Game View preset likely not added.
- Sprint posture: ahead of where the morning brief thought we were. Day 2 already in the can means the user's actually on Day 3–4 today. Not behind — possibly slightly ahead if they finish the scene flip + adapter wiring + storyboard config in one sitting.

## 2026-05-07 — Day 3–4 of 9

- Quiet day. No new commits since `df648fd` (still 2026-05-04). Nothing auto-checked.
- TabletLayoutAdapter.cs untouched since yesterday's 22:17 scaffold — still needs Inspector wire-up to GridContainer + scene attach.
- `CanvasScaler.m_MatchWidthOrHeight` still `0` in Make10Scene.unity (needs flip to `1`).
- iPad Launch Storyboard path still empty in ProjectSettings.asset: `iOSLaunchScreeniPadType: 0`, `iOSLaunchScreeniPadImage: {fileID: 0}`.
- Working tree still carries the same ~24-line `Make10Scene.unity` diff from prior days, plus untracked SHIP_SPRINT_PLAN.md / SPRINT_LOG.md / TabletLayoutAdapter.cs / Cosmetic + Shop stubs. Two calendar days of no commits.
- Today's focus: finish Day 3 in one sitting — canvas flip, adapter wire-up + scene attach, Launch Storyboard iPad path, 1640×2360 Game View preset, then build to Xcode (Day 4).
- Watch items unchanged: Day 6–8 mockup inventory still not started; distribution cert unverified; orphan banner asset still present; version still `1.0`.
- Sprint posture: 4 calendar days into a ~9-working-day plan, sitting at Day 3–4. Two quiet days back-to-back means the slack is gone — a third quiet day pushes this to "behind."

## 2026-05-11 — Day 3–4 of 9

- Zero new commits since `df648fd` (2026-05-04). Seven calendar days / five working days of git silence. Nothing auto-checked.
- **Real progress in the working tree, uncommitted:** `CanvasScaler.m_MatchWidthOrHeight` is now `1` in `Make10Scene.unity` (was `0` on 2026-05-07). Day 3–4 canvas flip is done in the editor, not yet in git. Per rules ("shipped = committed"), no checkbox yet — flagged in briefing.
- Working tree diff on `Make10Scene.unity` has grown to 21 insertions / 341 deletions (was ~24 lines on 2026-05-07) — substantial cleanup landed alongside the canvas flip but never committed.
- TabletLayoutAdapter.cs still has zero references in `Make10Scene.unity` — not attached to a scene object, GridContainer not wired into its serialized field. The script is dead code until that lands.
- Launch Storyboard iPad path still empty in ProjectSettings.asset (`iOSLaunchScreeniPadType: 0`, `iOSLaunchScreeniPadImage: {fileID: 0}`). This is the rejection-fix item; can't be skipped.
- Orphan `Assets/Images/brainlessgamejambanner.png` still present. Version still `1.0`, build number still empty.
- Sprint posture: officially behind. By working-day count user should be ~Day 5 today. Ship window (2026-05-15 → 2026-05-22) is tightening — four working days to optimistic ship date, minus 1–3 for Apple review. Today is either a shipping day or May 15 floor evaporates.
- Today's focus framed: commit the working tree (canvas flip + scene cleanup + delete orphan banner + track plan/log files), wire TabletLayoutAdapter to scene, configure Launch Storyboard iPad path, add 1640×2360 Game View preset, build to Xcode.
