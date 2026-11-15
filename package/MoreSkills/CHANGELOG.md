# Changelog

All notable changes to this project are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Exploring skill: level it by uncovering new map, on foot or by ship. The area you uncover around you grows as it
  levels, up to twice as wide at level 100 (`MaxRadiusBonus`). Known ground, portals and shared maps give nothing.
- Brewing skill: level it by filling and tapping fermenters. A tap has a growing chance to give a third more potions
  (8 instead of 6), up to 30% at level 100 (`MaxBonusChance`). The first batch reaches level 1, and each level after
  that takes a little more brewing.

### Changed

- Hauling and Paddling level about 2.5 times faster, Endurance about 1.75 times, and Landscaping 4 times (each hoe
  use now counts as much as a pickaxe hit). They were too slow for how rarely you pull a cart, paddle, walk overweight
  or use the hoe. Your current levels are kept.
- Hauling levels at the same pace with any load, and at half pace with an empty cart. Before, a lighter load levelled
  slower, only a cart carrying 300 weight or more levelled at full pace, and an empty cart gave nothing.

## [0.1.0] - 2026-10-25

### Added

- Hauling skill: level it by pulling a loaded cart (faster with heavier loads, nothing for an empty cart). The cargo
  of a cart you pull weighs less on you as it levels, up to half at level 100 (`MaxCargoReduction`). At level 0 carts
  are vanilla. Turning the skill off hides it and stops its levelling and effect, but keeps your level.
- Landscaping skill: level it by using the hoe. The hoe costs less stamina as it levels, up to half at level 100.
- Endurance skill: level it by walking over the weight limit. Walking overweight drains less stamina as it levels, up
  to half at level 100 (`MaxDrainReduction`).
- Paddling skill: level it by paddling a ship without the sail. You paddle faster as it levels, up to 40% at level
  100 (`MaxPaddleBonus`). Sailing is unchanged.
