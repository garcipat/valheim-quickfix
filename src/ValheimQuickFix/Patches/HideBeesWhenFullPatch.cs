using System;
using HarmonyLib;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// Hides the bees around a full beehive, so a hive with honey to take looks different from one that is still working.
    /// A hive is full when its honey level reaches <c>m_maxHoney</c>. The level lives in the hive's ZDO, which every
    /// client has, so this needs no networking.
    /// </summary>
    /// <remarks>
    /// <c>UpdateBees</c> runs every 10 s on every loaded hive (and once right after it spawns), so the bees follow the honey
    /// level with up to that much delay. <c>RPC_Extract</c> brings them back right away when the honey is taken.
    /// </remarks>
    [HarmonyPatch(typeof(Beehive), "UpdateBees")]
    internal static class BeehiveUpdateBeesPatch
    {
        private static void Postfix(Beehive __instance, ZNetView ___m_nview)
        {
            HideBees.Apply(__instance, ___m_nview);
        }
    }

    [HarmonyPatch(typeof(Beehive), "RPC_Extract")]
    internal static class BeehiveExtractPatch
    {
        private static void Postfix(Beehive __instance, ZNetView ___m_nview)
        {
            HideBees.Apply(__instance, ___m_nview);
        }
    }

    internal static class HideBees
    {
        // ZDOVars.s_level in the live game; the compile-time libs predate ZDOVars, so hash the key the same way.
        private static readonly int LevelHash = "level".GetStableHashCode();

        internal static void Apply(Beehive hive, ZNetView nview)
        {
            if (!Plugin.HideBeesWhenFullEnabled.Value)
            {
                return;
            }
            try
            {
                // Only ever turn the bees off: vanilla decides every other time whether they show.
                if (hive.m_beeEffect != null && IsFull(hive, nview))
                {
                    hive.m_beeEffect.SetActive(false);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"HideBeesWhenFull failed: {e}");
            }
        }

        private static bool IsFull(Beehive hive, ZNetView nview)
        {
            if (nview == null || !nview.IsValid())
            {
                return false;
            }
            return nview.GetZDO().GetInt(LevelHash) >= hive.m_maxHoney;
        }
    }
}
