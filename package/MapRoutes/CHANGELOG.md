# Changelog

All notable changes to this project are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Route drawing on the large map: a quill button above the pin icons switches to draw mode, where the cursor turns
  into a quill and holding the left mouse button draws a dotted red route freehand. Right-drag moves the map while
  drawing. Clicking the quill again, picking a pin icon or closing the map stops drawing. Right-clicking a route
  removes it, unless a pin is under the cursor. Routes show on the large map and the corner map, and are saved with
  the character, per world.
