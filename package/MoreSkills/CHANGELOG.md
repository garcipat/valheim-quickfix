# Changelog

All notable changes to this project are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

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
