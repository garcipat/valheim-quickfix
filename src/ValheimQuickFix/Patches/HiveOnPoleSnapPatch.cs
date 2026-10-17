using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// A pole only has snap points at its ends, so a beehive cannot be snapped to its middle. While a beehive is
    /// being placed with a manual snap point chosen (Q / E), every nearby pole offers an extra snap point halfway
    /// along it. The points are hidden child transforms that are not tagged "snappoint", so vanilla scans and every
    /// other piece never see them.
    /// </summary>
    /// <remarks>
    /// If the beehive has no snap point of its own, Q would have nothing to select, so the ghost gets one at its origin.
    /// </remarks>
    internal static class HiveOnPoleSnap
    {
        internal const string PointName = "QuickFixSnapPoint";

        private static AccessTools.FieldRef<Player, GameObject> _ghost;
        private static AccessTools.FieldRef<Player, int> _manualSnapPoint;
        private static bool _resolved;
        private static bool _failed;

        private static readonly List<Transform> Temp = new List<Transform>();

        /// <summary>The beehive ghost the local player is placing, or null.</summary>
        internal static Piece GetHiveGhost(out int manualSnapPoint)
        {
            manualSnapPoint = -1;
            if (_failed || !Plugin.HiveOnPoleSnapEnabled.Value || Player.m_localPlayer == null)
            {
                return null;
            }
            if (!Resolve())
            {
                return null;
            }
            var ghost = _ghost(Player.m_localPlayer);
            if (ghost == null || ghost.GetComponent<Beehive>() == null)
            {
                return null;
            }
            manualSnapPoint = _manualSnapPoint(Player.m_localPlayer);
            return ghost.GetComponent<Piece>();
        }

        private static bool Resolve()
        {
            if (_resolved)
            {
                return true;
            }
            try
            {
                _ghost = AccessTools.FieldRefAccess<Player, GameObject>("m_placementGhost");
                _manualSnapPoint = AccessTools.FieldRefAccess<Player, int>("m_manualSnapPoint");
                _resolved = true;
            }
            catch (Exception e)
            {
                _failed = true;
                Plugin.Log.LogWarning("HiveSnapsToPoleMiddle disabled: Player.m_placementGhost / m_manualSnapPoint not found (" + e.Message + ")");
            }
            return _resolved;
        }

        internal static Transform GetOrCreatePoint(Transform parent, Vector3 worldPosition)
        {
            var point = parent.Find(PointName);
            if (point == null)
            {
                point = new GameObject(PointName).transform;
                point.SetParent(parent, false);
            }
            point.position = worldPosition;
            return point;
        }

        internal static bool IsPole(Piece piece)
        {
            return piece.name.IndexOf("pole", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static void AddPoleMiddles(List<Transform> points, List<Piece> pieces)
        {
            var ghost = GetHiveGhost(out int manual);
            if (ghost == null || manual < 0)
            {
                return;
            }
            foreach (var piece in pieces)
            {
                if (piece == null || piece == ghost || !IsPole(piece))
                {
                    continue;
                }
                Temp.Clear();
                piece.GetSnapPoints(Temp);
                if (Temp.Count < 2)
                {
                    continue;
                }
                var sum = Vector3.zero;
                foreach (var p in Temp)
                {
                    sum += p.position;
                }
                points.Add(GetOrCreatePoint(piece.transform, sum / Temp.Count));
            }
        }
    }

    [HarmonyPatch(typeof(Piece), nameof(Piece.GetSnapPoints), typeof(Vector3), typeof(float), typeof(List<Transform>), typeof(List<Piece>))]
    internal static class HiveOnPoleSnapPatch
    {
        private static void Postfix(List<Transform> points, List<Piece> pieces)
        {
            try
            {
                HiveOnPoleSnap.AddPoleMiddles(points, pieces);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("HiveSnapsToPoleMiddle: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(Piece), nameof(Piece.GetSnapPoints), typeof(List<Transform>))]
    internal static class HiveGhostSnapPointPatch
    {
        private static void Prefix(List<Transform> points, out int __state)
        {
            __state = points.Count;
        }

        private static void Postfix(Piece __instance, List<Transform> points, int __state)
        {
            try
            {
                if (points.Count != __state || __instance.GetComponent<Beehive>() == null)
                {
                    return;
                }
                if (HiveOnPoleSnap.GetHiveGhost(out _) == __instance)
                {
                    points.Add(HiveOnPoleSnap.GetOrCreatePoint(__instance.transform, __instance.transform.position));
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("HiveSnapsToPoleMiddle: " + e.Message);
            }
        }
    }
}
