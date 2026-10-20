# Item Compare

By [garcipat](https://github.com/garcipat) · [Source code on GitHub](https://github.com/garcipat/valheim-quickfix)

Hover a weapon, shield, tool or piece of armor and its tooltip shows how it compares with the item you are wearing
in that slot. Every stat that differs gets a line with the old and the new value, and the new value is coloured:
**green** when it is better, **red** when it is worse.

```
vs. Iron Sword
Slash: 54 -> 61
Weight: 4.0 -> 5.0
```

Stats that are the same are left out, and so is anything the game already shows: the normal tooltip is unchanged and
the comparison is added underneath it. Lower is better for weight and stamina use, so a lighter item shows green.

Client-side only: it works on any server and nothing needs to be installed on the server. Other players are not
affected.

## Using it

- Open your inventory (or a chest) and hover an item you are not wearing.
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
