#!/usr/bin/env python3
"""Build Make10 for Android AND iOS in one go.

Bumps the version once, then runs the Android pipeline (signed .aab) and the
iOS pipeline (Xcode export -> archive -> .ipa) at that same version. Both
platforms share bundleVersion and the version code (iOS CFBundleVersion =
AndroidBundleVersionCode), so one bump keeps the stores in step.

    ./Tools/build_all.py                  # bump, build both
    ./Tools/build_all.py --name 1.2       # milestone name, build both
    ./Tools/build_all.py --no-bump        # rebuild both at the current version
    ./Tools/build_all.py --upload         # build both, upload Play draft + App Store Connect
    ./Tools/build_all.py --only ios       # one platform (still bumps unless --no-bump)

Requirements: everything listed in build_android.py and build_ios.py. The Unity
editor must be CLOSED. Android runs first; a failure there stops before iOS so
a half-bumped version never gets uploaded anywhere.
"""
from __future__ import annotations

import argparse
import sys
import time

import build_android
import build_ios
import bump_version


def main(argv: list[str] | None = None) -> None:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--no-bump", action="store_true", help="build at the current version code")
    p.add_argument("--upload", action="store_true", help="upload both builds after building")
    p.add_argument("--name", help="set the version name (implies a bump)")
    p.add_argument("--only", choices=["android", "ios"], help="build a single platform")
    args = p.parse_args(argv)

    if args.no_bump and args.name:
        sys.exit("ERROR: --name can't be combined with --no-bump (same code = same release).")

    # Bump exactly once here; the per-platform scripts then run with --no-bump.
    if not args.no_bump:
        bump_version.bump(new_name=args.name)
    code, name = bump_version.read_version()
    print(f"==> Make10 v{name} (code {code})")

    child = ["--no-bump"] + (["--upload"] if args.upload else [])
    platforms = [args.only] if args.only else ["android", "ios"]
    started = time.time()

    for platform in platforms:
        print(f"\n===================== {platform.upper()} =====================")
        t0 = time.time()
        (build_android if platform == "android" else build_ios).main(child)
        print(f"==> {platform} done in {(time.time() - t0) / 60:.1f} min")

    print(f"\n==> All done: {', '.join(platforms)} v{name} (code {code}) in "
          f"{(time.time() - started) / 60:.1f} min")


if __name__ == "__main__":
    main()
