using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace ValheimSignIcons
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "dev.pgarcia.valheim.signicons";
        public const string PluginName = "Sign Icons";
        // Comes from the release tag via the build (see Directory.Build.props); 0.0.0-dev for local builds.
        public const string PluginVersion = BuildInfo.Version;

        internal static ManualLogSource Log;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
