using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// Filling a smelter, kiln, blast furnace or any other <c>Smelter</c> takes one E press per piece of ore or fuel. Holding
    /// E on the ore or fuel slot now fills it to capacity at once from what you carry, and the hover text says so. A tap
    /// still adds one, as before.
    /// </summary>
    /// <remarks>
    /// Each piece goes in through the game's own <c>OnAddOre</c> / <c>OnAddFuel</c>, so the item checks, messages and the
    /// add RPC to the smelter's owner are the vanilla ones. When someone else owns the smelter, its queue only updates once
    /// that RPC arrives, so the number of free slots is worked out once, up front, counting the piece the press added.
    /// Those members are private, so they are looked up by name and the patch disables itself, with a log line, if a game
    /// version lacks one.
    /// </remarks>
    internal static class SmelterFillOnHoldPatch
    {
        // How long E must stay down before it counts as a hold. The game repeats the interaction about every 0.2 s.
        private const float HoldSeconds = 0.5f;
        private const string HoverHint = " <color=#cccccc>(Hold to fill)</color>";

        private static readonly MethodInfo AddOre = AccessTools.Method(typeof(Smelter), "OnAddOre");
        private static readonly MethodInfo AddFuel = AccessTools.Method(typeof(Smelter), "OnAddFuel");
        private static readonly MethodInfo QueueSize = AccessTools.Method(typeof(Smelter), "GetQueueSize");
        private static readonly MethodInfo Fuel = AccessTools.Method(typeof(Smelter), "GetFuel");
        private static readonly MethodInfo HoverAddOre = AccessTools.Method(typeof(Smelter), "OnHoverAddOre");
        private static readonly MethodInfo HoverAddFuel = AccessTools.Method(typeof(Smelter), "OnHoverAddFuel");

        private static bool _loggedMissing;

        // The slot whose E press is being held, when it was pressed, how full it should be after the press, and whether the
        // hold already filled it.
        private static Switch _switch;
        private static float _pressedAt;
        private static float _expected;
        private static bool _filled;

        private static bool Available()
        {
            if (AddOre != null && AddFuel != null && QueueSize != null && Fuel != null)
            {
                return true;
            }

            if (!_loggedMissing)
            {
                _loggedMissing = true;
                Plugin.Log.LogError("SmelterFillOnHold disabled: Smelter.OnAddOre, OnAddFuel, GetQueueSize or GetFuel not found in this game version");
            }

            return false;
        }

        // The smelter this switch is the ore or fuel slot of, or null for any other switch (including its "empty" slot).
        private static Smelter FindSmelter(Switch sw, out bool ore)
        {
            ore = false;
            var smelter = sw.GetComponentInParent<Smelter>();
            if (smelter == null)
            {
                return null;
            }

            if (smelter.m_addOreSwitch == sw)
            {
                ore = true;
                return smelter;
            }

            return smelter.m_addWoodSwitch == sw ? smelter : null;
        }

        private static float Current(Smelter smelter, bool ore)
        {
            return ore ? (int)QueueSize.Invoke(smelter, null) : (float)Fuel.Invoke(smelter, null);
        }

        private static void Fill(Smelter smelter, Switch sw, bool ore, Player player)
        {
            // Fuel can go in while it is at most one below the maximum, the same as the game's own check.
            var current = Mathf.Max(Current(smelter, ore), _expected);
            var free = ore ? smelter.m_maxOre - (int)current : Mathf.FloorToInt(smelter.m_maxFuel - current);
            var add = ore ? AddOre : AddFuel;
            var args = new object[] { sw, player, null };
            for (var i = 0; i < free; i++)
            {
                if (!(bool)add.Invoke(smelter, args))
                {
                    break;
                }
            }
        }

        [HarmonyPatch(typeof(Switch), nameof(Switch.Interact))]
        private static class InteractPatch
        {
            // Returning false skips the game's own repeat, which would add one more piece every hold interval.
            private static bool Prefix(Switch __instance, Humanoid character, bool hold, ref bool __result, out bool __state)
            {
                __state = false;
                try
                {
                    var player = Player.m_localPlayer;
                    if (!Plugin.SmelterFillOnHoldEnabled.Value || player == null || character != player || !Available())
                    {
                        return true;
                    }

                    var smelter = FindSmelter(__instance, out var ore);
                    if (smelter == null)
                    {
                        return true;
                    }

                    if (!hold)
                    {
                        _switch = __instance;
                        _pressedAt = Time.time;
                        _expected = Current(smelter, ore);
                        _filled = false;
                        __state = true;
                        return true;
                    }

                    if (_switch == __instance && !_filled && Time.time - _pressedAt >= HoldSeconds)
                    {
                        _filled = true;
                        Fill(smelter, __instance, ore, player);
                    }

                    __result = false;
                    return false;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"SmelterFillOnHold failed: {e}");
                    return true;
                }
            }

            // The press itself added one piece if the game's Interact succeeded.
            private static void Postfix(bool __result, bool __state)
            {
                if (__state && __result)
                {
                    _expected += 1f;
                }
            }
        }

        [HarmonyPatch]
        private static class HoverTextPatch
        {
            private static bool Prepare()
            {
                return HoverAddOre != null && HoverAddFuel != null;
            }

            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return HoverAddOre;
                yield return HoverAddFuel;
            }

            private static void Postfix(ref string __result)
            {
                if (Plugin.SmelterFillOnHoldEnabled.Value)
                {
                    __result += HoverHint;
                }
            }
        }
    }
}
