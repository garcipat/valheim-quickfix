using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// The game lets you use anything the crosshair hits within 5 m of your eyes. <c>Player.FindHoverObject</c> adds
    /// each hoverable's <c>GetHoverOffset</c> to that reach, so a Postfix on it raises the reach for chests and dungeon
    /// entrances to 7.5 m. Everything else keeps the game's 5 m.
    /// </summary>
    /// <remarks>
    /// A dungeon entrance's <c>Teleport</c> collider sits back in the doorway, so 5 m only reaches it from inside the
    /// opening. Crafting stations also refuse to open (and close their window) beyond their own, much shorter use
    /// distance from your feet; that limit is raised so the crosshair reach decides, and the window still closes once
    /// you walk away. <c>GetHoverOffset</c> is newer than the compile-time game libraries, so it is patched by name.
    /// </remarks>
    [HarmonyPatch]
    internal static class EnhancedInteractionReachPatch
    {
        private const float ExtraReach = 2.5f;
        // Feet to station centre at the furthest a 5 m crosshair reach can open a station from, with some slack.
        private const float StationReach = 7f;

        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Teleport), "GetHoverOffset");
            yield return AccessTools.Method(typeof(Container), "GetHoverOffset");
        }

        private static void Postfix(ref float __result)
        {
            if (!Plugin.EnhancedInteractionReachEnabled.Value)
            {
                return;
            }
            __result += ExtraReach;
        }

        [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.InUseDistance))]
        internal static class StationUseDistance
        {
            private static void Postfix(CraftingStation __instance, Humanoid human, ref bool __result)
            {
                if (!Plugin.EnhancedInteractionReachEnabled.Value || __result)
                {
                    return;
                }
                __result = Vector3.Distance(human.transform.position, __instance.transform.position) < StationReach;
            }
        }

        /// <summary>
        /// An open chest closes once you are more than 4 m from it, which would shut one opened from 7.5 m at once.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGui), "Awake")]
        internal static class ChestCloseDistance
        {
            private const float CloseDistance = 8f;

            private static void Postfix(InventoryGui __instance)
            {
                if (!Plugin.EnhancedInteractionReachEnabled.Value)
                {
                    return;
                }
                __instance.m_autoCloseDistance = Mathf.Max(__instance.m_autoCloseDistance, CloseDistance);
            }
        }
    }
}
