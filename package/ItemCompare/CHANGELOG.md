# Changelog

All notable changes to this project are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Changed

- The comparison is now shown right behind each value, like `Slash: 61 (+7)` with only the number coloured, instead of
  as `old -> new` lines under the tooltip, so the tooltip only grows by one line.

### Added

- The crafting panel shows the comparison too: each stat of the selected recipe gets the difference behind it, like
  `Slash: 61 (+7)`. A new item compares with what you wear, and an upgrade compares the next level with the item's
  current level.

### Fixed

- Weapons, shields and tools that are put away (sheathed with R, or hidden by a crafting station) are now still
  compared against instead of showing no comparison.

## [0.1.0] - 2026-10-20

### Added

- Item comparison: hovering a weapon, shield, tool or armor piece shows how its stats compare with the item you
  wear in that slot, as `old -> new` with the new value in green when it is better and red when it is worse. Stats
  that are equal are left out. The `Mode` setting chooses between always showing it (default) and only while the
  `CompareKey` (default Left Shift) is held.
