using System;
using System.Reflection;
using HarmonyLib;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// The first scene shows the Coffee Stain and Iron Gate logos for a couple of seconds each before loading the menu.
    /// <c>SceneLoader</c> sets <c>_showLogos</c> in <c>Awake</c> and checks it when its loading coroutine starts, so
    /// clearing it afterwards skips both logos while the scene still loads as usual. The Unity splash screen is
    /// drawn by the engine before any game code runs and cannot be reached from here.
    /// </summary>
    [HarmonyPatch]
    internal static class SkipStartupLogosPatch
    {
        private static readonly Type Loader = AccessTools.TypeByName("SceneLoader");
        private static readonly FieldInfo ShowLogos = AccessTools.Field(Loader, "_showLogos");

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(Loader, "Awake");
        }

        private static bool Prepare()
        {
            if (TargetMethod() != null && ShowLogos != null)
            {
                return true;
            }

            Plugin.Log.LogWarning("SkipStartupLogos disabled: SceneLoader._showLogos is missing in this game version");
            return false;
        }

        private static void Postfix(object __instance)
        {
            try
            {
                if (Plugin.SkipStartupLogosEnabled.Value)
                {
                    ShowLogos.SetValue(__instance, false);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"SkipStartupLogos patch failed: {e}");
            }
        }
    }
}
