using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace ValheimQuickFix
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "dev.pgarcia.valheim.quickfix";
        public const string PluginName = "Valheim QuickFix";
        public const string PluginVersion = "0.1.0";

        internal static ConfigEntry<bool> RestoreShieldEnabled;
        internal static ConfigEntry<bool> PushBoatEnabled;
        internal static ConfigEntry<float> PushStrength;

        private Harmony _harmony;

        private void Awake()
        {
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

            PushStrength = Config.Bind(
                "Boats",
                "PushStrength",
                3f,
                new ConfigDescription(
                    "How hard the boat is pushed, as acceleration in m/s². Independent of boat mass. Tune until it feels right.",
                    new AcceptableValueRange<float>(0.5f, 20f)));

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
