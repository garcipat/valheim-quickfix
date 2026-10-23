using System;
using HarmonyLib;
using UnityEngine;

namespace ValheimQuickFix.Patches
{
    /// <summary>
    /// <c>Player.UpdateStats</c> asks <c>SEMan.ModifyStaminaRegen</c> for the stamina regen multiplier of the active
    /// status effects and scales the regen with it. A Postfix there multiplies it once more while the local player sits
    /// (on the ground or in a chair, the same check the game uses for resting), so the game's own regen logic, delay
    /// after spending stamina and world setting all still apply.
    /// </summary>
    [HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyStaminaRegen))]
    internal static class SittingStaminaRegenPatch
    {
        private static readonly AccessTools.FieldRef<SEMan, Character> CharacterRef =
            AccessTools.FieldRefAccess<SEMan, Character>("m_character");

        private static void Postfix(SEMan __instance, ref float staminaMultiplier)
        {
            try
            {
                if (!Plugin.SittingStaminaRegenEnabled.Value)
                {
                    return;
                }

                var player = Player.m_localPlayer;
                if (player == null || CharacterRef(__instance) != player || !player.IsSitting())
                {
                    return;
                }

                staminaMultiplier *= Multiplier();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"SittingStaminaRegen patch failed: {e}");
            }
        }

        // The setting moves in 0.1 steps; a hand-edited config may hold anything in between.
        internal static float Multiplier()
        {
            return Mathf.Round(Plugin.SittingStaminaMultiplier.Value * 10f) / 10f;
        }
    }
}
