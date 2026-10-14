# Valheim QuickFix

By [garcipat](https://github.com/garcipat) · [Source code on GitHub](https://github.com/garcipat/valheim-quickfix)

A small collection of quality-of-life fixes for Valheim. Client-side only: it works on any server, and
other players don't need it.

## Fixes

- **Restore shield** — when a bow or two-handed weapon replaces your shield, switching back to a
  one-handed weapon puts the shield back on automatically.
- **Push boat** — look at a boat from the shore (or from shallow water) and press `E` to push it away
  from you, e.g. off a beach. Hold `E` to keep pushing. Not available while swimming or standing on the boat.

## Configuration

Every fix can be switched off on its own. Open the config in your mod manager (in Gale: the profile's
**Config** page, `dev.pgarcia.valheim.quickfix.cfg`). The file is created the first time you start the game
with the mod installed, and changes apply after a restart.

| Section            | Option          | Default | Description                                          |
| ------------------ | --------------- | ------- | ---------------------------------------------------- |
| `Weapon switching` | `RestoreShield` | `true`  | Re-equip the shield after switching back from a bow. |
| `Boats`            | `PushBoat`      | `true`  | Show the "Push boat" interaction.                    |

## Installation

Install through a mod manager such as Gale or r2modman. It installs
[BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) automatically.

For a manual install, put `ValheimQuickFix.dll` into `Valheim/BepInEx/plugins/`.

## Feedback

Bug reports, ideas, and source code: <https://github.com/garcipat/valheim-quickfix>
