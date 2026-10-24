using System;
using HarmonyLib;
using UnityEngine;

namespace ValheimMoreSkills.Core
{
    /// <summary>
    /// <c>CheatRaiseSkill</c> (the <c>raiseskill</c> console command) only searches the <c>SkillType</c> enum. A Prefix
    /// handles the names of custom skills the same way and lets vanilla run for everything else, including "all".
    /// </summary>
    [HarmonyPatch(typeof(Skills), nameof(Skills.CheatRaiseSkill))]
    internal static class RaiseSkillCommandPatch
    {
        private static readonly Func<Skills, Skills.SkillType, Skills.Skill> GetSkill =
            AccessTools.MethodDelegate<Func<Skills, Skills.SkillType, Skills.Skill>>(
                AccessTools.Method(typeof(Skills), "GetSkill"));

        private static bool Prefix(Skills __instance, string name, float value)
        {
            try
            {
                var custom = SkillRegistry.Find(name);
                if (custom == null)
                {
                    return true;
                }

                var skill = GetSkill(__instance, custom.Type);
                skill.m_level = Mathf.Clamp(skill.m_level + value, 0f, 100f);
                global::Console.instance.Print($"Skill {custom.Name} = {skill.m_level}");
                return false;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"RaiseSkillCommand patch failed: {e}");
                return true;
            }
        }
    }
}
