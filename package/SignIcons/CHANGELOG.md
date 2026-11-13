# Changelog

All notable changes to this project are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- A list of every item name you can put on a sign, linked from the mod description.
- Signs on chests: a sign placed on a chest no longer blocks it. Looking at the sign, `E` opens the chest behind it
  and `Shift+E` edits the sign. Signs with no chest behind them work as before.
- Signs on chests: signs can be placed on chests, and the chest holds them up. Looking at such a sign, `E` opens the
  chest behind it and `Shift+E` edits the sign. Signs with no chest behind them work as before. On a server, a sign
  held only by a chest can break if a nearby player without the mod owns that area.

## [0.1.0] - 2026-10-15

### Added

- Sign icons: `[ItemPrefabName]` in sign text, for example `[SwordIron]`, shows the item's icon. Without the mod the
  sign still reads as plain text. A sign holding only icons shows them large, and `/` between icons starts a new row,
  for example `[SwordIron][Iron]/[Coal][Wood]`.
  No settings; disable the mod to turn it off.
