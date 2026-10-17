# Fixes

Behavior of each fix. Implementation lives in `src/ValheimQuickFix/Patches/`.

## Restore shield

**Config:** `Weapon switching` → `RestoreShield` (default `true`)
**Patch:** `Humanoid.EquipItem` (Prefix + Postfix) — `Patches/RestoreShieldPatch.cs`

**Problem:** with a shield and one-handed weapon equipped, equipping a bow (or any two-handed
weapon) unequips the shield. Switching back to the one-handed weapon leaves the shield in the
inventory and you must re-equip it by hand.

**Behavior:**

1. Equipping a bow / two-handed weapon while a shield is in the left hand remembers that shield.
2. Equipping a one-handed weapon afterwards re-equips the remembered shield, if the left hand is
   empty and the shield is still in the inventory.
3. Manually equipping any shield clears the remembered one.

**Known limits:** the remembered shield is held in memory only and is forgotten on game restart.

## Push boat

**Config:** `Boats` → `PushBoat` (default `true`)
**Patch:** `Ship.Awake` (Postfix) adds a `BoatPush` component — `Patches/PushBoatPatch.cs`

**Problem:** a boat run aground can't be moved by the player standing next to it.

**Behavior:**

1. Looking at a boat from outside it shows `[E] Push boat`.
2. Pressing E pushes the boat away from the side you stand on, for a short burst. Holding E keeps pushing.
3. No prompt and no push while you are standing on the boat or swimming (wading in shallow water is fine).
4. The boat's owner simulates its physics, so pressing E claims ownership of the boat first.

**Known limits:** no check that the boat is actually grounded; you can push a floating boat too.
The push strength is a fixed 20 m/s² (acceleration, so it feels the same for every boat), chosen by testing in-game.
**Compatibility notes:** the real `Hoverable` interface has a member (`GetHoverOffset`) that older
compile-time libs lack, and `Character.IsSwimming` is looked up at runtime for the same reason.

## Stack on take all

**Config:** `Inventory` → `StackOnTakeAll` (default `true`)
**Patch:** `Container.RPC_TakeAllResponse` (Postfix) — `Patches/StackOnTakeAllPatch.cs`

**Problem:** "Take all" on any container (chest, tombstone, ...) goes through `Inventory.MoveAll`, which puts each item in
the slot it had in the container. That never merges into another stack, so a stack taken into an empty slot can sit
next to a partial stack of the same item you already carry (for example wood 20 and wood 15 in two slots). A chest's
grid differs from yours, so its slot numbers land on arbitrary slots of your inventory, which makes this common.
Moving a single item (clicking it) does merge in vanilla.

**Behavior:** after a granted take-all, partial stacks of the same item in your inventory are topped up:

1. "Same item" is the game's own rule: same item name, quality, and world level.
2. The earliest partial stack (hotbar first, then row by row) is topped up from the latest one, until at most one partial
   stack of that item remains. Full stacks are never touched, so nothing is rearranged.
3. Equipped stacks (such as arrows equipped as ammo) are skipped.
4. It applies to every container, tombstones included, and only to the player who took the items.

**Known limits:** "Take all" only, which includes the automatic take-all when a whole tombstone fits. Dragging single items
out of an open container is vanilla behavior and can still leave partial stacks.

**Compatibility notes:** `ItemData.m_equipped` was spelled `m_equiped` and `m_worldLevel` did not exist in the compile-time
libs (0.202), so both are looked up by name at runtime (`ItemFields`). If the equipped flag can't be found the fix disables
itself and logs why, rather than risk merging away an equipped stack.

## Restore tombstone slots

**Config:** `Inventory` → `RestoreTombstoneSlots` (default `true`)
**Patch:** `Container.RPC_TakeAllResponse` (Prefix) — `Patches/RestoreTombstoneSlotsPatch.cs`

**Problem:** taking everything from a tombstone first tries to put each item back at its original slot. If that slot now holds a
different item (picked up after respawning), vanilla gives up on the slot and the corpse item goes to the first free
slot, so your layout is not restored.

**Behavior:**

1. Before vanilla moves the items, the item in the way is moved to a free slot, so the corpse item gets its old place back.
2. The displaced item takes the first free slot, top row first and left to right. It never takes a slot another corpse
   item wants back.
3. An empty slot, or a stack of the same item with room left, is left to vanilla (it merges). A full stack or an identical
   non-stackable item can't merge, so it is moved.
4. If there is no free slot for the item in the way, nothing is moved and vanilla behaves as before: the corpse item is
   not picked up and stays in the tombstone.
5. Only tombstones are affected; "take all" from a chest or any other container is untouched.

**Known limits:** a displaced item loses its hotbar slot if it was on the hotbar. Works together with
*Stack on take all*: after slots are restored, partial stacks are topped up, earliest slot first.


## Fair resource rounding

**Config:** `Drops` → `FairResourceRounding` (default `true`)
**Patches:** `Game.ScaleDrops(ItemData, int)` and `Game.ScaleDrops(int, int)` (Postfix), `DropTable.GetDropList(int)`
(Prefix + Postfix + Finalizer) — `Patches/FairResourceRoundingPatch.cs`

**Problem:** the world's resource rate (for example 1.5x) scales how many items drop, but the game rounds to the nearest
whole number, and loot tables (`DropTable`) round every stack of their extra fractional pass up to at least 1. So 1 x 1.5
always gives 2 and 3 x 1.5 = 4.5 always gives 5: at 1.5x you get closer to 2x.

**Behavior:**

1. Fixed and ranged amounts (pickables, monster drops, ...): the scaled amount is rounded down or up at random in
   proportion to its fractional part, so 4.5 gives 4 or 5, 50% each. The game's minimum of 1 and its stack-size cap stay.
2. Loot tables (trees, rocks, containers, ...) at a fractional rate above 1x: the game rolls the whole steps of the rate
   (the `1` of 1.5x), then the table is rolled once more at normal size and each item kept in proportion to the fraction,
   rounded at random the same way. Items the table marks as not scaled are left out of that extra share.
3. Whole rates (1x, 2x, ...) and rates below 1x are left to the game; only fractional rates above 1x change.

**Known limits:** the roll happens on whichever client owns the thing being broken or killed, so drops rolled by another
player's game (or the server) are unaffected. The extra loot-table roll draws the table's drop chance again.
**Compatibility notes:** `Game.m_resourceRate`, `Game.ScaleDrops` and `m_nonScaledDropTypes` are newer than the compile-time
libs, so everything is looked up by name at runtime and the fix disables itself with a log line if one is missing.
