using System;
using HarmonyLib;
using UnityEngine;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// Holding the block button while a bow or crossbow is the current weapon zooms the camera in, drawn or not, so
    /// the zoom lasts across shots. Vanilla blocks with that button and cancels a draw while blocking
    /// (<c>Player.UpdateAttackBowDraw</c> resets the draw while <c>m_blocking</c> is set), so a Prefix on
    /// <c>Player.SetControls</c> takes the block input away while the local player holds a bow or crossbow and
    /// remembers it as "zooming". A Postfix on <c>GameCamera.LateUpdate</c> then narrows the camera's field of view on
    /// top of the one the game set, easing in and out, so the game's own FOV handling keeps running.
    /// </summary>
    internal static class BowZoomPatch
    {
        private static readonly AccessTools.FieldRef<GameCamera, Camera> CameraRef =
            AccessTools.FieldRefAccess<GameCamera, Camera>("m_camera");

        private static readonly AccessTools.FieldRef<GameCamera, bool> FreeFlyRef =
            AccessTools.FieldRefAccess<GameCamera, bool>("m_freeFly");

        // Crossbows is newer than the compile-time game libs; skill ids are saved with the character, so 14 is stable.
        private const Skills.SkillType Crossbows = (Skills.SkillType)14;

        // Time to ease fully in or out of the zoom.
        private const float EaseSeconds = 0.15f;

        private static bool _zoomHeld;
        private static float _zoom;

        private static bool IsZoomable(Player player)
        {
            if (player == null || player != Player.m_localPlayer)
            {
                return false;
            }

            var skill = player.GetCurrentWeapon()?.m_shared.m_skillType;
            return skill == Skills.SkillType.Bows || skill == Crossbows;
        }

        [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
        private static class SetControlsPatch
        {
            private static void Prefix(Player __instance, ref bool block, ref bool blockHold)
            {
                try
                {
                    if (__instance != Player.m_localPlayer)
                    {
                        return;
                    }

                    _zoomHeld = false;
                    if (!Plugin.BowZoomEnabled.Value || !IsZoomable(__instance))
                    {
                        return;
                    }

                    _zoomHeld = blockHold;
                    block = false;
                    blockHold = false;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"BowZoom patch failed: {e}");
                }
            }
        }

        [HarmonyPatch(typeof(GameCamera), "LateUpdate")]
        private static class CameraPatch
        {
            private static void Postfix(GameCamera __instance)
            {
                try
                {
                    if (!Plugin.BowZoomEnabled.Value || FreeFlyRef(__instance))
                    {
                        _zoom = 0f;
                        return;
                    }

                    var target = _zoomHeld && IsZoomable(Player.m_localPlayer) ? 1f : 0f;
                    _zoom = Mathf.MoveTowards(_zoom, target, Time.unscaledDeltaTime / EaseSeconds);
                    if (_zoom <= 0f)
                    {
                        return;
                    }

                    // Magnify by narrowing the vertical FOV: tan(fov/2) shrinks by the magnification.
                    var camera = CameraRef(__instance);
                    var magnification = Mathf.Lerp(1f, Plugin.BowZoomMagnification.Value, Mathf.SmoothStep(0f, 1f, _zoom));
                    var halfFov = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
                    var fov = 2f * Mathf.Atan(Mathf.Tan(halfFov) / magnification) * Mathf.Rad2Deg;
                    camera.fieldOfView = fov;
                    __instance.m_skyCamera.fieldOfView = fov;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"BowZoom patch failed: {e}");
                }
            }
        }
    }
}
