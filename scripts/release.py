#!/usr/bin/env python3
"""Build locally against Schedule I, push a version tag, and publish a verified release."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shlex
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile


ROOT = Path(__file__).resolve().parents[1]


def run(*args: str, capture: bool = False) -> str:
    result = subprocess.run(args, cwd=ROOT, check=True, text=True,
                            stdout=subprocess.PIPE if capture else None)
    return result.stdout.strip() if capture else ""


def release_info(tag: str | None = None) -> tuple[str, Path]:
    version = ET.parse(ROOT / "CashBricks.csproj").findtext("PropertyGroup/Version") or ""
    if not re.fullmatch(r"\d+\.\d+\.\d+", version):
        raise ValueError("CashBricks.csproj must contain a numeric release version.")
    source = (ROOT / "src/Main.cs").read_text()
    melon = re.search(r'MelonInfo\(typeof\(CashBricks.Main\), "Cash Bricks", "([^\"]+)"', source)
    if not melon or melon.group(1) != version:
        raise ValueError("MelonInfo and project versions must match.")
    if tag is not None and tag != f"v{version}":
        raise ValueError(f"Tag must be v{version}, got {tag}.")
    notes = ROOT / "releases" / f"v{version}.md"
    if not notes.is_file() or not notes.read_text().strip():
        raise ValueError(f"Write release notes in {notes.relative_to(ROOT)} first.")
    return version, notes


def github_cli() -> str:
    found = shutil.which("gh")
    if found:
        return found
    local = ROOT / "tools/gh/bin/gh"
    if local.is_file():
        return str(local)
    raise ValueError("Install GitHub CLI and run gh auth login before releasing.")


def repository() -> tuple[str, str, str]:
    remote = run("git", "remote", "get-url", "origin", capture=True)
    match = re.fullmatch(r"(?:https://github\.com/|git@github\.com:)([\w.-]+/[\w.-]+?)(?:\.git)?/?", remote)
    if not match:
        raise ValueError("origin must be a GitHub HTTPS or SSH repository URL without embedded credentials.")
    branch = run("git", "branch", "--show-current", capture=True)
    if branch != "main":
        raise ValueError("Release from the main branch.")
    if run("git", "status", "--porcelain", "--untracked-files=normal", capture=True):
        raise ValueError("Commit all source, version, and release-note changes before releasing.")
    return match.group(1), branch, run("git", "rev-parse", "HEAD", capture=True)


def authenticate(gh: str, repo: str) -> None:
    status = subprocess.run((gh, "auth", "status", "--hostname", "github.com"),
                            cwd=ROOT, text=True, capture_output=True)
    if status.returncode != 0 and not os.environ.get("GH_TOKEN") and not os.environ.get("GITHUB_TOKEN"):
        # Reuse an existing HTTPS credential without printing it or writing it
        # into shell arguments, repository files, or Git configuration.
        result = subprocess.run(("git", "credential", "fill"), cwd=ROOT, text=True, capture_output=True,
                                input="protocol=https\nhost=github.com\n\n",
                                env={**os.environ, "GIT_TERMINAL_PROMPT": "0"})
        fields = dict(line.split("=", 1) for line in result.stdout.splitlines() if "=" in line)
        if result.returncode == 0 and fields.get("password"):
            os.environ["GH_TOKEN"] = fields["password"]
    run(gh, "api", "user", "--jq", ".login")
    if run(gh, "api", f"repos/{repo}", "--jq", ".permissions.push", capture=True) != "true":
        raise ValueError("The authenticated GitHub account needs push permission for this repository.")


def verify_package(archive: Path) -> None:
    with zipfile.ZipFile(archive) as bundle:
        if bundle.testzip() is not None or len(bundle.namelist()) != len(set(bundle.namelist())):
            raise ValueError("Invalid release archive.")
        files = {}
        for line in bundle.read("SHA256SUMS.txt").decode().splitlines():
            digest, name = line.split("  ", 1)
            if name in files or not re.fullmatch(r"[a-f0-9]{64}", digest):
                raise ValueError("Invalid package checksum manifest.")
            files[name] = digest
        expected = {"Mods/CashBricks.dll", "README.md", "CHANGELOG.md"}
        expected.update(guide.relative_to(ROOT).as_posix() for guide in (ROOT / "docs").glob("*.md"))
        expected.update(f"docs/images/{name}" for name in (
            "cash-brick.png", "pouring-stack.png"))
        expected.update(f"UserData/CashBricks/{name}" for name in (
            "cashbrick.obj", "cashbrick_packed.obj", "cashbrick_albedo.png", "cashbrick_packed_albedo.png", "cashbrick_icon.png",
            "cashbrick_wrap.obj", "cashbrick_wrap_albedo.png", "cashbrick_wrapped_icon.png"))
        if set(files) != expected or set(bundle.namelist()) != expected | {"SHA256SUMS.txt"}:
            raise ValueError("Release package contains unexpected or missing files.")
        for name, digest in files.items():
            if hashlib.sha256(bundle.read(name)).hexdigest() != digest:
                raise ValueError(f"Package checksum mismatch: {name}")


def publish_release(gh: str, repo: str, tag: str, version: str, notes: Path,
                    archive: Path, checksum: Path, exists: bool) -> None:
    # GitHub's REST tag lookup excludes drafts. Resolve the draft through CLI's
    # GraphQL lookup, then read its assets by stable numeric release ID.
    if not exists:
        run(gh, "release", "create", tag, "--repo", repo, "--verify-tag", "--draft",
            "--title", f"Cash Bricks {version}", "--notes-file", str(notes))
    identity = json.loads(run(gh, "release", "view", tag, "--repo", repo, "--json", "databaseId", capture=True))
    release_id = identity["databaseId"]
    if not isinstance(release_id, int) or release_id <= 0:
        raise ValueError("GitHub did not return a valid release ID.")
    endpoint = f"repos/{repo}/releases/{release_id}"
    release = json.loads(run(gh, "api", endpoint, capture=True))
    assets = {asset["name"]: asset for asset in release["assets"]}
    for path in (archive, checksum):
        digest = "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest()
        asset = assets.get(path.name)
        if asset is None:
            if not release["draft"]:
                raise ValueError("A published release is missing assets; refusing to change it.")
            run(gh, "release", "upload", tag, str(path), "--repo", repo)
            updated = json.loads(run(gh, "api", endpoint, capture=True))
            asset = next(item for item in updated["assets"] if item["name"] == path.name)
        if asset.get("digest") != digest or asset["size"] != path.stat().st_size:
            raise ValueError(f"Remote checksum/size differs for {path.name}; publishing stopped.")
    if release["draft"]:
        run(gh, "release", "edit", tag, "--repo", repo, "--draft=false", "--latest")
    print(f"Published and verified https://github.com/{repo}/releases/tag/{tag}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Validate version/notes without building, authentication, or publishing")
    parser.add_argument("--tag", help="Require an exact version tag (used by CI)")
    parser.add_argument("--offline", action="store_true", help="Build using installed .NET reference packs")
    parser.add_argument("--game-dir", type=Path, help="Game folder containing generated MelonLoader assemblies")
    args = parser.parse_args()
    version, notes = release_info(args.tag)
    tag = f"v{version}"
    if args.check:
        print(f"PASS: {tag} matches MelonInfo, project version, and release notes.")
        return
    gh = github_cli()
    repo, branch, commit = repository()
    # Confirm the authenticated account can publish before building or pushing.
    authenticate(gh, repo)
    remote_release = subprocess.run((gh, "release", "view", tag, "--repo", repo, "--json", "isDraft,url"),
                                    cwd=ROOT, text=True, capture_output=True)
    if remote_release.returncode == 0:
        existing = json.loads(remote_release.stdout)
    elif "release not found" in remote_release.stderr.lower():
        existing = None
    else:
        raise ValueError("Cannot query the GitHub release: " + remote_release.stderr.strip())

    run(sys.executable, "-m", "unittest", "discover", "-s", "tests", "-p", "test_*.py")
    build = [sys.executable, str(ROOT / "scripts/package.py")]
    if args.offline:
        build.append("--offline")
    if args.game_dir:
        build.extend(("--game-dir", str(args.game_dir.resolve())))
    run(*build)
    if repository()[2] != commit:
        raise ValueError("The source changed during the release build.")
    archive = ROOT / "dist" / f"CashBricks-{version}.zip"
    verify_package(archive)
    checksum = archive.with_suffix(archive.suffix + ".sha256")
    checksum.write_text(f"{hashlib.sha256(archive.read_bytes()).hexdigest()}  {archive.name}\n")

    tag_check = subprocess.run(("git", "rev-parse", "--verify", f"refs/tags/{tag}^{{commit}}"),
                               cwd=ROOT, text=True, capture_output=True)
    if tag_check.returncode == 0:
        if tag_check.stdout.strip() != commit:
            raise ValueError(f"{tag} already identifies another commit; bump the version.")
    else:
        run("git", "tag", "-a", tag, "-m", f"Cash Bricks {version}")
    run("git", "-c", "credential.https://github.com.helper=", "-c",
        f"credential.https://github.com.helper=!{shlex.quote(gh)} auth git-credential",
        "push", "--atomic", "--set-upstream", "origin", f"HEAD:refs/heads/{branch}", f"refs/tags/{tag}")

    # A draft can be resumed after interrupted uploads. Never replace an
    # existing published asset or make a release visible before verification.
    publish_release(gh, repo, tag, version, notes, archive, checksum, existing is not None)


if __name__ == "__main__":
    try:
        main()
    except (ValueError, subprocess.CalledProcessError, OSError) as error:
        sys.exit(f"Release failed: {error}")
