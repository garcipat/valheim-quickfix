using System;
using HarmonyLib;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// Enemies treat torches as buildings worth attacking, so they stop to smash a standing torch instead of
    /// going after you. Drop a torch from the static targets a monster can pick, and it carries on as if the
    /// torch were not there.
    /// </summary>
    /// <remarks>
    /// Monsters pick a structure through <c>BaseAI.FindClosestStaticPriorityTarget</c> (buildings they prefer) and
    /// <c>BaseAI.FindRandomStaticTarget</c> (anything nearby). Only the peer that owns a monster runs its AI, so this
    /// affects monsters simulated on this machine, which is always the case in singleplayer. Torches are matched by
    /// prefab name (<c>piece_groundtorch*</c>, <c>piece_walltorch</c>); braziers and other fires are left alone.
    /// </remarks>
    internal static class IgnoreTorchesPatch
    {
        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.FindClosestStaticPriorityTarget))]
        private static class Priority
        {
            private static void Postfix(ref StaticTarget __result) => Filter(ref __result);
        }

        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.FindRandomStaticTarget))]
        private static class Random
        {
            private static void Postfix(ref StaticTarget __result) => Filter(ref __result);
        }

        private static void Filter(ref StaticTarget result)
        {
            try
            {
                if (!Plugin.IgnoreTorchesEnabled.Value || Player.m_localPlayer == null || result == null)
                {
                    return;
                }

                if (IsTorch(result))
                {
                    result = null;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"IgnoreTorches failed: {e}");
            }
        }

        private static bool IsTorch(StaticTarget target)
        {
            var piece = target.GetComponentInParent<Piece>();
            if (piece == null)
            {
                return false;
            }

            var name = piece.gameObject.name;
            return name.IndexOf("torch", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
