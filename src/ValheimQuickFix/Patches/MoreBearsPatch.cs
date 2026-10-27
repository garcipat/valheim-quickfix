using System;
using HarmonyLib;

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
                var trophies = SpawnTuning.TuneDrops(__instance, TrophyPrefab, chance => Math.Max(chance, TrophyChance));
                var spawns = SpawnTuning.TuneSpawns(
                    "MoreBears",
                    BearPrefab,
                    interval => Math.Min(interval, SpawnInterval),
                    max => Math.Max(max, MaxSpawned));
                Plugin.Log.LogInfo($"MoreBears: raised {spawns} bear spawn entries and {trophies} trophy drops");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"MoreBears failed: {e}");
            }
        }
    }
}
