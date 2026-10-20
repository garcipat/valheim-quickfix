using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ValheimItemCompare
{
    public enum CompareMode
    {
        Always,
        OnKeyPress,
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "dev.pgarcia.valheim.itemcompare";
        public const string PluginName = "Item Compare";
        // Comes from the release tag via the build (see Directory.Build.props); 0.0.0-dev for local builds.
        public const string PluginVersion = BuildInfo.Version;

        internal static ManualLogSource Log;

        internal static ConfigEntry<CompareMode> Mode;
        internal static ConfigEntry<KeyboardShortcut> CompareKey;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            Mode = Config.Bind("General", "Mode", CompareMode.Always,
                "Always: every equipment tooltip shows how it compares with what you wear. OnKeyPress: only while the compare key is held.");
            CompareKey = Config.Bind("General", "CompareKey", new KeyboardShortcut(KeyCode.LeftShift),
                "Key to hold while hovering an item to see the comparison. Only used when Mode is OnKeyPress.");

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
