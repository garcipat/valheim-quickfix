using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// Vanilla ignores every object tagged "leaky" when it looks for a roof above a point, and floors carry that tag,
    /// so standing under a floor (a second storey, a deck) never counts as being under a roof. Casts the same ray
    /// again when vanilla found nothing, and accepts a leaky hit that is part of a floor piece.
    /// </summary>
    /// <remarks>
    /// Cover.GetCoverForPoint, which feeds the shelter check, already counts leaky objects as cover; only IsUnderRoof
    /// rejects them. Cover is static and also serves rain, wetness and fireplaces, so they follow this too.
    /// </remarks>
    [HarmonyPatch(typeof(Cover), nameof(Cover.IsUnderRoof))]
    internal static class FloorRoofPatch
    {
        private const string LeakyTag = "leaky";
        private const float CastRadius = 0.1f;
        private const float CastDistance = 100f;

        // Same layers as Cover.Setup; looked up by name so a renumbered layer can't break it.
        private static readonly string[] CoverLayers = { "Default", "static_solid", "Default_small", "piece", "terrain", "vehicle" };

        private static int _mask;
        private static readonly RaycastHit[] Hits = new RaycastHit[128];
        private static readonly HashSet<string> LoggedLeakyPieces = new HashSet<string>();

        private static void Postfix(Vector3 startPos, ref bool __result)
        {
            try
            {
                if (__result || !Plugin.FloorsNotLeakyEnabled.Value)
                {
                    return;
                }

                if (_mask == 0)
                {
                    _mask = LayerMask.GetMask(CoverLayers);
                }

                var count = Physics.SphereCastNonAlloc(startPos, CastRadius, Vector3.up, Hits, CastDistance, _mask);
                for (var i = 0; i < count; i++)
                {
                    var collider = Hits[i].collider;
                    if (collider == null || !collider.gameObject.CompareTag(LeakyTag))
                    {
                        continue;
                    }

                    var piece = collider.GetComponentInParent<Piece>();
                    if (piece == null)
                    {
                        continue;
                    }

                    LogOnce(piece);
                    if (IsFloor(piece))
                    {
                        __result = true;
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"FloorsNotLeaky patch failed: {e}");
            }
        }

        private static bool IsFloor(Piece piece)
        {
            return piece.gameObject.name.IndexOf("floor", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Lists the leaky pieces seen above the player, so the names can be checked against what should count.
        private static void LogOnce(Piece piece)
        {
            var name = piece.gameObject.name;
            if (LoggedLeakyPieces.Add(name))
            {
                Plugin.Log.LogInfo($"FloorsNotLeaky: leaky piece above a point: '{name}' (counts as roof: {IsFloor(piece)})");
            }
        }
    }
}
