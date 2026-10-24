using System;
using System.Collections.Generic;

namespace ValheimMoreSkills.Core
{
    /// <summary>Every <see cref="CustomSkill"/> the plugin registers at startup.</summary>
    internal static class SkillRegistry
    {
        private static readonly List<CustomSkill> All = new List<CustomSkill>();

        internal static IReadOnlyList<CustomSkill> Registered => All;

        internal static void Add(CustomSkill skill)
        {
            All.Add(skill);
        }

        internal static CustomSkill Find(Skills.SkillType type)
        {
            foreach (var skill in All)
            {
                if (skill.Type == type)
                {
                    return skill;
                }
            }

            return null;
        }

        /// <summary>By English name (any case) or by the number the game knows it as.</summary>
        internal static CustomSkill Find(string name)
        {
            foreach (var skill in All)
            {
                if (string.Equals(skill.Name, name, StringComparison.OrdinalIgnoreCase)
                    || ((int)skill.Type).ToString() == name)
                {
                    return skill;
                }
            }

            return null;
        }
    }
}
