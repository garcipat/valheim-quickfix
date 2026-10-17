using System;
using System.Collections.Generic;
using HarmonyLib;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// The "Place stacks" button (and holding E on a container) moves every item you carry into a container that already
    /// holds an item of the same name, whether or not it can stack. Your spare hoe goes into the chest next to the other
    /// hoe, and so do weapons, tools, food and potions. Vanilla only skips equipped items, so while a place-stacks call
    /// runs, report those item types as equipped and leave the game's own logic alone.
    /// </summary>
    /// <remarks>
    /// Inventory.StackAll is missing from the compile-time libs (0.202), so it is targeted by name and the patch
    /// disables itself, with a log line, if a game version doesn't have it.
    /// </remarks>
    [HarmonyPatch(typeof(Inventory), "StackAll")]
    internal static class PlaceStacksKeepGearPatch
    {
        // Compared by name, because some of these (TwoHandedWeaponLeft) are missing from older compile-time libs.
        private static readonly HashSet<string> EquipmentTypes = new HashSet<string>
        {
            "OneHandedWeapon",
            "TwoHandedWeapon",
            "TwoHandedWeaponLeft",
            "Bow",
            "Torch",
            "Tool",
        };

        private const string ConsumableType = "Consumable";

        // Names of consumables the game produces: cooked at a cooking station, brewed in a fermenter or made from a cauldron
        // recipe (stews, soups). Raw berries and mushrooms are only ever ingredients, so they are not in it, and neither is anything
        // that a cooking station or fermenter takes as input, such as raw meat or the mead bases a cauldron makes.
        // Consumables made at other crafting stations (the food preparation table, ...) are only logged, not protected.
        // Built once, on first use, and again only if the game loads a new item database.
        private static HashSet<string> _producedConsumables;
        private static ObjectDB _producedFor;

        private const string CauldronStation = "cauldron";

        // True only while the local player's inventory is being stacked into a container.
        internal static bool Active;

        internal static bool IsProtected(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null)
            {
                return false;
            }

            var type = item.m_shared.m_itemType.ToString();
            if (Plugin.PlaceStacksKeepEquipmentEnabled.Value && EquipmentTypes.Contains(type))
            {
                return true;
            }

            return Plugin.PlaceStacksKeepConsumablesEnabled.Value && type == ConsumableType && IsProducedConsumable(item);
        }

        private static bool IsProducedConsumable(ItemDrop.ItemData item)
        {
            var produced = ProducedConsumables();

            // Without a list, protect every consumable rather than risk moving cooked food away.
            return produced == null || produced.Contains(item.m_shared.m_name);
        }

        private static HashSet<string> ProducedConsumables()
        {
            var db = ObjectDB.instance;
            if (_producedConsumables != null && _producedFor == db)
            {
                return _producedConsumables;
            }

            var scene = ZNetScene.instance;
            if (db == null || scene == null)
            {
                return null;
            }

            try
            {
                var produced = new HashSet<string>();
                var otherStations = new SortedDictionary<string, HashSet<string>>();
                var ingredients = new HashSet<string>();

                foreach (var recipe in db.m_recipes)
                {
                    if (recipe == null)
                    {
                        continue;
                    }

                    var station = recipe.m_craftingStation != null ? recipe.m_craftingStation.m_name : "(no station)";
                    if (station.IndexOf(CauldronStation, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        AddIfConsumable(produced, recipe.m_item);
                        continue;
                    }

                    // Not protected; collected only for the log.
                    var others = new HashSet<string>();
                    AddIfConsumable(others, recipe.m_item);
                    if (others.Count > 0)
                    {
                        if (!otherStations.TryGetValue(station, out var names))
                        {
                            otherStations[station] = names = new HashSet<string>();
                        }

                        names.UnionWith(others);
                    }
                }

                foreach (var prefab in scene.m_prefabs)
                {
                    if (prefab == null)
                    {
                        continue;
                    }

                    var station = prefab.GetComponent<CookingStation>();
                    if (station != null)
                    {
                        foreach (var conversion in station.m_conversion)
                        {
                            AddIfConsumable(produced, conversion.m_to);
                            AddName(ingredients, conversion.m_from);
                        }
                    }

                    var fermenter = prefab.GetComponent<Fermenter>();
                    if (fermenter != null)
                    {
                        foreach (var conversion in fermenter.m_conversion)
                        {
                            AddIfConsumable(produced, conversion.m_to);
                            AddName(ingredients, conversion.m_from);
                        }
                    }
                }

                produced.ExceptWith(ingredients);

                _producedConsumables = produced;
                _producedFor = db;
                Plugin.Log.LogInfo($"PlaceStacksKeepGear: {produced.Count} cooked or brewed consumables protected: {string.Join(", ", new List<string>(produced).ToArray())}");
                foreach (var entry in otherStations)
                {
                    Plugin.Log.LogInfo($"PlaceStacksKeepGear: consumables made at {entry.Key}, not protected: {string.Join(", ", new List<string>(entry.Value).ToArray())}");
                }

                return produced;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"PlaceStacksKeepGear: could not list cooked consumables, protecting all consumables: {e}");
                return null;
            }
        }

        private static void AddName(HashSet<string> names, ItemDrop drop)
        {
            var shared = drop != null ? drop.m_itemData?.m_shared : null;
            if (shared != null)
            {
                names.Add(shared.m_name);
            }
        }

        private static void AddIfConsumable(HashSet<string> produced, ItemDrop drop)
        {
            var shared = drop != null ? drop.m_itemData?.m_shared : null;
            if (shared != null && shared.m_itemType.ToString() == ConsumableType)
            {
                produced.Add(shared.m_name);
            }
        }

        private static bool Prepare()
        {
            if (AccessTools.Method(typeof(Inventory), "StackAll") != null)
            {
                return true;
            }

            Plugin.Log.LogError("PlaceStacksKeepGear disabled: Inventory.StackAll not found in this game version");
            return false;
        }

        private static void Prefix(Inventory fromInventory)
        {
            try
            {
                var player = Player.m_localPlayer;
                Active = player != null && fromInventory == player.GetInventory();
            }
            catch (Exception e)
            {
                Active = false;
                Plugin.Log.LogError($"PlaceStacksKeepGear prefix failed: {e}");
            }
        }

        // A finalizer, not a postfix, so the flag is cleared even if vanilla throws.
        private static Exception Finalizer(Exception __exception)
        {
            Active = false;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.IsItemEquiped))]
    internal static class PlaceStacksKeepGearEquippedPatch
    {
        private static void Postfix(Humanoid __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (!PlaceStacksKeepGearPatch.Active || __result || __instance != Player.m_localPlayer)
            {
                return;
            }

            try
            {
                if (PlaceStacksKeepGearPatch.IsProtected(item))
                {
                    __result = true;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"PlaceStacksKeepGear patch failed: {e}");
            }
        }
    }
}
