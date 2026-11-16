# Changelog

All notable changes to this project are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- `VikingMap` in `General` (off by default) puts the small map in the corner on a round, painted Viking shield. The
  map is a little bigger and shows more of the area around you, and the status effect icons move left to make room.

## [0.3.0] - 2026-11-15

### Added

- Pin what you look at: press F8 while looking at berries, mushrooms, copper, tin or silver, tar, a dragon egg, a cave
  or dungeon entrance or a ship to put a pin with its icon on the map. A portal gets the rune pin with the portal's
  tag as its name. Change the key with `PinHotkey` in `General`.

### Changed

- A pin named `Burial chamber` shows the sarcophagus (Dungeon) icon instead of the crypt gate.
- The mod description is a short overview of what the mod does, and the README shows each pin icon next to its name.

### Fixed

- The README's links to its own sections went nowhere in mod managers; they are plain references now.

## [0.2.0] - 2026-11-15

### Added

- Pin icons: a circle pin whose whole name is a known name, such as `Cave`, `Portal`, `Ore` or `Swamp`, shows a
  matching icon instead of the circle. Short names show only the icon; longer names such as `Troll cave` or `Copper`
  keep their name under it. The README lists all names.
- Biome borders: the large map shows faint gray dashed lines where one land biome meets another, only in explored areas
  and not over open water. Off by default; turn them on with `BiomeBorders` in the `General` config section.

## [0.1.0] - 2026-11-13

### Added

- Route drawing on the large map: a quill button above the pin icons switches to draw mode, where the cursor turns
  into a quill and holding the left mouse button draws a dotted red route freehand. Right-drag moves the map while
  drawing. Clicking the quill again, picking a pin icon or closing the map stops drawing. Right-clicking a route
  removes it, unless a pin is under the cursor. Routes show on the large map and the corner map, and are saved with
  the character, per world.
