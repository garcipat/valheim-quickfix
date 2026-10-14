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

        private Harmony _harmony;

        private void Awake()
        {
            RestoreShieldEnabled = Config.Bind(
                "Weapon switching",
                "RestoreShield",
                true,
                "Re-equip the shield automatically when switching from a bow or two-handed weapon back to a one-handed weapon.");

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
