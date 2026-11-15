using System;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using ValheimMoreSkills.Core;

namespace ValheimMoreSkills.Patches
{
    /// <summary>
    /// Nourishment: levels with every food you eat and keeps food effective for longer before it wears off.
    /// </summary>
    /// <remarks>
    /// The pace is set in food-minutes (a food's duration): level N to N+1 takes
    /// <c>MinutesPerLevel * (1 + CurveGrowth * N)</c>. Level 1 is one meal of three 30-minute foods, level 10 about
    /// 13 meals in total, 50 about 124, 100 about 397. Vanilla only lets you eat a food again once half of it is gone,
    /// so eating can't be farmed.
    /// </remarks>
    internal static class Nourishment
    {
        internal const string Identifier = "dev.pgarcia.valheim.moreskills.nourishment";

        internal static readonly Skills.SkillType Type = CustomSkill.TypeOf(Identifier);

        private const string IconPrefab = "CookedMeat";
        private const float MinutesPerLevel = 90f;
        private const float CurveGrowth = 0.06f;

        internal static CustomSkill Create(ConfigEntry<bool> enabled)
        {
            return new CustomSkill(
                Identifier,
                "Nourishment",
                "Eating food. Higher levels keep food effective for longer before it wears off.",
                FoodIcon,
                enabled);
        }

        // Cooked meat's own item icon; null before ObjectDB has loaded it.
        private static Sprite FoodIcon()
        {
            if (ObjectDB.instance == null)
            {
                return null;
            }

            var prefab = ObjectDB.instance.GetItemPrefab(IconPrefab);
            var drop = prefab == null ? null : prefab.GetComponent<ItemDrop>();
            return drop == null ? null : drop.m_itemData.GetIcon();
        }

        internal static void Grant(Player player, float minutes)
        {
            SkillCurve.Grant(player, Type, minutes, MinutesPerLevel, CurveGrowth);
        }
    }

    /// <summary>
    /// <c>Player.UpdateFood</c> sets each food's health, stamina and eitr to <c>full * (time left)^0.3</c> once per
    /// second, then adds them up in the private <c>GetTotalFoodValue</c> and sets the max bars. A Prefix on
    /// <c>GetTotalFoodValue</c> recomputes the values with the exponent
    /// <c>0.3 * (1 - MaxFalloffReduction * Nourishment skill factor)</c> first, so the bars are set once with the final
    /// values. Rewriting them after the bars were set would clamp current health to the lower vanilla maximum.
    /// </summary>
    /// <remarks>
    /// At level 100 with the default 0.5, a food with 10% of its time left gives 71% instead of 50%, on average 87%
    /// instead of 77% over its duration. Duration, re-eating at half time and the full value stay vanilla.
    /// </remarks>
    [HarmonyPatch(typeof(Player), "GetTotalFoodValue")]
    internal static class NourishmentEffectPatch
    {
        private const float VanillaFalloff = 0.3f;

        // Eitr is newer than the compile-time libs, so its fields are accessed by name.
        private static readonly AccessTools.FieldRef<Player.Food, float> FoodEitr =
            AccessTools.FieldRefAccess<Player.Food, float>("m_eitr");

        private static readonly AccessTools.FieldRef<ItemDrop.ItemData.SharedData, float> SharedEitr =
            AccessTools.FieldRefAccess<ItemDrop.ItemData.SharedData, float>("m_foodEitr");

        private static void Prefix(Player __instance)
        {
            try
            {
                if (!Plugin.NourishmentEnabled.Value || __instance != Player.m_localPlayer)
                {
                    return;
                }

                var factor = __instance.GetSkills().GetSkillFactor(Nourishment.Type);
                if (factor <= 0f)
                {
                    return;
                }

                var falloff = VanillaFalloff * (1f - Plugin.MaxFalloffReduction.Value * factor);
                foreach (var food in __instance.GetFoods())
                {
                    var shared = food.m_item?.m_shared;
                    if (shared == null || shared.m_foodBurnTime <= 0f)
                    {
                        continue;
                    }

                    var left = Mathf.Pow(Mathf.Clamp01(food.m_time / shared.m_foodBurnTime), falloff);
                    food.m_health = shared.m_food * left;
                    food.m_stamina = shared.m_foodStamina * left;
                    FoodEitr(food) = SharedEitr(shared) * left;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"NourishmentEffect patch failed: {e}");
            }
        }
    }

    /// <summary>
    /// A food the local player actually ate (<c>EatFood</c> returned true) raises Nourishment by its duration in
    /// minutes, so better food levels it more.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.EatFood))]
    internal static class NourishmentLevelPatch
    {
        private static void Postfix(Player __instance, ItemDrop.ItemData item, bool __result)
        {
            try
            {
                if (!__result || !Plugin.NourishmentEnabled.Value || __instance != Player.m_localPlayer
                    || item?.m_shared == null)
                {
                    return;
                }

                Nourishment.Grant(__instance, item.m_shared.m_foodBurnTime / 60f);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"NourishmentLevel patch failed: {e}");
            }
        }
    }
}
