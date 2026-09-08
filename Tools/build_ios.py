#!/usr/bin/env python3
"""One-command iOS build for Make10.

Bumps the version, exports an Xcode project headlessly with Unity, archives and
signs it with xcodebuild, and exports an App Store .ipa. With --upload the
archive goes straight to App Store Connect (TestFlight processing starts; the
build never reaches the App Store until you submit it in App Store Connect).

    ./Tools/build_ios.py                 # bump code +1 (name 0.<code>), build .ipa
    ./Tools/build_ios.py --upload        # build, then upload to App Store Connect
    ./Tools/build_ios.py --name 1.2 --upload   # milestone name too
    ./Tools/build_ios.py --no-bump       # rebuild the current version code
    ./Tools/build_ios.py --skip-unity    # re-archive the last exported Xcode project

Requirements:
  - The Unity editor for this project must be CLOSED (Unity locks the project).
  - Xcode installed and selected (`xcode-select -p` ends in Xcode.app/Contents/Developer).
  - Apple Development + Apple Distribution certs for the team in the login
    keychain (Xcode > Settings > Accounts > Manage Certificates), with the WWDR
    G3 intermediate present. Check: `security find-identity -v -p codesigning`.
  - Signing is automatic. xcodebuild creates/refreshes provisioning profiles via
    -allowProvisioningUpdates, which needs EITHER the Apple ID signed into Xcode
    OR an App Store Connect API key in the environment:
      export M10_ASC_KEY_ID='ABC123DEFG'
      export M10_ASC_ISSUER_ID='xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
      export M10_ASC_KEY_PATH=~/.config/asc/AuthKey_ABC123DEFG.p8
    The API key is required for --upload when nobody is logged into Xcode
    (headless / CI).
  - Optional: M10_APPLE_TEAM_ID overrides the team in Player Settings.

Versioning: CFBundleShortVersionString = Unity bundleVersion, CFBundleVersion =
AndroidBundleVersionCode (set by BuildScript.BuildiOS), so one bump covers both
stores.
"""
from __future__ import annotations

import argparse
import os
import plistlib
import re
import shutil
import subprocess
import sys
from pathlib import Path

import bump_version  # sibling module in Tools/

PROJECT_ROOT = Path(__file__).resolve().parent.parent
PROJECT_SETTINGS = PROJECT_ROOT / "ProjectSettings" / "ProjectSettings.asset"
PROJECT_VERSION = PROJECT_ROOT / "ProjectSettings" / "ProjectVersion.txt"
DEFAULT_OUTPUT_DIR = Path(os.environ.get("M10_BUILD_DIR", str(Path.home() / "Developer" / "Make10Builds")))
XCODE_SCHEME = "Unity-iPhone"

_TEAM_RE = re.compile(r"^  appleDeveloperTeamID: (\S+)\s*$", re.M)


def die(msg: str) -> None:
    sys.exit(f"ERROR: {msg}")


def unity_binary() -> Path:
    if "UNITY_BIN" in os.environ:
        return Path(os.environ["UNITY_BIN"])
    version = ""
    for line in PROJECT_VERSION.read_text().splitlines():
        if line.startswith("m_EditorVersion:"):
            version = line.split(":", 1)[1].strip()
            break
    return Path(f"/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/MacOS/Unity")


def team_id() -> str:
    env = os.environ.get("M10_APPLE_TEAM_ID")
    if env:
        return env
    m = _TEAM_RE.search(PROJECT_SETTINGS.read_text())
    if not m:
        die("appleDeveloperTeamID not set in Player Settings (or export M10_APPLE_TEAM_ID).")
    return m.group(1)


def asc_auth_args() -> list[str]:
    """xcodebuild flags for App Store Connect API-key auth, or [] to rely on the
    Apple ID signed into Xcode."""
    key_id = os.environ.get("M10_ASC_KEY_ID")
    issuer = os.environ.get("M10_ASC_ISSUER_ID")
    key_path = os.environ.get("M10_ASC_KEY_PATH")
    if not (key_id or issuer or key_path):
        return []
    if not (key_id and issuer and key_path):
        die("Set all three of M10_ASC_KEY_ID, M10_ASC_ISSUER_ID, M10_ASC_KEY_PATH (or none).")
    key = Path(key_path).expanduser()
    if not key.is_file():
        die(f"App Store Connect key not found at {key}.")
    return ["-authenticationKeyPath", str(key),
            "-authenticationKeyID", key_id,
            "-authenticationKeyIssuerID", issuer]


def preflight_signing(team: str) -> None:
    if not shutil.which("xcodebuild"):
        die("xcodebuild not found. Install Xcode from the App Store.")
    dev_dir = subprocess.run(["xcode-select", "-p"], capture_output=True, text=True).stdout.strip()
    if "Xcode.app" not in dev_dir:
        die(f"xcode-select points at {dev_dir!r}, not Xcode. "
            "Run: sudo xcode-select -s /Applications/Xcode.app/Contents/Developer")
    ids = subprocess.run(["security", "find-identity", "-v", "-p", "codesigning"],
                         capture_output=True, text=True).stdout
    if f"Apple Distribution" not in ids or team not in ids:
        die(f"No valid 'Apple Distribution' identity for team {team} in the keychain.\n"
            "  Xcode > Settings > Accounts > Manage Certificates > + > Apple Distribution.\n"
            "  If the cert exists but is listed invalid, the WWDR G3 intermediate is "
            "missing: import https://www.apple.com/certificateauthority/AppleWWDRCAG3.cer")


def run(cmd: list[str], log: Path | None = None, **kw) -> None:
    print("    $ " + " ".join(str(c) for c in cmd))
    if log:
        with log.open("w") as fh:
            proc = subprocess.run(cmd, stdout=fh, stderr=subprocess.STDOUT, **kw)
        if proc.returncode != 0:
            print(subprocess.run(["tail", "-n", "40", str(log)], capture_output=True, text=True).stdout)
            die(f"command failed (exit {proc.returncode}); full log: {log}")
    else:
        subprocess.run(cmd, check=True, **kw)


def main(argv: list[str] | None = None) -> None:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--no-bump", action="store_true", help="build at the current version code")
    p.add_argument("--upload", action="store_true", help="upload the archive to App Store Connect")
    p.add_argument("--name", help="set the version name (implies a bump)")
    p.add_argument("--skip-unity", action="store_true",
                   help="reuse the existing Xcode export (implies --no-bump)")
    args = p.parse_args(argv)
    if args.skip_unity:
        args.no_bump = True

    # --- Preflight ---------------------------------------------------------
    unity = unity_binary()
    if not args.skip_unity and not os.access(unity, os.X_OK):
        die(f"Unity not found at {unity} (set UNITY_BIN to override).")
    if args.no_bump and args.name:
        die("--name can't be combined with --no-bump (same code = same release).")
    team = team_id()
    preflight_signing(team)
    auth = asc_auth_args()
    if args.upload and not auth:
        print("    (no M10_ASC_* key set: upload will use the Apple ID signed into Xcode)")
    DEFAULT_OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    # --- Version bump ------------------------------------------------------
    if not args.no_bump:
        bump_version.bump(new_name=args.name)

    code, name = bump_version.read_version(PROJECT_SETTINGS)
    safe_name = re.sub(r"[^0-9A-Za-z]+", "_", name)
    xcode_dir = DEFAULT_OUTPUT_DIR / f"Make10v{safe_name}-ios"
    archive = DEFAULT_OUTPUT_DIR / f"Make10v{safe_name}.xcarchive"
    ipa_dir = DEFAULT_OUTPUT_DIR / f"Make10v{safe_name}-ipa"
    unity_log = DEFAULT_OUTPUT_DIR / f"build-ios-v{safe_name}.log"
    xcode_log = DEFAULT_OUTPUT_DIR / f"xcodebuild-v{safe_name}.log"

    if archive.exists() and not args.no_bump:
        die(f"{archive} already exists — version name {name} was likely built/uploaded. "
            "Bump again (--name for a new milestone), or remove the stale archive if "
            "it was never uploaded.")

    # --- Unity export ------------------------------------------------------
    if args.skip_unity:
        if not (xcode_dir / f"{XCODE_SCHEME}.xcodeproj").is_dir():
            die(f"--skip-unity but no Xcode project at {xcode_dir}.")
        print(f"==> Reusing Xcode project at {xcode_dir}")
    else:
        print(f"==> Exporting Make10 v{name} (build {code}) -> {xcode_dir}")
        print(f"    (Unity is silent in batchmode; tail the log: {unity_log})")
        env = {**os.environ, "M10_BUILD_OUTPUT": str(xcode_dir), "M10_APPLE_TEAM_ID": team}
        subprocess.run(
            [str(unity), "-quit", "-batchmode", "-nographics",
             "-projectPath", str(PROJECT_ROOT), "-buildTarget", "iOS",
             "-executeMethod", "BuildScript.BuildiOS", "-logFile", str(unity_log)],
            env=env, check=True,
        )
        if not (xcode_dir / f"{XCODE_SCHEME}.xcodeproj").is_dir():
            die(f"Unity reported success but no Xcode project at {xcode_dir} — check {unity_log}.")

    # --- Archive (compile + sign) ------------------------------------------
    print(f"==> Archiving -> {archive}")
    shutil.rmtree(archive, ignore_errors=True)
    run(["xcodebuild", "archive",
         "-project", str(xcode_dir / f"{XCODE_SCHEME}.xcodeproj"),
         "-scheme", XCODE_SCHEME,
         "-configuration", "Release",
         "-destination", "generic/platform=iOS",
         "-archivePath", str(archive),
         "-allowProvisioningUpdates",
         f"DEVELOPMENT_TEAM={team}",
         "CODE_SIGN_STYLE=Automatic",
         *auth], log=xcode_log)

    # --- Export .ipa / upload ----------------------------------------------
    export_opts = {
        "method": "app-store-connect",
        "teamID": team,
        "signingStyle": "automatic",
        "uploadSymbols": True,
        "destination": "upload" if args.upload else "export",
    }
    opts_plist = DEFAULT_OUTPUT_DIR / f"ExportOptions-v{safe_name}.plist"
    opts_plist.write_bytes(plistlib.dumps(export_opts))

    action = "Uploading to App Store Connect" if args.upload else f"Exporting .ipa -> {ipa_dir}"
    print(f"==> {action}")
    shutil.rmtree(ipa_dir, ignore_errors=True)
    run(["xcodebuild", "-exportArchive",
         "-archivePath", str(archive),
         "-exportOptionsPlist", str(opts_plist),
         "-exportPath", str(ipa_dir),
         "-allowProvisioningUpdates",
         *auth], log=xcode_log.with_suffix(".export.log"))

    if args.upload:
        print(f"==> Uploaded v{name} (build {code}). App Store Connect will process it for "
              "TestFlight in a few minutes. Nothing ships until you submit for review.")
    else:
        ipas = list(ipa_dir.glob("*.ipa"))
        if not ipas:
            die(f"export finished but no .ipa in {ipa_dir}.")
        size_mb = ipas[0].stat().st_size / (1024 * 1024)
        print(f"==> Built {size_mb:.0f} MB  {ipas[0]}")
        print(f"Next: ./Tools/build_ios.py --skip-unity --upload   (uploads this archive)\n"
              f"  or: open {archive}   (Xcode Organizer > Distribute App)")


if __name__ == "__main__":
    main()
