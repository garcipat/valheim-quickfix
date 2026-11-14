using System;
using HarmonyLib;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// Writhans are tuned so rarely that a player can search many Swamps without meeting one: the spawn entry rolls once
    /// every 4000 s at 10%, with at most one alive, only in the Swamp interior below 10 m and 2000 to 8000 m from the
    /// world centre. This halves the interval and raises the spawn chance once, when the game's prefabs load, by editing
    /// the game's own data; the one-alive cap, the location limits and the trophy chance are left alone. Nothing runs per
    /// frame and no vanilla logic is replaced.
    /// </summary>
    /// <remarks>
    /// Values are set to a floor (or ceiling), never scaled, so loading a second world in the same session changes nothing
    /// further. Turning the toggle off takes effect on the next game start. Spawning is decided by the machine that owns
    /// the zone, so this only has an effect when that is you (singleplayer or hosting).
    /// A zone that has not rolled for a whole interval rolls as soon as a player enters it, once per Writhan it may still
    /// hold, so while travelling the chance and the cap set the rate, not the interval. Raising the cap to two made every
    /// new Swamp zone roll twice and let pairs live side by side, which turned out far too many; keep it at one.
    /// </remarks>
    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class MoreWrithansPatch
    {
        internal const float SpawnInterval = 2000f;
        internal const float SpawnChance = 15f;

        private const string WrithanPrefab = "Writhan";

        private static void Postfix()
        {
            if (!Plugin.MoreWrithansEnabled.Value)
            {
                return;
            }

            try
            {
                var spawns = SpawnTuning.TuneSpawns(
                    "MoreWrithans",
                    WrithanPrefab,
                    interval => Math.Min(interval, SpawnInterval),
                    max => max,
                    chance => Math.Max(chance, SpawnChance));
                Plugin.Log.LogInfo($"MoreWrithans: raised {spawns} Writhan spawn entries");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"MoreWrithans failed: {e}");
            }
        }
    }
}
