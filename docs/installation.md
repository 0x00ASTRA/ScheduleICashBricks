# Installation

[README](../README.md) · [Gameplay](gameplay.md) · [Troubleshooting](troubleshooting.md)

Download the ZIP from the [latest GitHub release](https://github.com/0x00ASTRA/ScheduleICashBricks/releases/latest).

## Install or update

Install MelonLoader for the IL2CPP game and launch the game once. Close the game,
then extract `CashBricks-X.Y.Z.zip` into the game directory, alongside
`Schedule I.exe`. The resulting files should include:

```text
Mods/CashBricks.dll
UserData/CashBricks/cashbrick.obj
UserData/CashBricks/cashbrick_packed.obj
UserData/CashBricks/cashbrick_albedo.png
UserData/CashBricks/cashbrick_icon.png
UserData/CashBricks/cashbrick_wrap.obj
UserData/CashBricks/cashbrick_wrap_albedo.png
UserData/CashBricks/cashbrick_wrapped_icon.png
```

## Linux with Steam Proton

On Linux with Steam Proton, open **Schedule I → Properties → General → Launch
Options** and enter:

```text
WINEDLLOVERRIDES="version=n,b" %command%
```

This lets Proton load MelonLoader's installed `version.dll`. Without the override,
the game can start normally while all mods remain unloaded. See the
[official MelonLoader Linux instructions](https://github.com/LavaGang/MelonWiki/blob/master/docs/gettingstarted.md#linux-launch-instructions).

Launch the game and check `MelonLoader/Latest.log` for `Registered cash brick`.
If an asset is missing, the log gives its expected folder. Registration retries
without registering an incomplete item.

## Remove the mod

Unpack all cash bricks before removing the mod. Bricks use the custom item ID
`astra_cashbrick`; their money depends on that definition being available when
the save loads. Close the game before removing mod files.
