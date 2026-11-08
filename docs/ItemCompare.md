# Item compare

Behavior and implementation notes for the ItemCompare mod. Player-facing docs: [package/ItemCompare/README.md](../package/ItemCompare/README.md).

**Config** (`dev.pgarcia.valheim.itemcompare.cfg`, section `General`): `Mode` (`Always` default / `OnKeyPress`) and
`CompareKey` (`KeyboardShortcut`, default Left Shift, used only in `OnKeyPress`). There is no enable flag; disabling
the mod is the off switch.
**Patches:** `ItemDrop.ItemData.GetTooltip` instance overload (Postfix) — `src/ItemCompare/Patches/TooltipComparePatch.cs`;
`InventoryGui.UpdateRecipe` (Postfix) — `src/ItemCompare/Patches/RecipeComparePatch.cs`.
The numbers and lines come from `src/ItemCompare/StatDiff.cs`.

**Behavior:**

1. The comparison is written into the tooltip text: vanilla lines and values are kept, and the difference is inserted
   behind each matching value (rule 3).
2. An item compares with the local player's equipped item of the same group: each armor type and the trinket / utility
   slot with itself, a shield with a shield, a torch with a torch, a tool with a tool, and one-handed / two-handed /
   bow weapons with each other. Other item types, equipped items and items with nothing equipped in their group get
   nothing. Hand items the player put away (sheathing with R, or a crafting station hiding them) still count as
   equipped: they are read from `Humanoid.m_hiddenLeftItem` / `m_hiddenRightItem`.
3. Each stat that differs gets ` (+15)` behind the vanilla value on its line, with only the signed number in green
   (`#6FE36F`) if better and red (`#FF5A5A`) if worse; the brackets keep the default colour. The line is found by its
   label (the `$` key, or its localized text in the recipe panel) at the start of a line, and the delta goes after the
   line's first `</color>`. Stats with no vanilla line (movement modifier on some items, a stat only the worn item
   has, which is compared with zero) get a short `label: value (delta)` line at the end. A one-line grey
   `vs. <name>` footer names the compared item and is only added when something differs. Stats equal once formatted
   get nothing.
4. Higher is better except weight and stamina use, where lower is better. Movement penalty is a negative percentage,
   so a smaller penalty is higher.
5. Stats: damage by type, knockback, stamina use, block armor, block force, parry bonus, armor, weight, max durability,
   movement modifier. Labels are the game's `$item_*` / `$inventory_*` localization keys; only the `vs.` header is
   English.
6. Crafting panel: the same inline format (the panel's text shrinks to fit, so a block underneath made it tiny). A new
   recipe compares a quality-1 copy of the item it makes (at the current world level) with the
   equipped item, as in rule 2. An upgrade compares a copy at the next quality with the item being upgraded, footed
   `vs. <name> (<current quality>)`, whether or not that item is equipped.

**How it works:** values come from `ItemData` getters and fields, not from parsing the vanilla text; the text is
only searched for where to insert each difference. `InventoryGrid`
rebuilds the hovered item's tooltip every frame, so the key is read live in `OnKeyPress` mode. Only the instance
`GetTooltip` is patched; the static overload is also used for recipes and chained tooltips. It is found by name because
its signature differs between the compile-time libs and the live game. `GetWeight` and the equipped flag are reached
by name for the same reason (`ItemAccess`). The recipe panel is covered by its own postfix on `UpdateRecipe`, which
rewrites the description every frame, so the differences never stack up. The selected
recipe (`KeyValuePair` in the libs, `RecipeDataPair` live) and the description (`Text` in the libs, `TMP_Text` live) are
read by name. A failure in either postfix is logged and leaves the text alone.

**Known limits:** in the inventory the durability line shows the current percentage first, so a max-durability
difference reads `100% (+200) (1000/1200)`. A vanilla line format change that drops the `label:` prefix moves that
stat to the extra lines at the end instead of breaking the tooltip.
