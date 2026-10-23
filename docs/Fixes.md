# Fixes

Behavior of each fix. Implementation lives in `src/ValheimQuickFix/Patches/`.

## Restore shield

**Config:** `Gear` → `RestoreShield` (default `true`)
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

**Config:** `Building & Travel` → `PushBoat` (default `true`)
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


## Floors count as a roof

**Config:** `Building & Travel` → `FloorsNotLeaky` (default `true`)
**Patch:** `Cover.IsUnderRoof` (Postfix) — `Patches/FloorRoofPatch.cs`

**Problem:** `Cover.IsUnderRoof` casts a sphere straight up from a point and treats any hit as a roof, except objects tagged
`leaky`. Floors carry that tag, so standing under a floor (a second storey, a deck) never counts as being under a roof, even
though `Cover.GetCoverForPoint` counts the same floor as cover. `Player.InShelter` needs both, so the Rested bonus and
sleeping fail under a floor.

**Behavior:**

1. When vanilla finds no roof, the same upward cast is repeated with the same layers.
2. A hit counts as a roof if it is tagged `leaky`, belongs to a building `Piece`, and the piece's name contains "floor".
3. Everything else stays as vanilla: other leaky objects (and anything not part of a piece) still let rain through.
4. The first time each leaky piece is seen above a point, its name is logged with whether it counts, so pieces that
   should count too (roof ribs, grates, ...) can be added.

**Known limits:** `Cover` is static and is also used for rain, wetness and fireplaces, so those follow the new result.
The "Sheltered" status and the Rested bonus still need walls: `Player.InShelter` also wants 14 of the 17 cover rays to
hit something, and a roof alone gives at most 9, same as a normal roof.
Pieces are matched by prefab name, not by a game-defined category.

## Skip startup logos

**Config:** `General` → `SkipStartupLogos` (default `true`)
**Patch:** `SceneLoader.Awake` (Postfix) — `Patches/SkipStartupLogosPatch.cs`

**Problem:** the first scene shows the Coffee Stain and Iron Gate logos for about two seconds each before the game loads.

**Behavior:** `SceneLoader._showLogos` is cleared after `Awake`, so its loading coroutine goes straight past both logos
(the scene still loads in the background as usual).

**Known limits:** the Unity splash screen is drawn by the engine before any game code runs and is not affected.
**Compatibility notes:** `SceneLoader` and `_showLogos` are looked up by name; the fix disables itself with a log line if
either is missing.

## Fair resource rounding

**Config:** `Enemies & Drops` → `FairResourceRounding` (default `true`)
**Patches:** `Game.ScaleDrops(ItemData, int)` and `Game.ScaleDrops(int, int)` (Postfix), `DropTable.GetDropList(int)`
(Prefix + Postfix + Finalizer) — `Patches/FairResourceRoundingPatch.cs`

**Problem:** the world's resource rate (for example 1.5x) scales how many items drop, but the game rounds to the nearest
whole number, and loot tables (`DropTable`) round every stack of their extra fractional pass up to at least 1. So 1 x 1.5
always gives 2 and 3 x 1.5 = 4.5 always gives 5: at 1.5x you get closer to 2x.

**Behavior:**

1. Fixed and ranged amounts (pickables, monster drops, ...): a ranged amount is first rolled as a whole number (the game
   rolls a float between min and max + 1, which averages too high), then the scaled amount is rounded down or up at random in
   proportion to its fractional part, so 4.5 gives 4 or 5, 50% each. The game's minimum of 1 and its stack-size cap stay.
2. Loot tables (trees, rocks, containers, ...) at a fractional rate above 1x: the game rolls the whole steps of the rate
   (the `1` of 1.5x), then the table is rolled once more at normal size and each item kept in proportion to the fraction,
   rounded at random the same way. Items the table marks as not scaled are left out of that extra share.
3. Whole rates (1x, 2x, ...) and rates below 1x are left to the game; only fractional rates above 1x change.

**Known limits:** the roll happens on whichever client owns the thing being broken or killed, so drops rolled by another
player's game (or the server) are unaffected. The extra loot-table roll draws the table's drop chance again.
**Compatibility notes:** `Game.m_resourceRate`, `Game.ScaleDrops` and `m_nonScaledDropTypes` are newer than the compile-time
libs, so everything is looked up by name at runtime and the fix disables itself with a log line if one is missing.

## Ignore torches

**Config:** `Enemies & Drops` → `IgnoreTorches` (default `true`)
**Patch:** `BaseAI.FindClosestStaticPriorityTarget` and `BaseAI.FindRandomStaticTarget` (Postfix) — `Patches/IgnoreTorchesPatch.cs`

**Problem:** enemies treat standing and wall torches as structures to attack, and stop to smash them instead of
going after you.

**Behavior:** when a monster picks a structure to attack and it is a torch, the choice is discarded and the monster
carries on as if the torch were not there. Torches are matched by prefab name (contains `torch`); braziers and
other fires are not affected.

**Known limits:** only the peer that owns a monster runs its AI, so this only affects monsters simulated on your
machine (always in singleplayer; on a server only those you own). A monster that is already attacking a torch is
not interrupted until it next picks a target.

## Place stacks keeps your gear

**Config:** `Inventory` → `PlaceStacksKeepEquipment` (weapons and tools) and `PlaceStacksKeepConsumables` (cooked and brewed food and potions), both default `true`
**Patch:** `Inventory.StackAll` (Prefix, Finalizer) and `Humanoid.IsItemEquiped` (Postfix) — `Patches/PlaceStacksKeepGearPatch.cs`

**Problem:** the "Place stacks" button, and holding E on a container, call `Inventory.StackAll`. It moves every item you
carry into a container that already holds an item with the same name and only skips equipped items. It never checks
that the item can actually stack, so a spare hoe goes into a chest that holds a hoe, and so do weapons, tools, food
and potions.

**Behavior:**

1. While `StackAll` runs for your own inventory, `Humanoid.IsItemEquiped` also answers `true` for items of type
   OneHandedWeapon, TwoHandedWeapon, TwoHandedWeaponLeft, Bow, Torch and Tool when `PlaceStacksKeepEquipment` is on, and
   for Consumable items that the game produces when `PlaceStacksKeepConsumables` is on.
   "Produced" means the item is the output of a cauldron recipe, a `CookingStation` conversion or a `Fermenter` conversion,
   which covers cooked meat, cauldron food and mead. Anything a cooking station or fermenter takes as input is an
   ingredient and is not protected, even if a cauldron recipe makes it, so mead bases and raw meat move like any stack. Consumables made at any other crafting station (for
   example the food preparation table) are not protected; the log lists them per station. Raw berries and mushrooms are only ingredients, so they are
   moved like any other stack. The list is built from the cauldron recipes in `ObjectDB.m_recipes` and the scene prefabs on first use, cached,
   rebuilt only if the item database changes, and logged once. If it can't be built, every consumable is protected.
   `StackAll` skips equipped items, so those stay with you.
2. Everything else is moved as before: ammo, materials, trophies, armor, and so on. The two switches are independent, so
   food and potions can keep restocking chests while weapons and tools stay put. They share one patch because it is one
   mechanism with a per-category filter.
3. The flag is cleared in a Finalizer, so it can't stay set if the game throws partway through.

**Known limits:** item types are matched by name, so a type this list doesn't know stays unprotected. Cooked food and
potions that really would merge into a partial stack in the container are kept too. Raw meat counts as an ingredient,
so it is moved. Shields and armor are not protected.

## Bees leave a full hive

**Config:** `Building & Travel` → `HideBeesWhenFull`, default `true`
**Patch:** `Beehive.UpdateBees` and `Beehive.RPC_Extract` (Postfix) — `Patches/HideBeesWhenFullPatch.cs`

**Problem:** a full beehive looks exactly like an empty one. The bees show whenever the hive has a valid biome, free
space above it and it is daytime, whatever its honey level, so the only way to know is to walk up and read the hover text.

**Behavior:**

1. A hive is full when its ZDO `level` is at least `m_maxHoney`. The ZDO reaches every client, so nothing is networked.
2. While full, the bee effect is switched off after vanilla has set it. Vanilla never switches it on for a hive that is
   blocked or in the wrong biome, so this only ever hides it.
3. `UpdateBees` runs every 10 s, so the bees lag the honey level by up to that; `RPC_Extract` refreshes them at once on
   the machine that owns the hive.

**Known limits:** the bees are daylight-only in vanilla, so at night every hive looks the same. A first attempt that
also drew honey leaking down the sides was dropped: primitives placed from the mesh bounds floated in the air.

## Beehive snaps to a pole's middle

**Config:** `Building & Travel` → `HiveSnapsToPoleMiddle` (default `true`)
**Patch:** `Piece.GetSnapPoints` (both overloads, Postfix) — `Patches/HiveOnPoleSnapPatch.cs`

**Problem:** a pole only has snap points at its ends, so a beehive cannot be snapped to the middle of one.

**Behavior:**

1. While the local player places a beehive and has picked a manual snap point (Q / E), each nearby piece whose name
   contains "pole" offers an extra snap point halfway along it (the average of its own snap points).
2. The extra points are hidden, untagged child transforms, so vanilla scans and other pieces never see them. Auto-snap
   (no manual point chosen) is unchanged.
3. If the beehive has no snap point of its own, the ghost gets one at its origin so Q has something to select.

**Known limits:** poles are matched by prefab name. The hive snaps to whichever of the pole's end or middle points is
closest to where you aim.

## More bears

**Config:** `Enemies & Drops` → `MoreBears` (default `true`)
**Patch:** `ZNetScene.Awake` (Postfix) — `Patches/MoreBearsPatch.cs`

**Problem:** in the live game the Black Forest bear spawn entry (`Bjorn`, in `_SpawnList_base`) rolls once every 2000 s at
15% by day and 20% by night, with `m_maxSpawned` 1, and `TrophyBjorn` drops at `m_chance` 0.1. Players can search several
forests without finding a bear and rarely see a trophy.

**Behavior:**

1. Once, when the prefabs load, each `Bjorn` spawn entry gets a spawn interval of at most 1000 s and room for two bears
   alive in the area. The spawn chance (15% / 20%) is left alone.
2. Every `TrophyBjorn` entry on a `CharacterDrop` gets a chance of at least 0.2. The level multiplier and the game's
   pseudo-random drop counter still apply.
3. Values are set to a floor or ceiling, not scaled, so loading another world in the same session changes nothing more.

**Known limits:** the toggle is read at load, so turning it off needs a game restart. Spawns and drops are decided by the
machine that owns the zone or creature, so the fix only works in singleplayer or when you host. The Plains `Unbjorn` is
unchanged. `SpawnSystemList` is missing from the compile-time libs, so it is found by name.

## Meaningful tool upgrades

**Config:** `Gear` → `MeaningfulToolUpgrades` (default `false`), `ChopTierBonus` (default `5`) and `PickaxeTierBonus` (default `8`)
**Patch:** `TreeBase.Damage`, `TreeLog.Damage`, `Destructible.Damage`, `MineRock.Damage`, `MineRock5.Damage` (Prefix) —
`Patches/MeaningfulToolUpgradesPatch.cs`

**Problem:** an axe gains 3 chop per upgrade level but only 10 between materials (stone 20, flint 30, bronze 40, iron 50),
so with the wide random skill roll an iron axe barely fells a tree faster than a bronze one. Pickaxes are worse: a level-3
bronze pickaxe (33) equals a level-1 iron one (33), as levels add 4 to 5 against 8 between bronze and iron. Slash damage
is ignored by trees, so only chop counts.

**Behavior:**

1. A hit from the local player whose tool tier is above 0 gets `ChopTierBonus * toolTier` added to its chop damage and
   `PickaxeTierBonus * toolTier` to its pickaxe damage, if it has any. This happens just before the tree, log, stump or rock
   takes the hit.
2. With the defaults, chop at level 1 goes stone 20, flint 35, bronze 50, iron 65 (was 20, 30, 40, 50); pickaxes go
   bronze 33, iron 49, black metal 73 (was 25, 33, 49), so a level-1 iron pickaxe beats a level-3 bronze one (41). The +3 per upgrade level is unchanged.
3. The item's stats, the tooltip and combat damage are untouched, since the bonus is on the hit and only reaches the
   listed targets.

**Known limits:** it applies by tool tier, so battleaxes and later axes get a bonus too (tier 3 and above). A flat bonus
raises everything but keeps the gap in swings between two axes about the same, because swings go as health over damage.
The bonus is not shown in the tooltip.

## Repair all on hold

**Config:** `Crafting` → `RepairAllOnHold` (default `true`)
**Patch:** `CraftingStation.Interact` (Prefix), `Player.Update` (Postfix), `CraftingStation.GetHoverText` (Postfix) — `Patches/RepairAllOnHoldPatch.cs`

**Problem:** repairing is only the Repair button in the crafting window, one item per click, so a full set of worn gear
takes a click per piece.

**Behavior:**

1. Pressing E on a station that can repair (`m_canRepair`, read by name: workbench, forge, ...) no longer opens the
   crafting window at once; the press is recorded instead.
2. If E is still down half a second later (the game repeats the interaction about every 0.2 s), the game's own
   `InventoryGui.RepairOneItem` runs until `HaveRepairableItems` is false, without opening the window. Skill gain,
   effects and messages are the vanilla ones, once per item. It runs once per press, and says "No more item to repair" if
   nothing is damaged.
3. If E comes up first (a tap), `Player.Update` opens the window then, by calling the station's own `Interact`. A tap
   therefore opens it on release instead of on press.
4. The repair reads `Player.m_currentStation`, which is only set while the window is open, so the station is put there for
   the repair and the old value restored. The field is written directly: `SetCraftingStation` would also hide the hand items.
5. The station's hover text gets "(Hold to repair all)" appended while you are within use distance.

**Known limits:** `RepairOneItem`, `HaveRepairableItems`, `InUseDistance`, `m_canRepair` and `m_currentStation` are private or
missing from the compile-time libs, so they are found by name; the patch disables itself with a log line if one is
missing. The hint is plain English, not localized. Looking away from the station before releasing E still opens its window.

## Portal press or hold

**Config:** `Building & Travel` → `PortalPressOrHold` (default `false`)
**Patch:** `TeleportWorld.Interact` (Prefix), `Player.Update` (Postfix), `TeleportWorld.GetHoverText` (Postfix), `TeleportWorld.Awake` (Postfix) — `Patches/PortalPressOrHoldPatch.cs`

**Problem:** E on a portal only opens the tag dialog, so renaming is the only thing the key does, and the portal can only be
aimed at by its frame: the opening is a trigger volume that the hover ray skips.

**Behavior:**

1. Pressing E on a portal records the press instead of opening the dialog.
2. If E is still down half a second later (the game repeats the interaction about every 0.2 s), the game's own
   `Interact` runs again (so the private-area check and the tag dialog are vanilla), once per press. The dialog's input
   field is then made read-only, because a field opened under a held E is typed full of "e" by the key's auto-repeat.
3. `Player.Update` watches for E to come up. A tap calls the game's own `TeleportWorld.Teleport`, so an unlinked portal,
   the no-portal rules, boss events and the metal check behave as when walking in; a tap on an unlinked portal does
   nothing. After a hold, the field becomes editable one frame after E is up, so the last repeated character is not typed.
4. The hover text reads "Go through (Hold to set tag)" in place of the "Set tag" line.
5. On `Awake` a script-less trigger `BoxCollider` copying the portal's `TeleportWorldTrigger` is added as its child on the
   `piece_nonsolid` layer, so the hover ray hits the opening and `GetComponentInParent` resolves it to the portal. Walking
   in still goes through the original trigger. The log line at load names the trigger's layer and whether that layer is in
   `Player.m_interactMask`.

**Known limits:** `Player.m_interactMask` is private, so it is read by name. The extra collider needs the opening to be a
`BoxCollider` and the hover ray to hit trigger colliders; if not, the patch logs a warning and only the frame is hoverable.
The hint is plain English, not localized. `TextInput.m_inputField` is private and its TextMeshPro type is not in the compile-time libs, so it is reached by name and
`readOnly` is set by reflection; the patch disables itself with a log line if the field is missing. "Held" is the `Use` button or
the raw E key. Looking away from the portal before releasing E still sends you through.

## Smelter fill on hold

**Config:** `Crafting` → `SmelterFillOnHold` (default `true`)
**Patch:** `Switch.Interact` (Prefix, Postfix), `Smelter.OnHoverAddOre` / `OnHoverAddFuel` (Postfix) — `Patches/SmelterFillOnHoldPatch.cs`

**Problem:** a smelter, kiln, blast furnace, spinning wheel, eitr refinery or any other `Smelter` takes one E press per
piece of ore or fuel, so filling one is a lot of presses (or a slow hold, on stations that repeat).

**Behavior:**

1. A press on the ore or fuel slot runs as in vanilla and adds one piece. The slot's fill level after the press is
   recorded.
2. While E stays down, the game's own hold repeats on those slots are skipped. Half a second after the press, the game's
   own `OnAddOre` / `OnAddFuel` is called until the slot is full or you run out, once per press. Item checks, messages and
   the add RPC are vanilla.
3. The number of free slots is worked out once, from the larger of the slot's current level and the level the press
   should have reached, because when another player owns the smelter its queue only updates when the RPC arrives. Fuel
   uses the game's own rule that it can go in while at most one below the maximum.
4. Ore goes in in the order the station lists its conversions, the same as vanilla E presses: with copper and tin in the
   bag, the copper goes first. The queue itself takes mixed ore, as in vanilla.
5. The hover text of both slots gets "(Hold to fill)" appended.

**Known limits:** `OnAddOre`, `OnAddFuel`, `GetQueueSize`, `GetFuel` and the hover methods are private, so they are found by
name; the patch disables itself with a log line if one is missing. Two players filling the same smelter at the same time
can put a piece or two past its maximum, as vanilla can. Campfires, hearths and other `Fireplace` pieces are not covered.
The hint is plain English, not localized.
