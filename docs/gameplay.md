# Gameplay

[README](../README.md) · [Installation](installation.md) · [Troubleshooting](troubleshooting.md)

## Pack loose cash

1. Place loose cash in either or both brick press inputs, totalling at least $1,000.
2. Leave the output empty or containing a cash-brick stack with room.
3. Click **Begin**. Grab the bucket and tip it with **A/D** to pour twenty physical
   $50 cash stacks into the mould, using the normal product-pouring controls.
4. Once all twenty stacks are in the mould, grab the handle and pull it fully down.
   The cycle creates **one $1,000 cash brick** in the output.
5. Collect the brick or repeat the cycle. Excess cash remains in the inputs;
   the larger cash input is consumed first.

The task keeps the game's original pouring and handle instructions. Cancelling
before completing the handle pull leaves the input cash untouched. Cash is
reserved during pouring, deducted at completion, and its slots are released when
the task ends. The normal product sequence is also described in this
[Steam gameplay discussion](https://steamcommunity.com/app/3164500/discussions/1/830459392060093785/).

## Unpack cash bricks

Use either packaging station version, Mk1 or Mk2:

1. Switch the station to **Unpack**, just as for product bricks.
2. Place cash bricks in its **output slot**, which supplies the packaged items
   in Unpack mode.
3. Leave its **product slot** empty or holding loose cash.
4. Click **Unpack**. Each click removes one brick from the output slot and adds
   **$1,000 of loose cash to the product slot**.
5. Collect the cash with the game's normal cash-drag controls.

Packaging supplies stay untouched. Brick wrapping is discarded, as with product
bricks. There is no separate unpack hotkey; the station's native button and
keyboard controls handle the action. Collect the loose cash before returning
that station to ordinary product packaging.

The brick press only packs cash and rejects new cash-brick inputs. Bricks saved
in press inputs by an older version remain removable; take them to a packaging
station to unpack them.

## Employee automation

Packager employees can load loose cash through their usual supply routes and
operate an assigned brick press. Supply at least $1,000 across the two input
slots. Each native work/animation cycle packs one brick; remaining cash stays
in the inputs.

Employees retain the game's movement, work speed, station ownership,
cancellation, and output-delivery behavior. Unpacking remains a player action
in the packaging station's Unpack mode.

## Slot requirements and cash value

Mixed product/cash inputs, locked or removal-locked inputs, and incompatible or
full outputs block pressing. The press's restriction on manually depositing
items into its output stays enabled and does not block machine production.
Ordinary product pressing uses the game's existing behavior.

Each brick is worth $1,000. Its stack limit comes from the game's product-brick
packaging. Cash calculations preserve cents. If a very large native float
balance cannot represent the result to the cent, the operation is refused;
split or move some loose cash first. Failed inventory writes attempt to restore
every affected snapshot and report restoration failures in the log.

Unpacking uses station slots and leaves the wallet, equipped item, task state,
and global inventory/interaction settings to the game.
