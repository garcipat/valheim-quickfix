using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace ValheimMapRoutes
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "dev.pgarcia.valheim.maproutes";
        public const string PluginName = "Map Routes";
        // Comes from the release tag via the build (see Directory.Build.props); 0.0.0-dev for local builds.
        public const string PluginVersion = BuildInfo.Version;

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> BiomeBordersEnabled;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            BiomeBordersEnabled = Config.Bind(
                "General",
                "BiomeBorders",
                true,
                "Draw faint dotted lines along the borders between land biomes on the large map, where it is explored.");

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            RouteArt.ResetCursor();
        }
    }
}
