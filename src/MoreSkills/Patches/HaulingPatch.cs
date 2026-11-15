using System;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using ValheimMoreSkills.Core;

namespace ValheimMoreSkills.Patches
{
    /// <summary>
    /// Hauling: levels while you pull a loaded cart and makes the cargo weigh less on you.
    /// </summary>
    internal static class Hauling
    {
        internal const string Identifier = "dev.pgarcia.valheim.moreskills.hauling";

        internal static readonly Skills.SkillType Type = CustomSkill.TypeOf(Identifier);

        internal static CustomSkill Create(ConfigEntry<bool> enabled)
        {
            return new CustomSkill(
                Identifier,
                "Hauling",
                "Pulling a loaded cart. Higher levels make its cargo weigh less on you.",
                CartIcon,
                enabled);
        }

        // The game's own cart piece icon; null before ZNetScene exists (main menu).
        private static Sprite CartIcon()
        {
            if (ZNetScene.instance == null)
            {
                return null;
            }

            var prefab = ZNetScene.instance.GetPrefab("Cart");
            if (prefab == null)
            {
                return null;
            }

            var piece = prefab.GetComponent<Piece>();
            return piece == null ? null : piece.m_icon;
        }
    }

    /// <summary>
    /// <c>Vagon.UpdateMass</c> runs every 5 s on the cart's owner (the player pulling it) and sets the mass to
    /// <c>m_baseMass + cargo weight * m_itemWeightMassFactor</c>. While the local player is the one pulling, a Postfix
    /// sets it again with the cargo part scaled by <c>1 - MaxCargoReduction * Hauling skill factor</c>, through the
    /// game's own <c>SetMass</c>. A parked cart, or one someone else pulls, keeps the vanilla mass.
    /// </summary>
    [HarmonyPatch(typeof(Vagon), "UpdateMass")]
    internal static class HaulingMassPatch
    {
        private static readonly Action<Vagon, float> SetMass =
            AccessTools.MethodDelegate<Action<Vagon, float>>(AccessTools.Method(typeof(Vagon), "SetMass"));

        private static readonly AccessTools.FieldRef<Vagon, ZNetView> NView =
            AccessTools.FieldRefAccess<Vagon, ZNetView>("m_nview");

        private static void Postfix(Vagon __instance)
        {
            try
            {
                if (!Plugin.HaulingEnabled.Value)
                {
                    return;
                }

                var player = Player.m_localPlayer;
                var nview = NView(__instance);
                if (player == null || __instance.m_container == null || nview == null || !nview.IsOwner()
                    || !__instance.IsAttached(player))
                {
                    return;
                }

                var reduction = Plugin.MaxCargoReduction.Value * player.GetSkills().GetSkillFactor(Hauling.Type);
                var cargo = __instance.m_container.GetInventory().GetTotalWeight() * __instance.m_itemWeightMassFactor;
                SetMass(__instance, __instance.m_baseMass + cargo * (1f - reduction));
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"HaulingMass patch failed: {e}");
            }
        }
    }

    /// <summary>
    /// The mass is only recomputed every 5 s. Postfixes on the private <c>AttachTo</c> and <c>Detach</c> run
    /// <c>UpdateMass</c> right away, so the reduction starts at hitching and ends at unhitching.
    /// </summary>
    [HarmonyPatch]
    internal static class HaulingHitchPatch
    {
        private static readonly Action<Vagon> UpdateMass =
            AccessTools.MethodDelegate<Action<Vagon>>(AccessTools.Method(typeof(Vagon), "UpdateMass"));

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Vagon), "AttachTo")]
        private static void AfterAttach(Vagon __instance)
        {
            Refresh(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Vagon), "Detach")]
        private static void AfterDetach(Vagon __instance)
        {
            HaulingLevelPatch.Forget(__instance);
            Refresh(__instance);
        }

        private static void Refresh(Vagon cart)
        {
            try
            {
                if (Plugin.HaulingEnabled.Value)
                {
                    UpdateMass(cart);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"HaulingHitch patch failed: {e}");
            }
        }
    }

    /// <summary>
    /// Counts how far the cart moves while the local player pulls it and is walking (giving movement input), so
    /// hitching, standing still, or being dragged by a cart rolling downhill earns nothing. Once per second, if that
    /// averaged at least <see cref="MinSpeed"/>, raise Hauling by <c>distance * GainPerMeter</c> if the cart holds
    /// anything, however much, and by <see cref="EmptyCartFactor"/> of that if it is empty. With cargo at a cart's
    /// usual 3 to 4 m/s that is about 0.25 per second, a quarter of vanilla Swim and Run: level 10 after about 5
    /// minutes of pulling, 50 after about 4 hours, 100 after about 22 hours (the game's own level curve does the rest).
    /// </summary>
    [HarmonyPatch(typeof(Vagon), "Update")]
    internal static class HaulingLevelPatch
    {
        private const float Interval = 1f;
        private const float MinSpeed = 1f;
        // A step longer than this in one frame is a teleport or a physics jump, not hauling.
        private const float MaxStep = 2f;
        private const float GainPerMeter = 0.075f;
        private const float EmptyCartFactor = 0.5f;

        private static Vagon _cart;
        private static Vector3 _lastPosition;
        private static float _distance;
        private static float _timer;

        internal static void Forget(Vagon cart)
        {
            if (_cart == cart)
            {
                _cart = null;
            }
        }

        private static void Postfix(Vagon __instance)
        {
            try
            {
                if (!Plugin.HaulingEnabled.Value)
                {
                    return;
                }

                var player = Player.m_localPlayer;
                if (player == null || __instance.m_container == null || !__instance.IsAttached(player))
                {
                    return;
                }

                var position = __instance.transform.position;
                if (_cart != __instance)
                {
                    _cart = __instance;
                    _lastPosition = position;
                    _distance = 0f;
                    _timer = 0f;
                    return;
                }

                var step = Vector3.Distance(position, _lastPosition);
                _lastPosition = position;
                if (step <= MaxStep && player.GetMoveDir().sqrMagnitude > 0.01f)
                {
                    _distance += step;
                }

                _timer += Time.deltaTime;
                if (_timer < Interval)
                {
                    return;
                }

                var distance = _distance;
                var speed = distance / _timer;
                _distance = 0f;
                _timer = 0f;

                if (speed < MinSpeed)
                {
                    return;
                }

                var load = __instance.m_container.GetInventory().NrOfItems() == 0 ? EmptyCartFactor : 1f;
                player.RaiseSkill(Hauling.Type, distance * GainPerMeter * load);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"HaulingLevel patch failed: {e}");
            }
        }
    }
}
