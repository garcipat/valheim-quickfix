using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ValheimItemCompare.Patches
{
    /// <summary>
    /// Appends a "vs. equipped" block to the tooltip of an item the player hovers in a container or inventory. The
    /// vanilla tooltip text is never touched.
    /// </summary>
    /// <remarks>
    /// Only the instance <c>GetTooltip</c> is patched: the inventory grid calls it every frame while an item is hovered
    /// (so a held compare key shows up live), whereas the static overload also serves recipes and the item it chains to.
    /// The instance method's signature differs between the compile-time libs and the live game (a stack-override
    /// parameter was added), so it is found by name rather than by signature.
    /// </remarks>
    [HarmonyPatch]
    internal static class TooltipComparePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var method in typeof(ItemDrop.ItemData).GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.Name == "GetTooltip")
                {
                    yield return method;
                }
            }
        }

        private static void Postfix(ItemDrop.ItemData __instance, ref string __result)
        {
            try
            {
                var player = Player.m_localPlayer;
                if (player == null || __instance?.m_shared == null)
                {
                    return;
                }

                if (Plugin.Mode.Value == CompareMode.OnKeyPress && !Plugin.CompareKey.Value.IsPressed())
                {
                    return;
                }

                __result += StatDiff.Build(__instance, player);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Tooltip comparison failed: {e}");
            }
        }
    }
}
