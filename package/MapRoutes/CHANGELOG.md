# Changelog

All notable changes to this project are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

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
