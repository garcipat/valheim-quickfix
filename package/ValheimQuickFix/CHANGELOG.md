# Changelog

All notable changes to this project are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Fixed

- Portals no longer get blocked when several players use them one after another: a player standing in the opening is
  invisible and walk-through on your screen until they are gone. Only affects your own view, so each player needs the
  mod. Configurable via `GhostPortalPlayers`.
- With `PortalPressOrHold` on, every portal that loaded wrote an `InvalidCastException` to the log. Nothing was broken
  in-game, but the log was filled with errors.

## [0.3.1] - 2026-10-18

### Changed

- The settings are now grouped into five sections instead of nine: `Inventory`, `Gear`, `Building & Travel`,
  `Enemies & Drops` and `General`. A setting that moved starts at its default value; if you had changed it, set it again
  or delete the config file to start fresh.

### Fixed

- Axes, pickaxes and other tools did no damage to trees, rocks and spawners on the current game version, even with
  `MeaningfulToolUpgrades` off.

## [0.3.0] - 2026-10-17

### Added

- Portals on a button (off by default): tap E on a portal to go through it, hold E to rename it (the dialog opens at once, and typing starts when you let go of E). The opening can be
  aimed at, not only the frame, and the hover text shows "Go through (Hold to set tag)". Walking in still works. Turn it on with
  `PortalPressOrHold`.
- Repair everything at once: hold E on a workbench, forge or any other station that can repair, and every damaged item
  you carry is repaired without opening the crafting window, instead of one per click of the Repair button. A tap opens
  the window as before, when you let go of E. The station's hover text shows
  "(Hold to repair all)". Configurable via `RepairAllOnHold`.
- Meaningful tool upgrades (off by default): a better tool material now counts for more than a higher upgrade level.
  Axes and pickaxes deal extra chop or mining damage to trees, logs, stumps and rocks, growing with the tool's tier, so
  a level-1 iron axe out-chops a level-3 bronze one. Combat damage is unchanged. Turn it on with
  `MeaningfulToolUpgrades`; `ChopTierBonus` (5 per tier by default) and `PickaxeTierBonus` (8 per tier) set the size.
- More bears: bears were tuned so rarely that whole Black Forests could hold none. They now get a spawn roll every
  17 minutes instead of every 33, up to two can live in an area, and they drop their trophy 20% of the time instead of
  10%. Needs the machine running the spawning (singleplayer or host). Configurable via `MoreBears`.
- Beehive snaps to the middle of a pole: while placing a beehive, press Q (or E) to pick its snap point and aim at a
  pole; the pole now offers a snap point halfway along it, besides its ends. Configurable via `HiveSnapsToPoleMiddle`.
- Bees leave a full hive: the bees around a beehive disappear while it holds its maximum honey, so you can see from a
  distance which hives are ready, and return once you take the honey. Configurable via `HideBeesWhenFull`.

## [0.2.0] - 2026-10-17

### Added

- Place stacks keeps your gear: "Place stacks" and holding E on a container no longer move weapons and tools into a
  container that already holds one with the same name, such as a spare hoe into a chest with a hoe, and no longer move
  cooked or brewed food and potions either. Raw food such as berries and mushrooms, ammo, materials and other items are
  moved as before. Configurable separately via
  `PlaceStacksKeepEquipment` and `PlaceStacksKeepConsumables`.
- Floors count as a roof: standing under a floor, such as a second storey or a deck, now counts as being under a roof,
  so it keeps the rain off you. Sheltered and the Rested bonus still need walls, as with any roof. Configurable via
  `FloorsNotLeaky`.
- Skip startup logos: the Coffee Stain and Iron Gate logos shown on launch are skipped. The Unity splash screen stays.
  Configurable via `SkipStartupLogos`.
- Fair resource rounding: with a fractional resource rate in the world settings (such as 1.5x), the number of dropped
  resources is rounded up or down at random instead of always up, so 1.5x averages 1.5 and 4.5 gives 4 or 5.
  Configurable via `FairResourceRounding`.
- Stack on take all: after "Take all" on any container, such as a chest or your tombstone, partial stacks of the same
  item in your inventory are merged instead of taking two slots. Configurable via `StackOnTakeAll`.
- Ignore torches: enemies no longer stop to attack torches as structures. Only affects monsters simulated on your
  machine, so singleplayer. Configurable via `IgnoreTorches`.

### Changed

- Config sections: `RestoreShield` moved from `Weapon switching` to `Combat`, and `IgnoreTorches` from `Combat` to the new
  `Enemies` section. A moved setting goes back to its default once, so check it after updating.
- "Stack on tombstone pickup" is now part of "Stack on take all", which covers tombstones too. The `StackOnTombstonePickup`
  setting is replaced by `StackOnTakeAll`; a leftover `StackOnTombstonePickup` line in your config file is ignored.

## [0.1.0] - 2026-10-15

### Added

- Restore shield: switching from a bow or two-handed weapon back to a one-handed weapon
  re-equips the shield that was dropped. Configurable via `RestoreShield`.
- Push boat: `[E] Push boat` interaction when standing next to a boat, to push it away from you.
  Not available while swimming or standing on the boat. Configurable via `PushBoat`.
- Stack on tombstone pickup: after taking everything from your tombstone, partial stacks of the same item in your
  inventory are merged instead of taking two slots. Configurable via `StackOnTombstonePickup`.
- Restore tombstone slots: taking everything from a tombstone puts each item back in its original slot, moving whatever
  you picked up there since to a free slot. Configurable via `RestoreTombstoneSlots`.
