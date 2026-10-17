# Changelog

All notable changes to this project are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

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
