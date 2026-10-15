# Cash Bricks

A MelonLoader mod for the IL2CPP build of Schedule I. Use a brick press to turn
loose cash into storable bricks worth **$1,000 each**. The stack limit is read
from the game's product brick packaging. Packaged bricks have a separate plastic
wrapper over a compact cash model, with a matching inventory icon, storage
model, and held model. Loose cash and pouring stacks keep the original art.
Unpack cash bricks at a packaging station, using its normal Unpack mode.

## Install

Download the ZIP from the [latest GitHub release](https://github.com/0x00ASTRA/ScheduleICashBricks/releases/latest).

Install MelonLoader for the IL2CPP game and launch the game once. Close the game,
then extract `CashBricks-1.0.12.zip` into the game directory, alongside
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

## Use

1. Place loose cash in either or both brick press input slots.
2. Leave the output empty or containing a cash brick stack with room.
3. Click **Begin**. Grab the bucket and tip it with **A/D** to pour twenty physical
   $50 cash stacks into the mould, using the game's normal product pouring controls.
   The physical task keeps the game's original pouring and handle instructions.
4. Once all twenty stacks are in the mould, grab the handle and pull it all the
   way down. The completed cycle creates **one $1,000 cash brick** in the output.
   Repeat Begin/pour/press for another brick. Excess cash remains in the inputs;
   the larger cash input is consumed first.
5. Move the bricks into inventory or storage.

To unpack, use either packaging station version and switch it to **Unpack**,
just as for product bricks. Place cash bricks in the station's **output slot**
(the package source in Unpack mode). Leave its **product slot** empty or holding
loose cash. Each click of **Unpack** removes one brick from the output slot and
adds **$1,000 of loose cash to the product slot**. Collect that cash with the
game's normal cash drag controls. Packaging supplies stay untouched; brick
wrapping is discarded, as with product bricks. There is no separate unpack
hotkey. The brick press only packs cash and rejects new cash-brick inputs.
Remove any bricks left in press inputs by an earlier version and take them to
a packaging station.

Packager employees can load loose cash through their usual supply routes and
operate an assigned brick press. Each native work/animation cycle packs one
$1,000 brick; remaining cash stays in the inputs. Supply cash totalling at least
$1,000 across the two slots. Employees retain the game's movement, work speed,
station ownership, cancellation, and output delivery behavior. Unpacking cash
bricks remains an explicit action in the packaging station's Unpack mode.

Cancelling before pulling the handle leaves the input cash untouched. Cash is
reserved during pouring and is deducted only at native press completion. Slots
are released when the task ends. The normal product sequence is also described
in this [Steam gameplay discussion](https://steamcommunity.com/app/3164500/discussions/1/830459392060093785/).

Mixed product/cash inputs, actual slot locks, removal-locked inputs, and
incompatible or full outputs block cash pressing. The press's permanent
restriction on manual output deposits does not block machine production and
remains enabled. Normal product pressing uses the game's existing behavior.
Unpacking uses the station interface and leaves the wallet, equipped item,
task state, and global interaction/inventory settings to the game. Cash bricks
and loose cash cannot be mixed in press inputs. Packaging stations return loose
cash to the product slot; collect it before returning that station to product
packaging.

Cash calculations preserve cents. If a very large native float balance cannot
represent the intended balance to the cent, the operation is refused; move or
split some loose cash first. Failed inventory writes attempt to restore every
affected snapshot and report any restoration failure in the log.

Bricks use the stable item ID `astra_cashbrick` and the native item save format.
Unpack all cash bricks before removing the mod so their money does not depend on
the custom item definition being present at the next load.

## Build from source

Requires Python 3.9+, a .NET SDK with .NET 6 reference assemblies, the .NET 8
runtime/reference assemblies for the standalone checks, and a game installation
with MelonLoader-generated `Il2CppAssemblies`.

```sh
python3 scripts/package.py --game-dir "/path/to/Schedule I"
```

For this project's local game installation, the default `GameDir` is already set
in `CashBricks.csproj`. With the installed reference packs, build without network
access using:

```sh
python3 scripts/package.py --offline
```

The script builds the DLL, runs conservation/precision/rollback checks, and
creates `dist/CashBricks-1.0.12.zip` with verified contents and SHA-256 checksums.
Game assemblies and MelonLoader binaries are excluded from the package.
Blender asset sources and rebuild instructions are in `assets/README.md` in the
source checkout.

When `inspection/dump.cs` is available, packaging also checks every Harmony
target for a unique native address. Run this check directly with
`python3 scripts/check_native_patches.py`. The dump must correspond to the game
version being built; this check does not replace a game launch.

## Releases

GitHub Actions runs the portable cash/transaction tests, release validation,
version checks, and Python syntax checks on pushes to `main`, version tags, and
pull requests. Its actions are pinned to exact commits. Building the mod DLL
requires a local Schedule I installation with MelonLoader-generated assemblies;
these game and loader files are excluded from Git and release packages.

To publish a new version:

1. Update `Version` in `CashBricks.csproj` and `MelonInfo` in `src/Main.cs`.
2. Add `releases/vX.Y.Z.md` with release notes and update the install/build ZIP
   names in this README. Commit the changes on `main`.
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

Validate release metadata without publishing or using a game installation:

```sh
python3 scripts/release.py --check
```

The publishing commands follow the [GitHub CLI release workflow](https://cli.github.com/manual/gh_release_create).

Version 1.0.1 removes the `CanSlotAcceptCash` detour that caused a startup
trampoline error in 1.0.0. That native method shares its implementation with
hundreds of unrelated methods, so it cannot safely be patched as an inventory
method. Cash input filtering now uses the existing unique hard-filter hook.

Version 1.0.2 fixes the cash snapshots used during pressing and rollback. In
Schedule I 0.4.7f12, `CashInstance.GetCopy()` leaves the copy's `Balance` at zero.
The mod now creates native cash instances and explicitly assigns their balances,
following the factory used by the game's cash drag code. Runtime checks cover
snapshot isolation and native `CashData` JSON round trips as well as pressing.

Version 1.0.3 replaces instant cash conversion with the native `UseBrickPress`
task, its draggable bucket, mould detection, handle, camera, and cleanup. The
twenty physical pieces use cash meshes and box colliders on native draggable
components. A temporary product instance supplies native task physics only;
it is never placed in inventory, saved, or allowed to create a product brick.
The station calculates capacity from the custom brick's stack limit, updates
the Begin button each frame, and displays cash-specific instructions. Completion
requires all twenty owned pieces and a full handle pull, and is guarded against
repeated callbacks. Native slot snapshots still preserve `CashInstance.Balance`
independently from item quantity. UI/task state changes are logged for diagnosis.

Version 1.0.4 fixes the disabled Begin button seen in the live 1.0.3 log:
the output was empty with capacity 20, but `IsAddLocked` was true. The game sets
that flag on every brick press output to forbid manual deposits; native machine
production ignores it. Cash eligibility now follows that policy while retaining
the flag, actual slot lock checks, output item validation, and capacity checks.
The native self-test includes this output configuration, verifies production
without unlocking manual deposits, and checks actual locks and full outputs.

Version 1.0.5 removes the U shortcut and all direct wallet/equipped-slot unpack
writes. Loading cash bricks into the brick press inputs selects an Unpack
action that converts one brick per click into native loose cash in the output.
Like native product unpacking, this is an inventory operation without a new
pouring/handle task. In the base game product unpacking belongs to the packaging
station; cash unpacking is offered at the brick press as requested. Input
filtering, instruction/button text, and previews support both cash press modes,
and native product behavior resumes when the cash inputs are cleared. Existing
cash brick saves retain their item ID and $1,000 value.

Version 1.0.6 fixes the native task UI lock left behind after cash pressing.
`Task` registers a camera UI element using its `TaskName` during construction,
and removes that name again during `StopTask`. The mod previously changed
`Use brick press` to `Press cash` after construction, so cleanup tried to remove
the wrong name. The remaining `Use brick press` entry blocked normal interaction.
Version 1.0.6 preserves the native task name while customizing the cash instruction.
After each task ends, it checks the task manager, task activity, and the native
camera's count of that task's UI lock against the count before the task began.
Other menus and UI locks are retained. The press interface can remain open as
usual; the cleanup check concerns the finished task's lock.

Fully restart the game after installing 1.0.6 to clear locks leaked by the old
version. The held brick also explicitly permits normal interaction and pickup.

Version 1.0.7 removes the physical task instruction overrides, including the
periodic update that alternated with the game's pouring message. The game now
owns the original instruction throughout pouring and pressing; the station's
Pack/Unpack interface still describes cash operations. It also updates the cash
model, texture, and inventory icon to better match the game's stylized art.

Version 1.0.8 adds a separate fitted plastic wrapper to packaged cash bricks,
using the native product brick's wrapping as a visual and material reference.
The translucent shell has folded ends and a glossy surface, with the existing
cash visible underneath. Stored and held bricks, brick input previews, and the
brick inventory icon use the packaged appearance. Loose cash input previews
and physical pouring stacks retain the original model and texture. The original
cash assets are unchanged; wrapping uses separate files and a separate material.

Version 1.0.9 fixes employee cash transfers and employee press completion. The
native `CashInstance.GetCopy` copied definition/quantity but omitted `Balance`;
employee pickup and delivery use this virtual method outside the mod's own
snapshot helper. A postfix now preserves the balance in those copies, including
quantity-overridden transfers. It never multiplies dollars by item quantity.
The native employee coroutine also required twenty product units at its final
`HasSufficientProduct` check. Cash instead uses a server-owned completion token
for each native work cycle and the existing cent-safe cash transaction. Tokens
cannot complete twice or mint the temporary adapter's product. Native employee
timing, animation and cleanup still run, and player tasks never run for an NPC.
Ordinary products continue through their native checks. Cancelled, locked,
mixed, insufficient, or full-output cash operations do not consume cash.

Restart the game to load this fix. Previously zeroed cash instances contain no
record of their former balance; this version prevents new losses but does not
guess or automatically refund old amounts. All 1.0.8 art assets are unchanged.

Version 1.0.10 fixes cash bricks being invisible when their hotbar slot is
selected. The native hotbar clones the held prefab without activating it;
the old prefab had an inactive root. Its root now stays active beneath an
inactive persistent template parent, so the source remains hidden and the
equipped clone renders normally. Native equipping still assigns the held
transform and viewmodel layer and handles unequipping. The cash model,
textures, plastic wrapper, and packing behavior are unchanged. Detached native
self-tests now verify clone activation and both held meshes without equipping
anything in player inventory. Fully restart the game to load the updated DLL.

Version 1.0.11 gives packaged cash bricks tightly packed paper edges, using the
player's wallet cash stacks as a visual reference. A separate packaged core
replaces the deep tier gaps with continuous paper sides and thin bill lines.
It uses the existing cash texture, bands, dimensions, and plastic wrapper, with
an updated packaged icon. Held bricks, stored bricks, and brick press previews
use the new core. Loose previews and physical pouring stacks keep the original
mesh. The OBJ loader also preserves the exported outward triangle winding:
the old loader reversed every face, exposing interior surfaces instead of the
outer paper and film. Native mesh inspection confirmed that the exported
winding already matches the game. Detached held-clone checks now verify face
orientation on the loaded paper and wrapper. Install the new
`cashbrick_packed.obj` alongside the updated DLL and icon, then fully restart
the game.

Version 1.0.12 moves cash-brick unpacking from the brick press to packaging
stations Mk1 and Mk2. It uses the existing Unpack mode, native button/keyboard
handling, output-slot source and product-slot destination. The station's state
and unpack methods handle cash bricks with the existing cent-safe transaction;
ordinary products retain their native behavior. Cash never starts a packaging
task or writes the wallet. The product slot displays loose cash without trying
to cast it to a product definition. Full/incompatible/locked destinations and
removal-locked bricks block the conversion. The brick press rejects new brick
inputs, disables Begin for saved legacy brick inputs, and keeps those bricks
available to remove. Cash packing, employee press cycles, item IDs, and all art
files are unchanged. Fully restart the game after updating.

## In-game verification

Standalone checks and compilation can run without launching the game. Native
inventory, rendering, animation, save/load, and multiplayer behavior also need
in-game verification; those are not certified by the standalone checks.

Add `--cashbricks-selftest` to the game's launch options. After registration,
the mod exercises detached native slots, inactive Mk1/Mk2 unpack routing,
held-prefab clones, and an item JSON round trip. Look for
`SELFTEST COMPLETE` in `MelonLoader/Latest.log`. The checks do not use player
inventory or saved stations.

With Steam Proton, use the combined launch options:

```text
WINEDLLOVERRIDES="version=n,b" %command% --cashbricks-selftest
```

For a manual check, load two $1,000 cash stacks. Cancel during pouring and confirm
both remain unchanged. Then pour all twenty pieces, pull the handle, and confirm
one brick plus $1,000 remaining. Repeat the physical cycle for the second brick
and take both resulting bricks to a packaging station. Switch to Unpack and
put them in its output slot. Confirm each Unpack click removes one and leaves
$2,000 of native loose cash in the product slot after two clicks. Collect it
and confirm that inventory and E interaction still work. Repeat with both
station versions and the native keyboard shortcut, then
try $600.10 plus $399.90 and $650.25 plus $500.50. Confirm one brick and a
$150.75 remainder for the last pair. Test a full output, mixed inputs, ordinary
product pressing after cash pressing, and returning to the menu and loading
again. Save/load a brick in the hotbar, storage, and press output. Verify these
flows with the same mod installed on each participant before using multiplayer.
For the packaged visuals, inspect a held/stored cash brick and its inventory
icon: the cash should remain visible through glossy folded plastic. Confirm the
press rejects new cash bricks, while loose cash previews and pouring stacks
remain unwrapped. Confirm unpacked cash appears at the packaging station and
ordinary product displays resume after collecting it. If an old save contains
cash bricks in press inputs, verify Begin stays disabled and the bricks can be
removed without losing value.
Select a cash brick's hotbar slot, switch to another item and back, and open
and close inventory. The held brick should appear whenever selected; ordinary
item equipping and E interaction should still work. Reload a save containing
a cash brick in the selected slot and check the held appearance again.
For employee automation, supply $3,000 in loose cash, assign a packager to the
press and watch pickup, carrying and delivery: the balance must never become
zero. Let three native cycles finish, confirming one brick per cycle and $3,000
total conserved across source, employee inventory, press inputs and output.
Repeat with $650.25 plus $500.50 (one brick and $150.75 left), a full output,
interrupted work, and ordinary products afterward. Logs should show `Cash
employee press started` and `Cash employee press complete`. Detached self-tests
cover the native copy/slot operations and completion transaction, while actual
employee pathfinding, timing and station release require this live check.

`SELFTEST COMPLETE` checks detached native cash slots, station unpacking and save data. It does not
run a physical task or prove that Begin, pouring, and the handle work in a live
station. In the live log, look for `Cash press UI: mode=Pack, state=CanBegin`,
`Cash press task started`, `Cash press mould loaded`, and `Physical cash press
complete`. Unpacking logs `Cash brick unpacked at the packaging station`.
After physical pressing or cancellation, look for `Cash press task cleanup
verified` with `taskActive=False`, `taskInManager=False`, and `taskUiLocks=0`
(normally `before=0`). Close the press and confirm that inventory and E work.
These live checks are distinct from the detached native inventory self-tests.
