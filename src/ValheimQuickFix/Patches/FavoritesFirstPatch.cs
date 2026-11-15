using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// The build menu fills its piece grid from the current tab's <c>IPieceList.GetAvailablePiecesWithTag</c>, in the
    /// order that call returns. A Postfix on the "By usage" and "By material" lists moves the pieces that
    /// <c>BuildUi.IsFavoritePiece</c> reports as favorites to the front, keeping the game's order within favorites and
    /// within the rest. The "Recent" and "Favorites" tabs are left alone. The build menu is newer than the compile-time
    /// game libs, so its types and <c>Hud.m_buildUi</c> are reached through reflection.
    /// </summary>
    [HarmonyPatch]
    internal static class FavoritesFirstPatch
    {
        private static readonly string[] SortedLists = { "ByUsagePieceList", "ByMaterialPieceList" };

        private static readonly FieldInfo BuildUiField = AccessTools.Field(typeof(Hud), "m_buildUi");
        private static readonly MethodInfo IsFavoritePiece =
            AccessTools.Method(AccessTools.TypeByName("BuildUi"), "IsFavoritePiece", new[] { typeof(Piece) });

        private static readonly List<Piece> Favorites = new List<Piece>();
        private static readonly List<Piece> Others = new List<Piece>();

        private static bool Prepare()
        {
            if (BuildUiField != null && IsFavoritePiece != null)
            {
                return true;
            }

            Plugin.Log.LogWarning("FavoritesFirst disabled: the build menu's favorites are missing in this game version");
            return false;
        }

        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var name in SortedLists)
            {
                var method = AccessTools.Method(AccessTools.TypeByName(name), "GetAvailablePiecesWithTag");
                if (method != null)
                {
                    yield return method;
                }
            }
        }

        private static void Prefix(IList<Piece> resultOut, out int __state)
        {
            __state = resultOut?.Count ?? 0;
        }

        private static void Postfix(IList<Piece> resultOut, int __state)
        {
            try
            {
                if (!Plugin.FavoritesFirstEnabled.Value || resultOut == null || Hud.instance == null)
                {
                    return;
                }

                var buildUi = BuildUiField.GetValue(Hud.instance);
                if (buildUi == null)
                {
                    return;
                }

                // Stable partition of what this call added, written back only once every piece is classified.
                Favorites.Clear();
                Others.Clear();
                var args = new object[1];
                for (var i = __state; i < resultOut.Count; i++)
                {
                    var piece = resultOut[i];
                    args[0] = piece;
                    var favorite = piece != null && (bool)IsFavoritePiece.Invoke(buildUi, args);
                    (favorite ? Favorites : Others).Add(piece);
                }

                if (Favorites.Count == 0)
                {
                    return;
                }

                var next = __state;
                foreach (var piece in Favorites)
                {
                    resultOut[next++] = piece;
                }

                foreach (var piece in Others)
                {
                    resultOut[next++] = piece;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"FavoritesFirst patch failed: {e}");
            }
            finally
            {
                Favorites.Clear();
                Others.Clear();
            }
        }
    }
}
