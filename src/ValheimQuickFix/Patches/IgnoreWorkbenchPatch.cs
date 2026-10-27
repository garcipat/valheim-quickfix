using System;
using System.Reflection;
using HarmonyLib;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// <c>Player.HaveRequirements(Piece, RequirementMode)</c> (placing) and <c>Player.CheckCanRemovePiece</c>
    /// (deconstructing) refuse a piece whose <c>m_craftingStation</c> is not in range. A Prefix clears that field on the
    /// piece prefab for the duration of the call and a Finalizer puts it back, so the rest of the game's checks (known
    /// station, resources, DLC, world modifiers) still run. Only applies to pieces that need the workbench, and only for
    /// the local player in a singleplayer game.
    /// </summary>
    internal static class IgnoreWorkbenchPatch
    {
        // CraftingStation.m_name of the workbench; the stonecutter, forge, black forge, ... keep their requirement.
        private const string WorkbenchName = "$piece_workbench";

        // ZNet.IsSinglePlayer is newer than the compile-time game libs.
        private static readonly MethodInfo IsSinglePlayerGetter =
            AccessTools.PropertyGetter(typeof(ZNet), "IsSinglePlayer");

        private static bool Active(Player player)
        {
            if (!Plugin.IgnoreWorkbenchEnabled.Value || player == null || player != Player.m_localPlayer)
            {
                return false;
            }

            return IsSinglePlayerGetter != null && (bool)IsSinglePlayerGetter.Invoke(null, null);
        }

        private static void Clear(Player player, Piece piece, out CraftingStation __state)
        {
            __state = null;
            try
            {
                if (piece == null || piece.m_craftingStation == null ||
                    piece.m_craftingStation.m_name != WorkbenchName || !Active(player))
                {
                    return;
                }

                __state = piece.m_craftingStation;
                piece.m_craftingStation = null;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"IgnoreWorkbench patch failed: {e}");
            }
        }

        private static void Restore(Piece piece, CraftingStation __state)
        {
            if (__state != null && piece != null)
            {
                piece.m_craftingStation = __state;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Piece), typeof(Player.RequirementMode))]
        internal static class HaveRequirementsPatch
        {
            private static void Prefix(Player __instance, Piece piece, Player.RequirementMode mode,
                out CraftingStation __state)
            {
                __state = null;
                // IsKnown and CanAlmostBuild only ask whether the station has ever been seen; keep that check.
                if (mode == Player.RequirementMode.CanBuild)
                {
                    Clear(__instance, piece, out __state);
                }
            }

            private static Exception Finalizer(Exception __exception, Piece piece, CraftingStation __state)
            {
                Restore(piece, __state);
                return __exception;
            }
        }

        [HarmonyPatch(typeof(Player), "CheckCanRemovePiece")]
        internal static class CheckCanRemovePiecePatch
        {
            private static void Prefix(Player __instance, Piece piece, out CraftingStation __state)
            {
                Clear(__instance, piece, out __state);
            }

            private static Exception Finalizer(Exception __exception, Piece piece, CraftingStation __state)
            {
                Restore(piece, __state);
                return __exception;
            }
        }
    }
}
