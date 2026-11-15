using System;
using HarmonyLib;
using UnityEngine;

namespace ValheimMoreSkills.Core
{
    /// <summary>
    /// A levelling pace set in units of an activity (potions, food-minutes) rather than by the game's XP table: level N
    /// to N+1 takes <c>unitsPerLevel * (1 + growth * N)</c> units. Each unit raises the game's requirement for the
    /// current level divided by that count, one unit at a time, so a level-up in between uses the new level's rate.
    /// <c>Skill.Raise</c> grants at most one level per call and drops the rest, and a unit is always less than a level.
    /// </summary>
    internal static class SkillCurve
    {
        private static readonly Func<Skills, Skills.SkillType, Skills.Skill> GetSkill =
            AccessTools.MethodDelegate<Func<Skills, Skills.SkillType, Skills.Skill>>(
                AccessTools.Method(typeof(Skills), "GetSkill"));

        internal static void Grant(Player player, Skills.SkillType type, float units, float unitsPerLevel, float growth)
        {
            var skills = player.GetSkills();
            while (units > 0f)
            {
                var step = Mathf.Min(1f, units);
                units -= step;

                var level = Mathf.Floor(GetSkill(skills, type).m_level);
                if (level >= 100f)
                {
                    return;
                }

                // The game's XP for the next level; the same for every skill.
                var requirement = Mathf.Pow(level + 1f, 1.5f) * 0.5f + 0.5f;
                player.RaiseSkill(type, step * requirement / (unitsPerLevel * (1f + growth * level)));
            }
        }
    }
}
