using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

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
        internal static ConfigEntry<KeyboardShortcut> PinHotkey;
        internal static ConfigEntry<bool> VikingMap;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            BiomeBordersEnabled = Config.Bind(
                "General",
                "BiomeBorders",
                false,
                "Draw faint dotted lines along the borders between land biomes on the large map, where it is explored.");

            PinHotkey = Config.Bind(
                "General",
                "PinHotkey",
                new KeyboardShortcut(KeyCode.F8),
                "Key that pins what you look at (berries, mushrooms, copper, tin, silver, tar, dragon eggs, cave and " +
                "dungeon entrances, portals, ships) on the map with its icon. None turns it off.");

            VikingMap = Config.Bind(
                "General",
                "VikingMap",
                false,
                "Make the small map in the corner of the screen round, a little bigger, and put it on a painted Viking " +
                "shield with a crossed axe and club.");

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
