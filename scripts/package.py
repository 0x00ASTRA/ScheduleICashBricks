#!/usr/bin/env python3
"""Build, test, and package Cash Bricks without installing into the game."""

import argparse
import hashlib
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
import zipfile


ROOT = Path(__file__).resolve().parents[1]


def run(*args: str) -> None:
    subprocess.run(args, cwd=ROOT, check=True)


def installed_reference_version(major: int) -> str:
    sdks = subprocess.check_output(["dotnet", "--list-sdks"], text=True)
    versions = set()
    roots = []
    for sdk_path in re.findall(r"\[([^\]]+)\]", sdks):
        roots.append(Path(sdk_path).parent / "packs" / "Microsoft.NETCore.App.Ref")
    cache_info = subprocess.check_output(["dotnet", "nuget", "locals", "global-packages", "--list"], text=True)
    cache_path = cache_info.strip().split(": ", 1)[-1]
    roots.append(Path(cache_path) / "microsoft.netcore.app.ref")
    for packs in roots:
        if packs.exists():
            versions.update(p.name for p in packs.iterdir() if re.fullmatch(rf"{major}\.0\.\d+", p.name))
    if not versions:
        raise RuntimeError(f"Offline build needs an installed .NET {major} reference pack.")
    return max(versions, key=lambda version: tuple(map(int, version.split("."))))


def build_properties(offline: bool, major: int) -> list[str]:
    if not offline:
        return []
    return [f"-p:RuntimeFrameworkVersion={installed_reference_version(major)}", "-p:EnableTargetingPackDownload=false"]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-dir", type=Path, help="Schedule I folder containing MelonLoader")
    parser.add_argument("--offline", action="store_true", help="Use installed reference packs and disable downloads")
    args = parser.parse_args()
    if (ROOT / "inspection/dump.cs").is_file():
        run(sys.executable, str(ROOT / "scripts/check_native_patches.py"))
    game_props = [f"-p:GameDir={args.game_dir.resolve()}"] if args.game_dir else []
    mod_props = build_properties(args.offline, 6) + game_props
    test_props = build_properties(args.offline, 8)

    with tempfile.TemporaryDirectory(prefix="cashbricks-feed-") as empty_feed:
        for project, properties in (("CashBricks.csproj", mod_props), ("tests/CashRulesTests.csproj", test_props)):
            restore_args = ["--source", empty_feed] if args.offline else []
            run("dotnet", "restore", project, "-p:NuGetAudit=false", *properties, *restore_args)
        run("dotnet", "build", "CashBricks.csproj", "-c", "Release", "--no-restore", *mod_props)
        run("dotnet", "run", "--project", "tests/CashRulesTests.csproj", "-c", "Release", "--no-restore", *test_props)

    version = ET.parse(ROOT / "CashBricks.csproj").findtext("PropertyGroup/Version")
    if not version or not re.fullmatch(r"\d+\.\d+\.\d+", version):
        raise RuntimeError("Expected a numeric mod version in CashBricks.csproj")
    files = {
        "Mods/CashBricks.dll": ROOT / "bin/Release/net6.0/CashBricks.dll",
        "README.md": ROOT / "README.md",
        "CHANGELOG.md": ROOT / "CHANGELOG.md",
    }
    for guide in sorted((ROOT / "docs").glob("*.md")):
        files[guide.relative_to(ROOT).as_posix()] = guide
    for preview in ("cash-brick.png", "pouring-stack.png"):
        files[f"docs/images/{preview}"] = ROOT / "docs/images" / preview
    for asset in ("cashbrick.obj", "cashbrick_packed.obj", "cashbrick_albedo.png", "cashbrick_packed_albedo.png", "cashbrick_icon.png",
                  "cashbrick_wrap.obj", "cashbrick_wrap_albedo.png", "cashbrick_wrapped_icon.png"):
        files[f"UserData/CashBricks/{asset}"] = ROOT / "assets" / asset
    payload = {name: path.read_bytes() for name, path in sorted(files.items())}
    payload["SHA256SUMS.txt"] = "".join(
        f"{hashlib.sha256(data).hexdigest()}  {name}\n" for name, data in payload.items()
    ).encode()

    dist = ROOT / "dist"
    dist.mkdir(exist_ok=True)
    archive = dist / f"CashBricks-{version}.zip"
    with tempfile.TemporaryDirectory(prefix="cashbricks-package-", dir=dist) as staging:
        staged_archive = Path(staging) / archive.name
        with zipfile.ZipFile(staged_archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as bundle:
            for name, data in sorted(payload.items()):
                info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                info.external_attr = 0o100644 << 16
                bundle.writestr(info, data)
        with zipfile.ZipFile(staged_archive) as bundle:
            if bundle.testzip() is not None or set(bundle.namelist()) != set(payload):
                raise RuntimeError("Package validation failed")
            for name, data in payload.items():
                if bundle.read(name) != data:
                    raise RuntimeError(f"Package content mismatch: {name}")
        staged_archive.replace(archive)
    print(f"Created and verified {archive}")


if __name__ == "__main__":
    main()
