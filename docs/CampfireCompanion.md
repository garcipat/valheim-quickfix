# Campfire companion

Implementation notes for the CampfireCompanion mod. Player-facing docs:
[package/CampfireCompanion/README.md](../package/CampfireCompanion/README.md).

**Config:** `General.Companion` (default `None`) and `General.Stars` (0 to 2); both apply live in the menu.
**Patch:** `FejdStartup.Start` (Postfix); the companion is placed on the next frame.

**How it works:**

- Creature prefabs are already in memory in the menu, because the menu's asset bundle depends on the shared bundle
  that holds them. `CompanionSpawner` copies the prefab's `Visual` under an inactive holder, applies stars from its
  `LevelEffects`, strips scripts and physics, and `KeepIdle` keeps the animator in its first state.
- Each creature's scale, distance and turn live in `Companion.cs`, tuned by eye. Placement is measured from
  `m_characterPreviewPoint`, not from a light: the character's glowing gear has a point light of its own.
- The character selection camera moves back 2 m while a companion is shown.

**Known limits:** gear a creature gets at runtime (weapons, armour) is not shown.
