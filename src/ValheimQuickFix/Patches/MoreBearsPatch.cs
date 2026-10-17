using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// Bears (Bjorn) are tuned so rarely that a player can search several Black Forests without meeting one: the spawn
    /// entry rolls once every 2000 s at 15% (day) or 20% (night), with at most one bear alive, and the trophy drops 10%
    /// of the time. This shortens the interval, allows a second bear and raises the trophy chance once, when the game's
    /// prefabs load, by editing the game's own data; the spawn chance is left alone. Nothing runs per frame and no vanilla
    /// logic is replaced.
    /// </summary>
    /// <remarks>
    /// Values are set to a floor (or ceiling), never scaled, so loading a second world in the same session changes nothing
    /// further. Turning the toggle off takes effect on the next game start. Spawning and drops are decided by the machine
    /// that owns the zone or the creature, so this only has an effect when that is you (singleplayer or hosting).
    /// </remarks>
    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class MoreBearsPatch
    {
        internal const float SpawnInterval = 1000f;
        internal const int MaxSpawned = 2;
        internal const float TrophyChance = 0.2f;

        private const string BearPrefab = "Bjorn";
        private const string TrophyPrefab = "TrophyBjorn";

        private static void Postfix(ZNetScene __instance)
        {
            if (!Plugin.MoreBearsEnabled.Value)
            {
                return;
            }

            try
            {
                var trophies = 0;
                foreach (var prefab in __instance.m_prefabs)
                {
                    var drops = prefab != null ? prefab.GetComponent<CharacterDrop>() : null;
                    if (drops == null)
                    {
                        continue;
                    }

                    foreach (var drop in drops.m_drops)
                    {
                        if (drop.m_prefab != null && drop.m_prefab.name == TrophyPrefab && drop.m_chance < TrophyChance)
                        {
                            drop.m_chance = TrophyChance;
                            trophies++;
                        }
                    }
                }

                var spawns = RaiseSpawns();
                Plugin.Log.LogInfo($"MoreBears: raised {spawns} bear spawn entries and {trophies} trophy drops");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"MoreBears failed: {e}");
            }
        }

        // SpawnSystemList is missing from the compile-time libs, so find it and its fields by name.
        private static int RaiseSpawns()
        {
            var listType = typeof(SpawnSystem).Assembly.GetType("SpawnSystemList");
            var spawnersField = listType != null ? AccessTools.Field(listType, "m_spawners") : null;
            var dataType = typeof(SpawnSystem.SpawnData);
            var prefab = AccessTools.Field(dataType, "m_prefab");
            var interval = AccessTools.Field(dataType, "m_spawnInterval");
            var max = AccessTools.Field(dataType, "m_maxSpawned");
            if (spawnersField == null || prefab == null || interval == null || max == null)
            {
                Plugin.Log.LogWarning("MoreBears: bear spawn entries not changed, SpawnSystemList or SpawnData fields are missing in this game version");
                return 0;
            }

            var changed = 0;
            foreach (var list in Resources.FindObjectsOfTypeAll(listType))
            {
                foreach (var entry in (IEnumerable)spawnersField.GetValue(list))
                {
                    var bear = prefab.GetValue(entry) as GameObject;
                    if (bear == null || bear.name != BearPrefab)
                    {
                        continue;
                    }

                    interval.SetValue(entry, Math.Min((float)interval.GetValue(entry), SpawnInterval));
                    max.SetValue(entry, Math.Max((int)max.GetValue(entry), MaxSpawned));
                    changed++;
                }
            }

            return changed;
        }
    }
}
