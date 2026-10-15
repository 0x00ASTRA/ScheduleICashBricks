# Testing and in-game verification

[README](../README.md) · [Building](building.md) · [Troubleshooting](troubleshooting.md)

## Portable checks

Run from the source checkout:

```sh
dotnet run --project tests/CashRulesTests.csproj --configuration Release
python3 -m unittest discover -s tests -p 'test_*.py'
python3 scripts/release.py --check
```

The standalone tests cover cash conservation, precision, capacity, rollback,
physical-task completion, and employee-cycle guards. Release tests cover package
validation and publishing safeguards. See [building](building.md) for the full
build/package command.

Standalone checks and compilation can run without launching the game. Native
inventory, rendering, animation, save/load, and multiplayer behavior also need
in-game verification; those are not certified by the standalone checks.

## Detached native self-tests

Add `--cashbricks-selftest` to the game's launch options. After registration,
the mod exercises detached native slots, inactive Mk1/Mk2 unpack routing,
held-prefab and native pouring-stack clones, texture compositing, icon visibility,
and an item JSON round trip. Version 1.0.13 runs 100 checks. Look for
`SELFTEST COMPLETE` in `MelonLoader/Latest.log`. The checks do not use player
inventory or saved stations.

With Steam Proton, use the combined launch options:

```text
WINEDLLOVERRIDES="version=n,b" %command% --cashbricks-selftest
```

## Manual packing and unpacking

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

## Visuals and compatibility

For the packaged visuals, inspect a held/stored cash brick and its inventory
icon: native bill faces and one centered white-and-purple `$1000` band should
remain visible through glossy folded plastic, with readable lettering. Confirm
the held model lines up with an ordinary product brick and stays within its
forward reach. Pouring stacks should match the native cash stack's bill faces,
paper edges, and blue-and-white band while remaining draggable. Confirm the
press rejects new cash bricks, while loose cash previews and pouring stacks
remain unwrapped. Confirm unpacked cash appears at the packaging station and
ordinary product displays resume after collecting it. If an old save contains
cash bricks in press inputs, verify Begin stays disabled and the bricks can be
removed without losing value.
Select a cash brick's hotbar slot, switch to another item and back, and open
and close inventory. The held brick should appear whenever selected; ordinary
item equipping and E interaction should still work. Reload a save containing
a cash brick in the selected slot and check the held appearance again.

## Employee automation

For employee automation, supply $3,000 in loose cash, assign a packager to the
press and watch pickup, carrying and delivery: the balance must never become
zero. Let three native cycles finish, confirming one brick per cycle and $3,000
total conserved across source, employee inventory, press inputs and output.
Repeat with $650.25 plus $500.50 (one brick and $150.75 left), a full output,
interrupted work, and ordinary products afterward. Logs should show `Cash
employee press started` and `Cash employee press complete`. Detached self-tests
cover the native copy/slot operations and completion transaction, while actual
employee pathfinding, timing and station release require this live check.

## Log checks

`SELFTEST COMPLETE` checks detached native cash slots, station unpacking and save data. It does not
run a physical task or prove that Begin, pouring, and the handle work in a live
station. In the live log, look for `Cash press UI: mode=Pack, state=CanBegin`,
`Cash press task started`, `Cash press mould loaded`, and `Physical cash press
complete`. Unpacking logs `Cash brick unpacked at the packaging station`.
After physical pressing or cancellation, look for `Cash press task cleanup
verified` with `taskActive=False`, `taskInManager=False`, and `taskUiLocks=0`
(normally `before=0`). Close the press and confirm that inventory and E work.
These live checks are distinct from the detached native inventory self-tests.
