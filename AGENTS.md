# Make10 — project & release guide

A Unity 6 (6000.5.x) Android number-puzzle game published on Google Play as
**Make10** (`gg.wizardbodega.make10deluxe`). This file is the memory for how the
project is built, tested, and shipped — for both humans and agents.

Studio: CJ Rhone / Wizard Bodega. For gameplay/design detail see `CLAUDE.md`.

## Prerequisites (CLI dependencies)

Every dev machine that builds/publishes needs these. Versions are what's in use
today (2026-07-22); newer patch versions are fine.

| Tool | Install | Used for | Notes |
|------|---------|----------|-------|
| **Unity 6000.5.4f1** + **Android Build Support** (incl. OpenJDK, SDK, NDK) | Unity Hub | headless `.aab` build, `keytool` for signing/verify | Must match `ProjectSettings/ProjectVersion.txt` exactly. The SDK Platform for the target API (36) must be installed. |
| **fastlane** (2.237.0) | `brew install fastlane` | Google Play upload lanes | Pulls its own Ruby dependency. |
| **Python 3** (3.10+) | preinstalled on macOS / `brew install python` | `Tools/*.py` | 3.10+ required — scripts use `X \| None` type hints. |
| **git** (2.x) + SSH key on GitHub | Xcode CLT / `brew install git` | clone/commit/push; `origin` is SSH (`git@github.com:cjrhone/Make10.git`) | — |
| **Ruby** | comes with the `fastlane` formula | fastlane runtime | No separate install needed. |
| **Java / keytool** | bundled in Unity's Android module | keystore verify/sign | Use Unity's OpenJDK; no system JDK needed. |

Quick machine setup:

```bash
brew install fastlane            # + Ruby
# Unity 6000.5.4f1 with Android Build Support via Unity Hub
# GitHub SSH key configured (git@github.com)
```

Not CLI tools, but required (kept **outside** the repo — see Signing below):
upload keystore + `M10_KEYSTORE_PASS`/`M10_KEYALIAS_PASS` in `~/.zshenv`, and the
Play service-account key at `~/.config/play/Make10.play.json`.

## Layout

| Path | What |
|------|------|
| `Assets/Scripts/` | Game code (managers, grid, tile, VFX) — see `CLAUDE.md` inventory |
| `Assets/Scenes/Make10Scene.unity` | The only scene (in Build Settings) |
| `Assets/Editor/BuildScript.cs` | Headless build entry point (`BuildScript.BuildAndroid`) |
| `Assets/Editor/iOSBuildHelper.cs` | Pre-existing iOS build helper |
| `Assets/Tests/` | EditMode + PlayMode tests — **not created yet** |
| `Tools/*.py` | Build / version / test tooling (Python — preferred over Bash here) |
| `fastlane/` | Google Play upload lanes |
| `~/.claude/skills/update-play-target-api/` | User-level skill (shared across all games): bump Android target API to the latest Play requirement |

## Conventions

- **Version name = `0.<code>`** (v10 = 0.10). `bump_version.py` keeps them in
  sync so Play never shows a mismatch like `11(0.10)`. Use `--name` only for a
  real milestone (e.g. `1.0`).
- **versionCode is global**: once a code is uploaded to *any* track it's
  consumed forever — always bump before building.
- **Env-var prefix is `M10_`** (CardMatch uses `CM_`) so both games' keystore
  passwords can coexist in one shell without collision.

## Testing

Editor must be **closed** (Unity locks the project).

```bash
./Tools/run_tests.py              # EditMode + PlayMode
./Tools/run_tests.py playmode     # one suite
./Tools/run_tests.py --allow-empty  # tolerate a suite with 0 tests
```

The runner parses the NUnit results XML and **fails on a suite that ran 0
tests** (Unity's `-runTests` exits 0 on nothing — a false green without this
guard). Pass `--allow-empty` to tolerate it.

> **No tests exist yet**, and adding them needs a refactor first: game code
> lives in `Assembly-CSharp`, which a test asmdef cannot reference. To write
> real game-logic tests, move the code under test behind its own asmdef, then
> add an EditMode/PlayMode test asmdef that references it. Until then every suite
> runs 0 tests (so `run_tests.py` fails by design).

## Build & publish

**Prereqs (each shell):** Unity closed, and keystore passwords exported:

```bash
export M10_KEYSTORE_PASS='...'
export M10_KEYALIAS_PASS='...'
```

One command does bump → build signed `.aab` → upload a **Production draft**:

```bash
./Tools/build_android.py --upload
```

- Draft never goes live on its own — press **publish** in the Play Console.
- Output named by version **name**: `~/Developer/Make10Builds/Make10v<name>.aab`
  (`.`→`_`, e.g. `1.1` → `Make10v1_1.aab`; default `0.<code>` → `Make10v0_2.aab`)
  plus `build-v<name>.log`. The version **code** stays Play-side tracking only.
- Variants: `--no-bump` (rebuild same code), `--name 1.1 --upload` (milestone).

### Native debug symbols (ANR / crash symbolication)

`BuildScript` sets `UserBuildSettings.DebugSymbols` to **SymbolTable / Zip**, so
each build emits a `*.symbols.zip` next to the `.aab`. `build_android.py` finds
it and, on `--upload`, passes it to the `production_draft` lane, which uploads it
as **native debug symbols** for that versionCode (`mapping_paths` → nativeCode)
so Google Play can symbolicate native crashes and ANRs. Missing symbols is a
warning, not a build failure. (`SymbolTable` is enough for ANR stacks; `Full`
also embeds DWARF and is much larger.)

## Releasing (git tags)

Each Play release gets an **annotated tag** `v<name>` on the release commit:

```bash
git tag -a v1.1 <commit> -m "Make10 v1.1 (Android) — Play versionCode 2 ..."
git push origin v1.1
```

The release commit bumps `bundleVersion` + `AndroidBundleVersionCode` and updates
`CHANGELOG.md`. Tag *after* the draft uploads cleanly.

Build only (no upload), then upload later:

```bash
./Tools/build_android.py
./Tools/bump_version.py           # standalone version bump if needed
```

## Android target/min SDK

- **Target SDK → 36 / Min SDK 25 / IL2CPP / ARM64-only** (`AndroidTargetArchitectures: 2`).
  Play requires **API 36 (Android 16)** for updates by **Aug 31 2026** (extendable
  to Nov 1 2026). Changelog review (2026-07-22) found no blockers for this game;
  the large-screen orientation change is waived by **App Category = Game**.
- **Target API level**: Play requires the latest each year. Run the
  `update-play-target-api` skill to bump `AndroidTargetSdkVersion` after checking
  the Android behavior-change changelogs. Don't raise `AndroidMinSdkVersion` to
  satisfy a *target* requirement (min = device reach; Unity 6 floor is API 26).

## Signing & credentials (all outside the repo — never commit)

- **Upload keystore**: alias `make10`. Passwords come from `M10_KEYSTORE_PASS` /
  `M10_KEYALIAS_PASS` (put them in `~/.zshenv`). The keystore *path* lives in
  Player Settings (`AndroidKeystoreName`) and is machine-specific — for Editor
  builds use a relative path (`../Make10Builds/user.keystore`); for headless/CI
  set `M10_KEYSTORE_PATH` and `BuildScript` applies it at build time, so the
  committed value is irrelevant to the pipeline.
- **Play service account key**: `~/.config/play/Make10.play.json`
  (`SUPPLY_JSON_KEY`); `build_android.py` falls back to this path if unset. It is
  a **byte-identical copy of `CardMatch.play.json`** — one shared service-account
  key authorized across all studio games (Make10 listing included). Run
  `fastlane verify` to confirm before the first upload.

## fastlane lanes (`fastlane/Fastfile`)

- `fastlane verify` — check the Play credential works (read-only). Run this FIRST.
- `fastlane production_draft aab:<path> [symbols:<path>]` — upload an `.aab` to
  Production as a draft; optional `symbols:` uploads the native debug symbols zip.
- `fastlane promote version:<code>` — promote an already-uploaded build to a
  Production **draft** without re-uploading (a code can only be uploaded once).
- `fastlane internal aab:<path>` — internal testing track.

## Open setup items (finish before first release)

1. **Keystore path**: point `AndroidKeystoreName` at the local keystore — relative
   `../Make10Builds/user.keystore` for the Editor, and/or `M10_KEYSTORE_PATH` for
   headless. Save Project so it flushes to `ProjectSettings.asset`.
2. **Keystore passwords**: put `M10_KEYSTORE_PASS` / `M10_KEYALIAS_PASS` in
   `~/.zshenv` (not `~/.zshrc` — see gotcha below) so automation sees them.
3. **Play key**: `~/.config/play/Make10.play.json` in place; run `fastlane verify`.
4. **App Category = Game**: confirm Player Settings → Other → Application Category
   is **Game** (`android:appCategory="game"` in the manifest). Required so the
   portrait lock survives on large screens under target API 36 (see below).

## Gotchas learned the hard way

- **Env vars don't reach non-interactive shells.** `~/.zshrc` only loads for
  interactive terminals; scripts/cron read `~/.zshenv`. Put exports there if you
  need them in automation.
- **Promoting uses `track_promote_release_status`**, not `release_status` — the
  `promote` lane sets it to `draft` so a promote never auto-goes-live.
- **Production uploads as a DRAFT only.** Going live is always a manual publish
  click in the Console — the tooling never auto-rolls-out.
