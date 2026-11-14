using System;
using HarmonyLib;
using UnityEngine;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// The rungs of the built step ladder are too far apart (up to 0.66 m) for physics to push the player's capsule
    /// over, so the game puts auto-jump triggers (<c>AutoJumpLedge</c>) on it that launch the player onto each rung
    /// with a real jump: jump sound, falling animation and a camera bob. A Postfix on <c>Character.UpdateWalking</c>
    /// (which runs every physics step) lifts the walking local player onto the highest ladder rung within reach
    /// instead, over a few physics steps, and keeps them counted as on the ground; <see cref="SmoothLaddersAutoJumpPatch"/>
    /// skips the auto-jump whenever such a rung is found. Ladders are told apart by their piece's prefab or name
    /// containing "ladder"; stairs and everything else keep vanilla behavior.
    /// </summary>
    [HarmonyPatch(typeof(Character), "UpdateWalking")]
    internal static class SmoothLaddersPatch
    {
        // Lower edges are left to physics; they don't cause a visible hop.
        private const float MinStep = 0.05f;
        // A bit over the step ladder's highest rung gap, which the auto-jump already got you onto.
        private const float MaxStep = 0.75f;
        // Where ahead of the player's centre rung tops are looked for, in capsule radii.
        private static readonly float[] Reach = { 0.5f, 1f, 1.4f };
        // Lift speed relative to the walking speed, with a floor for when a rung holds the player back.
        private const float LiftPerSpeed = 1.5f;
        private const float MinLiftSpeed = 2.5f;

        private static readonly AccessTools.FieldRef<Character, Vector3> MoveDirRef =
            AccessTools.FieldRefAccess<Character, Vector3>("m_moveDir");
        private static readonly AccessTools.FieldRef<Character, Rigidbody> BodyRef =
            AccessTools.FieldRefAccess<Character, Rigidbody>("m_body");
        private static readonly AccessTools.FieldRef<Character, CapsuleCollider> ColliderRef =
            AccessTools.FieldRefAccess<Character, CapsuleCollider>("m_collider");
        private static readonly AccessTools.FieldRef<Character, float> LastGroundTouchRef =
            AccessTools.FieldRefAccess<Character, float>("m_lastGroundTouch");
        private static readonly AccessTools.FieldRef<Character, float> JumpTimerRef =
            AccessTools.FieldRefAccess<Character, float>("m_jumpTimer");

        private static int _mask;

        private static void Postfix(Character __instance, float dt)
        {
            try
            {
                if (!Plugin.SmoothLaddersEnabled.Value)
                {
                    return;
                }

                var player = Player.m_localPlayer;
                if (player == null || __instance != player)
                {
                    return;
                }

                var rise = RungAhead(player);
                if (rise < MinStep)
                {
                    return;
                }

                var body = BodyRef(player);
                var velocity = body.velocity;
                var walkSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
                var liftSpeed = Mathf.Max(MinLiftSpeed, walkSpeed * LiftPerSpeed);
                body.position += Vector3.up * Mathf.Min(rise, liftSpeed * dt);
                if (velocity.y < 0f)
                {
                    velocity.y = 0f;
                    body.velocity = velocity;
                }

                LastGroundTouchRef(player) = 0f;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"SmoothLadders patch failed: {e}");
            }
        }

        /// <summary>
        /// How far above the walking player's feet the highest ladder rung within reach is, or 0 when there is none
        /// or the player isn't simply walking.
        /// </summary>
        internal static float RungAhead(Player player)
        {
            // Leave jumps, dodges, sitting, flying and staggering alone.
            if (!player.IsOnGround() || JumpTimerRef(player) < 0.3f || player.IsAttached() || player.InDodge()
                || player.IsDebugFlying() || player.IsStaggering() || !player.CanMove())
            {
                return 0f;
            }

            var move = MoveDirRef(player);
            move.y = 0f;
            var body = BodyRef(player);
            var capsule = ColliderRef(player);
            if (move.sqrMagnitude < 0.01f || body == null || capsule == null)
            {
                return 0f;
            }
            move.Normalize();

            if (_mask == 0)
            {
                _mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "vehicle");
            }

            // A ray starting inside a rung or rail passes through it, so only tops within MaxStep are found.
            var feet = body.position;
            var highest = 0f;
            foreach (var reach in Reach)
            {
                var probe = feet + move * (capsule.radius * reach) + Vector3.up * (MaxStep + MinStep);
                if (Physics.Raycast(probe, Vector3.down, out var top, MaxStep + MinStep, _mask, QueryTriggerInteraction.Ignore)
                    && top.normal.y >= 0.7f
                    && IsLadder(top.collider))
                {
                    highest = Mathf.Max(highest, top.point.y - feet.y);
                }
            }
            return highest <= MaxStep ? highest : 0f;
        }

        internal static bool IsLadder(Component component)
        {
            var piece = component.GetComponentInParent<Piece>();
            return piece != null
                && (piece.name.IndexOf("ladder", StringComparison.OrdinalIgnoreCase) >= 0
                    || piece.m_name.IndexOf("ladder", StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }

    /// <summary>
    /// Skips a ladder's auto-jump trigger for the local player while <see cref="SmoothLaddersPatch"/> lifts them onto a
    /// rung instead. When no rung is found ahead, the game's jump still fires, so the ladder can always be climbed.
    /// </summary>
    [HarmonyPatch(typeof(AutoJumpLedge), "OnTriggerStay")]
    internal static class SmoothLaddersAutoJumpPatch
    {
        private static bool Prefix(AutoJumpLedge __instance, Collider collider)
        {
            try
            {
                if (!Plugin.SmoothLaddersEnabled.Value)
                {
                    return true;
                }

                var player = Player.m_localPlayer;
                if (player == null || collider == null || collider.gameObject != player.gameObject
                    || !SmoothLaddersPatch.IsLadder(__instance))
                {
                    return true;
                }

                return SmoothLaddersPatch.RungAhead(player) <= 0f;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"SmoothLadders auto-jump patch failed: {e}");
                return true;
            }
        }
    }
}
