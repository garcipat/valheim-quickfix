# Sign Icons

By [garcipat](https://github.com/garcipat) · [Source code on GitHub](https://github.com/garcipat/valheim-quickfix)

Show item icons on signs. Write `<icon=ItemName>` in a sign's text, the same way you would write
`<color=#hex>` to color it, and the item's icon appears at that position.

```
<icon=SwordIron> Weapons
<icon=Iron> x10
```

Client-side only: it works on any server and nothing needs to be installed on the server. Other players only see
the icons if they have the mod too; without it they see the raw tag.

## Using it

- Use the item's **prefab name**: `SwordIron`, `Iron`, `Torch`, `Coal`. Capital letters don't matter.
- An unknown name stays visible as text, so a typo is easy to spot.
- A sign that contains **only icons** shows them large, filling the sign's text area (several icons share the width).
  Add any text next to an icon and it shrinks to the size of the letters, so it sits in the line of text.
- Editing a sign shows the original text with its tags, so icons survive an edit.
- Signs hold 50 characters, and a tag counts fully: `<icon=SwordIron>` is 16 of them.

## Configuration

There are no settings. To turn the icons off, disable the mod in your mod manager.

## Installation

Install through a mod manager such as Gale or r2modman. It installs
[BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) automatically.

For a manual install, put `SignIcons.dll` into `Valheim/BepInEx/plugins/`.

## Feedback

Bug reports, ideas, and source code: <https://github.com/garcipat/valheim-quickfix>

## License

MIT. See the `LICENSE` file in the package, or the source repository.
