using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// The world's resource rate (for example 1.5x) scales how many items drop, but the game rounds the scaled amount to
    /// the nearest whole number, so 1 x 1.5 always gives 2 and 3 x 1.5 = 4.5 always gives 5. These patches round the
    /// scaled amount randomly instead, in proportion to the fractional part: 4.5 gives 4 or 5, 50% each.
    /// </summary>
    internal static class FairRounding
    {
        internal static readonly FieldInfo ResourceRate = AccessTools.Field(typeof(Game), "m_resourceRate");
        internal static readonly FieldInfo NonScaledTypes = AccessTools.Field(typeof(Game), "m_nonScaledDropTypes");

        internal static bool Active
        {
            get
            {
                return Plugin.FairResourceRoundingEnabled.Value && Player.m_localPlayer != null && ResourceRate != null;
            }
        }

        internal static float Rate
        {
            get { return (float)ResourceRate.GetValue(null); }
            set { ResourceRate.SetValue(null, value); }
        }

        // Rounds down or up at random, so the average over many rolls is the exact value: 4.5 gives 4 or 5, 50% each.
        internal static int RandomRound(float value)
        {
            var whole = Mathf.Floor(value);
            return (int)whole + (UnityEngine.Random.value < value - whole ? 1 : 0);
        }
    }

    /// <summary>
    /// <c>Game.ScaleDrops(ItemData, int)</c> — a fixed amount scaled by the rate (pickables, bee hives, ...).
    /// </summary>
    [HarmonyPatch]
    internal static class ScaleFixedDropPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Game), "ScaleDrops", new[] { typeof(ItemDrop.ItemData), typeof(int) });
        }

        private static bool Prepare()
        {
            if (TargetMethod() != null && FairRounding.ResourceRate != null && FairRounding.NonScaledTypes != null)
            {
                return true;
            }

            Plugin.Log.LogWarning("FairResourceRounding disabled for fixed amounts: Game.ScaleDrops or its fields are missing in this game version");
            return false;
        }

        // Positional arguments (__0 = item data, __1 = amount, __instance = Game) because the names differ across versions.
        private static void Postfix(object __instance, ItemDrop.ItemData __0, int __1, ref int __result)
        {
            try
            {
                var rate = FairRounding.Active ? FairRounding.Rate : 1f;
                if (rate <= 1f || ((IList)FairRounding.NonScaledTypes.GetValue(__instance)).Contains(__0.m_shared.m_itemType))
                {
                    return;
                }

                var max = __0.m_shared.m_maxStackSize > 1 ? __0.m_shared.m_maxStackSize : 1000;
                __result = Mathf.Clamp(FairRounding.RandomRound(__1 * rate), 1, max);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"FairResourceRounding (fixed amount) failed: {e}");
            }
        }
    }

    /// <summary>
    /// <c>Game.ScaleDrops(int, int)</c> — a random amount between a minimum and maximum scaled by the rate (monster
    /// drops, pickable items). The overloads taking a prefab or item data end up here.
    /// </summary>
    [HarmonyPatch]
    internal static class ScaleRangeDropPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Game), "ScaleDrops", new[] { typeof(int), typeof(int) });
        }

        private static bool Prepare()
        {
            if (TargetMethod() != null && FairRounding.ResourceRate != null)
            {
                return true;
            }

            Plugin.Log.LogWarning("FairResourceRounding disabled for ranged amounts: Game.ScaleDrops or m_resourceRate is missing in this game version");
            return false;
        }

        private static void Postfix(int __0, int __1, ref int __result)
        {
            try
            {
                var rate = FairRounding.Active ? FairRounding.Rate : 1f;
                if (rate <= 1f)
                {
                    return;
                }

                // The game rolls a float here, but its callers pass an exclusive maximum (stack 1 arrives as 1..2), so
                // that float roll averages too high. Roll a whole number like the game does at 1x, then scale it and
                // round fairly; at least 1, as in the game.
                __result = Mathf.Max(1, FairRounding.RandomRound(UnityEngine.Random.Range(__0, __1) * rate));
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"FairResourceRounding (ranged amount) failed: {e}");
            }
        }
    }

    /// <summary>
    /// <c>DropTable.GetDropList(int)</c> — loot tables (trees, rocks, containers, ...). Above 1x the game rolls the
    /// table once per whole step of the rate plus one extra, shrunken pass for the fraction, and that shrunken pass
    /// rounds every stack up to at least 1. Instead let the game roll only the whole steps, roll the table once more
    /// unscaled, and keep a fair share of that last roll's items, rounded randomly.
    /// </summary>
    [HarmonyPatch]
    internal static class DropTableFractionPatch
    {
        private static readonly FieldInfo DontScale = AccessTools.Field(typeof(DropTable.DropData), "m_dontScale");

        private static MethodBase _target;

        private static MethodBase TargetMethod()
        {
            _target = _target ?? AccessTools.Method(typeof(DropTable), "GetDropList", new[] { typeof(int) });
            return _target;
        }

        private static bool Prepare()
        {
            if (TargetMethod() != null && FairRounding.ResourceRate != null)
            {
                return true;
            }

            Plugin.Log.LogWarning("FairResourceRounding disabled for loot tables: DropTable.GetDropList(int) or m_resourceRate is missing in this game version");
            return false;
        }

        private static void Prefix(out float __state)
        {
            __state = float.NaN;
            try
            {
                if (!FairRounding.Active)
                {
                    return;
                }

                // Only above 1x with a fraction; the extra roll below re-enters this patch at a whole rate and is skipped.
                var rate = FairRounding.Rate;
                if (rate > 1f && rate != Mathf.Floor(rate))
                {
                    __state = rate;
                    FairRounding.Rate = Mathf.Floor(rate);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"FairResourceRounding (loot table) failed: {e}");
            }
        }

        private static void Postfix(DropTable __instance, int __0, float __state, ref List<GameObject> __result)
        {
            if (float.IsNaN(__state))
            {
                return;
            }

            try
            {
                // One more roll of the table at rate 1, then keep (rate - whole steps) of it.
                FairRounding.Rate = 1f;
                var extra = (List<GameObject>)TargetMethod().Invoke(__instance, new object[] { __0 });
                var fraction = __state - Mathf.Floor(__state);

                // Items the table marks as not scaled drop once per roll and are not part of the extra share.
                var unscaled = new HashSet<GameObject>();
                if (DontScale != null)
                {
                    foreach (var drop in __instance.m_drops)
                    {
                        if ((bool)DontScale.GetValue(drop))
                        {
                            unscaled.Add(drop.m_item);
                        }
                    }
                }

                var counts = new Dictionary<GameObject, int>();
                var order = new List<GameObject>();
                foreach (var item in extra)
                {
                    if (unscaled.Contains(item))
                    {
                        continue;
                    }

                    int count;
                    if (!counts.TryGetValue(item, out count))
                    {
                        order.Add(item);
                    }

                    counts[item] = count + 1;
                }

                foreach (var item in order)
                {
                    var keep = FairRounding.RandomRound(counts[item] * fraction);
                    for (var i = 0; i < keep; i++)
                    {
                        __result.Add(item);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"FairResourceRounding (loot table) failed: {e}");
            }
        }

        // Always put the world's rate back, even if the game's own roll threw.
        private static void Finalizer(float __state)
        {
            if (!float.IsNaN(__state))
            {
                FairRounding.Rate = __state;
            }
        }
    }
}
