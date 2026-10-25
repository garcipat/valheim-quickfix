using System;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using ValheimMoreSkills.Core;

namespace ValheimMoreSkills.Patches
{
    /// <summary>
    /// Endurance: levels while you walk over the weight limit and lowers the stamina that costs.
    /// </summary>
    internal static class Endurance
    {
        internal const string Identifier = "dev.pgarcia.valheim.moreskills.endurance";

        internal static readonly Skills.SkillType Type = CustomSkill.TypeOf(Identifier);

        private const string MegingjordPrefab = "BeltStrength";

        internal static CustomSkill Create(ConfigEntry<bool> enabled)
        {
            return new CustomSkill(
                Identifier,
                "Endurance",
                "Walking under too heavy a load. Higher levels make it drain less stamina.",
                MegingjordIcon,
                enabled);
        }

        // Megingjord's item icon; null before ObjectDB has loaded it.
        private static Sprite MegingjordIcon()
        {
            if (ObjectDB.instance == null)
            {
                return null;
            }

            var prefab = ObjectDB.instance.GetItemPrefab(MegingjordPrefab);
            var drop = prefab == null ? null : prefab.GetComponent<ItemDrop>();
            return drop == null ? null : drop.m_itemData.GetIcon();
        }
    }

    /// <summary>
    /// <c>Player.UpdateStats(dt)</c> runs for the local player only. While over the weight limit and moving, it drains
    /// <c>m_encumberedStaminaDrain</c> (10/s) and blocks regen. A Prefix scales that field by
    /// <c>1 - MaxDrainReduction * Endurance skill factor</c> and a Finalizer puts the original back, so the game's own
    /// logic runs unchanged. The Postfix raises the skill by <see cref="GainPerInterval"/> per second of walking
    /// overweight: twice Hauling's pace, because overweight walking comes in short stretches. Level 10 after about 7
    /// minutes of it, 50 after about 5 hours (the game's own level curve does the rest).
    /// </summary>
    [HarmonyPatch(typeof(Player), "UpdateStats", typeof(float))]
    internal static class EndurancePatch
    {
        private const float Interval = 1f;
        private const float GainPerInterval = 0.2f;
        // The game's own threshold for "moving" in the drain.
        private const float MinMoveInput = 0.1f;
        private const float MinSpeed = 0.5f;

        private static float _timer;

        private static void Prefix(Player __instance, out float __state)
        {
            __state = -1f;
            try
            {
                if (!Plugin.EnduranceEnabled.Value || __instance != Player.m_localPlayer)
                {
                    return;
                }

                __state = __instance.m_encumberedStaminaDrain;
                var reduction = Plugin.MaxDrainReduction.Value * __instance.GetSkills().GetSkillFactor(Endurance.Type);
                __instance.m_encumberedStaminaDrain = __state * (1f - reduction);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Endurance patch failed: {e}");
            }
        }

        private static void Postfix(Player __instance, float dt)
        {
            try
            {
                if (!Plugin.EnduranceEnabled.Value || __instance != Player.m_localPlayer)
                {
                    return;
                }

                // Walking under the load: the drain's own condition, plus actually moving (at zero stamina the input
                // is still there, but the player cannot move).
                if (!__instance.IsEncumbered() || __instance.IsAttached()
                    || __instance.GetMoveDir().magnitude <= MinMoveInput
                    || __instance.GetVelocity().magnitude < MinSpeed)
                {
                    return;
                }

                _timer += dt;
                if (_timer < Interval)
                {
                    return;
                }

                _timer -= Interval;
                __instance.RaiseSkill(Endurance.Type, GainPerInterval);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Endurance patch failed: {e}");
            }
        }

        private static void Finalizer(Player __instance, float __state)
        {
            if (__state >= 0f)
            {
                __instance.m_encumberedStaminaDrain = __state;
            }
        }
    }
}
