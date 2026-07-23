# BRP → URP Visual Regression Report

**Device size:** iPhone 13 — 1170 × 2532 (Android game-view group)
**Method:** UnityMCP `ScreenCapture` on `Make10Scene`, same screens before (BRP) and after (URP 17.5.0).
**Automated gate:** `Make10.Tests.PlayMode` — 4/4 green on **both** BRP and URP, including the `UI/Additive` shader-supported test.

## Result: no visual regressions

| Screen | BRP | URP | Verdict |
|--------|-----|-----|---------|
| Main Menu | `brp/01_mainmenu.png` | `urp/01_mainmenu.png` | Identical — title, buttons, SHOP, BP, avatar bg, parallax, TMP |
| Options popup | `brp/02_options.png` | `urp/02_options.png` | Identical — sliders (blue/green), header, Done, dimmed bg |
| Arcade grid | `brp/06_arcade_game.png` | `urp/06_arcade_game.png` | Identical — 5×5 grid, tinted numbers (0 grey,1 gold,2 blue,3 green,4 coral), HUD, avatar, pause glow |

BRP-only reference shots (no URP pair needed — transient/duplicate states): `03_tutorial_howtoplay`, `04_countdown`, `05_scoring_popup`, `07_makezen_intro`.

## Investigated & cleared: timer "color difference"

At first glance the Arcade timer looked different (BRP: small **red** wedge at 7s; URP: **black** blob at 49s). Root-caused as **not a regression**:

- The timer is `TimerRadial`, an `Image` (radial fill, `UI/Default`) whose **color is driven by remaining time** (`UIManager.UpdateTimerDisplay` → `GetTimerColor`).
- Scene-serialized states: **Healthy = black** `(0,0,0,1)`, Warning (≤20s) = gold, Danger (≤10s) = red `(0.9,0.2,0.2)`.
- The two screenshots were captured at different timer values (49s Healthy vs 7s Danger), so the colors *should* differ. Same-state rendering is identical across pipelines (`UI/Default` overlay is pipeline-agnostic; live Danger color reads red on URP, matching BRP).

## Why the migration is low-risk (confirmed)

All rendering is Screen Space – Overlay UGUI + TMP + procedural textures. No post-processing, RenderTexture, ParticleSystem, or SpriteRenderer. The single custom shader `UI/Additive` compiles and is supported under URP (asserted by the test suite).
