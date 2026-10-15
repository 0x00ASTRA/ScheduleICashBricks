# Changelog

Entries describe behavior at each release. For current instructions, see the
[gameplay guide](docs/gameplay.md). Since 1.0.12, cash bricks unpack at packaging
stations; the brick press only packs cash.

## 1.0.12

Moves cash-brick unpacking from the brick press to packaging
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

## 1.0.11

Gives packaged cash bricks tightly packed paper edges, using the
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

## 1.0.10

Fixes cash bricks being invisible when their hotbar slot is
selected. The native hotbar clones the held prefab without activating it;
the old prefab had an inactive root. Its root now stays active beneath an
inactive persistent template parent, so the source remains hidden and the
equipped clone renders normally. Native equipping still assigns the held
transform and viewmodel layer and handles unequipping. The cash model,
textures, plastic wrapper, and packing behavior are unchanged. Detached native
self-tests now verify clone activation and both held meshes without equipping
anything in player inventory. Fully restart the game to load the updated DLL.

## 1.0.9

Fixes employee cash transfers and employee press completion. The
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

## 1.0.8

Adds a separate fitted plastic wrapper to packaged cash bricks,
using the native product brick's wrapping as a visual and material reference.
The translucent shell has folded ends and a glossy surface, with the existing
cash visible underneath. Stored and held bricks, brick input previews, and the
brick inventory icon use the packaged appearance. Loose cash input previews
and physical pouring stacks retain the original model and texture. The original
cash assets are unchanged; wrapping uses separate files and a separate material.

## 1.0.7

Removes the physical task instruction overrides, including the
periodic update that alternated with the game's pouring message. The game now
owns the original instruction throughout pouring and pressing; the station's
Pack/Unpack interface still describes cash operations. It also updates the cash
model, texture, and inventory icon to better match the game's stylized art.

## 1.0.6

Fixes the native task UI lock left behind after cash pressing.
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

## 1.0.5

Removes the U shortcut and all direct wallet/equipped-slot unpack
writes. Loading cash bricks into the brick press inputs selects an Unpack
action that converts one brick per click into native loose cash in the output.
Like native product unpacking, this is an inventory operation without a new
pouring/handle task. In the base game product unpacking belongs to the packaging
station; cash unpacking is offered at the brick press as requested. Input
filtering, instruction/button text, and previews support both cash press modes,
and native product behavior resumes when the cash inputs are cleared. Existing
cash brick saves retain their item ID and $1,000 value.

## 1.0.4

Fixes the disabled Begin button seen in the live 1.0.3 log:
the output was empty with capacity 20, but `IsAddLocked` was true. The game sets
that flag on every brick press output to forbid manual deposits; native machine
production ignores it. Cash eligibility now follows that policy while retaining
the flag, actual slot lock checks, output item validation, and capacity checks.
The native self-test includes this output configuration, verifies production
without unlocking manual deposits, and checks actual locks and full outputs.

## 1.0.3

Replaces instant cash conversion with the native `UseBrickPress`
task, its draggable bucket, mould detection, handle, camera, and cleanup. The
twenty physical pieces use cash meshes and box colliders on native draggable
components. A temporary product instance supplies native task physics only;
it is never placed in inventory, saved, or allowed to create a product brick.
The station calculates capacity from the custom brick's stack limit, updates
the Begin button each frame, and displays cash-specific instructions. Completion
requires all twenty owned pieces and a full handle pull, and is guarded against
repeated callbacks. Native slot snapshots still preserve `CashInstance.Balance`
independently from item quantity. UI/task state changes are logged for diagnosis.

## 1.0.2

Fixes the cash snapshots used during pressing and rollback. In
Schedule I 0.4.7f12, `CashInstance.GetCopy()` leaves the copy's `Balance` at zero.
The mod now creates native cash instances and explicitly assigns their balances,
following the factory used by the game's cash drag code. Runtime checks cover
snapshot isolation and native `CashData` JSON round trips as well as pressing.

## 1.0.1

Removes the `CanSlotAcceptCash` detour that caused a startup
trampoline error in 1.0.0. That native method shares its implementation with
hundreds of unrelated methods, so it cannot safely be patched as an inventory
method. Cash input filtering now uses the existing unique hard-filter hook.
