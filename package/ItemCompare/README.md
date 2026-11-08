# Item Compare

By [garcipat](https://github.com/garcipat) · [Source code on GitHub](https://github.com/garcipat/valheim-quickfix)

Hover a weapon, shield, tool or piece of armor and its tooltip shows how it compares with the item you are wearing
in that slot. Every stat that differs gets the difference right behind its value, coloured **green** when it is
better and **red** when it is worse:

```
Slash: 61 (+7)
Weight: 5.0 (+1.0)
vs. Iron Sword
```

Stats that are the same get nothing, and the game's own values and lines stay as they are. Lower is better for weight and stamina use, so a lighter item shows green.

Client-side only: it works on any server and nothing needs to be installed on the server. Other players are not
affected.

## Using it

- Open your inventory (or a chest) and hover an item you are not wearing.
- At a workbench, forge or other crafting station, select a recipe: its description shows the differences the same
  way. A new item compares with what you wear; an
  upgrade compares the next level with the item's current level.
- Weapons you have put away (with R, or because a crafting station put them on your back) still count as the ones
  you wield.
- Armor compares with the piece in the same slot, a shield with your shield, and a one-handed, two-handed or bow
  weapon with the weapon you wield. Torches and tools compare with their own kind.
- Items you are already wearing, and things that are not equipment, show no comparison.

## Configuration

The settings are in `Valheim/BepInEx/config/dev.pgarcia.valheim.itemcompare.cfg`, or in your mod manager's config editor.

| Setting | Default | What it does |
| ------- | ------- | ------------ |
| `Mode` | `Always` | `Always` adds the comparison to every tooltip. `OnKeyPress` only shows it while the compare key is held. |
| `CompareKey` | Left Shift | The key to hold in `OnKeyPress` mode. |

To turn the comparison off completely, disable the mod in your mod manager.

## Installation

Install through a mod manager such as Gale or r2modman. It installs
[BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) automatically.

For a manual install, put `ItemCompare.dll` into `Valheim/BepInEx/plugins/`.

## Feedback

Bug reports, ideas, and source code: <https://github.com/garcipat/valheim-quickfix>

## License

MIT. See the `LICENSE` file in the package, or the source repository.
