using System;
using System.Reflection;
using HarmonyLib;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// Reads <see cref="HitData"/> members whose type differs between the compile-time libs and the live game.
    /// </summary>
    /// <remarks>
    /// <c>HitData.m_toolTier</c> is an <c>int</c> in the libs but a <c>short</c> in the game, so a direct access compiles
    /// and then throws <c>MissingFieldException</c> when the calling method is JIT-compiled, which is outside any
    /// <c>try</c> in that method and aborts the patched game method. It is read by name and converted instead.
    /// </remarks>
    internal static class HitDataAccess
    {
        private static readonly FieldInfo ToolTierField = AccessTools.Field(typeof(HitData), "m_toolTier");

        internal static int ToolTier(HitData hit)
        {
            if (ToolTierField == null)
            {
                return 0;
            }

            return Convert.ToInt32(ToolTierField.GetValue(hit));
        }
    }
}
