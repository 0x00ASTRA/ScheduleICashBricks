# Publishing releases

[README](../README.md) · [Building](building.md) · [Testing](testing.md)

GitHub Actions runs the portable cash/transaction tests, release validation,
version checks, and Python syntax checks on pushes to `main`, version tags, and
pull requests. Its actions are pinned to exact commits. Building the mod DLL
requires a local Schedule I installation with MelonLoader-generated assemblies;
these game and loader files are excluded from Git and release packages.

## Publish a version

Run these commands from a source checkout. To publish a new version:

1. Update `Version` in `CashBricks.csproj` and `MelonInfo` in `src/Main.cs`.
2. Add `releases/vX.Y.Z.md` with release notes and update `CHANGELOG.md`.
   Commit the changes on `main`.
3. Install [GitHub CLI](https://cli.github.com/) and authenticate with
   `gh auth login --hostname github.com --git-protocol https --web --scopes workflow`.
   An existing GitHub HTTPS credential or `GH_TOKEN` can also supply the token.
4. Run `python3 scripts/release.py --offline`, adding `--game-dir "/path/to/Schedule I"`
   if needed. Omit `--offline` when .NET reference packs need to download.

The release command requires a clean checkout, checks version/notes and GitHub
access, builds/tests locally, validates the package, and pushes `main` with an
annotated `vX.Y.Z` tag atomically. It creates a draft GitHub release, uploads the
ZIP plus its `.sha256` checksum, checks the uploaded digests/sizes, then publishes
the release. A retry can finish an interrupted draft; a tag pointing to another
commit or a published asset with different contents is rejected. Authentication
tokens are never stored in repository files or command arguments.

## Validate metadata

Validate release metadata without publishing or using a game installation:

```sh
python3 scripts/release.py --check
```

The publishing commands follow the [GitHub CLI release workflow](https://cli.github.com/manual/gh_release_create).
