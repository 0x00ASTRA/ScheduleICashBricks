# Cash Bricks

A MelonLoader mod for Schedule I's IL2CPP build. Press loose cash into **$1,000
bricks**, store them like other items, and unpack them at a packaging station.
Bricks use native bill faces, a centered white-and-purple **$1,000** band, and
plastic wrapping. Pouring stacks use the base game's cash model and materials.

| $1,000 cash brick | Cash stack for pouring |
| :---: | :---: |
| <img src="docs/images/cash-brick.png" alt="Plastic-wrapped cash brick with native bill faces and a centered white-and-purple $1000 band" width="400"> | <img src="docs/images/pouring-stack.png" alt="Base-game cash stack with its original bill faces and blue-and-white band" width="400"> |

*Model renders of the current visuals; lighting varies in game.*

## Install

1. Install MelonLoader for IL2CPP and launch the game once.
2. Close the game and extract the [latest release ZIP](https://github.com/0x00ASTRA/ScheduleICashBricks/releases/latest)
   into the game folder, alongside `Schedule I.exe`.
3. Fully restart the game.

For Steam Proton, use these launch options:

```text
WINEDLLOVERRIDES="version=n,b" %command%
```

See [installation](docs/installation.md) for expected files and update instructions.

## Use

- **Pack:** Load at least $1,000 into the brick press, click **Begin**, pour the
  cash, and pull the handle. Each cycle makes one brick.
- **Unpack:** Switch a packaging station to **Unpack**. Put bricks in its
  **output slot** and collect $1,000 per brick from its **product slot**.
- **Automate:** Packager employees can supply and operate the brick press through
  their normal routes.

Unpack all cash bricks before removing the mod. See [gameplay](docs/gameplay.md)
for controls, slot requirements, and employee setup.

## Documentation

- [Troubleshooting](docs/troubleshooting.md)
- [Building from source](docs/building.md)
- [Testing and in-game verification](docs/testing.md)
- [Publishing releases](docs/releasing.md)
- [Asset sources and Blender builds](https://github.com/0x00ASTRA/ScheduleICashBricks/blob/main/assets/README.md)
- [Changelog](CHANGELOG.md)
