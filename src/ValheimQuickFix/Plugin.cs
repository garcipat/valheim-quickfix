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
        internal static ConfigEntry<bool> FloorsNotLeakyEnabled;
        internal static ConfigEntry<bool> SkipStartupLogosEnabled;
        internal static ConfigEntry<bool> FairResourceRoundingEnabled;
        internal static ConfigEntry<bool> IgnoreTorchesEnabled;
        internal static ConfigEntry<bool> PlaceStacksKeepEquipmentEnabled;
        internal static ConfigEntry<bool> PlaceStacksKeepConsumablesEnabled;
        internal static ConfigEntry<bool> HideBeesWhenFullEnabled;
        internal static ConfigEntry<bool> HiveOnPoleSnapEnabled;
        internal static ConfigEntry<bool> MoreBearsEnabled;
        internal static ConfigEntry<bool> MeaningfulToolUpgradesEnabled;
        internal static ConfigEntry<bool> RepairAllOnHoldEnabled;
        internal static ConfigEntry<bool> PortalPressOrHoldEnabled;
        internal static ConfigEntry<float> ChopTierBonus;
        internal static ConfigEntry<float> PickaxeTierBonus;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            RestoreShieldEnabled = Config.Bind(
                "Combat",
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

            FloorsNotLeakyEnabled = Config.Bind(
                "Building",
                "FloorsNotLeaky",
                true,
                "Count floors (wooden floors, stone floors, ...) as a roof for whoever stands under them, so a second storey keeps the rain off you.");

            SkipStartupLogosEnabled = Config.Bind(
                "Startup",
                "SkipStartupLogos",
                true,
                "Skip the Coffee Stain and Iron Gate logos shown when the game starts. The Unity splash screen cannot be skipped.");

            FairResourceRoundingEnabled = Config.Bind(
                "Drops",
                "FairResourceRounding",
                true,
                "With a fractional resource rate in the world settings (for example 1.5x), round the number of dropped resources up or down at random instead of always up, so 1.5x averages 1.5 and 4.5 gives 4 or 5.");

            IgnoreTorchesEnabled = Config.Bind(
                "Enemies",
                "IgnoreTorches",
                true,
                "Enemies ignore torches instead of stopping to attack them as structures. Only affects monsters simulated on your machine (always the case in singleplayer).");

            PlaceStacksKeepEquipmentEnabled = Config.Bind(
                "Inventory",
                "PlaceStacksKeepEquipment",
                true,
                "\"Place stacks\" (and holding E on a container) no longer moves weapons and tools into a container that already holds one with the same name. Ammo, materials and other items are moved as before.");

            PlaceStacksKeepConsumablesEnabled = Config.Bind(
                "Inventory",
                "PlaceStacksKeepConsumables",
                true,
                "\"Place stacks\" (and holding E on a container) no longer moves cooked and brewed food and potions into a container that already holds some. Raw food such as berries and mushrooms is still moved. Turn this off to keep restocking chests with all of them.");

            HideBeesWhenFullEnabled = Config.Bind(
                "Building",
                "HideBeesWhenFull",
                true,
                "Hide the bees around a beehive while it is full, so you can tell from a distance that there is honey to take. They come back once you take it.");

            HiveOnPoleSnapEnabled = Config.Bind(
                "Building",
                "HiveSnapsToPoleMiddle",
                true,
                "While placing a beehive, pressing Q (or E) to pick its snap point also offers a snap point halfway along nearby poles, so the hive can sit in the middle of a pole.");

            MoreBearsEnabled = Config.Bind(
                "Enemies",
                "MoreBears",
                true,
                "Make bears easier to find: they get a spawn roll every 17 minutes instead of every 33, up to two can live in an area, and drop their trophy 20% of the time instead of 10%. Takes effect on the next game start, and only when your machine runs the spawning (singleplayer or hosting).");

            MeaningfulToolUpgradesEnabled = Config.Bind(
                "Tools",
                "MeaningfulToolUpgrades",
                false,
                "Make a better tool material count for more than a higher upgrade level: axes and pickaxes deal extra chop / mining damage to trees, logs, stumps and rocks, growing with the tool's tier (stone 0, flint 1, bronze 2, iron 3 for axes). Combat damage is not affected.");

            RepairAllOnHoldEnabled = Config.Bind(
                "Inventory",
                "RepairAllOnHold",
                true,
                "Holding E on a workbench, forge or any other station that can repair repairs every damaged item you carry at once, instead of one per click of the Repair button. The station's hover text says so.");

            PortalPressOrHoldEnabled = Config.Bind(
                "Portals",
                "PortalPressOrHold",
                false,
                "Pressing E on a portal sends you through it, and holding E opens the tag dialog to rename it. The opening of the portal can be aimed at, not only its frame.");

            ChopTierBonus = Config.Bind(
                "Tools",
                "ChopTierBonus",
                5f,
                new ConfigDescription(
                    "MeaningfulToolUpgrades: chop damage added per tool tier. A tier-3 iron axe gets 3 times this. Axes gain 3 per upgrade level for comparison.",
                    new AcceptableValueRange<float>(0f, 50f)));

            PickaxeTierBonus = Config.Bind(
                "Tools",
                "PickaxeTierBonus",
                8f,
                new ConfigDescription(
                    "MeaningfulToolUpgrades: mining damage added per tool tier. A tier-2 iron pickaxe gets 2 times this. Pickaxes gain 4 to 5 per upgrade level for comparison. Higher than the axe value because pickaxe tiers start at 1.",
                    new AcceptableValueRange<float>(0f, 50f)));

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
