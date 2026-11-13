using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// The game pauses while the Esc menu is open, but only when nobody else can be affected: you are the server and
    /// no peer is connected (<c>Game.CanPause</c>). The large map does not pause it, so monsters keep attacking and
    /// food keeps running out while you read the map. <c>Game.UpdatePause</c> writes <c>Time.timeScale</c> every frame;
    /// the postfix sets it to 0 while the map is open under the same condition. <c>Game.IsPaused</c> is left alone:
    /// it hides the key hints, and the map shows some.
    /// </summary>
    [HarmonyPatch(typeof(Game), "UpdatePause")]
    internal static class PauseOnMapPatch
    {
        /// <summary>True while this fix holds the game at time scale 0.</summary>
        internal static bool MapPaused;

        private static void Postfix()
        {
            try
            {
                MapPaused = Plugin.PauseOnMapEnabled.Value && Minimap.IsOpen() && CanPause();
                if (MapPaused)
                {
                    Time.timeScale = 0f;
                }
            }
            catch (Exception e)
            {
                MapPaused = false;
                Plugin.Log.LogError($"PauseOnMap patch failed: {e}");
            }
        }

        // Same condition as the private Game.CanPause, without its debug-mode branch for clients.
        private static bool CanPause()
        {
            return ZNet.instance != null
                && Player.m_localPlayer != null
                && ZNet.instance.IsServer()
                && ZNet.instance.GetPeerConnections() == 0;
        }

        /// <summary>Replacement for <c>Time.deltaTime</c> in the map's UI code: real frame time while paused.</summary>
        internal static float DeltaTime()
        {
            return MapPaused ? Mathf.Min(Time.unscaledDeltaTime, Time.maximumDeltaTime) : Time.deltaTime;
        }

        /// <summary>
        /// Replacement for <c>Time.time</c> in the map's UI code. Always the real-time clock while the fix is on, so a
        /// press and its release are measured on the same clock even if the pause starts or ends in between.
        /// </summary>
        internal static float Now()
        {
            return Plugin.PauseOnMapEnabled.Value ? Time.unscaledTime : Time.time;
        }
    }

    /// <summary>
    /// The map times its own UI with game time: click vs. drag and double-click detection (<c>Time.time</c>), pan
    /// inertia, gamepad panning and the delay before the pin name field lets go of the keyboard
    /// (<c>Time.deltaTime</c>). At time scale 0 those stop, so every drag would end in a click, every second click
    /// would count as a double click, and the map could not be closed after naming a pin. This rewrites those calls
    /// in the map's UI methods to <see cref="PauseOnMapPatch.Now"/> and <see cref="PauseOnMapPatch.DeltaTime"/>.
    /// Outside the pause both return what the game would have used, apart from <c>Time.time</c> running ahead of game
    /// time after an earlier pause, which only matters for differences measured on it.
    /// </summary>
    [HarmonyPatch]
    internal static class PauseOnMapClockPatch
    {
        private static readonly string[] Methods =
        {
            "Update", "UpdateMap", "UpdateNameInput", "UpdatePins", "OnMapLeftDown", "OnMapLeftUp",
        };

        private static readonly MethodInfo GetDeltaTime = AccessTools.PropertyGetter(typeof(Time), nameof(Time.deltaTime));
        private static readonly MethodInfo GetTime = AccessTools.PropertyGetter(typeof(Time), nameof(Time.time));
        private static readonly MethodInfo DeltaTime = AccessTools.Method(typeof(PauseOnMapPatch), nameof(PauseOnMapPatch.DeltaTime));
        private static readonly MethodInfo Now = AccessTools.Method(typeof(PauseOnMapPatch), nameof(PauseOnMapPatch.Now));

        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var name in Methods)
            {
                var method = AccessTools.DeclaredMethod(typeof(Minimap), name);
                if (method == null)
                {
                    Plugin.Log.LogWarning($"PauseOnMap: Minimap.{name} is missing in this game version; the map may misread clicks while paused");
                    continue;
                }

                yield return method;
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.Select(code =>
            {
                if (code.Calls(GetDeltaTime))
                {
                    code.operand = DeltaTime;
                }
                else if (code.Calls(GetTime))
                {
                    code.operand = Now;
                }

                return code;
            });
        }
    }
}
