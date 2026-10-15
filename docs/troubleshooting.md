# Troubleshooting

[README](../README.md) · [Installation](installation.md) · [Testing](testing.md)

## The mod does not load

Check `MelonLoader/Latest.log` for the Cash Bricks version and `Registered cash brick`.
If MelonLoader does not appear under Proton, check the
[launch options](installation.md#linux-with-steam-proton).

Confirm you installed the IL2CPP version of MelonLoader and extracted the ZIP
into the game folder. A missing-asset message names the required file and
expected directory. Registration retries without registering an incomplete item.
Copy the full release's `UserData/CashBricks` folder rather than only the DLL.

Always close and restart the game after replacing mod files. A running game
continues using the DLL and assets it already loaded.

## Begin is disabled at the brick press

Confirm both inputs contain loose cash only, with at least $1,000 combined.
The output must be empty or a cash-brick stack with room, and actual slot locks
must be clear. Cash bricks belong at a packaging station for unpacking; remove
any left in press inputs by an older version.

Look for `Cash press UI: mode=Pack` in the log. It records the state, balances,
quantities, capacity, and locks. `manualAddLocked=True` on the output is normal:
it prevents manual deposits and does not prevent pressing.

## Unpack is disabled at a packaging station

Switch to **Unpack** mode. Put cash bricks in the **output slot**, and leave
the **product slot** empty or holding loose cash. Other contents, slot locks,
removal-locked bricks, a full cash balance, or a balance that cannot retain
the result to the cent prevent unpacking.

The brick press no longer unpacks cash. See [gameplay](gameplay.md#unpack-cash-bricks).

## An employee stalls or cash becomes zero

Install the current release and restart the game. Check supply routes, station
assignment, available output capacity, and the combined input cash balance.
Logs should show `Cash employee press started` and `Cash employee press complete`.

Versions before 1.0.9 could lose balances during native employee cash copies.
Previously zeroed instances retain no record of their former balance; the fix
prevents new losses and does not guess or automatically refund old amounts.
See the [changelog](../CHANGELOG.md#109) for the underlying fixes.

## Inventory or E interaction stays blocked

Fully restart after updating an older version. Version 1.0.6 fixed a task UI
lock leaked by the earlier cash-press task naming code. Current task cleanup
logs `Cash press task cleanup verified`; normal values include
`taskActive=False`, `taskInManager=False`, and `taskUiLocks=0` with `before=0`.
Other open menus may still own their own UI locks.

If it recurs, record whether you completed or cancelled the press, close its
interface, and check the cleanup message in `MelonLoader/Latest.log`.

## Held cash bricks are invisible or look hollow

Install the complete current release and restart the game. Version 1.0.10 fixed
held-prefab activation; 1.0.11 corrected triangle winding and added the compact
packaged core. Confirm `cashbrick_packed.obj`, both wrapper files, and the
updated DLL are installed together.

For reproducible checks and the native self-test flag, see [testing](testing.md).
