using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// Edits the game's world spawn entries and creature drops in place, for the fixes that make a creature easier to
    /// find (<see cref="MoreBearsPatch"/>, <see cref="MoreWrithansPatch"/>).
    /// </summary>
    /// <remarks>
    /// Each spawn entry and drop is changed at most once per session, and the vanilla and new spawn values are logged.
    /// <c>SpawnSystemList</c> is missing from the compile-time libs, so it and the spawn entry fields are found by name.
    /// </remarks>
    internal static class SpawnTuning
    {
        private static readonly HashSet<object> Tuned = new HashSet<object>();

        /// <summary>
        /// Sets the spawn interval, the maximum alive count and, when <paramref name="spawnChance"/> is given, the spawn
        /// chance (in percent) of every spawn entry for <paramref name="prefabName"/>. Returns the number of entries changed.
        /// </summary>
        internal static int TuneSpawns(
            string fixName,
            string prefabName,
            Func<float, float> interval,
            Func<int, int> maxSpawned,
            Func<float, float> spawnChance = null)
        {
            var listType = typeof(SpawnSystem).Assembly.GetType("SpawnSystemList");
            var spawnersField = listType != null ? AccessTools.Field(listType, "m_spawners") : null;
            var dataType = typeof(SpawnSystem.SpawnData);
            var prefab = AccessTools.Field(dataType, "m_prefab");
            var intervalField = AccessTools.Field(dataType, "m_spawnInterval");
            var maxField = AccessTools.Field(dataType, "m_maxSpawned");
            var chanceField = AccessTools.Field(dataType, "m_spawnChance");
            if (spawnersField == null || prefab == null || intervalField == null || maxField == null || chanceField == null)
            {
                Plugin.Log.LogWarning($"{fixName}: {prefabName} spawn entries not changed, SpawnSystemList or SpawnData fields are missing in this game version");
                return 0;
            }

            var changed = 0;
            foreach (var list in Resources.FindObjectsOfTypeAll(listType))
            {
                foreach (var entry in (IEnumerable)spawnersField.GetValue(list))
                {
                    var creature = prefab.GetValue(entry) as GameObject;
                    if (creature == null || creature.name != prefabName || !Tuned.Add(entry))
                    {
                        continue;
                    }

                    var oldInterval = (float)intervalField.GetValue(entry);
                    var oldMax = (int)maxField.GetValue(entry);
                    var oldChance = (float)chanceField.GetValue(entry);
                    intervalField.SetValue(entry, interval(oldInterval));
                    maxField.SetValue(entry, maxSpawned(oldMax));
                    if (spawnChance != null)
                    {
                        chanceField.SetValue(entry, spawnChance(oldChance));
                    }

                    Plugin.Log.LogInfo($"{fixName}: {prefabName} spawn entry in {((UnityEngine.Object)list).name}: interval {oldInterval} s -> {intervalField.GetValue(entry)} s, chance {oldChance}% -> {chanceField.GetValue(entry)}%, max alive {oldMax} -> {maxField.GetValue(entry)}");
                    changed++;
                }
            }

            return changed;
        }

        /// <summary>
        /// Sets the drop chance of every <paramref name="itemName"/> drop on any creature. Returns the number changed.
        /// </summary>
        internal static int TuneDrops(ZNetScene scene, string itemName, Func<float, float> chance)
        {
            var changed = 0;
            foreach (var prefab in scene.m_prefabs)
            {
                var drops = prefab != null ? prefab.GetComponent<CharacterDrop>() : null;
                if (drops == null)
                {
                    continue;
                }

                foreach (var drop in drops.m_drops)
                {
                    if (drop.m_prefab != null && drop.m_prefab.name == itemName && Tuned.Add(drop))
                    {
                        drop.m_chance = chance(drop.m_chance);
                        changed++;
                    }
                }
            }

            return changed;
        }
    }
}
