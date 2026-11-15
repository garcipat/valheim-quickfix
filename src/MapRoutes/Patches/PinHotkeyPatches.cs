using System;
using System.Reflection;
using HarmonyLib;

namespace ValheimMapRoutes.Patches
{
    /// <summary>
    /// The HUD writes the hover text here; a pinnable object gets the pin key line under it. The label is a TextMeshPro
    /// text in the live game but a UI Text in the compile-time libraries, so its text is set by name.
    /// </summary>
    [HarmonyPatch(typeof(Hud), "UpdateCrosshair")]
    internal static class HudUpdateCrosshairPatch
    {
        private static readonly FieldInfo HoverNameField = AccessTools.Field(typeof(Hud), "m_hoverName");
        private static bool _failed;

        private static void Postfix(Hud __instance, Player player)
        {
            if (_failed)
            {
                return;
            }
            try
            {
                var hint = PinHotkey.Hint(player);
                var label = hint == null ? null : HoverNameField?.GetValue(__instance);
                var textProperty = label?.GetType().GetProperty("text");
                if (textProperty == null || !(textProperty.GetValue(label, null) is string text) || text.Length == 0)
                {
                    return;
                }
                textProperty.SetValue(label, text + "\n" + hint, null);
            }
            catch (Exception e)
            {
                _failed = true;
                Plugin.Log.LogError($"Showing the pin key hint failed: {e}");
            }
        }
    }

    /// <summary>Reads the pin key.</summary>
    [HarmonyPatch(typeof(Player), "Update")]
    internal static class PlayerUpdatePatch
    {
        private static void Postfix(Player __instance)
        {
            PinHotkey.Update(__instance);
        }
    }
}
