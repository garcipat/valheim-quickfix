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
        internal static ConfigEntry<bool> LandscapingEnabled;
        internal static ConfigEntry<bool> EnduranceEnabled;
        internal static ConfigEntry<float> MaxDrainReduction;
        internal static ConfigEntry<bool> PaddlingEnabled;
        internal static ConfigEntry<float> MaxPaddleBonus;

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

            LandscapingEnabled = Config.Bind(
                "Landscaping",
                "Enabled",
                true,
                "Landscaping skill: levels with every use of the hoe and makes the hoe cost less stamina, up to half at level 100. Off: the skill is hidden, does not level and has no effect, but your level is kept.");
            LandscapingEnabled.SettingChanged += (_, __) => Landscaping.Apply();

            EnduranceEnabled = Config.Bind(
                "Endurance",
                "Enabled",
                true,
                "Endurance skill: levels while you walk over the weight limit and makes that drain less stamina. Off: the skill is hidden, does not level and has no effect, but your level is kept.");

            MaxDrainReduction = Config.Bind(
                "Endurance",
                "MaxDrainReduction",
                0.5f,
                new ConfigDescription(
                    "How much less stamina walking over the weight limit drains at Endurance level 100 (0.5 = half). Scales linearly from vanilla at level 0. Regen stays blocked while overweight, as in vanilla.",
                    new AcceptableValueRange<float>(0f, 0.9f)));

            PaddlingEnabled = Config.Bind(
                "Paddling",
                "Enabled",
                true,
                "Paddling skill: levels while you paddle a ship (forward without the sail, or backwards) and makes you paddle faster. Off: the skill is hidden, does not level and has no effect, but your level is kept.");

            MaxPaddleBonus = Config.Bind(
                "Paddling",
                "MaxPaddleBonus",
                0.4f,
                new ConfigDescription(
                    "How much harder you paddle at Paddling level 100 (0.4 = 40% more force, roughly 40% faster). Scales linearly from vanilla at level 0. Sailing is unchanged.",
                    new AcceptableValueRange<float>(0f, 1f)));

            SkillRegistry.Add(Hauling.Create(HaulingEnabled));
            SkillRegistry.Add(Landscaping.Create(LandscapingEnabled));
            SkillRegistry.Add(Endurance.Create(EnduranceEnabled));
            SkillRegistry.Add(Paddling.Create(PaddlingEnabled));

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
