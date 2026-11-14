using HarmonyLib;

namespace ValheimMapRoutes.Patches
{
    /// <summary>
    /// The game creates and positions pin images here, on the large and the corner map, so a circle pin with a known
    /// name gets its icon right after.
    /// </summary>
    [HarmonyPatch(typeof(Minimap), "UpdatePins")]
    internal static class MinimapUpdatePinsPatch
    {
        private static void Postfix(Minimap __instance)
        {
            PinIcons.Apply(__instance);
        }
    }
}
