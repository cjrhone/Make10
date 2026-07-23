# Changelog

All notable changes to Make10 are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
this project uses versionName = `0.<code>` for interim builds and a milestone
name (e.g. `1.1`) for releases (see `AGENTS.md`).

## [Unreleased]

## [1.1] — 2026-07-22

**Platform: Android only.** Ships to Google Play (App Bundle, versionCode 2);
no iOS build in this release. No gameplay changes — infrastructure/tooling only.
**Play status:** submitted to Production 2026-07-22, under review.

### Changed
- Updated Unity Editor to **6000.5.4f1**.
- Android **target API level 35 → 36** (Android 16) to meet Google Play's
  Aug 31 2026 requirement; **min SDK 25 → 26** (Unity 6 floor). App Category set
  to **Game** so portrait lock survives on large screens under API 36.

### Added
- Headless **Android build/publish pipeline** for Google Play Store builds
  (`Tools/*.py`, `Assets/Editor/BuildScript.cs`, `fastlane/`) — one command
  bumps the version, builds a signed `.aab`, and uploads a Production draft.
