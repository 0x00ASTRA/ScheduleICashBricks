# Building from source

[README](../README.md) · [Testing](testing.md) · [Publishing releases](releasing.md)

Run these commands from a source checkout, not the extracted game-install ZIP.

Requires Python 3.9+, a .NET SDK with .NET 6 reference assemblies, the .NET 8
runtime/reference assemblies for the standalone checks, and a game installation
with MelonLoader-generated `Il2CppAssemblies`.

```sh
python3 scripts/package.py --game-dir "/path/to/Schedule I"
```

The default `GameDir` uses the Linux Steam location under
`$HOME/.local/share/Steam/steamapps/common/Schedule I`. With the installed reference
packs, build without network access using:

```sh
python3 scripts/package.py --offline
```

The script builds the DLL, runs conservation/precision/rollback checks, and
creates `dist/CashBricks-X.Y.Z.zip` with verified contents and SHA-256 checksums.
The ZIP includes the documentation and changelog. Game assemblies and
MelonLoader binaries are excluded; packaging does not install into the game.
Blender sources and rebuild commands are in the
[asset guide](https://github.com/0x00ASTRA/ScheduleICashBricks/blob/main/assets/README.md).

## Native patch audit

When `inspection/dump.cs` is available, packaging also checks every Harmony
target for a unique native address. Run this check directly with
`python3 scripts/check_native_patches.py`. The dump must correspond to the game
version being built; this check does not replace a game launch.
