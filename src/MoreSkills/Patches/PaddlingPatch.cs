using System;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using ValheimMoreSkills.Core;

namespace ValheimMoreSkills.Patches
{
    /// <summary>
    /// Paddling: levels while you paddle a ship (Slow forward or Back) and makes it paddle faster.
    /// </summary>
    internal static class Paddling
    {
        internal const string Identifier = "dev.pgarcia.valheim.moreskills.paddling";

        internal static readonly Skills.SkillType Type = CustomSkill.TypeOf(Identifier);

        internal static CustomSkill Create(ConfigEntry<bool> enabled)
        {
            return new CustomSkill(
                Identifier,
                "Paddling",
                "Paddling a ship without the sail. Higher levels make you paddle faster.",
                KarveIcon,
                enabled);
        }

        // The game's own Karve piece icon; null before ZNetScene exists (main menu).
        private static Sprite KarveIcon()
        {
            if (ZNetScene.instance == null)
            {
                return null;
            }

            var prefab = ZNetScene.instance.GetPrefab("Karve");
            var piece = prefab == null ? null : prefab.GetComponent<Piece>();
            return piece == null ? null : piece.m_icon;
        }

        // The live game returns the helmsman's player ID; the compile-time libs still have an older signature, so it is
        // called by name.
        private static readonly Func<ShipControlls, long> GetUser =
            AccessTools.MethodDelegate<Func<ShipControlls, long>>(AccessTools.Method(typeof(ShipControlls), "GetUser"));

        internal static bool IsHelmsman(Ship ship, Player player)
        {
            return player != null && ship.m_shipControlls != null
                && GetUser(ship.m_shipControlls) == player.GetPlayerID();
        }

        internal static bool IsPaddling(Ship.Speed speed)
        {
            return speed == Ship.Speed.Slow || speed == Ship.Speed.Back;
        }
    }

    /// <summary>
    /// <c>Ship.CustomFixedUpdate</c> simulates the ship on its owner only. In Slow and Back it pushes with
    /// <c>m_backwardForce</c>; half and full sail don't use it. While the local player steers and owns the ship, a
    /// Prefix scales that force by <c>1 + MaxPaddleBonus * Paddling skill factor</c> and a Finalizer puts the original
    /// back. With another player aboard owning the ship, the helmsman paddles at vanilla speed.
    /// </summary>
    /// <remarks>
    /// The Postfix levels the skill: while the local player steers in Slow or Back, it counts how far the ship moves;
    /// once per second, if that averaged at least <see cref="MinSpeed"/>, it raises Paddling by
    /// <c>distance * GainPerMeter</c> (about 0.1 to 0.15 per second at paddling speed). That needs no ownership, since
    /// skills are local.
    /// </remarks>
    [HarmonyPatch(typeof(Ship), "CustomFixedUpdate")]
    internal static class PaddlingPatch
    {
        private const float Interval = 1f;
        private const float MinSpeed = 0.5f;
        // A step longer than this in one physics frame is a teleport, not paddling.
        private const float MaxStep = 2f;
        private const float GainPerMeter = 0.05f;

        private static readonly AccessTools.FieldRef<Ship, ZNetView> NView =
            AccessTools.FieldRefAccess<Ship, ZNetView>("m_nview");

        private static readonly AccessTools.FieldRef<Ship, Ship.Speed> Speed =
            AccessTools.FieldRefAccess<Ship, Ship.Speed>("m_speed");

        private static Ship _ship;
        private static Vector3 _lastPosition;
        private static float _distance;
        private static float _timer;

        private static void Prefix(Ship __instance, out float __state)
        {
            __state = -1f;
            try
            {
                if (!Plugin.PaddlingEnabled.Value)
                {
                    return;
                }

                var player = Player.m_localPlayer;
                var nview = NView(__instance);
                if (nview == null || !nview.IsOwner() || !Paddling.IsHelmsman(__instance, player))
                {
                    return;
                }

                __state = __instance.m_backwardForce;
                var bonus = Plugin.MaxPaddleBonus.Value * player.GetSkills().GetSkillFactor(Paddling.Type);
                __instance.m_backwardForce = __state * (1f + bonus);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Paddling patch failed: {e}");
            }
        }

        private static void Postfix(Ship __instance, float fixedDeltaTime)
        {
            try
            {
                if (!Plugin.PaddlingEnabled.Value)
                {
                    return;
                }

                var player = Player.m_localPlayer;
                if (!Paddling.IsHelmsman(__instance, player) || !Paddling.IsPaddling(Speed(__instance)))
                {
                    if (_ship == __instance)
                    {
                        _ship = null;
                    }

                    return;
                }

                var position = __instance.transform.position;
                if (_ship != __instance)
                {
                    _ship = __instance;
                    _lastPosition = position;
                    _distance = 0f;
                    _timer = 0f;
                    return;
                }

                var step = Vector3.Distance(position, _lastPosition);
                _lastPosition = position;
                if (step <= MaxStep)
                {
                    _distance += step;
                }

                _timer += fixedDeltaTime;
                if (_timer < Interval)
                {
                    return;
                }

                var distance = _distance;
                var speed = distance / _timer;
                _distance = 0f;
                _timer = 0f;

                if (speed >= MinSpeed)
                {
                    player.RaiseSkill(Paddling.Type, distance * GainPerMeter);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Paddling patch failed: {e}");
            }
        }

        private static void Finalizer(Ship __instance, float __state)
        {
            if (__state >= 0f)
            {
                __instance.m_backwardForce = __state;
            }
        }
    }
}
