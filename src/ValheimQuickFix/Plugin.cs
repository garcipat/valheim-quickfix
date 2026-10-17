using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace ValheimQuickFix
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "dev.pgarcia.valheim.quickfix";
        public const string PluginName = "Valheim QuickFix";
        // Comes from the release tag via the build (see Directory.Build.props); 0.0.0-dev for local builds.
        public const string PluginVersion = BuildInfo.Version;

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> RestoreShieldEnabled;
        internal static ConfigEntry<bool> PushBoatEnabled;
        internal static ConfigEntry<bool> StackOnTakeAllEnabled;
        internal static ConfigEntry<bool> RestoreTombstoneSlotsEnabled;
        internal static ConfigEntry<bool> FairResourceRoundingEnabled;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            RestoreShieldEnabled = Config.Bind(
                "Weapon switching",
                "RestoreShield",
                true,
                "Re-equip the shield automatically when switching from a bow or two-handed weapon back to a one-handed weapon.");

            PushBoatEnabled = Config.Bind(
                "Boats",
                "PushBoat",
                true,
                "Show a \"Push boat\" interaction when standing next to a boat, to push it away from you (e.g. off a beach).");

            StackOnTakeAllEnabled = Config.Bind(
                "Inventory",
                "StackOnTakeAll",
                true,
                "After using \"Take all\" on any container (chests, tombstones, ...), merge partial stacks of the same item in your inventory so they don't take two slots.");

            RestoreTombstoneSlotsEnabled = Config.Bind(
                "Inventory",
                "RestoreTombstoneSlots",
                true,
                "When taking everything from a tombstone, put each item back in its original slot, moving whatever you picked up there since to a free slot.");

            FairResourceRoundingEnabled = Config.Bind(
                "Drops",
                "FairResourceRounding",
                true,
                "With a fractional resource rate in the world settings (for example 1.5x), round the number of dropped resources up or down at random instead of always up, so 1.5x averages 1.5 and 4.5 gives 4 or 5.");

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
