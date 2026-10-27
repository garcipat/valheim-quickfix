# Valheim QuickFix

By [garcipat](https://github.com/garcipat) · [Source code on GitHub](https://github.com/garcipat/valheim-quickfix)

A small collection of quality-of-life fixes for Valheim. Client-side only: it works on any server, and
other players don't need it.

## Fixes

- **Restore shield** — when a bow or two-handed weapon replaces your shield, switching back to a
  one-handed weapon puts the shield back on automatically.
- **Stack on take all** — after "Take all" on any container, such as a chest or your tombstone, partial stacks of the
  same item in your inventory are merged, so they don't end up as two half stacks in two slots. Full and equipped
  stacks are left alone, and moving single items is unchanged.
- **Restore tombstone slots** — taking everything from a tombstone puts your items back in the slots they came from.
  If you picked something up in that slot since respawning, that item is moved to a free slot instead. Only
  tombstones are affected, not chests.
- **Push boat** — look at a boat from the shore (or from shallow water) and press `E` to push it away
  from you, e.g. off a beach. Hold `E` to keep pushing. Not available while swimming or standing on the boat.
- **Floors count as a roof** — standing under a floor, such as a second storey or a deck, counts as being under a roof,
  so it keeps the rain off you. Sheltered and the Rested bonus still need walls, as with any roof.
- **Skip startup logos** — the Coffee Stain and Iron Gate logos shown on launch are skipped. The Unity splash screen
  can't be skipped.
- **Faster stamina while sitting** (off by default) — sitting on the ground or in a chair regenerates stamina 1.5
  times as fast. The speed-up can be set from 1x to 2x.
- **Ignore torches** — enemies no longer stop to attack your torches as if they were buildings. Only monsters
  simulated on your machine are affected, so this works in singleplayer but not against monsters on a server.
- **Fair resource rounding** — with a fractional resource rate in the world settings (such as 1.5x), the number of
  dropped resources is rounded up or down at random instead of always up, so 1.5x really averages 1.5 and 4.5 gives 4
  or 5. Only drops rolled on your machine are affected.
- **More bears** (off by default) — bears get a spawn roll every 17 minutes instead of every 33, up to two can live in an area, and they
  drop their trophy 20% of the time instead of 10%. Only works in singleplayer or when you host.
- **More Writhans** (off by default) — Writhans get a spawn roll every 25 minutes instead of every 67 with a 15%
  chance instead of 10%, and up to two can live in an area. Only works in singleplayer or when you host.
- **Meaningful tool upgrades** (off by default) — a better tool material counts for more than a higher upgrade level.
  Axes and pickaxes deal extra chop or mining damage to trees, logs, stumps and rocks, growing with the tool's tier, so
  a level-1 iron axe out-chops a level-3 bronze one. Combat damage is unchanged. The bonus per tier can be set
  separately for axes and pickaxes.
- **Place stacks keeps your gear** — "Place stacks" (and holding `E` on a container) no longer moves your weapons
  and tools, or your cooked food and potions, into a container that already holds one with the same name. Your spare hoe
  stays with you instead of ending up in the chest next to the other hoe. Raw food such as berries and mushrooms is
  still moved. Equipment and consumables can be switched on or off separately.
- **Bees leave a full hive** — the bees around a beehive disappear while it holds its maximum honey, so you can see
  from a distance which hives are ready. They return once you take the honey.
- **Beehive snaps to a pole's middle** — while placing a beehive, press `Q` (or `E`) to pick its snap point and aim
  at a pole: the pole now also offers a snap point halfway along it, not just at its ends.
- **Repair all on hold** — hold `E` on a workbench, forge or any other station that can repair to repair every damaged
  item you carry at once, instead of one per click of the Repair button. A tap opens the crafting window as before.
- **Fill smelters on hold** — hold `E` on the ore or fuel slot of a smelter, kiln, blast furnace, spinning wheel or
  other processing station to fill it as full as it goes in one go. A tap still adds one piece.
- **Portal press or hold** (off by default) — tap `E` on a portal to go through it, hold `E` to rename it. The opening
  of the portal can be aimed at, not only its frame. Walking in still works.
- **Build without a workbench** (off by default) — build and deconstruct workbench pieces without a workbench nearby.
  Pieces that need a stonecutter, forge or other station still need it. You still need the materials, and you must
  have seen a workbench once to unlock its pieces.
  Only works in a singleplayer game ("Start server" unchecked); on a multiplayer server nothing changes.
- **Portals don't get blocked** — a player who walks into a portal stays solid in its opening for a couple of seconds
  and blocks whoever follows. While they stand there they are invisible and walk-through on your screen. Only affects
  your own view, so each player needs the mod.

## Configuration

Every fix can be switched off on its own. Open the config in your mod manager (in Gale: the profile's
**Config** page, `dev.pgarcia.valheim.quickfix.cfg`). The file is created the first time you start the game
with the mod installed, and changes apply after a restart.

| Section            | Option          | Default | Description                                          |
| ------------------ | --------------- | ------- | ---------------------------------------------------- |
| `Gear`           | `RestoreShield` | `true`  | Re-equip the shield after switching back from a bow. |
| `Building & Travel`            | `PushBoat`      | `true`  | Show the "Push boat" interaction.                    |
| `Inventory`        | `StackOnTakeAll` | `true` | Merge partial stacks after "Take all" on any container. |
| `Inventory`        | `RestoreTombstoneSlots` | `true` | Put tombstone items back in their original slots. |
| `Building & Travel`         | `FloorsNotLeaky` | `true` | A floor above you counts as a roof. |
| `General`          | `SkipStartupLogos` | `true` | Skip the Coffee Stain and Iron Gate logos. |
| `General`          | `SittingStaminaRegen` | `false` | Stamina regenerates faster while sitting. |
| `General`          | `SittingStaminaMultiplier` | `1.5` | How much faster, from 1 to 2 in steps of 0.1. |
| `Enemies & Drops`          | `IgnoreTorches` | `true`  | Enemies don't attack torches as structures. |
| `Enemies & Drops`  | `FairResourceRounding` | `true` | Round fractional resource drops up or down at random. |
| `Enemies & Drops`  | `MoreBears` | `false` | Bears spawn more often and drop their trophy more often. |
| `Gear`             | `MeaningfulToolUpgrades` | `false` | Better axe and pickaxe materials deal extra chop / mining damage. |
| `Gear`             | `ChopTierBonus` | `5` | Chop damage added per axe tier (0 to 50). |
| `Gear`             | `PickaxeTierBonus` | `8` | Mining damage added per pickaxe tier (0 to 50). |
| `Inventory`        | `PlaceStacksKeepEquipment` | `true` | "Place stacks" leaves weapons and tools alone. |
| `Inventory`        | `PlaceStacksKeepConsumables` | `true` | "Place stacks" leaves cooked food and potions alone. |
| `Building & Travel`         | `HideBeesWhenFull` | `true` | Full beehives lose their bees. |
| `Building & Travel`         | `HiveSnapsToPoleMiddle` | `true` | Beehives can snap to the middle of a pole. |
| `Crafting`         | `RepairAllOnHold` | `true` | Hold E on a workbench or forge to repair every item at once. |
| `Building & Travel`          | `PortalPressOrHold` | `false` | Tap E on a portal to go through, hold E to rename it. |
| `Crafting`         | `SmelterFillOnHold` | `true` | Hold E on a smelter's ore or fuel slot to fill it at once. |
| `Building & Travel`  | `IgnoreWorkbench` | `false` | Build and deconstruct workbench pieces without a workbench nearby (singleplayer only). |
| `Building & Travel`          | `GhostPortalPlayers` | `true` | Players standing in a portal opening are invisible and walk-through, so they do not block it. |

## Installation

Install through a mod manager such as Gale or r2modman. It installs
[BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) automatically.

For a manual install, put `ValheimQuickFix.dll` into `Valheim/BepInEx/plugins/`.

## Feedback

Bug reports, ideas, and source code: <https://github.com/garcipat/valheim-quickfix>

## License

MIT. See the `LICENSE` file in the package, or the source repository.
