using System;
using System.Reflection;
using HarmonyLib;

namespace ValheimItemCompare.Patches
{
    /// <summary>
    /// Writes the difference behind each stat of the recipe description in the crafting panel, as "54 (+7)". The panel's
    /// text shrinks to fit, so a separate block underneath would make everything small. A new craft compares the item it
    /// makes with what the player wields or wears; an upgrade compares the next quality level with the item's current one.
    /// </summary>
    /// <remarks>
    /// UpdateRecipe runs every frame while the panel is open and rewrites the description from scratch, so editing it
    /// here never stacks up and a held compare key shows up live. The selected recipe and the description field changed
    /// shape since the compile-time libs (KeyValuePair to RecipeDataPair, Text to TMP_Text), so both are read by name.
    /// </remarks>
    [HarmonyPatch(typeof(InventoryGui), "UpdateRecipe")]
    internal static class RecipeComparePatch
    {
        private static readonly FieldInfo DescriptionField = AccessTools.Field(typeof(InventoryGui), "m_recipeDecription");

        private static void Postfix(InventoryGui __instance)
        {
            try
            {
                var player = Player.m_localPlayer;
                if (player == null || DescriptionField == null)
                {
                    return;
                }

                if (Plugin.Mode.Value == CompareMode.OnKeyPress && !Plugin.CompareKey.Value.IsPressed())
                {
                    return;
                }

                var selected = Traverse.Create(__instance).Field("m_selectedRecipe");
                var recipe = selected.Property("Recipe").GetValue<Recipe>() ?? selected.Property("Key").GetValue<Recipe>();
                if (recipe == null || recipe.m_item == null)
                {
                    return;
                }

                var upgrading = selected.Property("ItemData").GetValue<ItemDrop.ItemData>()
                    ?? selected.Property("Value").GetValue<ItemDrop.ItemData>();
                ItemDrop.ItemData preview;
                ItemDrop.ItemData worn;
                string title;
                if (upgrading != null)
                {
                    preview = upgrading.Clone();
                    preview.m_quality = upgrading.m_quality + 1;
                    worn = upgrading;
                    title = upgrading.m_shared.m_name + " (" + upgrading.m_quality + ")";
                }
                else
                {
                    preview = recipe.m_item.m_itemData.Clone();
                    preview.m_quality = 1;
                    ItemAccess.SetCraftWorldLevel(preview);
                    worn = StatDiff.FindWorn(preview, player);
                    title = worn?.m_shared.m_name;
                }

                var description = DescriptionField.GetValue(__instance);
                var text = description == null ? null : AccessTools.Property(description.GetType(), "text");
                if (worn != null && text != null)
                {
                    text.SetValue(description, StatDiff.Inline((string)text.GetValue(description), preview, worn, title));
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Recipe comparison failed: {e}");
            }
        }
    }
}
