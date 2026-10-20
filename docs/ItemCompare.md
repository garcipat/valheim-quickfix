# Item compare

Behavior and implementation notes for the ItemCompare mod. Player-facing docs: [package/ItemCompare/README.md](../package/ItemCompare/README.md).

**Config** (`dev.pgarcia.valheim.itemcompare.cfg`, section `General`): `Mode` (`Always` default / `OnKeyPress`) and
`CompareKey` (`KeyboardShortcut`, default Left Shift, used only in `OnKeyPress`). There is no enable flag; disabling
the mod is the off switch.
**Patch:** `ItemDrop.ItemData.GetTooltip` instance overload (Postfix) — `src/ItemCompare/Patches/TooltipComparePatch.cs`.
The numbers and lines come from `src/ItemCompare/StatDiff.cs`.

**Behavior:**

1. The comparison is appended to the tooltip text; the vanilla text is never changed.
2. An item compares with the local player's equipped item of the same group: each armor type and the trinket / utility
   slot with itself, a shield with a shield, a torch with a torch, a tool with a tool, and one-handed / two-handed /
   bow weapons with each other. Other item types, equipped items and items with nothing equipped in their group get
   nothing.
3. Each stat that differs prints `label: old -> new`, with `new` in green (`#6FE36F`) if better and red (`#FF5A5A`)
   if worse. Stats equal once formatted print nothing, and a stat only one item has is compared with zero.
4. Higher is better except weight and stamina use, where lower is better. Movement penalty is a negative percentage,
   so a smaller penalty is higher.
5. Stats: damage by type, knockback, stamina use, block armor, block force, parry bonus, armor, weight, max durability,
   movement modifier. Labels are the game's `$item_*` / `$inventory_*` localization keys; only the `vs.` header is
   English.

**How it works:** values come from `ItemData` getters and fields, not from parsing the vanilla text. `InventoryGrid`
rebuilds the hovered item's tooltip every frame, so the key is read live in `OnKeyPress` mode. Only the instance
`GetTooltip` is patched; the static overload is also used for recipes and chained tooltips. It is found by name because
its signature differs between the compile-time libs and the live game. `GetWeight` and the equipped flag are reached
by name for the same reason (`ItemAccess`). A failure in the postfix is logged and leaves the tooltip alone.

**Known limits:** crafting and upgrade tooltips (recipe panel) are not compared. The arrow is the ASCII `->` because
the game's font is not known to have an arrow glyph. Not verified in game yet.
