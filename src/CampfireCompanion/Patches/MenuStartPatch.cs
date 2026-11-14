using System;
using HarmonyLib;

namespace ValheimCampfireCompanion.Patches
{
    /// <summary>The menu scene is loaded fresh at startup and after every logout; place the companion each time.</summary>
    [HarmonyPatch(typeof(FejdStartup), "Start")]
    internal static class MenuStartPatch
    {
        private static void Postfix(FejdStartup __instance)
        {
            try
            {
                CompanionSpawner.OnMenuStart(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Campfire companion failed: {e}");
            }
        }
    }
}
