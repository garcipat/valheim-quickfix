using System;
using HarmonyLib;

namespace ValheimMoreSkills.Core
{
    /// <summary>
    /// The player's <c>Skills</c> component only knows the skills in its <c>m_skills</c> list, and <c>Skills.Load</c>
    /// drops every saved entry whose type is not in the <c>SkillType</c> enum. A Postfix on <c>Awake</c> (which runs
    /// before the character is loaded) adds a definition for each custom skill, and a Postfix on the private
    /// <c>IsSkillValid</c> accepts their types, so saved levels survive. <c>Save</c> already writes every skill.
    /// </summary>
    [HarmonyPatch(typeof(Skills), nameof(Skills.Awake))]
    internal static class SkillRegistrationPatch
    {
        private static void Postfix(Skills __instance)
        {
            try
            {
                foreach (var skill in SkillRegistry.Registered)
                {
                    var def = __instance.m_skills.Find(d => d.m_skill == skill.Type);
                    if (def == null)
                    {
                        def = new Skills.SkillDef
                        {
                            m_skill = skill.Type,
                            m_description = skill.Description,
                            m_increseStep = skill.IncreaseStep,
                        };
                        __instance.m_skills.Add(def);
                    }

                    if (def.m_icon == null)
                    {
                        def.m_icon = skill.Icon();
                    }
                }

                SkillWords.Add();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"SkillRegistration patch failed: {e}");
            }
        }
    }

    [HarmonyPatch(typeof(Skills), "IsSkillValid")]
    internal static class SkillValidPatch
    {
        private static void Postfix(Skills.SkillType type, ref bool __result)
        {
            try
            {
                if (!__result && SkillRegistry.Find(type) != null)
                {
                    __result = true;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"SkillValid patch failed: {e}");
            }
        }
    }

    /// <summary>
    /// English names for every language. <c>SetLanguage</c> rebuilds the word list, so the names are added again
    /// after it. <c>AddWord</c> is private in the live game, so it is called by name.
    /// </summary>
    [HarmonyPatch(typeof(Localization), nameof(Localization.SetLanguage))]
    internal static class SkillWords
    {
        private static readonly Action<Localization, string, string> AddWord =
            AccessTools.MethodDelegate<Action<Localization, string, string>>(
                AccessTools.Method(typeof(Localization), "AddWord"));

        private static void Postfix()
        {
            try
            {
                Add();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"SkillWords patch failed: {e}");
            }
        }

        internal static void Add()
        {
            foreach (var skill in SkillRegistry.Registered)
            {
                AddWord(Localization.instance, skill.NameKey, skill.Name);
            }
        }
    }
}
