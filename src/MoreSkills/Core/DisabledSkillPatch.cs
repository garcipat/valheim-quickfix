using System;
using System.Collections.Generic;
using HarmonyLib;

namespace ValheimMoreSkills.Core
{
    /// <summary>
    /// A custom skill whose toggle is off stays in the character's skill data (so it is saved and comes back when
    /// turned on) but is left out of <c>GetSkillList</c>, which is what the skills dialog shows. The same Postfix fills
    /// in the icon of a skill registered before the game had loaded it.
    /// </summary>
    [HarmonyPatch(typeof(Skills), nameof(Skills.GetSkillList))]
    internal static class HideDisabledSkillsPatch
    {
        private static void Postfix(List<Skills.Skill> __result)
        {
            try
            {
                __result.RemoveAll(s =>
                {
                    var custom = s.m_info == null ? null : SkillRegistry.Find(s.m_info.m_skill);
                    if (custom == null)
                    {
                        return false;
                    }

                    if (s.m_info.m_icon == null)
                    {
                        s.m_info.m_icon = custom.Icon();
                    }

                    return !custom.Enabled.Value;
                });
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"HideDisabledSkills patch failed: {e}");
            }
        }
    }

    /// <summary>
    /// <c>LowerAllSkills</c> (the death penalty) lowers every skill in the data. A Prefix saves the level and progress
    /// of each disabled custom skill and a Postfix puts them back, so a turned-off skill is frozen.
    /// </summary>
    [HarmonyPatch(typeof(Skills), nameof(Skills.LowerAllSkills))]
    internal static class FreezeDisabledSkillsPatch
    {
        private static readonly AccessTools.FieldRef<Skills, Dictionary<Skills.SkillType, Skills.Skill>> SkillData =
            AccessTools.FieldRefAccess<Skills, Dictionary<Skills.SkillType, Skills.Skill>>("m_skillData");

        private static void Prefix(Skills __instance, out List<(Skills.Skill skill, float level, float accumulator)> __state)
        {
            __state = new List<(Skills.Skill, float, float)>();
            try
            {
                foreach (var pair in SkillData(__instance))
                {
                    var custom = SkillRegistry.Find(pair.Key);
                    if (custom != null && !custom.Enabled.Value)
                    {
                        __state.Add((pair.Value, pair.Value.m_level, pair.Value.m_accumulator));
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"FreezeDisabledSkills patch failed: {e}");
            }
        }

        private static void Postfix(List<(Skills.Skill skill, float level, float accumulator)> __state)
        {
            try
            {
                foreach (var (skill, level, accumulator) in __state)
                {
                    skill.m_level = level;
                    skill.m_accumulator = accumulator;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"FreezeDisabledSkills patch failed: {e}");
            }
        }
    }
}
