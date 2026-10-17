using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// The crafting window repairs one item per click of its Repair button. Holding E on a workbench, forge or any other
    /// station that can repair now repairs everything you carry without opening the window, and the station's hover text
    /// says so. A tap still opens the window, a moment later: when E comes up.
    /// </summary>
    /// <remarks>
    /// The work is done by the game's own <c>InventoryGui.RepairOneItem</c>, called until nothing is left to repair, so the
    /// crafting skill, effects and messages are the vanilla ones. It reads the player's current station, which is only set
    /// while the window is open, so the station is put there for the repair and the old value restored. Those members are
    /// private, so they are looked up by name and the patch disables itself, with a log line, if a game version lacks one.
    /// </remarks>
    internal static class RepairAllOnHoldPatch
    {
        // How long E must stay down before it counts as a hold. The game repeats the interaction about every 0.2 s.
        private const float HoldSeconds = 0.5f;
        private const string HoverHint = " <color=#cccccc>(Hold to repair all)</color>";

        private static readonly MethodInfo RepairOne = AccessTools.Method(typeof(InventoryGui), "RepairOneItem");
        private static readonly MethodInfo HaveRepairable = AccessTools.Method(typeof(InventoryGui), "HaveRepairableItems");
        private static readonly MethodInfo InUseDistance = AccessTools.Method(typeof(CraftingStation), "InUseDistance");
        private static readonly FieldInfo CanRepair = AccessTools.Field(typeof(CraftingStation), "m_canRepair");
        private static readonly FieldInfo CurrentStation = AccessTools.Field(typeof(Player), "m_currentStation");

        private static bool _loggedMissing;

        // The station whose E press is being held, when it was pressed, and whether the hold already repaired.
        private static CraftingStation _station;
        private static float _pressedAt;
        private static bool _repaired;
        private static bool _opening;

        private static bool Available()
        {
            if (RepairOne != null && HaveRepairable != null && InUseDistance != null && CanRepair != null && CurrentStation != null)
            {
                return true;
            }

            if (!_loggedMissing)
            {
                _loggedMissing = true;
                Plugin.Log.LogError("RepairAllOnHold disabled: InventoryGui repair methods, CraftingStation.m_canRepair or Player.m_currentStation not found in this game version");
            }

            return false;
        }

        private static void RepairAll(CraftingStation station, Player player)
        {
            var gui = InventoryGui.instance;
            if (gui == null)
            {
                return;
            }

            var previous = CurrentStation.GetValue(player);
            CurrentStation.SetValue(player, station);
            try
            {
                // At least one call, so a player with nothing damaged still gets the game's "No more item to repair".
                do
                {
                    RepairOne.Invoke(gui, null);
                }
                while ((bool)HaveRepairable.Invoke(gui, null));
            }
            finally
            {
                CurrentStation.SetValue(player, previous);
            }
        }

        [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.Interact))]
        private static class InteractPatch
        {
            // Returning false skips the game's own Interact, which would open the crafting window on the first frame.
            private static bool Prefix(CraftingStation __instance, Humanoid user, bool repeat)
            {
                try
                {
                    var player = Player.m_localPlayer;
                    if (_opening || !Plugin.RepairAllOnHoldEnabled.Value || player == null || user != player || !Available()
                        || !(bool)CanRepair.GetValue(__instance))
                    {
                        return true;
                    }

                    if (!repeat)
                    {
                        _station = __instance;
                        _pressedAt = Time.time;
                        _repaired = false;
                        return false;
                    }

                    if (_station == __instance && !_repaired && Time.time - _pressedAt >= HoldSeconds)
                    {
                        _repaired = true;
                        RepairAll(__instance, player);
                    }

                    return true;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"RepairAllOnHold failed: {e}");
                    return true;
                }
            }
        }

        // A press that was not held into a repair opens the window once E comes up, as it would have on the press.
        [HarmonyPatch(typeof(Player), "Update")]
        private static class ReleasePatch
        {
            private static void Postfix(Player __instance)
            {
                if (_station == null || __instance != Player.m_localPlayer || ZInput.GetButton("Use") || ZInput.GetButton("JoyUse"))
                {
                    return;
                }

                var station = _station;
                var repaired = _repaired;
                _station = null;
                if (repaired || station == null)
                {
                    return;
                }

                try
                {
                    _opening = true;
                    station.Interact(__instance, false, false);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"RepairAllOnHold failed to open the station: {e}");
                }
                finally
                {
                    _opening = false;
                }
            }
        }

        [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.GetHoverText))]
        private static class HoverTextPatch
        {
            private static void Postfix(CraftingStation __instance, ref string __result)
            {
                try
                {
                    if (!Plugin.RepairAllOnHoldEnabled.Value || !Available() || !(bool)CanRepair.GetValue(__instance)
                        || Player.m_localPlayer == null || !(bool)InUseDistance.Invoke(__instance, new object[] { Player.m_localPlayer }))
                    {
                        return;
                    }

                    __result += HoverHint;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"RepairAllOnHold hover text failed: {e}");
                }
            }
        }
    }
}
