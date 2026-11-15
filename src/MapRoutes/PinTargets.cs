using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimMapRoutes
{
    /// <summary>
    /// Recognises what the player looks at by its component and the game's internal names, so it works in any game
    /// language, and gives the pin for it. Circle pin names are a pin icon's own name, so the pin shows only the icon; a portal
    /// gets a rune pin with its tag.
    /// </summary>
    internal static class PinTargets
    {
        // The game's circle pin, fourth in the pin picker, and the rune pin, the last one.
        internal const Minimap.PinType CirclePin = Minimap.PinType.Icon3;
        private const Minimap.PinType RunePin = Minimap.PinType.Icon4;

        // Dungeon entrances only say "Enter"; the location they belong to tells which one it is, by name prefix.
        private static readonly KeyValuePair<string, string>[] Entrances =
        {
            new KeyValuePair<string, string>("SunkenCrypt", "Crypt"),
            new KeyValuePair<string, string>("Crypt", "Dungeon"),
            new KeyValuePair<string, string>("TrollCave", "Cave"),
            new KeyValuePair<string, string>("MountainCave", "Cave"),
            new KeyValuePair<string, string>("Mistlands_DvergrTownEntrance", "Mine"),
        };

        // By the item a pickable gives.
        private static readonly Dictionary<string, string> Pickables = new Dictionary<string, string>
        {
            { "Raspberry", "Berries" },
            { "Blueberries", "Berries" },
            { "Cloudberry", "Berries" },
            { "Mushroom", "Mushroom" },
            { "MushroomYellow", "Mushroom" },
            { "MushroomBlue", "Mushroom" },
            { "MushroomJotunPuffs", "Mushroom" },
            { "MushroomMagecap", "Mushroom" },
            { "Tar", "Tar" },
            { "DragonEgg", "Egg" },
        };

        // Deposits are recognised by the ore they drop.
        private static readonly HashSet<string> Ores = new HashSet<string> { "CopperOre", "TinOre", "SilverOre" };

        // m_spawnWhenDamaged is newer than the compile-time game libraries, so both are reached by name.
        private static readonly FieldInfo[] DestructibleSpawnFields =
        {
            AccessTools.Field(typeof(Destructible), "m_spawnWhenDamaged"),
            AccessTools.Field(typeof(Destructible), "m_spawnWhenDestroyed"),
        };

        internal static bool TryGet(GameObject hoverObject, out string name, out Minimap.PinType type,
            out Vector3 position)
        {
            name = null;
            type = CirclePin;
            position = Vector3.zero;
            var hoverable = hoverObject == null ? null : hoverObject.GetComponentInParent<Hoverable>() as Component;
            if (hoverable == null)
            {
                return false;
            }
            position = hoverable.transform.position;
            if (hoverable is TeleportWorld portal)
            {
                // Portals get the game's rune pin, named with the portal's tag, as players mark them by hand.
                name = portal.GetText()?.Trim() ?? "";
                type = RunePin;
                return true;
            }
            name = NameFor(hoverable);
            return name != null;
        }

        internal static string Describe(GameObject hoverObject)
        {
            var hoverable = hoverObject == null ? null : hoverObject.GetComponentInParent<Hoverable>() as Component;
            if (hoverable == null)
            {
                return "nothing";
            }
            var components = string.Join(", ", hoverable.GetComponents<Component>()
                .Where(c => c != null && !(c is Transform))
                .Select(c => c.GetType().Name)
                .ToArray());
            var location = hoverable.GetComponentInParent<Location>();
            var where = location == null ? "" : $" in location '{PrefabName(location.gameObject)}'";
            return $"'{PrefabName(hoverable.gameObject)}' ({components}){where}";
        }

        private static string NameFor(Component hoverable)
        {
            switch (hoverable)
            {
                case Teleport _:
                    var location = hoverable.GetComponentInParent<Location>();
                    var locationName = location == null ? "" : PrefabName(location.gameObject);
                    foreach (var entrance in Entrances)
                    {
                        if (locationName.StartsWith(entrance.Key, StringComparison.Ordinal))
                        {
                            return entrance.Value;
                        }
                    }
                    return "Dungeon";
                case ShipControlls _:
                    return "Ship";
                case Pickable pickable:
                    return pickable.m_itemPrefab != null && Pickables.TryGetValue(pickable.m_itemPrefab.name, out var picked)
                        ? picked
                        : null;
                case ItemDrop item:
                    var prefab = item.m_itemData?.m_dropPrefab;
                    return prefab != null && prefab.name == "DragonEgg" ? "Egg" : null;
                case MineRock rock:
                    return OreIn(rock.m_dropItems);
                case MineRock5 rock5:
                    return OreIn(rock5.m_dropItems);
                default:
                    return OreOf(hoverable.gameObject, followDestructible: true);
            }
        }

        // An untouched deposit is a Destructible that is swapped for its broken variant on the first hit; only that
        // variant has the drops, so the deposit is recognised by what it turns into.
        private static string OreOf(GameObject go, bool followDestructible)
        {
            var rock5 = go.GetComponentInParent<MineRock5>();
            if (rock5 != null)
            {
                return OreIn(rock5.m_dropItems);
            }
            var rock = go.GetComponentInParent<MineRock>();
            if (rock != null)
            {
                return OreIn(rock.m_dropItems);
            }
            var drops = go.GetComponentInParent<DropOnDestroyed>();
            var ore = drops == null ? null : OreIn(drops.m_dropWhenDestroyed);
            if (ore != null || !followDestructible)
            {
                return ore;
            }
            var destructible = go.GetComponentInParent<Destructible>();
            if (destructible == null)
            {
                return null;
            }
            foreach (var field in DestructibleSpawnFields)
            {
                if (field?.GetValue(destructible) is GameObject spawned && spawned != null)
                {
                    ore = OreOf(spawned, followDestructible: false);
                    if (ore != null)
                    {
                        return ore;
                    }
                }
            }
            return null;
        }

        private static string OreIn(DropTable table)
        {
            if (table?.m_drops == null)
            {
                return null;
            }
            foreach (var drop in table.m_drops)
            {
                if (drop.m_item != null && Ores.Contains(drop.m_item.name))
                {
                    return "Ore";
                }
            }
            return null;
        }

        // Instances are named "<prefab>(Clone)"; locations can also carry a " (1)" suffix in the scene.
        private static string PrefabName(GameObject go)
        {
            var name = go.name;
            var cut = name.IndexOf('(');
            return (cut < 0 ? name : name.Substring(0, cut)).Trim();
        }
    }
}
