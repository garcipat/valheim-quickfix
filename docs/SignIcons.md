# Sign icons

Behavior and implementation notes for the SignIcons mod. Player-facing docs: [package/SignIcons/README.md](../package/SignIcons/README.md).

**Config:** none. The mod does one thing, so disabling the mod in the mod manager is the off switch, and no
`.cfg` file is created.
**Patch:** `Sign.OnCheckPermissionCompleted` (Postfix) draws the icons, `Sign.GetText` (Postfix) returns the original
text — `src/SignIcons/Patches/SignIconPatch.cs`

**Problem:** signs can only show text and rich-text styling such as `<color=#hex>`; there is no way to show an item.

**Behavior:**

1. `<icon=ItemPrefabName>` in a sign's text, for example `<icon=SwordIron>`, shows that item's icon at the tag's position.
   The name is the item's prefab name and is case-insensitive. An unknown name is left as visible text.
2. A sign whose text is nothing but valid icon tags (other rich-text tags and whitespace don't count) draws no text;
   the icons are laid in a row, centered in the text widget's rect, as large as it allows (90% of the smaller of
   the rect's height and its width divided by the icon count). An unknown icon name stays visible as text, so it
   keeps the sign from counting as icon-only.
3. Editing a sign shows the original text with its tags, so icons survive an edit.
4. The tag is ordinary sign text, which vanilla already syncs, so the server needs nothing. Players without the mod
   see the raw tag instead of an icon.

**How it works:** the tag is replaced with an invisible placeholder inside a `<link>`; after TextMeshPro lays the text out,
the placeholder's position is read back and a `SpriteRenderer` showing the item's icon is placed there.
Icons sit on the text's own plane (local z = 0) and are drawn above the text by sort order; an earlier version
pushed them forward along z and they floated in front of the sign.
The sign re-applies its text every 2 s, so the icons are only rebuilt when the text actually changes.

**Known limits:** the 50-character sign limit counts the whole tag (`<icon=SwordIron>` is 16 characters).
TextMeshPro is driven through reflection because no NuGet package ships it; if a game update renames the members
it needs, the feature disables itself and logs why. Icon size and position are tuned by eye and unverified on
every sign model.
