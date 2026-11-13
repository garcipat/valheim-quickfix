using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ValheimSignIcons.Patches
{
    /// <summary>
    /// Chests don't support other pieces (<c>WearNTear.m_supports</c> is false), so a sign aimed at a chest shows a red
    /// ghost, and a sign held only by a chest would have no support. While the game checks a sign, nearby chests count
    /// as supports; everything else about placement and structure stays vanilla.
    /// </summary>
    /// <remarks>
    /// Support is computed by whoever owns the sign's area. Without this mod there, a sign held only by a chest
    /// loses its support and breaks.
    /// </remarks>
    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    internal static class SignOnChestPlacementPatch
    {
        private static readonly AccessTools.FieldRef<Player, GameObject> PlacementGhost =
            AccessTools.FieldRefAccess<Player, GameObject>("m_placementGhost");

        private static readonly AccessTools.FieldRef<Player, int> PlaceRayMask =
            AccessTools.FieldRefAccess<Player, int>("m_placeRayMask");

        private static void Prefix(Player __instance, out List<WearNTear> __state)
        {
            __state = null;
            var ghost = PlacementGhost(__instance);
            if (ghost == null || ghost.GetComponent<Sign>() == null || GameCamera.instance == null)
            {
                return;
            }

            // The same ray the game uses to pick the surface the ghost sits on (Player.PieceRayTest).
            var camera = GameCamera.instance.transform;
            if (Physics.Raycast(camera.position, camera.forward, out var hit, 50f, PlaceRayMask(__instance)))
            {
                __state = ChestSupport.Enable(hit.collider);
            }
        }

        // A finalizer, so the chest is restored even if the game method throws.
        private static void Finalizer(List<WearNTear> __state) => ChestSupport.Restore(__state);
    }

    [HarmonyPatch(typeof(WearNTear), "UpdateSupport")]
    internal static class SignOnChestStructurePatch
    {
        // Covers the sign's size plus the small margin the game's own overlap test adds around it.
        private const float Radius = 1.5f;

        private static readonly Collider[] Nearby = new Collider[64];

        private static void Prefix(WearNTear __instance, out List<WearNTear> __state)
        {
            __state = null;
            if (__instance.GetComponent<Sign>() == null)
            {
                return;
            }

            var count = Physics.OverlapSphereNonAlloc(__instance.transform.position, Radius, Nearby);
            for (var i = 0; i < count; i++)
            {
                var enabled = ChestSupport.Enable(Nearby[i]);
                if (enabled != null)
                {
                    __state = __state ?? new List<WearNTear>();
                    foreach (var chest in enabled)
                    {
                        if (!__state.Contains(chest))
                        {
                            __state.Add(chest);
                        }
                    }
                }
            }
        }

        // A finalizer, so the chest is restored even if the game method throws.
        private static void Finalizer(List<WearNTear> __state) => ChestSupport.Restore(__state);
    }

    internal static class ChestSupport
    {
        /// <summary>
        /// If <paramref name="collider"/> belongs to a chest that doesn't support pieces, makes it a support and
        /// returns it for <see cref="Restore"/>; otherwise null.
        /// </summary>
        internal static List<WearNTear> Enable(Collider collider)
        {
            if (collider == null || collider.GetComponentInParent<Container>() == null)
            {
                return null;
            }

            var wear = collider.GetComponentInParent<WearNTear>();
            if (wear == null || wear.m_supports)
            {
                return null;
            }

            wear.m_supports = true;
            return new List<WearNTear> { wear };
        }

        internal static void Restore(List<WearNTear> chests)
        {
            if (chests == null)
            {
                return;
            }

            foreach (var chest in chests)
            {
                if (chest != null)
                {
                    chest.m_supports = false;
                }
            }
        }
    }
}
