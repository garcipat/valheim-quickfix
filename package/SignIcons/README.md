# Sign Icons

By [garcipat](https://github.com/garcipat) · [Source code on GitHub](https://github.com/garcipat/valheim-quickfix)

Show item icons on signs, and open the chest a sign is placed on. Write an item's name in square brackets, such as `[SwordIron]`, in a sign's text and
the item's icon appears at that position.

```
[SwordIron] Weapons
[Iron] x10
```

A sign with **only icons** shows them large, filling the sign. Put a `/` between icons to start a new row:

```
[SwordIron][AxeIron]/[BowFineWood][Torch]
```

Nothing needs to be installed on the server. Other players only see
the icons if they have the mod too; without it they simply read `[SwordIron]`, so the sign still makes sense.

## Using it

- Use the item's **prefab name**: `SwordIron`, `Iron`, `Torch`, `Coal`. Capital letters don't matter.
  The [full list of item names](https://github.com/garcipat/valheim-quickfix/blob/main/package/SignIcons/ItemList.md)
  shows what to type for each item.
- Brackets around anything that isn't an item stay as plain text, so a typo is easy to spot. Anything in brackets that
  *is* an item name becomes an icon.
- A sign that contains **only icons** shows them large, filling the sign's text area. Several icons share the width,
  and each `/` starts a new row, so `[A][B]/[C][D]` is two rows of two.
- Add any text next to an icon and it shrinks to the size of the letters, so it sits in the line of text. In a sign with
  text, a `/` is just a slash.
- A sign with **a single icon** and nothing else, such as `[Iron]`, is drawn at half width, so it makes a neat label on
  a chest. It keeps its full height, so the icon stays large. This only changes how it looks on your screen: you aim at it as before, and other players see a normal
  sign.
- Editing a sign shows the original text with its tags, so icons survive an edit.
- Signs hold 50 characters, and the brackets count: `[SwordIron]` is 11 of them.

## Signs on chests

Without this mod, signs can't be placed on chests at all. With it, you can put a sign on a chest and the chest holds it
up. Looking at a sign that has a chest right behind it:

- **E** opens the chest.
- **Shift+E** edits the sign. Shift is the game's alternate key, so it follows your Controls settings and works on a
  controller too.

The hover text shows both keys. Signs with no chest behind them work exactly as before.

**On a server where not everyone has the mod:** the game lets one nearby player check whether buildings around them are
still held up. If that player doesn't have the mod, a sign held only by a chest has nothing holding it, and it breaks.
Signs that are also attached to a wall or beam are safe. Everything else about this mod is visible only to you.

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
