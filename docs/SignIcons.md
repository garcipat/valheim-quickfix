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

## Signs on chests

**Patch:** `Sign.Interact` (Prefix) and `Sign.GetHoverText` (Postfix) — `src/SignIcons/Patches/SignOnChestPatch.cs`;
`Player.UpdatePlacementGhost` and `WearNTear.UpdateSupport` (Prefix + Finalizer) — `src/SignIcons/Patches/SignOnChestSupportPatch.cs`

**Problem:** a sign can't be placed on a chest, and a sign in front of a chest takes the crosshair, so the chest behind
it can't be opened.

**Placing and support** (`SignOnChestSupportPatch.cs`): chests have `WearNTear.m_supports == false`, which makes
`Player.UpdatePlacementGhost` mark a sign aimed at them invalid and makes `WearNTear.UpdateSupport` ignore them as
support. A prefix sets `m_supports = true` on the chest for the duration of those two calls (the chest under the
placement ray; chests within 1.5 m of a sign), and a finalizer restores it. Nothing else about the checks changes, so
every other placement rule still applies and the chest passes on its own support with the usual loss.

This is the one part of the mod that isn't only local: `UpdateSupport` runs on the ZDO owner of the sign, which is
usually the nearby player who loaded the area first. If that player doesn't have the mod, the chest gives no support
and a sign held only by the chest breaks. The README says so.

**Behavior:**

1. When the hovered sign has a `Container` right behind it, Use opens the container and AltPlace + Use (Shift+E by
   default) edits the sign. The game already passes that key to
   `Interact` as `alt`; neither `Sign` nor `Container` used it.
2. The hover text keeps the sign's text, then shows the container's own hover text (name, empty marker, Open key)
   and an `AltPlace + Use` line for the sign. If vanilla's hover text isn't in its usual shape (for example without
   ward access, where it has no Use line), it is left alone.
3. Opening goes through `Container.Interact`, so ward and private-chest checks still apply.

**How it works:** the chest is found along the same camera ray the game uses to pick the hovered object
(`Player.FindHoverObject`: camera position and forward, `m_interactMask`): after the sign's own colliders, the next hit
must be a `Container` within 0.75 m of the sign's surface. Anything else in between (a wall) means no chest. The
result is cached per frame, since hover text is rebuilt every frame.

**Known limits:** the hover line always names `$KEY_AltPlace`. On a non-classic gamepad layout the game reads AltKeys
for `alt` instead (vanilla `ItemStand` switches the hint via `ZInput.IsNonClassicFunctionality`, which the reference
assemblies in `libs/` predate), so the hint may name the wrong button there.
