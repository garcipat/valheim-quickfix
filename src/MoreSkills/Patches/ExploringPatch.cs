using System;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using ValheimMoreSkills.Core;

namespace ValheimMoreSkills.Patches
{
    /// <summary>
    /// Exploring: levels while you uncover new map and widens the circle you uncover around you.
    /// </summary>
    internal static class Exploring
    {
        internal const string Identifier = "dev.pgarcia.valheim.moreskills.exploring";

        internal static readonly Skills.SkillType Type = CustomSkill.TypeOf(Identifier);

        internal static CustomSkill Create(ConfigEntry<bool> enabled)
        {
            return new CustomSkill(
                Identifier,
                "Exploring",
                "Uncovering new map. Higher levels uncover a wider area around you.",
                CartographyTableIcon,
                enabled);
        }

        // The game's own cartography table piece icon; null before ZNetScene exists (main menu).
        private static Sprite CartographyTableIcon()
        {
            if (ZNetScene.instance == null)
            {
                return null;
            }

            var prefab = ZNetScene.instance.GetPrefab("piece_cartographytable");
            var piece = prefab == null ? null : prefab.GetComponent<Piece>();
            return piece == null ? null : piece.m_icon;
        }
    }

    /// <summary>
    /// <c>Minimap.UpdateExplore</c> calls the private <c>Explore(Vector3, float)</c> every 2 s with
    /// <c>m_exploreRadius</c> around the local player; nothing else calls that overload. A Prefix scales its radius by
    /// <c>1 + MaxRadiusBonus * Exploring skill factor</c>, so level 0 is vanilla.
    /// </summary>
    /// <remarks>
    /// The same call levels the skill. <c>Explore(int, int)</c> returns true only for a pixel not yet in the player's
    /// own explored layer (cartography table data lives in a separate layer, so walking through a shared area still
    /// counts). The new pixels become meters of new ground, <c>area / (2 * radius)</c>, so a wider circle does not speed
    /// up its own levelling and the pace does not depend on the map's pixel size. The first call after a gap (load,
    /// respawn) and a jump of more than <see cref="MaxStep"/> (portal) earn nothing.
    /// </remarks>
    [HarmonyPatch(typeof(Minimap), "Explore", new[] { typeof(Vector3), typeof(float) })]
    internal static class ExploringPatch
    {
        // Vanilla explores every 2 s; a longer gap means the player was not simply moving between two calls.
        private const float MaxGap = 5f;
        // Farther than this between two calls is a teleport, not walking or sailing.
        private const float MaxStep = 60f;
        // About 0.14 per second running through fresh land.
        private const float GainPerMeter = 0.02f;

        private static bool _counting;
        private static int _newPixels;
        private static bool _hasLast;
        private static Vector3 _lastPosition;
        private static float _lastTime;

        internal static void Count()
        {
            if (_counting)
            {
                _newPixels++;
            }
        }

        private static void Prefix(ref float radius)
        {
            try
            {
                var player = Player.m_localPlayer;
                if (!Plugin.ExploringEnabled.Value || player == null)
                {
                    return;
                }

                radius *= 1f + Plugin.MaxRadiusBonus.Value * player.GetSkills().GetSkillFactor(Exploring.Type);
                _counting = true;
                _newPixels = 0;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Exploring patch failed: {e}");
            }
        }

        private static void Postfix(Minimap __instance, Vector3 p, float radius)
        {
            try
            {
                if (!_counting)
                {
                    return;
                }

                var player = Player.m_localPlayer;
                if (player == null)
                {
                    return;
                }

                var moved = _hasLast && Time.time - _lastTime <= MaxGap && Vector3.Distance(p, _lastPosition) <= MaxStep;
                _hasLast = true;
                _lastPosition = p;
                _lastTime = Time.time;
                if (!moved || _newPixels == 0 || radius <= 0f)
                {
                    return;
                }

                var area = _newPixels * __instance.m_pixelSize * __instance.m_pixelSize;
                player.RaiseSkill(Exploring.Type, area / (2f * radius) * GainPerMeter);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Exploring patch failed: {e}");
            }
        }

        private static void Finalizer()
        {
            _counting = false;
        }
    }

    /// <summary>Counts the pixels the scaled call reveals for the first time.</summary>
    [HarmonyPatch(typeof(Minimap), "Explore", new[] { typeof(int), typeof(int) })]
    internal static class ExploringPixelPatch
    {
        private static void Postfix(bool __result)
        {
            if (__result)
            {
                ExploringPatch.Count();
            }
        }
    }
}
