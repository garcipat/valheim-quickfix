using System;
using HarmonyLib;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// Vanilla Cooking XP is flat: 0.4 for putting food on a cooking station and 0.6 for taking it off, whatever it is,
    /// and 1 per item from the cauldron. Food that takes longer now gives more: taking it off a cooking station scales the
    /// 0.6 by its cook time against <see cref="ReferenceCookTime"/>, and a cauldron recipe scales its XP by the station
    /// level it needs.
    /// </summary>
    /// <remarks>
    /// The game raises the skill itself, inside <c>CookingStation.OnInteract</c> and <c>InventoryGui.DoCrafting</c>.
    /// Prefixes there work out the factor and a Finalizer clears it, and a Prefix on <c>Player.RaiseSkill</c> applies it
    /// to Cooking only while one of them runs. Putting food on, and taking off burnt food, keep the vanilla amount.
    /// </remarks>
    internal static class CookingXpByTimePatch
    {
        // About a boar meat's cook time: food that cooks this long gives the vanilla XP.
        private const float ReferenceCookTime = 25f;
        // Each cauldron level a recipe needs above the first adds this much of the vanilla XP.
        private const float PerStationLevel = 0.5f;
        // Skills.SkillType.Cooking; the compile-time libs predate it.
        private const Skills.SkillType Cooking = (Skills.SkillType)105;

        // 0 while no cooking interaction is running.
        private static float _factor;

        private static readonly AccessTools.FieldRef<InventoryGui, Recipe> CraftRecipe =
            AccessTools.FieldRefAccess<InventoryGui, Recipe>("m_craftRecipe");

        [HarmonyPatch(typeof(Player), nameof(Player.RaiseSkill))]
        private static class RaiseSkillPatch
        {
            private static void Prefix(Player __instance, Skills.SkillType skill, ref float value)
            {
                if (_factor > 0f && skill == Cooking && __instance == Player.m_localPlayer)
                {
                    value *= _factor;
                }
            }
        }

        [HarmonyPatch(typeof(CookingStation), "OnInteract")]
        private static class CookingStationPatch
        {
            private static void Prefix(CookingStation __instance)
            {
                try
                {
                    if (Plugin.CookingXpByTimeEnabled.Value)
                    {
                        _factor = DoneItemFactor(__instance);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"CookingXpByTime patch failed: {e}");
                }
            }

            private static void Finalizer()
            {
                _factor = 0f;
            }
        }

        [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
        private static class CauldronPatch
        {
            private static void Prefix(InventoryGui __instance)
            {
                try
                {
                    if (!Plugin.CookingXpByTimeEnabled.Value)
                    {
                        return;
                    }

                    var recipe = CraftRecipe(__instance);
                    if (recipe != null && recipe.m_minStationLevel > 1)
                    {
                        _factor = 1f + PerStationLevel * (recipe.m_minStationLevel - 1);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"CookingXpByTime patch failed: {e}");
                }
            }

            private static void Finalizer()
            {
                _factor = 0f;
            }
        }

        // The factor for the item the press would take off: the first slot holding cooked food, as the game picks it.
        // 0 (vanilla) when nothing is done or the next done item is burnt.
        private static float DoneItemFactor(CookingStation station)
        {
            var nview = station.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid())
            {
                return 0f;
            }

            var zdo = nview.GetZDO();
            for (var i = 0; i < station.m_slots.Length; i++)
            {
                var itemName = zdo.GetString("slot" + i);
                if (itemName == "")
                {
                    continue;
                }

                if (station.m_overCookedItem != null && itemName == station.m_overCookedItem.name)
                {
                    return 0f;
                }

                foreach (var conversion in station.m_conversion)
                {
                    if (conversion.m_to != null && conversion.m_to.name == itemName)
                    {
                        return conversion.m_cookTime / ReferenceCookTime;
                    }
                }
            }

            return 0f;
        }
    }
}
