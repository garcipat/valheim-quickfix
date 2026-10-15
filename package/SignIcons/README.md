# Sign Icons

By [garcipat](https://github.com/garcipat) · [Source code on GitHub](https://github.com/garcipat/valheim-quickfix)

Show item icons on signs. Write an item's name in square brackets, such as `[SwordIron]`, in a sign's text and
the item's icon appears at that position.

```
[SwordIron] Weapons
[Iron] x10
```

A sign with **only icons** shows them large, filling the sign. Put a `/` between icons to start a new row:

```
[SwordIron][AxeIron]/[BowFineWood][Torch]
```

Client-side only: it works on any server and nothing needs to be installed on the server. Other players only see
the icons if they have the mod too; without it they simply read `[SwordIron]`, so the sign still makes sense.

## Using it

- Use the item's **prefab name**: `SwordIron`, `Iron`, `Torch`, `Coal`. Capital letters don't matter.
- Brackets around anything that isn't an item stay as plain text, so a typo is easy to spot. Anything in brackets that
  *is* an item name becomes an icon.
- A sign that contains **only icons** shows them large, filling the sign's text area. Several icons share the width,
  and each `/` starts a new row, so `[A][B]/[C][D]` is two rows of two.
- Add any text next to an icon and it shrinks to the size of the letters, so it sits in the line of text. In a sign with
  text, a `/` is just a slash.
- Editing a sign shows the original text with its tags, so icons survive an edit.
- Signs hold 50 characters, and the brackets count: `[SwordIron]` is 11 of them.

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
