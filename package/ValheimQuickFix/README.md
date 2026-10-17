# Valheim QuickFix

By [garcipat](https://github.com/garcipat) · [Source code on GitHub](https://github.com/garcipat/valheim-quickfix)

A small collection of quality-of-life fixes for Valheim. Client-side only: it works on any server, and
other players don't need it.

## Fixes

- **Restore shield** — when a bow or two-handed weapon replaces your shield, switching back to a
  one-handed weapon puts the shield back on automatically.
- **Stack on take all** — after "Take all" on any container, such as a chest or your tombstone, partial stacks of the
  same item in your inventory are merged, so they don't end up as two half stacks in two slots. Full and equipped
  stacks are left alone, and moving single items is unchanged.
- **Restore tombstone slots** — taking everything from a tombstone puts your items back in the slots they came from.
  If you picked something up in that slot since respawning, that item is moved to a free slot instead. Only
  tombstones are affected, not chests.
- **Push boat** — look at a boat from the shore (or from shallow water) and press `E` to push it away
  from you, e.g. off a beach. Hold `E` to keep pushing. Not available while swimming or standing on the boat.
- **Floors count as a roof** — standing under a floor, such as a second storey or a deck, counts as being under a roof,
  so it keeps the rain off you. Sheltered and the Rested bonus still need walls, as with any roof.
- **Skip startup logos** — the Coffee Stain and Iron Gate logos shown on launch are skipped. The Unity splash screen
  can't be skipped.
- **Ignore torches** — enemies no longer stop to attack your torches as if they were buildings. Only monsters
  simulated on your machine are affected, so this works in singleplayer but not against monsters on a server.
- **Place stacks keeps your gear** — "Place stacks" (and holding `E` on a container) no longer moves your weapons
  and tools, or your cooked food and potions, into a container that already holds one with the same name. Your spare hoe
  stays with you instead of ending up in the chest next to the other hoe. Raw food such as berries and mushrooms is
  still moved. Equipment and consumables can be switched on or off separately.

## Configuration

Every fix can be switched off on its own. Open the config in your mod manager (in Gale: the profile's
**Config** page, `dev.pgarcia.valheim.quickfix.cfg`). The file is created the first time you start the game
with the mod installed, and changes apply after a restart.

| Section            | Option          | Default | Description                                          |
| ------------------ | --------------- | ------- | ---------------------------------------------------- |
| `Combat`           | `RestoreShield` | `true`  | Re-equip the shield after switching back from a bow. |
| `Boats`            | `PushBoat`      | `true`  | Show the "Push boat" interaction.                    |
| `Inventory`        | `StackOnTakeAll` | `true` | Merge partial stacks after "Take all" on any container. |
| `Inventory`        | `RestoreTombstoneSlots` | `true` | Put tombstone items back in their original slots. |
| `Building`         | `FloorsNotLeaky` | `true` | A floor above you counts as a roof. |
| `Startup`          | `SkipStartupLogos` | `true` | Skip the Coffee Stain and Iron Gate logos. |
| `Enemies`          | `IgnoreTorches` | `true`  | Enemies don't attack torches as structures. |
| `Inventory`        | `PlaceStacksKeepEquipment` | `true` | "Place stacks" leaves weapons and tools alone. |
| `Inventory`        | `PlaceStacksKeepConsumables` | `true` | "Place stacks" leaves cooked food and potions alone. |

## Installation

Install through a mod manager such as Gale or r2modman. It installs
[BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) automatically.

For a manual install, put `ValheimQuickFix.dll` into `Valheim/BepInEx/plugins/`.

## Feedback

Bug reports, ideas, and source code: <https://github.com/garcipat/valheim-quickfix>

## License

MIT. See the `LICENSE` file in the package, or the source repository.
