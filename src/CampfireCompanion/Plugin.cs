using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace ValheimCampfireCompanion
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "dev.pgarcia.valheim.campfirecompanion";
        public const string PluginName = "Campfire Companion";
        // Comes from the release tag via the build (see Directory.Build.props); 0.0.0-dev for local builds.
        public const string PluginVersion = BuildInfo.Version;

        internal static ManualLogSource Log;

        internal static ConfigEntry<Companion> Choice;
        internal static ConfigEntry<int> Stars;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            Choice = Config.Bind("General", "Companion", Companion.None,
                "The creature that stands with your character at the main menu campfire. None leaves the menu as it is; Random picks a different one each time the menu loads.");
            Stars = Config.Bind("General", "Stars", 0,
                new ConfigDescription(
                    "Star level of the companion, from 0 to 2. Starred creatures get the colouring and extra parts they have in the world.",
                    new AcceptableValueRange<int>(0, 2)));

            Choice.SettingChanged += (_, __) => CompanionSpawner.Refresh();
            Stars.SettingChanged += (_, __) => CompanionSpawner.Refresh();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
        }

        private void Update()
        {
            CompanionSpawner.Tick();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
