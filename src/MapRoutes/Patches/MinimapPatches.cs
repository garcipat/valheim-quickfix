using System;
using HarmonyLib;

namespace ValheimMapRoutes.Patches
{
    /// <summary>Adds the quill button and draws the routes on the open large map.</summary>
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.Update))]
    internal static class MinimapUpdatePatch
    {
        private static void Postfix(Minimap __instance)
        {
            try
            {
                RouteMap.Update(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Drawing routes failed: {e}");
            }
        }
    }

    /// <summary>Closing the large map ends draw mode, so the cursor never stays a quill.</summary>
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.SetMapMode))]
    internal static class MinimapSetMapModePatch
    {
        private static void Postfix(Minimap __instance, Minimap.MapMode mode)
        {
            if (mode != Minimap.MapMode.Large)
            {
                RouteMap.StopDrawing(__instance);
            }
        }
    }

    /// <summary>Picking a pin type ends draw mode.</summary>
    [HarmonyPatch(typeof(Minimap), "SelectIcon")]
    internal static class MinimapSelectIconPatch
    {
        private static void Postfix(Minimap __instance)
        {
            RouteMap.StopDrawing(__instance);
        }
    }

    /// <summary>In draw mode a left press starts a stroke instead of dragging the map.</summary>
    [HarmonyPatch(typeof(Minimap), "OnMapLeftDown")]
    internal static class MinimapLeftDownPatch
    {
        private static bool Prefix(Minimap __instance)
        {
            if (!RouteMap.Drawing)
            {
                return true;
            }
            try
            {
                RouteMap.StartStroke(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Starting a stroke failed: {e}");
            }
            return false;
        }
    }

    /// <summary>
    /// In draw mode a left release ends the stroke. The game's release would cross out a pin or open the pin name
    /// input on a double-click, so it doesn't run.
    /// </summary>
    [HarmonyPatch(typeof(Minimap), "OnMapLeftUp")]
    internal static class MinimapLeftUpPatch
    {
        private static bool Prefix(Minimap __instance)
        {
            if (!RouteMap.Drawing)
            {
                return true;
            }
            try
            {
                RouteMap.EndStroke(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Ending a stroke failed: {e}");
            }
            return false;
        }
    }

    /// <summary>
    /// Right-click on a route removes it, in any mode. A pin under the pointer wins, so the game removes that as usual.
    /// In draw mode a right press that dragged was a pan, so it removes nothing.
    /// </summary>
    [HarmonyPatch(typeof(Minimap), "RemovePinUnderPointer")]
    internal static class MinimapRemovePinUnderPointerPatch
    {
        private static bool Prefix(Minimap __instance)
        {
            try
            {
                if (RouteMap.RightDragged())
                {
                    return false;
                }
                return !RouteMap.RemoveRouteUnderPointer(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Removing a route failed: {e}");
                return true;
            }
        }
    }
}
