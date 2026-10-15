# Sign icons

Behavior and implementation notes for the SignIcons mod. Player-facing docs: [package/SignIcons/README.md](../package/SignIcons/README.md).

**Config:** none. The mod does one thing, so disabling the mod in the mod manager is the off switch, and no
`.cfg` file is created.
**Patch:** `Sign.OnCheckPermissionCompleted` (Postfix) draws the icons, `Sign.GetText` (Postfix) returns the original
text — `src/SignIcons/Patches/SignIconPatch.cs`

**Problem:** signs can only show text and rich-text styling such as `<color=#hex>`; there is no way to show an item.

**Behavior:**

1. `[ItemPrefabName]` in a sign's text, for example `[SwordIron]`, shows that item's icon at the token's position.
   The name is the item's prefab name (letters, digits, underscore) and is case-insensitive. Brackets around a name
   that isn't an item are left as plain text, and a sign containing no real item name isn't touched at all.
2. A sign whose text is nothing but `[Item]` tokens (rich-text tags and whitespace don't count) draws no text;
   the icons fill the text widget's rect instead. Without the mod the sign simply reads `[SwordIron][Iron]`.
3. In such an icon-only sign, each `/` starts a new row: `[A][B]/[C][D]` is two rows of two. All icons share one
   cell size (the smaller of the rect's height divided by the row count and its width divided by the widest row),
   drawn at 90% of the cell, and each row is centered. Empty rows are skipped. In a sign that also has text, a `/`
   is just text (`and/or`), and a closing tag such as `</color>` is not a row break.
4. An unknown `[Name]` stays visible as text, so it also keeps a sign from counting as icon-only.
5. Editing a sign shows the original text with its tokens, so icons survive an edit.
6. The token is ordinary sign text, which vanilla already syncs, so the server needs nothing. Players without the mod
   read the tokens as plain text, which is why the syntax uses brackets rather than a rich-text tag.

**How it works:** in a sign with text, each token is replaced with an invisible placeholder inside a `<link>`; after
TextMeshPro lays the text out, the placeholder's position is read back and a `SpriteRenderer` showing the item's icon
is placed there. Icon-only signs skip the text entirely and place the icons from the widget's rect.
Icons sit on the text's own plane (local z = 0) and are drawn above the text by sort order; an earlier version
pushed them forward along z and they floated in front of the sign.
The sign re-applies its text every 2 s, so the icons are only rebuilt when the text actually changes.

**Known limits:** the 50-character sign limit counts the brackets (`[SwordIron]` is 11 characters). There is no escape
syntax: a `[Name]` that matches an item always becomes an icon.
TextMeshPro is driven through reflection because no NuGet package ships it; if a game update renames the members
it needs, the feature disables itself and logs why. Icon size and position are tuned by eye and unverified on
every sign model.
