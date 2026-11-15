using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// With a crafting skill, each craft of a stackable item has a <c>skill factor x m_craftBonusChance</c> chance of a
    /// bonus, but the bonus is a flat <c>m_craftBonusAmount</c> (1): a recipe that makes 20 arrows gets 1 more, a recipe
    /// that makes 1 gets twice as many. The vanilla multi-craft loop also adds its running total on every hit, so three
    /// hits give 1 + 2 + 3. Here each hit gives one more batch of the recipe instead (20 arrows, 1 mushroom dish), and
    /// hits only add up.
    /// </summary>
    /// <remarks>
    /// A Prefix on <c>InventoryGui.DoCrafting</c> rolls the hits the way the game does and sets the bonus chance to 0 for
    /// the call, so the game's own roll never hits. A Postfix on the first <c>Recipe.GetAmount</c> inside it adds the
    /// bonus to the amount the game checks for space and adds to the inventory. When that item reaches the inventory,
    /// the Postfix on <c>DoCrafting</c> shows the game's "+N" text and effect. Cooking stations use the same chance
    /// outside crafting and keep the vanilla +1, which is already one batch there.
    /// </remarks>
    internal static class ProportionalCraftBonusPatch
    {
        // All newer than the compile-time libs.
        private static readonly FieldInfo BonusChance = AccessTools.Field(typeof(InventoryGui), "m_craftBonusChance");
        private static readonly FieldInfo BonusEffect = AccessTools.Field(typeof(InventoryGui), "m_craftBonusEffect");
        private static readonly FieldInfo CraftingSkill = AccessTools.Field(typeof(CraftingStation), "m_craftingSkill");
        private static readonly MethodInfo GetAmount = AccessTools.Method(typeof(Recipe), "GetAmount");
        private static readonly MethodInfo ShowTextString = AccessTools.Method(
            typeof(DamageText), "ShowText",
            new[] { typeof(DamageText.TextType), typeof(Vector3), typeof(string), typeof(bool) });
        // DamageText.TextType.Bonus; the compile-time libs predate it.
        private const DamageText.TextType BonusText = (DamageText.TextType)7;

        private static readonly FieldInfo MultiCrafting = AccessTools.Field(typeof(InventoryGui), "m_multiCrafting");
        private static readonly FieldInfo MultiCraftAmount = AccessTools.Field(typeof(InventoryGui), "m_multiCraftAmount");

        private static bool _active;
        private static float _savedChance;
        private static Recipe _recipe;
        private static int _hits;
        private static int _bonus;
        private static bool _amountApplied;
        private static bool _added;

        private static bool Available(string part)
        {
            if (BonusChance != null && CraftingSkill != null && GetAmount != null && AddItemTarget() != null)
            {
                return true;
            }

            Plugin.Log.LogWarning($"ProportionalCraftBonus disabled ({part}): the crafting bonus fields are missing in this game version");
            return false;
        }

        // Inventory.AddItem(string name, int stack, int quality, int variant, long crafterID, string crafterName,
        // Vector2i position, bool cheated, bool pickedUp, bool dropIfFullInv): the overload DoCrafting adds the item with.
        private static MethodBase AddItemTarget()
        {
            foreach (var method in typeof(Inventory).GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.Name != nameof(Inventory.AddItem) || method.ReturnType != typeof(ItemDrop.ItemData))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length >= 7 && parameters[0].ParameterType == typeof(string)
                    && parameters[1].ParameterType == typeof(int) && parameters[6].ParameterType == typeof(Vector2i))
                {
                    return method;
                }
            }

            return null;
        }

        private static void Reset()
        {
            _active = false;
            _recipe = null;
            _hits = 0;
            _bonus = 0;
            _amountApplied = false;
            _added = false;
        }

        [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
        private static class DoCraftingPatch
        {
            private static bool Prepare()
            {
                return Available("crafting");
            }

            private static void Prefix(InventoryGui __instance)
            {
                Reset();
                try
                {
                    if (!Plugin.ProportionalCraftBonusEnabled.Value || Player.m_localPlayer == null)
                    {
                        return;
                    }

                    var recipe = __instance.m_craftRecipe;
                    var station = Player.m_localPlayer.GetCurrentCraftingStation();
                    if (recipe == null || recipe.m_item == null || station == null)
                    {
                        return;
                    }

                    var skill = (Skills.SkillType)CraftingSkill.GetValue(station);
                    if (skill == Skills.SkillType.None || recipe.m_item.m_itemData.m_shared.m_maxStackSize <= 1)
                    {
                        return;
                    }

                    // From here the game would roll; roll here instead and keep its roll from ever hitting.
                    _savedChance = (float)BonusChance.GetValue(__instance);
                    BonusChance.SetValue(__instance, 0f);
                    _active = true;
                    _recipe = recipe;

                    var chance = Player.m_localPlayer.GetSkillFactor(skill) * _savedChance;
                    var crafts = 1;
                    if (MultiCrafting != null && MultiCraftAmount != null && (bool)MultiCrafting.GetValue(__instance))
                    {
                        crafts = (int)MultiCraftAmount.GetValue(__instance);
                    }
                    for (var i = 0; i < crafts; i++)
                    {
                        if (UnityEngine.Random.value < chance)
                        {
                            _hits++;
                        }
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"ProportionalCraftBonus patch failed: {e}");
                }
            }

            private static void Postfix(InventoryGui __instance)
            {
                try
                {
                    if (!_added || _bonus <= 0)
                    {
                        return;
                    }

                    var station = Player.m_localPlayer.GetCurrentCraftingStation();
                    var pos = (station != null ? station.transform.position : Player.m_localPlayer.transform.position)
                              + Vector3.up;
                    if (ShowTextString != null)
                    {
                        ShowTextString.Invoke(DamageText.instance, new object[] { BonusText, pos, "+" + _bonus, true });
                    }
                    else
                    {
                        DamageText.instance.ShowText(BonusText, pos, _bonus, true);
                    }

                    (BonusEffect?.GetValue(__instance) as EffectList)?.Create(pos, Quaternion.identity);
                    Plugin.Log.LogInfo($"Bonus craft: {_hits} x {_bonus / _hits} {_recipe.m_item.name}");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"ProportionalCraftBonus patch failed: {e}");
                }
            }

            private static void Finalizer(InventoryGui __instance)
            {
                if (_active)
                {
                    BonusChance.SetValue(__instance, _savedChance);
                }

                Reset();
            }
        }

        [HarmonyPatch]
        private static class GetAmountPatch
        {
            private static bool Prepare()
            {
                return Available("amount");
            }

            private static MethodBase TargetMethod()
            {
                return GetAmount;
            }

            // __result is the output of all crafts in the call; the last argument is how many crafts that is.
            private static void Postfix(Recipe __instance, object[] __args, ref int __result)
            {
                if (!_active || _amountApplied || _hits == 0 || __instance != _recipe)
                {
                    return;
                }

                // Only the first call is the craft itself; DoCrafting calls it again later to refresh the panel.
                _amountApplied = true;
                var crafts = __args.Length > 0 && __args[__args.Length - 1] is int n && n > 0 ? n : 1;
                _bonus = _hits * (__result / crafts);
                __result += _bonus;
            }
        }

        [HarmonyPatch]
        private static class AddItemPatch
        {
            private static bool Prepare()
            {
                return Available("inventory");
            }

            private static MethodBase TargetMethod()
            {
                return AddItemTarget();
            }

            private static void Postfix(string __0, ItemDrop.ItemData __result)
            {
                if (_active && _bonus > 0 && __result != null && _recipe != null && __0 == _recipe.m_item.gameObject.name)
                {
                    _added = true;
                }
            }
        }
    }
}
