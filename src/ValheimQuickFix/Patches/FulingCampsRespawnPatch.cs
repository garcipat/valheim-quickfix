using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// Fuling villages are filled by spawners that spawn once and never again, so a cleared village stays empty. This
    /// lets a cleared Fuling spawner within reach of a standing totem spawn its Fuling again, up to
    /// <see cref="SpawnsPerWave"/> Fulings per totem per interval, the way tar pits keep producing Growths. Smashing the totem stops it for good.
    /// </summary>
    /// <remarks>
    /// The spawner's respawn time is set to the interval only for the duration of one vanilla update, so the game's own
    /// respawn checks (player in range, player base, day/night) decide the spawn. A Fuling comes back only after the
    /// interval has passed since this session first saw it gone. The totem's
    /// wave starts with the first spawn that actually happens; spawns that don't make it into the
    /// <see cref="WaveSeconds"/> after it wait for the next wave. Totems are found among the loaded objects: a spawner only updates
    /// while its area is loaded, and a smashed totem's object is gone whoever smashed it. Spawning is decided by the
    /// machine that owns the zone, so this only has an effect when that is you (singleplayer or hosting), and other
    /// players see the respawned Fulings.
    /// </remarks>
    [HarmonyPatch(typeof(CreatureSpawner), "UpdateSpawner")]
    internal static class FulingCampsRespawnPatch
    {
        private const float TotemCacheSeconds = 30f;
        private const int SpawnsPerWave = 2;
        private const double WaveSeconds = 60;

        private static readonly HashSet<string> FulingPrefabs = new HashSet<string> { "Goblin", "GoblinBrute", "GoblinShaman" };
        private static readonly int TotemPrefab = "goblin_totem".GetStableHashCode();

        private static readonly AccessTools.FieldRef<CreatureSpawner, ZNetView> NView =
            AccessTools.FieldRefAccess<CreatureSpawner, ZNetView>("m_nview");
        private static readonly AccessTools.FieldRef<ZNetScene, Dictionary<ZDO, ZNetView>> Instances =
            AccessTools.FieldRefAccess<ZNetScene, Dictionary<ZDO, ZNetView>>("m_instances");

        // ZDO connections are missing from the compile-time libs, so the spawned creature's id is read by name.
        private static readonly MethodInfo GetConnection = AccessTools.Method(typeof(ZDO), "GetConnectionZDOID");
        private static readonly object[] SpawnedArgs = SpawnedConnectionArgs();

        private static readonly Dictionary<ZDOID, Wave> Waves = new Dictionary<ZDOID, Wave>();
        private static readonly Dictionary<ZDOID, DateTime> DeadSince = new Dictionary<ZDOID, DateTime>();
        private static readonly Dictionary<CreatureSpawner, KeyValuePair<ZDO, float>> TotemCache =
            new Dictionary<CreatureSpawner, KeyValuePair<ZDO, float>>();

        private static ZDOID _feedingTotem = ZDOID.None;
        private static bool _failed;

        private struct Wave
        {
            public DateTime Started;
            public int Spawned;
        }

        private struct State
        {
            public bool Active;
            public float OldMinutes;
        }

        private static void Prefix(CreatureSpawner __instance, ref State __state)
        {
            if (!Plugin.FulingCampsRespawnEnabled.Value || _failed)
            {
                return;
            }

            if (GetConnection == null || SpawnedArgs == null)
            {
                _failed = true;
                Plugin.Log.LogError("FulingCampsRespawn: the game's spawner connections were not found, so it stays off");
                return;
            }

            try
            {
                var nview = NView(__instance);
                if (nview == null || !nview.IsValid() || !nview.IsOwner() || __instance.m_respawnTimeMinuts > 0f
                    || __instance.m_creaturePrefab == null || !FulingPrefabs.Contains(__instance.m_creaturePrefab.name))
                {
                    return;
                }

                var spawned = (ZDOID)GetConnection.Invoke(nview.GetZDO(), SpawnedArgs);
                if (spawned.IsNone() || ZDOMan.instance.GetZDO(spawned) != null)
                {
                    return;
                }

                // Vanilla stops tracking a one-shot spawner's alive time, so the wait after a death is timed here.
                var now = ZNet.instance.GetTime();
                var spawner = nview.GetZDO().m_uid;
                if (!DeadSince.TryGetValue(spawner, out var since))
                {
                    DeadSince[spawner] = now;
                    return;
                }

                if ((now - since).TotalMinutes < Plugin.FulingCampsRespawnMinutes.Value)
                {
                    return;
                }

                var totem = FindTotem(__instance);
                if (totem == null)
                {
                    return;
                }

                if (Waves.TryGetValue(totem.m_uid, out var wave) && !CanJoinOrStart(wave, now))
                {
                    return;
                }

                __state = new State { Active = true, OldMinutes = __instance.m_respawnTimeMinuts };
                __instance.m_respawnTimeMinuts = Plugin.FulingCampsRespawnMinutes.Value;
                _feedingTotem = totem.m_uid;
            }
            catch (Exception e)
            {
                _failed = true;
                Plugin.Log.LogError($"FulingCampsRespawn failed and is off until the next game start: {e}");
            }
        }

        private static void Finalizer(CreatureSpawner __instance, State __state)
        {
            if (__state.Active)
            {
                __instance.m_respawnTimeMinuts = __state.OldMinutes;
                _feedingTotem = ZDOID.None;
            }
        }

        private static bool IsOpen(Wave wave, DateTime now)
        {
            return wave.Spawned < SpawnsPerWave && (now - wave.Started).TotalSeconds < WaveSeconds;
        }

        private static bool CanJoinOrStart(Wave wave, DateTime now)
        {
            return IsOpen(wave, now) || (now - wave.Started).TotalMinutes >= Plugin.FulingCampsRespawnMinutes.Value;
        }

        private static object[] SpawnedConnectionArgs()
        {
            var type = typeof(ZDO).Assembly.GetType("ZDOExtraData+ConnectionType");
            return type != null ? new[] { Enum.Parse(type, "Spawned") } : null;
        }

        /// <summary>
        /// Counts the spawn toward the wave of the totem that allowed this update, also when a spawn group picked another spawner.
        /// </summary>
        [HarmonyPatch(typeof(CreatureSpawner), "Spawn")]
        private static class SpawnPatch
        {
            private static void Postfix(CreatureSpawner __instance, ZNetView __result)
            {
                if (_feedingTotem.IsNone() || __result == null)
                {
                    return;
                }

                var now = ZNet.instance.GetTime();
                var wave = Waves.TryGetValue(_feedingTotem, out var current) && IsOpen(current, now)
                    ? new Wave { Started = current.Started, Spawned = current.Spawned + 1 }
                    : new Wave { Started = now, Spawned = 1 };
                Waves[_feedingTotem] = wave;
                DeadSince.Remove(NView(__instance).GetZDO().m_uid);
                Plugin.Log.LogInfo(
                    $"FulingCampsRespawn: {__result.name} respawned at {__instance.transform.position} ({wave.Spawned}/{SpawnsPerWave} this wave)");
            }
        }

        /// <summary>
        /// Returns the closest standing totem within the configured radius of the spawner, or null. The answer is kept
        /// for <see cref="TotemCacheSeconds"/> so a waiting spawner doesn't search every update.
        /// </summary>
        private static ZDO FindTotem(CreatureSpawner spawner)
        {
            if (TotemCache.TryGetValue(spawner, out var cached) && Time.time - cached.Value < TotemCacheSeconds)
            {
                var zdo = cached.Key;
                return zdo != null && zdo.IsValid() && zdo.GetPrefab() == TotemPrefab && ZDOMan.instance.GetZDO(zdo.m_uid) != null
                    ? zdo
                    : null;
            }

            var pos = spawner.transform.position;
            var best = Plugin.FulingCampsTotemRadius.Value;
            ZDO totem = null;
            foreach (var zdo in Instances(ZNetScene.instance).Keys)
            {
                if (zdo.GetPrefab() != TotemPrefab)
                {
                    continue;
                }

                var distance = Utils.DistanceXZ(zdo.GetPosition(), pos);
                if (distance <= best)
                {
                    best = distance;
                    totem = zdo;
                }
            }

            if (TotemCache.Count > 512)
            {
                TotemCache.Clear();
            }

            TotemCache[spawner] = new KeyValuePair<ZDO, float>(totem, Time.time);
            return totem;
        }
    }
}
