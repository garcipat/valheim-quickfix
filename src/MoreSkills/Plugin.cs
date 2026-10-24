using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ValheimMoreSkills.Core;
using ValheimMoreSkills.Patches;

namespace ValheimMoreSkills
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "dev.pgarcia.valheim.moreskills";
        public const string PluginName = "More Skills";
        // Comes from the release tag via the build (see Directory.Build.props); 0.0.0-dev for local builds.
        public const string PluginVersion = BuildInfo.Version;

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> HaulingEnabled;
        internal static ConfigEntry<float> MaxCargoReduction;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            HaulingEnabled = Config.Bind(
                "Hauling",
                "Enabled",
                true,
                "Hauling skill: levels while you pull a loaded cart and makes the cargo weigh less on you. Off: the skill is hidden, does not level and has no effect, but your level is kept.");

            MaxCargoReduction = Config.Bind(
                "Hauling",
                "MaxCargoReduction",
                0.5f,
                new ConfigDescription(
                    "How much lighter the cargo in a cart feels at Hauling level 100 (0.5 = half its weight). Scales linearly from vanilla at level 0. The empty cart's own weight never changes.",
                    new AcceptableValueRange<float>(0f, 0.9f)));

            SkillRegistry.Add(Hauling.Create(HaulingEnabled));

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
