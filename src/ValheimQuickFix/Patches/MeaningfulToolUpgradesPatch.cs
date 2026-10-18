using System;
using HarmonyLib;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// Makes the jump from one tool material to the next count for more than the jump between upgrade levels of the same
    /// tool. Axes and pickaxes gain a flat bonus per tool tier on the chop / pickaxe damage they deal to trees, logs,
    /// stumps and rocks, so a level-1 iron axe out-chops a level-3 bronze one.
    /// </summary>
    /// <remarks>
    /// The bonus is added to the <see cref="HitData"/> just before the target takes it, so the item's own stats, combat
    /// damage and the tooltip are untouched. <c>HitData.m_toolTier</c> (read by name, see <see cref="HitDataAccess"/>) is the tier of the tool that made the hit, set by
    /// the attack. Only hits from the local player are changed. Tree, log, stump and rock health live in the target's
    /// own <c>Damage</c> methods, one patch each.
    /// </remarks>
    internal static class MeaningfulToolUpgradesPatch
    {
        [HarmonyPatch(typeof(TreeBase), nameof(TreeBase.Damage))]
        private static class Tree
        {
            private static void Prefix(HitData hit) => Apply(hit);
        }

        [HarmonyPatch(typeof(TreeLog), nameof(TreeLog.Damage))]
        private static class Log
        {
            private static void Prefix(HitData hit) => Apply(hit);
        }

        [HarmonyPatch(typeof(Destructible), nameof(Destructible.Damage))]
        private static class Stump
        {
            private static void Prefix(HitData hit) => Apply(hit);
        }

        [HarmonyPatch(typeof(MineRock), nameof(MineRock.Damage))]
        private static class Rock
        {
            private static void Prefix(HitData hit) => Apply(hit);
        }

        [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.Damage))]
        private static class BigRock
        {
            private static void Prefix(HitData hit) => Apply(hit);
        }

        private static void Apply(HitData hit)
        {
            try
            {
                if (!Plugin.MeaningfulToolUpgradesEnabled.Value || hit == null)
                {
                    return;
                }

                var toolTier = HitDataAccess.ToolTier(hit);
                if (toolTier <= 0)
                {
                    return;
                }

                var player = Player.m_localPlayer;
                if (player == null || hit.GetAttacker() != player)
                {
                    return;
                }

                // Only add to a damage type the tool actually deals, so other tools and weapons are untouched.
                if (hit.m_damage.m_chop > 0f)
                {
                    hit.m_damage.m_chop += Plugin.ChopTierBonus.Value * toolTier;
                }

                if (hit.m_damage.m_pickaxe > 0f)
                {
                    hit.m_damage.m_pickaxe += Plugin.PickaxeTierBonus.Value * toolTier;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"MeaningfulToolUpgrades failed: {e}");
            }
        }
    }
}
